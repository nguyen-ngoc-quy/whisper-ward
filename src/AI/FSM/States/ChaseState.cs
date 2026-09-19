using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;
using WhisperWard.AI.Navigation;

namespace WhisperWard.AI.FSM.States
{
    /// <summary>
    /// Live pursuit state. Player liveness and position are event-sourced inputs;
    /// catch queries use the shared validated E20 physics profile and only run while
    /// this state is authoritative.
    /// </summary>
    public class ChaseState : GuardStateBase
    {
        public override string StateName { get { return "Chase"; } }

        /// <summary>Last authoritative position retained for lifecycle handoff.</summary>
        public Vector3 AuthoritativeEpisodePosition
        {
            get { return _lastKnownPosition; }
        }

        private string _entryId;
        private string _cause;
        private string _terminalCause;
        private Vector3 _initialPosition;
        private Vector3 _promotionPosition;
        private Vector3 _holdPosition;
        private Vector3 _lastKnownPosition;
        private Vector3 _lastObservedPlayerPosition;
        private bool _hasObservedPlayerPosition;
        private bool _witnessedInteriorCapture;
        private PhysicsQueryProfile _physicsProfile;

        private NoiseRuntimeConfiguration _noiseConfiguration;

        private float _giveupTimer;
        private float _durationTimer;
        private int _resightCount;
        private float _resightAccumulator;
        private bool _hasLOS;
        private bool _hasLostLOS;
        private bool _resightActive;
        private bool _hasReachabilityVerdict;
        private bool _isReachable;
        private float _catchTimer;
        private bool _isCatchLive;
        private bool _isHoldingSpotFront;
        private bool _spotOccupancyKnown;
        private bool _spotOccupied;
        private bool _isLivenessPromotion;
        private bool _hasOpenerTPublish;
        private float _openerTPublish;

        private NavMeshAgent _agent;
        private GuardNavigator _navigator;
        private GuardFSM _fsm;

        public void Init(string entryId, string cause, Vector3 position,
            bool witnessedInteriorCapture = false, Vector3? spotFrontPosition = null,
            float? openerTPublish = null)
        {
            _entryId = entryId ?? string.Empty;
            _cause = cause ?? "unknown";
            _initialPosition = position;
            _promotionPosition = position;
            _holdPosition = spotFrontPosition.HasValue
                ? spotFrontPosition.Value : position;
            _lastKnownPosition = position;
            _witnessedInteriorCapture = witnessedInteriorCapture;
            _isLivenessPromotion = false;
            _terminalCause = null;
            _openerTPublish = openerTPublish.HasValue
                ? openerTPublish.Value : 0f;
            _hasOpenerTPublish = openerTPublish.HasValue;
        }

        /// <summary>Initializes Chase as an in-place liveness promotion.</summary>
        public void InitPromotion(string entryId, string cause, Vector3 position)
        {
            InitPromotion(entryId, cause, position, position);
        }

        /// <summary>
        /// Initializes Chase promotion while retaining the authoritative
        /// Investigate episode position separately from Chase entry geometry.
        /// </summary>
        public void InitPromotion(string entryId, string cause, Vector3 position,
            Vector3 promotionPosition, bool witnessedInteriorCapture = false,
            Vector3? spotFrontPosition = null)
        {
            Init(entryId, cause, position, witnessedInteriorCapture,
                spotFrontPosition);
            _promotionPosition = promotionPosition;
            _isLivenessPromotion = true;
        }


        /// <summary>
        /// Injects the validated shared physics profile. A missing profile fails
        /// partial-path catch checks closed instead of rebuilding a World mask.
        /// </summary>
        public void ConfigurePhysicsProfile(PhysicsQueryProfile profile)
        {
            _physicsProfile = profile;
        }

        /// <summary>
        /// Injects the immutable registry-backed Chase tuning contract. The
        /// registered compatibility values remain available for deterministic
        /// fixtures that do not use the composition root.
        /// </summary>
        public void ConfigureNoiseRuntime(NoiseRuntimeConfiguration configuration)
        {
            _noiseConfiguration = configuration;
        }

        private float ChaseGiveupSeconds
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredChaseGiveupSeconds
                : _noiseConfiguration.ChaseGiveupSeconds; }
        }

        private float ChaseCapSeconds
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredChaseCapSeconds
                : _noiseConfiguration.ChaseCapSeconds; }
        }

        private int ResightCap
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredResightCap
                : _noiseConfiguration.ResightCap; }
        }

        private float ResightMinimumSeconds
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredResightMinimumSeconds
                : _noiseConfiguration.ResightMinimumSeconds; }
        }

        private float CatchSeconds
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredCatchSeconds
                : _noiseConfiguration.CatchSeconds; }
        }

        private float CatchRangeMeters
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredCatchRangeMeters
                : _noiseConfiguration.CatchRangeMeters; }
        }

        private float DeltaYToleranceMeters
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredDeltaYToleranceMeters
                : _noiseConfiguration.DeltaYToleranceMeters; }
        }

        private float HysteresisMeters
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredHysteresisMeters
                : _noiseConfiguration.HysteresisMeters; }
        }

        private float NavMeshSampleMaxDistanceMeters
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredNavMeshSampleMaxDistanceMeters
                : _noiseConfiguration.NavMeshSampleMaxDistanceMeters; }
        }

        private float GuardEyeHeightMeters
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredGuardEyeHeightMeters
                : _noiseConfiguration.GuardEyeHeightMeters; }
        }

        public override void OnEnter(GuardFSM fsm)
        {
            Debug.Log($"[ChaseState] Entering Chase via {_cause}");
            _fsm = fsm;
            _terminalCause = null;
            _giveupTimer = ChaseGiveupSeconds;
            _durationTimer = ChaseCapSeconds;
            _resightCount = 0;
            _resightAccumulator = 0f;
            _hasLOS = false;
            _hasLostLOS = false;
            _resightActive = false;
            _hasReachabilityVerdict = false;
            _isReachable = false;
            _hasObservedPlayerPosition = false;
            _lastObservedPlayerPosition = Vector3.zero;
            _catchTimer = CatchSeconds;
            _isCatchLive = false;
            _isHoldingSpotFront = _witnessedInteriorCapture;
            _spotOccupancyKnown = false;
            _spotOccupied = false;

            if (fsm.TryGetComponent<GuardNavigator>(out var nav))
            {
                _navigator = nav;
                _navigator.SetChaseSpeed();
                _navigator.MoveTo(_isHoldingSpotFront
                    ? _holdPosition : _lastKnownPosition);
            }
            _agent = fsm.GetComponent<NavMeshAgent>();
            fsm.SetGoalMode(_isHoldingSpotFront
                ? GuardGoalMode.HideSpotFront : GuardGoalMode.LivePursuit);

            if (_isLivenessPromotion
                && fsm.IsLivenessPromotionPending(_entryId))
            {
                fsm.PublishLivenessFact("promote", "Chase",
                    "investigate-to-chase", fsm.CurrentVirtualTime, _entryId,
                    _promotionPosition, LivenessPositionSources.EpisodePosition);
                fsm.ConsumeLivenessPromotion();
            }
            else
            {
                // A direct Chase opener keeps the authoritative source-event
                // timestamp, including exact zero; the virtual clock is only a
                // fallback when the opener carries no timestamp authority.
                fsm.currentEntryId = _entryId;
                fsm.PublishLivenessFact("open", "Chase",
                    "Chase-entry",
                    _hasOpenerTPublish ? _openerTPublish
                        : fsm.CurrentVirtualTime,
                    _entryId,
                    _initialPosition, LivenessPositionSources.OpenerDecision);
            }

            // The liveness transition is authoritative and must precede the
            // presentation-facing ChaseEntry decision in the same boundary.
            fsm.PublishDecision(new ChaseEntry
            {
                EntryId = _entryId,
                Cause = _cause,
                ThresholdState = "active",
                Position = _initialPosition,
                PairedChaseReached = _cause == "threshold"
            });
        }

        public override void OnUpdate(GuardFSM fsm, long tick, float delta)
        {
            if (fsm.CurrentState != this) return;

            // A witnessed HideSpotFront hold suspends both give-up and duration
            // caps; occupancy or sight-presence must resolve the hold instead.
            if (!_isHoldingSpotFront)
            {
                _durationTimer -= delta;
                if (_durationTimer <= 0f)
                {
                    TerminateChase(fsm, "duration_cap");
                    return;
                }

                bool visibleAndReachable = _hasLOS
                    && _hasReachabilityVerdict && _isReachable;
                if (!visibleAndReachable)
                {
                    _giveupTimer -= delta;
                    if (_giveupTimer <= 0f)
                    {
                        TerminateChase(fsm, "giveup");
                        return;
                    }
                }
                else if (_resightActive)
                {
                    _resightAccumulator += delta;
                    if (_resightAccumulator >= ResightMinimumSeconds)
                    {
                        _resightCount++;
                        _resightAccumulator = 0f;
                        _resightActive = false;
                        if (_resightCount >= ResightCap)
                        {
                            TerminateChase(fsm, "resight_cap");
                            return;
                        }
                    }
                }
            }

            UpdatePursuitTarget(fsm);
            bool hasCatchTarget = _isHoldingSpotFront
                ? _spotOccupancyKnown && _spotOccupied
                : _hasObservedPlayerPosition;
            if (fsm.currentGoalMode != GuardGoalMode.StaleLKP
                && fsm.currentGoalMode != GuardGoalMode.None
                && hasCatchTarget)
            {
                Vector3 catchTarget = _isHoldingSpotFront
                    ? _initialPosition : _lastObservedPlayerPosition;
                bool canCatch = EvaluateCatchContract(catchTarget);
                if (canCatch)
                {
                    if (!_isCatchLive)
                    {
                        _isCatchLive = true;
                        _catchTimer = CatchSeconds;
                    }
                    else
                    {
                        _catchTimer -= delta;
                        if (_catchTimer <= 0f)
                        {
                            Debug.Log("[ChaseState] CAPTURE achieved!");
                            fsm.PublishDecision(new Capture
                            {
                                EntryId = _entryId,
                                Position = catchTarget,
                                Victim = null,
                                IsCarveOut = _witnessedInteriorCapture
                            });
                            TerminateChase(fsm, "capture");
                            return;
                        }
                    }
                }
                else if (_isCatchLive && !IsWithinHysteresis(catchTarget))
                {
                    _isCatchLive = false;
                    _catchTimer = CatchSeconds;
                }
            }
        }

        private void UpdatePursuitTarget(GuardFSM fsm)
        {
            if (_isHoldingSpotFront)
            {
                // A witnessed hide-entry fix remains the interior datum while the
                // occupant is hidden. A genuine sight-presence fact releases the
                // hold and resumes ordinary pursuit.
                if (!_hasLOS || !_hasObservedPlayerPosition)
                {
                    fsm.SetGoalMode(GuardGoalMode.HideSpotFront);
                    return;
                }
                _isHoldingSpotFront = false;
            }

            if (_hasObservedPlayerPosition && _hasLOS)
            {
                _lastKnownPosition = SamplePlayerPosition(_lastObservedPlayerPosition);
                fsm.SetGoalMode(GuardGoalMode.LivePursuit);
            }
            else
            {
                fsm.SetGoalMode(GuardGoalMode.StaleLKP);
                if (_navigator != null) _navigator.MoveTo(_lastKnownPosition);
            }
        }

        private Vector3 SamplePlayerPosition(Vector3 playerPosition)
        {
            NavMeshHit hit;
            if (_agent != null && _agent.areaMask != 0
                && NavMesh.SamplePosition(playerPosition, out hit,
                    NavMeshSampleMaxDistanceMeters, _agent.areaMask))
                return hit.position;
            return playerPosition;
        }

        private bool EvaluateCatchContract(Vector3 playerPosition)
        {
            // State identity is a conjunct of the catch contract; a stale state
            // cannot capture during a same-boundary transition.
            if (_fsm == null || _fsm.CurrentState != this
                || (_fsm.currentGoalMode != GuardGoalMode.LivePursuit
                    && _fsm.currentGoalMode != GuardGoalMode.HideSpotFront))
                return false;

            // A shallow off-mesh target may use the occlusion-gated backstop when
            // no same-surface proxy exists; a projected target requires both path
            // arrival and the backstop.
            NavMeshHit proxy;
            if (_agent == null || _agent.areaMask == 0)
                return false;
            if (!NavMesh.SamplePosition(playerPosition, out proxy,
                NavMeshSampleMaxDistanceMeters, _agent.areaMask))
                return EvaluateBackstop(playerPosition);
            return EvaluatePathArrival(proxy.position, playerPosition)
                && EvaluateBackstop(playerPosition);
        }

        private bool EvaluatePathArrival(Vector3 pathTarget,
            Vector3 playerPosition)
        {
            if (_agent == null) return false;
            NavMeshPath path = new NavMeshPath();
            _agent.CalculatePath(pathTarget, path);
            if (path.status == NavMeshPathStatus.PathInvalid) return false;

            float pathLength = GetPathLength(path);
            if (path.status == NavMeshPathStatus.PathPartial)
                return EvaluatePartialPathClearance(path, playerPosition)
                    && pathLength <= CatchRangeMeters + HysteresisMeters;
            return pathLength <= CatchRangeMeters + HysteresisMeters;
        }

        private bool EvaluatePartialPathClearance(NavMeshPath path, Vector3 playerPosition)
        {
            if (_physicsProfile == null || path.corners == null || path.corners.Length == 0)
                return false;

            Vector3 lastCorner = path.corners[path.corners.Length - 1];
            float height = _agent == null ? 2f : _agent.height;
            Vector3 origin = lastCorner + Vector3.up * (height / 2f);
            Vector3 target = playerPosition + Vector3.up * (height / 2f);
            return _physicsProfile.Linecast(origin, target).Clear;
        }

        private bool EvaluateBackstop(Vector3 playerPosition)
        {
            if (_physicsProfile == null) return false;
            Vector3 delta = playerPosition - _fsm.transform.position;
            float xzDistance = new Vector2(delta.x, delta.z).magnitude;
            if (xzDistance > CatchRangeMeters + HysteresisMeters
                || Mathf.Abs(delta.y) > DeltaYToleranceMeters)
                return false;

            Vector3 eye = _fsm.transform.position
                + Vector3.up * GuardEyeHeightMeters;
            Vector3 target = playerPosition + Vector3.up * 0.25f;
            return _physicsProfile.Linecast(eye, target).Clear;
        }

        private bool IsWithinHysteresis(Vector3 playerPosition)
        {
            Vector3 delta = playerPosition - _fsm.transform.position;
            float xzDistance = new Vector2(delta.x, delta.z).magnitude;
            return xzDistance <= CatchRangeMeters + HysteresisMeters
                && Mathf.Abs(delta.y) <= DeltaYToleranceMeters;
        }

        private float GetPathLength(NavMeshPath path)
        {
            float length = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++)
                length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            return length;
        }

        public override void OnHandleEvent(GuardFSM fsm, IEvent evt)
        {
            if (evt is HideSpotOccupancy occupancy && _isHoldingSpotFront
                && (string.IsNullOrEmpty(occupancy.EntryId)
                    || string.Equals(occupancy.EntryId, _entryId,
                        System.StringComparison.Ordinal)))
            {
                _spotOccupancyKnown = true;
                _spotOccupied = occupancy.IsOccupied;
                if (!occupancy.IsOccupied)
                {
                    // Exit wins over a same-boundary catch check. Chase-tier
                    // holds publish their normal terminal ChaseEnd; occupancy
                    // itself remains HideSpot-owned.
                    _terminalCause = "occupancy-exit";
                    fsm.PublishDecision(new ChaseEnd
                    {
                        EntryId = _entryId,
                        Guard = fsm.gameObject
                    });
                    fsm.TransitionTo(fsm.GetPatrolState());
                }
                return;
            }

            if (evt is LivenessSnapshot snapshot)
            {
                if (!string.IsNullOrEmpty(snapshot.GuardEid)
                    && !string.Equals(snapshot.GuardEid, fsm.GuardEid,
                        System.StringComparison.Ordinal))
                    return;

                if (snapshot.HasLOS && !_hasLOS && _hasLostLOS)
                {
                    _resightActive = true;
                    _resightAccumulator = 0f;
                }
                if (!snapshot.HasLOS && _hasLOS)
                    _hasLostLOS = true;
                _hasLOS = snapshot.HasLOS;
                if (snapshot.HasLOS)
                {
                    _lastObservedPlayerPosition = snapshot.PlayerPosition;
                    _hasObservedPlayerPosition = true;
                }
                return;
            }

            if (evt is LOSGain losGain)
            {
                if (!string.IsNullOrEmpty(losGain.GuardEid)
                    && !string.Equals(losGain.GuardEid, fsm.GuardEid,
                        System.StringComparison.Ordinal))
                    return;
                if (!_hasLOS && _hasLostLOS)
                {
                    _resightActive = true;
                    _resightAccumulator = 0f;
                }
                _hasLOS = true;
                _lastObservedPlayerPosition = losGain.Position;
                _hasObservedPlayerPosition = true;
                return;
            }

            if (evt is LOSBreak losBreak)
            {
                if (!string.IsNullOrEmpty(losBreak.GuardEid)
                    && !string.Equals(losBreak.GuardEid, fsm.GuardEid,
                        System.StringComparison.Ordinal))
                    return;
                _hasLOS = false;
                _hasLostLOS = true;
                _resightActive = false;
                return;
            }

            if (evt is NoiseHeardRelay relay)
            {
                // Chase never opens a second episode from noise. Still reserve
                // the relay identity so duplicate delivery has one outcome.
                if (!fsm.TryBeginRelay(relay))
                {
                    fsm.PublishRelayOutcome(relay, RelayConsumption.Ignored);
                    return;
                }
                fsm.PublishRelayOutcome(relay, RelayConsumption.Ignored);
                return;
            }

            if (evt is Reachability reachEvt)
            {
                if (!string.IsNullOrEmpty(reachEvt.GuardEid)
                    && !string.Equals(reachEvt.GuardEid, fsm.GuardEid,
                        System.StringComparison.Ordinal))
                    return;
                _hasReachabilityVerdict = true;
                _isReachable = reachEvt.IsReachable;
                if (!reachEvt.IsReachable && !_isHoldingSpotFront)
                {
                    fsm.SetGoalMode(GuardGoalMode.StaleLKP);
                    if (_navigator != null) _navigator.MoveTo(_lastKnownPosition);
                }
                else if (reachEvt.IsReachable && !_isHoldingSpotFront)
                {
                    fsm.SetGoalMode(GuardGoalMode.LivePursuit);
                }
            }
        }

        private void TerminateChase(GuardFSM fsm, string reason)
        {
            _terminalCause = reason;
            Debug.Log($"[ChaseState] Chase terminated: {reason}");
            // Every Chase-tier termination, including a witnessed hide-entry
            // capture, closes the Chase episode. Investigate-tier carve-out
            // captures are emitted by InvestigateState and never reach here.
            fsm.PublishDecision(new ChaseEnd
            {
                EntryId = _entryId,
                Guard = fsm.gameObject
            });
            fsm.TransitionTo(fsm.GetPatrolState());
        }

        public override void OnExit(GuardFSM fsm)
        {
            Debug.Log("[ChaseState] Exiting state");
            if (!fsm.IsBoundaryResetting && !string.IsNullOrWhiteSpace(_entryId))
            {
                fsm.PublishLivenessFact("close", "Chase",
                    _terminalCause ?? "Chase-end", fsm.CurrentVirtualTime,
                    _entryId, _lastKnownPosition,
                    LivenessPositionSources.TerminalDecision);
            }
            fsm.SetGoalMode(GuardGoalMode.None);
        }
    }
}
