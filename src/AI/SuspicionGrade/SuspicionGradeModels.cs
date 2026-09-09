using System;

namespace WhisperWard.AI.SuspicionGrade
{
    /// <summary>
    /// Semantic presentation region for a guard's current suspicion meter.
    /// </summary>
    public enum SuspicionMeterRegion
    {
        /// <summary>The meter has insufficient authoritative input.</summary>
        Unavailable,

        /// <summary>The guard is below the supplied investigation entry threshold.</summary>
        Quiet,

        /// <summary>The guard is at or above the supplied investigation entry threshold.</summary>
        Investigate,

        /// <summary>The guard has reached the Chase threshold or authoritative Chase state.</summary>
        Chase
    }

    /// <summary>
    /// Stable session, attempt, room, and guard identity for Suspicion/Grade projections.
    /// </summary>
    public readonly struct SuspicionGradeIdentity : IEquatable<SuspicionGradeIdentity>
    {
        /// <summary>
        /// Creates a projection identity.
        /// </summary>
        /// <param name="sessionId">Session identity.</param>
        /// <param name="attemptEpoch">Attempt epoch within the session.</param>
        /// <param name="roomId">Room identity.</param>
        /// <param name="guardEid">Guard entity identity.</param>
        public SuspicionGradeIdentity(
            string sessionId, long attemptEpoch, string roomId, string guardEid)
        {
            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            RoomId = roomId;
            GuardEid = guardEid;
        }

        /// <summary>Gets the session identity.</summary>
        public string SessionId { get; }

        /// <summary>Gets the attempt epoch.</summary>
        public long AttemptEpoch { get; }

        /// <summary>Gets the room identity.</summary>
        public string RoomId { get; }

        /// <summary>Gets the guard entity identity.</summary>
        public string GuardEid { get; }

        /// <inheritdoc />
        public bool Equals(SuspicionGradeIdentity other)
        {
            return AttemptEpoch == other.AttemptEpoch
                && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
                && string.Equals(RoomId, other.RoomId, StringComparison.Ordinal)
                && string.Equals(GuardEid, other.GuardEid, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is SuspicionGradeIdentity other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (SessionId == null ? 0 : StringComparer.Ordinal.GetHashCode(SessionId));
                hash = hash * 31 + AttemptEpoch.GetHashCode();
                hash = hash * 31 + (RoomId == null ? 0 : StringComparer.Ordinal.GetHashCode(RoomId));
                hash = hash * 31 + (GuardEid == null ? 0 : StringComparer.Ordinal.GetHashCode(GuardEid));
                return hash;
            }
        }

        /// <inheritdoc />
        public static bool operator ==(SuspicionGradeIdentity left, SuspicionGradeIdentity right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc />
        public static bool operator !=(SuspicionGradeIdentity left, SuspicionGradeIdentity right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Immutable authoritative inputs for one guard's suspicion meter projection.
    /// </summary>
    public readonly struct GuardSuspicionSnapshot
    {
        /// <summary>
        /// Creates a guard suspicion snapshot. Presence flags preserve missing-data
        /// semantics without fabricating authoritative values.
        /// </summary>
        /// <param name="identity">Session, attempt, room, and guard identity.</param>
        /// <param name="hasAccumulator">Whether <paramref name="aCurrent" /> is authoritative.</param>
        /// <param name="aCurrent">Current authoritative suspicion accumulator.</param>
        /// <param name="hasResidual">Whether <paramref name="rCurrent" /> is authoritative.</param>
        /// <param name="rCurrent">Current authoritative residual wariness.</param>
        /// <param name="hasChaseThreshold">Whether <paramref name="tChase" /> is authoritative.</param>
        /// <param name="tChase">Authoritative Chase threshold.</param>
        /// <param name="investigationEntryThreshold">Supplied authoritative investigation entry threshold.</param>
        /// <param name="guardState">Authoritative FSM guard state.</param>
        /// <param name="lastCausalEvent">Most recent causal event identifier or description.</param>
        /// <param name="sourceEntryId">Source episode entry identity, when available.</param>
        public GuardSuspicionSnapshot(
            SuspicionGradeIdentity identity,
            bool hasAccumulator, float aCurrent,
            bool hasResidual, float rCurrent,
            bool hasChaseThreshold, float tChase,
            float investigationEntryThreshold,
            string guardState, string lastCausalEvent, string sourceEntryId)
        {
            Identity = identity;
            HasAccumulator = hasAccumulator;
            ACurrent = aCurrent;
            HasResidual = hasResidual;
            RCurrent = rCurrent;
            HasChaseThreshold = hasChaseThreshold;
            TChase = tChase;
            InvestigationEntryThreshold = investigationEntryThreshold;
            GuardState = guardState;
            LastCausalEvent = lastCausalEvent;
            SourceEntryId = sourceEntryId;
        }

        /// <summary>Gets the session, attempt, room, and guard identity.</summary>
        public SuspicionGradeIdentity Identity { get; }

        /// <summary>Gets whether the accumulator value is available.</summary>
        public bool HasAccumulator { get; }

        /// <summary>Gets the authoritative current suspicion accumulator.</summary>
        public float ACurrent { get; }

        /// <summary>Gets whether the residual value is available.</summary>
        public bool HasResidual { get; }

        /// <summary>Gets the authoritative current residual wariness.</summary>
        public float RCurrent { get; }

        /// <summary>Gets whether the Chase threshold value is available.</summary>
        public bool HasChaseThreshold { get; }

        /// <summary>Gets the authoritative Chase threshold.</summary>
        public float TChase { get; }

        /// <summary>Gets the supplied authoritative investigation entry threshold.</summary>
        public float InvestigationEntryThreshold { get; }

        /// <summary>Gets the authoritative FSM guard state.</summary>
        public string GuardState { get; }

        /// <summary>Gets the most recent causal event.</summary>
        public string LastCausalEvent { get; }

        /// <summary>Gets the source episode entry identity.</summary>
        public string SourceEntryId { get; }
    }

    /// <summary>
    /// Immutable read-only projection consumed by the Suspicion Meter presenter.
    /// </summary>
    public sealed class SuspicionMeterReadModel
    {
        /// <summary>
        /// Creates a Suspicion Meter read model.
        /// </summary>
        /// <param name="identity">Session, attempt, room, and guard identity.</param>
        /// <param name="isAvailable">Whether the projection has required authoritative input.</param>
        /// <param name="aCurrent">Copied authoritative current accumulator.</param>
        /// <param name="tChase">Copied authoritative Chase threshold.</param>
        /// <param name="meterRatio">Clamped normalized meter ratio.</param>
        /// <param name="meterPercent">Rounded display value.</param>
        /// <param name="region">Semantic meter region.</param>
        /// <param name="rCurrent">Copied authoritative residual.</param>
        /// <param name="guardState">Copied authoritative guard state.</param>
        /// <param name="lastCausalEvent">Copied causal event.</param>
        /// <param name="sourceEntryId">Copied source episode entry identity.</param>
        public SuspicionMeterReadModel(
            SuspicionGradeIdentity identity, bool isAvailable,
            float aCurrent, float tChase, float meterRatio, int meterPercent,
            SuspicionMeterRegion region, float rCurrent, string guardState,
            string lastCausalEvent, string sourceEntryId)
        {
            Identity = identity;
            IsAvailable = isAvailable;
            ACurrent = aCurrent;
            TChase = tChase;
            MeterRatio = meterRatio;
            MeterPercent = meterPercent;
            Region = region;
            RCurrent = rCurrent;
            GuardState = guardState;
            LastCausalEvent = lastCausalEvent;
            SourceEntryId = sourceEntryId;
        }

        /// <summary>Gets the session, attempt, room, and guard identity.</summary>
        public SuspicionGradeIdentity Identity { get; }

        /// <summary>Gets whether required authoritative input was available.</summary>
        public bool IsAvailable { get; }

        /// <summary>Gets the copied authoritative current accumulator.</summary>
        public float ACurrent { get; }

        /// <summary>Gets the copied authoritative Chase threshold.</summary>
        public float TChase { get; }

        /// <summary>Gets the clamped normalized accumulator ratio.</summary>
        public float MeterRatio { get; }

        /// <summary>Gets the rounded display meter value.</summary>
        public int MeterPercent { get; }

        /// <summary>Gets the semantic meter region.</summary>
        public SuspicionMeterRegion Region { get; }

        /// <summary>Gets the copied authoritative residual wariness.</summary>
        public float RCurrent { get; }

        /// <summary>Gets the copied authoritative FSM guard state.</summary>
        public string GuardState { get; }

        /// <summary>Gets the copied causal event.</summary>
        public string LastCausalEvent { get; }

        /// <summary>Gets the copied source episode entry identity.</summary>
        public string SourceEntryId { get; }
    }
}
