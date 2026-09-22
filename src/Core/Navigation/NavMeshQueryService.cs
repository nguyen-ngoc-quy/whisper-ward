using System;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Core.Navigation
{
    /// <summary>
    /// Delegate for NavMesh sampling operations, enabling headless test injection.
    /// </summary>
    public delegate bool SamplePositionDelegate(Vector3 sourcePosition, out Vector3 hitPosition, float maxDistance, int areaMask);

    /// <summary>
    /// Delegate for physics linecast queries, enabling headless test injection.
    /// </summary>
    public delegate bool LinecastDelegate(Vector3 start, Vector3 end, LayerMask layerMask, QueryTriggerInteraction queryTriggerInteraction);

    /// <summary>
    /// Authoritative NavMesh query service implementing INavMeshQueryService.
    /// Manages zero-allocation path queries, cumulative piecewise 3D polyline distance calculations,
    /// surface-snapping with thin-wall linecast verification, arrival precedence, and catch-gate hysteresis.
    /// Governed by ADR-0007, GDD #12, and control-manifest.md.
    /// </summary>
    public sealed class NavMeshQueryService : INavMeshQueryService
    {
        public const float DefaultSampleMaxDistance = 0.40f;
        public const float DefaultArriveThreshold = 0.30f;
        public const float DefaultCatchRange = 5.50f;
        public const float DefaultCatchHysteresis = 0.50f;
        public const float DefaultRepathMinInterval = 0.10f;
        public const float DefaultRepathCadencePeriod = 0.25f;
        public const float DefaultRepathDisplacementTrigger = 0.50f;
        public const int DefaultCornerBufferCapacity = 64;

        private readonly NavMeshPath _sharedPath;
        private readonly Vector3[] _cornerBuffer;
        private readonly LayerMask _worldLayerMask;
        private SamplePositionDelegate _samplePositionOverride;
        private LinecastDelegate _linecastOverride;

        /// <summary>
        /// Gets the internal pre-allocated corner buffer capacity.
        /// </summary>
        public int CornerBufferCapacity => _cornerBuffer.Length;

        /// <summary>
        /// Initializes a new instance of the NavMeshQueryService.
        /// </summary>
        /// <param name="worldLayerMask">Layer mask for static world collision geometry (defaults to layer 20: World).</param>
        /// <param name="cornerBufferCapacity">Maximum corners to pre-allocate (default 64).</param>
        public NavMeshQueryService(LayerMask? worldLayerMask = null, int cornerBufferCapacity = DefaultCornerBufferCapacity)
        {
            _sharedPath = new NavMeshPath();
            _cornerBuffer = new Vector3[Mathf.Max(16, cornerBufferCapacity)];
            _worldLayerMask = worldLayerMask ?? (1 << 20); // Default Layer 20: World per ADR-0002
        }

        /// <summary>
        /// Sets delegate overrides for spatial queries to support headless test mocking.
        /// </summary>
        /// <param name="sampleDelegate">Custom sampling delegate.</param>
        /// <param name="linecastDelegate">Custom linecast delegate.</param>
        public void SetTestHooks(SamplePositionDelegate sampleDelegate, LinecastDelegate linecastDelegate)
        {
            _samplePositionOverride = sampleDelegate;
            _linecastOverride = linecastDelegate;
        }

        /// <summary>
        /// Samples the nearest walkable NavMesh surface point within maxDistance (default 0.40m).
        /// Verifies thin-wall linecast occlusion to prevent sampling across partitions.
        /// Satisfies AC-NAV-09, AC-NAV-10, and ADR-0007.
        /// </summary>
        public bool TrySamplePosition(Vector3 sourcePosition, out Vector3 snappedPosition, float maxDistance = DefaultSampleMaxDistance)
        {
            bool hitValid;
            Vector3 candidateHit;

            if (_samplePositionOverride != null)
            {
                hitValid = _samplePositionOverride(sourcePosition, out candidateHit, maxDistance, NavMesh.AllAreas);
            }
            else
            {
                hitValid = NavMesh.SamplePosition(sourcePosition, out NavMeshHit hit, maxDistance, NavMesh.AllAreas);
                candidateHit = hit.position;
            }

            if (!hitValid)
            {
                snappedPosition = Vector3.zero;
                return false;
            }

            // AC-NAV-09: Thin-wall linecast verification
            bool isOccluded;
            if (_linecastOverride != null)
            {
                isOccluded = _linecastOverride(sourcePosition, candidateHit, _worldLayerMask, QueryTriggerInteraction.Ignore);
            }
            else
            {
                isOccluded = Physics.Linecast(sourcePosition, candidateHit, _worldLayerMask, QueryTriggerInteraction.Ignore);
            }

            if (isOccluded)
            {
                snappedPosition = Vector3.zero;
                return false; // Intervening wall detected; reject sample
            }

            snappedPosition = candidateHit;
            return true;
        }

        /// <summary>
        /// Calculates an agent-parameterized path toward the target using pre-allocated internal buffers.
        /// Guarantees 0 bytes of managed heap allocation.
        /// Satisfies TR-CORE-005, AC-NAV-11, and AC-NAV-15.
        /// </summary>
        public PathQueryResult CalculatePathNonAlloc(NavMeshAgent agent, Vector3 targetPosition)
        {
            if (agent == null || !agent.isOnNavMesh)
            {
                return new PathQueryResult(PathQueryResultStatus.Invalid, float.PositiveInfinity, 0, targetPosition, false);
            }

            // Sample destination position within 0.40m tolerance
            if (!TrySamplePosition(targetPosition, out Vector3 snappedTarget, DefaultSampleMaxDistance))
            {
                return new PathQueryResult(PathQueryResultStatus.Invalid, float.PositiveInfinity, 0, targetPosition, false);
            }

            // Agent-parameterized path authority
            _sharedPath.ClearCorners();
            bool querySuccess = agent.CalculatePath(snappedTarget, _sharedPath);

            PathQueryResultStatus status;
            if (!querySuccess || _sharedPath.status == NavMeshPathStatus.PathInvalid)
            {
                status = PathQueryResultStatus.Invalid;
            }
            else if (_sharedPath.status == NavMeshPathStatus.PathComplete)
            {
                status = PathQueryResultStatus.Complete;
            }
            else
            {
                status = PathQueryResultStatus.Partial;
            }

            int cornerCount = _sharedPath.GetCornersNonAlloc(_cornerBuffer);
            float cumulativeDistance = CalculateCumulativeDistanceNonAlloc(_sharedPath);

            Vector3 terminalCorner = (cornerCount > 0) ? _cornerBuffer[cornerCount - 1] : snappedTarget;
            bool isReachable = (status == PathQueryResultStatus.Complete);

            return new PathQueryResult(status, cumulativeDistance, cornerCount, terminalCorner, isReachable);
        }

        /// <summary>
        /// Computes the piecewise-linear 3D Euclidean distance along an existing calculated path polyline.
        /// Satisfies AC-NAV-01 and AC-NAV-02.
        /// </summary>
        public float CalculateCumulativeDistanceNonAlloc(NavMeshPath path)
        {
            if (path == null || path.status == NavMeshPathStatus.PathInvalid)
            {
                return float.PositiveInfinity;
            }

            int count = path.GetCornersNonAlloc(_cornerBuffer);
            if (count < 2)
            {
                return float.PositiveInfinity;
            }

            return CalculateCumulativeDistance(_cornerBuffer, count);
        }

        /// <summary>
        /// Static pure calculation of piecewise Euclidean distance along an array of corner coordinates.
        /// Formula D1: sum ||p_{k+1} - p_k||_2.
        /// </summary>
        public static float CalculateCumulativeDistance(Vector3[] corners, int count, PathQueryResultStatus status = PathQueryResultStatus.Complete)
        {
            if (status == PathQueryResultStatus.Invalid || corners == null || count < 2)
            {
                return float.PositiveInfinity;
            }

            float cumulativeDistance = 0f;
            int limit = Mathf.Min(count, corners.Length);
            for (int i = 0; i < limit - 1; i++)
            {
                cumulativeDistance += Vector3.Distance(corners[i], corners[i + 1]);
            }

            return cumulativeDistance;
        }

        /// <summary>
        /// Evaluates horizontal 2D Euclidean distance on the XZ plane against arrival tolerance.
        /// </summary>
        public bool IsArrived(Vector3 currentPosition, Vector3 targetPosition, float arriveThreshold = DefaultArriveThreshold)
        {
            float dx = currentPosition.x - targetPosition.x;
            float dz = currentPosition.z - targetPosition.z;
            return (dx * dx + dz * dz) <= (arriveThreshold * arriveThreshold);
        }

        /// <summary>
        /// Evaluates mutually exclusive arrival states with precedence lock (Arrived strictly overrides PathEnd).
        /// Satisfies AC-NAV-05 and GDD #12 Section D3.
        /// </summary>
        public ArrivalState EvaluateArrival(Vector3 guardPosition, Vector3 targetPosition, PathQueryResult queryResult, float arriveThreshold = DefaultArriveThreshold)
        {
            return EvaluateArrivalState(guardPosition, targetPosition, queryResult.TerminalCorner, queryResult.Status, arriveThreshold);
        }

        /// <summary>
        /// Static pure evaluation of arrival precedence logic.
        /// Precedence rule: If within arriveThreshold of target => Arrived.
        /// Else if path is Partial and within arriveThreshold of terminal corner => PathEnd.
        /// Otherwise => Traversing.
        /// </summary>
        public static ArrivalState EvaluateArrivalState(
            Vector3 guardPosition,
            Vector3 targetPosition,
            Vector3 terminalCorner,
            PathQueryResultStatus pathStatus,
            float arriveThreshold = DefaultArriveThreshold)
        {
            // Case A & Case C: True arrival and Precedence lock (Arrived strictly overrides PathEnd)
            float dxTarget = guardPosition.x - targetPosition.x;
            float dzTarget = guardPosition.z - targetPosition.z;
            if ((dxTarget * dxTarget + dzTarget * dzTarget) <= (arriveThreshold * arriveThreshold))
            {
                return ArrivalState.Arrived;
            }

            // Case B: Obstruction path-end
            if (pathStatus == PathQueryResultStatus.Partial)
            {
                float dxCorner = guardPosition.x - terminalCorner.x;
                float dzCorner = guardPosition.z - terminalCorner.z;
                if ((dxCorner * dxCorner + dzCorner * dzCorner) <= (arriveThreshold * arriveThreshold))
                {
                    return ArrivalState.PathEnd;
                }
            }

            return ArrivalState.Traversing;
        }

        /// <summary>
        /// Evaluates catch-gate engagement and hysteresis state.
        /// Satisfies AC-NAV-03, AC-NAV-04, and GDD #12 Section D2.
        /// </summary>
        public bool EvaluateCatchGate(
            bool currentGateState,
            PathQueryResultStatus pathStatus,
            float cumulativeDistance,
            float catchRange = DefaultCatchRange,
            float hysteresis = DefaultCatchHysteresis)
        {
            // AC-NAV-04: PathPartial or PathInvalid immediately disengages catch-gate
            if (pathStatus != PathQueryResultStatus.Complete)
            {
                return false;
            }

            if (float.IsPositiveInfinity(cumulativeDistance) || float.IsNaN(cumulativeDistance))
            {
                return false;
            }

            // AC-NAV-03: Formula D2 hysteresis logic
            if (!currentGateState)
            {
                // Engagement threshold: d_path <= catch_range
                return cumulativeDistance <= catchRange;
            }
            else
            {
                // Retention threshold: holds until d_path > catch_range + hysteresis
                return cumulativeDistance <= (catchRange + hysteresis);
            }
        }

        /// <summary>
        /// Evaluates the chase re-pathing dual-trigger predicate with rate floor clamp.
        /// Satisfies AC-NAV-06, AC-NAV-16, and GDD #12 Section D4.
        /// </summary>
        public bool ShouldRepathChase(
            float timeSinceLastPath,
            Vector3 currentPlayerPos,
            Vector3 lastSampledPlayerPos,
            float minInterval = DefaultRepathMinInterval,
            float cadencePeriod = DefaultRepathCadencePeriod,
            float displacementTrigger = DefaultRepathDisplacementTrigger)
        {
            // Rate floor clamp (e.g. 0.10s / 10Hz limit)
            if (timeSinceLastPath < minInterval)
            {
                return false;
            }

            // Cadence trigger (e.g. 0.25s interval)
            if (timeSinceLastPath >= cadencePeriod)
            {
                return true;
            }

            // Displacement trigger on planar XZ (e.g. 0.50m)
            float dx = currentPlayerPos.x - lastSampledPlayerPos.x;
            float dz = currentPlayerPos.z - lastSampledPlayerPos.z;
            float distSq = dx * dx + dz * dz;

            return distSq >= (displacementTrigger * displacementTrigger);
        }
    }
}
