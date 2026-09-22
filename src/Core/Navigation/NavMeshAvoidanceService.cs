using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Core.Navigation
{
    /// <summary>
    /// Delegate for simulating NavMeshAgent warp operations in headless tests.
    /// </summary>
    public delegate bool AgentWarpDelegate(NavMeshAgent agent, Vector3 destination);

    /// <summary>
    /// Internal queue item for the time-sliced query scheduler.
    /// </summary>
    internal readonly struct ScheduledQuery
    {
        public readonly NavMeshAgent Agent;
        public readonly Vector3 TargetPosition;
        public readonly Action<PathQueryResult> OnComplete;

        public ScheduledQuery(NavMeshAgent agent, Vector3 targetPosition, Action<PathQueryResult> onComplete)
        {
            Agent = agent;
            TargetPosition = targetPosition;
            OnComplete = onComplete;
        }
    }

    /// <summary>
    /// Authoritative service implementing INavMeshAvoidanceService.
    /// Manages RVO avoidance configuration, priority escalation, off-mesh warp recovery,
    /// HideSpot approach standoff routing, and round-robin query time-slicing.
    /// Governed by ADR-0007, GDD #12, and control-manifest.md.
    /// </summary>
    public sealed class NavMeshAvoidanceService : INavMeshAvoidanceService
    {
        public const float DefaultGuardRadius = 0.40f;
        public const float DefaultAvoidanceRadius = 0.45f;
        public const float DefaultOffMeshSearchRadius = 1.0f;
        public const int DefaultMaxQueriesPerTick = 1;

        private readonly INavMeshQueryService _queryService;
        private readonly Queue<ScheduledQuery> _queryQueue;
        private SamplePositionDelegate _samplePositionOverride;
        private AgentWarpDelegate _warpOverride;

        public int PendingQueryCount => _queryQueue.Count;

        /// <summary>
        /// Initializes a new instance of NavMeshAvoidanceService.
        /// </summary>
        /// <param name="queryService">Authoritative NavMeshQueryService for executing scheduled queries.</param>
        public NavMeshAvoidanceService(INavMeshQueryService queryService)
        {
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
            _queryQueue = new Queue<ScheduledQuery>(16);
        }

        /// <summary>
        /// Sets delegate overrides for spatial queries and agent actions to support headless testing.
        /// </summary>
        public void SetTestHooks(SamplePositionDelegate sampleDelegate, AgentWarpDelegate warpDelegate)
        {
            _samplePositionOverride = sampleDelegate;
            _warpOverride = warpDelegate;
        }

        /// <summary>
        /// Configures a NavMeshAgent for high-quality RVO avoidance with state-driven priority.
        /// Satisfies AC-NAV-12 and ADR-0007.
        /// </summary>
        public void ConfigureRVO(NavMeshAgent agent, GuardNavigationState state)
        {
            if (agent == null) return;

            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.radius = DefaultGuardRadius;
            agent.avoidancePriority = GetAvoidancePriority(state);
        }

        /// <summary>
        /// Resolves the integer avoidance priority for a given navigation state.
        /// Chase = 10 (Highest), Investigate = 30, Patrol = 50 (Lowest).
        /// </summary>
        public int GetAvoidancePriority(GuardNavigationState state)
        {
            return state switch
            {
                GuardNavigationState.Chase => 10,
                GuardNavigationState.Investigate => 30,
                GuardNavigationState.Patrol => 50,
                _ => 50
            };
        }

        /// <summary>
        /// Recovers an agent that has slipped off the NavMesh by warping to the nearest walkable surface within range.
        /// Satisfies AC-NAV-13 and GDD #12 Section D5.
        /// </summary>
        public bool TryRecoverOffMesh(NavMeshAgent agent, float searchRadius = DefaultOffMeshSearchRadius)
        {
            if (agent == null) return false;

            if (agent.isOnNavMesh)
            {
                return true;
            }

            Vector3 agentPos = agent.transform.position;
            bool hitValid;
            Vector3 targetSurface;

            if (_samplePositionOverride != null)
            {
                hitValid = _samplePositionOverride(agentPos, out targetSurface, searchRadius, NavMesh.AllAreas);
            }
            else
            {
                hitValid = NavMesh.SamplePosition(agentPos, out NavMeshHit hit, searchRadius, NavMesh.AllAreas);
                targetSurface = hit.position;
            }

            if (!hitValid)
            {
                Debug.LogError($"[NavMeshAvoidanceService] ERR_GUARD_FALLEN_OFF_NAVMESH: Guard {agent.name} at {agentPos} unrecoverable within {searchRadius}m.");
                return false;
            }

            if (_warpOverride != null)
            {
                return _warpOverride(agent, targetSurface);
            }

            return agent.Warp(targetSurface);
        }

        /// <summary>
        /// Computes the standoff guard_hold anchor for approaching an occupied HideSpot.
        /// Formula: spot_front_anchor + (r_guard * hold_vector).
        /// Satisfies AC-NAV-14 and GDD #5.
        /// </summary>
        public Vector3 CalculateHideSpotHoldAnchor(Vector3 spotFrontAnchor, Vector3 holdVector, float guardRadius = DefaultGuardRadius)
        {
            Vector3 normalizedHold = holdVector.sqrMagnitude > 0.001f ? holdVector.normalized : Vector3.forward;
            return spotFrontAnchor + (normalizedHold * guardRadius);
        }

        /// <summary>
        /// Enqueues a path calculation request into the time-slicing scheduler.
        /// Satisfies AC-NAV-17.
        /// </summary>
        public void EnqueueQuery(NavMeshAgent agent, Vector3 targetPosition, Action<PathQueryResult> onComplete)
        {
            if (agent == null) return;
            _queryQueue.Enqueue(new ScheduledQuery(agent, targetPosition, onComplete));
        }

        /// <summary>
        /// Ticks the round-robin query scheduler, processing up to maxQueriesPerTick (default 1).
        /// Satisfies AC-NAV-17 and ensures burst cost < 1.0ms.
        /// </summary>
        public int TickScheduler(int maxQueriesPerTick = DefaultMaxQueriesPerTick)
        {
            int dispatched = 0;
            while (_queryQueue.Count > 0 && dispatched < maxQueriesPerTick)
            {
                ScheduledQuery query = _queryQueue.Dequeue();
                if (query.Agent != null)
                {
                    PathQueryResult result = _queryService.CalculatePathNonAlloc(query.Agent, query.TargetPosition);
                    query.OnComplete?.Invoke(result);
                    dispatched++;
                }
            }

            return dispatched;
        }
    }
}
