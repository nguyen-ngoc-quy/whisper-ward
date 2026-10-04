using System;
using UnityEngine;
using WhisperWard.AI.FSM;

namespace WhisperWard.AI.Alert
{
    /// <summary>
    /// Coordinates multi-guard search behavior at a shared Last Known Position (LKP).
    /// Enforces:
    /// 1. Standoff position separation (d >= 2.0m) to prevent NavMeshAgent stacking and crowding.
    /// 2. Divergent dwell look-around scan angles (180 deg opposite for 2 guards, 360/N for N guards)
    ///    to maximize visual coverage during the 4.0s dwell search.
    /// 3. Zero GC allocation dynamic registration and slot release.
    /// Conforms to GDD guard-ai-fsm.md (lines 514..548) and Story ALERT-02 (AC-ALERT-05, AC-ALERT-06).
    /// </summary>
    public class MultiGuardSearchCoordinator : MonoBehaviour
    {
        public struct SearchAssignment
        {
            public int GuardId;
            public Vector3 TargetLkp;
            public Vector3 StandoffPosition;
            public float DivergentYawOffset;
            public float RegisteredTime;
            public bool IsActive;
        }

        [Header("Standoff & Separation Tuning")]
        [Tooltip("Minimum spatial separation distance between any two guards investigating the same LKP (GDD: 2.0m).")]
        [SerializeField] private float _minSeparation = 2.00f;

        [Tooltip("Radius threshold for clustering investigations into the same search zone in meters.")]
        [SerializeField] private float _clusterRadius = 3.00f;

        [Tooltip("Maximum concurrent search assignments supported without heap allocation.")]
        [SerializeField] private int _maxAssignments = 16;

        private SearchAssignment[] _assignments;
        private int _activeCount;

        public float MinSeparation => _minSeparation;
        public float ClusterRadius => _clusterRadius;
        public int ActiveCount => _activeCount;

        private void Awake()
        {
            InitializeBuffer();
        }

        private void InitializeBuffer()
        {
            if (_assignments == null || _assignments.Length != _maxAssignments)
            {
                _assignments = new SearchAssignment[_maxAssignments];
                _activeCount = 0;
            }
        }

        /// <summary>
        /// Explicit initialization for unit tests and headless runtime without Awake.
        /// Zero allocations.
        /// </summary>
        public void Configure(float minSeparation = 2.00f, float clusterRadius = 3.00f, int maxAssignments = 16)
        {
            _minSeparation = Mathf.Max(0.5f, minSeparation);
            _clusterRadius = Mathf.Max(1.0f, clusterRadius);
            _maxAssignments = Mathf.Clamp(maxAssignments, 4, 64);
            _assignments = new SearchAssignment[_maxAssignments];
            _activeCount = 0;
        }

        /// <summary>
        /// Registers a guard for investigation at an LKP, allocating a standoff position
        /// and divergent dwell scan yaw offset.
        /// Zero managed GC allocations.
        /// </summary>
        public Vector3 RegisterGuardInvestigation(
            int guardId,
            Vector3 targetLkp,
            out float divergentYaw,
            float currentTime = 0f)
        {
            InitializeBuffer();

            // Check if guard is already registered in an active slot
            int existingSlot = -1;
            for (int i = 0; i < _assignments.Length; i++)
            {
                if (_assignments[i].IsActive && _assignments[i].GuardId == guardId)
                {
                    existingSlot = i;
                    break;
                }
            }

            // Count peers already in this spatial cluster (excluding this guard if re-registering)
            int clusterCount = 0;
            float clusterSq = _clusterRadius * _clusterRadius;

            for (int i = 0; i < _assignments.Length; i++)
            {
                if (!_assignments[i].IsActive) continue;
                if (i == existingSlot) continue;

                float dx = _assignments[i].TargetLkp.x - targetLkp.x;
                float dz = _assignments[i].TargetLkp.z - targetLkp.z;
                if ((dx * dx + dz * dz) <= clusterSq)
                {
                    clusterCount++;
                }
            }

            // Guard slot index in the cluster (0 = primary arrival, 1 = first standoff peer, etc.)
            int guardSlotIndex = clusterCount;
            int totalGuardsInCluster = clusterCount + 1;

            Vector3 standoffPos = CalculateStandoffPosition(targetLkp, guardSlotIndex, totalGuardsInCluster, _minSeparation);
            divergentYaw = CalculateDivergentYawOffset(guardSlotIndex, totalGuardsInCluster);

            int slotToUse = existingSlot;
            if (slotToUse < 0)
            {
                // Find empty slot
                for (int i = 0; i < _assignments.Length; i++)
                {
                    if (!_assignments[i].IsActive)
                    {
                        slotToUse = i;
                        _activeCount++;
                        break;
                    }
                }
            }

            if (slotToUse >= 0)
            {
                _assignments[slotToUse] = new SearchAssignment
                {
                    GuardId = guardId,
                    TargetLkp = targetLkp,
                    StandoffPosition = standoffPos,
                    DivergentYawOffset = divergentYaw,
                    RegisteredTime = currentTime,
                    IsActive = true
                };
            }

            return standoffPos;
        }

        /// <summary>
        /// Releases a guard from active search coordination upon returning to Patrol or entering Chase.
        /// </summary>
        public void ReleaseGuard(int guardId)
        {
            if (_assignments == null) return;

            for (int i = 0; i < _assignments.Length; i++)
            {
                if (_assignments[i].IsActive && _assignments[i].GuardId == guardId)
                {
                    _assignments[i].IsActive = false;
                    _activeCount = Mathf.Max(0, _activeCount - 1);
                    break;
                }
            }
        }

        /// <summary>
        /// Registers and directly applies coordinated standoff destination and divergent dwell yaw to a guard FSM.
        /// </summary>
        public Vector3 CoordinateGuardInvestigation(
            int guardId,
            GuardFSMRuntimeController fsm,
            Vector3 targetLkp,
            float currentTime = 0f)
        {
            Vector3 standoffPos = RegisterGuardInvestigation(guardId, targetLkp, out float divergentYaw, currentTime);
            if (fsm != null)
            {
                fsm.SetDwellYawOffset(divergentYaw);
                fsm.TriggerInvestigate(standoffPos, "coordinated_search_standoff");
            }
            return standoffPos;
        }

        /// <summary>
        /// Retrieves the active search assignment for a guard, if present.
        /// </summary>
        public bool TryGetGuardAssignment(int guardId, out SearchAssignment assignment)
        {
            if (_assignments != null)
            {
                for (int i = 0; i < _assignments.Length; i++)
                {
                    if (_assignments[i].IsActive && _assignments[i].GuardId == guardId)
                    {
                        assignment = _assignments[i];
                        return true;
                    }
                }
            }

            assignment = default;
            return false;
        }

        /// <summary>
        /// Counts how many guards are actively investigating near the specified LKP coordinate.
        /// </summary>
        public int GetActiveGuardCountForTarget(Vector3 targetLkp, float radius = 3.0f)
        {
            if (_assignments == null) return 0;

            int count = 0;
            float rSq = radius * radius;
            for (int i = 0; i < _assignments.Length; i++)
            {
                if (!_assignments[i].IsActive) continue;
                float dx = _assignments[i].TargetLkp.x - targetLkp.x;
                float dz = _assignments[i].TargetLkp.z - targetLkp.z;
                if ((dx * dx + dz * dz) <= rSq)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Clears all active search assignments.
        /// </summary>
        public void ClearAll()
        {
            if (_assignments != null)
            {
                for (int i = 0; i < _assignments.Length; i++)
                {
                    _assignments[i].IsActive = false;
                }
            }
            _activeCount = 0;
        }

        #region Static Mathematical Solvers

        /// <summary>
        /// Calculates a standoff position for a guard participating in a multi-guard search around an LKP.
        /// Guarantees pairwise distance between all guards is >= minSeparation (GDD: 2.0m).
        /// Pure deterministic calculation, zero allocations.
        /// </summary>
        public static Vector3 CalculateStandoffPosition(
            Vector3 lkp,
            int guardSlotIndex,
            int totalGuardsInCluster,
            float minSeparation = 2.00f)
        {
            if (totalGuardsInCluster <= 1 || guardSlotIndex <= 0)
            {
                // Primary guard takes the raw LKP
                return lkp;
            }

            if (totalGuardsInCluster == 2)
            {
                // Secondary guard offsets along lateral X axis by minSeparation (2.0m)
                return lkp + new Vector3(minSeparation, 0f, 0f);
            }

            // For N >= 3 guards, secondary guards (slot 1..N-1) distribute on a circle around LKP
            // with radius R >= minSeparation to ensure distance to primary guard is >= minSeparation,
            // and chord length between peers >= minSeparation.
            int peerCount = totalGuardsInCluster - 1;
            float angleStepRad = (Mathf.PI * 2f) / peerCount;
            float peerAngleRad = (guardSlotIndex - 1) * angleStepRad;

            // Chord length formula: chord = 2 * R * sin(angleStep / 2) >= minSeparation
            float chordDiv = 2f * Mathf.Sin(angleStepRad * 0.5f);
            float minRadiusForPeers = chordDiv > 0.001f ? (minSeparation / chordDiv) : minSeparation;
            float radius = Mathf.Max(minSeparation, minRadiusForPeers);

            float offsetX = Mathf.Cos(peerAngleRad) * radius;
            float offsetZ = Mathf.Sin(peerAngleRad) * radius;

            return new Vector3(lkp.x + offsetX, lkp.y, lkp.z + offsetZ);
        }

        /// <summary>
        /// Calculates a divergent yaw offset in degrees for dwell search look-around.
        /// 2 guards: 0 deg and 180 deg (opposite arcs).
        /// N guards: (guardSlotIndex * 360 / N) % 360.
        /// </summary>
        public static float CalculateDivergentYawOffset(int guardSlotIndex, int totalGuardsInCluster)
        {
            if (totalGuardsInCluster <= 1)
            {
                return 0f;
            }

            if (totalGuardsInCluster == 2)
            {
                return guardSlotIndex == 0 ? 0f : 180f;
            }

            float stepDeg = 360f / totalGuardsInCluster;
            return (guardSlotIndex * stepDeg) % 360f;
        }

        #endregion
    }
}
