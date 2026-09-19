using System;
using UnityEngine;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Authoritative source class for deterministic raw-noise ordering.
    /// Movement precedes Burst when source timestamps are equal.
    /// </summary>
    public enum NoiseSourceKind
    {
        Movement = 0,
        Burst = 1,
        Legacy = 2
    }

    /// <summary>
    /// Immutable source submission owned by the producer of a noise fact. The
    /// record carries its authoritative origin and provenance into NoiseEmitter;
    /// no listener arrival order is part of the record.
    /// </summary>
    public sealed class NoiseSourceRecord
    {
        /// <summary>
        /// Creates a source record with an explicit source rank and provenance.
        /// </summary>
        public NoiseSourceRecord(string sourceEventId, NoiseSourceKind sourceKind,
            string kind, string publisher, string provenance, Vector3 origin,
            float radius, float sourceTimestamp)
            : this(sourceEventId, sourceKind, kind, publisher, provenance, origin,
                radius, sourceTimestamp, origin, string.Empty, string.Empty, 0f, 0f,
                string.Empty, 0, null)
        {
        }

        /// <summary>
        /// Creates a source record with controller stride provenance. The optional
        /// metadata is populated only by the controller-owned movement adapter.
        /// </summary>
        public NoiseSourceRecord(string sourceEventId, NoiseSourceKind sourceKind,
            string kind, string publisher, string provenance, Vector3 origin,
            float radius, float sourceTimestamp, Vector3 previousPosition,
            string sourceState, string strideTargetState, float strideLengthMeters,
            float ledgerRemainderMeters)
            : this(sourceEventId, sourceKind, kind, publisher, provenance, origin,
                radius, sourceTimestamp, previousPosition, sourceState,
                strideTargetState, strideLengthMeters, ledgerRemainderMeters,
                string.Empty, 0, null)
        {
        }

        private NoiseSourceRecord(string sourceEventId, NoiseSourceKind sourceKind,
            string kind, string publisher, string provenance, Vector3 origin,
            float radius, float sourceTimestamp, Vector3 previousPosition,
            string sourceState, string strideTargetState, float strideLengthMeters,
            float ledgerRemainderMeters, string sessionId, long attemptEpoch,
            ulong? burstFlightHandleId, float? terminalPublicationTime = null)
        {
            string canonicalSourceEventId = CanonicalSourceEventId(
                sourceEventId, sourceKind);
            if (string.IsNullOrWhiteSpace(canonicalSourceEventId))
                throw new ArgumentException("sourceEventId");
            if (sourceKind != NoiseSourceKind.Legacy)
            {
                string sourceIdError;
                if (!NoiseSourceOrdering.TryValidateSourceEventId(
                    canonicalSourceEventId, sourceKind, out sourceIdError))
                    throw new ArgumentException(sourceIdError, nameof(sourceEventId));
            }
            if (string.IsNullOrWhiteSpace(kind))
                throw new ArgumentException("kind");
            if (string.IsNullOrWhiteSpace(publisher))
                throw new ArgumentException("publisher");
            if (string.IsNullOrWhiteSpace(provenance))
                throw new ArgumentException("provenance");
            if (sourceKind != NoiseSourceKind.Movement
                && sourceKind != NoiseSourceKind.Burst
                && sourceKind != NoiseSourceKind.Legacy)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceKind));
            }
            if (!IsFinite(origin.x) || !IsFinite(origin.y) || !IsFinite(origin.z))
                throw new ArgumentException("origin");
            if (!IsFinite(previousPosition.x) || !IsFinite(previousPosition.y)
                || !IsFinite(previousPosition.z))
                throw new ArgumentException("previousPosition");
            if (!IsFiniteNonNegative(strideLengthMeters)
                || !IsFiniteNonNegative(ledgerRemainderMeters))
                throw new ArgumentException("movement-ledger");
            if (!IsFinitePositive(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (!IsFinite(sourceTimestamp))
                throw new ArgumentException("sourceTimestamp");
            if (sourceKind == NoiseSourceKind.Burst
                && (string.IsNullOrWhiteSpace(sessionId) || attemptEpoch < 0
                    || !burstFlightHandleId.HasValue || burstFlightHandleId.Value == 0))
                throw new ArgumentException("burst-envelope");

            SourceEventId = canonicalSourceEventId;
            SourceKind = sourceKind;
            Kind = kind;
            Publisher = publisher;
            Provenance = provenance;
            Origin = origin;
            Radius = radius;
            SourceTimestamp = sourceTimestamp;
            TerminalPublicationTime = sourceKind == NoiseSourceKind.Burst
                ? terminalPublicationTime : (float?)null;
            if (sourceKind == NoiseSourceKind.Burst
                && (!TerminalPublicationTime.HasValue
                    || !IsFinite(TerminalPublicationTime.Value)))
                throw new ArgumentException("terminalPublicationTime");
            if (sourceKind == NoiseSourceKind.Burst
                && TerminalPublicationTime.Value < sourceTimestamp)
                throw new ArgumentException("terminalPublicationTime-order");
            PreviousPosition = previousPosition;
            SourceState = sourceState ?? string.Empty;
            StrideTargetState = strideTargetState ?? string.Empty;
            StrideLengthMeters = strideLengthMeters;
            LedgerRemainderMeters = ledgerRemainderMeters;
            SessionId = sessionId ?? string.Empty;
            AttemptEpoch = attemptEpoch;
            BurstFlightHandleId = burstFlightHandleId;
        }

        /// <summary>Stable producer-owned event identity.</summary>
        public string SourceEventId { get; }

        /// <summary>Authoritative source class used by the ordering contract.</summary>
        public NoiseSourceKind SourceKind { get; }

        /// <summary>Stable source rank: Movement 0, Burst 1, Legacy 2.</summary>
        public int SourceEventClassRank { get { return (int)SourceKind; } }

        /// <summary>Gameplay kind exposed to hearing and diagnostics.</summary>
        public string Kind { get; }

        /// <summary>Producer name carried into the event envelope.</summary>
        public string Publisher { get; }

        /// <summary>
        /// Gameplay source attribution carried separately from the producer name.
        /// </summary>
        public string Source
        {
            get
            {
                if (SourceKind == NoiseSourceKind.Movement) return "player";
                if (SourceKind == NoiseSourceKind.Burst) return "burst-landing";
                return Publisher;
            }
        }

        /// <summary>Authoritative origin/contact provenance label.</summary>
        public string Provenance { get; }

        /// <summary>Authoritative world-space origin used by hearing.</summary>
        public Vector3 Origin { get; }

        /// <summary>Published hearing radius in metres.</summary>
        public float Radius { get; }

        /// <summary>Producer-captured source timestamp in virtual seconds.</summary>
        public float SourceTimestamp { get; }

        /// <summary>Virtual time at which a terminal Burst becomes publishable.</summary>
        public float? TerminalPublicationTime { get; }

        /// <summary>Canonical source-kind publication time used by hearing deadlines.</summary>
        public float TPublish
        {
            get
            {
                if (SourceKind != NoiseSourceKind.Burst)
                    return SourceTimestamp;
                // The Burst factory contract guarantees a finite terminal
                // publication time; a null here means an adapter bypassed the
                // constructor's fail-closed validation. Fail closed with an
                // explicit diagnostic instead of an unguarded .Value throw.
                if (!TerminalPublicationTime.HasValue)
                    throw new InvalidOperationException(
                        "noise-source-burst-terminal-publication-time-missing");
                return TerminalPublicationTime.Value;
            }
        }

        /// <summary>Controller position immediately before the committed step.</summary>
        public Vector3 PreviousPosition { get; }

        /// <summary>Controller state at the source event.</summary>
        public string SourceState { get; }

        /// <summary>Latched Walk or Run state used to select radius.</summary>
        public string StrideTargetState { get; }

        /// <summary>Committed stride distance in metres.</summary>
        public float StrideLengthMeters { get; }

        /// <summary>Controller-owned retained ledger remainder in metres.</summary>
        public float LedgerRemainderMeters { get; }

        /// <summary>Source session envelope, when supplied by an authoritative producer.</summary>
        public string SessionId { get; }

        /// <summary>Source attempt epoch, when supplied by an authoritative producer.</summary>
        public long AttemptEpoch { get; }

        /// <summary>Typed Burst handle, without downstream string parsing.</summary>
        public ulong? BurstFlightHandleId { get; }

        /// <summary>Creates a movement source with the controller-owned feet origin.</summary>
        public static NoiseSourceRecord Movement(string sourceEventId,
            string publisher, Vector3 feetOrigin, float radius, float sourceTimestamp,
            string provenance)
        {
            return new NoiseSourceRecord(CanonicalSourceId("step:", sourceEventId),
                NoiseSourceKind.Movement, "movement", publisher, provenance,
                feetOrigin, radius, sourceTimestamp);
        }

        /// <summary>
        /// Creates a complete controller stride-commit source. Radius selection is
        /// intentionally performed by the caller from the latched target state.
        /// </summary>
        public static NoiseSourceRecord Movement(string stepId, string publisher,
            string sourceState, string strideTargetState, Vector3 previousPosition,
            Vector3 feetPosition, float strideLengthMeters,
            float ledgerRemainderMeters, float radius, float sourceTimestamp)
        {
            if (!string.Equals(strideTargetState, "Walk", StringComparison.Ordinal)
                && !string.Equals(strideTargetState, "Run", StringComparison.Ordinal))
                throw new ArgumentException("strideTargetState");
            if (!IsFinitePositive(strideLengthMeters))
                throw new ArgumentOutOfRangeException(nameof(strideLengthMeters));
            if (!HasPositivePlanarDisplacement(previousPosition, feetPosition))
                throw new ArgumentException("step-displacement");
            return new NoiseSourceRecord(CanonicalSourceId("step:", stepId),
                NoiseSourceKind.Movement, "movement", publisher, "controller-feet", feetPosition, radius,
                sourceTimestamp, previousPosition, sourceState, strideTargetState,
                strideLengthMeters, ledgerRemainderMeters);
        }

        /// <summary>
        /// Creates a movement source with the lifecycle envelope supplied by the
        /// controller-owned session boundary.
        /// </summary>
        public static NoiseSourceRecord Movement(string sessionId,
            long attemptEpoch, string stepId, string publisher,
            string sourceState, string strideTargetState,
            Vector3 previousPosition, Vector3 feetPosition,
            float strideLengthMeters, float ledgerRemainderMeters,
            float radius, float sourceTimestamp)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || attemptEpoch < 0)
                throw new ArgumentException("movement-envelope");
            if (!string.Equals(strideTargetState, "Walk", StringComparison.Ordinal)
                && !string.Equals(strideTargetState, "Run", StringComparison.Ordinal))
                throw new ArgumentException("strideTargetState");
            if (!IsFinitePositive(strideLengthMeters))
                throw new ArgumentOutOfRangeException(nameof(strideLengthMeters));
            if (!HasPositivePlanarDisplacement(previousPosition, feetPosition))
                throw new ArgumentException("step-displacement");
            return new NoiseSourceRecord(CanonicalSourceId("step:", stepId),
                NoiseSourceKind.Movement, "movement", publisher,
                "controller-feet", feetPosition, radius, sourceTimestamp,
                previousPosition, sourceState, strideTargetState,
                strideLengthMeters, ledgerRemainderMeters, sessionId,
                attemptEpoch, null);
        }

        /// <summary>
        /// Creates a Burst source with the lifecycle envelope and authoritative
        /// numeric flight handle. The handle is converted to transport text once.
        /// </summary>
        public static NoiseSourceRecord Burst(string sessionId, long attemptEpoch,
            ulong flightHandleId, string publisher, Vector3 landingContact,
            float radius, float sourceTimestamp, float terminalPublicationTime,
            string provenance)
        {
            if (flightHandleId == 0)
                throw new ArgumentOutOfRangeException(nameof(flightHandleId));
            return new NoiseSourceRecord("flight:" + flightHandleId.ToString("D"),
                NoiseSourceKind.Burst, "burst", publisher, provenance,
                landingContact, radius, sourceTimestamp, landingContact,
                string.Empty, string.Empty, 0f, 0f, sessionId, attemptEpoch,
                flightHandleId, terminalPublicationTime);
        }

        /// <summary>Compatibility overload for terminal time equal to source time.</summary>
        [Obsolete("Use the terminal-time Burst factory.")]
        public static NoiseSourceRecord Burst(string sessionId, long attemptEpoch,
            ulong flightHandleId, string publisher, Vector3 landingContact,
            float radius, float sourceTimestamp, string provenance)
        {
            return Burst(sessionId, attemptEpoch, flightHandleId, publisher,
                landingContact, radius, sourceTimestamp, sourceTimestamp, provenance);
        }

        /// <summary>
        /// Compatibility adapter for old fixtures. The adapter intentionally
        /// emits a Legacy record because an old call has no authoritative Burst
        /// session envelope or numeric flight handle.
        /// </summary>
        [Obsolete("Use the envelope-aware Burst factory.")]
        public static NoiseSourceRecord Burst(string sourceEventId,
            string publisher, Vector3 landingContact, float radius,
            float sourceTimestamp, string provenance)
        {
            return Legacy(sourceEventId, "Burst", publisher, landingContact,
                radius, sourceTimestamp);
        }

        /// <summary>
        /// Creates a compatibility source for old fixtures. New gameplay code must
        /// use Movement or Burst so rank and provenance remain explicit.
        /// </summary>
        public static NoiseSourceRecord Legacy(string sourceEventId, string kind,
            string publisher, Vector3 origin, float radius, float sourceTimestamp)
        {
            return new NoiseSourceRecord(sourceEventId, NoiseSourceKind.Legacy,
                kind, publisher, "legacy-adapter", origin, radius, sourceTimestamp);
        }

        private static string CanonicalSourceEventId(
            string sourceEventId, NoiseSourceKind sourceKind)
        {
            if (sourceKind == NoiseSourceKind.Movement)
                return CanonicalSourceId("step:", sourceEventId);
            if (sourceKind == NoiseSourceKind.Burst)
                return CanonicalSourceId("flight:", sourceEventId);
            return sourceEventId;
        }

        private static string CanonicalSourceId(string prefix, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.StartsWith(prefix, StringComparison.Ordinal)
                ? value : prefix + value;
        }

        private static bool HasPositivePlanarDisplacement(Vector3 previous,
            Vector3 current)
        {
            float dx = current.x - previous.x;
            float dz = current.z - previous.z;
            if (!IsFinite(dx) || !IsFinite(dz)) return false;
            float planarMagnitude = (float)Math.Sqrt((double)dx * dx
                + (double)dz * dz);
            return IsFinite(planarMagnitude) && planarMagnitude > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }
    }
}
