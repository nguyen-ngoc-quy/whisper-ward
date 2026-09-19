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
        /// <summary>Public Player Noise contract member.</summary>
        public GuardGoalMode currentGoalMode = GuardGoalMode.None;
        /// <summary>Public Player Noise contract member.</summary>
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
            /// <summary>Public Player Noise contract member.</summary>
            public LivenessFact Fact;
            /// <summary>Public Player Noise contract member.</summary>
            public int RetryCount;
        }

        private sealed class PendingSuppressionReceipt
        {
            /// <summary>Public Player Noise contract member.</summary>
            public NoiseSuppressedReceipt Receipt;
            /// <summary>Public Player Noise contract member.</summary>
            public int RetryCount;
        }

        private sealed class PendingRelayOutcome
        {
            /// <summary>Public Player Noise contract member.</summary>
            public NoiseHeardRelay Relay;
            /// <summary>Public Player Noise contract member.</summary>
            public RelayConsumption Consumption;
            /// <summary>Public Player Noise contract member.</summary>
            public float Timestamp;
            /// <summary>Public Player Noise contract member.</summary>
            public int RetryCount;
        }

        private readonly List<ISensingFact> _pendingFacts = new List<ISensingFact>();
        private readonly List<PendingLiveness> _pendingLiveness =
            new List<PendingLiveness>();
        private readonly HashSet<string> _consumedRelays = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _relayReservations =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _relayOutcomes =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<PendingRelayOutcome> _pendingRelayOutcomes =
            new List<PendingRelayOutcome>();
        private long _decisionSequence;
        private int _maxLivenessRetries = 3;
        private float _fsmIntervalSeconds;
        private float _fsmCadenceAccumulator;
        private float _noiseRecommitCooldownSeconds =
            NoiseRuntimeConfiguration.RegisteredNoiseRecommitCooldownSeconds;
        private float _noiseRecommitCooldownUntil;
        private string _lastLivenessFailureCode = string.Empty;
        private string _lastRelayOutcomeFailureCode = string.Empty;
        private bool _suppressStateLiveness;
        private bool _livenessPromotionPending;
        private string _livenessPromotionEntryId = string.Empty;
        private ISuppressionVisibilityPolicy _suppressionVisibilityPolicy;
        private Func<Vector3, bool> _noiseEpisodeAreaTest;
        private readonly HashSet<string> _suppressionReceipts =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<PendingSuppressionReceipt> _pendingSuppressionReceipts =
            new List<PendingSuppressionReceipt>();
        private string _lastSuppressionReceiptFailureCode = string.Empty;
        private int _maxSuppressionReceiptRetries = 3;
        private float _microTellCooldownUntil;
        private float _microTellCooldownSeconds = 0.5f;

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
                _phaseCoordinator.RegisterFsmParticipant("guard-fsm:" + guardEid, 10, ProcessTick);
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
                _phaseCoordinator.RegisterFsmParticipant("guard-fsm:" + guardEid, 10, ProcessTick);
            else if (wasBound)
                BindRuntime(_eventBus, _clock);
        }

        /// <summary>Public API member for the Player Noise contract.</summary>
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

        /// <summary>Injects the one-query visibility policy for suppression receipts.</summary>
        public void ConfigureSuppressionVisibilityPolicy(
            ISuppressionVisibilityPolicy policy)
        {
            _suppressionVisibilityPolicy = policy;
        }

        /// <summary>
        /// Injects the registered live-episode area predicate used to classify
        /// suppressed relays. The predicate receives the relay's authoritative
        /// raw origin; a missing predicate cannot claim an out-of-area result.
        /// </summary>
        public void ConfigureNoiseEpisodeAreaTest(Func<Vector3, bool> areaTest)
        {
            _noiseEpisodeAreaTest = areaTest;
        }

        /// <summary>Configures deterministic spacing between visible micro-tells.</summary>
        public void ConfigureMicroTellCooldown(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            _microTellCooldownSeconds = seconds;
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

        /// <summary>Stable terminal code for relay-outcome publication.</summary>
        public string LastRelayOutcomeFailureCode
        {
            get { return _lastRelayOutcomeFailureCode; }
        }

        /// <summary>
        /// Stable terminal code for the latest suppressed-receipt publication
        /// failure. An empty value means every receipt was accepted, duplicated,
        /// or is still legitimately staged for retry.
        /// </summary>
        public string LastSuppressionReceiptFailureCode
        {
            get { return _lastSuppressionReceiptFailureCode; }
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
            Vector3 stalePosition = GetLastAuthoritativeEpisodePosition();
            float staleTimestamp = _clock == null ? 0f : _clock.CurrentTime;
            // SessionBoundaryService invokes this callback before advancing the bus.
            // The stale close is admitted in the old generation exactly once; the
            // bus retains that handoff across the barrier instead of relabelling it.
            if (!string.IsNullOrWhiteSpace(staleEntryId)
                && _eventBus != null)
            {
                PublishLivenessFact("close", GetLivenessTier(), "stale",
                    staleTimestamp, staleEntryId, stalePosition,
                    LivenessPositionSources.LastAuthoritativeEpisodePosition);
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
            // A stale close staged for retry is part of the previous generation's
            // contract. It is never silently dropped: retained stale-close
            // handoffs keep their original envelope, and any other pending
            // liveness is closed with an explicit fail-closed code.
            bool retainedStaleClose = false;
            for (int i = 0; i < _pendingLiveness.Count; i++)
            {
                if (_pendingLiveness[i].Fact is LivenessFact fact
                    && fact.RetainAcrossGenerationBarrier)
                {
                    retainedStaleClose = true;
                    continue;
                }
                _lastLivenessFailureCode =
                    "fsm-liveness-boundary-discarded";
                break;
            }
            if (!retainedStaleClose)
                _pendingLiveness.Clear();
            else
            {
                for (int i = _pendingLiveness.Count - 1; i >= 0; i--)
                {
                    LivenessFact fact = _pendingLiveness[i].Fact as LivenessFact;
                    if (fact == null || !fact.RetainAcrossGenerationBarrier)
                        _pendingLiveness.RemoveAt(i);
                }
            }
            if (_pendingSuppressionReceipts.Count > 0)
            {
                _lastSuppressionReceiptFailureCode =
                    "fsm-suppression-receipt-boundary-discarded";
                _pendingSuppressionReceipts.Clear();
            }
            _pendingFacts.Clear();
            _consumedRelays.Clear();
            _relayReservations.Clear();
            _relayOutcomes.Clear();
            _pendingRelayOutcomes.Clear();
            _decisionSequence = 0;
            _fsmCadenceAccumulator = 0f;
            _noiseRecommitCooldownUntil = 0f;
            _livenessPromotionPending = false;
            _livenessPromotionEntryId = string.Empty;
            _suppressionReceipts.Clear();
            _microTellCooldownUntil = 0f;
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
            FlushPendingRelayOutcomes();
            FlushPendingLiveness();
            FlushPendingSuppressionReceipts();
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

            NoiseHeardRelay leftRelay = left as NoiseHeardRelay;
            NoiseHeardRelay rightRelay = right as NoiseHeardRelay;
            if (leftRelay != null && rightRelay != null)
            {
                compare = leftRelay.TPublish.CompareTo(rightRelay.TPublish);
                if (compare != 0) return compare;
                compare = leftRelay.SourceEventClassRank.CompareTo(
                    rightRelay.SourceEventClassRank);
                if (compare != 0) return compare;
                compare = NoiseSourceOrdering.CompareSourceEventIds(
                    leftRelay.SourceEventId, rightRelay.SourceEventId);
                if (compare != 0) return compare;
                compare = leftRelay.FactId.CompareTo(rightRelay.FactId);
                if (compare != 0) return compare;
            }
            else
            {
                compare = left.Timestamp.CompareTo(right.Timestamp);
                if (compare != 0) return compare;
            }

            return string.Compare(left.Identity, right.Identity,
                StringComparison.Ordinal);
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
            if (_consumedRelays.Contains(relayKey)
                || _relayReservations.Contains(relayKey)
                || _relayOutcomes.Contains(relayKey))
                return false;
            return _relayReservations.Add(relayKey);
        }

        /// <summary>Public API member for the Player Noise contract.</summary>
        public EventPublishResult PublishRelayOutcome(NoiseHeardRelay relay,
            RelayConsumption consumption)
        {
            if (relay == null || _eventBus == null
                || !IsCurrentEnvelope(relay)
                || !string.Equals(relay.GuardEid, GuardEid, StringComparison.Ordinal))
                return EventPublishResult.Rejected("fsm-relay-outcome-invalid");

            string outcomeKey = sessionId + "|" + attemptEpoch + "|"
                + GuardEid + "|" + relay.FactId.ToString("D");
            if (_relayOutcomes.Contains(outcomeKey))
                return EventPublishResult.Duplicate();

            float timestamp = _clock == null ? relay.Timestamp : _clock.CurrentTime;
            PendingRelayOutcome pending = new PendingRelayOutcome
            {
                Relay = relay,
                Consumption = consumption,
                Timestamp = timestamp
            };
            EventPublishResult result = TryPublishRelayOutcome(pending);
            if (result.Admission == EventAdmission.Retry)
            {
                _pendingRelayOutcomes.Add(pending);
            }
            return result;
        }

        private EventPublishResult TryPublishRelayOutcome(PendingRelayOutcome pending)
        {
            var outcome = new NoiseConsumptionOutcome(sessionId, attemptEpoch,
                pending.Relay.FactId, GuardEid, pending.Relay.EntryId,
                pending.Consumption, pending.Timestamp);
            EventPublishResult result = _eventBus.Publish(outcome);
            if (result.Admission == EventAdmission.Retry)
            {
                pending.RetryCount = Math.Max(pending.RetryCount + 1,
                    result.RetryCount);
                return result;
            }
            string outcomeKey = sessionId + "|" + attemptEpoch + "|"
                + GuardEid + "|" + pending.Relay.FactId.ToString("D");
            _relayReservations.Remove(outcomeKey);
            _relayOutcomes.Add(outcomeKey);
            _consumedRelays.Add(outcomeKey);
            if (result.Admission == EventAdmission.Rejected)
            {
                _lastRelayOutcomeFailureCode = string.IsNullOrWhiteSpace(result.Code)
                    ? "fsm-relay-outcome-rejected" : result.Code;
                return result;
            }
            if (pending.Consumption == RelayConsumption.Ignored)
                PublishSuppressionForAcceptedOutcome(pending);
            return result;
        }

        private void PublishSuppressionForAcceptedOutcome(PendingRelayOutcome pending)
        {
            NoiseHeardRelay relay = pending.Relay;
            string reason = GetSuppressionReason(relay);
            bool eligible = IsVisibleSuppressionEligible();
            bool visible = false;
            bool microTell = false;
            string visibilityQueryId = string.Empty;
            string visibilityQueryProvenance = string.Empty;
            if (!eligible)
            {
                visibilityQueryId = "not-applicable";
                visibilityQueryProvenance = "not-performed:state-exclusion";
            }
            else if (_suppressionVisibilityPolicy != null)
            {
                string queryId = "visibility:" + sessionId + ":"
                    + attemptEpoch + ":" + GuardEid + ":"
                    + relay.FactId.ToString("D");
                var query = new SuppressionVisibilityQuery(queryId,
                    relay.AuthoritativeOrigin, "noise-relay-origin");
                var typedPolicy = _suppressionVisibilityPolicy
                    as ITypedSuppressionVisibilityPolicy;
                SuppressionVisibilityResult visibility = typedPolicy != null
                    ? typedPolicy.Evaluate(query)
                    : new SuppressionVisibilityResult(
                        _suppressionVisibilityPolicy.IsVisibleToPlayer(relay.Position),
                        queryId, "legacy-adapter");
                visible = visibility.Visible;
                visibilityQueryId = visibility.QueryId;
                visibilityQueryProvenance = visibility.QueryProvenance;
                if (visible && pending.Timestamp >= _microTellCooldownUntil)
                {
                    microTell = true;
                    _microTellCooldownUntil = pending.Timestamp
                        + _microTellCooldownSeconds;
                }
            }
            else
            {
                // An eligible suppression decision without an injected visibility
                // policy is an unconfigured consumer, not a silent no-query pass.
                // The receipt records the missing policy explicitly instead of
                // shipping empty provenance; no micro-tell is emitted.
                visibilityQueryId = "not-applicable";
                visibilityQueryProvenance = "not-performed:policy-unavailable";
            }
            PublishSuppressedReceipt(relay, reason, visible, microTell,
                pending.Timestamp, visibilityQueryId, visibilityQueryProvenance);
        }

        private void FlushPendingRelayOutcomes()
        {
            for (int i = 0; i < _pendingRelayOutcomes.Count;)
            {
                PendingRelayOutcome pending = _pendingRelayOutcomes[i];
                EventPublishResult result = TryPublishRelayOutcome(pending);
                if (result.Admission == EventAdmission.Retry)
                {
                    if (pending.RetryCount > _maxLivenessRetries)
                    {
                        _lastRelayOutcomeFailureCode =
                            "fsm-relay-outcome-retry-exhausted";
                        string key = sessionId + "|" + attemptEpoch + "|"
                            + GuardEid + "|"
                            + pending.Relay.FactId.ToString("D");
                        _relayReservations.Remove(key);
                        _relayOutcomes.Add(key);
                        _consumedRelays.Add(key);
                        _pendingRelayOutcomes.RemoveAt(i);
                    }
                    else break;
                    continue;
                }
                _pendingRelayOutcomes.RemoveAt(i);
            }
        }

        private string GetSuppressionReason(NoiseHeardRelay relay)
        {
            // State exclusion has precedence over all timing and geometry
            // classifications. It deliberately produces no visibility query.
            if (_currentState == _chaseState
                || currentGoalMode == GuardGoalMode.HideSpotFront)
                return "state-exclusion";
            if (_clock != null && CurrentVirtualTime < _noiseRecommitCooldownUntil)
                return "cooldown";
            if (_noiseEpisodeAreaTest != null
                && relay != null
                && !_noiseEpisodeAreaTest(relay.AuthoritativeOrigin))
                return "out-of-area";
            return "out-of-window";
        }

        private bool IsVisibleSuppressionEligible()
        {
            // Chase, HideSpotFront, stale, epoch-invalid, and duplicate relays
            // never enter the player-facing visibility query.
            return _currentState != _chaseState
                && currentGoalMode != GuardGoalMode.HideSpotFront;
        }

        /// <summary>
        /// Publishes one immutable presentation receipt for suppressed noise.
        /// Exactly-once per (session, epoch, guard, fact): a bus Retry retains
        /// the receipt for a later flush instead of silently dropping it, and a
        /// terminal Rejection is recorded as a stable failure code.
        /// </summary>
        public void PublishSuppressedReceipt(NoiseHeardRelay relay, string reason,
            bool visibleToPlayer, bool microTellEmitted, float timestamp)
        {
            PublishSuppressedReceipt(relay, reason, visibleToPlayer,
                microTellEmitted, timestamp, string.Empty, string.Empty);
        }

        /// <summary>Public API member for the Player Noise contract.</summary>
        public void PublishSuppressedReceipt(NoiseHeardRelay relay, string reason,
            bool visibleToPlayer, bool microTellEmitted, float timestamp,
            string visibilityQueryId, string visibilityQueryProvenance)
        {
            if (relay == null || _eventBus == null) return;
            string receiptKey = sessionId + "|" + attemptEpoch + "|"
                + GuardEid + "|" + relay.FactId.ToString("D");
            if (!_suppressionReceipts.Add(receiptKey)) return;
            var receipt = new NoiseSuppressedReceipt(sessionId, attemptEpoch,
                relay.FactId, GuardEid, relay.EntryId, reason,
                visibleToPlayer, microTellEmitted, timestamp,
                visibilityQueryId, visibilityQueryProvenance,
                relay.Kind, relay.SourceEventId, relay.TPublish);

            // A retrying predecessor stays staged ahead of this receipt, matching
            // the write-side ordering discipline of pending liveness facts.
            FlushPendingSuppressionReceipts();
            if (_pendingSuppressionReceipts.Count > 0)
            {
                if (_pendingSuppressionReceipts.Count >= 32)
                {
                    _lastSuppressionReceiptFailureCode =
                        "fsm-suppression-receipt-queue-overflow-rejected";
                    return;
                }
                _pendingSuppressionReceipts.Add(
                    new PendingSuppressionReceipt { Receipt = receipt });
                return;
            }

            EventPublishResult result = _eventBus.Publish(receipt);
            if (result.Admission == EventAdmission.Retry)
            {
                _pendingSuppressionReceipts.Add(
                    new PendingSuppressionReceipt
                    {
                        Receipt = receipt,
                        RetryCount = result.RetryCount
                    });
            }
            else if (result.Admission == EventAdmission.Rejected)
            {
                _lastSuppressionReceiptFailureCode =
                    string.IsNullOrWhiteSpace(result.Code)
                        ? "fsm-suppression-receipt-rejected"
                        : result.Code;
            }
        }

        private void FlushPendingSuppressionReceipts()
        {
            for (int i = 0; i < _pendingSuppressionReceipts.Count;)
            {
                PendingSuppressionReceipt pending = _pendingSuppressionReceipts[i];
                EventPublishResult result = _eventBus.Publish(pending.Receipt);
                if (result.Admission == EventAdmission.Retry)
                {
                    pending.RetryCount = Math.Max(pending.RetryCount + 1,
                        result.RetryCount);
                    if (pending.RetryCount > _maxSuppressionReceiptRetries)
                    {
                        _lastSuppressionReceiptFailureCode =
                            "fsm-suppression-receipt-retry-exhausted";
                        _pendingSuppressionReceipts.RemoveAt(i);
                    }
                    else
                    {
                        // Ordering: a still-blocked head blocks the queue; later
                        // receipts must not overtake it.
                        break;
                    }
                    continue;
                }

                if (result.Admission == EventAdmission.Rejected)
                    _lastSuppressionReceiptFailureCode =
                        string.IsNullOrWhiteSpace(result.Code)
                            ? "fsm-suppression-receipt-rejected"
                            : result.Code;
                _pendingSuppressionReceipts.RemoveAt(i);
            }
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

        /// <summary>Public API member for the Player Noise contract.</summary>
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

        /// <summary>Public API member for the Player Noise contract.</summary>
        public PatrolState GetPatrolState() { return _patrolState; }
        /// <summary>Public API member for the Player Noise contract.</summary>
        public InvestigateState GetInvestigateState() { return _investigateState; }
        /// <summary>Public API member for the Player Noise contract.</summary>
        public ChaseState GetChaseState() { return _chaseState; }
        /// <summary>Public Player Noise contract member.</summary>
        public IGuardState CurrentState { get { return _currentState; } }
        /// <summary>Public Player Noise contract member.</summary>
        public string GuardEid { get { return guardEid; } }
        /// <summary>Public Player Noise contract member.</summary>
        public string SessionId { get { return sessionId; } }
        /// <summary>Public Player Noise contract member.</summary>
        public long AttemptEpoch { get { return attemptEpoch; } }
        /// <summary>Current injected virtual time for authoritative liveness records.</summary>
        public float CurrentVirtualTime
        {
            get { return _clock == null ? 0f : _clock.CurrentTime; }
        }
        /// <summary>Public Player Noise contract member.</summary>
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
            return PublishLivenessFact(operation, tier, cause, sourceTimestamp,
                entryId, transform.position,
                LivenessPositionSources.GuardTransform);
        }

        /// <summary>Publishes liveness with the authoritative position datum.</summary>
        public EventPublishResult PublishLivenessFact(string operation, string tier,
            string cause, float sourceTimestamp, string entryId, Vector3 position,
            string positionSource)
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
                    cause, sourceTimestamp, evaluatedAt, GuardEid, entryId,
                    position, positionSource);
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

        private Vector3 GetLastAuthoritativeEpisodePosition()
        {
            if (_currentState == _investigateState)
                return _investigateState.AuthoritativeEpisodePosition;
            if (_currentState == _chaseState)
                return _chaseState.AuthoritativeEpisodePosition;
            return transform.position;
        }

        /// <summary>Indicates that a lifecycle reset suppresses state exit facts.</summary>
        public bool IsBoundaryResetting { get { return _suppressStateLiveness; } }

        /// <summary>Arms an in-place Investigate-to-Chase liveness promotion.</summary>
        public void PrepareLivenessPromotion(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                throw new ArgumentException("entryId");
            _livenessPromotionPending = true;
            _livenessPromotionEntryId = entryId;
            currentEntryId = entryId;
        }

        /// <summary>Public API member for the Player Noise contract.</summary>
        public bool IsLivenessPromotionPending(string entryId)
        {
            return _livenessPromotionPending
                && string.Equals(_livenessPromotionEntryId, entryId,
                    StringComparison.Ordinal);
        }

        /// <summary>Public API member for the Player Noise contract.</summary>
        public void ConsumeLivenessPromotion()
        {
            _livenessPromotionPending = false;
            _livenessPromotionEntryId = string.Empty;
        }

        /// <summary>Public API member for the Player Noise contract.</summary>
        public void SetGoalMode(GuardGoalMode mode)
        {
            currentGoalMode = mode;
        }
    }
}
