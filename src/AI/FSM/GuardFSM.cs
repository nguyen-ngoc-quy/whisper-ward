using UnityEngine;
using System.Collections.Generic;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.FSM
{
    /// <summary>
    /// The core controller for the Guard AI. 3-state machine.
    /// Orchestrates transitions and coordinates with the event bus and virtual clock.
    /// </summary>
    public class GuardFSM : MonoBehaviour
    {
        [Header("FSM Configuration")]
        public GuardGoalMode currentGoalMode = GuardGoalMode.None;
        public string currentEntryId = null;

        private IGuardState _currentState;
        private bool _isTransitioning = false;

        // Cached states to avoid allocations
        private PatrolState _patrolState = new PatrolState();
        private InvestigateState _investigateState = new InvestigateState();
        private ChaseState _chaseState = new ChaseState();

        private void Awake()
        {
            // Initialize to Patrol
            TransitionTo(_patrolState);
        }

        private void Start()
        {
            // Subscribe to the Virtual Tick Clock
            if (VirtualTickClock.Instance != null)
            {
                VirtualTickClock.Instance.Subscribe(OnTick);
            }

            // Subscribe to all sensing facts (inputs)
            EventBus.Subscribe<SensingFact>(OnSensingFactReceived);
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
            if (_currentState != null && !_isTransitioning)
            {
                _currentState.OnUpdate(this, tick, delta);
            }
        }

        private void OnSensingFactReceived(SensingFact fact)
        {
            if (_currentState != null)
            {
                _currentState.OnHandleEvent(this, fact);
            }
        }

        public void TransitionTo(IGuardState newState)
        {
            if (_currentState == newState) return;

            _isTransitioning = true;

            _currentState?.OnExit(this);
            _currentState = newState;
            _currentState.OnEnter(this);

            _isTransitioning = false;
            Debug.Log($"[GuardFSM] Transitioned to {newState.StateName}");
        }

        // Helpers for states to access cached states
        public PatrolState GetPatrolState() => _patrolState;
        public InvestigateState GetInvestigateState() => _investigateState;
        public ChaseState GetChaseState() => _chaseState;

        public void SetGoalMode(GuardGoalMode mode)
        {
            currentGoalMode = mode;
        }
    }
}
