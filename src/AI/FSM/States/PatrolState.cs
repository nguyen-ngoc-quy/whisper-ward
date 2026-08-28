using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;

namespace WhisperWard.AI.FSM.States
{
    public class PatrolState : GuardStateBase
    {
        public override string StateName => "Patrol";

        private int _currentWaypointIndex = 0;
        private float _dwellTimer = 0f;
        private float _scanTimer = 0f;
        private bool _isDwelling = false;
        private bool _isScanning = false;
        private PatrolRoute _route;

        public override void OnEnter(GuardFSM fsm)
        {
            Debug.Log("[PatrolState] Entering Patrol State");
            if (fsm.TryGetComponent<GuardNavigator>(out var nav))
            {
                nav.SetPatrolSpeed();
            }

            // Initialize route if available
            _route = fsm.GetComponent<PatrolRoute>();
            if (_route != null && _route.waypoints.Count > 0)
            {
                _currentWaypointIndex = 0;
                MoveToNextWaypoint(fsm);
            }
        }

        public override void OnUpdate(GuardFSM fsm, long tick, float delta)
        {
            // Handle dwell timer
            if (_isDwelling)
            {
                _dwellTimer -= delta;
                if (_dwellTimer <= 0)
                {
                    _isDwelling = false;
                    _isScanning = true;
                    _scanTimer = _route.waypoints[_currentWaypointIndex].scanArcSpan;
                }
            }
            // Handle scanning
            else if (_isScanning)
            {
                _scanTimer -= delta;
                if (_scanTimer <= 0)
                {
                    _isScanning = false;
                    MoveToNextWaypoint(fsm);
                }
            }
            // Handle movement
            else if (_isMovingToTarget && fsm.TryGetComponent<GuardNavigator>(out var nav))
            {
                if (nav.HasReachedDestination())
                {
                    StartDwell();
                }
            }
        }

        private void MoveToNextWaypoint(GuardFSM fsm)
        {
            if (_route == null || _route.waypoints.Count == 0) return;

            _currentWaypointIndex = (_currentWaypointIndex + 1) % _route.waypoints.Count;
            var wp = _route.waypoints[_currentWaypointIndex];

            if (fsm.TryGetComponent<GuardNavigator>(out var nav))
            {
                nav.MoveTo(wp.targetPosition.position);
            }
        }

        private void StartDwell()
        {
            if (_route == null || _route.waypoints.Count == 0) return;
            _isDwelling = true;
            _dwellTimer = _route.waypoints[_currentWaypointIndex].dwellTime;
        }

        public override void OnHandleEvent(GuardFSM fsm, IEvent evt)
        {
            if (evt is ConfirmWindowElapsed confirmEvt)
            {
                // Transition to Investigate state
                var investigateState = fsm.GetInvestigateState();
                investigateState.Init(confirmEvt.EntryId, fsm.transform.position, "threshold");
                fsm.TransitionTo(investigateState);
            }
            else if (evt is NoiseHeard noiseEvt)
            {
                // Transition to Investigate state
                var investigateState = fsm.GetInvestigateState();
                investigateState.Init(noiseEvt.EntryId, noiseEvt.Position, "noise");
                fsm.TransitionTo(investigateState);
            }
            else if (evt is ChaseReached chaseEvt)
            {
                // Transition to Chase state
                var chaseState = fsm.GetChaseState();
                chaseState.Init(chaseEvt.EntryId, "threshold", chaseEvt.Position);
                fsm.TransitionTo(chaseState);
            }
        }

        private void StartDwell()
        {
            if (_route == null || _route.waypoints.Count == 0) return;
            _isDwelling = true;
            _dwellTimer = _route.waypoints[_currentWaypointIndex].dwellTime;
        }
    }
}