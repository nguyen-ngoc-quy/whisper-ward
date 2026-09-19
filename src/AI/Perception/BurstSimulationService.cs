using System;
using System.Globalization;
using UnityEngine;
using WhisperWard.AI.Core;

namespace WhisperWard.AI.Perception
{
    /// <summary>Lifecycle state of one limited Burst resource.</summary>
    public enum BurstFlightState
    {
        Placed,
        Carried,
        InFlight,
        Landed,
        Consumed
    }

    /// <summary>Terminal winner for a Burst flight.</summary>
    public enum BurstTerminalEvent
    {
        Contact,
        Timeout,
        DeathCancelled,
        BoundaryCancelled
    }

    /// <summary>Admission result for a pickup or Throw input edge.</summary>
    public enum BurstThrowAdmission
    {
        Accepted,
        Rejected
    }

    /// <summary>Authoritative locomotion label latched at the Throw edge.</summary>
    public enum BurstLocomotionLabel
    {
        Stationary,
        Walk,
        Run
    }

    /// <summary>Authoritative stance label latched at the Throw edge.</summary>
    public enum BurstStanceLabel
    {
        Standing,
        Crouched
    }

    /// <summary>Publication state of a Burst terminal's optional noise fact.</summary>
    public enum BurstFactPublicationState
    {
        NotApplicable,
        Pending,
        Published,
        Rejected,
        Invalidated
    }

    /// <summary>
    /// Input-edge request used to attempt a Burst Throw. The lifecycle service,
    /// not the caller, allocates the flight handle after admission checks pass.
    /// </summary>
    public struct BurstThrowRequest
    {
        public BurstThrowRequest(string sessionId, long attemptEpoch,
            string publisher, Vector3 feetOrigin, Vector3 cameraAzimuth,
            Vector3 throwerVelocity, float sourceTimestamp)
            : this(sessionId, attemptEpoch, publisher, feetOrigin, cameraAzimuth,
                throwerVelocity, sourceTimestamp, BurstLocomotionLabel.Stationary,
                BurstStanceLabel.Standing, 0f)
        {
        }

        /// <summary>Creates a request with the controller's latched movement snapshot.</summary>
        public BurstThrowRequest(string sessionId, long attemptEpoch,
            string publisher, Vector3 feetOrigin, Vector3 cameraAzimuth,
            Vector3 throwerVelocity, float sourceTimestamp,
            BurstLocomotionLabel locomotionLabel, BurstStanceLabel stanceLabel,
            float resolvedPlanarSpeed, Vector3 resolvedVelocity = default(Vector3),
            float resolvedFacingAzimuthDegrees = 0f, string geometryVariantId = "canonical",
            bool sameTickStandResolved = false)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || attemptEpoch < 0)
                throw new ArgumentException("throw-envelope");
            if (!IsFinite(feetOrigin) || !IsFinite(cameraAzimuth)
                || !IsFinite(throwerVelocity))
                throw new ArgumentException("throw-snapshot-position");
            if (!IsFinite(sourceTimestamp) || !IsFinite(resolvedPlanarSpeed)
                || resolvedPlanarSpeed < 0f
                || !IsFinite(resolvedFacingAzimuthDegrees))
                throw new ArgumentException("throw-snapshot-timing");
            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            Publisher = string.IsNullOrWhiteSpace(publisher)
                ? "BurstSimulation" : publisher;
            FeetOrigin = feetOrigin;
            CameraAzimuth = cameraAzimuth;
            CameraAzimuthDegrees = NormalizeAzimuthDegrees(cameraAzimuth);
            ThrowerVelocity = throwerVelocity;
            SourceTimestamp = sourceTimestamp;
            LocomotionLabel = locomotionLabel;
            StanceLabel = stanceLabel;
            ResolvedPlanarSpeed = resolvedPlanarSpeed;
            SameTickStandResolved = stanceLabel == BurstStanceLabel.Standing
                || sameTickStandResolved;
            ResolvedVelocity = IsFinite(resolvedVelocity)
                && resolvedVelocity.sqrMagnitude > 0f
                ? resolvedVelocity : throwerVelocity;
            ResolvedFacingAzimuthDegrees = resolvedFacingAzimuthDegrees;
            GeometryVariantId = string.IsNullOrWhiteSpace(geometryVariantId)
                ? "canonical" : geometryVariantId;
        }

        public string SessionId { get; }
        public long AttemptEpoch { get; }
        public string Publisher { get; }
        public Vector3 FeetOrigin { get; }
        public Vector3 CameraAzimuth { get; }
        /// <summary>Normalized scalar camera azimuth in degrees [0, 360).</summary>
        public float CameraAzimuthDegrees { get; }
        public Vector3 ThrowerVelocity { get; }
        public float SourceTimestamp { get; }
        public BurstLocomotionLabel LocomotionLabel { get; }
        public BurstStanceLabel StanceLabel { get; }
        public float ResolvedPlanarSpeed { get; }
        /// <summary>
        /// Whether a crouched input edge completed its same-tick stand
        /// resolution before Burst admission.
        /// </summary>
        public bool SameTickStandResolved { get; }
        public Vector3 ResolvedVelocity { get; }
        public float ResolvedFacingAzimuthDegrees { get; }
        public string GeometryVariantId { get; }

        private static float NormalizeAzimuthDegrees(Vector3 direction)
        {
            float planarMagnitude = Mathf.Sqrt(direction.x * direction.x
                + direction.z * direction.z);
            if (!IsFinite(planarMagnitude) || planarMagnitude <= 0.0001f)
                return 0f;
            float degrees = Mathf.Atan2(direction.x, direction.z)
                * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;
            return degrees >= 360f ? degrees - 360f : degrees;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Immutable accepted Throw snapshot. The numeric handle is assigned only
    /// after all rejection-prone checks have completed.
    /// </summary>
    public struct BurstThrowSnapshot
    {
        public BurstThrowSnapshot(string sessionId, long attemptEpoch,
            ulong flightHandleId, string publisher, Vector3 feetOrigin,
            Vector3 cameraAzimuth, Vector3 throwerVelocity, float sourceTimestamp)
            : this(sessionId, attemptEpoch, flightHandleId, publisher, feetOrigin,
                cameraAzimuth, throwerVelocity, sourceTimestamp,
                BurstLocomotionLabel.Stationary, BurstStanceLabel.Standing, 0f)
        {
        }

        public BurstThrowSnapshot(string sessionId, long attemptEpoch,
            ulong flightHandleId, string publisher, Vector3 feetOrigin,
            Vector3 cameraAzimuth, Vector3 throwerVelocity, float sourceTimestamp,
            BurstLocomotionLabel locomotionLabel, BurstStanceLabel stanceLabel,
            float resolvedPlanarSpeed, string throwSnapshotId = null,
            Vector3 launchPosition = default(Vector3),
            Vector3 resolvedVelocity = default(Vector3),
            float resolvedFacingAzimuthDegrees = 0f,
            float trajectoryV0Mps = 0f, float trajectoryThetaDegrees = 0f,
            float throwReleaseHeightMeters = 0f, float projectileRadiusMeters = 0f,
            string sourcePickupId = null, string geometryVariantId = "canonical")
        {
            if (string.IsNullOrWhiteSpace(sessionId) || attemptEpoch < 0)
                throw new ArgumentException("throw-envelope");
            if (flightHandleId == 0)
                throw new ArgumentOutOfRangeException(nameof(flightHandleId));
            if (!IsFinite(feetOrigin) || !IsFinite(cameraAzimuth)
                || !IsFinite(throwerVelocity))
                throw new ArgumentException("throw-snapshot-position");
            if (!IsFinite(sourceTimestamp) || !IsFinite(resolvedPlanarSpeed)
                || resolvedPlanarSpeed < 0f)
                throw new ArgumentException("throw-snapshot-timing");
            if (!IsFinite(launchPosition)
                || !IsFinite(resolvedVelocity)
                || !IsFinite(resolvedFacingAzimuthDegrees)
                || !IsFinite(trajectoryV0Mps)
                || !IsFinite(trajectoryThetaDegrees)
                || !IsFinite(throwReleaseHeightMeters)
                || !IsFinite(projectileRadiusMeters))
                throw new ArgumentException("throw-snapshot-geometry");
            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            FlightHandleId = flightHandleId;
            Publisher = string.IsNullOrWhiteSpace(publisher)
                ? "BurstSimulation" : publisher;
            FeetOrigin = feetOrigin;
            CameraAzimuth = cameraAzimuth;
            CameraAzimuthDegrees = NormalizeAzimuthDegrees(cameraAzimuth);
            ThrowerVelocity = throwerVelocity;
            SourceTimestamp = sourceTimestamp;
            LocomotionLabel = locomotionLabel;
            StanceLabel = stanceLabel;
            ResolvedPlanarSpeed = resolvedPlanarSpeed;
            ThrowSnapshotId = string.IsNullOrWhiteSpace(throwSnapshotId)
                ? "throw:" + sessionId + ":" + attemptEpoch + ":"
                    + flightHandleId.ToString("D") : throwSnapshotId;
            LaunchPosition = launchPosition;
            ResolvedVelocity = IsFinite(resolvedVelocity)
                && resolvedVelocity.sqrMagnitude > 0f
                ? resolvedVelocity : throwerVelocity;
            ResolvedFacingAzimuthDegrees = resolvedFacingAzimuthDegrees;
            TrajectoryV0Mps = trajectoryV0Mps;
            TrajectoryThetaDegrees = trajectoryThetaDegrees;
            ThrowReleaseHeightMeters = throwReleaseHeightMeters;
            ProjectileRadiusMeters = projectileRadiusMeters;
            SourcePickupId = sourcePickupId ?? string.Empty;
            GeometryVariantId = string.IsNullOrWhiteSpace(geometryVariantId)
                ? "canonical" : geometryVariantId;
        }

        /// <summary>Compatibility constructor for canonical decimal handles.</summary>
        [Obsolete("Use BurstThrowRequest; the service owns handle allocation.")]
        public BurstThrowSnapshot(string flightHandleId, string publisher,
            Vector3 feetOrigin, Vector3 cameraAzimuth, Vector3 throwerVelocity,
            float sourceTimestamp)
            : this("legacy-compat", 0, ParseHandle(flightHandleId), publisher, feetOrigin,
                cameraAzimuth, throwerVelocity, sourceTimestamp)
        {
        }

        public string SessionId { get; }
        public long AttemptEpoch { get; }
        public ulong FlightHandleId { get; }
        public string FlightHandleIdText
        {
            get { return FlightHandleId.ToString("D"); }
        }
        public string Publisher { get; }
        public Vector3 FeetOrigin { get; }
        public Vector3 CameraAzimuth { get; }
        /// <summary>Normalized scalar camera azimuth in degrees [0, 360).</summary>
        public float CameraAzimuthDegrees { get; }
        public Vector3 ThrowerVelocity { get; }
        public float SourceTimestamp { get; }
        public BurstLocomotionLabel LocomotionLabel { get; }
        public BurstStanceLabel StanceLabel { get; }
        public float ResolvedPlanarSpeed { get; }
        public string ThrowSnapshotId { get; }
        public Vector3 LaunchPosition { get; }
        public Vector3 ResolvedVelocity { get; }
        public float ResolvedFacingAzimuthDegrees { get; }
        public float TrajectoryV0Mps { get; }
        public float TrajectoryThetaDegrees { get; }
        public float ThrowReleaseHeightMeters { get; }
        public float ProjectileRadiusMeters { get; }
        public string SourcePickupId { get; }
        public string GeometryVariantId { get; }

        private static ulong ParseHandle(string value)
        {
            ulong handle;
            if (!ulong.TryParse(value, NumberStyles.None,
                CultureInfo.InvariantCulture, out handle) || handle == 0)
                throw new ArgumentException("flightHandleId");
            return handle;
        }

        private static float NormalizeAzimuthDegrees(Vector3 direction)
        {
            float planarMagnitude = Mathf.Sqrt(direction.x * direction.x
                + direction.z * direction.z);
            if (!IsFinite(planarMagnitude) || planarMagnitude <= 0.0001f)
                return 0f;
            float degrees = Mathf.Atan2(direction.x, direction.z)
                * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;
            return degrees >= 360f ? degrees - 360f : degrees;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Immutable terminal trace for one Burst flight. FactId is nullable in the
    /// conceptual contract; the value is never exposed as a successful zero ID.
    /// </summary>
    public struct BurstTerminalRecord
    {
        internal BurstTerminalRecord(string sessionId, long attemptEpoch,
            ulong flightHandleId, BurstTerminalEvent terminalEvent,
            float flightElapsedSeconds, float? contactFraction,
            float? contactEventTime, float timeoutBoundarySeconds,
            string comparisonResult, Vector3 terminalPosition,
            Vector3? surfaceContactPosition,
            BurstFactPublicationState factPublicationState, ulong? factId,
            float timestamp, float? terminalPublicationTime, int terminalTickIndex,
            float sourceTimestamp, Vector3 resolvedVelocity,
            string terminalCause, BurstFlightState burstStateAfter,
            float? tPublish, int winningTickIndex)
        {
            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            FlightHandleId = flightHandleId;
            TerminalEvent = terminalEvent;
            FlightElapsedSeconds = flightElapsedSeconds;
            ContactFraction = contactFraction;
            ContactEventTime = contactEventTime;
            TimeoutBoundarySeconds = timeoutBoundarySeconds;
            ComparisonResult = comparisonResult;
            TerminalPosition = terminalPosition;
            SurfaceContactPosition = surfaceContactPosition;
            FactPublicationState = factPublicationState;
            FactId = factId;
            Timestamp = timestamp;
            TerminalPublicationTime = terminalPublicationTime;
            TerminalTickIndex = terminalTickIndex;
            SourceTimestamp = sourceTimestamp;
            ResolvedVelocity = resolvedVelocity;
            TerminalCause = terminalCause ?? string.Empty;
            BurstStateAfter = burstStateAfter;
            TPublish = tPublish;
            WinningTickIndex = winningTickIndex;
        }

        public string SessionId { get; }
        public long AttemptEpoch { get; }
        public ulong FlightHandleId { get; }
        public BurstTerminalEvent TerminalEvent { get; }
        public float FlightElapsedSeconds { get; }
        public float? ContactFraction { get; }
        public float? ContactEventTime { get; }
        public float TimeoutBoundarySeconds { get; }
        public string ComparisonResult { get; }
        public Vector3 TerminalPosition { get; }
        /// <summary>Physical collider contact, distinct from pushed-out center.</summary>
        public Vector3? SurfaceContactPosition { get; }
        public BurstFactPublicationState FactPublicationState { get; }
        public ulong? FactId { get; }
        /// <summary>Absolute virtual time at which the terminal became publishable.</summary>
        public float? TerminalPublicationTime { get; }
        /// <summary>Fixed simulation tick that resolved the terminal event.</summary>
        public int TerminalTickIndex { get; }
        /// <summary>Source-edge timestamp from the accepted Throw snapshot.</summary>
        public float SourceTimestamp { get; }
        /// <summary>Snapshot-authoritative resolved launch velocity.</summary>
        public Vector3 ResolvedVelocity { get; }
        /// <summary>Canonical terminal cause for fixture serialization.</summary>
        public string TerminalCause { get; }
        /// <summary>Lifecycle state after terminal resolution.</summary>
        public BurstFlightState BurstStateAfter { get; }
        /// <summary>Raw-fact publication time when the terminal has a fact.</summary>
        public float? TPublish { get; }
        /// <summary>Canonical alias for the inclusive winning tick.</summary>
        public int WinningTickIndex { get; }
        public float Timestamp { get; }
    }

    /// <summary>Result of one virtual-clock simulation callback.</summary>
    public struct BurstStepBatchResult
    {
        internal BurstStepBatchResult(int tickCount, int discardedTickCount,
            bool backlogClamped, bool hasTerminal, BurstTerminalRecord terminal)
        {
            TickCount = tickCount;
            DiscardedTickCount = discardedTickCount;
            BacklogClamped = backlogClamped;
            HasTerminal = hasTerminal;
            Terminal = terminal;
        }

        /// <summary>Fixed simulation ticks completed this callback.</summary>
        public int TickCount { get; }

        /// <summary>Due ticks discarded by the bounded catch-up cap.</summary>
        public int DiscardedTickCount { get; }

        /// <summary>Whether due work exceeded MaxBacklogTicks.</summary>
        public bool BacklogClamped { get; }

        /// <summary>Whether this callback resolved the flight.</summary>
        public bool HasTerminal { get; }

        /// <summary>Terminal record when HasTerminal is true.</summary>
        public BurstTerminalRecord Terminal { get; }
    }

    /// <summary>Receives serialized Burst simulation diagnostics.</summary>
    public interface IBurstDiagnosticSink
    {
        void Record(string code, string sessionId, long attemptEpoch,
            long callbackTick, int discardedTickCount);
    }

    /// <summary>
    /// Fixed-step Burst lifecycle and simulation owner. It is deliberately
    /// independent of Unity's render clock: a session phase owner calls
    /// AdvanceGameplayTime on the injected virtual clock, which invokes Advance.
    /// </summary>
    public sealed class BurstSimulationService : IDisposable
    {
        private sealed class Flight
        {
            public BurstThrowSnapshot Snapshot;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Elapsed;
            public long StartTick;
            public int TickIndex;
            public float Accumulator;
        }

        private readonly BurstRuntimeConfiguration _configuration;
        private readonly IBurstPhysicsQuery _physicsQuery;
        private readonly NoiseEmitter _noiseEmitter;
        private readonly IVirtualTickClock _clock;
        private readonly float _pickupOriginOffset;
        private readonly IBurstDiagnosticSink _diagnosticSink;
        private Flight _flight;
        private BurstFlightState _state = BurstFlightState.Placed;
        private string _carriedPickupId = string.Empty;
        private BurstTerminalRecord _lastTerminal;
        private bool _hasLastTerminal;
        private BurstStepBatchResult _lastBatch;
        private string _activeSessionId;
        private long _activeAttemptEpoch;
        private ulong _nextFlightHandle = 1;
        private string _lastRejectionCode = string.Empty;
        private string _lastRejectionDiagnosticCode = string.Empty;
        private long _lastAdvanceTick = long.MinValue;
        private bool _disposed;

        /// <summary>
        /// Creates a Burst owner from one validated registry profile and injected
        /// physics, clock, and noise publication seams.
        /// </summary>
        public BurstSimulationService(BurstRuntimeConfiguration configuration,
            IBurstPhysicsQuery physicsQuery, NoiseEmitter noiseEmitter,
            IVirtualTickClock clock)
            : this(configuration, physicsQuery, noiseEmitter, clock,
                NoiseRuntimeConfiguration.RegisteredF12NoiseOriginOffset, null)
        {
        }

        /// <summary>
        /// Creates a Burst owner with the session-owned pickup linecast origin and
        /// an optional serialized diagnostic sink.
        /// </summary>
        public BurstSimulationService(BurstRuntimeConfiguration configuration,
            IBurstPhysicsQuery physicsQuery, NoiseEmitter noiseEmitter,
            IVirtualTickClock clock, float pickupOriginOffset,
            IBurstDiagnosticSink diagnosticSink = null)
        {
            _configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            _physicsQuery = physicsQuery
                ?? throw new ArgumentNullException(nameof(physicsQuery));
            _noiseEmitter = noiseEmitter
                ?? throw new ArgumentNullException(nameof(noiseEmitter));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (float.IsNaN(pickupOriginOffset)
                || float.IsInfinity(pickupOriginOffset)
                || pickupOriginOffset < 0f)
                throw new ArgumentOutOfRangeException(nameof(pickupOriginOffset));
            _pickupOriginOffset = pickupOriginOffset;
            _diagnosticSink = diagnosticSink;
            _activeSessionId = _noiseEmitter.EventBusSessionId;
            _activeAttemptEpoch = _noiseEmitter.EventBusAttemptEpoch;
        }

        /// <summary>
        /// Releases the injected physics resolver and its process-global physics
        /// setting pin. Production calls this when the composition root is
        /// destroyed; fixture-owned query seams that are not disposable are left
        /// untouched.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IDisposable disposable = _physicsQuery as IDisposable;
            if (disposable != null)
                disposable.Dispose();
        }

        /// <summary>Current limited-resource lifecycle state.</summary>
        public BurstFlightState State { get { return _state; } }

        /// <summary>Whether a flight is currently consuming fixed-step time.</summary>
        public bool HasActiveFlight { get { return _state == BurstFlightState.InFlight; } }

        /// <summary>Current simulated projectile center, when a flight exists.</summary>
        public Vector3 CurrentPosition
        {
            get { return _flight == null ? Vector3.zero : _flight.Position; }
        }

        /// <summary>
        /// Fractional fixed-step time retained after the latest bounded advance.
        /// </summary>
        public float CurrentAccumulatorSeconds
        {
            get { return _flight == null ? 0f : _flight.Accumulator; }
        }

        /// <summary>Current authoritative velocity from the accepted throw snapshot.</summary>
        public Vector3 CurrentVelocity
        {
            get { return _flight == null ? Vector3.zero : _flight.Velocity; }
        }

        /// <summary>Current numeric flight identity, or zero when none is active.</summary>
        public ulong CurrentFlightHandleId
        {
            get { return _flight == null ? 0 : _flight.Snapshot.FlightHandleId; }
        }

        /// <summary>Current flight identity as canonical decimal transport text.</summary>
        public string CurrentFlightHandleIdText
        {
            get { return _flight == null ? string.Empty
                : _flight.Snapshot.FlightHandleIdText; }
        }

        /// <summary>Most recent terminal trace, if one has been resolved.</summary>
        public bool TryGetLastTerminal(out BurstTerminalRecord terminal)
        {
            terminal = _lastTerminal;
            return _hasLastTerminal;
        }

        /// <summary>
        /// Resolves a pending terminal after NoiseEmitter.Flush has completed.
        /// Only publication metadata changes; the terminal winner is immutable.
        /// </summary>
        public void ResolvePendingTerminalPublication()
        {
            if (!_hasLastTerminal
                || _lastTerminal.FactPublicationState
                    != BurstFactPublicationState.Pending)
                return;

            NoisePublicationStatus status;
            ulong? factId;
            EventPublishResult result;
            if (!_noiseEmitter.TryGetPublication(NoiseSourceKind.Burst,
                _lastTerminal.FlightHandleId.ToString("D"),
                out status, out factId, out result))
                return;

            BurstFactPublicationState publicationState;
            switch (status)
            {
                case NoisePublicationStatus.Published:
                    publicationState = BurstFactPublicationState.Published;
                    break;
                case NoisePublicationStatus.Rejected:
                    publicationState = BurstFactPublicationState.Rejected;
                    _lastRejectionCode = string.IsNullOrWhiteSpace(result.Code)
                        ? "burst-noise-publication-rejected" : result.Code;
                    break;
                case NoisePublicationStatus.Invalidated:
                    publicationState = BurstFactPublicationState.Invalidated;
                    break;
                default:
                    return;
            }

            _lastTerminal = new BurstTerminalRecord(_lastTerminal.SessionId,
                _lastTerminal.AttemptEpoch, _lastTerminal.FlightHandleId,
                _lastTerminal.TerminalEvent, _lastTerminal.FlightElapsedSeconds,
                _lastTerminal.ContactFraction, _lastTerminal.ContactEventTime,
                _lastTerminal.TimeoutBoundarySeconds,
                _lastTerminal.ComparisonResult, _lastTerminal.TerminalPosition,
                _lastTerminal.SurfaceContactPosition, publicationState, factId,
                _lastTerminal.Timestamp,
                _lastTerminal.TerminalPublicationTime,
                _lastTerminal.TerminalTickIndex,
                _lastTerminal.SourceTimestamp, _lastTerminal.ResolvedVelocity,
                _lastTerminal.TerminalCause, _lastTerminal.BurstStateAfter,
                publicationState == BurstFactPublicationState.Rejected
                    || publicationState == BurstFactPublicationState.Invalidated
                    ? (float?)null : _lastTerminal.TPublish,
                _lastTerminal.WinningTickIndex);
        }

        /// <summary>Most recent fixed-step batch result.</summary>
        public BurstStepBatchResult LastBatch { get { return _lastBatch; } }

        /// <summary>Stable code for the last rejected pickup or Throw edge.</summary>
        public string LastRejectionCode { get { return _lastRejectionCode; } }

        /// <summary>Diagnostic subcode for the latest stable rejection family.</summary>
        public string LastRejectionDiagnosticCode
        {
            get { return _lastRejectionDiagnosticCode; }
        }

        /// <summary>
        /// Arms one authored pickup when the player enters its reach radius. A
        /// second pickup cannot be carried while this resource is not Placed.
        /// </summary>
        public BurstThrowAdmission TryPickup(string pickupId,
            Vector3 playerPosition, Vector3 pickupPosition)
        {
            if (_state != BurstFlightState.Placed)
                return Reject("burst-pickup-already-consumed");
            if (string.IsNullOrWhiteSpace(pickupId)
                || !IsFinite(playerPosition) || !IsFinite(pickupPosition))
                return Reject("burst-pickup-invalid-input");

            IBurstPickupQueryWithOrigin raisedOriginQuery =
                _physicsQuery as IBurstPickupQueryWithOrigin;
            if (raisedOriginQuery == null)
                return Reject("burst-pickup-origin-seam-required");

            PickupReachResult reach = raisedOriginQuery.EvaluatePickupReach(
                playerPosition, pickupPosition,
                _configuration.PickupReachRadiusMeters, _pickupOriginOffset);
            if (reach.Status == PickupReachStatus.Incomplete)
                return Reject("burst-pickup-query-incomplete");
            if (reach.Status == PickupReachStatus.Blocked)
                return Reject("burst-pickup-blocked");
            if (reach.Status == PickupReachStatus.OutOfReach)
                return Reject("burst-pickup-out-of-reach");

            _carriedPickupId = pickupId;
            _state = BurstFlightState.Carried;
            _lastRejectionCode = string.Empty;
            _lastRejectionDiagnosticCode = string.Empty;
            return BurstThrowAdmission.Accepted;
        }

        /// <summary>
        /// Attempts an input-edge launch. Initial overlap is tested before handle
        /// allocation and spend, so rejection leaves the resource Carried.
        /// </summary>
        public BurstThrowAdmission TryThrow(BurstThrowRequest request,
            out BurstThrowSnapshot acceptedSnapshot)
        {
            acceptedSnapshot = default(BurstThrowSnapshot);
            if (_state != BurstFlightState.Carried)
                return Reject("burst-throw-not-carried");
            if (!string.Equals(request.SessionId, _activeSessionId,
                    StringComparison.Ordinal)
                || request.AttemptEpoch != _activeAttemptEpoch)
                return Reject("burst-throw-stale-envelope");
            if (!IsFinite(request.FeetOrigin)
                || !IsFinite(request.CameraAzimuth)
                || !IsFinite(request.ThrowerVelocity)
                || !IsFinite(request.SourceTimestamp)
                || !IsFinite(request.ResolvedPlanarSpeed)
                || !IsFinite(request.ResolvedFacingAzimuthDegrees))
                return Reject("burst-throw-invalid-snapshot");
            if (request.LocomotionLabel != BurstLocomotionLabel.Stationary)
                return Reject(NoiseRejectionCodes.ThrowWhileMovingRejected,
                    "burst-throw-locomotion-not-stationary");
            if (request.StanceLabel != BurstStanceLabel.Standing
                && request.StanceLabel != BurstStanceLabel.Crouched)
                return Reject(NoiseRejectionCodes.ThrowWhileMovingRejected,
                    "burst-throw-unsupported-stance");
            if (request.StanceLabel == BurstStanceLabel.Crouched
                && !request.SameTickStandResolved)
                return Reject(NoiseRejectionCodes.BurstStandClearanceBlocked,
                    "burst-stand-clearance-blocked");
            if (request.ResolvedPlanarSpeed > 0.0001f)
                return Reject(NoiseRejectionCodes.ThrowWhileMovingRejected,
                    "burst-throw-planar-speed-positive");

            BurstStanceLabel resolvedStance = request.StanceLabel
                == BurstStanceLabel.Crouched
                ? BurstStanceLabel.Standing : request.StanceLabel;
            Vector3 direction = new Vector3(request.CameraAzimuth.x, 0f,
                request.CameraAzimuth.z);
            float directionMagnitude = direction.magnitude;
            if (!IsFinite(direction) || directionMagnitude <= 0.0001f)
            {
                float facingRadians = request.ResolvedFacingAzimuthDegrees
                    * Mathf.Deg2Rad;
                direction = new Vector3(Mathf.Sin(facingRadians), 0f,
                    Mathf.Cos(facingRadians));
                directionMagnitude = direction.magnitude;
            }
            if (!IsFinite(direction) || directionMagnitude <= 0f)
                return Reject("burst-throw-invalid-azimuth");
            direction /= directionMagnitude;

            Vector3 origin = request.FeetOrigin
                + Vector3.up * _configuration.ReleaseHeightMeters;
            // Request validation is complete and no resource has been allocated;
            // synchronize authored solids once before the launch overlap probe.
            _physicsQuery.PrepareBatch();
            BurstOverlapResult overlap = GetInitialOverlap(origin,
                _configuration.ProjectileRadiusMeters);
            if (overlap.Status == BurstOverlapStatus.Incomplete)
                return Reject(NoiseRejectionCodes.InitialOverlapQueryIncomplete,
                    NoiseRejectionCodes.InitialOverlapQueryIncomplete);
            if (overlap.Status == BurstOverlapStatus.Blocked)
                return Reject(NoiseRejectionCodes.InitialOverlapRejected,
                    NoiseRejectionCodes.InitialOverlapRejected);
            if (_nextFlightHandle == 0)
                return Reject("burst-flight-handle-exhausted");

            ulong handle = _nextFlightHandle++;
            float speed = _configuration.InitialSpeedMetersPerSecond;
            Vector3 resolvedVelocity = _configuration.ResolveInitialVelocity(direction);
            acceptedSnapshot = new BurstThrowSnapshot(request.SessionId,
                request.AttemptEpoch, handle, request.Publisher,
                request.FeetOrigin, request.CameraAzimuth,
                request.ThrowerVelocity, request.SourceTimestamp,
                request.LocomotionLabel, resolvedStance,
                request.ResolvedPlanarSpeed,
                "throw:" + request.SessionId + ":" + request.AttemptEpoch
                    + ":" + handle.ToString("D"),
                origin, resolvedVelocity,
                request.ResolvedFacingAzimuthDegrees,
                speed, _configuration.LaunchAngleDegrees,
                _configuration.ReleaseHeightMeters,
                _configuration.ProjectileRadiusMeters,
                _carriedPickupId, request.GeometryVariantId);
            return StartFlight(acceptedSnapshot, origin);
        }

        /// <summary>
        /// Compatibility adapter for an already-constructed snapshot. Its supplied
        /// handle is ignored; authoritative callers must use BurstThrowRequest.
        /// </summary>
        [Obsolete("Use the envelope-aware TryThrow overload.")]
        public BurstThrowAdmission TryThrow(BurstThrowSnapshot snapshot)
        {
            BurstThrowSnapshot acceptedSnapshot;
            if (!string.Equals(snapshot.SessionId, _activeSessionId,
                StringComparison.Ordinal))
                return Reject("burst-throw-envelope-required");
            return TryThrow(new BurstThrowRequest(snapshot.SessionId,
                snapshot.AttemptEpoch, snapshot.Publisher, snapshot.FeetOrigin,
                snapshot.CameraAzimuth, snapshot.ThrowerVelocity,
                snapshot.SourceTimestamp), out acceptedSnapshot);
        }

        private BurstOverlapResult GetInitialOverlap(Vector3 origin, float radius)
        {
            IBurstPhysicsQueryWithOverlapStatus typed =
                _physicsQuery as IBurstPhysicsQueryWithOverlapStatus;
            return typed == null
                ? BurstOverlapResult.Incomplete
                : typed.GetInitialOverlap(origin, radius);
        }

        private BurstThrowAdmission StartFlight(BurstThrowSnapshot snapshot,
            Vector3 origin)
        {
            Vector3 velocity = snapshot.ResolvedVelocity;

            _flight = new Flight
            {
                Snapshot = snapshot,
                Position = snapshot.LaunchPosition,
                Velocity = velocity,
                Elapsed = 0f,
                StartTick = _clock.CurrentTick,
                TickIndex = 0,
                Accumulator = 0f
            };
            _hasLastTerminal = false;
            _lastAdvanceTick = _clock.CurrentTick;
            _state = BurstFlightState.InFlight;
            _carriedPickupId = string.Empty;
            _lastRejectionCode = string.Empty;
            _lastRejectionDiagnosticCode = string.Empty;
            return BurstThrowAdmission.Accepted;
        }

        /// <summary>
        /// Cancels an in-flight throw on player death. The committed spend is not
        /// refunded and no Burst noise is submitted.
        /// </summary>
        public bool CancelForDeath()
        {
            if (_state != BurstFlightState.InFlight || _flight == null)
                return false;
            CompleteWithoutNoise(BurstTerminalEvent.DeathCancelled,
                "death-cancelled", null, null, _clock.CurrentTime);
            return true;
        }

        /// <summary>
        /// Cancels old in-flight work at an epoch boundary while retaining a
        /// Carried resource, matching the checkpoint lifecycle contract.
        /// </summary>
        public void ResetForBoundary(string activeSessionId,
            long activeAttemptEpoch, bool preserveCarried)
        {
            if (string.IsNullOrWhiteSpace(activeSessionId)
                || activeAttemptEpoch < 0)
                throw new ArgumentException("burst-boundary-envelope");

            if (_hasLastTerminal
                && _lastTerminal.FactPublicationState
                    == BurstFactPublicationState.Pending)
            {
                _lastTerminal = WithPublicationState(
                    BurstFactPublicationState.Invalidated, null);
            }
            if (_state == BurstFlightState.InFlight && _flight != null)
                CompleteWithoutNoise(BurstTerminalEvent.BoundaryCancelled,
                    "epoch-transition-stale");

            _activeSessionId = activeSessionId;
            _activeAttemptEpoch = activeAttemptEpoch;
            _nextFlightHandle = 1;
            if (!preserveCarried)
            {
                _state = BurstFlightState.Placed;
                _carriedPickupId = string.Empty;
            }
            // A boundary never restores a spent in-flight resource. Only a
            // full-room/new-session boundary restores authored pickups.
        }

        /// <summary>Compatibility boundary adapter for old fixture callers.</summary>
        [Obsolete("Use the envelope-aware boundary overload.")]
        public void ResetForBoundary(bool preserveCarried)
        {
            ResetForBoundary(_noiseEmitter.EventBusSessionId,
                _noiseEmitter.EventBusAttemptEpoch, preserveCarried);
        }

        /// <summary>Restores the resource to the authored Placed state.</summary>
        public void ResetForSession()
        {
            _flight = null;
            _state = BurstFlightState.Placed;
            _carriedPickupId = string.Empty;
            _nextFlightHandle = 1;
            _activeSessionId = _noiseEmitter.EventBusSessionId;
            _activeAttemptEpoch = _noiseEmitter.EventBusAttemptEpoch;
            _hasLastTerminal = false;
            _lastBatch = default(BurstStepBatchResult);
            _lastAdvanceTick = long.MinValue;
            _lastRejectionCode = string.Empty;
            _lastRejectionDiagnosticCode = string.Empty;
        }

        /// <summary>
        /// Advances one injected virtual-clock callback. At most the configured
        /// number of fixed ticks execute; excess due ticks are discarded while
        /// the fractional accumulator remainder is retained.
        /// </summary>
        public BurstStepBatchResult Advance(long tick, float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0f)
            {
                _lastRejectionCode = "burst-gameplay-delta-invalid";
                _lastRejectionDiagnosticCode = "burst-gameplay-delta-invalid";
                _lastBatch = new BurstStepBatchResult(0, 0, false, false,
                    default(BurstTerminalRecord));
                return _lastBatch;
            }
            if (_state != BurstFlightState.InFlight || _flight == null
                || _clock.IsPaused || delta == 0f)
            {
                _lastBatch = new BurstStepBatchResult(0, 0, false, false,
                    default(BurstTerminalRecord));
                return _lastBatch;
            }
            long currentTick = _clock.CurrentTick;
            bool isNextDirectCallback = currentTick < long.MaxValue
                && tick == currentTick + 1;
            if (tick <= _lastAdvanceTick
                || tick > currentTick
                    && !isNextDirectCallback)
            {
                _lastRejectionCode = "burst-callback-tick-invalid";
                _lastRejectionDiagnosticCode = "burst-callback-tick-nonmonotonic";
                _lastBatch = new BurstStepBatchResult(0, 0, false, false,
                    default(BurstTerminalRecord));
                return _lastBatch;
            }
            _lastAdvanceTick = tick;

            float fixedStep = _configuration.FixedSubstepSeconds;
            // Keep due-tick arithmetic wide until after the backlog cap. A finite
            // float delta can represent more ticks than Int32 can hold; narrowing
            // first would wrap the work count and could turn a huge hitch into a
            // negative or unbounded simulation request.
            double accumulated = (double)_flight.Accumulator + (double)delta;
            double dueTicksWide = Math.Floor(accumulated / fixedStep);
            if (dueTicksWide <= 0d)
            {
                _flight.Accumulator = (float)accumulated;
                _lastBatch = new BurstStepBatchResult(0, 0, false, false,
                    default(BurstTerminalRecord));
                return _lastBatch;
            }

            // Consume all due whole ticks, retaining only the fractional
            // remainder; the bounded cap controls simulation work, not time.
            double remainder = accumulated % fixedStep;
            if (remainder < 0d || remainder >= fixedStep)
                remainder = Math.Max(0d, Math.Min((double)fixedStep,
                    remainder));
            _flight.Accumulator = (float)remainder;
            int simulatedTicks = dueTicksWide
                >= _configuration.MaxBacklogTicks
                ? _configuration.MaxBacklogTicks
                : (int)dueTicksWide;
            bool backlogClamped = dueTicksWide > simulatedTicks;
            int discardedTicks;
            if (!backlogClamped)
            {
                discardedTicks = 0;
            }
            else if (dueTicksWide - simulatedTicks >= int.MaxValue)
            {
                discardedTicks = int.MaxValue;
            }
            else
            {
                discardedTicks = (int)(dueTicksWide - simulatedTicks);
            }
            if (backlogClamped && _diagnosticSink != null)
            {
                _diagnosticSink.Record("burst-backlog-clamped", _activeSessionId,
                    _activeAttemptEpoch, tick, discardedTicks);
            }
            _physicsQuery.PrepareBatch();

            BurstTerminalRecord terminal = default(BurstTerminalRecord);
            bool hasTerminal = false;
            for (int i = 0; i < simulatedTicks && !hasTerminal; i++)
            {
                hasTerminal = SimulateOneTick(fixedStep, out terminal);
            }

            _lastBatch = new BurstStepBatchResult(simulatedTicks, discardedTicks,
                backlogClamped, hasTerminal, terminal);
            return _lastBatch;
        }

        private bool SimulateOneTick(float fixedStep,
            out BurstTerminalRecord terminal)
        {
            terminal = default(BurstTerminalRecord);
            if (_state != BurstFlightState.InFlight || _flight == null)
                return false;

            Vector3 current = _flight.Position;
            Vector3 nextVelocity = _flight.Velocity
                + Vector3.down * (_configuration.GravityMagnitude * fixedStep);
            Vector3 next = current + nextVelocity * fixedStep;
            ClosestContactResult contact = _physicsQuery.Sweep(current, next,
                _configuration.ProjectileRadiusMeters);
            int nextTickIndex = _flight.TickIndex + 1;
            float nextElapsed = nextTickIndex * fixedStep;

            if (contact.HasContact)
            {
                // A reported contact is an authoritative physics result. Invalid
                // contact data must terminate the committed flight fail-closed;
                // treating it as free flight would mutate state and could later
                // publish a misleading timeout fact.
                float segmentLength = Vector3.Distance(current, next);
                if (!contact.IsFiniteValid() || !IsFinite(segmentLength)
                    || segmentLength < 0f)
                {
                    _lastRejectionCode = "burst-contact-invalid";
                    _lastRejectionDiagnosticCode = "burst-contact-invalid";
                    CompleteWithoutNoise(BurstTerminalEvent.BoundaryCancelled,
                        "malformed-contact-fail-closed", nextTickIndex,
                        nextElapsed);
                    terminal = _lastTerminal;
                    return true;
                }

                float fraction = segmentLength <= 0f ? 0f
                    : Mathf.Clamp01(contact.DistanceAlongSegment / segmentLength);
                float contactEventTime = _flight.Elapsed + fraction * fixedStep;
                if (!IsFinite(fraction) || !IsFinite(contactEventTime))
                {
                    _lastRejectionCode = "burst-contact-invalid";
                    _lastRejectionDiagnosticCode = "burst-contact-time-invalid";
                    CompleteWithoutNoise(BurstTerminalEvent.BoundaryCancelled,
                        "malformed-contact-time-fail-closed", nextTickIndex,
                        nextElapsed);
                    terminal = _lastTerminal;
                    return true;
                }

                // Contact after the integer timeout tick is not malformed; the
                // timeout path below remains the winner. Contact at the boundary
                // wins and is the only path allowed to publish a landing fact.
                if (nextTickIndex <= _configuration.TimeoutTick)
                {
                    Vector3 publishedContact = BurstCollisionResolver.GetPublishedContact(
                        contact, _configuration.ProjectileRadiusMeters,
                        _configuration.EpsilonContactMeters);
                    float terminalPublicationTime = (_flight.StartTick
                        + nextTickIndex) * _clock.TickInterval;
                    if (!IsFinite(publishedContact)
                        || !IsFinite(terminalPublicationTime))
                    {
                        _lastRejectionCode = "burst-contact-invalid";
                        _lastRejectionDiagnosticCode = "burst-contact-pushout-invalid";
                        CompleteWithoutNoise(BurstTerminalEvent.BoundaryCancelled,
                            "malformed-contact-pushout-fail-closed", nextTickIndex,
                            nextElapsed);
                        terminal = _lastTerminal;
                        return true;
                    }

                    _flight.Position = publishedContact;
                    _flight.Velocity = nextVelocity;
                    _flight.TickIndex = nextTickIndex;
                    _flight.Elapsed = contactEventTime;
                    terminal = CompleteWithNoise(BurstTerminalEvent.Contact,
                        "contact-before-or-at-timeout", fraction,
                        contactEventTime, publishedContact,
                        contact.ColliderSurfaceContact,
                        terminalPublicationTime);
                    return true;
                }
            }

            if (!IsFinite(next) || !IsFinite(nextVelocity)
                || !IsFinite(nextElapsed))
            {
                // A diverged ballistic integration would publish a non-finite
                // timeout terminal. Fail closed at the last finite simulated
                // point without advancing the flight or publishing a raw fact;
                // the committed spend is not refunded.
                _lastRejectionCode = "burst-simulation-diverged";
                _lastRejectionDiagnosticCode = "burst-simulation-diverged";
                CompleteWithoutNoise(BurstTerminalEvent.BoundaryCancelled,
                    "simulation-diverged-fail-closed", nextTickIndex,
                    nextElapsed);
                terminal = _lastTerminal;
                return true;
            }

            _flight.Position = next;
            _flight.Velocity = nextVelocity;
            _flight.TickIndex = nextTickIndex;
            _flight.Elapsed = nextElapsed;
            if (nextTickIndex >= _configuration.TimeoutTick)
            {
                _flight.Elapsed = _configuration.FlightTimeoutSeconds;
                float terminalPublicationTime = (_flight.StartTick
                    + nextTickIndex) * _clock.TickInterval;
                terminal = CompleteWithNoise(BurstTerminalEvent.Timeout,
                    "timeout-before-contact", null, null, next, null,
                    terminalPublicationTime);
                return true;
            }
            return false;
        }

        private BurstTerminalRecord CompleteWithNoise(BurstTerminalEvent eventType,
            string comparisonResult, float? contactFraction,
            float? contactEventTime, Vector3 terminalPosition,
            Vector3? surfaceContactPosition, float terminalPublicationTime)
        {
            if (_state != BurstFlightState.InFlight || _flight == null)
                return _lastTerminal;

            BurstThrowSnapshot snapshot = _flight.Snapshot;
            EventPublishResult submission = _noiseEmitter.SubmitBurst(
                snapshot.SessionId, snapshot.AttemptEpoch,
                snapshot.FlightHandleId, snapshot.Publisher, terminalPosition,
                _configuration.HearingRadiusMeters, snapshot.SourceTimestamp,
                terminalPublicationTime, "burst-landing");
            BurstFactPublicationState publicationState =
                submission.Admission == EventAdmission.Rejected
                    ? BurstFactPublicationState.Rejected
                    : BurstFactPublicationState.Pending;
            if (submission.Admission == EventAdmission.Rejected)
                _lastRejectionCode = string.IsNullOrWhiteSpace(submission.Code)
                    ? "burst-noise-publication-rejected" : submission.Code;

            BurstTerminalRecord terminal = new BurstTerminalRecord(
                snapshot.SessionId, snapshot.AttemptEpoch,
                snapshot.FlightHandleId, eventType, _flight.Elapsed,
                contactFraction, contactEventTime,
                _configuration.FlightTimeoutSeconds, comparisonResult,
                terminalPosition, surfaceContactPosition, publicationState, null,
                terminalPublicationTime,
                terminalPublicationTime, _flight.TickIndex,
                snapshot.SourceTimestamp, snapshot.ResolvedVelocity,
                GetTerminalCause(eventType), BurstFlightState.Consumed,
                publicationState == BurstFactPublicationState.Rejected
                    ? (float?)null : terminalPublicationTime,
                _flight.TickIndex);
            _flight = null;
            _state = BurstFlightState.Consumed;
            _lastTerminal = terminal;
            _hasLastTerminal = true;
            return terminal;
        }

        private void CompleteWithoutNoise(BurstTerminalEvent eventType,
            string comparisonResult, int? terminalTickIndex = null,
            float? terminalElapsed = null,
            float? terminalPublicationTime = null)
        {
            if (_state != BurstFlightState.InFlight || _flight == null)
                return;
            BurstThrowSnapshot snapshot = _flight.Snapshot;
            int resolvedTickIndex = terminalTickIndex.HasValue
                ? terminalTickIndex.Value : _flight.TickIndex;
            float resolvedElapsed = terminalElapsed.HasValue
                ? terminalElapsed.Value : _flight.Elapsed;
            BurstTerminalRecord terminal = new BurstTerminalRecord(
                snapshot.SessionId, snapshot.AttemptEpoch,
                snapshot.FlightHandleId, eventType, resolvedElapsed,
                null, null, _configuration.FlightTimeoutSeconds,
                comparisonResult, _flight.Position, null,
                BurstFactPublicationState.NotApplicable, null,
                _clock.CurrentTime, terminalPublicationTime, resolvedTickIndex,
                snapshot.SourceTimestamp, snapshot.ResolvedVelocity,
                GetTerminalCause(eventType), BurstFlightState.Consumed,
                null, resolvedTickIndex);
            _flight = null;
            _state = BurstFlightState.Consumed;
            _lastTerminal = terminal;
            _hasLastTerminal = true;
        }

        private BurstTerminalRecord WithPublicationState(
            BurstFactPublicationState state, ulong? factId)
        {
            return new BurstTerminalRecord(_lastTerminal.SessionId,
                _lastTerminal.AttemptEpoch, _lastTerminal.FlightHandleId,
                _lastTerminal.TerminalEvent, _lastTerminal.FlightElapsedSeconds,
                _lastTerminal.ContactFraction, _lastTerminal.ContactEventTime,
                _lastTerminal.TimeoutBoundarySeconds,
                _lastTerminal.ComparisonResult, _lastTerminal.TerminalPosition,
                _lastTerminal.SurfaceContactPosition, state, factId,
                _lastTerminal.Timestamp,
                _lastTerminal.TerminalPublicationTime,
                _lastTerminal.TerminalTickIndex,
                _lastTerminal.SourceTimestamp, _lastTerminal.ResolvedVelocity,
                _lastTerminal.TerminalCause, _lastTerminal.BurstStateAfter,
                state == BurstFactPublicationState.Rejected
                    || state == BurstFactPublicationState.Invalidated
                    ? (float?)null : _lastTerminal.TPublish,
                _lastTerminal.WinningTickIndex);
        }

        private static string GetTerminalCause(BurstTerminalEvent eventType)
        {
            return eventType == BurstTerminalEvent.Contact
                ? "Collision" : eventType.ToString();
        }

        private BurstThrowAdmission Reject(string code)
        {
            return Reject(code, code);
        }

        private BurstThrowAdmission Reject(string stableCode, string diagnosticCode)
        {
            _lastRejectionCode = string.IsNullOrWhiteSpace(stableCode)
                ? "burst-input-rejected" : stableCode;
            _lastRejectionDiagnosticCode = string.IsNullOrWhiteSpace(diagnosticCode)
                ? _lastRejectionCode : diagnosticCode;
            return BurstThrowAdmission.Rejected;
        }

        private static float NormalizeAzimuthDegrees(Vector3 direction)
        {
            float planarMagnitude = Mathf.Sqrt(direction.x * direction.x
                + direction.z * direction.z);
            if (!IsFinite(planarMagnitude) || planarMagnitude <= 0.0001f)
                return 0f;
            float degrees = Mathf.Atan2(direction.x, direction.z)
                * Mathf.Rad2Deg;
            if (degrees < 0f) degrees += 360f;
            return degrees >= 360f ? degrees - 360f : degrees;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
