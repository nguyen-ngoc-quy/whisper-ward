using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using WhisperWard.AI.Core;

namespace WhisperWard.AI.Perception
{
    /// <summary>Publication state for one staged source event.</summary>
    public enum NoisePublicationStatus
    {
        Unknown,
        Pending,
        Published,
        Rejected,
        Invalidated
    }

    /// <summary>
    /// Source admission and raw-fact publication boundary. Producers submit
    /// immutable records; this class orders them before allocating Noise fact IDs.
    /// Movement ledgers and Burst simulation remain owned by their source systems.
    /// </summary>
    public sealed class NoiseEmitter
    {
        private sealed class PendingSource
        {
            public NoiseSourceRecord Record;
            public ulong FactId;
            public int RetryCount;
        }

        private readonly IEventBus _eventBus;
        private readonly HashSet<string> _sourceEvents =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<PendingSource> _pending = new List<PendingSource>();
        private readonly Dictionary<string, EventPublishResult> _lastResults =
            new Dictionary<string, EventPublishResult>(StringComparer.Ordinal);
        private readonly Dictionary<string, ulong> _factIdsBySource =
            new Dictionary<string, ulong>(StringComparer.Ordinal);
        private readonly HashSet<string> _invalidatedSources =
            new HashSet<string>(StringComparer.Ordinal);
        private string _factSessionId;
        private readonly float _walkRadiusMeters;
        private readonly float _runRadiusMeters;
        private bool _hasCommittedMovementOrder;
        private float _lastMovementTimestamp;
        private int _lastMovementRank;
        private string _lastMovementEventId;
        private ulong _nextFactId = 1;

        /// <summary>Creates an emitter bound to one session-scoped event bus.</summary>
        public NoiseEmitter(IEventBus eventBus)
            : this(eventBus, NoiseRuntimeConfiguration.RegisteredWalkNoiseRadiusMeters,
                NoiseRuntimeConfiguration.RegisteredRunNoiseRadiusMeters)
        {
        }

        /// <summary>
        /// Creates an emitter with the validated registry movement-radius mapping.
        /// </summary>
        public NoiseEmitter(IEventBus eventBus, float walkRadiusMeters,
            float runRadiusMeters)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            if (!IsFinitePositive(walkRadiusMeters)
                || !IsFinitePositive(runRadiusMeters)
                || walkRadiusMeters >= runRadiusMeters)
                throw new ArgumentException("noise-emitter-radius-map");
            _walkRadiusMeters = walkRadiusMeters;
            _runRadiusMeters = runRadiusMeters;
            _factSessionId = _eventBus.SessionId;
        }

        /// <summary>Active session envelope supplied by the lifecycle-owned bus.</summary>
        public string EventBusSessionId { get { return _eventBus.SessionId; } }
        public long EventBusAttemptEpoch { get { return _eventBus.AttemptEpoch; } }

        /// <summary>
        /// Stages a source event for deterministic publication. The source ID is
        /// deduplicated within the active session and attempt epoch.
        /// </summary>
        public EventPublishResult Submit(NoiseSourceRecord record)
        {
            if (record == null)
                return EventPublishResult.Rejected("noise-emitter-invalid-source-record");
            if (record.SourceKind == NoiseSourceKind.Legacy)
                return EventPublishResult.Rejected(
                    "noise-emitter-authoritative-source-kind-required");

            return SubmitAcceptedSource(record);
        }

        private EventPublishResult SubmitAcceptedSource(NoiseSourceRecord record)
        {
            if (record.SourceKind == NoiseSourceKind.Movement
                && (!string.Equals(record.StrideTargetState, "Walk",
                        StringComparison.Ordinal)
                    && !string.Equals(record.StrideTargetState, "Run",
                        StringComparison.Ordinal)))
            {
                return EventPublishResult.Rejected(
                    "noise-emitter-movement-mode-required");
            }
            if (record.SourceKind == NoiseSourceKind.Movement)
            {
                float expectedRadius = string.Equals(record.StrideTargetState,
                    "Walk", StringComparison.Ordinal)
                    ? _walkRadiusMeters : _runRadiusMeters;
                if (Math.Abs(record.Radius - expectedRadius) > 0.000001f)
                    return EventPublishResult.Rejected(
                        "noise-emitter-movement-radius-not-registered");
            }
            if ((record.SourceKind == NoiseSourceKind.Movement
                    || record.SourceKind == NoiseSourceKind.Burst)
                && (!string.Equals(record.SessionId, _eventBus.SessionId,
                        StringComparison.Ordinal)
                    || record.AttemptEpoch != _eventBus.AttemptEpoch))
            {
                return EventPublishResult.Rejected(
                    "noise-emitter-stale-source-envelope");
            }

            string key = BuildSourceKey(record.SourceKind, record.SourceEventId);
            if (!_sourceEvents.Add(key))
            {
                PendingSource pending = FindPending(key);
                if (pending != null)
                    return EventPublishResult.Retry(
                        "noise-emitter-source-pending", pending.RetryCount);

                EventPublishResult prior;
                if (_lastResults.TryGetValue(key, out prior)
                    && prior.Admission == EventAdmission.Retry)
                    return prior;
                return EventPublishResult.Duplicate(
                    "noise-emitter-duplicate-source-event");
            }

            _pending.Add(new PendingSource { Record = record });
            return EventPublishResult.Accepted();
        }

        /// <summary>
        /// Stages one complete controller-owned Movement stride commit with the
        /// producer's immutable session envelope.
        /// </summary>
        public EventPublishResult SubmitMovement(string sessionId,
            long attemptEpoch, string stepId, string publisher,
            string sourceState, string strideTargetState,
            Vector3 previousPosition, Vector3 feetPosition,
            float strideLengthMeters, float ledgerRemainderMeters,
            float radius, float sourceTimestamp)
        {
            try
            {
                return Submit(NoiseSourceRecord.Movement(sessionId, attemptEpoch,
                    stepId, publisher, sourceState, strideTargetState,
                    previousPosition, feetPosition, strideLengthMeters,
                    ledgerRemainderMeters, radius, sourceTimestamp));
            }
            catch (ArgumentException)
            {
                return EventPublishResult.Rejected(
                    "noise-emitter-invalid-movement-source");
            }
        }

        /// <summary>
        /// Compatibility adapter retained for callers that have not migrated to
        /// the session-aware Movement boundary. It fails closed rather than
        /// relabeling an unbound source as belonging to the current generation.
        /// </summary>
        [Obsolete("Use the session-aware Movement overload.")]
        public EventPublishResult SubmitMovement(string stepId, string publisher,
            string sourceState, string strideTargetState, Vector3 previousPosition,
            Vector3 feetPosition, float strideLengthMeters,
            float ledgerRemainderMeters, float radius, float sourceTimestamp)
        {
            return EventPublishResult.Rejected(
                "noise-emitter-movement-envelope-required");
        }

        /// <summary>
        /// Stages one authoritative Burst landing contact using a numeric handle
        /// and the producer's immutable session envelope.
        /// </summary>
        public EventPublishResult SubmitBurst(string sessionId, long attemptEpoch,
            ulong flightHandleId, string publisher, Vector3 landingContact,
            float radius, float sourceTimestamp, float terminalPublicationTime,
            string provenance = "burst-landing")
        {
            try
            {
                return Submit(NoiseSourceRecord.Burst(sessionId, attemptEpoch,
                    flightHandleId, publisher, landingContact, radius,
                    sourceTimestamp, terminalPublicationTime, provenance));
            }
            catch (ArgumentException)
            {
                return EventPublishResult.Rejected(
                    "noise-emitter-invalid-burst-source");
            }
        }

        /// <summary>Compatibility overload using source time as terminal time.</summary>
        [Obsolete("Use the terminal-time Burst overload.")]
        public EventPublishResult SubmitBurst(string sessionId, long attemptEpoch,
            ulong flightHandleId, string publisher, Vector3 landingContact,
            float radius, float sourceTimestamp,
            string provenance = "burst-landing")
        {
            return SubmitBurst(sessionId, attemptEpoch, flightHandleId, publisher,
                landingContact, radius, sourceTimestamp, sourceTimestamp, provenance);
        }

        /// <summary>
        /// Compatibility adapter accepting only canonical decimal handles. It
        /// cannot relabel an arbitrary legacy string as an authoritative identity.
        /// </summary>
        [Obsolete("Use the envelope-aware numeric Burst overload.")]
        public EventPublishResult SubmitBurst(string flightHandleId,
            string publisher, Vector3 landingContact, float radius,
            float sourceTimestamp, string provenance = "burst-landing")
        {
            ulong handle;
            if (!ulong.TryParse(flightHandleId, NumberStyles.None,
                CultureInfo.InvariantCulture, out handle) || handle == 0)
            {
                return EventPublishResult.Rejected(
                    "noise-emitter-burst-handle-must-be-canonical-ulong");
            }
            return SubmitBurst(_eventBus.SessionId, _eventBus.AttemptEpoch,
                handle, publisher, landingContact, radius, sourceTimestamp,
                provenance);
        }

        /// <summary>
        /// Flushes staged sources in the canonical source timestamp, rank, and ID
        /// order. A full bus retains the source and its uncommitted fact-ID
        /// candidate for a later retry; only accepted publication commits the ID.
        /// </summary>
        public int Flush()
        {
            if (_pending.Count == 0) return 0;
            _pending.Sort(ComparePendingSources);

            int published = 0;
            for (int i = 0; i < _pending.Count;)
            {
                PendingSource pending = _pending[i];
                NoiseSourceRecord record = pending.Record;
                EventPublishResult result;
                if (record.SourceKind == NoiseSourceKind.Movement
                    && _hasCommittedMovementOrder
                    && CompareSourceOrder(record, _lastMovementTimestamp,
                        _lastMovementRank, _lastMovementEventId) < 0)
                {
                    result = EventPublishResult.Rejected(
                        "noise-emitter-late-source-order");
                }
                else
                {
                    if (pending.FactId == 0)
                    {
                        if (_nextFactId == 0)
                        {
                            result = EventPublishResult.Rejected(
                                "noise-emitter-fact-id-exhausted");
                            goto publication_result;
                        }
                        // Reserve the next candidate without consuming it. The
                        // cursor advances only after the bus accepts the raw fact.
                        pending.FactId = _nextFactId;
                    }

                    try
                    {
                        result = _eventBus.Publish(new NoisePublished(
                        _eventBus.SessionId,
                        _eventBus.AttemptEpoch,
                        pending.FactId,
                        record.SourceEventId,
                        record.Kind,
                        record.Source,
                        record.Origin,
                        record.Radius,
                        record.SourceTimestamp,
                        record.SourceEventClassRank,
                        record.Origin,
                        record.Provenance,
                        record.PreviousPosition,
                        record.SourceState,
                        record.StrideTargetState,
                        record.StrideLengthMeters,
                        record.LedgerRemainderMeters,
                        record.TerminalPublicationTime,
                        record.BurstFlightHandleId,
                        record.StrideTargetState,
                        "published"));
                }
                    catch (ArgumentException)
                    {
                        result = EventPublishResult.Rejected(
                            "noise-emitter-invalid-source-record");
                    }
                }

            publication_result:
                string sourceKey = BuildSourceKey(record.SourceKind, record.SourceEventId);
                _lastResults[sourceKey] = result;
                if (result.Admission == EventAdmission.Accepted
                    || result.Admission == EventAdmission.Duplicate)
                {
                    _factIdsBySource[sourceKey] = pending.FactId;
                    if (pending.FactId == _nextFactId)
                        _nextFactId = _nextFactId == ulong.MaxValue
                            ? 0 : _nextFactId + 1;
                }
                if (result.Admission == EventAdmission.Retry)
                {
                    pending.RetryCount = Math.Max(pending.RetryCount + 1,
                        result.RetryCount);
                    // Sources after a retry must not overtake it. Keeping the
                    // cursor at the oldest blocked source preserves the
                    // canonical source tuple on every later flush.
                    break;
                }

                _pending.RemoveAt(i);
                if (record.SourceKind == NoiseSourceKind.Movement
                    && (result.Admission == EventAdmission.Accepted
                        || result.Admission == EventAdmission.Duplicate))
                {
                    _hasCommittedMovementOrder = true;
                    _lastMovementTimestamp = record.SourceTimestamp;
                    _lastMovementRank = record.SourceEventClassRank;
                    _lastMovementEventId = record.SourceEventId;
                }
                if (result.Admission == EventAdmission.Accepted)
                    published++;
            }
            return published;
        }

        /// <summary>
        /// Publishes one legacy source immediately. New gameplay producers should
        /// call Submit followed by Flush so equal-timestamp ordering is preserved.
        /// </summary>
        public EventPublishResult Publish(string sourceEventId, string kind,
            string source, Vector3 position, float radius, float timestamp)
        {
            if (string.IsNullOrWhiteSpace(sourceEventId))
                return EventPublishResult.Rejected("noise-emitter-invalid-source-event");
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(source))
                return EventPublishResult.Rejected("noise-emitter-invalid-kind-or-source");
            if (!IsFinite(position))
                return EventPublishResult.Rejected("noise-emitter-invalid-origin");
            if (!IsFinitePositive(radius))
                return EventPublishResult.Rejected("noise-emitter-invalid-radius");
            if (!IsFinite(timestamp))
                return EventPublishResult.Rejected("noise-emitter-invalid-timestamp");

            // The compatibility overload is deliberately routed through the
            // authoritative admission boundary. Legacy source kinds fail closed
            // instead of bypassing source-kind validation and fact ordering.
            EventPublishResult admission = Submit(
                NoiseSourceRecord.Legacy(sourceEventId, kind, source, position,
                    radius, timestamp));
            if (admission.Admission == EventAdmission.Retry)
            {
                Flush();
                EventPublishResult retryResult;
                if (_lastResults.TryGetValue(BuildSourceKey(NoiseSourceKind.Legacy, sourceEventId),
                    out retryResult))
                    return retryResult;
                return admission;
            }
            if (admission.Admission != EventAdmission.Accepted)
                return admission;

            Flush();
            EventPublishResult flushed;
            if (_lastResults.TryGetValue(BuildSourceKey(NoiseSourceKind.Legacy, sourceEventId),
                out flushed))
            {
                return flushed;
            }
            return EventPublishResult.Retry("noise-emitter-event-bus-retry", 0);
        }

        /// <summary>
        /// Returns the fact identity assigned when a source was committed to the
        /// Event Bus. A submitted-but-not-yet-flushed source has no fact identity.
        /// </summary>
        public bool TryGetFactId(string sourceEventId, out ulong factId)
        {
            return TryGetFactId(NoiseSourceKind.Legacy, sourceEventId, out factId);
        }

        /// <summary>Returns the committed fact identity for a typed source.</summary>
        public bool TryGetFactId(NoiseSourceKind sourceKind, string sourceEventId,
            out ulong factId)
        {
            factId = 0;
            if (string.IsNullOrWhiteSpace(sourceEventId)) return false;
            return _factIdsBySource.TryGetValue(BuildSourceKey(sourceKind, sourceEventId),
                out factId) && factId != 0;
        }

        /// <summary>
        /// Distinguishes a source that is awaiting flush from one that was
        /// published, rejected, invalidated, or never known to this emitter.
        /// </summary>
        public bool TryGetPublication(string sourceEventId,
            out NoisePublicationStatus status, out ulong? factId,
            out EventPublishResult result)
        {
            return TryGetPublication(NoiseSourceKind.Legacy, sourceEventId,
                out status, out factId, out result);
        }

        /// <summary>Returns publication state for a typed source identity.</summary>
        public bool TryGetPublication(NoiseSourceKind sourceKind,
            string sourceEventId, out NoisePublicationStatus status,
            out ulong? factId, out EventPublishResult result)
        {
            status = NoisePublicationStatus.Unknown;
            factId = null;
            result = default(EventPublishResult);
            if (string.IsNullOrWhiteSpace(sourceEventId)) return false;

            string key = BuildSourceKey(sourceKind, sourceEventId);
            ulong committedFactId;
            if (_factIdsBySource.TryGetValue(key, out committedFactId)
                && committedFactId != 0)
            {
                status = NoisePublicationStatus.Published;
                factId = committedFactId;
                _lastResults.TryGetValue(key, out result);
                return true;
            }

            if (FindPending(key) != null)
            {
                status = NoisePublicationStatus.Pending;
                _lastResults.TryGetValue(key, out result);
                return true;
            }

            if (_invalidatedSources.Contains(key))
            {
                status = NoisePublicationStatus.Invalidated;
                return true;
            }

            if (_lastResults.TryGetValue(key, out result))
            {
                status = result.Admission == EventAdmission.Rejected
                    ? NoisePublicationStatus.Rejected
                    : NoisePublicationStatus.Pending;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears staged source state and fact allocation at a lifecycle-owned
        /// session boundary. Epoch transitions may call this only when the
        /// canonical lifecycle owner has also invalidated the bus generation.
        /// </summary>
        public void ResetForBoundary()
        {
            ResetForBoundary(_eventBus.SessionId);
        }

        /// <summary>
        /// Clears epoch-local source work. Fact IDs remain monotonic for the active
        /// session and restart only when the lifecycle owner starts a new session.
        /// </summary>
        public void ResetForBoundary(string activeSessionId)
        {
            for (int i = 0; i < _pending.Count; i++)
                _invalidatedSources.Add(BuildSourceKey(
                    _pending[i].Record.SourceKind,
                    _pending[i].Record.SourceEventId));

            bool newSession = !string.Equals(_factSessionId, activeSessionId,
                StringComparison.Ordinal);
            _sourceEvents.Clear();
            _pending.Clear();
            _lastResults.Clear();
            _factIdsBySource.Clear();
            if (newSession)
                _invalidatedSources.Clear();
            _hasCommittedMovementOrder = false;
            _lastMovementTimestamp = 0f;
            _lastMovementRank = 0;
            _lastMovementEventId = string.Empty;
            if (!string.Equals(_factSessionId, activeSessionId,
                StringComparison.Ordinal))
            {
                _factSessionId = activeSessionId ?? string.Empty;
                _nextFactId = 1;
            }
        }

        private PendingSource FindPending(string sourceKey)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (string.Equals(BuildSourceKey(_pending[i].Record.SourceKind, _pending[i].Record.SourceEventId),
                    sourceKey, StringComparison.Ordinal))
                    return _pending[i];
            }
            return null;
        }

        private string BuildSourceKey(NoiseSourceKind sourceKind,
            string sourceEventId)
        {
            string prefix = sourceKind == NoiseSourceKind.Movement ? "step:"
                : sourceKind == NoiseSourceKind.Burst ? "flight:" : "legacy:";
            string canonicalId = sourceEventId ?? string.Empty;
            if (!canonicalId.StartsWith(prefix, StringComparison.Ordinal))
                canonicalId = prefix + canonicalId;
            return _eventBus.SessionId + "|" + _eventBus.AttemptEpoch + "|"
                + canonicalId;
        }

        private static int CompareSourceOrder(NoiseSourceRecord record,
            float timestamp, int sourceRank, string sourceEventId)
        {
            int compare = record.SourceTimestamp.CompareTo(timestamp);
            if (compare != 0) return compare;
            compare = record.SourceEventClassRank.CompareTo(sourceRank);
            if (compare != 0) return compare;
            return CompareSourceEventIds(record.SourceEventId, sourceEventId);
        }

        private static int ComparePendingSources(PendingSource left,
            PendingSource right)
        {
            int compare = left.Record.SourceTimestamp.CompareTo(
                right.Record.SourceTimestamp);
            if (compare != 0) return compare;
            compare = left.Record.SourceEventClassRank.CompareTo(
                right.Record.SourceEventClassRank);
            if (compare != 0) return compare;
            return CompareSourceEventIds(left.Record.SourceEventId,
                right.Record.SourceEventId);
        }

        private static int CompareSourceEventIds(string left, string right)
        {
            return NoiseSourceOrdering.CompareSourceEventIds(left, right);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }
    }
}
