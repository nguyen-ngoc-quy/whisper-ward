using System;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Camera;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Tests.Unit.Camera
{
    /// <summary>
    /// Automated NUnit EditMode unit test suite for CameraRigService deocclusion and damping.
    /// Verifies instantaneous deocclusion collapse on collision (AC-CAM-06),
    /// exponential distance recovery damping (AC-CAM-07),
    /// character dither transparency signal on tight crevice (AC-CAM-08),
    /// and vertical leash hard override on rapid descent (AC-CAM-09).
    /// Governed by ADR-0008 and story-002-spherecast-deocclusion-damping.md.
    /// </summary>
    [TestFixture]
    public class CameraDeocclusionTest
    {
        private CameraRigService _cameraRig;

        [SetUp]
        public void SetUp()
        {
            _cameraRig = new CameraRigService();
        }

        #region AC-CAM-06: Instantaneous Deocclusion Collapse

        [Test]
        public void Test_CameraRig_EvaluateCameraDistance_ObstacleAtOnePointFiveMeters_CollapsesInstantlyToOnePointThirty()
        {
            // Arrange: Nominal distance = 2.80m, obstacle detected at d_hit = 1.50m (radius = 0.20m, min = 0.40m)
            // Expected collapse: max(0.40, 1.50 - 0.20) = 1.30m
            Vector3 targetPos = new Vector3(0f, 1.35f, 0f);
            Vector3 camDir = Vector3.back;
            float currentDistance = 2.80f;
            float dt = 0.0166667f; // 60 fps frame

            _cameraRig.SetTestHooks(
                timeScaleProvider: () => 1.0f,
                sphereCastDelegate: (origin, radius, direction, results, maxDist, mask, trigger) =>
                {
                    results[0] = new RaycastHit { distance = 1.50f };
                    return 1;
                });

            // Act
            float evaluatedDist = _cameraRig.EvaluateCameraDistance(targetPos, camDir, currentDistance, dt);

            // Assert: Instantaneous collapse to 1.30m in same frame (0ms)
            Assert.AreEqual(1.30f, evaluatedDist, 0.001f, "Camera distance must instantly collapse to d_hit - R = 1.30m.");
            Assert.AreEqual(1.30f, _cameraRig.CurrentDistance, 0.001f, "CurrentDistance property must reflect evaluated distance.");
        }

        [Test]
        public void Test_CameraRig_EvaluateCameraDistance_ObstacleCloserThanMinDistance_ClampsToMinDistanceFloor()
        {
            // Arrange: Obstacle at d_hit = 0.30m -> max(0.40, 0.30 - 0.20 = 0.10) = 0.40m
            Vector3 targetPos = new Vector3(0f, 1.35f, 0f);
            Vector3 camDir = Vector3.back;
            float currentDistance = 2.80f;
            float dt = 0.0166667f;

            _cameraRig.SetTestHooks(
                timeScaleProvider: () => 1.0f,
                sphereCastDelegate: (origin, radius, direction, results, maxDist, mask, trigger) =>
                {
                    results[0] = new RaycastHit { distance = 0.30f };
                    return 1;
                });

            // Act
            float evaluatedDist = _cameraRig.EvaluateCameraDistance(targetPos, camDir, currentDistance, dt);

            // Assert: Distance must not drop below D_min = 0.40m
            Assert.AreEqual(0.40f, evaluatedDist, 0.001f, "Camera distance must clamp strictly to D_min = 0.40m.");
        }

        [Test]
        public void Test_CameraRig_EvaluateCameraDistance_MultipleHits_PicksClosestObstacleHit()
        {
            // Arrange: Two hits in buffer (1.80m and 1.20m), closest hit = 1.20m -> 1.20 - 0.20 = 1.00m
            Vector3 targetPos = new Vector3(0f, 1.35f, 0f);
            Vector3 camDir = Vector3.back;
            float currentDistance = 2.80f;
            float dt = 0.0166667f;

            _cameraRig.SetTestHooks(
                timeScaleProvider: () => 1.0f,
                sphereCastDelegate: (origin, radius, direction, results, maxDist, mask, trigger) =>
                {
                    results[0] = new RaycastHit { distance = 1.80f };
                    results[1] = new RaycastHit { distance = 1.20f };
                    return 2;
                });

            // Act
            float evaluatedDist = _cameraRig.EvaluateCameraDistance(targetPos, camDir, currentDistance, dt);

            // Assert: Must pick closest hit (1.20m - 0.20m = 1.00m)
            Assert.AreEqual(1.00f, evaluatedDist, 0.001f, "Evaluation must select closest obstacle distance.");
        }

        #endregion

        #region AC-CAM-07: Exponential Distance Recovery Damping

        [Test]
        public void Test_CameraRig_EvaluateCameraDistance_ObstacleRemoved_RecoversExponentiallyToNinetyFivePercentAtThreeTau()
        {
            // Arrange: Compressed distance D(0) = 1.30m, obstacle removed (0 hits -> target = 2.80m)
            // Time step dt = 1/60 s, tau = 0.25 s, duration = 0.75 s (45 frames = 3 * tau)
            // Theoretical D(3*tau) = 1.30 + (2.80 - 1.30) * (1 - e^-3) = 1.30 + 1.50 * 0.95021 = 2.7253m (>= 95% nominal)
            Vector3 targetPos = new Vector3(0f, 1.35f, 0f);
            Vector3 camDir = Vector3.back;
            float currentDistance = 1.30f;
            float dt = 0.75f / 45f; // exactly 45 steps to reach 0.75s

            _cameraRig.SetTestHooks(
                timeScaleProvider: () => 1.0f,
                sphereCastDelegate: (origin, radius, direction, results, maxDist, mask, trigger) => 0); // 0 hits

            // Act: Advance simulation through 45 frames
            float previousDist = currentDistance;
            for (int frame = 0; frame < 45; frame++)
            {
                currentDistance = _cameraRig.EvaluateCameraDistance(targetPos, camDir, currentDistance, dt);

                // Monotonicity check: distance must increase every frame
                Assert.Greater(currentDistance, previousDist, $"Distance must increase monotonically at frame {frame}.");
                previousDist = currentDistance;
            }

            // Assert: Must reach >= 95% of nominal distance (2.80 * 0.95 = 2.66m, and >= 2.72m per QA spec)
            Assert.GreaterOrEqual(currentDistance, 2.72f, "Distance after 0.75s (3 tau) must reach >= 2.72m.");
            Assert.LessOrEqual(currentDistance, 2.80f, "Distance must not overshoot nominal distance.");
        }

        #endregion

        #region AC-CAM-08: Dither Transparency Signal on Tight Crevice

        [TestCase(0.40f, 0.15f)] // Min distance: 15% opacity (85% transparent)
        [TestCase(0.50f, 0.50f)] // Midpoint crevice: 50% opacity
        [TestCase(0.60f, 1.00f)] // Threshold distance: 100% opaque
        [TestCase(0.70f, 1.00f)] // Clear distance: 100% opaque
        [TestCase(2.80f, 1.00f)] // Nominal distance: 100% opaque
        public void Test_CameraRig_CalculateDitherOpacity_ReturnsExpectedOpacityCurves(float distance, float expectedOpacity)
        {
            // Act
            float opacity = _cameraRig.CalculateDitherOpacity(distance);

            // Assert: Opacity_dither = clamp((D_actual - 0.40) / 0.20, 0.15, 1.0)
            Assert.AreEqual(expectedOpacity, opacity, 0.001f, $"Dither opacity at {distance}m must match expected curve value.");
        }

        #endregion

        #region AC-CAM-09: Vertical Leash Hard Override on Rapid Descent

        [Test]
        public void Test_CameraRig_EnforceVerticalLeash_RapidDescentBeyondLeash_SnapsAltitudeToLeashThreshold()
        {
            // Arrange: Player drops with Delta Y = 3.20m (> 2.50m leash)
            // Target is at Y = 0.0m, Camera lagged at Y = 3.20m
            Vector3 targetPosition = new Vector3(0f, 0.0f, 0f);
            Vector3 cameraPosition = new Vector3(0f, 3.20f, -2.80f);

            // Act
            Vector3 snappedCamPos = CameraRigService.EnforceVerticalLeash(cameraPosition, targetPosition, 2.50f);

            // Assert: Vertical displacement must snap exactly to 2.50m (Camera Y = 2.50m)
            float verticalOffset = snappedCamPos.y - targetPosition.y;
            Assert.AreEqual(2.50f, verticalOffset, 0.001f, "Camera vertical offset must snap strictly to Delta Y = 2.50m.");
            Assert.AreEqual(2.50f, snappedCamPos.y, 0.001f, "Camera Y position must equal target Y + 2.50m.");
        }

        [Test]
        public void Test_CameraRig_EnforceVerticalLeash_WithinLeashThreshold_LeavesVerticalPositionUntouched()
        {
            // Arrange: Player displacement Delta Y = 1.50m (<= 2.50m leash)
            Vector3 targetPosition = new Vector3(0f, 0.0f, 0f);
            Vector3 cameraPosition = new Vector3(0f, 1.50f, -2.80f);

            // Act
            Vector3 adjustedCamPos = CameraRigService.EnforceVerticalLeash(cameraPosition, targetPosition, 2.50f);

            // Assert: Camera position remains unchanged
            Assert.AreEqual(1.50f, adjustedCamPos.y, 0.001f, "Position within leash must not be snapped.");
        }

        #endregion
    }
}
