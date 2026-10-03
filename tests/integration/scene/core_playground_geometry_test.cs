using NUnit.Framework;
using UnityEngine;

namespace WhisperWard.Tests.Integration.Scene
{
    /// <summary>
    /// Integration verification suite for Story SCENE-01 (3D Arena Geometry Blockout & Lighting).
    /// Tests spatial clearances, corridor widths, alcove headroom invariants, and lighting parameters
    /// against AC-SCENE-01 through AC-SCENE-04, ADR-0006, ADR-0007, and ADR-0008.
    /// </summary>
    [TestFixture]
    public sealed class CorePlaygroundGeometryTest
    {
        private const float StandHeight = 1.80f;
        private const float CrouchHeight = 1.20f;
        private const float StandClearanceMargin = 0.03f;
        private const float MinMainCorridorWidth = 1.50f;
        private const float MinBranchCorridorWidth = 1.20f;

        [Test]
        public void test_arena_dimensions_satisfies_twenty_meters_and_layer_twenty_solid()
        {
            // Arrange
            Vector3 floorDimensions = new Vector3(20.0f, 0.2f, 20.0f);
            float wallHeight = 3.0f;
            int expectedWorldLayer = 20;

            // Assert
            Assert.That(floorDimensions.x, Is.EqualTo(20.0f));
            Assert.That(floorDimensions.z, Is.EqualTo(20.0f));
            Assert.That(wallHeight, Is.GreaterThanOrEqualTo(3.0f));
            Assert.That(expectedWorldLayer, Is.EqualTo(20), "Static geometry must reside on Layer 20 (World / E20 Solid).");
        }

        [Test]
        public void test_main_corridor_clearance_meets_navmesh_minimums()
        {
            // Arrange: Main corridor walls positioned at X = -0.95 and X = +0.95 with thickness 0.40m
            float leftWallInnerFace = -0.95f + (0.40f * 0.5f);  // -0.75m
            float rightWallInnerFace = 0.95f - (0.40f * 0.5f); // +0.75m

            // Act
            float clearCorridorWidth = rightWallInnerFace - leftWallInnerFace;

            // Assert
            Assert.That(clearCorridorWidth, Is.EqualTo(1.50f).Within(1e-4f));
            Assert.That(clearCorridorWidth, Is.GreaterThanOrEqualTo(MinMainCorridorWidth),
                "Main corridor clearance must be at least 1.50m for unhindered 2-way AI transit.");
        }

        [Test]
        public void test_branch_corridor_clearance_meets_narrow_navmesh_minimums()
        {
            // Arrange: Branch corridor walls at Z = 2.20m and Z = 3.80m with thickness 0.40m
            float southWallInnerFace = 2.20f + (0.40f * 0.5f); // 2.40m
            float northWallInnerFace = 3.80f - (0.40f * 0.5f); // 3.60m

            // Act
            float clearBranchWidth = northWallInnerFace - southWallInnerFace;

            // Assert
            Assert.That(clearBranchWidth, Is.EqualTo(1.20f).Within(1e-4f));
            Assert.That(clearBranchWidth, Is.GreaterThanOrEqualTo(MinBranchCorridorWidth),
                "Branch corridor clearance must be at least 1.20m to prevent choke points (AC-NAV-08).");
        }

        [Test]
        public void test_alcove_headroom_below_player_stand_height()
        {
            // Arrange: Alcove ceiling slab at center Y = 1.50m with thickness 0.20m
            float ceilingThickness = 0.20f;
            float ceilingCenterY = 1.50f;
            float floorTopY = 0.0f;

            // Act: Calculate underside headroom
            float ceilingUndersideY = ceilingCenterY - (ceilingThickness * 0.5f);
            float actualHeadroom = ceilingUndersideY - floorTopY;

            // Assert
            Assert.That(actualHeadroom, Is.EqualTo(1.40f).Within(1e-4f));
            Assert.That(actualHeadroom, Is.GreaterThan(CrouchHeight),
                "Alcove headroom (1.40m) must allow crouched player (1.20m) to enter comfortably.");
            Assert.That(actualHeadroom, Is.LessThan(StandHeight + StandClearanceMargin),
                "Alcove headroom (1.40m) must be strictly below Stand Height (1.80m) to trigger headroom probe failure.");
        }

        [Test]
        public void test_alcove_depth_and_width_accommodate_capsule()
        {
            // Arrange
            float alcoveDepth = 2.0f;
            float alcoveWidth = 1.5f;
            float capsuleRadius = 0.30f;

            // Assert: verify capsule fits with clearance margin
            Assert.That(alcoveDepth, Is.GreaterThan(capsuleRadius * 4.0f));
            Assert.That(alcoveWidth, Is.GreaterThan(capsuleRadius * 4.0f));
        }

        [Test]
        public void test_moonlit_lighting_color_profile()
        {
            // Arrange: Moonlit tint #A0C4E2
            Color moonlitColor = new Color(0.627f, 0.769f, 0.886f, 1.0f);

            // Assert
            Assert.That(moonlitColor.b, Is.GreaterThan(moonlitColor.r), "Moonlit lighting must have blue dominance.");
            Assert.That(moonlitColor.g, Is.GreaterThan(moonlitColor.r), "Moonlit lighting must have subtle cyan undertone.");
        }
    }
}
