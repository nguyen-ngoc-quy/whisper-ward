using System;
using UnityEngine;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;
using WhisperWard.AI.Navigation;

namespace WhisperWard.AI.FSM.States
{
    /// <summary>
    /// Patrol is the default state and the only state that opens a new noise-led
    /// Investigate episode from an eligible immutable relay.
    /// </summary>
    public class PatrolState : GuardStateBase
    {
        public override string StateName { get { return "Patrol"; } }

        private int _currentWaypointIndex;
        private float _dwellTimer;
        private float _scanTimer;
        private bool _isDwelling;
        private bool _isScanning;
        private PatrolRoute _route;

        public override void OnEnter(GuardFSM fsm)
        {
            Debug.Log("[PatrolState] Entering Patrol State");
            fsm.SetGoalMode(GuardGoalMode.None);
            if (fsm.TryGetComponent<GuardNavigator>(out var nav))
                nav.SetPatrolSpeed();

            _route = fsm.GetComponent<PatrolRoute>();
            _isDwelling = false;
            _isScanning = false;
            if (_route != null && _route.waypoints.Count > 0)
            {
                _currentWaypointIndex = 0;
                MoveToNextWaypoint(fsm);
            }
        }

        public override void OnUpdate(GuardFSM fsm, long tick, float delta)
        {
            if (_route == null || _route.waypoints.Count == 0) return;

            if (_isDwelling)
            {
                _dwellTimer -= delta;
                if (_dwellTimer <= 0f)
                {
                    _isDwelling = false;
                    _isScanning = true;
                    _scanTimer = _route.waypoints[_currentWaypointIndex].scanArcSpan;
                }
            }
            else if (_isScanning)
            {
                _scanTimer -= delta;
                if (_scanTimer <= 0f)
                {
                    _isScanning = false;
                    MoveToNextWaypoint(fsm);
                }
            }
            else if (fsm.TryGetComponent<GuardNavigator>(out var nav)
                && nav.HasReachedDestination())
            {
                StartDwell();
            }
        }

        private void MoveToNextWaypoint(GuardFSM fsm)
        {
            if (_route == null || _route.waypoints.Count == 0) return;
            _currentWaypointIndex = (_currentWaypointIndex + 1) % _route.waypoints.Count;
            var waypoint = _route.waypoints[_currentWaypointIndex];
            if (fsm.TryGetComponent<GuardNavigator>(out var nav))
                nav.MoveTo(waypoint.targetPosition.position);
        }

        private void StartDwell()
        {
            if (_route == null || _route.waypoints.Count == 0) return;
            _isDwelling = true;
            _dwellTimer = _route.waypoints[_currentWaypointIndex].dwellTime;
        }

        public override void OnHandleEvent(GuardFSM fsm, IEvent evt)
        {
            if (evt is LOSBreak hideBreak && hideBreak.IsHideEntry)
            {
                if (!string.IsNullOrEmpty(hideBreak.GuardEid)
                    && !string.Equals(hideBreak.GuardEid, fsm.GuardEid,
                        StringComparison.Ordinal))
                    return;

                if (hideBreak.APreBreak >= hideBreak.ThresholdApplied)
                {
                    var chaseState = fsm.GetChaseState();
                    chaseState.Init(hideBreak.EntryId, "hide-entry",
                        hideBreak.SpotPosition, true,
                        hideBreak.HasSpotFrontPosition
                            ? hideBreak.SpotFrontPosition : (Vector3?)null);
                    fsm.TransitionTo(chaseState);
                }
                else
                {
                    var investigateState = fsm.GetInvestigateState();
                    investigateState.InitHideEntry(hideBreak.EntryId,
                        hideBreak.SpotPosition, hideBreak.ResidualR,
                        hideBreak.HasSpotFrontPosition
                            ? hideBreak.SpotFrontPosition : (Vector3?)null);
                    fsm.TransitionTo(investigateState);
                }
                return;
            }

            if (evt is CapReached capEvt)
            {
                var investigateState = fsm.GetInvestigateState();
                investigateState.Init(capEvt.EntryId, capEvt.Position,
                    "cap-forced", 0f);
                fsm.TransitionTo(investigateState);
                return;
            }

            if (evt is ConfirmWindowElapsed confirmEvt)
            {
                var investigateState = fsm.GetInvestigateState();
                investigateState.Init(confirmEvt.EntryId, fsm.transform.position,
                    "threshold", confirmEvt.ResidualR);
                fsm.TransitionTo(investigateState);
                return;
            }

            if (evt is NoiseHeardRelay relay)
            {
                if (!fsm.TryBeginRelay(relay))
                {
                    fsm.PublishRelayOutcome(relay, RelayConsumption.Ignored);
                    return;
                }

                if (fsm.currentGoalMode == GuardGoalMode.HideSpotFront)
                {
                    fsm.PublishRelayOutcome(relay, RelayConsumption.Ignored);
                    return;
                }

                var investigateState = fsm.GetInvestigateState();
                investigateState.Init(relay);
                fsm.PublishRelayOutcome(relay, RelayConsumption.Consumed);
                fsm.TransitionTo(investigateState);
                return;
            }

            if (evt is ChaseReached chaseEvt)
            {
                var chaseState = fsm.GetChaseState();
                chaseState.Init(chaseEvt.EntryId, "threshold", chaseEvt.Position);
                fsm.TransitionTo(chaseState);
            }
        }
    }
}
