using System;
using System.Collections.Generic;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.SuspicionGrade
{
    /// <summary>
    /// Event-driven composition adapter for the Suspicion Meter and room Grade.
    /// It owns only projections and boundary evidence; Perception, the FSM, and
    /// session state remain authoritative elsewhere.
    /// </summary>
    public sealed class SuspicionGradeSystem : ISuspicionGradeSystem,
        IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly SessionBoundaryService _sessionBoundary;
        private readonly SuspicionGradeConfiguration _configuration;
        private readonly ISuspicionMeterPresenter _presenter;
        private readonly IEventDiagnosticSink _diagnosticSink;
        private readonly Dictionary<SuspicionGradeIdentity, SuspicionMeterReadModel>
            _readModels = new Dictionary<SuspicionGradeIdentity, SuspicionMeterReadModel>();
        private readonly Dictionary<BoundaryKey, RoomGradeFinalized> _finalizedRooms =
            new Dictionary<BoundaryKey, RoomGradeFinalized>();
        private readonly HashSet<BoundaryKey> _publishedFinalResults =
            new HashSet<BoundaryKey>();
        private readonly Dictionary<BoundaryKey, float> _finalResultTimestamps =
            new Dictionary<BoundaryKey, float>();

        private GradeTraceReducer _traceReducer;
        private SubscriptionToken _snapshotToken;
        private SubscriptionToken _traceToken;
        private SubscriptionToken _boundaryEventToken;
        private SubscriptionToken _decisionToken;
        private SubscriptionToken _sessionBoundaryToken;
        private string _activeSessionId;
        private long _activeAttemptEpoch;
        private string _decisionRoomId;
        private string _decisionGuardEid;
        private float _lastObservedEventTimestamp;
        private bool _isBound;
        private bool _isDisposed;

        private readonly struct BoundaryKey : IEquatable<BoundaryKey>
        {
            public BoundaryKey(string sessionId, long attemptEpoch, string roomId)
            {
                SessionId = sessionId;
                AttemptEpoch = attemptEpoch;
                RoomId = roomId;
            }

            private readonly string SessionId;
            private readonly long AttemptEpoch;
            private readonly string RoomId;

            public bool Equals(BoundaryKey other)
            {
                return AttemptEpoch == other.AttemptEpoch
                    && string.Equals(SessionId, other.SessionId,
                        StringComparison.Ordinal)
                    && string.Equals(RoomId, other.RoomId,
                        StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is BoundaryKey && Equals((BoundaryKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StringComparer.Ordinal.GetHashCode(SessionId);
                    hash = hash * 31 + AttemptEpoch.GetHashCode();
                    return hash * 31 + StringComparer.Ordinal.GetHashCode(RoomId);
                }
            }
        }

        /// <summary>
        /// Creates and binds a session-scoped Suspicion Meter / Grade adapter.
        /// </summary>
        /// <param name="eventBus">The session-owned typed event transport.</param>
        /// <param name="sessionBoundary">The lifecycle barrier owner.</param>
        /// <param name="configuration">Registry-backed immutable tuning.</param>
        /// <param name="presenter">Optional read-model presenter.</param>
        /// <param name="diagnosticSink">Optional serialized diagnostic sink.</param>
        /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
        public SuspicionGradeSystem(IEventBus eventBus,
            SessionBoundaryService sessionBoundary,
            SuspicionGradeConfiguration configuration,
            ISuspicionMeterPresenter presenter = null,
            IEventDiagnosticSink diagnosticSink = null)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _sessionBoundary = sessionBoundary
                ?? throw new ArgumentNullException(nameof(sessionBoundary));
            _configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            _presenter = presenter;
            _diagnosticSink = diagnosticSink;
            _traceReducer = new GradeTraceReducer();
            _activeSessionId = sessionBoundary.SessionId;
            _activeAttemptEpoch = sessionBoundary.AttemptEpoch;
            Bind();
        }

        /// <summary>Gets the most recent stable system diagnostic code.</summary>
        public string LastDiagnostic { get; private set; } = "suspicion-grade-none";

        /// <summary>Gets the number of cached finalized room boundaries.</summary>
        public int FinalizedCount { get { return _finalizedRooms.Count; } }

        /// <summary>
        /// Gets the active session identity used to reject stale deliveries.
        /// </summary>
        public string ActiveSessionId { get { return _activeSessionId; } }

        /// <summary>
        /// Gets the active attempt epoch used to reject stale deliveries.
        /// </summary>
        public long ActiveAttemptEpoch { get { return _activeAttemptEpoch; } }

        /// <summary>
        /// Binds explicit room and guard identity for legacy DecisionRecord events.
        /// The binding is supplied by the composition root; it is never inferred
        /// from unrelated DecisionRecord fields.
        /// </summary>
        /// <param name="roomId">Explicit room identity.</param>
        /// <param name="guardEid">Explicit stable guard identity.</param>
        /// <remarks>
        /// Single-guard MVP limitation: exactly one guard may be bound. The legacy
        /// R12 decision records do not carry guard identity, so decisions from a
        /// second guard in the same room would be misattributed to the bound
        /// guard. Multi-guard attribution is excluded from the MVP by the
        /// acceleration plan.
        /// </remarks>
        public void BindDecisionContext(string roomId, string guardEid)
        {
            _decisionRoomId = roomId ?? string.Empty;
            _decisionGuardEid = guardEid ?? string.Empty;
            LastDiagnostic = IsValidDecisionBinding()
                ? "suspicion-grade-decision-binding-ready"
                : "suspicion-grade-decision-binding-unavailable";
        }

        /// <summary>
        /// Binds the typed event and session lifecycle subscriptions. Rebinding is
        /// idempotent and does not reset valid projection state.
        /// </summary>
        /// <remarks>
        /// While unbound, this adapter cannot receive session or epoch boundary
        /// notifications. Rebinding therefore resynchronizes the tracked active
        /// identity to the boundary service's current identity when the two
        /// differ (missed-boundary recovery). A rebinding within an unchanged
        /// generation performs no reset, so valid projection state survives an
        /// ordinary disable/enable cycle.
        /// </remarks>
        public void Bind()
        {
            if (_isDisposed || _isBound) return;

            _snapshotToken = _eventBus.Subscribe<SuspicionSnapshotEvent>(
                OnSnapshotEvent);
            _traceToken = _eventBus.Subscribe<GradeTraceEvent>(OnTraceEvent);
            _boundaryEventToken = _eventBus.Subscribe<RoomCompletionBoundaryEvent>(
                OnRoomBoundaryEvent);
            _decisionToken = _eventBus.Subscribe<DecisionRecord>(OnDecisionRecord);
            _sessionBoundaryToken = _sessionBoundary.Subscribe(OnSessionBoundary);
            _isBound = true;

            if (!string.Equals(_activeSessionId, _sessionBoundary.SessionId,
                    StringComparison.Ordinal)
                || _activeAttemptEpoch != _sessionBoundary.AttemptEpoch)
            {
                ResetForBoundary(_sessionBoundary.SessionId,
                    _sessionBoundary.AttemptEpoch);
            }
        }

        /// <summary>
        /// Unbinds every event and lifecycle token owned by this adapter.
        /// </summary>
        public void Unbind()
        {
            if (!_isBound) return;

            _eventBus.Unsubscribe(_snapshotToken);
            _eventBus.Unsubscribe(_traceToken);
            _eventBus.Unsubscribe(_boundaryEventToken);
            _eventBus.Unsubscribe(_decisionToken);
            _sessionBoundary.Unsubscribe(_sessionBoundaryToken);
            _isBound = false;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_isDisposed) return;
            Unbind();
            _isDisposed = true;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Rejections observed through this direct API are not recorded to the
        /// diagnostic sink because no transport envelope exists. Event-driven
        /// snapshot deliveries record their rejections through the originating
        /// event envelope.
        /// </remarks>
        public void AcceptSnapshot(GuardSuspicionSnapshot snapshot)
        {
            AcceptSnapshotCore(snapshot, null);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Rejections observed through this direct API are not recorded to the
        /// diagnostic sink because no transport envelope exists. Event-driven
        /// trace deliveries record every rejection through the originating
        /// event envelope.
        /// </remarks>
        public void AcceptTrace(GradeTraceRecord traceRecord)
        {
            AcceptTraceCore(traceRecord, null);
        }

        /// <summary>
        /// Finalizes one room boundary idempotently.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A boundary is finalized at most once per session, epoch, and room:
        /// repeated finalization returns the same immutable result object and
        /// publishes exactly one <see cref="RoomGradeFinalizedEvent" />.
        /// </para>
        /// <para>
        /// Publication is retry-safe. When the transport answers a publication
        /// with <c>Retry</c> or <c>Rejected</c> (a full queue retains nothing),
        /// the boundary is not marked published and a later duplicate
        /// finalization re-attempts delivery of the same cached result,
        /// surfacing the
        /// <c>suspicion-grade-final-result-publication-recovered</c>
        /// diagnostic on success.
        /// </para>
        /// </remarks>
        /// <param name="boundary">Session-owned room completion boundary.</param>
        /// <returns>The immutable final result for this boundary.</returns>
        public RoomGradeFinalized FinalizeRoom(RoomCompletionBoundary boundary)
        {
            return FinalizeRoomCore(boundary, null);
        }

        private RoomGradeFinalized FinalizeRoomCore(
            RoomCompletionBoundary boundary, IEvent source)
        {
            if (!IsValidBoundaryIdentity(boundary)
                || !IsCurrentBoundary(boundary))
            {
                LastDiagnostic = boundary == null
                    ? "suspicion-grade-null-boundary"
                    : (IsValidBoundaryIdentity(boundary)
                        ? "suspicion-grade-stale-boundary"
                        : "suspicion-grade-invalid-boundary");
                RecordDiagnostic(LastDiagnostic, source);
                return RoomGradeOperator.FinalizeRoom(boundary, null,
                    _configuration);
            }

            var key = new BoundaryKey(boundary.SessionId,
                boundary.AttemptEpoch, boundary.RoomId);
            RoomGradeFinalized cached;
            if (_finalizedRooms.TryGetValue(key, out cached))
            {
                if (_publishedFinalResults.Contains(key))
                {
                    LastDiagnostic = "suspicion-grade-finalization-duplicate";
                    return cached;
                }

                // The earlier publication was Retry'd or Rejected by the
                // transport, which retains neither payload. Re-attempt the
                // delivery of the same cached result instead of silently
                // dropping it forever. PublishFinalResult reuses the timestamp
                // selected by the first attempt for this boundary.
                PublishFinalResult(key, cached, true);
                return cached;
            }

            GradeTraceAggregate aggregate = _traceReducer.Aggregate(
                boundary.SessionId, boundary.AttemptEpoch, boundary.RoomId);
            RoomGradeFinalized result = RoomGradeOperator.FinalizeRoom(
                boundary, aggregate, _configuration);
            _finalizedRooms.Add(key, result);
            LastDiagnostic = "suspicion-grade-finalized";
            PublishFinalResult(key, result, false);
            return result;
        }

        /// <inheritdoc />
        /// <remarks>
        /// <para>
        /// A valid reset replaces the trace reducer and clears read models, the
        /// finalization cache, and the final-result publication tracking.
        /// Invalid identity arguments are rejected without clearing any state.
        /// </para>
        /// <para>
        /// Same-generation contract: resetting within the SAME bus generation
        /// (same session and epoch) does not clear the transport's admission
        /// set. A re-finalization of the same room recomputes a fresh result
        /// object, but the transport reports the stable room identity as a
        /// duplicate and never delivers that fresh object; the silent
        /// suppression is surfaced as the
        /// <c>suspicion-grade-final-result-publication-duplicate-suppressed</c>
        /// diagnostic and sink record. Advancing the session or epoch through
        /// <see cref="SessionBoundaryService" /> clears the transport
        /// generation and restores normal publication.
        /// </para>
        /// </remarks>
        public void ResetForBoundary(string sessionId, long attemptEpoch)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || attemptEpoch < 0)
            {
                LastDiagnostic = "suspicion-grade-invalid-boundary-reset";
                return;
            }

            _activeSessionId = sessionId;
            _activeAttemptEpoch = attemptEpoch;
            _traceReducer = new GradeTraceReducer();
            _readModels.Clear();
            _finalizedRooms.Clear();
            _publishedFinalResults.Clear();
            _finalResultTimestamps.Clear();
            _lastObservedEventTimestamp = 0f;
            LastDiagnostic = "suspicion-grade-reset";
        }

        /// <summary>
        /// Attempts to read the latest projection for one exact identity.
        /// </summary>
        /// <param name="identity">Projection identity to query.</param>
        /// <param name="readModel">Latest read model when available.</param>
        /// <returns>True when a projection has been accepted for the identity.</returns>
        public bool TryGetReadModel(SuspicionGradeIdentity identity,
            out SuspicionMeterReadModel readModel)
        {
            return _readModels.TryGetValue(identity, out readModel);
        }

        private void AcceptSnapshotCore(GuardSuspicionSnapshot snapshot,
            IEvent source)
        {
            if (!IsValidSnapshotIdentity(snapshot))
            {
                LastDiagnostic = "suspicion-grade-invalid-snapshot";
                RecordDiagnostic(LastDiagnostic, source);
                return;
            }

            if (!IsCurrentIdentity(snapshot.Identity))
            {
                LastDiagnostic = "suspicion-grade-stale-snapshot";
                RecordDiagnostic(LastDiagnostic, source);
                return;
            }

            SuspicionMeterReadModel readModel = SuspicionMeterCalculator.Calculate(
                snapshot, _configuration);
            _readModels[snapshot.Identity] = readModel;
            if (HasTChaseDivergence(snapshot))
            {
                // The meter stays snapshot-authoritative; an authoritative
                // snapshot Chase threshold that differs from the registry-backed
                // configuration is surfaced instead of silently projecting one
                // source under the other's tuning. The comparison is exact:
                // both sides are authoritative float values, so only true
                // divergence counts, even when another required input makes the
                // resulting projection unavailable.
                LastDiagnostic = "suspicion-grade-tchase-divergence";
            }
            else
            {
                LastDiagnostic = readModel.IsAvailable
                    ? "suspicion-grade-snapshot-accepted"
                    : "suspicion-grade-snapshot-unavailable";
            }

            if (_presenter == null) return;

            try
            {
                _presenter.Present(readModel);
            }
            catch (Exception)
            {
                LastDiagnostic = "suspicion-grade-presenter-failed";
            }
        }

        private void AcceptTraceCore(GradeTraceRecord traceRecord, IEvent source)
        {
            if (traceRecord != null && IsValidTraceIdentity(traceRecord)
                && !IsCurrentTrace(traceRecord))
            {
                LastDiagnostic = ClassifyTraceGeneration(traceRecord);
                RecordDiagnostic(LastDiagnostic, source);
                return;
            }

            bool accepted = _traceReducer.Accept(traceRecord);
            LastDiagnostic = _traceReducer.LastDiagnostic;
            if (!accepted)
                RecordDiagnostic(LastDiagnostic, source);
        }

        private void OnSnapshotEvent(SuspicionSnapshotEvent evt)
        {
            if (evt == null || string.IsNullOrWhiteSpace(evt.EventId))
            {
                LastDiagnostic = "suspicion-grade-invalid-snapshot-event";
                RecordDiagnostic(LastDiagnostic, evt);
                return;
            }

            _lastObservedEventTimestamp = evt.Timestamp;
            AcceptSnapshotCore(evt.Snapshot, evt);
        }

        private void OnTraceEvent(GradeTraceEvent evt)
        {
            if (evt == null)
            {
                LastDiagnostic = "suspicion-grade-null-trace-event";
                return;
            }

            _lastObservedEventTimestamp = evt.Timestamp;
            AcceptTraceCore(evt.TraceRecord, evt);
        }

        private void OnRoomBoundaryEvent(RoomCompletionBoundaryEvent evt)
        {
            if (evt == null)
            {
                LastDiagnostic = "suspicion-grade-null-room-boundary-event";
                return;
            }

            _lastObservedEventTimestamp = evt.Timestamp;
            FinalizeRoomCore(evt.Boundary, evt);
        }

        private void OnDecisionRecord(DecisionRecord decisionRecord)
        {
            if (decisionRecord != null)
                _lastObservedEventTimestamp = decisionRecord.Timestamp;

            if (!IsValidDecisionBinding())
            {
                LastDiagnostic = "suspicion-grade-decision-binding-unavailable";
                RecordDiagnostic(LastDiagnostic, decisionRecord);
                return;
            }

            if (IsKnownNonIncidentDecision(decisionRecord))
            {
                // InvestigateCommit and ChaseEnd are normal high-frequency FSM
                // records that never contribute grade incidents. Ignoring them
                // here must not flood rejection diagnostics or the sink.
                LastDiagnostic = "suspicion-grade-decision-non-incident-ignored";
                return;
            }

            GradeTraceRecord adapted;
            if (!GradeTraceRecord.TryAdaptDecisionRecord(decisionRecord,
                _decisionRoomId, _decisionGuardEid, out adapted))
            {
                LastDiagnostic = "suspicion-grade-decision-adaptation-rejected";
                RecordDiagnostic(LastDiagnostic, decisionRecord);
                return;
            }

            AcceptTraceCore(adapted, decisionRecord);
        }

        private void OnSessionBoundary(string activeSessionId, long activeEpoch)
        {
            ResetForBoundary(activeSessionId, activeEpoch);
        }

        private void PublishFinalResult(BoundaryKey key, RoomGradeFinalized result,
            bool isRecovery)
        {
            // Select the timestamp once, before the first transport attempt.
            // Retry and rejection paths must preserve that original ordering
            // metadata even if later events update the observed timestamp.
            float timestamp;
            if (!_finalResultTimestamps.TryGetValue(key, out timestamp))
            {
                timestamp = _lastObservedEventTimestamp;
                _finalResultTimestamps.Add(key, timestamp);
            }

            // For a boundary-event-driven finalization, the observed timestamp
            // was set from that boundary event before this method was called.
            // Direct finalization intentionally uses the current observed value
            // (0f when no event has been observed); no clock is read.
            var evt = new RoomGradeFinalizedEvent(result, timestamp);
            EventPublishResult publication = _eventBus.Publish(evt);
            if (publication.Admission == EventAdmission.Accepted)
            {
                _publishedFinalResults.Add(key);
                if (isRecovery)
                    LastDiagnostic =
                        "suspicion-grade-final-result-publication-recovered";
                return;
            }

            if (publication.Admission == EventAdmission.Duplicate)
            {
                // The transport still holds this identity from the current bus
                // generation (a same-generation ResetForBoundary cleared the
                // local cache but not the bus admission set), so this fresh
                // result object can never be delivered under the same identity.
                // Surface the suppression instead of silently recomputing an
                // undeliverable result, and mark the boundary published so
                // later duplicate finalizations do not flood the sink with the
                // same suppression.
                _publishedFinalResults.Add(key);
                LastDiagnostic =
                    "suspicion-grade-final-result-publication-duplicate-suppressed";
                RecordDiagnostic(LastDiagnostic, evt);
                return;
            }

            // Retry and Rejected payloads are not retained by the transport.
            // The boundary stays unpublished so a later duplicate finalization
            // re-attempts delivery. Keep the adapter-level codes stable even
            // when the transport's detailed reason changes.
            LastDiagnostic = publication.Admission == EventAdmission.Retry
                ? "suspicion-grade-final-result-publication-retry"
                : "suspicion-grade-final-result-publication-rejected";
            RecordDiagnostic(LastDiagnostic, evt);
        }

        private string ClassifyTraceGeneration(GradeTraceRecord record)
        {
            if (!string.Equals(record.SessionId, _activeSessionId,
                    StringComparison.Ordinal)
                || record.AttemptEpoch < _activeAttemptEpoch)
                return "suspicion-grade-stale-trace";
            return "suspicion-grade-future-trace";
        }

        private bool IsCurrentIdentity(SuspicionGradeIdentity identity)
        {
            return IsValidIdentity(identity.SessionId, identity.AttemptEpoch,
                    identity.RoomId, identity.GuardEid)
                && string.Equals(identity.SessionId, _activeSessionId,
                    StringComparison.Ordinal)
                && identity.AttemptEpoch == _activeAttemptEpoch;
        }

        private bool IsCurrentTrace(GradeTraceRecord record)
        {
            return string.Equals(record.SessionId, _activeSessionId,
                    StringComparison.Ordinal)
                && record.AttemptEpoch == _activeAttemptEpoch;
        }

        private bool IsCurrentBoundary(RoomCompletionBoundary boundary)
        {
            return string.Equals(boundary.SessionId, _activeSessionId,
                    StringComparison.Ordinal)
                && boundary.AttemptEpoch == _activeAttemptEpoch;
        }

        private bool IsValidDecisionBinding()
        {
            return !string.IsNullOrWhiteSpace(_decisionRoomId)
                && !string.IsNullOrWhiteSpace(_decisionGuardEid);
        }

        private bool HasTChaseDivergence(GuardSuspicionSnapshot snapshot)
        {
            return snapshot.HasChaseThreshold
                && snapshot.TChase > 0f
                && !float.IsNaN(snapshot.TChase)
                && !float.IsInfinity(snapshot.TChase)
                && snapshot.TChase != _configuration.TChase;
        }

        private static bool IsKnownNonIncidentDecision(DecisionRecord decisionRecord)
        {
            return decisionRecord is InvestigateCommit
                || decisionRecord is ChaseEnd;
        }

        private static bool IsValidSnapshotIdentity(
            GuardSuspicionSnapshot snapshot)
        {
            return IsValidIdentity(snapshot.Identity.SessionId,
                snapshot.Identity.AttemptEpoch, snapshot.Identity.RoomId,
                snapshot.Identity.GuardEid);
        }

        private static bool IsValidTraceIdentity(GradeTraceRecord record)
        {
            return !string.IsNullOrWhiteSpace(record.SessionId)
                && record.AttemptEpoch >= 0
                && !string.IsNullOrWhiteSpace(record.RoomId)
                && !string.IsNullOrWhiteSpace(record.GuardEid)
                && !string.IsNullOrWhiteSpace(record.EntryId)
                && !string.IsNullOrWhiteSpace(record.EventId);
        }

        private static bool IsValidBoundaryIdentity(RoomCompletionBoundary boundary)
        {
            return boundary != null
                && IsValidIdentity(boundary.SessionId, boundary.AttemptEpoch,
                    boundary.RoomId, "bound");
        }

        private static bool IsValidIdentity(string sessionId, long attemptEpoch,
            string roomId, string guardEid)
        {
            return !string.IsNullOrWhiteSpace(sessionId)
                && attemptEpoch >= 0
                && !string.IsNullOrWhiteSpace(roomId)
                && !string.IsNullOrWhiteSpace(guardEid);
        }

        private void RecordDiagnostic(string code, IEvent source)
        {
            if (_diagnosticSink == null || source == null) return;

            try
            {
                var envelope = new EventEnvelope(source.SessionId,
                    source.AttemptEpoch, source.Timestamp, source.Publisher,
                    source.Identity,
                    source is IEventPhase ? ((IEventPhase)source).Phase
                        : EventBusPhase.GameplayIngress,
                    source);
                _diagnosticSink.Record(code, envelope, 0, 0, 0);
            }
            catch (Exception)
            {
                // Diagnostics cannot become a gameplay failure path.
            }
        }
    }
}
