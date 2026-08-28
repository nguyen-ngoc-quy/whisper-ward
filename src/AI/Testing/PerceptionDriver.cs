using UnityEngine;
using System.Collections.Generic;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Simulated Perception driver for H.0 automated testing.
    /// Injects scripted SensingFacts into the EventBus at specific virtual ticks.
    /// This replaces the real Perception system for deterministic unit/integration tests.
    /// </summary>
    public class PerceptionDriver : MonoBehaviour
    {
        [Header("Test Fixture")]
        public List<ScriptedEvent> eventScript = new List<ScriptedEvent>();

        private int _currentEventIndex = 0;
        private long _lastProcessedTick = -1;

        private void Start()
        {
            // Subscribe to the Virtual Tick Clock
            if (VirtualTickClock.Instance != null)
            {
                VirtualTickClock.Instance.Subscribe(OnTick);
            }
        }

        private void OnDestroy()
        {
            if (VirtualTickClock.Instance != null)
            {
                VirtualTickClock.Instance.Unsubscribe(OnTick);
            }
        }

        private void OnTick(long tick, float delta)
        {
            // Process all events scheduled for this tick
            while (_currentEventIndex < eventScript.Count)
            {
                var evt = eventScript[_currentEventIndex];
                if (evt.Tick <= tick)
                {
                    PublishEvent(evt);
                    _currentEventIndex++;
                }
                else
                {
                    break;
                }
            }
            _lastProcessedTick = tick;
        }

        private void PublishEvent(ScriptedEvent scripted)
        {
            switch (scripted.Type)
            {
                case EventType.ConfirmWindowElapsed:
                    EventBus.Publish(new ConfirmWindowElapsed
                    {
                        EntryId = scripted.EntryId,
                        ResidualR = scripted.ResidualR,
                        ThresholdState = scripted.ThresholdState,
                        Timestamp = VirtualTickClock.Instance.TickInterval * scripted.Tick
                    });
                    break;

                case EventType.NoiseHeard:
                    EventBus.Publish(new NoiseHeard
                    {
                        EntryId = scripted.EntryId,
                        Position = scripted.Position,
                        Kind = scripted.Kind,
                        IsFirstConsumption = scripted.IsFirstConsumption,
                        Timestamp = VirtualTickClock.Instance.TickInterval * scripted.Tick
                    });
                    break;

                case EventType.ChaseReached:
                    EventBus.Publish(new ChaseReached
                    {
                        EntryId = scripted.EntryId,
                        Position = scripted.Position,
                        ResidualR = scripted.ResidualR,
                        ThresholdState = scripted.ThresholdState,
                        Timestamp = VirtualTickClock.Instance.TickInterval * scripted.Tick
                    });
                    break;

                case EventType.Reachability:
                    EventBus.Publish(new Reachability
                    {
                        EntryId = scripted.EntryId,
                        Position = scripted.Position,
                        IsReachable = scripted.IsReachable,
                        PathArrivalM = scripted.PathArrivalM,
                        DeltaYM = scripted.DeltaYM,
                        Timestamp = VirtualTickClock.Instance.TickInterval * scripted.Tick
                    });
                    break;

                case EventType.CapReached:
                    EventBus.Publish(new CapReached
                    {
                        EntryId = scripted.EntryId,
                        Counter = scripted.Counter,
                        Position = scripted.Position,
                        Timestamp = VirtualTickClock.Instance.TickInterval * scripted.Tick
                    });
                    break;

                case EventType.LOSGain:
                    EventBus.Publish(new LOSGain
                    {
                        Position = scripted.Position,
                        Distance = scripted.Distance,
                        Guard = this.gameObject,
                        Timestamp = VirtualTickClock.Instance.TickInterval * scripted.Tick
                    });
                    break;

                case EventType.LOSBreak:
                    EventBus.Publish(new LOSBreak
                    {
                        ResidualR = scripted.ResidualR,
                        BreakType = scripted.BreakType,
                        CausalClass = scripted.CausalClass,
                        GuardYawDelta = scripted.GuardYawDelta,
                        Timestamp = VirtualTickClock.Instance.TickInterval * scripted.Tick
                    });
                    break;
            }
        }

        // Helper methods for the ChaseState to query state (placeholder for real Perception)
        public bool HasLOS()
        {
            // In a real test, this would be driven by the event script
            return false;
        }

        public Vector3 GetPlayerPosition()
        {
            return Vector3.zero;
        }
    }

    [System.Serializable]
    public class ScriptedEvent
    {
        public EventType Type;
        public long Tick;
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
    }

    public enum EventType
    {
        ConfirmWindowElapsed,
        NoiseHeard,
        ChaseReached,
        Reachability,
        CapReached,
        LOSGain,
        LOSBreak
    }
}