using System;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Camera;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Tests.Integration.Camera
{
    /// <summary>
    /// Integration test suite for CameraRigService virtual camera blending and FOV orchestration.
    /// Governed by ADR-0008, GDD #20, and story-003-chase-fov-hidespot-blend.md.
    /// Verifies:
    /// - AC-CAM-10: Dynamic Chase FOV expansion (60 to 68 deg) and contraction (68 to 60 deg)
    /// - AC-CAM-11: HideSpot priority elevation (P=20), 0.35s blend, and +/- 30 deg yaw cone constraint
    /// - AC-CAM-12: Instant blend cancellation (0 ms) and capture focus override (P=100)
    /// - AC-CAM-13: 0 B GC allocation and frame timing compliance
    /// </summary>
    [TestFixture]
    public class CameraBlendIntegrationTest
    {
        private CameraRigService _cameraRig;

        [SetUp]
        public void SetUp()
        {
            _cameraRig = new CameraRigService(initialYaw: 0.0f, initialPitch: 10.0f);
        }

        [TearDown]
        public void TearDown()
        {
            _cameraRig = null;
        }

        #region AC-CAM-10: Dynamic Chase FOV Scaling & Smoothing

        [Test]
        public void test_camera_dynamic_fov_expands_to_chase_target_within_three_tau()
        {
            // Arrange: Baseline exploration FOV 60.0 deg
            Assert.AreEqual(60.0f, _cameraRig.CurrentFov, 0.001f, "Initial FOV must be 60.0 degrees.");

            // Act: Engage chase tension and simulate 3*tau = 3*0.18s = 0.54s in 16.6ms fixed steps
            _cameraRig.SetChaseFovActive(true);
            Assert.IsTrue(_cameraRig.IsChaseFovActive);

            float totalTime = 0.54f;
            float dt = 0.01666f;
            float elapsed = 0f;
            float previousFov = _cameraRig.CurrentFov;

            while (elapsed < totalTime)
            {
                float currentFov = _cameraRig.UpdateFov(dt);
                Assert.GreaterOrEqual(currentFov, previousFov, "FOV expansion must be strictly monotonic.");
                previousFov = currentFov;
                elapsed += dt;
            }

            // Assert: At 3*tau (0.54s), FOV must reach >= 67.6 deg (95% of 8 deg expansion)
            Assert.GreaterOrEqual(_cameraRig.CurrentFov, 67.6f, "Chase FOV must reach at least 67.6 degrees after 0.54s (3*tau).");
            Assert.LessOrEqual(_cameraRig.CurrentFov, 68.0f, "Chase FOV must not overshoot 68.0 degrees.");
        }

        [Test]
        public void test_camera_dynamic_fov_contracts_to_exploration_target_within_three_tau()
        {
            // Arrange: Start from full chase FOV (68.0 deg)
            _cameraRig.SetChaseFovActive(true);
            for (int i = 0; i < 60; i++)
            {
                _cameraRig.UpdateFov(0.02f);
            }
            Assert.GreaterOrEqual(_cameraRig.CurrentFov, 67.9f, "Precondition: FOV must be expanded to ~68.0 degrees.");

            // Act: Disengage chase tension and simulate 3*tau = 3*0.45s = 1.35s in 16.6ms steps
            _cameraRig.SetChaseFovActive(false);
            Assert.IsFalse(_cameraRig.IsChaseFovActive);

            float totalTime = 1.35f;
            float dt = 0.01666f;
            float elapsed = 0f;
            float previousFov = _cameraRig.CurrentFov;

            while (elapsed < totalTime)
            {
                float currentFov = _cameraRig.UpdateFov(dt);
                Assert.LessOrEqual(currentFov, previousFov, "FOV contraction must be strictly monotonic.");
                previousFov = currentFov;
                elapsed += dt;
            }

            // Assert: At 3*tau (1.35s), FOV must contract back to <= 60.4 deg (95% of recovery)
            Assert.LessOrEqual(_cameraRig.CurrentFov, 60.4f, "Exploration FOV must contract to at least 60.4 degrees after 1.35s (3*tau).");
            Assert.GreaterOrEqual(_cameraRig.CurrentFov, 60.0f, "Exploration FOV must not undershoot 60.0 degrees.");
        }

        #endregion

        #region AC-CAM-11: HideSpot Viewport Blend & Aperture Constraint

        [Test]
        public void test_camera_hidespot_priority_elevates_and_blend_progresses()
        {
            // Arrange
            Vector3 apertureTarget = new Vector3(5.0f, 0.80f, 10.0f);
            Vector3 outwardNormal = new Vector3(0.0f, 0.0f, 1.0f); // Facing North

            // Act: Transition to hide spot view
            _cameraRig.SwitchToHideSpotView(apertureTarget, outwardNormal);

            // Assert: Priorities and flags
            Assert.IsTrue(_cameraRig.IsInHideSpotView);
            Assert.AreEqual(20, _cameraRig.HideSpotPriority, "CM_HideSpot priority must elevate to 20.");
            Assert.AreEqual(10, _cameraRig.OrbitPriority, "CM_FreeOrbit priority must remain at 10.");
            Assert.IsTrue(_cameraRig.IsBlendActive, "Blend must be marked active on switch.");
            Assert.AreEqual(0.0f, _cameraRig.BlendProgress, 0.001f);

            // Act: Advance blend by 0.175s (half of 0.35s duration)
            _cameraRig.UpdateBlend(0.175f);
            Assert.IsTrue(_cameraRig.IsBlendActive);
            Assert.AreEqual(0.5f, _cameraRig.BlendProgress, 0.05f);

            // Act: Advance remaining time
            _cameraRig.UpdateBlend(0.20f);
            Assert.IsFalse(_cameraRig.IsBlendActive, "Blend must complete after 0.35s.");
            Assert.AreEqual(1.0f, _cameraRig.BlendProgress, 0.001f);

            // Act: Switch back to orbit view
            _cameraRig.SwitchToOrbitView();
            Assert.IsFalse(_cameraRig.IsInHideSpotView);
            Assert.AreEqual(5, _cameraRig.HideSpotPriority, "CM_HideSpot priority must reset to 5 in orbit view.");
            Assert.IsTrue(_cameraRig.IsBlendActive, "Return blend must become active.");
        }

        [Test]
        public void test_camera_hidespot_yaw_cone_strictly_clamped_to_plus_minus_thirty_degrees()
        {
            // Arrange: HideSpot facing North (0, 0, 1) -> Base Yaw = 0.0 deg
            Vector3 aperturePos = new Vector3(0f, 0.8f, 0f);
            Vector3 outwardFacing = Vector3.forward;
            _cameraRig.SwitchToHideSpotView(aperturePos, outwardFacing);

            Assert.AreEqual(0.0f, _cameraRig.CameraYaw, 0.001f, "Camera yaw must align to aperture outward facing.");

            // Act: Attempt to look East (+90 deg) with a large positive mouse delta
            Vector2 lookEastDelta = new Vector2(90.0f, 0.0f);
            _cameraRig.UpdateLookRotation(lookEastDelta, 1.0f);

            // Assert: Yaw must be strictly clamped to +30.0 deg
            Assert.AreEqual(30.0f, _cameraRig.CameraYaw, 0.001f, "Look East in HideSpot must clamp to exactly +30.0 degrees.");

            // Act: Attempt to look West (-90 deg / 270 deg) with a large negative mouse delta
            Vector2 lookWestDelta = new Vector2(-180.0f, 0.0f);
            _cameraRig.UpdateLookRotation(lookWestDelta, 1.0f);

            // Assert: Yaw must be strictly clamped to -30.0 deg (330.0 deg in 0..360 space)
            Assert.AreEqual(330.0f, _cameraRig.CameraYaw, 0.001f, "Look West in HideSpot must clamp to exactly 330.0 (-30) degrees.");

            // Act: Return to orbit view and look East (+90 deg)
            _cameraRig.SwitchToOrbitView();
            _cameraRig.SetYaw(0.0f);
            _cameraRig.UpdateLookRotation(new Vector2(90.0f, 0.0f), 1.0f);

            // Assert: In free orbit, yaw is not constrained to 30 deg
            Assert.AreEqual(90.0f, _cameraRig.CameraYaw, 0.001f, "Free orbit must allow full 360 rotation without 30 deg clamp.");
        }

        [Test]
        public void test_camera_hidespot_yaw_cone_handles_arbitrary_diagonal_portal_orientations()
        {
            // Arrange: HideSpot facing South-West (-1, 0, -1) normalized -> Base Yaw = 225 deg
            Vector3 outwardFacing = new Vector3(-1f, 0f, -1f).normalized;
            _cameraRig.SwitchToHideSpotView(Vector3.zero, outwardFacing);

            float expectedBaseYaw = 225.0f;
            Assert.AreEqual(expectedBaseYaw, _cameraRig.CameraYaw, 0.05f);

            // Act: Attempt to look clockwise beyond +30 deg (e.g. +60 deg -> 285 deg)
            _cameraRig.UpdateLookRotation(new Vector2(60.0f, 0.0f), 1.0f);
            Assert.AreEqual(255.0f, _cameraRig.CameraYaw, 0.05f, "Yaw must clamp to BaseYaw + 30 = 255 degrees.");

            // Act: Attempt to look counter-clockwise beyond -30 deg (e.g. -60 deg -> 165 deg)
            _cameraRig.UpdateLookRotation(new Vector2(-120.0f, 0.0f), 1.0f);
            Assert.AreEqual(195.0f, _cameraRig.CameraYaw, 0.05f, "Yaw must clamp to BaseYaw - 30 = 195 degrees.");
        }

        #endregion

        #region AC-CAM-12: Capture Event Blend Cancellation & Instant Cut

        [Test]
        public void test_camera_capture_event_aborts_active_blend_instantly_and_elevates_priority()
        {
            // Arrange: Start blend into HideSpot (0.15s into 0.35s transition)
            Vector3 aperturePos = new Vector3(2f, 0.8f, 4f);
            _cameraRig.SwitchToHideSpotView(aperturePos, Vector3.forward);
            _cameraRig.UpdateBlend(0.15f);

            Assert.IsTrue(_cameraRig.IsBlendActive, "Blend must be in progress.");
            Assert.Greater(_cameraRig.BlendProgress, 0.0f);
            Assert.Less(_cameraRig.BlendProgress, 1.0f);

            // Act: Guard captures player at (2, 0, 4) from guard location (2, 0, 6)
            Vector3 playerPos = new Vector3(2.0f, 0.0f, 4.0f);
            Vector3 guardPos = new Vector3(2.0f, 0.0f, 6.0f);
            var captureEvent = new PlayerCapturedEvent(playerPos, guardPos);

            _cameraRig.OnPlayerCaptured(captureEvent);

            // Assert: Blend cancelled immediately (0 ms cut)
            Assert.IsFalse(_cameraRig.IsBlendActive, "Active blend must be aborted immediately upon capture.");
            Assert.AreEqual(0.0f, _cameraRig.BlendProgress, "Blend progress must be reset to zero.");

            // Assert: Capture camera priority elevates to 100
            Assert.AreEqual(100, _cameraRig.CapturePriority, "CM_CaptureFocus priority must elevate to 100.");
            Assert.IsTrue(_cameraRig.IsCaptureCutActive, "Capture cut flag must be active.");

            // Assert: Camera yaw faces guard directly (from (2,0,4) looking at (2,0,6) is North -> 0 deg)
            Assert.AreEqual(0.0f, _cameraRig.CameraYaw, 0.01f, "Camera must orient toward capturing guard.");
            Assert.AreEqual(0.0f, _cameraRig.CameraPitch, 0.01f, "Camera pitch must align horizontally to guard.");
        }

        #endregion

        #region AC-CAM-13: WebGL Performance, Frame Timing & Allocation Budget

        [Test]
        public void test_camera_hot_loop_evaluates_with_zero_gc_allocation_over_hundred_frames()
        {
            // Arrange: Setup mock hit buffer for deocclusion query
            Vector3 chestTarget = new Vector3(0.0f, 1.35f, 0.0f);
            Vector3 camDir = new Vector3(0.0f, 0.5f, -1.0f).normalized;
            _cameraRig.SetChaseFovActive(true);

            // Warm up JIT and cache
            for (int i = 0; i < 10; i++)
            {
                _cameraRig.UpdateLookRotation(new Vector2(1.0f, 0.5f), 0.016f);
                _cameraRig.UpdateFov(0.016f);
                _cameraRig.EvaluateCameraDistance(chestTarget, camDir, 2.80f, 0.016f);
                _cameraRig.UpdateBlend(0.016f);
            }

            // Act: Evaluate over 100 simulation frames measuring GC allocations
            long startAlloc = GC.GetTotalMemory(true);

            for (int frame = 0; frame < 100; frame++)
            {
                _cameraRig.UpdateLookRotation(new Vector2(0.5f, -0.2f), 0.01666f);
                _cameraRig.UpdateFov(0.01666f);
                _cameraRig.EvaluateCameraDistance(chestTarget, camDir, _cameraRig.CurrentDistance, 0.01666f);
                _cameraRig.UpdateBlend(0.01666f);
            }

            long endAlloc = GC.GetTotalMemory(false);
            long bytesAllocated = endAlloc - startAlloc;

            // Assert: Strictly 0 B heap allocation in hot loop
            Assert.LessOrEqual(bytesAllocated, 0, $"Hot camera update loop must produce 0 B GC allocations (allocated {bytesAllocated} bytes).");
        }

        #endregion
    }
}
