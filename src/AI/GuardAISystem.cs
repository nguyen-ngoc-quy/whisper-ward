// Guard AI System - Entry point for Unity
using System;
using System.Collections.Generic;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;
using WhisperWard.AI.SuspicionGrade;
using WhisperWard.AI.Testing;

public class GuardAISystem : MonoBehaviour
{
    [Header("System Configuration")]
    [SerializeField] private GuardFSM fsm;
    [SerializeField] private PerceptionDriver perceptionDriver;
    [SerializeField] private DecisionTap decisionTap;

    [Header("Session Dependencies")]
    [SerializeField] private VirtualTickClock clock;
    [SerializeField] private string sessionId = "session-01";
    [SerializeField] private long initialAttemptEpoch = 1;
    [SerializeField] private string[] requiredWorldLayers = new[] { "World" };
    [SerializeField] private string[] forbiddenPhysicsLayers = new[] { "Player", "Guard", "trigger" };

    [Header("Noise Runtime Contract")]
    [SerializeField] private NoiseResponseProfile noiseResponseProfile = NoiseResponseProfile.MVP;
    [SerializeField] private float hearingIntervalSeconds =
        NoiseRuntimeConfiguration.RegisteredHearingIntervalSeconds;
    [SerializeField] private float hearingYHardCutoff = 4.0f;
    [SerializeField] private float guardEyeHeight = 1.6f;
    [SerializeField] private int maxHearingGuards = 30;
    [SerializeField] private int maxHearingFacts = 8;
    [SerializeField] private int maxHearingPairs = 30;
    [SerializeField] private int hearingWorkCapacity = 512;
    [SerializeField] private int rawHearingQueueCapacity = 128;
    [SerializeField] private int relayQueueCapacity = 512;
    [SerializeField] private int maxDeferredHearingBoundaries = 4;
    [SerializeField] private int maxHearingBoundariesPerFrame = 1;
    [SerializeField] private int maxNoiseRetries = 3;
    [SerializeField] private float compareToleranceMeters = 0.005f;
    [SerializeField] private float mathRelativeTolerance = 0.000001f;
    [SerializeField] private float noiseOriginOffset =
        NoiseRuntimeConfiguration.RegisteredF12NoiseOriginOffset;
    [SerializeField] private float investigateBaseSeconds = 4.0f;
    [SerializeField] private float noiseReanchorExtendSeconds = 2.0f;
    [SerializeField] private float reanchorMaxMeters = 1.0f;
    [SerializeField] private float thoroughnessCoefficient = 0.5f;

    [Header("Suspicion Grade Contract")]
    [SerializeField] private string suspicionGradeRoomId = "room-01";

    [Header("Burst Runtime Contract")]
    [SerializeField] private float burstInitialSpeedMetersPerSecond = 10.0f;
    [SerializeField] private float burstLaunchAngleDegrees = 38.0f;
    [SerializeField] private float burstReleaseHeightMeters = 1.5f;
    [SerializeField] private float burstGravityMagnitude = 9.81f;
    [SerializeField] private float burstFixedSubstepSeconds = 0.0083333333f;
    [SerializeField] private float burstFlightTimeoutSeconds = 3.0f;
    [SerializeField] private float burstProjectileRadiusMeters = 0.05f;
    [SerializeField] private float burstEpsilonContactMeters = 0.001f;
    [SerializeField] private int burstMaxBacklogTicks = 8;
    [SerializeField] private float burstHearingRadiusMeters = 10.5f;
    [SerializeField] private float burstPickupReachRadiusMeters = 1.6f;

    private IEventBus _eventBus;
    private IVirtualTickClock _virtualClock;
    private SessionPhaseCoordinator _phaseCoordinator;
    private SessionBoundaryService _boundary;
    private NoiseEmitter _noiseEmitter;
    private PerceptionHearingService _hearing;
    private BurstSimulationService _burst;
    private NoiseRuntimeConfiguration _noiseConfiguration;
    private BurstRuntimeConfiguration _burstConfiguration;
    private PhysicsQueryProfile _physicsProfile;
    private SuspicionGradeConfiguration _suspicionGradeConfiguration;
    private ISuspicionMeterPresenter _suspicionGradePresenter;
    private IEventDiagnosticSink _suspicionGradeDiagnosticSink;
    private SuspicionGradeSystem _suspicionGrade;
    private bool _isInitialized;
    private SubscriptionToken _boundaryToken;
    private string _lastBoundarySessionId;
    private bool _isStarted;

    private sealed class SingleGuardSnapshotProvider : IGuardHearingSnapshotProvider
    {
        private readonly GuardFSM _fsm;
        private readonly float _eyeHeight;

        public SingleGuardSnapshotProvider(GuardFSM fsm, float eyeHeight)
        {
            _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
            _eyeHeight = eyeHeight;
        }

        public IList<GuardHearingSnapshot> CaptureActiveGuardSnapshots()
        {
            var snapshots = new List<GuardHearingSnapshot>(1);
            snapshots.Add(new GuardHearingSnapshot(_fsm.GuardEid,
                _fsm.transform.position, new Vector3(0f, _eyeHeight, 0f), 0f));
            return snapshots;
        }
    }

    private void Awake()
    {
        Debug.Assert(fsm != null, "GuardFSM not assigned to GuardAISystem");
        Debug.Assert(decisionTap != null, "DecisionTap not assigned to GuardAISystem");
        _eventBus = new SessionEventBus(256, maxNoiseRetries);
    }

    /// <summary>
    /// Injects the session-scoped event transport before this composition root starts.
    /// The static EventBus facade is intentionally not used as an authoritative path.
    /// </summary>
    public void ConfigureEventBus(IEventBus eventBus)
    {
        if (_isInitialized)
            throw new InvalidOperationException("Event bus cannot change after initialization");
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <summary>
    /// Injects the registry-backed Suspicion Meter / Grade configuration and its
    /// optional read-only presentation and diagnostic adapters.
    /// </summary>
    /// <param name="configuration">Validated registry-backed tuning contract.</param>
    /// <param name="presenter">Optional unavailable-safe meter presenter.</param>
    /// <param name="diagnosticSink">Optional serialized diagnostic sink.</param>
    /// <param name="roomId">Explicit room identity for DecisionRecord adaptation.</param>
    public void ConfigureSuspicionGrade(
        SuspicionGradeConfiguration configuration,
        ISuspicionMeterPresenter presenter = null,
        IEventDiagnosticSink diagnosticSink = null,
        string roomId = null)
    {
        if (_isInitialized)
            throw new InvalidOperationException(
                "Suspicion Grade cannot change after initialization");
        _suspicionGradeConfiguration = configuration
            ?? throw new ArgumentNullException(nameof(configuration));
        _suspicionGradePresenter = presenter;
        _suspicionGradeDiagnosticSink = diagnosticSink;
        if (!string.IsNullOrWhiteSpace(roomId))
            suspicionGradeRoomId = roomId;
    }

    private void Start()
    {
        if (fsm == null || _isInitialized) return;

        IEventBus eventBus = _eventBus ?? new SessionEventBus(256, maxNoiseRetries);
        _eventBus = eventBus;
        IVirtualTickClock virtualClock = clock != null
            ? clock
            : fsm.GetComponent<VirtualTickClock>();
        if (virtualClock == null)
        {
            Debug.LogError("[GuardAISystem] A session virtual clock is required");
            return;
        }
        _virtualClock = virtualClock;
        if (Mathf.Abs(_virtualClock.TickInterval
                - VirtualTickClockService.CanonicalTickInterval) > 0.000001f)
        {
            Debug.LogError("[GuardAISystem] Shared clock must use canonical 1/120 s cadence");
            return;
        }
        if (string.IsNullOrWhiteSpace(sessionId) || initialAttemptEpoch < 0)
        {
            Debug.LogError("[GuardAISystem] Session envelope configuration is invalid");
            return;
        }
        if (!IsFinitePositive(hearingYHardCutoff)
            || Mathf.Abs(hearingYHardCutoff
                - NoiseRuntimeConfiguration.RegisteredHearingYHardCutoff)
                > 0.000001f
            || !IsFiniteNonNegative(guardEyeHeight)
            || maxHearingGuards <= 0 || maxHearingFacts <= 0
            || maxHearingPairs <= 0 || hearingWorkCapacity <= 0
            || rawHearingQueueCapacity <= 0 || relayQueueCapacity <= 0
            || maxDeferredHearingBoundaries <= 0
            || maxHearingBoundariesPerFrame <= 0)
        {
            Debug.LogError("[GuardAISystem] Hearing endpoint/capacity configuration is invalid");
            return;
        }

        PhysicsQueryProfile physicsProfile;
        string errorCode;
        if (!PhysicsQueryProfile.TryCreate(requiredWorldLayers,
            forbiddenPhysicsLayers, out physicsProfile, out errorCode))
        {
            Debug.LogError("[GuardAISystem] E20 profile rejected: " + errorCode);
            return;
        }
        _physicsProfile = physicsProfile;

        try
        {
            _noiseConfiguration = new NoiseRuntimeConfiguration(noiseResponseProfile,
                hearingIntervalSeconds, maxHearingGuards, maxHearingFacts, maxHearingPairs,
                hearingWorkCapacity, rawHearingQueueCapacity, relayQueueCapacity,
                maxDeferredHearingBoundaries, maxHearingBoundariesPerFrame,
                maxNoiseRetries, compareToleranceMeters, mathRelativeTolerance,
                noiseOriginOffset, investigateBaseSeconds,
                noiseReanchorExtendSeconds, reanchorMaxMeters,
                thoroughnessCoefficient);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("[GuardAISystem] Noise profile rejected: " + exception.Message);
            return;
        }

        try
        {
            _burstConfiguration = new BurstRuntimeConfiguration(
                burstInitialSpeedMetersPerSecond, burstLaunchAngleDegrees,
                burstReleaseHeightMeters, burstGravityMagnitude,
                burstFixedSubstepSeconds, burstFlightTimeoutSeconds,
                burstProjectileRadiusMeters, burstEpsilonContactMeters,
                burstMaxBacklogTicks, burstHearingRadiusMeters,
                burstPickupReachRadiusMeters);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("[GuardAISystem] Burst profile rejected: " + exception.Message);
            return;
        }

        // Establish the lifecycle generation before registering gameplay sources.
        _boundary = new SessionBoundaryService(eventBus);
        _boundaryToken = _boundary.Subscribe(OnSessionBoundary);
        _lastBoundarySessionId = null;
        _boundary.BeginSession(sessionId, initialAttemptEpoch);

        if (_suspicionGradeConfiguration != null)
        {
            _suspicionGrade = new SuspicionGradeSystem(eventBus, _boundary,
                _suspicionGradeConfiguration, _suspicionGradePresenter,
                _suspicionGradeDiagnosticSink);
            _suspicionGrade.BindDecisionContext(suspicionGradeRoomId,
                fsm.GuardEid);
        }
        else
        {
            Debug.LogWarning(
                "[GuardAISystem] Suspicion Grade is disabled until registry configuration is injected");
        }

        _phaseCoordinator = new SessionPhaseCoordinator(eventBus, virtualClock);
        fsm.ConfigureNoiseResponseProfile(noiseResponseProfile);
        fsm.ConfigureNoiseRecommitCooldown(
            _noiseConfiguration.NoiseRecommitCooldownSeconds);
        fsm.ConfigureLivenessRetryLimit(_noiseConfiguration.MaxRetries);
        fsm.ConfigureFsmCadence(_noiseConfiguration.FsmIntervalSeconds);
        fsm.ConfigureWithPhaseCoordinator(eventBus, virtualClock,
            eventBus.SessionId, eventBus.AttemptEpoch, _phaseCoordinator);

        _noiseEmitter = new NoiseEmitter(eventBus);
        // BurstCollisionResolver pins Physics.autoSyncTransforms=false for the
        // service lifetime and restores the project setting when disposed.
        _burst = new BurstSimulationService(_burstConfiguration,
            new BurstCollisionResolver(physicsProfile), _noiseEmitter, virtualClock);
        _hearing = new PerceptionHearingService(eventBus, virtualClock,
            physicsProfile, new SingleGuardSnapshotProvider(fsm, guardEyeHeight),
            hearingYHardCutoff, _noiseConfiguration);
        _hearing.BindWithoutClock();
        // A Burst terminal source must stage before the emitter flushes; this
        // makes landing and timeout publications part of the same ingress order.
        _phaseCoordinator.RegisterIngressSource(_burst.Advance);
        _phaseCoordinator.RegisterIngressSource(FlushNoiseSources);
        _phaseCoordinator.RegisterIngressSource(ResolvePendingBurstPublication);
        _phaseCoordinator.RegisterHearingParticipant(_hearing.ProcessHearingTick);

        if (decisionTap != null)
            decisionTap.Configure(eventBus);

        fsm.GetChaseState().ConfigurePhysicsProfile(physicsProfile);
        fsm.GetInvestigateState().ConfigurePhysicsProfile(physicsProfile);
        fsm.GetInvestigateState().ConfigureNoiseRuntime(_noiseConfiguration);

        // Hearing binds before the phase coordinator so its boundary work is
        // evaluated and its relays are available to the same FSM phase drain.
        _phaseCoordinator.Bind();
        _isInitialized = true;
        _isStarted = true;
        Debug.Log("[GuardAISystem] Initialized production noise path");
    }

    private void OnEnable()
    {
        if (!_isInitialized || _isStarted) return;

        if (_boundary != null)
            _boundaryToken = _boundary.Subscribe(OnSessionBoundary);
        if (_suspicionGrade != null)
            _suspicionGrade.Bind();
        if (_fsmCanRebind())
            fsm.ConfigureWithPhaseCoordinator(_eventBus, _virtualClock,
                _eventBus.SessionId, _eventBus.AttemptEpoch, _phaseCoordinator);
        if (_hearing != null)
            _hearing.BindWithoutClock();
        if (_phaseCoordinator != null)
        {
            if (_burst != null)
                _phaseCoordinator.RegisterIngressSource(_burst.Advance);
            _phaseCoordinator.RegisterIngressSource(FlushNoiseSources);
            _phaseCoordinator.RegisterIngressSource(ResolvePendingBurstPublication);
            if (_hearing != null)
                _phaseCoordinator.RegisterHearingParticipant(_hearing.ProcessHearingTick);
            _phaseCoordinator.Bind();
        }
        if (decisionTap != null)
            decisionTap.Configure(_eventBus);
        _isStarted = true;
    }

    private bool _fsmCanRebind()
    {
        return fsm != null && _virtualClock != null && _phaseCoordinator != null;
    }

    private void OnDisable()
    {
        _isStarted = false;
        if (_suspicionGrade != null)
            _suspicionGrade.Unbind();
        if (_boundary != null)
            _boundary.Unsubscribe(_boundaryToken);
        if (_phaseCoordinator != null)
        {
            if (_burst != null)
                _phaseCoordinator.UnregisterIngressSource(_burst.Advance);
            _phaseCoordinator.UnregisterIngressSource(FlushNoiseSources);
            _phaseCoordinator.UnregisterIngressSource(ResolvePendingBurstPublication);
            if (_hearing != null)
                _phaseCoordinator.UnregisterHearingParticipant(_hearing.ProcessHearingTick);
            _phaseCoordinator.Unbind();
        }
        if (_hearing != null)
            _hearing.Unbind();
    }

    private void OnDestroy()
    {
        // Unity normally invokes OnDisable first, but make destruction itself a
        // complete teardown boundary so no session, phase, or bus subscription can
        // outlive this composition root in test or unusual lifecycle paths.
        OnDisable();
        if (_suspicionGrade != null)
            _suspicionGrade.Dispose();
        if (_burst != null)
            _burst.Dispose();
    }

    private void OnSessionBoundary(string activeSessionId, long activeEpoch)
    {
        bool isNewSession = _boundary != null
            && _boundary.IsNewSessionTransition;
        if (_noiseEmitter != null)
            _noiseEmitter.ResetForBoundary(activeSessionId);
        if (_burst != null)
            _burst.ResetForBoundary(activeSessionId, activeEpoch,
                !isNewSession);
        if (_hearing != null)
            _hearing.ResetForBoundary();
        if (fsm != null)
            fsm.ResetForBoundary(activeSessionId, activeEpoch);
        if (perceptionDriver != null)
            perceptionDriver.ResetForBoundary();
        // SuspicionGradeSystem owns its SessionBoundaryService subscription so
        // this composition callback cannot reset it twice per transition.
        _lastBoundarySessionId = activeSessionId;
    }

    private void FlushNoiseSources(long tick, float delta)
    {
        if (_noiseEmitter != null)
            _noiseEmitter.Flush();
    }

    private void ResolvePendingBurstPublication(long tick, float delta)
    {
        if (_burst != null)
            _burst.ResolvePendingTerminalPublication();
    }

    /// <summary>
    /// Advances the session-owned gameplay clock. A gameplay/session owner must
    /// call this with virtual gameplay delta; render Time.deltaTime is never used
    /// as the production authority.
    /// </summary>
    public void AdvanceGameplayTime(float gameplayDelta)
    {
        if (_virtualClock == null)
            throw new InvalidOperationException("guard-ai-clock-not-started");
        _virtualClock.Advance(gameplayDelta);
    }

    /// <summary>Stages an authoritative Movement or Burst source record.</summary>
    public EventPublishResult SubmitNoise(NoiseSourceRecord record)
    {
        if (_noiseEmitter == null)
            return EventPublishResult.Rejected("guard-ai-not-started");
        return _noiseEmitter.Submit(record);
    }

    /// <summary>Returns the production raw-noise emitter.</summary>
    public NoiseEmitter NoiseEmitter { get { return _noiseEmitter; } }

    /// <summary>Returns the production hearing service.</summary>
    public PerceptionHearingService Hearing { get { return _hearing; } }

    /// <summary>Returns the production Burst lifecycle/simulation service.</summary>
    public BurstSimulationService Burst { get { return _burst; } }

    public GuardFSM FSM => fsm;
    public DecisionTap DecisionTap => decisionTap;

    /// <summary>Returns the injected Suspicion Meter / Grade runtime adapter.</summary>
    public SuspicionGradeSystem SuspicionGrade { get { return _suspicionGrade; } }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
