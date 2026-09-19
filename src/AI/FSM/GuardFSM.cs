using System;
using System.Collections.Generic;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;
using WhisperWard.AI.FSM.States;

namespace WhisperWard.AI.FSM
{
    /// <summary>
    /// The three-state Guard AI controller. Sensing arrives through a queued,
    /// session-scoped bus and is consumed at the FSM virtual-clock boundary.
    /// </summary>
    public class GuardFSM : MonoBehaviour
    {
        [Header("FSM Configuration")]
        public GuardGoalMode currentGoalMode = GuardGoalMode.None;
        public string currentEntryId = null;
        [SerializeField] private string guardEid = "guard-01";
        [SerializeField] private string sessionId = "default";
        [SerializeField] private long attemptEpoch = 0;
        [SerializeField] private NoiseResponseProfile noiseResponseProfile = NoiseResponseProfile.MVP;

        private IGuardState _currentState;
        private bool _isTransitioning;
        private bool _isBound;
        private bool _hasExplicitEnvelope;
        private IEventBus _eventBus;
        private IVirtualTickClock _clock;
        private SessionPhaseCoordinator _phaseCoordinator;
        private bool _phaseDriven;
        private SubscriptionToken _clockToken;
        private SubscriptionToken _factToken;
        private bool _factsSubscribed;
        private sealed class PendingLiveness
        {
            public LivenessFact Fact;
            public int RetryCount;
        }

        private readonly List<ISensingFact> _pendingFacts = new List<ISensingFact>();
        private readonly List<PendingLiveness> _pendingLiveness =
            new List<PendingLiveness>();
        private readonly HashSet<string> _consumedRelays = new HashSet<string>(StringComparer.Ordinal);
        private long _decisionSequence;
        private int _maxLivenessRetries = 3;
        private float _fsmIntervalSeconds;
        private float _fsmCadenceAccumulator;
        private float _noiseRecommitCooldownSeconds =
            NoiseRuntimeConfiguration.RegisteredNoiseRecommitCooldownSeconds;
        private float _noiseRecommitCooldownUntil;
        private string _lastLivenessFailureCode = string.Empty;
        private bool _suppressStateLiveness;

        // Cached states avoid allocations during gameplay.
        private readonly PatrolState _patrolState = new PatrolState();
        private readonly InvestigateState _investigateState = new InvestigateState();
        private readonly ChaseState _chaseState = new ChaseState();

        private void Awake()
        {
            TransitionTo(_patrolState);
        }

        private void OnEnable()
        {
            if (_phaseDriven && _phaseCoordinator != null)
            {
                _phaseCoordinator.RegisterFsmParticipant(ProcessTick);
                // A disable cycle unsubscribed the fact token; the participant
                // must resume receiving coordinator-phase relays with it.
                SubscribeFacts();
                return;
            }

            if (_eventBus != null && _clock != null)
                BindRuntime(_eventBus, _clock);
        }

        private void Start()
        {
            if (!_phaseDriven)
                BindRuntime(_eventBus ?? EventBus.Default, _clock ?? FindClock());
        }

        private void OnDisable()
        {
            UnbindRuntime();
            if (_phaseDriven && _phaseCoordinator != null)
                _phaseCoordinator.UnregisterFsmParticipant(ProcessTick);
        }

        private void OnDestroy()
        {
            UnbindRuntime();
            if (_phaseDriven && _phaseCoordinator != null)
                _phaseCoordinator.UnregisterFsmParticipant(ProcessTick);
        }

        /// <summary>
        /// Injects the session bus and virtual clock for production composition or
        /// deterministic tests. Calling this before Start replaces static adapters.
        /// </summary>
        public void Configure(IEventBus eventBus, IVirtualTickClock clock,
            string activeSessionId, long activeEpoch)
        {
            ConfigureDependencies(eventBus, clock, activeSessionId, activeEpoch, null);
        }

        /// <summary>
        /// Configures the FSM as a participant of the session-owned phase graph.
        /// The coordinator, rather than this component, drains event phases.
        /// </summary>
        public void ConfigureWithPhaseCoordinator(IEventBus eventBus,
            IVirtualTickClock clock, string activeSessionId, long activeEpoch,
            SessionPhaseCoordinator phaseCoordinator)
        {
            phaseCoordinator = phaseCoordinator
                ?? throw new ArgumentNullException(nameof(phaseCoordinator));
            ConfigureDependencies(eventBus, clock, activeSessionId, activeEpoch,
                phaseCoordinator);
        }

        private void ConfigureDependencies(IEventBus eventBus, IVirtualTickClock clock,
            string activeSessionId, long activeEpoch,
            SessionPhaseCoordinator phaseCoordinator)
        {
            eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            clock = clock ?? throw new ArgumentNullException(nameof(clock));
            bool envelopeChanged = !string.Equals(sessionId, activeSessionId,
                StringComparison.Ordinal) || attemptEpoch != activeEpoch;
            bool wasBound = _isBound;
            if (wasBound) UnbindRuntime();
            if (_phaseCoordinator != null)
                _phaseCoordinator.UnregisterFsmParticipant(ProcessTick);

            _eventBus = eventBus;
            _clock = clock;
            _phaseCoordinator = phaseCoordinator;
            _phaseDriven = phaseCoordinator != null;
            sessionId = activeSessionId ?? string.Empty;
            attemptEpoch = activeEpoch;
            _hasExplicitEnvelope = true;
            if (envelopeChanged)
                ClearBoundaryState();

            // The coordinator owns the tick in phase-driven mode, but sensing
            // facts still arrive through the same bus drain. Subscribe facts
            // explicitly; only the non-driven path binds the clock here.
            SubscribeFacts();
            if (_phaseDriven)
                _phaseCoordinator.RegisterFsmParticipant(ProcessTick);
            else if (wasBound)
                BindRuntime(_eventBus, _clock);
        }

        public void ConfigureNoiseResponseProfile(NoiseResponseProfile profile)
        {
            if (_isBound && noiseResponseProfile != profile)
                throw new InvalidOperationException("Noise response profile is immutable after binding");
            noiseResponseProfile = profile;
        }

        /// <summary>Applies the registered post-resolution noise cooldown.</summary>
        public void ConfigureNoiseRecommitCooldown(float seconds)
        {
            if (_isBound && !Mathf.Approximately(_noiseRecommitCooldownSeconds,
                seconds))
                throw new InvalidOperationException(
                    "Noise recommit cooldown is immutable after binding");
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            _noiseRecommitCooldownSeconds = seconds;
        }

        /// <summary>
        /// Applies the registry retry limit to FSM-owned liveness publication.
        /// Liveness facts use the same bounded transport policy as raw noise.
        /// </summary>
        public void ConfigureLivenessRetryLimit(int maxRetries)
        {
            if (maxRetries < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRetries));
            _maxLivenessRetries = maxRetries;
        }

        /// <summary>
        /// Applies the registered vision/FSM cadence. Fact consumption remains
        /// boundary-driven, while state timers and navigation updates run only at
        /// this lower-frequency cadence.
        /// </summary>
        public void ConfigureFsmCadence(float intervalSeconds)
        {
            if (float.IsNaN(intervalSeconds) || float.IsInfinity(intervalSeconds)
                || intervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            _fsmIntervalSeconds = intervalSeconds;
            _fsmCadenceAccumulator = 0f;
        }

        /// <summary>Stable terminal code for the latest liveness failure.</summary>
        public string LastLivenessFailureCode
        {
            get { return _lastLivenessFailureCode; }
        }

        /// <summary>
        /// Applies a lifecycle-owned session or epoch barrier and discards queued
        /// facts and relay reservations from the previous generation.
        /// </summary>
        public void ResetForBoundary(string activeSessionId, long activeEpoch)
        {
            if (string.IsNullOrWhiteSpace(activeSessionId))
                throw new ArgumentException("A session id is required", nameof(activeSessionId));
            if (activeEpoch < 0)
                throw new ArgumentOutOfRangeException(nameof(activeEpoch));

            string staleEntryId = currentEntryId;
            // SessionBoundaryService invokes this callback before advancing the bus.
            // Publish the close while the FSM and transport still share the old
            // envelope; the barrier then marks it stale with the rest of old work.
            if (!string.IsNullOrWhiteSpace(staleEntryId)
                && _eventBus != null)
            {
                PublishLivenessFact("close", GetLivenessTier(),
                    "epoch-transition-stale", _clock == null ? 0f : _clock.CurrentTime,
                    staleEntryId);
            }
            sessionId = activeSessionId;
            attemptEpoch = activeEpoch;
            _hasExplicitEnvelope = true;
            _suppressStateLiveness = true;
            if (_currentState != _patrolState)
                TransitionTo(_patrolState);
            _suppressStateLiveness = false;
            ClearBoundaryState();
        }

        private void ClearBoundaryState()
        {
            if (_pendingLiveness.Count > 0)
            {
                _lastLivenessFailureCode =
                    "fsm-liveness-boundary-discarded";
                _pendingLiveness.Clear();
            }
            _pendingFacts.Clear();
            _consumedRelays.Clear();
            _decisionSequence = 0;
            _fsmCadenceAccumulator = 0f;
            _noiseRecommitCooldownUntil = 0f;
            currentEntryId = null;
        }

        private void BindRuntime(IEventBus eventBus, IVirtualTickClock clock)
        {
            if (_isBound || eventBus == null || clock == null) return;
            _eventBus = eventBus;
            _clock = clock;
            if (string.IsNullOrWhiteSpace(sessionId)) sessionId = eventBus.SessionId;
            if (!_hasExplicitEnvelope) attemptEpoch = eventBus.AttemptEpoch;
            SubscribeFacts();
            _clockToken = _clock.Subscribe(OnTick);
            _isBound = true;
        }

        /// <summary>
        /// Subscribes sensing facts on the session bus. Split from the clock
        /// binding so a coordinator-driven FSM still receives FsmDecision-phase
        /// relays, which are dispatched by the bus drain rather than the clock.
        /// </summary>
        private void SubscribeFacts()
        {
            if (_factsSubscribed || _eventBus == null) return;
            _factToken = _eventBus.Subscribe<ISensingFact>(OnSensingFactReceived);
            _factsSubscribed = true;
        }

        private void UnbindRuntime()
        {
            if (_factsSubscribed && _eventBus != null)
            {
                _eventBus.Unsubscribe(_factToken);
                _factsSubscribed = false;
            }
            if (!_isBound) return;
            if (_clock != null) _clock.Unsubscribe(_clockToken);
            _isBound = false;
        }

        private IVirtualTickClock FindClock()
        {
            return GetComponent<VirtualTickClock>();
        }

        private void OnTick(long tick, float delta)
        {
            ProcessTick(tick, delta);
        }

        /// <summary>
        /// Consumes facts and updates the current state after the coordinator has
        /// drained the FsmDecision phase for this virtual boundary.
        /// </summary>
        public void ProcessTick(long tick, float delta)
        {
            FlushPendingLiveness();
            ProcessPendingFacts();
            if (_currentState == null || _isTransitioning)
                return;

            // A legacy direct binding has no cadence contract and retains its
            // historical per-callback update behavior. Production composition
            // always supplies the registry cadence.
            if (_fsmIntervalSeconds <= 0f)
            {
                _currentState.OnUpdate(this, tick, delta);
                return;
            }

            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0f)
                return;
            _fsmCadenceAccumulator += delta;
            const float tolerance = 0.000001f;
            if (_fsmCadenceAccumulator + tolerance < _fsmIntervalSeconds)
                return;

            int dueIntervals = (int)Math.Floor(
                (_fsmCadenceAccumulator + tolerance) / _fsmIntervalSeconds);
            _fsmCadenceAccumulator -= dueIntervals * _fsmIntervalSeconds;
            if (_fsmCadenceAccumulator < 0f)
                _fsmCadenceAccumulator = 0f;
            _currentState.OnUpdate(this, tick,
                dueIntervals * _fsmIntervalSeconds);
        }

        private void OnSensingFactReceived(ISensingFact fact)
        {
            if (fact == null || !IsCurrentEnvelope(fact)) return;
            var relay = fact as NoiseHeardRelay;
            if (relay != null
                && !string.Equals(relay.GuardEid, GuardEid, StringComparison.Ordinal))
                return;
            _pendingFacts.Add(fact);
        }

        private void ProcessPendingFacts()
        {
            if (_pendingFacts.Count == 0) return;
            _pendingFacts.Sort(CompareFacts);
            var batch = new List<ISensingFact>(_pendingFacts);
            _pendingFacts.Clear();

            for (int i = 0; i < batch.Count; i++)
            {
                if (!IsCurrentEnvelope(batch[i])) continue;
                _currentState?.OnHandleEvent(this, batch[i]);
            }
        }

        private static int CompareFacts(IEvent left, IEvent right)
        {
            int compare = GetFactRank(left).CompareTo(GetFactRank(right));
            if (compare != 0) return compare;
            compare = left.Timestamp.CompareTo(right.Timestamp);
            if (compare != 0) return compare;
            return string.Compare(left.Identity, right.Identity, StringComparison.Ordinal);
        }

        private static int GetFactRank(IEvent fact)
        {
            if (fact is ChaseReached) return 0;
            if (fact is LOSBreak hideBreak && hideBreak.IsHideEntry)
                return hideBreak.APreBreak >= hideBreak.ThresholdApplied ? 1 : 3;
            if (fact is CapReached) return 2;
            // Occupancy follows the opener so a same-boundary hold receives the
            // authoritative zone state after its episode is selected.
            if (fact is HideSpotOccupancy) return 4;
            if (fact is ConfirmWindowElapsed) return 5;
            if (fact is NoiseHeardRelay) return 6;
            if (fact is LOSGain || fact is LOSBreak) return 7;
            return 7;
        }

        private bool IsCurrentEnvelope(IEvent evt)
        {
            return evt != null
                && string.Equals(evt.SessionId, sessionId, StringComparison.Ordinal)
                && evt.AttemptEpoch == attemptEpoch;
        }

        /// <summary>
        /// Reserves one relay identity for this guard. Duplicate delivery is a
        /// no-op; the caller must publish the resulting consumption outcome.
        /// </summary>
        public bool TryBeginRelay(NoiseHeardRelay relay)
        {
            if (relay == null || !IsCurrentEnvelope(relay)
                || (!string.IsNullOrEmpty(relay.GuardEid)
                    && !string.Equals(relay.GuardEid, GuardEid, StringComparison.Ordinal)))
                return false;
            // Legacy fixture bindings may not supply a clock; without an
            // authoritative time source a cooldown cannot be evaluated and must
            // not permanently suppress fresh Patrol relays.
            if (_clock != null && _currentState == _patrolState
                && CurrentVirtualTime < _noiseRecommitCooldownUntil)
                return false;

            string relayKey = relay.SessionId + "|" + relay.AttemptEpoch + "|"
                + relay.GuardEid + "|" + relay.FactId.ToString("D");
            return _consumedRelays.Add(relayKey);
        }

        public void PublishRelayOutcome(NoiseHeardRelay relay, RelayConsumption consumption)
        {
            if (relay == null || _eventBus == null) return;
            var outcome = new NoiseConsumptionOutcome(sessionId, attemptEpoch,
                relay.FactId, GuardEid, relay.EntryId, consumption,
                _clock == null ? relay.Timestamp : _clock.CurrentTime);
            _eventBus.Publish(outcome);
        }

        /// <summary>
        /// Assigns current session metadata and a deterministic decision identity
        /// before publishing a decision record.
        /// </summary>
        public void PublishDecision(DecisionRecord record)
        {
            if (record == null || _eventBus == null) return;
            record.SessionId = sessionId;
            record.AttemptEpoch = attemptEpoch;
            if (record.Timestamp <= 0f && _clock != null)
                record.Timestamp = _clock.CurrentTime;
            record.Identity = "decision:" + (++_decisionSequence).ToString("D8");
            _eventBus.Publish(record);
        }

        public void TransitionTo(IGuardState newState)
        {
            if (newState == null || _currentState == newState) return;

            _isTransitioning = true;
            if (_currentState != null) _currentState.OnExit(this);
            _currentState = newState;
            _currentState.OnEnter(this);
            _isTransitioning = false;
            Debug.Log($"[GuardFSM] Transitioned to {newState.StateName}");
        }

        public PatrolState GetPatrolState() { return _patrolState; }
        public InvestigateState GetInvestigateState() { return _investigateState; }
        public ChaseState GetChaseState() { return _chaseState; }
        public IGuardState CurrentState { get { return _currentState; } }
        public string GuardEid { get { return guardEid; } }
        public string SessionId { get { return sessionId; } }
        public long AttemptEpoch { get { return attemptEpoch; } }
        /// <summary>Current injected virtual time for authoritative liveness records.</summary>
        public float CurrentVirtualTime
        {
            get { return _clock == null ? 0f : _clock.CurrentTime; }
        }
        public NoiseResponseProfile NoiseResponseProfile { get { return noiseResponseProfile; } }

        /// <summary>Starts the post-resolution cooldown for fresh noise relays.</summary>
        public void ArmNoiseRecommitCooldown()
        {
            // A legacy unbound FSM has no clock against which a cooldown can be
            // measured. Production always injects the session clock.
            if (_clock == null)
            {
                _noiseRecommitCooldownUntil = 0f;
                return;
            }
            _noiseRecommitCooldownUntil = CurrentVirtualTime
                + _noiseRecommitCooldownSeconds;
        }

        /// <summary>
        /// Publishes the immutable FSM-owned liveness transition consumed by the
        /// next Perception boundary. Perception owns the opaque entry identity;
        /// the FSM carries that exact string without allocating a second mapping.
        /// </summary>
        public EventPublishResult PublishLivenessFact(string operation, string tier,
            string cause, float sourceTimestamp, string entryId)
        {
            if (_eventBus == null || string.IsNullOrWhiteSpace(entryId)
                || string.IsNullOrWhiteSpace(operation))
            {
                _lastLivenessFailureCode = "fsm-liveness-invalid-input";
                return EventPublishResult.Rejected(_lastLivenessFailureCode);
            }

            float evaluatedAt = _clock == null ? sourceTimestamp : _clock.CurrentTime;
            LivenessFact fact;
            try
            {
                fact = new LivenessFact(sessionId, attemptEpoch, operation, tier,
                    cause, sourceTimestamp, evaluatedAt, GuardEid, entryId);
            }
            catch (ArgumentException)
            {
                _lastLivenessFailureCode = "fsm-liveness-invalid-fact";
                return EventPublishResult.Rejected(_lastLivenessFailureCode);
            }

            // A retrying predecessor must be acknowledged before a later
            // liveness transition can enter the bus. This preserves write-side
            // ordering just as the Event Bus preserves raw source ordering.
            FlushPendingLiveness();
            if (_pendingLiveness.Count > 0)
            {
                if (_pendingLiveness.Count >= 32)
                {
                    _lastLivenessFailureCode =
                        "fsm-liveness-queue-overflow-rejected";
                    return EventPublishResult.Rejected(_lastLivenessFailureCode);
                }
                _pendingLiveness.Add(new PendingLiveness { Fact = fact });
                return EventPublishResult.Retry(
                    "fsm-liveness-predecessor-pending", 0);
            }

            EventPublishResult result = _eventBus.Publish(fact);
            if (result.Admission == EventAdmission.Retry)
            {
                _pendingLiveness.Add(new PendingLiveness
                {
                    Fact = fact,
                    RetryCount = result.RetryCount
                });
            }
            else if (result.Admission == EventAdmission.Rejected)
            {
                _lastLivenessFailureCode = string.IsNullOrWhiteSpace(result.Code)
                    ? "fsm-liveness-publication-rejected" : result.Code;
            }
            return result;
        }

        private void FlushPendingLiveness()
        {
            for (int i = 0; i < _pendingLiveness.Count;)
            {
                PendingLiveness pending = _pendingLiveness[i];
                EventPublishResult result = _eventBus.Publish(pending.Fact);
                if (result.Admission == EventAdmission.Retry)
                {
                    pending.RetryCount = Math.Max(pending.RetryCount + 1,
                        result.RetryCount);
                    if (pending.RetryCount > _maxLivenessRetries)
                    {
                        _lastLivenessFailureCode =
                            "fsm-liveness-publication-retry-exhausted";
                        _pendingLiveness.RemoveAt(i);
                    }
                    else
                    {
                        break;
                    }
                    continue;
                }

                if (result.Admission == EventAdmission.Rejected)
                    _lastLivenessFailureCode = string.IsNullOrWhiteSpace(result.Code)
                        ? "fsm-liveness-publication-rejected" : result.Code;
                _pendingLiveness.RemoveAt(i);
            }
        }

        private string GetLivenessTier()
        {
            return _currentState == _chaseState ? "Chase" : "Investigate";
        }

        /// <summary>Indicates that a lifecycle reset suppresses state exit facts.</summary>
        public bool IsBoundaryResetting { get { return _suppressStateLiveness; } }

        public void SetGoalMode(GuardGoalMode mode)
        {
            currentGoalMode = mode;
        }
    }
}
