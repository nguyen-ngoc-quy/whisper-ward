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
            float resolvedPlanarSpeed)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || attemptEpoch < 0)
                throw new ArgumentException("throw-envelope");
            if (!IsFinite(feetOrigin) || !IsFinite(cameraAzimuth)
                || !IsFinite(throwerVelocity))
                throw new ArgumentException("throw-snapshot-position");
            if (!IsFinite(sourceTimestamp) || !IsFinite(resolvedPlanarSpeed)
                || resolvedPlanarSpeed < 0f)
                throw new ArgumentException("throw-snapshot-timing");
            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            Publisher = string.IsNullOrWhiteSpace(publisher)
                ? "BurstSimulation" : publisher;
            FeetOrigin = feetOrigin;
            CameraAzimuth = cameraAzimuth;
            ThrowerVelocity = throwerVelocity;
            SourceTimestamp = sourceTimestamp;
            LocomotionLabel = locomotionLabel;
            StanceLabel = stanceLabel;
            ResolvedPlanarSpeed = resolvedPlanarSpeed;
        }

        public string SessionId { get; }
        public long AttemptEpoch { get; }
        public string Publisher { get; }
        public Vector3 FeetOrigin { get; }
        public Vector3 CameraAzimuth { get; }
        public Vector3 ThrowerVelocity { get; }
        public float SourceTimestamp { get; }
        public BurstLocomotionLabel LocomotionLabel { get; }
        public BurstStanceLabel StanceLabel { get; }
        public float ResolvedPlanarSpeed { get; }

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
            float resolvedPlanarSpeed)
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
            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            FlightHandleId = flightHandleId;
            Publisher = string.IsNullOrWhiteSpace(publisher)
                ? "BurstSimulation" : publisher;
            FeetOrigin = feetOrigin;
            CameraAzimuth = cameraAzimuth;
            ThrowerVelocity = throwerVelocity;
            SourceTimestamp = sourceTimestamp;
            LocomotionLabel = locomotionLabel;
            StanceLabel = stanceLabel;
            ResolvedPlanarSpeed = resolvedPlanarSpeed;
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
        public Vector3 ThrowerVelocity { get; }
        public float SourceTimestamp { get; }
        public BurstLocomotionLabel LocomotionLabel { get; }
        public BurstStanceLabel StanceLabel { get; }
        public float ResolvedPlanarSpeed { get; }

        private static ulong ParseHandle(string value)
        {
            ulong handle;
            if (!ulong.TryParse(value, NumberStyles.None,
                CultureInfo.InvariantCulture, out handle) || handle == 0)
                throw new ArgumentException("flightHandleId");
            return handle;
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
            float timestamp, float terminalPublicationTime, int terminalTickIndex)
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
        public float TerminalPublicationTime { get; }
        /// <summary>Fixed simulation tick that resolved the terminal event.</summary>
        public int TerminalTickIndex { get; }
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
            public int TickIndex;
            public float Accumulator;
        }

        private readonly BurstRuntimeConfiguration _configuration;
        private readonly IBurstPhysicsQuery _physicsQuery;
        private readonly NoiseEmitter _noiseEmitter;
        private readonly IVirtualTickClock _clock;
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
        private bool _disposed;

        /// <summary>
        /// Creates a Burst owner from one validated registry profile and injected
        /// physics, clock, and noise publication seams.
        /// </summary>
        public BurstSimulationService(BurstRuntimeConfiguration configuration,
            IBurstPhysicsQuery physicsQuery, NoiseEmitter noiseEmitter,
            IVirtualTickClock clock)
        {
            _configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            _physicsQuery = physicsQuery
                ?? throw new ArgumentNullException(nameof(physicsQuery));
            _noiseEmitter = noiseEmitter
                ?? throw new ArgumentNullException(nameof(noiseEmitter));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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
                _lastTerminal.TerminalTickIndex);
        }

        /// <summary>Most recent fixed-step batch result.</summary>
        public BurstStepBatchResult LastBatch { get { return _lastBatch; } }

        /// <summary>Stable code for the last rejected pickup or Throw edge.</summary>
        public string LastRejectionCode { get { return _lastRejectionCode; } }

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
            if (Vector3.Distance(playerPosition, pickupPosition)
                > _configuration.PickupReachRadiusMeters)
                return Reject("burst-pickup-out-of-reach");

            _carriedPickupId = pickupId;
            _state = BurstFlightState.Carried;
            _lastRejectionCode = string.Empty;
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
                || !IsFinite(request.SourceTimestamp))
                return Reject("burst-throw-invalid-snapshot");
            if (request.LocomotionLabel != BurstLocomotionLabel.Stationary)
                return Reject("burst-throw-locomotion-not-stationary");
            if (request.StanceLabel != BurstStanceLabel.Standing
                && request.StanceLabel != BurstStanceLabel.Crouched)
                return Reject("burst-throw-unsupported-stance");
            if (request.ResolvedPlanarSpeed > 0.0001f)
                return Reject("burst-throw-planar-speed-positive");

            Vector3 direction = new Vector3(request.CameraAzimuth.x, 0f,
                request.CameraAzimuth.z);
            float directionMagnitude = direction.magnitude;
            if (directionMagnitude <= 0f)
                return Reject("burst-throw-invalid-azimuth");
            direction /= directionMagnitude;

            Vector3 origin = request.FeetOrigin
                + Vector3.up * _configuration.ReleaseHeightMeters;
            if (_physicsQuery.HasInitialOverlap(origin,
                _configuration.ProjectileRadiusMeters))
                return Reject("INITIAL_OVERLAP_REJECTED");
            if (_nextFlightHandle == 0)
                return Reject("burst-flight-handle-exhausted");

            ulong handle = _nextFlightHandle++;
            acceptedSnapshot = new BurstThrowSnapshot(request.SessionId,
                request.AttemptEpoch, handle, request.Publisher,
                request.FeetOrigin, request.CameraAzimuth,
                request.ThrowerVelocity, request.SourceTimestamp,
                request.LocomotionLabel, request.StanceLabel,
                request.ResolvedPlanarSpeed);
            return StartFlight(acceptedSnapshot, origin, direction);
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

        private BurstThrowAdmission StartFlight(BurstThrowSnapshot snapshot,
            Vector3 origin, Vector3 direction)
        {
            float angleRadians = _configuration.LaunchAngleDegrees * Mathf.Deg2Rad;
            float speed = _configuration.InitialSpeedMetersPerSecond;
            Vector3 velocity = direction * (speed * Mathf.Cos(angleRadians))
                + Vector3.up * (speed * Mathf.Sin(angleRadians));

            _flight = new Flight
            {
                Snapshot = snapshot,
                Position = origin,
                Velocity = velocity,
                Elapsed = 0f,
                TickIndex = 0,
                Accumulator = 0f
            };
            _hasLastTerminal = false;
            _state = BurstFlightState.InFlight;
            _carriedPickupId = string.Empty;
            _lastRejectionCode = string.Empty;
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
                "death-cancelled");
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
            _lastRejectionCode = string.Empty;
        }

        /// <summary>
        /// Advances one injected virtual-clock callback. At most the configured
        /// number of fixed ticks execute; excess due ticks are discarded while
        /// the fractional accumulator remainder is retained.
        /// </summary>
        public BurstStepBatchResult Advance(long tick, float delta)
        {
            if (_state != BurstFlightState.InFlight || _flight == null
                || _clock.IsPaused || delta <= 0f)
            {
                _lastBatch = new BurstStepBatchResult(0, 0, false, false,
                    default(BurstTerminalRecord));
                return _lastBatch;
            }
            if (float.IsNaN(delta) || float.IsInfinity(delta))
                throw new ArgumentException("Gameplay delta must be finite", nameof(delta));

            float fixedStep = _configuration.FixedSubstepSeconds;
            _flight.Accumulator += delta;
            int dueTicks = (int)Math.Floor(_flight.Accumulator / fixedStep);
            if (dueTicks <= 0)
            {
                _lastBatch = new BurstStepBatchResult(0, 0, false, false,
                    default(BurstTerminalRecord));
                return _lastBatch;
            }

            // Consume all due whole ticks, retaining only the fractional
            // remainder; the bounded cap controls simulation work, not time.
            _flight.Accumulator -= dueTicks * fixedStep;
            int simulatedTicks = Math.Min(dueTicks, _configuration.MaxBacklogTicks);
            int discardedTicks = dueTicks - simulatedTicks;
            bool backlogClamped = discardedTicks > 0;
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
                float segmentLength = Vector3.Distance(current, next);
                float fraction = segmentLength <= 0f ? 0f
                    : Mathf.Clamp01(contact.DistanceAlongSegment / segmentLength);
                float contactEventTime = _flight.Elapsed + fraction * fixedStep;
                bool contactWithinAuthority = nextTickIndex
                    <= _configuration.TimeoutTick
                    && contactEventTime <= _configuration.FlightTimeoutSeconds;
                if (contactWithinAuthority)
                {
                    Vector3 publishedContact = BurstCollisionResolver.GetPublishedContact(
                        contact, _configuration.ProjectileRadiusMeters,
                        _configuration.EpsilonContactMeters);
                    _flight.Position = publishedContact;
                    _flight.Velocity = nextVelocity;
                    _flight.TickIndex = nextTickIndex;
                    _flight.Elapsed = contactEventTime;
                    terminal = CompleteWithNoise(BurstTerminalEvent.Contact,
                        "contact-before-or-at-timeout", fraction,
                        contactEventTime, publishedContact,
                        contact.ColliderSurfaceContact,
                        _flight.Snapshot.SourceTimestamp + contactEventTime);
                    return true;
                }
            }

            _flight.Position = next;
            _flight.Velocity = nextVelocity;
            _flight.TickIndex = nextTickIndex;
            _flight.Elapsed = nextElapsed;
            if (nextTickIndex >= _configuration.TimeoutTick
                || nextElapsed >= _configuration.FlightTimeoutSeconds)
            {
                _flight.Elapsed = _configuration.FlightTimeoutSeconds;
                terminal = CompleteWithNoise(BurstTerminalEvent.Timeout,
                    "timeout-before-contact", null, null, next, null,
                    _flight.Snapshot.SourceTimestamp
                        + _configuration.FlightTimeoutSeconds);
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
                _clock.CurrentTime,
                terminalPublicationTime, _flight.TickIndex);
            _flight = null;
            _state = BurstFlightState.Consumed;
            _lastTerminal = terminal;
            _hasLastTerminal = true;
            return terminal;
        }

        private void CompleteWithoutNoise(BurstTerminalEvent eventType,
            string comparisonResult)
        {
            if (_state != BurstFlightState.InFlight || _flight == null)
                return;
            BurstThrowSnapshot snapshot = _flight.Snapshot;
            BurstTerminalRecord terminal = new BurstTerminalRecord(
                snapshot.SessionId, snapshot.AttemptEpoch,
                snapshot.FlightHandleId, eventType, _flight.Elapsed,
                null, null, _configuration.FlightTimeoutSeconds,
                comparisonResult, _flight.Position, null,
                BurstFactPublicationState.NotApplicable, null,
                _clock.CurrentTime, _clock.CurrentTime, _flight.TickIndex);
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
                _lastTerminal.TerminalTickIndex);
        }

        private BurstThrowAdmission Reject(string code)
        {
            _lastRejectionCode = string.IsNullOrWhiteSpace(code)
                ? "burst-input-rejected" : code;
            return BurstThrowAdmission.Rejected;
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
