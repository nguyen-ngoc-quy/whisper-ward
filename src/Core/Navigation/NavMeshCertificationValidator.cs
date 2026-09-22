using System;
using System.Collections.Generic;
using UnityEngine;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Core.Navigation
{
    /// <summary>
    /// Delegate for directional distance raycasts, enabling deterministic headless testing of geometry clearance.
    /// </summary>
    public delegate bool RaycastDistanceDelegate(
        Vector3 origin,
        Vector3 direction,
        out float distance,
        float maxDistance,
        int layerMask);

    /// <summary>
    /// Authoritative level build-time and editor validator for NavMesh certification.
    /// Enforces corridor clearances (AC-NAV-08, AC-NAV-18), zero off-mesh links (AC-NAV-19),
    /// layer mask exclusions (AC-NAV-20), voxel resolution (AC-NAV-07), and vertical slope/step ceilings (AC-NAV-21).
    /// Governed by ADR-0007, ADR-0002, and control-manifest.md.
    /// </summary>
    public sealed class NavMeshCertificationValidator : INavMeshCertificationValidator
    {
        public const float DefaultMinCorridorWidth = 1.20f;
        public const float DefaultMainPatrolCorridorWidth = 1.50f;
        public const float DefaultMaxVoxelSize = 0.1333f; // r_guard / 3 = 0.40m / 3
        public const float DefaultMaxSlope = 45.0f;
        public const float DefaultMaxClimbStep = 0.30f;

        public const string ErrCorridorChokePoint = "ERR_CORRIDOR_CHOKE_POINT";
        public const string ErrOffMeshLinksPresent = "ERR_OFF_MESH_LINKS_PRESENT";
        public const string ErrOffMeshLinksEnabled = "ERR_OFF_MESH_LINKS_ENABLED";
        public const string ErrDynamicLayersInBake = "ERR_DYNAMIC_LAYERS_IN_NAVMESH_BAKE";
        public const string ErrBakeVoxelTooCoarse = "ERR_BAKE_VOXEL_TOO_COARSE";
        public const string ErrMaxSlopeExceeded = "ERR_MAX_SLOPE_EXCEEDED";
        public const string ErrStepHeightExceeded = "ERR_STEP_HEIGHT_EXCEEDED";

        private RaycastDistanceDelegate _raycastOverride;
        private readonly int _worldLayerMask;
        private readonly int _forbiddenDynamicMask;

        public NavMeshCertificationValidator(int worldLayerMask = -1, int forbiddenDynamicMask = -1)
        {
            _worldLayerMask = worldLayerMask != -1 ? worldLayerMask : LayerMask.GetMask("World");
            _forbiddenDynamicMask = forbiddenDynamicMask != -1 ? forbiddenDynamicMask : LayerMask.GetMask("Player", "Guard", "HideSpot", "Trigger");
        }

        /// <summary>
        /// Sets a custom raycast delegate for testing geometric corridor clearance without live physics scenes.
        /// </summary>
        public void SetTestHooks(RaycastDistanceDelegate raycastDelegate)
        {
            _raycastOverride = raycastDelegate;
        }

        /// <summary>
        /// Validates complete bake configuration settings against architectural invariants.
        /// </summary>
        public bool ValidateBakeConfig(NavMeshBakeConfig config, out List<string> errorCodes)
        {
            errorCodes = new List<string>();

            if (config.GenerateLinks)
            {
                errorCodes.Add(ErrOffMeshLinksEnabled);
            }

            if (!ValidateVoxelSize(config.VoxelSize, DefaultMaxVoxelSize, out string voxelError))
            {
                errorCodes.Add(voxelError);
            }

            if (!ValidateBakeLayerMask(config.LayerMask, _forbiddenDynamicMask, out string maskError))
            {
                errorCodes.Add(maskError);
            }

            if (!ValidateSlopeAndClimb(config.AgentMaxSlope, config.AgentClimb, out List<string> verticalErrors))
            {
                errorCodes.AddRange(verticalErrors);
            }

            return errorCodes.Count == 0;
        }

        /// <summary>
        /// Validates spatial corridor clearance at a specific sample point along a tangent traversal vector.
        /// Satisfies AC-NAV-08 and AC-NAV-18.
        /// </summary>
        public bool ValidateCorridorClearance(
            Vector3 samplePoint,
            Vector3 tangentDirection,
            float requiredWidth,
            out float measuredWidth,
            out CorridorChokePoint chokePoint)
        {
            Vector3 tangent = tangentDirection.sqrMagnitude > 1e-8f ? tangentDirection.normalized : Vector3.forward;
            // Compute perpendicular normal on XZ plane: (-tangent.z, 0, tangent.x)
            Vector3 leftDir = new Vector3(-tangent.z, 0f, tangent.x).normalized;
            Vector3 rightDir = -leftDir;

            float maxScanDistance = Mathf.Max(requiredWidth * 2f, 5.0f);
            float dLeft = maxScanDistance;
            float dRight = maxScanDistance;

            if (_raycastOverride != null)
            {
                if (_raycastOverride(samplePoint, leftDir, out float hitDistLeft, maxScanDistance, _worldLayerMask))
                {
                    dLeft = hitDistLeft;
                }
                if (_raycastOverride(samplePoint, rightDir, out float hitDistRight, maxScanDistance, _worldLayerMask))
                {
                    dRight = hitDistRight;
                }
            }
            else
            {
                if (Physics.Raycast(samplePoint, leftDir, out RaycastHit hitLeft, maxScanDistance, _worldLayerMask))
                {
                    dLeft = hitLeft.distance;
                }
                if (Physics.Raycast(samplePoint, rightDir, out RaycastHit hitRight, maxScanDistance, _worldLayerMask))
                {
                    dRight = hitRight.distance;
                }
            }

            measuredWidth = dLeft + dRight;
            if (measuredWidth < requiredWidth)
            {
                chokePoint = new CorridorChokePoint(samplePoint, measuredWidth, requiredWidth);
                return false;
            }

            chokePoint = default;
            return true;
        }

        /// <summary>
        /// Verifies that zero off-mesh links exist in the NavMeshData and that link generation is disabled.
        /// Satisfies AC-NAV-19.
        /// </summary>
        public bool ValidateOffMeshLinks(int offMeshLinkCount, bool generateLinks, out string errorCode)
        {
            if (offMeshLinkCount > 0)
            {
                errorCode = ErrOffMeshLinksPresent;
                return false;
            }

            if (generateLinks)
            {
                errorCode = ErrOffMeshLinksEnabled;
                return false;
            }

            errorCode = null;
            return true;
        }

        /// <summary>
        /// Asserts that the NavMesh bake layer mask includes static geometry and strictly excludes dynamic actor/trigger layers.
        /// Satisfies AC-NAV-20.
        /// </summary>
        public bool ValidateBakeLayerMask(int bakeLayerMask, int forbiddenLayersMask, out string errorCode)
        {
            int activeForbidden = bakeLayerMask & forbiddenLayersMask;
            if (activeForbidden != 0)
            {
                errorCode = ErrDynamicLayersInBake;
                return false;
            }

            errorCode = null;
            return true;
        }

        /// <summary>
        /// Asserts that bake voxel size satisfies the resolution invariant (voxelSize <= r_guard / 3 = 0.1333m).
        /// Satisfies AC-NAV-07.
        /// </summary>
        public bool ValidateVoxelSize(float voxelSize, float maxVoxelSize, out string errorCode)
        {
            if (voxelSize > maxVoxelSize + 0.0001f)
            {
                errorCode = ErrBakeVoxelTooCoarse;
                return false;
            }

            errorCode = null;
            return true;
        }

        /// <summary>
        /// Asserts that agent slope (<= 45 deg) and step climb height (<= 0.30m) are within valid ceilings.
        /// Satisfies AC-NAV-21.
        /// </summary>
        public bool ValidateSlopeAndClimb(float maxSlope, float climbHeight, out List<string> errorCodes)
        {
            errorCodes = new List<string>();

            if (maxSlope > DefaultMaxSlope + 0.0001f)
            {
                errorCodes.Add(ErrMaxSlopeExceeded);
            }

            if (climbHeight > DefaultMaxClimbStep + 0.0001f)
            {
                errorCodes.Add(ErrStepHeightExceeded);
            }

            return errorCodes.Count == 0;
        }
    }
}
