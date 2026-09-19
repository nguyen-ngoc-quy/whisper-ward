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
        /// <summary>Public Player Noise contract member.</summary>
        public string SessionId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public long AttemptEpoch { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float Timestamp { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Publisher { get { return "Perception"; } }
        /// <summary>Public Player Noise contract member.</summary>
        public string Identity { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public EventBusPhase Phase { get { return EventBusPhase.FsmDecision; } }

        protected SensingFact()
        {
            SessionId = string.Empty;
            Identity = string.Empty;
        }

        /// <summary>Public API member for the Player Noise contract.</summary>
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

        /// <summary>Public Player Noise contract member.</summary>
        public string SessionId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public long AttemptEpoch { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float Timestamp { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public virtual string Publisher { get { return "Perception"; } }
        /// <summary>Public Player Noise contract member.</summary>
        public string Identity { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public EventBusPhase Phase { get { return EventBusPhase.FsmDecision; } }
    }

    /// <summary>
    /// Immutable raw publication owned by NoiseEmitter. One fact is shared by all
    /// guards that hear it; Perception adds per-guard relay and episode identity.
    /// </summary>
    public sealed class NoisePublished : IEvent, IEventPhase,
        IEventDeduplicationKey, IEventSourceOrdering
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
            float? terminalPublicationTime = null, ulong? flightHandleId = null,
            string movementMode = null, string publicationState = "published")
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId");
            if (factId == 0) throw new ArgumentOutOfRangeException(nameof(factId));
            if (string.IsNullOrWhiteSpace(sourceEventId)) throw new ArgumentException("sourceEventId");
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("kind");
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("source");
            if (sourceEventClassRank < 0) throw new ArgumentOutOfRangeException(nameof(sourceEventClassRank));
            string effectiveMovementMode = movementMode ?? sourceState ?? string.Empty;
            if (sourceEventClassRank == (int)NoiseSourceKind.Movement
                && (!string.Equals(effectiveMovementMode, "Walk", StringComparison.Ordinal)
                    && !string.Equals(effectiveMovementMode, "Run", StringComparison.Ordinal)))
                throw new ArgumentException("movementMode");
            if (sourceEventClassRank == (int)NoiseSourceKind.Movement
                || sourceEventClassRank == (int)NoiseSourceKind.Burst)
            {
                string sourceIdError;
                if (!NoiseSourceOrdering.TryValidateSourceEventId(
                    sourceEventId, (NoiseSourceKind)sourceEventClassRank,
                    out sourceIdError))
                    throw new ArgumentException(sourceIdError,
                        nameof(sourceEventId));
            }
            if (float.IsNaN(sourceTimestamp) || float.IsInfinity(sourceTimestamp))
                throw new ArgumentException("sourceTimestamp");
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (sourceEventClassRank == (int)NoiseSourceKind.Burst
                && (!flightHandleId.HasValue || flightHandleId.Value == 0
                    || !string.Equals(sourceEventId,
                        "flight:" + flightHandleId.Value.ToString("D"),
                        StringComparison.Ordinal)))
                throw new ArgumentException("burst-flight-identity");
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
            Kind = sourceEventClassRank == (int)NoiseSourceKind.Movement
                ? "movement" : sourceEventClassRank == (int)NoiseSourceKind.Burst
                    ? "burst" : kind;
            Source = source;
            Position = position;
            Radius = radius;
            ResolvedNominalRadius = radius;
            SourceTimestamp = sourceTimestamp;
            bool isBurst = sourceEventClassRank == (int)NoiseSourceKind.Burst;
            if (isBurst && !terminalPublicationTime.HasValue)
                throw new ArgumentException("terminalPublicationTime");
            if (!isBurst && terminalPublicationTime.HasValue)
                throw new ArgumentException("terminalPublicationTime-not-applicable");
            TerminalPublicationTime = terminalPublicationTime;
            TPublish = isBurst ? terminalPublicationTime.Value : SourceTimestamp;
            FlightHandleId = flightHandleId;
            MovementMode = effectiveMovementMode;
            if (!string.Equals(publicationState, "published",
                    StringComparison.Ordinal)
                && !string.Equals(publicationState, "rejected",
                    StringComparison.Ordinal)
                && !string.Equals(publicationState, "invalidated",
                    StringComparison.Ordinal))
                throw new ArgumentException("publicationState");
            PublicationState = publicationState;
            if (terminalPublicationTime.HasValue
                && (float.IsNaN(terminalPublicationTime.Value)
                    || float.IsInfinity(terminalPublicationTime.Value)))
                throw new ArgumentException("terminalPublicationTime");
            if (isBurst && terminalPublicationTime.Value < sourceTimestamp)
                throw new ArgumentException("terminalPublicationTime-order");
            SourceEventClassRank = sourceEventClassRank;
            AuthoritativeOrigin = authoritativeOrigin;
            OriginProvenance = originProvenance;
            PreviousPosition = previousPosition;
            SourceState = sourceState ?? string.Empty;
            StrideTargetState = strideTargetState ?? string.Empty;
            StrideLengthMeters = strideLengthMeters;
            LedgerRemainderMeters = ledgerRemainderMeters;
        }

        /// <summary>Public Player Noise contract member.</summary>
        public string SessionId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public long AttemptEpoch { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public ulong FactId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string SourceEventId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Kind { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Source { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float Radius { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float SourceTimestamp { get; }
        /// <summary>
        /// Terminal publication time for Burst; null for Movement facts.
        /// </summary>
        public float? TerminalPublicationTime { get; }
        /// <summary>Resolved source-kind publication time for deadlines.</summary>
        public float TPublish { get; }
        /// <summary>Typed Burst identity, when this is a Burst fact.</summary>
        public ulong? FlightHandleId { get; }
        /// <summary>Authoritative movement mode captured by the producer.</summary>
        public string MovementMode { get; }
        /// <summary>Resolved nominal radius used by hearing.</summary>
        public float ResolvedNominalRadius { get; }
        /// <summary>Raw publication state carried without downstream synthesis.</summary>
        public string PublicationState { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public int SourceEventClassRank { get; }
        /// <summary>Fact identity used as the final source-order tie-breaker.</summary>
        public ulong SourceFactId { get { return FactId; } }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 AuthoritativeOrigin { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string OriginProvenance { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 PreviousPosition { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string SourceState { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string StrideTargetState { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float StrideLengthMeters { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float LedgerRemainderMeters { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float Timestamp { get { return SourceTimestamp; } }
        /// <summary>Public Player Noise contract member.</summary>
        public string Publisher { get { return "NoiseEmitter"; } }
        /// <summary>Public API member for the Player Noise contract.</summary>
        public string Identity { get { return FactId.ToString("D"); } }
        /// <summary>Public API member for the Player Noise contract.</summary>
        public string DeduplicationIdentity { get { return FactId.ToString("D"); } }
        /// <summary>Public Player Noise contract member.</summary>
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
    public sealed class NoiseHeardRelay : ImmutableSensingFact,
        IEventSourceOrdering
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
                null)
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
            float? terminalPublicationTime = null, string sourceEventId = null,
            ulong? flightHandleId = null, string movementMode = null,
            float? resolvedNominalRadius = null, string publicationState = "published")
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
            if (residualAtHearing < 0f
                || residualAtHearing > NoiseRuntimeConfiguration.RegisteredReanchorMaxMeters)
                throw new ArgumentOutOfRangeException(nameof(residualAtHearing));
            if (string.IsNullOrWhiteSpace(originProvenance))
                throw new ArgumentException("originProvenance");

            FactId = factId;
            EntryId = entryId;
            GuardEid = guardEid;
            Kind = sourceEventClassRank == (int)NoiseSourceKind.Movement
                ? "movement" : sourceEventClassRank == (int)NoiseSourceKind.Burst
                    ? "burst" : kind ?? string.Empty;
            Source = source ?? string.Empty;
            Position = position;
            Radius = radius;
            ResolvedNominalRadius = resolvedNominalRadius ?? radius;
            SourceEventClassRank = sourceEventClassRank;
            OriginProvenance = originProvenance;
            AuthoritativeOrigin = authoritativeOrigin;
            SourceTimestamp = sourceTimestamp;
            bool isBurst = sourceEventClassRank == (int)NoiseSourceKind.Burst;
            if (isBurst && !terminalPublicationTime.HasValue)
                throw new ArgumentException("terminalPublicationTime");
            if (!isBurst && terminalPublicationTime.HasValue)
                throw new ArgumentException("terminalPublicationTime-not-applicable");
            TerminalPublicationTime = terminalPublicationTime;
            TPublish = isBurst ? terminalPublicationTime.Value : SourceTimestamp;
            SourceEventId = sourceEventId ?? (isBurst
                ? "flight:" + factId.ToString("D") : "step:" + factId.ToString("D"));
            if (sourceEventClassRank == (int)NoiseSourceKind.Movement
                || sourceEventClassRank == (int)NoiseSourceKind.Burst)
            {
                string sourceIdError;
                if (!NoiseSourceOrdering.TryValidateSourceEventId(
                    SourceEventId, (NoiseSourceKind)sourceEventClassRank,
                    out sourceIdError))
                    throw new ArgumentException(sourceIdError,
                        nameof(sourceEventId));
            }
            if (isBurst && (!flightHandleId.HasValue || flightHandleId.Value == 0
                    || !string.Equals(SourceEventId,
                        "flight:" + flightHandleId.Value.ToString("D"),
                        StringComparison.Ordinal)))
                throw new ArgumentException("burst-flight-identity");
            FlightHandleId = flightHandleId;
            MovementMode = movementMode ?? string.Empty;
            if (!string.Equals(publicationState, "published",
                    StringComparison.Ordinal)
                && !string.Equals(publicationState, "rejected",
                    StringComparison.Ordinal)
                && !string.Equals(publicationState, "invalidated",
                    StringComparison.Ordinal))
                throw new ArgumentException("publicationState");
            PublicationState = publicationState;
            if (terminalPublicationTime.HasValue
                && (float.IsNaN(terminalPublicationTime.Value)
                    || float.IsInfinity(terminalPublicationTime.Value)))
                throw new ArgumentException("terminalPublicationTime");
            if (isBurst && terminalPublicationTime.Value < SourceTimestamp)
                throw new ArgumentException("terminalPublicationTime-order");
            EvaluatedAt = evaluatedAt;
            ResidualAtHearing = residualAtHearing;
            Consumption = RelayConsumption.Eligible;
        }

        /// <summary>Public Player Noise contract member.</summary>
        public ulong FactId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string GuardEid { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Kind { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Source { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float Radius { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public int SourceEventClassRank { get; }
        /// <summary>Fact identity used as the final source-order tie-breaker.</summary>
        public ulong SourceFactId { get { return FactId; } }
        /// <summary>Public Player Noise contract member.</summary>
        public string OriginProvenance { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 AuthoritativeOrigin { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float SourceTimestamp { get; }
        /// <summary>Namespaced producer identity preserved on the relay.</summary>
        public string SourceEventId { get; }
        /// <summary>Terminal virtual time used as the hearing deadline anchor.</summary>
        public float? TerminalPublicationTime { get; }
        /// <summary>Resolved source-kind publication time for corroboration.</summary>
        public float TPublish { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public ulong? FlightHandleId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string MovementMode { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float ResolvedNominalRadius { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string PublicationState { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float EvaluatedAt { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float ResidualAtHearing { get; }
        /// <summary>Public Player Noise contract member.</summary>
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
        /// <summary>Public API member for the Player Noise contract.</summary>
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

        /// <summary>Public Player Noise contract member.</summary>
        public string SessionId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public long AttemptEpoch { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public ulong FactId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string GuardEid { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public RelayConsumption Consumption { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float Timestamp { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Publisher { get { return "GuardFSM"; } }
        /// <summary>Public API member for the Player Noise contract.</summary>
        public string Identity { get { return FactId.ToString("D") + ":" + GuardEid; } }
        /// <summary>Public Player Noise contract member.</summary>
        public EventBusPhase Phase { get { return EventBusPhase.Presentation; } }
    }

    /// <summary>
    /// Immutable receipt explaining why an eligible relay was suppressed by the
    /// FSM. It is presentation telemetry only and never creates gameplay liveness.
    /// </summary>
    public sealed class NoiseSuppressedReceipt : IEvent, IEventPhase
    {
        /// <summary>Public API member for the Player Noise contract.</summary>
        public NoiseSuppressedReceipt(string sessionId, long attemptEpoch,
            ulong factId, string guardEid, string entryId, string reason,
            bool visibleToPlayer, bool microTellEmitted, float timestamp)
            : this(sessionId, attemptEpoch, factId, guardEid, entryId, reason,
                visibleToPlayer, microTellEmitted, timestamp, string.Empty,
                string.Empty, "legacy-adapter", "legacy-adapter", timestamp)
        {
        }

        /// <summary>Public API member for the Player Noise contract.</summary>
        public NoiseSuppressedReceipt(string sessionId, long attemptEpoch,
            ulong factId, string guardEid, string entryId, string reason,
            bool visibleToPlayer, bool microTellEmitted, float timestamp,
            string visibilityQueryId, string visibilityQueryProvenance)
            : this(sessionId, attemptEpoch, factId, guardEid, entryId, reason,
                visibleToPlayer, microTellEmitted, timestamp, visibilityQueryId,
                visibilityQueryProvenance, "legacy-adapter", "legacy-adapter",
                timestamp)
        {
        }

        /// <summary>Creates a suppression receipt with relay provenance copied verbatim.</summary>
        public NoiseSuppressedReceipt(string sessionId, long attemptEpoch,
            ulong factId, string guardEid, string entryId, string reason,
            bool visibleToPlayer, bool microTellEmitted, float timestamp,
            string visibilityQueryId, string visibilityQueryProvenance,
            string sourceKind, string sourceEventId, float tPublish)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId");
            if (factId == 0) throw new ArgumentOutOfRangeException(nameof(factId));
            if (string.IsNullOrWhiteSpace(guardEid)) throw new ArgumentException("guardEid");
            if (string.IsNullOrWhiteSpace(entryId)) throw new ArgumentException("entryId");
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("reason");
            if (string.IsNullOrWhiteSpace(sourceKind)) throw new ArgumentException("sourceKind");
            if (string.IsNullOrWhiteSpace(sourceEventId)) throw new ArgumentException("sourceEventId");
            if (float.IsNaN(timestamp) || float.IsInfinity(timestamp))
                throw new ArgumentException("timestamp");
            if (float.IsNaN(tPublish) || float.IsInfinity(tPublish))
                throw new ArgumentException("tPublish");
            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            FactId = factId;
            GuardEid = guardEid;
            EntryId = entryId;
            SuppressionReason = reason;
            VisibleToPlayer = visibleToPlayer;
            MicroTellEmitted = microTellEmitted;
            Timestamp = timestamp;
            VisibilityQueryId = visibilityQueryId ?? string.Empty;
            VisibilityQueryProvenance = visibilityQueryProvenance ?? string.Empty;
            SourceKind = sourceKind;
            SourceEventId = sourceEventId;
            TPublish = tPublish;
        }

        /// <summary>Public Player Noise contract member.</summary>
        public string SessionId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public long AttemptEpoch { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public ulong FactId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string GuardEid { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string SuppressionReason { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool VisibleToPlayer { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool MicroTellEmitted { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float Timestamp { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string VisibilityQueryId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string VisibilityQueryProvenance { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string SourceKind { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string SourceEventId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float TPublish { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Publisher { get { return "GuardAISystem"; } }
        /// <summary>Public API member for the Player Noise contract.</summary>
        public string Identity { get { return FactId.ToString("D") + ":" + GuardEid + ":suppressed"; } }
        /// <summary>Public Player Noise contract member.</summary>
        public EventBusPhase Phase { get { return EventBusPhase.Presentation; } }
    }

    /// <summary>Closed vocabulary for authoritative liveness positions.</summary>
    public static class LivenessPositionSources
    {
        /// <summary>Public Player Noise contract member.</summary>
        public const string OpenerDecision = "opener_decision";
        /// <summary>Public Player Noise contract member.</summary>
        public const string EpisodePosition = "episode_position";
        /// <summary>Public Player Noise contract member.</summary>
        public const string TerminalDecision = "terminal_decision";
        /// <summary>Public Player Noise contract member.</summary>
        public const string LastAuthoritativeEpisodePosition =
            "last_authoritative_episode_position";

        // Compatibility names retain the canonical serialized vocabulary.
        /// <summary>Public Player Noise contract member.</summary>
        public const string GuardTransform = OpenerDecision;
        /// <summary>Public Player Noise contract member.</summary>
        public const string EpisodeAnchor = EpisodePosition;
        /// <summary>Public Player Noise contract member.</summary>
        public const string ChaseEntry = OpenerDecision;
        /// <summary>Public Player Noise contract member.</summary>
        public const string ChasePromotion = EpisodePosition;
        /// <summary>Public Player Noise contract member.</summary>
        public const string LifecycleStaleClose = LastAuthoritativeEpisodePosition;

        /// <summary>Public API member for the Player Noise contract.</summary>
        public static bool IsCanonical(string value)
        {
            return string.Equals(value, OpenerDecision, StringComparison.Ordinal)
                || string.Equals(value, EpisodePosition, StringComparison.Ordinal)
                || string.Equals(value, TerminalDecision, StringComparison.Ordinal)
                || string.Equals(value, LastAuthoritativeEpisodePosition,
                    StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Immutable FSM-owned liveness transition. Perception reads the last
    /// published fact at a boundary; it does not synchronously query FSM state.
    /// </summary>
    public sealed class LivenessFact : ImmutableSensingFact,
        IStaleGenerationHandoff
    {
        /// <summary>Compatibility constructor for legacy liveness fixtures.</summary>
        public LivenessFact(string sessionId, long attemptEpoch, string operation,
            string tier, string cause, float sourceTimestamp, float evaluatedAt,
            string guardEid, string entryId)
            : this(sessionId, attemptEpoch, operation, tier, cause, sourceTimestamp,
                evaluatedAt, guardEid, entryId, Vector3.zero,
                LivenessPositionSources.GuardTransform)
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
            if (!LivenessPositionSources.IsCanonical(positionSource))
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

        /// <summary>Public Player Noise contract member.</summary>
        public override string Publisher { get { return "GuardAISystem"; } }
        /// <summary>Public Player Noise contract member.</summary>
        public string Operation { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Tier { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Cause { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float SourceTimestamp { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public float EvaluatedAt { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string GuardEid { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public string PositionSource { get; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool RetainAcrossGenerationBarrier
        {
            get { return string.Equals(Operation, "close", StringComparison.Ordinal)
                && string.Equals(Cause, "stale", StringComparison.Ordinal); }
        }

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
        /// <summary>Public Player Noise contract member.</summary>
        public string GuardEid { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool HasLOS { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 PlayerPosition { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string LiveEntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string GoalMode { get; set; }

        /// <summary>Public API member for the Player Noise contract.</summary>
        public LivenessSnapshot()
        {
            GuardEid = string.Empty;
            LiveEntryId = string.Empty;
            GoalMode = string.Empty;
        }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class LOSGain : SensingFact
    {
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float Distance { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public GameObject Guard { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string GuardEid { get; set; }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class LOSBreak : SensingFact
    {
        /// <summary>Public Player Noise contract member.</summary>
        public float ResidualR { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string BreakType { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string CausalClass { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float GuardYawDelta { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string GuardEid { get; set; }

        // Populated only for the amended witnessed hide-entry gate. The payload is
        // retained on the fact so the FSM never reconstructs the spot or threshold.
        /// <summary>Public Player Noise contract member.</summary>
        public bool IsHideEntry { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float APreBreak { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float ThresholdApplied { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 SpotPosition { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 SpotFrontPosition { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool HasSpotFrontPosition { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string HideSpotId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
    }

    /// <summary>
    /// Authoritative player-occupancy transition for a hide spot. The HideSpot
    /// owner publishes this fact; the FSM never infers occupancy from absent LOS.
    /// </summary>
    public class HideSpotOccupancy : SensingFact
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string HideSpotId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 InteriorPosition { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool IsOccupied { get; set; }
    }

    /// <summary>
    /// Legacy mutable hearing shape retained only for old fixtures. Authoritative
    /// runtime consumers must use NoiseHeardRelay instead.
    /// </summary>
    [Obsolete("Use NoisePublished plus NoiseHeardRelay; this is a compatibility adapter only.")]
    /// <summary>Public Player Noise contract type.</summary>
    public class NoiseHeard : SensingFact
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string Kind { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool IsFirstConsumption { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public ulong FactId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string GuardEid { get; set; }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class ThresholdCrossing : SensingFact
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float ResidualR { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string ThresholdState { get; set; }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class ConfirmWindowElapsed : SensingFact
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float ResidualR { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string ThresholdState { get; set; }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class CapReached : SensingFact
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public int Counter { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; set; }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class Reachability : SensingFact
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string GuardEid { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool IsReachable { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float PathArrivalM { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float DeltaYM { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; set; }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class ChaseReached : SensingFact
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float ResidualR { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string ThresholdState { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; set; }
    }

    /// <summary>
    /// All decision records produced by the Guard FSM.
    /// </summary>
    public abstract class DecisionRecord : IEvent, IEventMetadataWriter, IEventPhase
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string SessionId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public long AttemptEpoch { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float Timestamp { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Publisher { get { return "GuardFSM"; } }
        /// <summary>Public Player Noise contract member.</summary>
        public string Identity { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public EventBusPhase Phase { get { return EventBusPhase.Presentation; } }

        protected DecisionRecord()
        {
            SessionId = string.Empty;
            Identity = string.Empty;
        }

        /// <summary>Public API member for the Player Noise contract.</summary>
        public void SetEnvelope(string sessionId, long attemptEpoch, string identity)
        {
            SessionId = sessionId ?? string.Empty;
            AttemptEpoch = attemptEpoch;
            Identity = identity ?? string.Empty;
        }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class InvestigateCommit : DecisionRecord
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float ResidualR { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Cause { get; set; }
        /// <summary>
        /// Authoritative episode-open position. For noise this is the raw noise
        /// origin; for authored episodes it is the authored commit position.
        /// It is not the sampled navigation target.
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string ThresholdState { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool IsHideEntry { get; set; }
        /// <summary>Session envelope of the opening anchor.</summary>
        public string EpisodeAnchorSessionId { get; set; }
        /// <summary>Guard identity of the opening anchor.</summary>
        public string EpisodeAnchorGuardEid { get; set; }
        /// <summary>Noise fact identity, or zero for authored anchors.</summary>
        public ulong EpisodeAnchorFactId { get; set; }
        /// <summary>Source-kind publication time of the opening anchor.</summary>
        public float EpisodeAnchorTPublish { get; set; }
        /// <summary>
        /// Deterministically selected navigation target actually used by the
        /// episode. This may differ from Position after target sampling.
        /// </summary>
        public Vector3 TargetPosition { get; set; }
        /// <summary>Deterministic target-stream seed used for this commit.</summary>
        public uint TargetSeed { get; set; }
        /// <summary>Zero-based deterministic target sample index.</summary>
        public int TargetSampleIndex { get; set; }
        /// <summary>Target resolution path used by navigation.</summary>
        public string TargetResolution { get; set; }
    }

    /// <summary>
    /// Typed FSM outcome for qualifying same-episode corroboration. It retains
    /// the original entry identity and the corroborating relay provenance.
    /// </summary>
    public class NoiseReanchor : DecisionRecord
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public ulong CorroboratingFactId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float TPublish { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string SourceKind { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string SourceEventId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float? TerminalPublicationTime { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float ResidualAtHearing { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public float ReanchorExtensionSeconds { get; set; }
        /// <summary>Anchor kind retained by the live episode.</summary>
        public string EpisodeAnchorKind { get; set; }
        /// <summary>Authoritative position of the retained episode anchor.</summary>
        public Vector3 EpisodeAnchorPosition { get; set; }
        /// <summary>Publication time of the retained episode anchor.</summary>
        public float EpisodeAnchorTPublish { get; set; }
        /// <summary>
        /// Noise anchor fact identity, or zero for authored non-noise anchors.
        /// </summary>
        public ulong EpisodeAnchorFactId { get; set; }
        /// <summary>
        /// Stable non-noise anchor identity, normally the existing entry id.
        /// </summary>
        public string EpisodeAnchorIdentity { get; set; }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class ChaseEntry : DecisionRecord
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Cause { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string ThresholdState { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool PairedChaseReached { get; set; }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class ChaseEnd : DecisionRecord
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public GameObject Guard { get; set; }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class InvestigateResolution : DecisionRecord
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public string Cause { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; set; }
    }

    /// <summary>Public Player Noise contract type.</summary>
    public class Capture : DecisionRecord
    {
        /// <summary>Public Player Noise contract member.</summary>
        public string EntryId { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public Vector3 Position { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public GameObject Victim { get; set; }
        /// <summary>Public Player Noise contract member.</summary>
        public bool IsCarveOut { get; set; }
    }
}
