using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.SuspicionGrade
{
    /// <summary>
    /// Incident classes that can contribute to a room grade.
    /// </summary>
    public enum GradeIncidentType
    {
        /// <summary>A distinct FSM Chase-entry decision.</summary>
        ChaseEntry,

        /// <summary>A distinct fruitless Investigate-resolution decision.</summary>
        FruitlessResolution,

        /// <summary>A distinct FSM Capture decision.</summary>
        Capture
    }

    /// <summary>
    /// Normalized causes retained when adapting an Investigate-resolution record.
    /// </summary>
    public enum InvestigationResolutionCause
    {
        /// <summary>The investigation was opened by a noise report.</summary>
        Noise,

        /// <summary>The investigation ended without finding the player.</summary>
        Fruitless,

        /// <summary>The investigation found or committed to a visual cause.</summary>
        Vision,

        /// <summary>A valid resolution cause outside the grade incident classes.</summary>
        Other
    }

    /// <summary>
    /// Immutable grade-relevant projection of one authoritative trace delivery.
    /// </summary>
    public sealed class GradeTraceRecord
    {
        /// <summary>
        /// Creates an immutable trace projection. Validation is deliberately deferred
        /// to <see cref="GradeTraceReducer.Accept"/> so malformed deliveries can be
        /// rejected without throwing or changing an existing aggregate.
        /// </summary>
        /// <param name="sessionId">Session identity.</param>
        /// <param name="attemptEpoch">Attempt epoch within the session.</param>
        /// <param name="roomId">Room identity.</param>
        /// <param name="guardEid">Stable guard identity.</param>
        /// <param name="entryId">Opaque episode identity.</param>
        /// <param name="incidentType">Grade incident class, or null for non-incidents.</param>
        /// <param name="resolutionCause">Normalized resolution cause, when applicable.</param>
        /// <param name="eventId">Authoritative event identity.</param>
        public GradeTraceRecord(string sessionId, long attemptEpoch, string roomId,
            string guardEid, string entryId, GradeIncidentType? incidentType,
            InvestigationResolutionCause? resolutionCause, string eventId)
        {
            SessionId = sessionId ?? string.Empty;
            AttemptEpoch = attemptEpoch;
            RoomId = roomId ?? string.Empty;
            GuardEid = guardEid ?? string.Empty;
            EntryId = entryId ?? string.Empty;
            IncidentType = incidentType;
            ResolutionCause = resolutionCause;
            EventId = eventId ?? string.Empty;
        }

        /// <summary>Gets the session identity.</summary>
        public string SessionId { get; }

        /// <summary>Gets the attempt epoch.</summary>
        public long AttemptEpoch { get; }

        /// <summary>Gets the room identity.</summary>
        public string RoomId { get; }

        /// <summary>Gets the stable guard identity.</summary>
        public string GuardEid { get; }

        /// <summary>Gets the opaque episode identity.</summary>
        public string EntryId { get; }

        /// <summary>Gets the grade incident class, or null for a non-incident.</summary>
        public GradeIncidentType? IncidentType { get; }

        /// <summary>Gets the normalized Investigate-resolution cause, when applicable.</summary>
        public InvestigationResolutionCause? ResolutionCause { get; }

        /// <summary>Gets the authoritative event identity.</summary>
        public string EventId { get; }

        /// <summary>
        /// Adapts an authoritative FSM decision into an immutable grade trace record.
        /// Room and guard identity are explicit because the legacy R12 decision
        /// subclasses do not own either field.
        /// </summary>
        /// <param name="decisionRecord">Authoritative FSM decision delivery.</param>
        /// <param name="roomId">Room identity from the composition/trace boundary.</param>
        /// <param name="guardEid">Stable guard identity from the composition boundary.</param>
        /// <param name="record">The copied immutable record when adaptation succeeds.</param>
        /// <returns>True when the decision class is supported and copied.</returns>
        public static bool TryAdaptDecisionRecord(DecisionRecord decisionRecord,
            string roomId, string guardEid, out GradeTraceRecord record)
        {
            record = null;
            if (decisionRecord == null) return false;

            var chaseEntry = decisionRecord as ChaseEntry;
            if (chaseEntry != null)
                return TryAdaptChaseEntry(chaseEntry, roomId, guardEid,
                    out record);

            var resolution = decisionRecord as InvestigateResolution;
            if (resolution != null)
                return TryAdaptResolution(resolution, roomId, guardEid,
                    out record);

            var capture = decisionRecord as Capture;
            if (capture != null)
                return TryAdaptCapture(capture, roomId, guardEid, out record);

            return false;
        }

        private static bool TryAdaptChaseEntry(ChaseEntry decisionRecord,
            string roomId, string guardEid, out GradeTraceRecord record)
        {
            record = new GradeTraceRecord(decisionRecord.SessionId,
                decisionRecord.AttemptEpoch, roomId, guardEid,
                decisionRecord.EntryId, GradeIncidentType.ChaseEntry, null,
                decisionRecord.Identity);
            return true;
        }

        private static bool TryAdaptResolution(InvestigateResolution decisionRecord,
            string roomId, string guardEid, out GradeTraceRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(decisionRecord.Cause)) return false;

            InvestigationResolutionCause cause = ParseResolutionCause(
                decisionRecord.Cause);
            GradeIncidentType? incidentType = cause ==
                InvestigationResolutionCause.Fruitless
                ? GradeIncidentType.FruitlessResolution
                : (GradeIncidentType?)null;
            record = new GradeTraceRecord(decisionRecord.SessionId,
                decisionRecord.AttemptEpoch, roomId, guardEid,
                decisionRecord.EntryId, incidentType, cause,
                decisionRecord.Identity);
            return true;
        }

        private static bool TryAdaptCapture(Capture decisionRecord,
            string roomId, string guardEid, out GradeTraceRecord record)
        {
            record = new GradeTraceRecord(decisionRecord.SessionId,
                decisionRecord.AttemptEpoch, roomId, guardEid,
                decisionRecord.EntryId, GradeIncidentType.Capture, null,
                decisionRecord.Identity);
            return true;
        }

        /// <summary>
        /// Compatibility alias for callers that use the shorter adaptation name.
        /// </summary>
        /// <param name="decisionRecord">Authoritative FSM decision delivery.</param>
        /// <param name="roomId">Room identity from the composition/trace boundary.</param>
        /// <param name="guardEid">Stable guard identity from the composition boundary.</param>
        /// <returns>The immutable copy, or null for an unsupported decision.</returns>
        public static GradeTraceRecord FromDecisionRecord(DecisionRecord decisionRecord,
            string roomId, string guardEid)
        {
            GradeTraceRecord record;
            return TryAdaptDecisionRecord(decisionRecord, roomId, guardEid,
                out record) ? record : null;
        }

        private static InvestigationResolutionCause ParseResolutionCause(string cause)
        {
            if (string.Equals(cause, "noise", StringComparison.OrdinalIgnoreCase))
                return InvestigationResolutionCause.Noise;
            if (string.Equals(cause, "fruitless", StringComparison.OrdinalIgnoreCase))
                return InvestigationResolutionCause.Fruitless;
            if (string.Equals(cause, "vision", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cause, "sight-committed",
                    StringComparison.OrdinalIgnoreCase))
                return InvestigationResolutionCause.Vision;
            return InvestigationResolutionCause.Other;
        }
    }

    /// <summary>
    /// Immutable incident counts for one guard within a room aggregate.
    /// </summary>
    public sealed class GuardGradeBreakdown : IEquatable<GuardGradeBreakdown>
    {
        /// <summary>Creates a per-guard incident breakdown.</summary>
        /// <param name="guardEid">Stable guard identity.</param>
        /// <param name="chaseEntryCount">Distinct Chase-entry count.</param>
        /// <param name="fruitlessResolutionCount">Distinct fruitless-resolution count.</param>
        /// <param name="captureCount">Distinct Capture count.</param>
        /// <param name="incidentPenalty">Reserved finalizer incident penalty.</param>
        /// <param name="residualPenalty">Reserved finalizer residual penalty.</param>
        /// <param name="qualityScore">Reserved finalizer quality score.</param>
        /// <param name="exposureWeight">Reserved multi-guard exposure weight.</param>
        public GuardGradeBreakdown(string guardEid, int chaseEntryCount,
            int fruitlessResolutionCount, int captureCount,
            float incidentPenalty = 0f, float residualPenalty = 0f,
            float qualityScore = 0f, float exposureWeight = 0f)
        {
            GuardEid = guardEid ?? string.Empty;
            ChaseEntryCount = chaseEntryCount;
            FruitlessResolutionCount = fruitlessResolutionCount;
            CaptureCount = captureCount;
            IncidentPenalty = incidentPenalty;
            ResidualPenalty = residualPenalty;
            QualityScore = qualityScore;
            ExposureWeight = exposureWeight;
        }

        /// <summary>Gets the stable guard identity.</summary>
        public string GuardEid { get; }

        /// <summary>Gets the distinct Chase-entry count.</summary>
        public int ChaseEntryCount { get; }

        /// <summary>Gets the distinct fruitless-resolution count.</summary>
        public int FruitlessResolutionCount { get; }

        /// <summary>Gets the distinct Capture count.</summary>
        public int CaptureCount { get; }

        /// <summary>Gets the incident penalty placeholder.</summary>
        public float IncidentPenalty { get; }

        /// <summary>Gets the residual penalty placeholder.</summary>
        public float ResidualPenalty { get; }

        /// <summary>Gets the quality score placeholder.</summary>
        public float QualityScore { get; }

        /// <summary>Gets the exposure weight placeholder.</summary>
        public float ExposureWeight { get; }

        /// <inheritdoc />
        public bool Equals(GuardGradeBreakdown other)
        {
            if (ReferenceEquals(other, null)) return false;
            return string.Equals(GuardEid, other.GuardEid, StringComparison.Ordinal)
                && ChaseEntryCount == other.ChaseEntryCount
                && FruitlessResolutionCount == other.FruitlessResolutionCount
                && CaptureCount == other.CaptureCount
                && IncidentPenalty.Equals(other.IncidentPenalty)
                && ResidualPenalty.Equals(other.ResidualPenalty)
                && QualityScore.Equals(other.QualityScore)
                && ExposureWeight.Equals(other.ExposureWeight);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as GuardGradeBreakdown);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(GuardEid);
                hash = hash * 31 + ChaseEntryCount;
                hash = hash * 31 + FruitlessResolutionCount;
                hash = hash * 31 + CaptureCount;
                hash = hash * 31 + IncidentPenalty.GetHashCode();
                hash = hash * 31 + ResidualPenalty.GetHashCode();
                hash = hash * 31 + QualityScore.GetHashCode();
                return hash * 31 + ExposureWeight.GetHashCode();
            }
        }
    }

    /// <summary>
    /// Immutable room-scoped incident aggregate consumed by the grade finalizer.
    /// </summary>
    public sealed class GradeTraceAggregate : IEquatable<GradeTraceAggregate>
    {
        /// <summary>Creates an immutable aggregate and defensively copies its lists.</summary>
        /// <param name="sessionId">Session identity.</param>
        /// <param name="attemptEpoch">Attempt epoch.</param>
        /// <param name="roomId">Room identity.</param>
        /// <param name="chaseEntryCount">Room-wide distinct Chase-entry count.</param>
        /// <param name="fruitlessResolutionCount">Room-wide distinct fruitless count.</param>
        /// <param name="captureCount">Room-wide distinct Capture count.</param>
        /// <param name="perGuard">Deterministically ordered per-guard breakdowns.</param>
        /// <param name="contributingEventIds">Deterministically sorted event IDs.</param>
        public GradeTraceAggregate(string sessionId, long attemptEpoch, string roomId,
            int chaseEntryCount, int fruitlessResolutionCount, int captureCount,
            IReadOnlyList<GuardGradeBreakdown> perGuard,
            IReadOnlyList<string> contributingEventIds)
        {
            SessionId = sessionId ?? string.Empty;
            AttemptEpoch = attemptEpoch;
            RoomId = roomId ?? string.Empty;
            ChaseEntryCount = chaseEntryCount;
            FruitlessResolutionCount = fruitlessResolutionCount;
            CaptureCount = captureCount;
            PerGuard = Copy(perGuard);
            ContributingEventIds = Copy(contributingEventIds);
        }

        /// <summary>Gets the session identity.</summary>
        public string SessionId { get; }

        /// <summary>Gets the attempt epoch.</summary>
        public long AttemptEpoch { get; }

        /// <summary>Gets the room identity.</summary>
        public string RoomId { get; }

        /// <summary>Gets the room-wide distinct Chase-entry count.</summary>
        public int ChaseEntryCount { get; }

        /// <summary>Gets the room-wide distinct fruitless-resolution count.</summary>
        public int FruitlessResolutionCount { get; }

        /// <summary>Gets the room-wide distinct Capture count.</summary>
        public int CaptureCount { get; }

        /// <summary>Gets deterministically ordered per-guard breakdowns.</summary>
        public IReadOnlyList<GuardGradeBreakdown> PerGuard { get; }

        /// <summary>Gets ordinally sorted contributing event IDs.</summary>
        public IReadOnlyList<string> ContributingEventIds { get; }

        /// <inheritdoc />
        public bool Equals(GradeTraceAggregate other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (!string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
                || AttemptEpoch != other.AttemptEpoch
                || !string.Equals(RoomId, other.RoomId, StringComparison.Ordinal)
                || ChaseEntryCount != other.ChaseEntryCount
                || FruitlessResolutionCount != other.FruitlessResolutionCount
                || CaptureCount != other.CaptureCount
                || PerGuard.Count != other.PerGuard.Count
                || ContributingEventIds.Count != other.ContributingEventIds.Count)
                return false;

            for (int i = 0; i < PerGuard.Count; i++)
            {
                if (!PerGuard[i].Equals(other.PerGuard[i])) return false;
            }

            for (int i = 0; i < ContributingEventIds.Count; i++)
            {
                if (!string.Equals(ContributingEventIds[i],
                    other.ContributingEventIds[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as GradeTraceAggregate);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(SessionId);
                hash = hash * 31 + AttemptEpoch.GetHashCode();
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(RoomId);
                hash = hash * 31 + ChaseEntryCount;
                hash = hash * 31 + FruitlessResolutionCount;
                hash = hash * 31 + CaptureCount;
                return hash;
            }
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            var copy = new List<T>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++) copy.Add(source[i]);
            }
            return new ReadOnlyCollection<T>(copy);
        }
    }

    /// <summary>
    /// Presentation seam for the read-only suspicion meter projection.
    /// Implementations may be unavailable-safe UI adapters or test presenters.
    /// </summary>
    public interface ISuspicionMeterPresenter
    {
        /// <summary>Presents one immutable meter read model.</summary>
        /// <param name="readModel">The authoritative projection to present.</param>
        void Present(SuspicionMeterReadModel readModel);
    }

    /// <summary>
    /// Composition-facing API for the event-driven Suspicion Meter and Grade system.
    /// </summary>
    public interface ISuspicionGradeSystem
    {
        /// <summary>Accepts one authoritative suspicion snapshot.</summary>
        /// <param name="snapshot">Snapshot to project without mutation.</param>
        void AcceptSnapshot(GuardSuspicionSnapshot snapshot);

        /// <summary>Accepts one immutable grade trace projection.</summary>
        /// <param name="traceRecord">Trace projection to reduce.</param>
        void AcceptTrace(GradeTraceRecord traceRecord);

        /// <summary>Finalizes one room boundary idempotently.</summary>
        /// <param name="boundary">Session-owned room completion boundary.</param>
        /// <returns>The immutable final result for this boundary.</returns>
        RoomGradeFinalized FinalizeRoom(RoomCompletionBoundary boundary);

        /// <summary>Starts a clean projection generation at a session boundary.</summary>
        /// <param name="sessionId">New active session identity.</param>
        /// <param name="attemptEpoch">New active attempt epoch.</param>
        void ResetForBoundary(string sessionId, long attemptEpoch);
    }

    /// <summary>
    /// Immutable typed event carrying one authoritative suspicion snapshot.
    /// </summary>
    public sealed class SuspicionSnapshotEvent : IEvent, IEventPhase,
        IEventDeduplicationKey
    {
        /// <summary>Creates a snapshot event with an explicit source event identity.</summary>
        /// <param name="snapshot">Copied authoritative snapshot value.</param>
        /// <param name="eventId">Stable source event identity.</param>
        /// <param name="timestamp">Authoritative virtual timestamp.</param>
        public SuspicionSnapshotEvent(GuardSuspicionSnapshot snapshot,
            string eventId, float timestamp)
        {
            Snapshot = snapshot;
            EventId = eventId ?? string.Empty;
            Timestamp = timestamp;
        }

        /// <summary>Gets the copied snapshot payload.</summary>
        public GuardSuspicionSnapshot Snapshot { get; }

        /// <summary>Gets the source event identity.</summary>
        public string EventId { get; }

        /// <inheritdoc />
        public string SessionId { get { return Snapshot.Identity.SessionId; } }

        /// <inheritdoc />
        public long AttemptEpoch { get { return Snapshot.Identity.AttemptEpoch; } }

        /// <inheritdoc />
        public float Timestamp { get; }

        /// <inheritdoc />
        public string Publisher { get { return "Perception"; } }

        /// <inheritdoc />
        public string Identity { get { return EventId; } }

        /// <inheritdoc />
        public EventBusPhase Phase { get { return EventBusPhase.Presentation; } }

        /// <inheritdoc />
        public string DeduplicationIdentity { get { return EventId; } }
    }

    /// <summary>
    /// Immutable typed event carrying one adapted grade trace record.
    /// </summary>
    public sealed class GradeTraceEvent : IEvent, IEventPhase,
        IEventDeduplicationKey
    {
        /// <summary>Creates a trace event with an explicit virtual timestamp.</summary>
        /// <param name="traceRecord">Immutable trace payload.</param>
        /// <param name="timestamp">Authoritative virtual timestamp.</param>
        public GradeTraceEvent(GradeTraceRecord traceRecord, float timestamp)
        {
            TraceRecord = traceRecord;
            Timestamp = timestamp;
        }

        /// <summary>Gets the immutable trace payload.</summary>
        public GradeTraceRecord TraceRecord { get; }

        /// <summary>Gets the authoritative trace event identity.</summary>
        public string EventId
        {
            get { return TraceRecord == null ? string.Empty : TraceRecord.EventId; }
        }

        /// <inheritdoc />
        public string SessionId
        {
            get { return TraceRecord == null ? string.Empty : TraceRecord.SessionId; }
        }

        /// <inheritdoc />
        public long AttemptEpoch
        {
            get { return TraceRecord == null ? -1L : TraceRecord.AttemptEpoch; }
        }

        /// <inheritdoc />
        public float Timestamp { get; }

        /// <inheritdoc />
        public string Publisher { get { return "GuardFSM"; } }

        /// <inheritdoc />
        public string Identity
        {
            get { return TraceRecord == null ? string.Empty : TraceRecord.EventId; }
        }

        /// <inheritdoc />
        public EventBusPhase Phase { get { return EventBusPhase.Presentation; } }

        /// <inheritdoc />
        public string DeduplicationIdentity { get { return Identity; } }
    }

    /// <summary>
    /// Immutable typed event carrying one explicit room completion boundary.
    /// </summary>
    public sealed class RoomCompletionBoundaryEvent : IEvent, IEventPhase,
        IEventDeduplicationKey
    {
        /// <summary>Creates a room-boundary event.</summary>
        /// <param name="boundary">Session-owned immutable boundary.</param>
        /// <param name="timestamp">Authoritative virtual timestamp.</param>
        public RoomCompletionBoundaryEvent(RoomCompletionBoundary boundary,
            float timestamp)
        {
            Boundary = boundary;
            Timestamp = timestamp;
        }

        /// <summary>Gets the immutable completion boundary.</summary>
        public RoomCompletionBoundary Boundary { get; }

        /// <inheritdoc />
        public string SessionId
        {
            get { return Boundary == null ? string.Empty : Boundary.SessionId; }
        }

        /// <inheritdoc />
        public long AttemptEpoch
        {
            get { return Boundary == null ? -1L : Boundary.AttemptEpoch; }
        }

        /// <inheritdoc />
        public float Timestamp { get; }

        /// <inheritdoc />
        public string Publisher { get { return "SessionBoundary"; } }

        /// <inheritdoc />
        public string Identity
        {
            get
            {
                return Boundary == null ? string.Empty
                    : "room-boundary:" + Boundary.SessionId + "|"
                        + Boundary.AttemptEpoch + "|" + Boundary.RoomId;
            }
        }

        /// <inheritdoc />
        public EventBusPhase Phase { get { return EventBusPhase.Presentation; } }

        /// <inheritdoc />
        public string DeduplicationIdentity { get { return Identity; } }
    }

    /// <summary>
    /// Immutable typed event published after one room-grade finalization.
    /// </summary>
    public sealed class RoomGradeFinalizedEvent : IEvent, IEventPhase,
        IEventDeduplicationKey
    {
        /// <summary>Creates a final-result event.</summary>
        /// <param name="result">Immutable final room-grade result.</param>
        /// <param name="timestamp">Authoritative virtual timestamp.</param>
        public RoomGradeFinalizedEvent(RoomGradeFinalized result, float timestamp)
        {
            Result = result;
            Timestamp = timestamp;
        }

        /// <summary>Gets the immutable final result payload.</summary>
        public RoomGradeFinalized Result { get; }

        /// <inheritdoc />
        public string SessionId
        {
            get { return Result == null ? string.Empty : Result.SessionId; }
        }

        /// <inheritdoc />
        public long AttemptEpoch
        {
            get { return Result == null ? -1L : Result.AttemptEpoch; }
        }

        /// <inheritdoc />
        public float Timestamp { get; }

        /// <inheritdoc />
        public string Publisher { get { return "SuspicionGrade"; } }

        /// <inheritdoc />
        public string Identity
        {
            get
            {
                return Result == null ? string.Empty
                    : "room-grade:" + Result.SessionId + "|"
                        + Result.AttemptEpoch + "|" + Result.RoomId;
            }
        }

        /// <inheritdoc />
        public EventBusPhase Phase { get { return EventBusPhase.Presentation; } }

        /// <inheritdoc />
        public string DeduplicationIdentity { get { return Identity; } }
    }
}
