using UnityEngine;
using UnityEngine.AI;

namespace WhisperWard.Core.Contracts
{
    /// <summary>
    /// Status classifications for NavMesh path queries.
    /// </summary>
    public enum PathQueryResultStatus
    {
        /// <summary>
        /// Query failed or zero path segments could be generated.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// An unobstructed, continuous path exists to the destination.
        /// </summary>
        Complete = 1,

        /// <summary>
        /// Path terminates prematurely at an intermediate corner due to an obstruction.
        /// </summary>
        Partial = 2
    }

    /// <summary>
    /// Discrete arrival state classification for NavMesh navigation.
    /// Governed by GDD #12 Section D3 and AC-NAV-05.
    /// </summary>
    public enum ArrivalState
    {
        /// <summary>
        /// Entity is actively traversing along the path towards the destination.
        /// </summary>
        Traversing = 0,

        /// <summary>
        /// Entity has reached within arrival tolerance of the authoritative target coordinate.
        /// Strictly overrides PathEnd when both evaluate true.
        /// </summary>
        Arrived = 1,

        /// <summary>
        /// Path is Partial (obstructed) and entity has arrived at the terminal corner.
        /// </summary>
        PathEnd = 2
    }

    /// <summary>
    /// Immutable, zero-allocation result container for non-allocating NavMesh path queries.
    /// </summary>
    public readonly struct PathQueryResult
    {
        /// <summary>
        /// Overall outcome status of the query.
        /// </summary>
        public readonly PathQueryResultStatus Status;

        /// <summary>
        /// Piecewise 3D Euclidean polyline distance along all path segments.
        /// Evaluates to positive infinity if status is Invalid or corner count is less than 2.
        /// </summary>
        public readonly float CumulativeDistance;

        /// <summary>
        /// Number of valid corners extracted into the non-allocating buffer.
        /// </summary>
        public readonly int CornerCount;

        /// <summary>
        /// World coordinates of the final traversable corner on the calculated path.
        /// </summary>
        public readonly Vector3 TerminalCorner;

        /// <summary>
        /// True if a complete path was calculated to the destination.
        /// </summary>
        public readonly bool IsReachable;

        /// <summary>
        /// Initializes a new path query result snapshot.
        /// </summary>
        public PathQueryResult(
            PathQueryResultStatus status,
            float cumulativeDistance,
            int cornerCount,
            Vector3 terminalCorner,
            bool isReachable)
        {
            Status = status;
            CumulativeDistance = cumulativeDistance;
            CornerCount = cornerCount;
            TerminalCorner = terminalCorner;
            IsReachable = isReachable;
        }
    }

    /// <summary>
    /// Authoritative contract for zero-allocation NavMesh path queries and spatial metrics.
    /// Governed by ADR-0007.
    /// </summary>
    public interface INavMeshQueryService
    {
        /// <summary>
        /// Samples the nearest walkable NavMesh surface point within a tight tolerance.
        /// Rejects coordinates farther than maxDistance (default 0.40m) or occluded by thin walls.
        /// </summary>
        /// <param name="sourcePosition">Arbitrary world coordinate.</param>
        /// <param name="snappedPosition">Closest point projected onto the walkable mesh.</param>
        /// <param name="maxDistance">Maximum sampling search radius in meters.</param>
        /// <returns>True if a valid walkable surface was found within range and unoccluded.</returns>
        bool TrySamplePosition(Vector3 sourcePosition, out Vector3 snappedPosition, float maxDistance = 0.40f);

        /// <summary>
        /// Calculates an agent-parameterized path toward the target using pre-allocated internal buffers.
        /// Guarantees 0 bytes of managed heap allocation.
        /// </summary>
        /// <param name="agent">Active NavMeshAgent providing physical radius and step parameters.</param>
        /// <param name="targetPosition">Destination world coordinates.</param>
        /// <returns>Immutable result containing status, polyline distance, and corner count.</returns>
        PathQueryResult CalculatePathNonAlloc(NavMeshAgent agent, Vector3 targetPosition);

        /// <summary>
        /// Computes the piecewise-linear 3D Euclidean distance along an existing calculated path polyline.
        /// </summary>
        /// <param name="path">NavMeshPath to measure.</param>
        /// <returns>Sum of segment lengths in meters, or positive infinity if invalid.</returns>
        float CalculateCumulativeDistanceNonAlloc(NavMeshPath path);

        /// <summary>
        /// Evaluates the horizontal planar arrival predicate against a target coordinate.
        /// </summary>
        /// <param name="currentPosition">Current world position of the entity.</param>
        /// <param name="targetPosition">Target world coordinates.</param>
        /// <param name="arriveThreshold">Arrival tolerance in meters (default 0.30m).</param>
        /// <returns>True if horizontal distance is less than or equal to arriveThreshold.</returns>
        bool IsArrived(Vector3 currentPosition, Vector3 targetPosition, float arriveThreshold = 0.30f);

        /// <summary>
        /// Evaluates arrival states with mutually exclusive precedence (Arrived strictly overrides PathEnd).
        /// Satisfies AC-NAV-05 and GDD #12 Section D3.
        /// </summary>
        /// <param name="guardPosition">Current position of the navigating guard.</param>
        /// <param name="targetPosition">Goal destination coordinates.</param>
        /// <param name="queryResult">Active path query result.</param>
        /// <param name="arriveThreshold">Arrival threshold in meters (default 0.30m).</param>
        /// <returns>Arrived, PathEnd, or Traversing.</returns>
        ArrivalState EvaluateArrival(Vector3 guardPosition, Vector3 targetPosition, PathQueryResult queryResult, float arriveThreshold = 0.30f);

        /// <summary>
        /// Evaluates catch-gate threshold and hysteresis state.
        /// Satisfies AC-NAV-03, AC-NAV-04, and GDD #12 Section D2.
        /// </summary>
        /// <param name="currentGateState">Gate state on the previous simulation tick.</param>
        /// <param name="pathStatus">Status of the active path query.</param>
        /// <param name="cumulativeDistance">Current cumulative path length in meters.</param>
        /// <param name="catchRange">Initial catch range threshold (default 5.50m).</param>
        /// <param name="hysteresis">Hysteresis retention buffer (default 0.50m).</param>
        /// <returns>True if catch gate is engaged/active; false otherwise.</returns>
        bool EvaluateCatchGate(bool currentGateState, PathQueryResultStatus pathStatus, float cumulativeDistance, float catchRange = 5.50f, float hysteresis = 0.50f);

        /// <summary>
        /// Evaluates the chase re-pathing dual-trigger predicate with rate floor clamp.
        /// Satisfies AC-NAV-06, AC-NAV-16, and GDD #12 Section D4.
        /// </summary>
        /// <param name="timeSinceLastPath">Time elapsed since last path calculation in seconds.</param>
        /// <param name="currentPlayerPos">Current world position of player feet.</param>
        /// <param name="lastSampledPlayerPos">Player position recorded when last path was calculated.</param>
        /// <param name="minInterval">Clamped rate floor interval in seconds (default 0.10s, 10Hz).</param>
        /// <param name="cadencePeriod">Periodic sensing tick interval in seconds (default 0.25s).</param>
        /// <param name="displacementTrigger">Player displacement distance threshold in meters (default 0.50m).</param>
        /// <returns>True if path recalculation should be triggered; false otherwise.</returns>
        bool ShouldRepathChase(float timeSinceLastPath, Vector3 currentPlayerPos, Vector3 lastSampledPlayerPos, float minInterval = 0.10f, float cadencePeriod = 0.25f, float displacementTrigger = 0.50f);
    }
}
