using System.Collections.Generic;
using UnityEngine;

namespace WhisperWard.Core.Contracts
{
    /// <summary>
    /// Configuration data structure holding NavMesh bake settings for validation.
    /// Governed by ADR-0007, ADR-0002, and control-manifest.md.
    /// </summary>
    public readonly struct NavMeshBakeConfig
    {
        public readonly float VoxelSize;
        public readonly float AgentMaxSlope;
        public readonly float AgentClimb;
        public readonly bool GenerateLinks;
        public readonly int LayerMask;

        public NavMeshBakeConfig(float voxelSize, float agentMaxSlope, float agentClimb, bool generateLinks, int layerMask)
        {
            VoxelSize = voxelSize;
            AgentMaxSlope = agentMaxSlope;
            AgentClimb = agentClimb;
            GenerateLinks = generateLinks;
            LayerMask = layerMask;
        }
    }

    /// <summary>
    /// Represents a detected corridor choke point violation.
    /// </summary>
    public readonly struct CorridorChokePoint
    {
        public readonly Vector3 Position;
        public readonly float MeasuredWidth;
        public readonly float RequiredWidth;

        public CorridorChokePoint(Vector3 position, float measuredWidth, float requiredWidth)
        {
            Position = position;
            MeasuredWidth = measuredWidth;
            RequiredWidth = requiredWidth;
        }
    }

    /// <summary>
    /// Authoritative contract for NavMesh spatial corridor clearance and level build certification.
    /// Satisfies TR-CORE-006, TR-CORE-007, and AC-NAV-07, AC-NAV-08, AC-NAV-18, AC-NAV-19, AC-NAV-20, AC-NAV-21.
    /// </summary>
    public interface INavMeshCertificationValidator
    {
        /// <summary>
        /// Validates complete bake configuration settings against architectural invariants.
        /// </summary>
        /// <param name="config">Bake settings configuration.</param>
        /// <param name="errorCodes">Output list of detected violation error codes.</param>
        /// <returns>True if all invariants pass; false otherwise.</returns>
        bool ValidateBakeConfig(NavMeshBakeConfig config, out List<string> errorCodes);

        /// <summary>
        /// Validates spatial corridor clearance at a specific level position.
        /// Fails if measured clearance width is less than required width (default 1.20m, 1.50m for main patrol).
        /// </summary>
        /// <param name="samplePoint">Sample point on NavMesh surface.</param>
        /// <param name="tangentDirection">Horizontal traversal direction along the corridor.</param>
        /// <param name="requiredWidth">Minimum required clearance width in meters.</param>
        /// <param name="measuredWidth">Output measured width in meters.</param>
        /// <param name="chokePoint">Output choke point details if violation detected.</param>
        /// <returns>True if corridor clearance is valid; false if choke point detected.</returns>
        bool ValidateCorridorClearance(
            Vector3 samplePoint,
            Vector3 tangentDirection,
            float requiredWidth,
            out float measuredWidth,
            out CorridorChokePoint chokePoint);

        /// <summary>
        /// Verifies that zero off-mesh links exist in the NavMeshData and that link generation is disabled.
        /// </summary>
        /// <param name="offMeshLinkCount">Count of off-mesh links in the NavMeshData.</param>
        /// <param name="generateLinks">State of the generateLinks flag.</param>
        /// <param name="errorCode">Output error code if invalid.</param>
        /// <returns>True if zero links present; false otherwise.</returns>
        bool ValidateOffMeshLinks(int offMeshLinkCount, bool generateLinks, out string errorCode);

        /// <summary>
        /// Asserts that the NavMesh bake layer mask includes static geometry and strictly excludes dynamic actor/trigger layers.
        /// </summary>
        /// <param name="bakeLayerMask">Layer mask used for NavMesh bake.</param>
        /// <param name="forbiddenLayersMask">Mask of forbidden dynamic layers (Player, Guard, HideSpot, Trigger).</param>
        /// <param name="errorCode">Output error code if invalid.</param>
        /// <returns>True if layer mask is valid; false otherwise.</returns>
        bool ValidateBakeLayerMask(int bakeLayerMask, int forbiddenLayersMask, out string errorCode);

        /// <summary>
        /// Asserts that bake voxel size satisfies the resolution invariant (voxelSize <= r_guard / 3 = 0.1333m).
        /// </summary>
        /// <param name="voxelSize">Configured voxel size in meters.</param>
        /// <param name="maxVoxelSize">Maximum allowed voxel size (default 0.1333m).</param>
        /// <param name="errorCode">Output error code if invalid.</param>
        /// <returns>True if voxel resolution is fine enough; false if too coarse.</returns>
        bool ValidateVoxelSize(float voxelSize, float maxVoxelSize, out string errorCode);

        /// <summary>
        /// Asserts that agent slope (<= 45 deg) and step climb height (<= 0.30m) are within valid ceilings.
        /// </summary>
        /// <param name="maxSlope">Configured slope angle in degrees.</param>
        /// <param name="climbHeight">Configured step height in meters.</param>
        /// <param name="errorCodes">Output list of vertical parameter violations.</param>
        /// <returns>True if vertical bounds are satisfied; false otherwise.</returns>
        bool ValidateSlopeAndClimb(float maxSlope, float climbHeight, out List<string> errorCodes);
    }
}
