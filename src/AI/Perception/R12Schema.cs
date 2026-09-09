using System;
using UnityEngine;
using WhisperWard.AI.Core;

namespace WhisperWard.AI.Perception
{
    /// <summary>
    /// Common marker for queued sensing inputs. The mutable base below is retained
    /// only for compatibility adapters; authoritative relays use the immutable base.
    /// </summary>
    public interface ISensingFact : IEvent, IEventPhase
    {
    }

    /// <summary>
    /// Mutable compatibility base for legacy sensing fixtures. New gameplay facts
    /// should use immutable contract types below and provide all envelope metadata.
    /// </summary>
    public abstract class SensingFact : ISensingFact, IEventMetadataWriter
    {
        public string SessionId { get; set; }
        public long AttemptEpoch { get; set; }
        public float Timestamp { get; set; }
        public string Publisher { get { return "Perception"; } }
        public string Identity { get; set; }
        public EventBusPhase Phase { get { return EventBusPhase.FsmDecision; } }

        protected SensingFact()
        {
            SessionId = string.Empty;
            Identity = string.Empty;
        }

        public void SetEnvelope(string sessionId, long attemptEpoch, string identity)
        {
            SessionId = sessionId ?? string.Empty;
            AttemptEpoch = attemptEpoch;
            Identity = identity ?? string.Empty;
        }
    }

    /// <summary>
    /// Read-only sensing envelope for authoritative per-guard relays. Unlike the
    /// compatibility base, it exposes no public metadata mutation seam.
    /// </summary>
    public abstract class ImmutableSensingFact : ISensingFact
    {
        protected ImmutableSensingFact(string sessionId, long attemptEpoch,
            float timestamp, string identity)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId");
            if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("identity");
            if (float.IsNaN(timestamp) || float.IsInfinity(timestamp))
                throw new ArgumentException("timestamp");
            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            Timestamp = timestamp;
            Identity = identity;
        }

        public string SessionId { get; }
        public long AttemptEpoch { get; }
        public float Timestamp { get; }
        public virtual string Publisher { get { return "Perception"; } }
        public string Identity { get; }
        public EventBusPhase Phase { get { return EventBusPhase.FsmDecision; } }
    }

    /// <summary>
    /// Immutable raw publication owned by NoiseEmitter. One fact is shared by all
    /// guards that hear it; Perception adds per-guard relay and episode identity.
    /// </summary>
    public sealed class NoisePublished : IEvent, IEventPhase,
        IEventDeduplicationKey
    {
        /// <summary>
        /// Compatibility constructor for legacy sources. Authoritative Movement
        /// and Burst producers should use the explicit-rank overload.
        /// </summary>
        public NoisePublished(string sessionId, long attemptEpoch, ulong factId,
            string sourceEventId, string kind, string source, Vector3 position,
            float radius, float timestamp)
            : this(sessionId, attemptEpoch, factId, sourceEventId, kind, source,
                position, radius, timestamp, 2, position, "legacy-adapter",
                position, string.Empty, string.Empty, 0f, 0f)
        {
        }

        /// <summary>
        /// Creates one immutable raw fact with source ordering and provenance.
        /// </summary>
        public NoisePublished(string sessionId, long attemptEpoch, ulong factId,
            string sourceEventId, string kind, string source, Vector3 position,
            float radius, float sourceTimestamp, int sourceEventClassRank,
            Vector3 authoritativeOrigin, string originProvenance)
            : this(sessionId, attemptEpoch, factId, sourceEventId, kind, source,
                position, radius, sourceTimestamp, sourceEventClassRank,
                authoritativeOrigin, originProvenance, position, string.Empty,
                string.Empty, 0f, 0f)
        {
        }

        /// <summary>
        /// Creates one raw fact with the complete controller source payload.
        /// </summary>
        public NoisePublished(string sessionId, long attemptEpoch, ulong factId,
            string sourceEventId, string kind, string source, Vector3 position,
            float radius, float sourceTimestamp, int sourceEventClassRank,
            Vector3 authoritativeOrigin, string originProvenance,
            Vector3 previousPosition, string sourceState, string strideTargetState,
            float strideLengthMeters, float ledgerRemainderMeters,
            float? terminalPublicationTime = null)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId");
            if (factId == 0) throw new ArgumentOutOfRangeException(nameof(factId));
            if (string.IsNullOrWhiteSpace(sourceEventId)) throw new ArgumentException("sourceEventId");
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("kind");
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("source");
            if (sourceEventClassRank < 0) throw new ArgumentOutOfRangeException(nameof(sourceEventClassRank));
            if (float.IsNaN(sourceTimestamp) || float.IsInfinity(sourceTimestamp))
                throw new ArgumentException("sourceTimestamp");
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (!IsFinite(position))
                throw new ArgumentException("position");
            if (!IsFinite(authoritativeOrigin))
                throw new ArgumentException("authoritativeOrigin");
            if (string.IsNullOrWhiteSpace(originProvenance))
                throw new ArgumentException("originProvenance");
            if (!IsFinite(previousPosition))
                throw new ArgumentException("previousPosition");
            if (!IsFiniteNonNegative(strideLengthMeters)
                || !IsFiniteNonNegative(ledgerRemainderMeters))
                throw new ArgumentException("movement-ledger");

            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            FactId = factId;
            SourceEventId = sourceEventId;
            Kind = kind;
            Source = source;
            Position = position;
            Radius = radius;
            SourceTimestamp = sourceTimestamp;
            TerminalPublicationTime = terminalPublicationTime ?? sourceTimestamp;
            if (float.IsNaN(TerminalPublicationTime)
                || float.IsInfinity(TerminalPublicationTime))
                throw new ArgumentException("terminalPublicationTime");
            SourceEventClassRank = sourceEventClassRank;
            AuthoritativeOrigin = authoritativeOrigin;
            OriginProvenance = originProvenance;
            PreviousPosition = previousPosition;
            SourceState = sourceState ?? string.Empty;
            StrideTargetState = strideTargetState ?? string.Empty;
            StrideLengthMeters = strideLengthMeters;
            LedgerRemainderMeters = ledgerRemainderMeters;
        }

        public string SessionId { get; }
        public long AttemptEpoch { get; }
        public ulong FactId { get; }
        public string SourceEventId { get; }
        public string Kind { get; }
        public string Source { get; }
        public Vector3 Position { get; }
        public float Radius { get; }
        public float SourceTimestamp { get; }
        public float TerminalPublicationTime { get; }
        public int SourceEventClassRank { get; }
        public Vector3 AuthoritativeOrigin { get; }
        public string OriginProvenance { get; }
        public Vector3 PreviousPosition { get; }
        public string SourceState { get; }
        public string StrideTargetState { get; }
        public float StrideLengthMeters { get; }
        public float LedgerRemainderMeters { get; }
        public float Timestamp { get { return SourceTimestamp; } }
        public string Publisher { get { return "NoiseEmitter"; } }
        public string Identity { get { return FactId.ToString("D"); } }
        public string DeduplicationIdentity { get { return FactId.ToString("D"); } }
        public EventBusPhase Phase { get { return EventBusPhase.Hearing; } }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }

    /// <summary>
    /// Relay consumption result. Perception publishes eligible relays; the FSM
    /// records the terminal outcome without mutating the relay.
    /// </summary>
    public enum RelayConsumption
    {
        Eligible,
        Consumed,
        Ignored,
        Stale
    }

    /// <summary>
    /// Immutable per-guard hearing relay. The raw fact identity is retained while
    /// entry identity remains owned by Perception.
    /// </summary>
    public sealed class NoiseHeardRelay : ImmutableSensingFact
    {
        /// <summary>
        /// Compatibility constructor for older FSM and fixture adapters.
        /// </summary>
        public NoiseHeardRelay(string sessionId, long attemptEpoch, ulong factId,
            string entryId, string guardEid, string kind, string source,
            Vector3 position, float sourceTimestamp, float evaluatedAt,
            float residualAtHearing)
            : this(sessionId, attemptEpoch, factId, entryId, guardEid, kind,
                source, position, 1f, 2, "legacy-adapter", position,
                sourceTimestamp, evaluatedAt, residualAtHearing,
                sourceTimestamp)
        {
        }

        /// <summary>
        /// Creates an immutable relay carrying all raw-fact provenance needed by
        /// the FSM and replay trace.
        /// </summary>
        public NoiseHeardRelay(string sessionId, long attemptEpoch, ulong factId,
            string entryId, string guardEid, string kind, string source,
            Vector3 position, float radius, int sourceEventClassRank,
            string originProvenance, Vector3 authoritativeOrigin,
            float sourceTimestamp, float evaluatedAt, float residualAtHearing,
            float? terminalPublicationTime = null)
            : base(sessionId, attemptEpoch, evaluatedAt,
                factId.ToString("D") + ":" + (guardEid ?? string.Empty))
        {
            if (string.IsNullOrWhiteSpace(entryId)) throw new ArgumentException("entryId");
            if (string.IsNullOrWhiteSpace(guardEid)) throw new ArgumentException("guardEid");
            if (factId == 0) throw new ArgumentOutOfRangeException(nameof(factId));
            if (float.IsNaN(sourceTimestamp) || float.IsInfinity(sourceTimestamp))
                throw new ArgumentException("sourceTimestamp");
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (sourceEventClassRank < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceEventClassRank));
            if (string.IsNullOrWhiteSpace(kind))
                throw new ArgumentException("kind");
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("source");
            if (!IsFinite(position))
                throw new ArgumentException("position");
            if (!IsFinite(authoritativeOrigin))
                throw new ArgumentException("authoritativeOrigin");
            if (float.IsNaN(evaluatedAt) || float.IsInfinity(evaluatedAt))
                throw new ArgumentException("evaluatedAt");
            if (float.IsNaN(residualAtHearing) || float.IsInfinity(residualAtHearing))
                throw new ArgumentException("residualAtHearing");
            if (string.IsNullOrWhiteSpace(originProvenance))
                throw new ArgumentException("originProvenance");

            FactId = factId;
            EntryId = entryId;
            GuardEid = guardEid;
            Kind = kind ?? string.Empty;
            Source = source ?? string.Empty;
            Position = position;
            Radius = radius;
            SourceEventClassRank = sourceEventClassRank;
            OriginProvenance = originProvenance;
            AuthoritativeOrigin = authoritativeOrigin;
            SourceTimestamp = sourceTimestamp;
            TerminalPublicationTime = terminalPublicationTime ?? sourceTimestamp;
            if (float.IsNaN(TerminalPublicationTime)
                || float.IsInfinity(TerminalPublicationTime))
                throw new ArgumentException("terminalPublicationTime");
            EvaluatedAt = evaluatedAt;
            ResidualAtHearing = residualAtHearing;
            Consumption = RelayConsumption.Eligible;
        }

        public ulong FactId { get; }
        public string EntryId { get; }
        public string GuardEid { get; }
        public string Kind { get; }
        public string Source { get; }
        public Vector3 Position { get; }
        public float Radius { get; }
        public int SourceEventClassRank { get; }
        public string OriginProvenance { get; }
        public Vector3 AuthoritativeOrigin { get; }
        public float SourceTimestamp { get; }
        /// <summary>Terminal virtual time used as the hearing deadline anchor.</summary>
        public float TerminalPublicationTime { get; }
        public float EvaluatedAt { get; }
        public float ResidualAtHearing { get; }
        public RelayConsumption Consumption { get; }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    /// <summary>
    /// Immutable FSM-owned outcome keyed to one relay identity.
    /// </summary>
    public sealed class NoiseConsumptionOutcome : IEvent, IEventPhase
    {
        public NoiseConsumptionOutcome(string sessionId, long attemptEpoch,
            ulong factId, string guardEid, string entryId,
            RelayConsumption consumption, float timestamp)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId");
            if (factId == 0) throw new ArgumentOutOfRangeException(nameof(factId));
            if (string.IsNullOrWhiteSpace(guardEid)) throw new ArgumentException("guardEid");
            if (string.IsNullOrWhiteSpace(entryId)) throw new ArgumentException("entryId");
            if (float.IsNaN(timestamp) || float.IsInfinity(timestamp))
                throw new ArgumentException("timestamp");

            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            FactId = factId;
            GuardEid = guardEid;
            EntryId = entryId;
            Consumption = consumption;
            Timestamp = timestamp;
        }

        public string SessionId { get; }
        public long AttemptEpoch { get; }
        public ulong FactId { get; }
        public string GuardEid { get; }
        public string EntryId { get; }
        public RelayConsumption Consumption { get; }
        public float Timestamp { get; }
        public string Publisher { get { return "GuardFSM"; } }
        public string Identity { get { return FactId.ToString("D") + ":" + GuardEid; } }
        public EventBusPhase Phase { get { return EventBusPhase.Presentation; } }
    }

    /// <summary>
    /// Immutable receipt explaining why an eligible relay was suppressed by the
    /// FSM. It is presentation telemetry only and never creates gameplay liveness.
    /// </summary>
    public sealed class NoiseSuppressedReceipt : IEvent, IEventPhase
    {
        public NoiseSuppressedReceipt(string sessionId, long attemptEpoch,
            ulong factId, string guardEid, string entryId, string reason,
            bool visibleToPlayer, bool microTellEmitted, float timestamp)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId");
            if (factId == 0) throw new ArgumentOutOfRangeException(nameof(factId));
            if (string.IsNullOrWhiteSpace(guardEid)) throw new ArgumentException("guardEid");
            if (string.IsNullOrWhiteSpace(entryId)) throw new ArgumentException("entryId");
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("reason");
            if (float.IsNaN(timestamp) || float.IsInfinity(timestamp))
                throw new ArgumentException("timestamp");
            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            FactId = factId;
            GuardEid = guardEid;
            EntryId = entryId;
            SuppressionReason = reason;
            VisibleToPlayer = visibleToPlayer;
            MicroTellEmitted = microTellEmitted;
            Timestamp = timestamp;
        }

        public string SessionId { get; }
        public long AttemptEpoch { get; }
        public ulong FactId { get; }
        public string GuardEid { get; }
        public string EntryId { get; }
        public string SuppressionReason { get; }
        public bool VisibleToPlayer { get; }
        public bool MicroTellEmitted { get; }
        public float Timestamp { get; }
        public string Publisher { get { return "GuardFSM"; } }
        public string Identity { get { return FactId.ToString("D") + ":" + GuardEid + ":suppressed"; } }
        public EventBusPhase Phase { get { return EventBusPhase.Presentation; } }
    }

    /// <summary>
    /// Immutable FSM-owned liveness transition. Perception reads the last
    /// published fact at a boundary; it does not synchronously query FSM state.
    /// </summary>
    public sealed class LivenessFact : ImmutableSensingFact
    {
        /// <summary>Compatibility constructor for legacy liveness fixtures.</summary>
        public LivenessFact(string sessionId, long attemptEpoch, string operation,
            string tier, string cause, float sourceTimestamp, float evaluatedAt,
            string guardEid, string entryId)
            : this(sessionId, attemptEpoch, operation, tier, cause, sourceTimestamp,
                evaluatedAt, guardEid, entryId, Vector3.zero, "legacy-unspecified")
        {
        }

        /// <summary>Creates an immutable open, promote, or close liveness fact.</summary>
        public LivenessFact(string sessionId, long attemptEpoch, string operation,
            string tier, string cause, float sourceTimestamp, float evaluatedAt,
            string guardEid, string entryId, Vector3 position,
            string positionSource)
            : base(sessionId, attemptEpoch, evaluatedAt,
                BuildIdentity(operation, guardEid, entryId, sourceTimestamp))
        {
            if (!string.Equals(operation, "open", StringComparison.Ordinal)
                && !string.Equals(operation, "promote", StringComparison.Ordinal)
                && !string.Equals(operation, "close", StringComparison.Ordinal))
                throw new ArgumentException("operation");
            if (!string.Equals(tier, "Investigate", StringComparison.Ordinal)
                && !string.Equals(tier, "Chase", StringComparison.Ordinal))
                throw new ArgumentException("tier");
            if (string.IsNullOrWhiteSpace(cause)) throw new ArgumentException("cause");
            if (string.IsNullOrWhiteSpace(guardEid)) throw new ArgumentException("guardEid");
            if (string.IsNullOrWhiteSpace(entryId)) throw new ArgumentException("entryId");
            if (float.IsNaN(sourceTimestamp) || float.IsInfinity(sourceTimestamp))
                throw new ArgumentException("sourceTimestamp");
            if (!IsFinite(position)) throw new ArgumentException("position");
            if (string.IsNullOrWhiteSpace(positionSource))
                throw new ArgumentException("positionSource");

            Operation = operation;
            Tier = tier;
            Cause = cause;
            SourceTimestamp = sourceTimestamp;
            EvaluatedAt = evaluatedAt;
            GuardEid = guardEid;
            EntryId = entryId;
            Position = position;
            PositionSource = positionSource;
        }

        public override string Publisher { get { return "GuardAISystem"; } }
        public string Operation { get; }
        public string Tier { get; }
        public string Cause { get; }
        public float SourceTimestamp { get; }
        public float EvaluatedAt { get; }
        public string GuardEid { get; }
        public string EntryId { get; }
        public Vector3 Position { get; }
        public string PositionSource { get; }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static string BuildIdentity(string operation, string guardEid,
            string entryId, float sourceTimestamp)
        {
            return (operation ?? string.Empty) + ":" + (guardEid ?? string.Empty)
                + ":" + (entryId ?? string.Empty) + ":" + sourceTimestamp.ToString("R");
        }
    }

    /// <summary>
    /// Event-sourced liveness snapshot consumed by the FSM. It replaces direct
    /// synchronous PerceptionDriver polling at state-update time.
    /// </summary>
    public sealed class LivenessSnapshot : SensingFact
    {
        public string GuardEid { get; set; }
        public bool HasLOS { get; set; }
        public Vector3 PlayerPosition { get; set; }
        public string LiveEntryId { get; set; }
        public string GoalMode { get; set; }

        public LivenessSnapshot()
        {
            GuardEid = string.Empty;
            LiveEntryId = string.Empty;
            GoalMode = string.Empty;
        }
    }

    public class LOSGain : SensingFact
    {
        public Vector3 Position { get; set; }
        public float Distance { get; set; }
        public GameObject Guard { get; set; }
        public string GuardEid { get; set; }
    }

    public class LOSBreak : SensingFact
    {
        public float ResidualR { get; set; }
        public string BreakType { get; set; }
        public string CausalClass { get; set; }
        public float GuardYawDelta { get; set; }
        public string GuardEid { get; set; }

        // Populated only for the amended witnessed hide-entry gate. The payload is
        // retained on the fact so the FSM never reconstructs the spot or threshold.
        public bool IsHideEntry { get; set; }
        public float APreBreak { get; set; }
        public float ThresholdApplied { get; set; }
        public Vector3 SpotPosition { get; set; }
        public Vector3 SpotFrontPosition { get; set; }
        public bool HasSpotFrontPosition { get; set; }
        public string HideSpotId { get; set; }
        public string EntryId { get; set; }
    }

    /// <summary>
    /// Authoritative player-occupancy transition for a hide spot. The HideSpot
    /// owner publishes this fact; the FSM never infers occupancy from absent LOS.
    /// </summary>
    public class HideSpotOccupancy : SensingFact
    {
        public string HideSpotId { get; set; }
        public string EntryId { get; set; }
        public Vector3 InteriorPosition { get; set; }
        public bool IsOccupied { get; set; }
    }

    /// <summary>
    /// Legacy mutable hearing shape retained only for old fixtures. Authoritative
    /// runtime consumers must use NoiseHeardRelay instead.
    /// </summary>
    [Obsolete("Use NoisePublished plus NoiseHeardRelay; this is a compatibility adapter only.")]
    public class NoiseHeard : SensingFact
    {
        public string Kind { get; set; }
        public Vector3 Position { get; set; }
        public string EntryId { get; set; }
        public bool IsFirstConsumption { get; set; }
        public ulong FactId { get; set; }
        public string GuardEid { get; set; }
    }

    public class ThresholdCrossing : SensingFact
    {
        public string EntryId { get; set; }
        public float ResidualR { get; set; }
        public string ThresholdState { get; set; }
    }

    public class ConfirmWindowElapsed : SensingFact
    {
        public string EntryId { get; set; }
        public float ResidualR { get; set; }
        public string ThresholdState { get; set; }
    }

    public class CapReached : SensingFact
    {
        public string EntryId { get; set; }
        public int Counter { get; set; }
        public Vector3 Position { get; set; }
    }

    public class Reachability : SensingFact
    {
        public string EntryId { get; set; }
        public string GuardEid { get; set; }
        public bool IsReachable { get; set; }
        public float PathArrivalM { get; set; }
        public float DeltaYM { get; set; }
        public Vector3 Position { get; set; }
    }

    public class ChaseReached : SensingFact
    {
        public string EntryId { get; set; }
        public float ResidualR { get; set; }
        public string ThresholdState { get; set; }
        public Vector3 Position { get; set; }
    }

    /// <summary>
    /// All decision records produced by the Guard FSM.
    /// </summary>
    public abstract class DecisionRecord : IEvent, IEventMetadataWriter, IEventPhase
    {
        public string SessionId { get; set; }
        public long AttemptEpoch { get; set; }
        public float Timestamp { get; set; }
        public string Publisher { get { return "GuardFSM"; } }
        public string Identity { get; set; }
        public EventBusPhase Phase { get { return EventBusPhase.Presentation; } }

        protected DecisionRecord()
        {
            SessionId = string.Empty;
            Identity = string.Empty;
        }

        public void SetEnvelope(string sessionId, long attemptEpoch, string identity)
        {
            SessionId = sessionId ?? string.Empty;
            AttemptEpoch = attemptEpoch;
            Identity = identity ?? string.Empty;
        }
    }

    public class InvestigateCommit : DecisionRecord
    {
        public string EntryId { get; set; }
        public float ResidualR { get; set; }
        public string Cause { get; set; }
        public Vector3 Position { get; set; }
        public string ThresholdState { get; set; }
        public bool IsHideEntry { get; set; }
    }

    public class ChaseEntry : DecisionRecord
    {
        public string EntryId { get; set; }
        public string Cause { get; set; }
        public string ThresholdState { get; set; }
        public Vector3 Position { get; set; }
        public bool PairedChaseReached { get; set; }
    }

    public class ChaseEnd : DecisionRecord
    {
        public string EntryId { get; set; }
        public GameObject Guard { get; set; }
    }

    public class InvestigateResolution : DecisionRecord
    {
        public string EntryId { get; set; }
        public string Cause { get; set; }
        public Vector3 Position { get; set; }
    }

    public class Capture : DecisionRecord
    {
        public string EntryId { get; set; }
        public Vector3 Position { get; set; }
        public GameObject Victim { get; set; }
        public bool IsCarveOut { get; set; }
    }
}
