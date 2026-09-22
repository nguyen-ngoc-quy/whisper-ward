using System;
using UnityEngine;
using UnityEngine.AI;

namespace WhisperWard.Core.Contracts
{
    /// <summary>
    /// Alertness and operational state determining guard navigation and avoidance priority.
    /// Governed by GDD #12 and ADR-0007.
    /// </summary>
    public enum GuardNavigationState
    {
        /// <summary>
        /// Actively pursuing the player. Priority = 10 (Highest; lower priority entities yield).
        /// </summary>
        Chase = 10,

        /// <summary>
        /// Investigating a sound or visual clue. Priority = 30.
        /// </summary>
        Investigate = 30,

        /// <summary>
        /// Routine patrol or idle standing. Priority = 50 (Yields to alerted guards).
        /// </summary>
        Patrol = 50
    }

    /// <summary>
    /// Authoritative contract for reciprocal velocity avoidance, query staggering,
    /// off-mesh recovery warping, and HideSpot approach standoff routing.
    /// Governed by ADR-0007, GDD #12, and control-manifest.md.
    /// </summary>
    public interface INavMeshAvoidanceService
    {
        /// <summary>
        /// Configures a NavMeshAgent for high-quality RVO avoidance with state-driven priority.
        /// Sets obstacleAvoidanceType to HighQualityObstacleAvoidance, radius to 0.40m, and priority.
        /// </summary>
        /// <param name="agent">Target guard NavMeshAgent.</param>
        /// <param name="state">Current navigation state determining avoidance priority.</param>
        void ConfigureRVO(NavMeshAgent agent, GuardNavigationState state);

        /// <summary>
        /// Resolves the integer avoidance priority for a given navigation state.
        /// </summary>
        /// <param name="state">Guard navigation state.</param>
        /// <returns>10 for Chase, 30 for Investigate, 50 for Patrol.</returns>
        int GetAvoidancePriority(GuardNavigationState state);

        /// <summary>
        /// Checks if an agent has fallen off the NavMesh and warps them back to the nearest walkable surface within range.
        /// Satisfies AC-NAV-13 and GDD #12 Section D5.
        /// </summary>
        /// <param name="agent">NavMeshAgent to evaluate.</param>
        /// <param name="searchRadius">Maximum surface search tolerance in meters (default 1.0m).</param>
        /// <returns>True if agent was already valid or successfully warped back; false if unrecoverable.</returns>
        bool TryRecoverOffMesh(NavMeshAgent agent, float searchRadius = 1.0f);

        /// <summary>
        /// Computes the standoff guard_hold anchor for approaching an occupied HideSpot.
        /// Formula: spot_front_anchor + (r_guard * hold_vector).
        /// Satisfies AC-NAV-14 and GDD #5.
        /// </summary>
        /// <param name="spotFrontAnchor">Front entry anchor coordinate of the HideSpot.</param>
        /// <param name="holdVector">Normalized outward direction vector from HideSpot entrance.</param>
        /// <param name="guardRadius">Physical radius of the navigating guard (default 0.40m).</param>
        /// <returns>Authoritative standoff destination coordinate.</returns>
        Vector3 CalculateHideSpotHoldAnchor(Vector3 spotFrontAnchor, Vector3 holdVector, float guardRadius = 0.40f);

        /// <summary>
        /// Enqueues a path calculation request into the time-slicing scheduler.
        /// Ensures at most 1 full CalculatePath query runs per frame tick across all chasing guards.
        /// Satisfies AC-NAV-17 and ADR-0007.
        /// </summary>
        /// <param name="agent">Requesting guard agent.</param>
        /// <param name="targetPosition">Target world coordinates.</param>
        /// <param name="onComplete">Callback invoked with query outcome when processed.</param>
        void EnqueueQuery(NavMeshAgent agent, Vector3 targetPosition, Action<PathQueryResult> onComplete);

        /// <summary>
        /// Ticks the round-robin query scheduler, processing up to maxQueriesPerTick (default 1).
        /// </summary>
        /// <param name="maxQueriesPerTick">Maximum queries allowed in this frame tick (default 1).</param>
        /// <returns>Number of queries dispatched during this tick.</returns>
        int TickScheduler(int maxQueriesPerTick = 1);

        /// <summary>
        /// Current number of queries pending in the scheduler queue.
        /// </summary>
        int PendingQueryCount { get; }
    }
}
