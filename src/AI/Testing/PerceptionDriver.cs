using UnityEngine;
using System.Collections.Generic;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Provisional Perception fixture. It injects immutable relays at explicit
    /// virtual boundaries; it is not the production hearing implementation.
    /// </summary>
    public class PerceptionDriver : MonoBehaviour
    {
        [Header("Test Fixture")]
        public List<ScriptedEvent> eventScript = new List<ScriptedEvent>();

        private int _currentEventIndex;
        private long _lastProcessedTick = -1;
        private IEventBus _eventBus;
        private IVirtualTickClock _clock;
        private SessionPhaseCoordinator _phaseCoordinator;
        private SubscriptionToken _clockToken;
        private bool _isBound;
        private bool _phaseDriven;
        private bool _drainPhases = true;

        public void Configure(IEventBus eventBus, IVirtualTickClock clock)
        {
            eventBus = eventBus ?? throw new System.ArgumentNullException(nameof(eventBus));
            clock = clock ?? throw new System.ArgumentNullException(nameof(clock));
            bool wasBound = _isBound;
            if (wasBound) UnbindRuntime();
            if (_phaseCoordinator != null)
                _phaseCoordinator.UnregisterIngressSource(OnTick);
            _eventBus = eventBus;
            _clock = clock;
            _phaseCoordinator = null;
            _phaseDriven = false;
            _drainPhases = true;
            if (wasBound) BindRuntime();
        }

        /// <summary>
        /// Registers this scripted source with the session-owned phase graph. It
        /// publishes at the source phase and leaves all drains to the coordinator.
        /// </summary>
        public void ConfigureWithPhaseCoordinator(IEventBus eventBus,
            IVirtualTickClock clock, SessionPhaseCoordinator phaseCoordinator)
        {
            eventBus = eventBus ?? throw new System.ArgumentNullException(nameof(eventBus));
            clock = clock ?? throw new System.ArgumentNullException(nameof(clock));
            phaseCoordinator = phaseCoordinator
                ?? throw new System.ArgumentNullException(nameof(phaseCoordinator));
            bool wasBound = _isBound;
            if (wasBound) UnbindRuntime();
            if (_phaseCoordinator != null)
                _phaseCoordinator.UnregisterIngressSource(OnTick);
            _eventBus = eventBus;
            _clock = clock;
            _phaseCoordinator = phaseCoordinator;
            _phaseDriven = true;
            _drainPhases = false;
            _phaseCoordinator.RegisterIngressSource(OnTick);
        }

        private void OnEnable()
        {
            if (_phaseDriven && _phaseCoordinator != null)
            {
                _phaseCoordinator.RegisterIngressSource(OnTick);
                return;
            }
            BindRuntime();
        }

        private void Start()
        {
            if (_clock == null)
                _clock = GetComponent<VirtualTickClock>();
            if (_eventBus == null)
                _eventBus = EventBus.Default;
            if (!_phaseDriven) BindRuntime();
        }

        private void OnDisable()
        {
            UnbindRuntime();
            if (_phaseDriven && _phaseCoordinator != null)
                _phaseCoordinator.UnregisterIngressSource(OnTick);
        }

        private void OnDestroy()
        {
            UnbindRuntime();
            if (_phaseDriven && _phaseCoordinator != null)
                _phaseCoordinator.UnregisterIngressSource(OnTick);
        }

        private void BindRuntime()
        {
            if (_isBound || _clock == null) return;
            _clockToken = _clock.Subscribe(OnTick);
            _isBound = true;
        }

        private void UnbindRuntime()
        {
            if (!_isBound) return;
            if (_clock != null)
                _clock.Unsubscribe(_clockToken);
            _isBound = false;
        }

        /// <summary>
        /// Public test seam for advancing a scripted virtual boundary directly.
        /// </summary>
        public void OnTick(long tick, float delta)
        {
            if (_eventBus == null)
                _eventBus = EventBus.Default;
            while (_currentEventIndex < eventScript.Count)
            {
                var scripted = eventScript[_currentEventIndex];
                if (scripted.Tick > tick) break;
                PublishEvent(scripted, _currentEventIndex);
                _currentEventIndex++;
            }
            _lastProcessedTick = tick;
            // Standalone fixture mode keeps a direct boundary seam for legacy tests;
            // coordinator mode leaves all drains to SessionPhaseCoordinator.
            if (_drainPhases)
            {
                _eventBus?.Drain(EventBusPhase.Hearing);
                _eventBus?.Drain(EventBusPhase.FsmDecision);
            }
        }

        /// <summary>
        /// Clears fixture progress at a lifecycle-owned session or epoch boundary.
        /// </summary>
        public void ResetForBoundary()
        {
            _currentEventIndex = 0;
            _lastProcessedTick = -1;
        }

        private float TimestampFor(ScriptedEvent scripted)
        {
            if (_clock != null) return _clock.CurrentTime;
            return scripted.Tick * 0.5f;
        }

        private string SessionFor(ScriptedEvent scripted)
        {
            return string.IsNullOrWhiteSpace(scripted.SessionId)
                ? (_eventBus == null ? "default" : _eventBus.SessionId)
                : scripted.SessionId;
        }

        private long EpochFor(ScriptedEvent scripted)
        {
            return scripted.AttemptEpoch >= 0
                ? scripted.AttemptEpoch
                : (_eventBus == null ? 0 : _eventBus.AttemptEpoch);
        }

        private string IdentityFor(ScriptedEvent scripted, int index)
        {
            return string.IsNullOrWhiteSpace(scripted.SourceEventId)
                ? "scripted:" + scripted.Type + ":" + scripted.Tick + ":" + index
                : scripted.SourceEventId;
        }

        private void Stamp(SensingFact fact, ScriptedEvent scripted, int index)
        {
            fact.Timestamp = TimestampFor(scripted);
            fact.SetEnvelope(SessionFor(scripted), EpochFor(scripted),
                IdentityFor(scripted, index));
        }

        private void PublishEvent(ScriptedEvent scripted, int index)
        {
            string session = SessionFor(scripted);
            long epoch = EpochFor(scripted);
            float timestamp = TimestampFor(scripted);
            string identity = IdentityFor(scripted, index);

            switch (scripted.Type)
            {
                case EventType.ConfirmWindowElapsed:
                    var confirm = new ConfirmWindowElapsed
                    {
                        EntryId = scripted.EntryId,
                        ResidualR = scripted.ResidualR,
                        ThresholdState = scripted.ThresholdState
                    };
                    Stamp(confirm, scripted, index);
                    _eventBus?.Publish(confirm);
                    break;

                case EventType.NoiseHeard:
                    ulong factId = scripted.FactId == 0
                        ? (ulong)(index + 1)
                        : scripted.FactId;
                    var relay = new NoiseHeardRelay(session, epoch, factId,
                        string.IsNullOrWhiteSpace(scripted.EntryId)
                            ? "scripted-entry:" + index : scripted.EntryId,
                        string.IsNullOrWhiteSpace(scripted.GuardEid)
                            ? "guard-01" : scripted.GuardEid,
                        string.IsNullOrWhiteSpace(scripted.Kind) ? "movement" : scripted.Kind,
                        "scripted", scripted.Position, timestamp, timestamp,
                        scripted.ResidualR);
                    _eventBus?.Publish(relay);
                    break;

                case EventType.ChaseReached:
                    var chase = new ChaseReached
                    {
                        EntryId = scripted.EntryId,
                        Position = scripted.Position,
                        ResidualR = scripted.ResidualR,
                        ThresholdState = scripted.ThresholdState
                    };
                    Stamp(chase, scripted, index);
                    _eventBus?.Publish(chase);
                    break;

                case EventType.Reachability:
                    var reach = new Reachability
                    {
                        EntryId = scripted.EntryId,
                        GuardEid = scripted.GuardEid,
                        Position = scripted.Position,
                        IsReachable = scripted.IsReachable,
                        PathArrivalM = scripted.PathArrivalM,
                        DeltaYM = scripted.DeltaYM
                    };
                    Stamp(reach, scripted, index);
                    _eventBus?.Publish(reach);
                    break;

                case EventType.CapReached:
                    var cap = new CapReached
                    {
                        EntryId = scripted.EntryId,
                        Counter = scripted.Counter,
                        Position = scripted.Position
                    };
                    Stamp(cap, scripted, index);
                    _eventBus?.Publish(cap);
                    break;

                case EventType.LOSGain:
                    var losGain = new LOSGain
                    {
                        Position = scripted.Position,
                        Distance = scripted.Distance,
                        Guard = gameObject,
                        GuardEid = scripted.GuardEid
                    };
                    Stamp(losGain, scripted, index);
                    _eventBus?.Publish(losGain);
                    break;

                case EventType.LOSBreak:
                    var losBreak = new LOSBreak
                    {
                        ResidualR = scripted.ResidualR,
                        BreakType = scripted.BreakType,
                        CausalClass = scripted.CausalClass,
                        GuardYawDelta = scripted.GuardYawDelta,
                        GuardEid = scripted.GuardEid,
                        IsHideEntry = scripted.IsHideEntry,
                        APreBreak = scripted.APreBreak,
                        ThresholdApplied = scripted.ThresholdApplied,
                        SpotPosition = scripted.SpotPosition,
                        SpotFrontPosition = scripted.SpotFrontPosition,
                        HasSpotFrontPosition = scripted.HasSpotFrontPosition,
                        HideSpotId = scripted.HideSpotId,
                        EntryId = scripted.EntryId
                    };
                    Stamp(losBreak, scripted, index);
                    _eventBus?.Publish(losBreak);
                    break;

                case EventType.HideSpotOccupancy:
                    var occupancy = new HideSpotOccupancy
                    {
                        HideSpotId = scripted.HideSpotId,
                        EntryId = scripted.EntryId,
                        InteriorPosition = scripted.SpotPosition,
                        IsOccupied = scripted.IsOccupied
                    };
                    Stamp(occupancy, scripted, index);
                    _eventBus?.Publish(occupancy);
                    break;
            }
        }
    }

    [System.Serializable]
    public class ScriptedEvent
    {
        public EventType Type;
        public long Tick;
        public string SessionId;
        public long AttemptEpoch = -1;
        public string SourceEventId;
        public ulong FactId;
        public string GuardEid;
        public string EntryId;
        public Vector3 Position;
        public float ResidualR;
        public string ThresholdState;
        public string Kind;
        public bool IsFirstConsumption;
        public bool IsReachable;
        public float PathArrivalM;
        public float DeltaYM;
        public int Counter;
        public float Distance;
        public string BreakType;
        public string CausalClass;
        public float GuardYawDelta;
        public bool IsHideEntry;
        public float APreBreak;
        public float ThresholdApplied;
        public Vector3 SpotPosition;
        public Vector3 SpotFrontPosition;
        public bool HasSpotFrontPosition;
        public string HideSpotId;
        public bool IsOccupied;
    }

    public enum EventType
    {
        ConfirmWindowElapsed,
        NoiseHeard,
        ChaseReached,
        Reachability,
        CapReached,
        LOSGain,
        LOSBreak,
        HideSpotOccupancy
    }
}
