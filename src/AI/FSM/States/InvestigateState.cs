using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.Core;
using WhisperWard.AI.Perception;
using WhisperWard.AI.Navigation;

namespace WhisperWard.AI.FSM.States
{
    /// <summary>
    /// Investigates an immutable sensing origin. Liveness arrives as facts; this
    /// state never polls Perception or a render-frame camera/controller component.
    /// </summary>
    public class InvestigateState : GuardStateBase
    {
        public override string StateName { get { return "Investigate"; } }

        /// <summary>Last authoritative position retained for lifecycle handoff.</summary>
        public Vector3 AuthoritativeEpisodePosition
        {
            get { return _targetPosition; }
        }

        private Vector3 _targetPosition;
        private Vector3 _interiorPosition;
        private Vector3 _episodeAnchorPosition;
        private string _entryId;
        private string _cause;
        private string _anchorSessionId;
        private string _anchorGuardEid;
        private ulong _episodeAnchorFactId;
        private float _episodeAnchorTimestamp;
        private float _residualAtAnchor;
        private float _giveupTimer;
        private bool _searchArmed;
        private bool _witnessedEntryAuthority;
        private float _thoroughness = 1f;
        private bool _isMovingToTarget;
        private float _pathEndSuppressionTimer;
        private int _pathEndObservationTicks;
        private string _startArm = "not-started";
        private bool _hasLOS;
        private bool _hasReachabilityVerdict;
        private bool _isReachable;
        private bool _hasEpisodeAnchorTimestamp;
        private bool _resolved;
        private bool _spotHoldActive;
        private int _currentSweep;
        private int _totalSweeps;
        private float _sweepTimer;
        private int _corroborationCount;
        private int _targetSampleIndex;
        private uint _targetSeed;
        private string _targetResolution = "authored";
        private readonly List<Vector3> _scanTargetPositions = new List<Vector3>();
        private int _currentScan;
        private bool _spotOccupancyKnown;
        private bool _spotOccupied;
        private float _spotVerifyTimer;
        private float _catchTimer;
        private bool _catchTimerLive;
        private NavMeshAgent _agent;
        private PhysicsQueryProfile _physicsProfile;
        private NoiseRuntimeConfiguration _noiseConfiguration;
        private float _difficultyScalar = 1f;
        private string _terminalCause;

        private float SweepBaseSeconds
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredSweepBaseSeconds
                : _noiseConfiguration.SweepBaseSeconds; }
        }

        private int SweepReferenceCount
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredSweepReferenceCount
                : _noiseConfiguration.SweepReferenceCount; }
        }

        private float InvestigateErrorRadiusMeters
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredInvestigateErrorRadiusMeters
                : _noiseConfiguration.InvestigateErrorRadiusMeters; }
        }

        private float SpotFrontVerifySeconds
        {
            get { return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredSpotFrontVerifySeconds
                : _noiseConfiguration.SpotFrontVerifySeconds; }
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

        /// <summary>
        /// Injects the immutable registry-backed timing contract. Until the
        /// composition root supplies it, the registered compatibility profile is
        /// used for deterministic fixtures.
        /// </summary>
        public void ConfigureNoiseRuntime(NoiseRuntimeConfiguration configuration)
        {
            _noiseConfiguration = configuration;
        }

        public void Init(string entryId, Vector3 position, string cause,
            float residualR = 0f, float? episodeOpenTPublish = null)
        {
            _entryId = entryId ?? string.Empty;
            _episodeAnchorPosition = position;
            _interiorPosition = position;
            _targetPosition = position;
            _cause = cause ?? "unknown";
            _anchorSessionId = string.Empty;
            _anchorGuardEid = string.Empty;
            _episodeAnchorFactId = 0;
            _episodeAnchorTimestamp = episodeOpenTPublish ?? 0f;
            _hasEpisodeAnchorTimestamp = episodeOpenTPublish.HasValue;
            _residualAtAnchor = Mathf.Clamp01(residualR);
            _targetSeed = 0u;
            _targetSampleIndex = 0;
            _targetResolution = "authored";
            _witnessedEntryAuthority = false;
            _spotHoldActive = false;
            _corroborationCount = 0;
            _targetSampleIndex = 0;
            _terminalCause = null;
            ResetTiming();
        }

        /// <summary>
        /// Injects the shared validated physics profile used by witnessed-entry
        /// interior catch checks.
        /// </summary>
        public void ConfigurePhysicsProfile(PhysicsQueryProfile profile)
        {
            _physicsProfile = profile;
        }

        /// <summary>
        /// Initializes a witnessed hide-entry inspection at the authored spot
        /// position. The authority survives retargeting and is not inferred later.
        /// </summary>
        public void InitHideEntry(string entryId, Vector3 spotPosition,
            float residualR, Vector3? spotFrontPosition = null,
            float? episodeOpenTPublish = null)
        {
            Init(entryId, spotPosition, "hide-entry", residualR,
                episodeOpenTPublish);
            _targetPosition = spotFrontPosition.HasValue
                ? spotFrontPosition.Value : spotPosition;
            _witnessedEntryAuthority = true;
            _spotHoldActive = true;
        }

        /// <summary>
        /// Retargets the current episode without publishing a second Investigate
        /// decision. This is the sub-threshold witnessed-entry fallback.
        /// </summary>
        public void RetargetHideEntry(GuardFSM fsm, string entryId,
            Vector3 spotPosition, float residualR,
            Vector3? spotFrontPosition = null)
        {
            Vector3 anchorPosition = _episodeAnchorPosition;
            string anchorSessionId = _anchorSessionId;
            string anchorGuardEid = _anchorGuardEid;
            ulong anchorFactId = _episodeAnchorFactId;
            float anchorTimestamp = _episodeAnchorTimestamp;
            bool hasAnchorTimestamp = _hasEpisodeAnchorTimestamp;
            InitHideEntry(entryId, spotPosition, residualR, spotFrontPosition,
                hasAnchorTimestamp ? anchorTimestamp : (float?)null);
            _episodeAnchorPosition = anchorPosition;
            _anchorSessionId = anchorSessionId;
            _anchorGuardEid = anchorGuardEid;
            _episodeAnchorFactId = anchorFactId;
            _episodeAnchorTimestamp = anchorTimestamp;
            _hasEpisodeAnchorTimestamp = hasAnchorTimestamp;
            BuildScanTargets();
            fsm.currentEntryId = _entryId;
            fsm.SetGoalMode(GuardGoalMode.HideSpotFront);
            if (fsm.TryGetComponent<GuardNavigator>(out var nav))
                nav.MoveTo(_targetPosition);
        }

        /// <summary>
        /// Initializes a noise-led episode from Perception's immutable relay.
        /// </summary>
        public void Init(NoiseHeardRelay relay)
        {
            if (relay == null) throw new ArgumentNullException(nameof(relay));
            _entryId = relay.EntryId;
            _episodeAnchorPosition = relay.AuthoritativeOrigin;
            _anchorSessionId = relay.SessionId;
            _anchorGuardEid = relay.GuardEid;
            _episodeAnchorFactId = relay.FactId;
            _episodeAnchorTimestamp = relay.TPublish;
            _hasEpisodeAnchorTimestamp = true;
            _residualAtAnchor = Mathf.Clamp01(relay.ResidualAtHearing);
            _witnessedEntryAuthority = false;
            _spotHoldActive = false;
            _cause = "noise";
            _corroborationCount = 1;
            _targetSampleIndex = 0;
            _targetPosition = BuildDeterministicTarget(relay.AuthoritativeOrigin, relay.SessionId,
                relay.GuardEid, relay.EntryId, relay.FactId, 0);
            _targetResolution = "candidate";
            _terminalCause = null;
            ResetTiming();
        }

        private void ResetTiming()
        {
            float thoroughnessCoefficient = _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredThoroughnessCoefficient : _noiseConfiguration.ThoroughnessCoefficient;
            float residualMax = _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredReanchorMaxMeters : _noiseConfiguration.ReanchorMaxMeters;
            float giveupBase = _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredInvestigateBaseSeconds : _noiseConfiguration.InvestigateBaseSeconds;
            _difficultyScalar = _noiseConfiguration == null
                ? 1f : _noiseConfiguration.DifficultyScalar;
            _thoroughness = 1f + thoroughnessCoefficient
                * (_residualAtAnchor / Mathf.Max(0.0001f, residualMax));
            _totalSweeps = Mathf.Max(1, Mathf.CeilToInt(SweepReferenceCount * _thoroughness));
            _giveupTimer = giveupBase * _difficultyScalar * _thoroughness;
            _searchArmed = false;
            _isMovingToTarget = true;
            _pathEndSuppressionTimer = NoiseRuntimeConfiguration
                .RegisteredInvestigatePathEndSuppressMaxSeconds;
            _pathEndObservationTicks = 0;
            _startArm = "not-started";
            _hasLOS = false;
            _hasReachabilityVerdict = false;
            _isReachable = false;
            _resolved = false;
            _currentSweep = 0;
            _currentScan = 0;
            _sweepTimer = 0f;
            _spotOccupancyKnown = false;
            _spotOccupied = false;
            _spotVerifyTimer = SpotFrontVerifySeconds;
            _catchTimer = CatchSeconds;
            _catchTimerLive = false;
            _scanTargetPositions.Clear();
        }

        private float GetGiveupBase()
        {
            return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredInvestigateBaseSeconds : _noiseConfiguration.InvestigateBaseSeconds;
        }

        private float GetThoroughnessCoefficient()
        {
            return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredThoroughnessCoefficient : _noiseConfiguration.ThoroughnessCoefficient;
        }

        private float GetResidualMax()
        {
            return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredReanchorMaxMeters : _noiseConfiguration.ReanchorMaxMeters;
        }

        private float GetReanchorExtend()
        {
            return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredNoiseReanchorExtendSeconds : _noiseConfiguration.NoiseReanchorExtendSeconds;
        }

        private float GetCorroborateWindow()
        {
            return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredNoiseCorroborateWindowSeconds
                : _noiseConfiguration.NoiseCorroborateWindowSeconds;
        }

        private float GetCorroborateRadius()
        {
            return _noiseConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredNoiseCorroborateRadiusMeters
                : _noiseConfiguration.NoiseCorroborateRadiusMeters;
        }

        public override void OnEnter(GuardFSM fsm)
        {
            Debug.Log($"[InvestigateState] Entering state for {_cause}");
            _agent = fsm.GetComponent<NavMeshAgent>();
            fsm.SetGoalMode(_spotHoldActive
                ? GuardGoalMode.HideSpotFront : GuardGoalMode.None);
            if (fsm.TryGetComponent<GuardNavigator>(out var nav))
            {
                nav.SetInvestigateSpeed();
                nav.MoveTo(_targetPosition);
            }
            ResetTiming();

            BuildScanTargets();

            fsm.currentEntryId = _entryId;
            fsm.PublishDecision(new InvestigateCommit
            {
                EntryId = _entryId,
                ResidualR = _residualAtAnchor,
                Cause = _cause,
                Position = _episodeAnchorPosition,
                ThresholdState = "active",
                IsHideEntry = _witnessedEntryAuthority,
                EpisodeAnchorSessionId = _anchorSessionId,
                EpisodeAnchorGuardEid = _anchorGuardEid,
                EpisodeAnchorFactId = _episodeAnchorFactId,
                EpisodeAnchorTPublish = _episodeAnchorTimestamp,
                TargetPosition = _targetPosition,
                TargetSeed = _targetSeed,
                TargetSampleIndex = _targetSampleIndex,
                TargetResolution = _targetResolution
            });
            fsm.PublishLivenessFact("open", "Investigate",
                "investigate-commit", _hasEpisodeAnchorTimestamp
                    ? _episodeAnchorTimestamp : fsm.CurrentVirtualTime, _entryId,
                _episodeAnchorPosition, LivenessPositionSources.OpenerDecision);
        }

        private Vector3 CalculateOffsetPosition(Vector3 center, float angleDegrees, float radius)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radius;
        }

        private void BuildScanTargets()
        {
            _scanTargetPositions.Clear();
            for (int i = 0; i < _totalSweeps; i++)
            {
                float angle = (360f / _totalSweeps) * i;
                _scanTargetPositions.Add(CalculateOffsetPosition(_targetPosition, angle, 2f));
            }
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (value != null)
                {
                    for (int i = 0; i < value.Length; i++)
                        hash = (hash ^ value[i]) * 16777619u;
                }
                return hash;
            }
        }

        private Vector3 BuildDeterministicTarget(Vector3 origin,
            string sessionId, string guardEid, string entryId, ulong factId,
            int sampleIndex)
        {
            uint seed = StableHash(sessionId) ^ StableHash(guardEid)
                ^ StableHash(entryId) ^ (uint)factId ^ (uint)(factId >> 32)
                ^ (uint)sampleIndex * 2654435761u;
            _targetSeed = seed;
            _targetSampleIndex = sampleIndex;
            seed = seed * 1664525u + 1013904223u;
            float u1 = (seed & 0x00ffffffu) / 16777216f;
            seed = seed * 1664525u + 1013904223u;
            float u2 = (seed & 0x00ffffffu) / 16777216f;
            float radius = InvestigateErrorRadiusMeters * Mathf.Sqrt(u1);
            float angle = 2f * Mathf.PI * u2;
            return origin + new Vector3(radius * Mathf.Cos(angle), 0f,
                radius * Mathf.Sin(angle));
        }

        public override void OnUpdate(GuardFSM fsm, long tick, float delta)
        {
            if (_resolved) return;

            if (_isMovingToTarget)
            {
                bool armed = false;
                if (fsm.TryGetComponent<GuardNavigator>(out var nav))
                {
                    // Arrival has precedence over path-end when both observations
                    // are true on one boundary.
                    if (nav.HasReachedArrival(_targetPosition,
                        NoiseRuntimeConfiguration.RegisteredInvestigateArrivalToleranceMeters))
                    {
                        ArmSearch("arrived");
                        armed = true;
                    }
                    else if (nav.HasPathEndObservation(
                        NoiseRuntimeConfiguration.RegisteredInvestigatePathEndToleranceMeters,
                        NoiseRuntimeConfiguration.RegisteredInvestigatePathEndSpeedMetersPerSecond))
                    {
                        _pathEndObservationTicks++;
                        if (_pathEndObservationTicks >= NoiseRuntimeConfiguration
                            .RegisteredInvestigatePathEndTicks)
                        {
                            ArmSearch("path-end");
                            armed = true;
                        }
                    }
                    else
                    {
                        _pathEndObservationTicks = 0;
                    }
                }

                if (!armed)
                {
                    _pathEndSuppressionTimer -= Mathf.Max(0f, delta);
                    if (_pathEndSuppressionTimer <= 0f)
                        ArmSearch("force-arm");
                }
            }

            // Witnessed-entry Investigate episodes hold the spot front instead of
            // searching. Their catch target is the authored interior datum, and
            // occupancy is an explicit HideSpot fact rather than an LOS inference.
            if (_spotHoldActive)
            {
                UpdateWitnessedHold(fsm, delta);
                return;
            }

            // Travel never consumes the search budget. A missing navigation
            // component remains fail-closed rather than silently starting the timer.
            if (!_searchArmed) return;

            if (!_isMovingToTarget)
            {
                _sweepTimer -= delta;
                if (_sweepTimer <= 0f)
                {
                    if (_currentSweep >= _totalSweeps)
                    {
                        Resolution(fsm, "fruitless");
                        return;
                    }
                    PerformScan();
                    _currentSweep++;
                    _sweepTimer = SweepBaseSeconds;
                }
            }

            // LOS and reachability are Perception-owned facts, received in
            // OnHandleEvent. A live but unreachable target still consumes the
            // remaining search budget; only a current reachable sighting pauses it.
            if (!_hasLOS || (_hasReachabilityVerdict && !_isReachable))
            {
                _giveupTimer -= delta;
                if (_giveupTimer <= 0f)
                {
                    Resolution(fsm, "fruitless");
                    return;
                }
            }
        }

        private void ArmSearch(string arm)
        {
            if (!_isMovingToTarget) return;
            _isMovingToTarget = false;
            _searchArmed = true;
            _startArm = arm ?? "unknown";
            _sweepTimer = SweepBaseSeconds;
            _spotVerifyTimer = SpotFrontVerifySeconds;
            _catchTimer = CatchSeconds;
            Debug.Log("[InvestigateState] Search armed: " + _startArm);
        }

        private void UpdateWitnessedHold(GuardFSM fsm, float delta)
        {
            if (!_searchArmed || !_spotOccupancyKnown) return;
            if (!_spotOccupied)
            {
                Resolution(fsm, "fruitless");
                return;
            }

            bool canCatch = EvaluateCatchContract(fsm);
            if (canCatch)
            {
                if (!_catchTimerLive)
                    _catchTimerLive = true;
                else
                    _catchTimer -= delta;
            }

            _spotVerifyTimer -= delta;
            if (_spotVerifyTimer <= 0f && canCatch && _catchTimerLive
                && _catchTimer <= 0f)
            {
                _terminalCause = "capture";
                fsm.PublishDecision(new Capture
                {
                    EntryId = _entryId,
                    Position = _interiorPosition,
                    Victim = null,
                    IsCarveOut = true
                });
                _resolved = true;
                fsm.SetGoalMode(GuardGoalMode.None);
                fsm.ArmNoiseRecommitCooldown();
                fsm.TransitionTo(fsm.GetPatrolState());
            }
        }

        private bool EvaluateCatchContract(GuardFSM fsm)
        {
            if (_physicsProfile == null || _agent == null
                || fsm.CurrentState != this
                || fsm.currentGoalMode != GuardGoalMode.HideSpotFront)
                return false;

            NavMeshPath path = new NavMeshPath();
            NavMeshHit hit;
            int areaMask = _agent.areaMask;
            if (areaMask == 0 || !NavMesh.SamplePosition(_interiorPosition,
                out hit, NavMeshSampleMaxDistanceMeters, areaMask))
                return false;
            Vector3 sampledInteriorPosition = hit.position;
            _agent.CalculatePath(sampledInteriorPosition, path);
            if (path.status == NavMeshPathStatus.PathInvalid) return false;

            float pathLength = GetPathLength(path);
            if (path.status == NavMeshPathStatus.PathPartial)
            {
                if (path.corners == null || path.corners.Length == 0)
                    return false;
                Vector3 lastCorner = path.corners[path.corners.Length - 1];
                if (!_physicsProfile.Linecast(lastCorner,
                    sampledInteriorPosition).Clear)
                    return false;
            }

            if (pathLength > CatchRangeMeters + HysteresisMeters) return false;
            Vector3 delta = sampledInteriorPosition - fsm.transform.position;
            float xzDistance = new Vector2(delta.x, delta.z).magnitude;
            return xzDistance <= CatchRangeMeters + HysteresisMeters
                && Mathf.Abs(delta.y) <= DeltaYToleranceMeters
                && _physicsProfile.Linecast(fsm.transform.position,
                    sampledInteriorPosition).Clear;
        }

        private float GetPathLength(NavMeshPath path)
        {
            float length = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++)
                length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            return length;
        }

        private void PerformScan()
        {
            if (_currentScan >= _scanTargetPositions.Count) return;
            Debug.Log($"[InvestigateState] Performing scan #{_currentScan + 1}/{_totalSweeps}");
            _currentScan++;
        }

        public override void OnHandleEvent(GuardFSM fsm, IEvent evt)
        {
            if (evt is HideSpotOccupancy occupancy && _spotHoldActive
                && (string.IsNullOrEmpty(occupancy.EntryId)
                    || string.Equals(occupancy.EntryId, _entryId,
                        StringComparison.Ordinal)))
            {
                _spotOccupancyKnown = true;
                _spotOccupied = occupancy.IsOccupied;
                if (!occupancy.IsOccupied && _searchArmed)
                    Resolution(fsm, "fruitless");
                return;
            }

            if (evt is LOSBreak hideBreak && hideBreak.IsHideEntry)
            {
                if (!string.IsNullOrEmpty(hideBreak.GuardEid)
                    && !string.Equals(hideBreak.GuardEid, fsm.GuardEid,
                        StringComparison.Ordinal))
                    return;

                if (hideBreak.APreBreak >= hideBreak.ThresholdApplied)
                {
                    // A witnessed hide-entry break promotes the existing
                    // Investigate episode in place. The episode identity and its
                    // authoritative target remain FSM-owned; the authored spot
                    // only supplies Chase's entry/hold geometry.
                    string promotionEntryId = _entryId;
                    Vector3 promotionPosition = _targetPosition;
                    var chaseState = fsm.GetChaseState();
                    chaseState.InitPromotion(promotionEntryId, "hide-entry",
                        hideBreak.SpotPosition, promotionPosition, true,
                        hideBreak.HasSpotFrontPosition
                            ? hideBreak.SpotFrontPosition : (Vector3?)null);
                    fsm.PrepareLivenessPromotion(promotionEntryId);
                    fsm.TransitionTo(chaseState);
                }
                else
                {
                    // A sub-threshold retarget continues the live episode: the
                    // Perception-owned identity must survive the authored spot
                    // change; only the geometry is retargeted.
                    RetargetHideEntry(fsm, _entryId,
                        hideBreak.SpotPosition, hideBreak.ResidualR,
                        hideBreak.HasSpotFrontPosition
                            ? hideBreak.SpotFrontPosition : (Vector3?)null);
                }
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

                if (_corroborationCount < 3
                    && IsQualifyingCorroboration(relay))
                {
                    if (fsm.NoiseResponseProfile == NoiseResponseProfile.MVP)
                    {
                        Reanchor(relay, fsm);
                        fsm.PublishRelayOutcome(relay, RelayConsumption.Consumed);
                    }
                    else
                    {
                        // Alert Propagation owns Target peer/zone capability. Until
                        // that service is injected, consume as a no-op rather than
                        // silently performing MVP behavior.
                        fsm.PublishRelayOutcome(relay, RelayConsumption.Ignored);
                    }
                }
                else
                {
                    fsm.PublishRelayOutcome(relay, RelayConsumption.Ignored);
                }
                return;
            }

            if (evt is Reachability reachability)
            {
                if ((!string.IsNullOrEmpty(reachability.GuardEid)
                        && !string.Equals(reachability.GuardEid, fsm.GuardEid,
                            StringComparison.Ordinal))
                    || (!string.IsNullOrEmpty(reachability.EntryId)
                        && !string.Equals(reachability.EntryId, _entryId,
                            StringComparison.Ordinal)))
                    return;

                _hasReachabilityVerdict = true;
                _isReachable = reachability.IsReachable;
                return;
            }

            if (evt is LOSGain losGain)
            {
                if (string.IsNullOrEmpty(losGain.GuardEid)
                    || string.Equals(losGain.GuardEid, fsm.GuardEid, StringComparison.Ordinal))
                {
                    _hasLOS = true;
                    if (_spotHoldActive)
                    {
                        // Sight-presence abandons the spot hold but preserves
                        // witnessed authority for a later re-engagement.
                        _spotHoldActive = false;
                        fsm.SetGoalMode(GuardGoalMode.LivePursuit);
                    }
                }
                return;
            }

            if (evt is LOSBreak losBreak)
            {
                if (string.IsNullOrEmpty(losBreak.GuardEid)
                    || string.Equals(losBreak.GuardEid, fsm.GuardEid, StringComparison.Ordinal))
                    _hasLOS = false;
                return;
            }

            if (evt is ChaseReached chaseEvt)
            {
                // ChaseReached can carry a conflicting adapter identity; the
                // live Investigate episode remains authoritative for promotion.
                string promotionEntryId = _entryId;
                var chaseState = fsm.GetChaseState();
                chaseState.InitPromotion(promotionEntryId, "threshold",
                    chaseEvt.Position, _targetPosition);
                fsm.PrepareLivenessPromotion(promotionEntryId);
                fsm.TransitionTo(chaseState);
            }
        }

        private bool IsQualifyingCorroboration(NoiseHeardRelay relay)
        {
            float dt = relay.TPublish - _episodeAnchorTimestamp;
            float distance = Vector2.Distance(
                new Vector2(relay.AuthoritativeOrigin.x, relay.AuthoritativeOrigin.z),
                new Vector2(_episodeAnchorPosition.x, _episodeAnchorPosition.z));
            return relay.FactId != _episodeAnchorFactId
                && dt >= 0f && dt <= GetCorroborateWindow()
                && distance <= GetCorroborateRadius();
        }

        private void Reanchor(NoiseHeardRelay relay, GuardFSM fsm)
        {
            if (_corroborationCount >= 3) return;
            _corroborationCount++;
            // Preserve the first raw source anchor for every later qualification;
            // only the current search target and residual budget are re-anchored.
            _residualAtAnchor = Mathf.Clamp01(relay.ResidualAtHearing);
            _targetPosition = relay.AuthoritativeOrigin;
            float residualMax = Mathf.Max(0.0001f, GetResidualMax());
            float coefficient = GetThoroughnessCoefficient();
            float giveupBase = GetGiveupBase();
            _thoroughness = 1f + coefficient * (_residualAtAnchor / residualMax);
            _totalSweeps = Mathf.Max(1, Mathf.CeilToInt(SweepReferenceCount * _thoroughness));
            // Re-anchor uses the authored difficulty ratio exactly once; the
            // additive extension is applied after the residual-scaled base.
            _giveupTimer = giveupBase * _difficultyScalar
                * (1f + coefficient * (_residualAtAnchor / residualMax))
                + GetReanchorExtend();
            _targetSeed = 0u;
            _targetSampleIndex = 0;
            _targetResolution = "reanchor-raw";
            _isMovingToTarget = true;
            _searchArmed = false;
            _pathEndSuppressionTimer = NoiseRuntimeConfiguration
                .RegisteredInvestigatePathEndSuppressMaxSeconds;
            _pathEndObservationTicks = 0;
            _startArm = "not-started";
            _currentSweep = 0;
            _currentScan = 0;
            BuildScanTargets();
            if (fsm.TryGetComponent<GuardNavigator>(out var nav))
                nav.MoveTo(_targetPosition);
            fsm.PublishDecision(new NoiseReanchor
            {
                EntryId = _entryId,
                CorroboratingFactId = relay.FactId,
                Position = relay.AuthoritativeOrigin,
                TPublish = relay.TPublish,
                SourceKind = relay.Kind,
                SourceEventId = relay.SourceEventId,
                TerminalPublicationTime = relay.SourceEventClassRank
                    == (int)NoiseSourceKind.Burst
                    ? relay.TerminalPublicationTime : (float?)null,
                ResidualAtHearing = relay.ResidualAtHearing,
                ReanchorExtensionSeconds = GetReanchorExtend(),
                EpisodeAnchorKind = _cause,
                EpisodeAnchorPosition = _episodeAnchorPosition,
                EpisodeAnchorTPublish = _episodeAnchorTimestamp,
                EpisodeAnchorFactId = _cause == "noise"
                    ? _episodeAnchorFactId : 0UL,
                EpisodeAnchorIdentity = _cause == "noise"
                    ? string.Empty : _entryId,
                Timestamp = relay.TPublish
            });
            Debug.Log("[InvestigateState] Re-anchored to corroborating noise");
        }

        public override void OnExit(GuardFSM fsm)
        {
            Debug.Log("[InvestigateState] Exiting state");
            if (!fsm.IsBoundaryResetting && !fsm.IsLivenessPromotionPending(_entryId)
                && !string.IsNullOrWhiteSpace(_entryId))
            {
                fsm.PublishLivenessFact("close", "Investigate",
                    _terminalCause ?? "investigate-resolution",
                    fsm.CurrentVirtualTime, _entryId,
                    _targetPosition, LivenessPositionSources.TerminalDecision);
            }
            fsm.SetGoalMode(GuardGoalMode.None);
        }

        private void Resolution(GuardFSM fsm, string cause)
        {
            if (_resolved) return;
            _resolved = true;
            _terminalCause = cause;
            Debug.Log($"[InvestigateState] Resolution with cause: {cause}");
            fsm.PublishDecision(new InvestigateResolution
            {
                EntryId = _entryId,
                Cause = cause,
                Position = _targetPosition
            });
            fsm.ArmNoiseRecommitCooldown();
            fsm.TransitionTo(fsm.GetPatrolState());
        }
    }
}
