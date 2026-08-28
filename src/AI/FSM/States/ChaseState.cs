using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Navigation;

namespace WhisperWard.AI.FSM.States
{
    public class ChaseState : GuardStateBase
    {
        public override string StateName => "Chase";

        private string _entryId;
        private string _cause;
        private Vector3 _initialPosition;
        private Vector3 _lastKnownPosition;

        // Tuning Knobs (consumed from registry/entities.yaml)
        private const float T_GIVEUP_CHASE = 8.0f;
        private const float T_CAP_CHASE = 30.0f;
        private const int N_RESIGHT_CAP = 2;
        private const float T_RESIGHT_MIN = 1.0f;
        private const float T_CATCH = 1.0f;
        private const float CATCH_RANGE = 5.5f;
        private const float DELTA_Y_TOLERANCE = 1.0f;
        private const float HYSTERESIS = 0.5f;
        private const float NAVMESH_SAMPLE_MAXDISTANCE = 0.4f;

        private float _giveupTimer;
        private float _durationTimer;
        private int _resightCount = 0;
        private float _resightAccumulator = 0f;
        private bool _hasLOS = false;

        private float _catchTimer = 0f;
        private bool _isCatchLive = false;
        private bool _isHoldingSpotFront = false;

        private NavMeshAgent _agent;
        private GuardNavigator _navigator;
        private GuardFSM _fsm;

        public void Init(string entryId, string cause, Vector3 position)
        {
            _entryId = entryId;
            _cause = cause;
            _initialPosition = position;
            _lastKnownPosition = position;
        }

        public override void OnEnter(GuardFSM fsm)
        {
            Debug.Log($"[ChaseState] Entering Chase via {_cause}");
            _fsm = fsm;
            _giveupTimer = T_GIVEUP_CHASE;
            _durationTimer = T_CAP_CHASE;
            _resightCount = 0;
            _resightAccumulator = 0f;
            _catchTimer = T_CATCH;
            _isCatchLive = false;
            _isHoldingSpotFront = false;

            // Set chase speed
            if (fsm.TryGetComponent<GuardNavigator>(out var nav))
            {
                _navigator = nav;
                _navigator.SetChaseSpeed();
                _navigator.MoveTo(_lastKnownPosition);
            }

            // Get NavMeshAgent reference
            _agent = fsm.GetComponent<NavMeshAgent>();

            // Set goal mode
            fsm.SetGoalMode(GuardGoalMode.LivePursuit);

            // Publish Chase-entry decision record
            PublishDecision(new ChaseEntry
            {
                EntryId = _entryId,
                Cause = _cause,
                ThresholdState = "active",
                Position = _initialPosition,
                PairedChaseReached = (_cause == "threshold")
            });
        }

        public override void OnUpdate(GuardFSM fsm, long tick, float delta)
        {
            // 1. Duration Cap
            _durationTimer -= delta;
            if (_durationTimer <= 0)
            {
                TerminateChase(fsm, "duration_cap");
                return;
            }

            // 2. Update LOS state
            UpdateLOS(fsm);

            // 3. Give-up Window (ticks when NOT in sustained LOS)
            if (!_hasLOS)
            {
                _giveupTimer -= delta;
                if (_giveupTimer <= 0)
                {
                    TerminateChase(fsm, "giveup");
                    return;
                }
            }
            else
            {
                // Reset give-up timer on re-sight
                _giveupTimer = T_GIVEUP_CHASE;
                _resightAccumulator += delta;

                if (_resightAccumulator >= T_RESIGHT_MIN)
                {
                    _resightCount++;
                    _resightAccumulator = 0;

                    if (_resightCount >= N_RESIGHT_CAP)
                    {
                        TerminateChase(fsm, "resight_cap");
                        return;
                    }
                }
            }

            // 4. Update pursuit target
            UpdatePursuitTarget(fsm);

            // 5. Catch Cycle
            if (_fsm.currentGoalMode != GuardGoalMode.StaleLKP &&
                _fsm.currentGoalMode != GuardGoalMode.None)
            {
                Vector3 playerPos = GetPlayerPosition(fsm);
                if (playerPos != Vector3.zero)
                {
                    bool canCatch = EvaluateCatchContract(playerPos);

                    if (canCatch)
                    {
                        if (!_isCatchLive)
                        {
                            _isCatchLive = true;
                            _catchTimer = T_CATCH;
                        }
                        else
                        {
                            _catchTimer -= delta;
                            if (_catchTimer <= 0)
                            {
                                // CAPTURE!
                                Debug.Log("[ChaseState] CAPTURE achieved!");
                                PublishDecision(new Capture
                                {
                                    EntryId = _entryId,
                                    Position = fsm.transform.position,
                                    Victim = null, // Would reference player GameObject
                                    IsCarveOut = _isHoldingSpotFront
                                });
                                TerminateChase(fsm, "capture");
                                return;
                            }
                        }
                    }
                    else
                    {
                        // Reset catch timer if not in range
                        if (_isCatchLive)
                        {
                            // Use hysteresis to prevent flickering
                            if (!IsWithinHysteresis(playerPos))
                            {
                                _isCatchLive = false;
                                _catchTimer = T_CATCH;
                            }
                        }
                    }
                }
            }
        }

        private void UpdateLOS(GuardFSM fsm)
        {
            var perceptionDriver = fsm.GetComponent<PerceptionDriver>();
            if (perceptionDriver != null)
            {
                _hasLOS = perceptionDriver.HasLOS();
            }
        }

        private Vector3 GetPlayerPosition(GuardFSM fsm)
        {
            var perceptionDriver = fsm.GetComponent<PerceptionDriver>();
            return perceptionDriver?.GetPlayerPosition() ?? Vector3.zero;
        }

        private void UpdatePursuitTarget(GuardFSM fsm)
        {
            Vector3 playerPos = GetPlayerPosition(fsm);

            if (playerPos != Vector3.zero && _hasLOS)
            {
                // Live pursuit - sample player position at throttle
                _lastKnownPosition = SamplePlayerPosition(playerPos);
                _fsm.SetGoalMode(GuardGoalMode.LivePursuit);
            }
            else if (_isHoldingSpotFront)
            {
                // Continue holding at spot front
                _fsm.SetGoalMode(GuardGoalMode.HideSpotFront);
            }
            else
            {
                // Move to last known position
                _fsm.SetGoalMode(GuardGoalMode.StaleLKP);
                if (_navigator != null)
                {
                    _navigator.MoveTo(_lastKnownPosition);
                }
            }
        }

        private Vector3 SamplePlayerPosition(Vector3 playerPosition)
        {
            // NavMesh.SamplePosition at 2-5 Hz throttle
            if (NavMesh.SamplePosition(playerPosition, out NavMeshHit hit, NAVMESH_SAMPLE_MAXDISTANCE, NavMesh.AllAreas))
            {
                return hit.position;
            }
            return playerPosition;
        }

        private bool EvaluateCatchContract(Vector3 playerPosition)
        {
            // Nav-goal gate: only run if in LivePursuit or HideSpotFront
            if (_fsm.currentGoalMode != GuardGoalMode.LivePursuit &&
                _fsm.currentGoalMode != GuardGoalMode.HideSpotFront)
            {
                return false;
            }

            // Path-arrival metric using agent-parameterized NavMeshAgent.CalculatePath
            if (!EvaluatePathArrival(playerPosition))
            {
                return false;
            }

            // Backstop: XZ-plane distance + DeltaY tolerance
            if (!EvaluateBackstop(playerPosition))
            {
                return false;
            }

            return true;
        }

        private bool EvaluatePathArrival(Vector3 playerPosition)
        {
            if (_agent == null) return false;

            NavMeshPath path = new NavMeshPath();
            _agent.CalculatePath(playerPosition, path);

            if (path.status == NavMeshPathStatus.PathInvalid)
            {
                return false;
            }

            float pathLength = GetPathLength(path);

            // Check against catch_range + hysteresis
            if (pathLength > CATCH_RANGE + HYSTERESIS)
            {
                // For partial paths, check if the closest point is geometry-clear
                if (path.status == NavMeshPathStatus.PathPartial)
                {
                    return EvaluatePartialPathClearance(path, playerPosition);
                }
                return false;
            }

            return true;
        }

        private bool EvaluatePartialPathClearance(NavMeshPath path, Vector3 playerPosition)
        {
            // Get the last corner (closest reachable point)
            Vector3 lastCorner = path.corners[path.corners.Length - 1];

            // Physics.Linecast check with capsule-height offsets
            float height = _agent?.height ?? 2f;
            Vector3 origin = lastCorner + Vector3.up * (height / 2f);
            Vector3 target = playerPosition + Vector3.up * (height / 2f);

            // Use World/solid layer-mask, QueryTriggerInteraction.Ignore
            int worldLayerMask = LayerMask.GetMask("World", "Solid");
            if (Physics.Linecast(origin, target, worldLayerMask, QueryTriggerInteraction.Ignore))
            {
                return false; // Obstructed partial path never passes
            }

            return true;
        }

        private bool EvaluateBackstop(Vector3 playerPosition)
        {
            Vector3 guardPos = _fsm.transform.position;
            Vector3 delta = playerPosition - guardPos;

            // XZ plane distance
            float xzDist = new Vector2(delta.x, delta.z).magnitude;
            if (xzDist > CATCH_RANGE + HYSTERESIS)
            {
                return false;
            }

            // DeltaY tolerance
            if (Mathf.Abs(delta.y) > DELTA_Y_TOLERANCE)
            {
                return false;
            }

            return true;
        }

        private bool IsWithinHysteresis(Vector3 playerPosition)
        {
            Vector3 guardPos = _fsm.transform.position;
            Vector3 delta = playerPosition - guardPos;
            float xzDist = new Vector2(delta.x, delta.z).magnitude;

            return xzDist <= CATCH_RANGE + HYSTERESIS && Mathf.Abs(delta.y) <= DELTA_Y_TOLERANCE;
        }

        private float GetPathLength(NavMeshPath path)
        {
            float length = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            }
            return length;
        }

        public override void OnHandleEvent(GuardFSM fsm, IEvent evt)
        {
            if (evt is Reachability reachEvt)
            {
                if (!reachEvt.IsReachable && !_isHoldingSpotFront)
                {
                    _fsm.SetGoalMode(GuardGoalMode.StaleLKP);
                    if (fsm.TryGetComponent<GuardNavigator>(out var nav))
                    {
                        nav.MoveTo(_lastKnownPosition);
                    }
                }
                else
                {
                    _fsm.SetGoalMode(GuardGoalMode.LivePursuit);
                }
            }
        }

        private void TerminateChase(GuardFSM fsm, string reason)
        {
            Debug.Log($"[ChaseState] Chase terminated: {reason}");

            PublishDecision(new ChaseEnd
            {
                EntryId = _entryId,
                Guard = fsm.gameObject
            });

            // In a real implementation, this would trigger the Post-Chase Sweep
            fsm.TransitionTo(fsm.GetPatrolState());
        }

        public override void OnExit(GuardFSM fsm)
        {
            Debug.Log("[ChaseState] Exiting state");
            // Reset goal mode
            fsm.SetGoalMode(GuardGoalMode.None);
        }
    }
}