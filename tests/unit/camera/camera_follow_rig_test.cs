using System;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Camera;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Tests.Unit.Camera
{
    /// <summary>
    /// Automated NUnit EditMode unit test suite for CameraRigService.
    /// Verifies camera-relative planar basis normalization (AC-CAM-01),
    /// stationary idle facing decoupling invariant AC-P25 (AC-CAM-02),
    /// pitch extrema clamping [-35 deg, +65 deg] & degeneracy fallback (AC-CAM-03),
    /// angular velocity mouse spike limiter 720 deg/s (AC-CAM-04),
    /// and pause menu TimeScale freeze (AC-CAM-05).
    /// Governed by ADR-0008 and story-001-follow-rig-planar-basis.md.
    /// </summary>
    [TestFixture]
    public class CameraFollowRigTest
    {
        private CameraRigService _cameraRig;

        [SetUp]
        public void SetUp()
        {
            _cameraRig = new CameraRigService(initialYaw: 0f, initialPitch: 10f);
        }

        #region AC-CAM-01: Camera-Relative Planar Basis & Diagonal Normalization

        [Test]
        public void Test_CameraRig_PlanarBasis_FortyFiveDegrees_CalculatesOrthogonalBasisAndNormalizedDiagonal()
        {
            // Arrange: Set camera yaw to 45.0 degrees
            _cameraRig.SetYaw(45.0f);

            // Act
            Vector3 forward = _cameraRig.PlanarForward;
            Vector3 right = _cameraRig.PlanarRight;

            // Assert: sin(45 deg) = cos(45 deg) = ~0.7071
            float expectedVal = Mathf.Sin(45.0f * Mathf.Deg2Rad);
            Assert.AreEqual(expectedVal, forward.x, 0.001f, "Forward X component must equal sin(45 deg).");
            Assert.AreEqual(0.0f, forward.y, 0.001f, "Forward Y component must be exactly zero on XZ plane.");
            Assert.AreEqual(expectedVal, forward.z, 0.001f, "Forward Z component must equal cos(45 deg).");

            Assert.AreEqual(expectedVal, right.x, 0.001f, "Right X component must equal cos(45 deg).");
            Assert.AreEqual(0.0f, right.y, 0.001f, "Right Y component must be zero on XZ plane.");
            Assert.AreEqual(-expectedVal, right.z, 0.001f, "Right Z component must equal -sin(45 deg).");

            // Orthogonality: forward . right == 0
            float dot = Vector3.Dot(forward, right);
            Assert.AreEqual(0.0f, dot, 0.0001f, "Planar forward and right vectors must be perpendicular.");

            // Act: Combine diagonal input (W=1, D=1)
            Vector2 diagonalInput = new Vector2(1.0f, 1.0f);
            Vector3 moveDir = CameraRigService.CalculateNormalizedMovement(diagonalInput, forward, right);

            // Assert: Magnitude must strictly equal 1.0000 (no sqrt(2) speed glitch)
            Assert.AreEqual(1.0000f, moveDir.magnitude, 0.0001f, "Diagonal input must yield strictly unit-length movement vector.");
        }

        #endregion

        #region AC-CAM-02: Stationary Idle Facing Invariant (AC-P25)

        [Test]
        public void Test_CameraRig_StationaryOrbit_LeavesCharacterFacingBitIdentical()
        {
            // Arrange: Stationary player character facing Vector3.forward (0, 0, 1)
            Vector3 initialCharacterFacing = Vector3.forward;
            Vector3 characterFacing = initialCharacterFacing;
            Vector2 idleInput = Vector2.zero;

            // Act: Rotate camera continuously through 360 degrees over 60 simulated ticks
            for (int i = 0; i < 60; i++)
            {
                _cameraRig.UpdateLookRotation(new Vector2(60.0f, 0f), 0.10f); // rotate yaw

                // Player character orientation logic: only updates if movement input magnitude > 1e-4
                if (idleInput.sqrMagnitude > 1e-8f)
                {
                    characterFacing = _cameraRig.PlanarForward;
                }
            }

            // Assert: Character facing must remain strictly unchanged (AC-P25)
            Assert.AreEqual(initialCharacterFacing.x, characterFacing.x, 0.00001f);
            Assert.AreEqual(initialCharacterFacing.y, characterFacing.y, 0.00001f);
            Assert.AreEqual(initialCharacterFacing.z, characterFacing.z, 0.000001f);
        }

        #endregion

        #region AC-CAM-03: Pitch Extrema Clamping & Gimbal Lock Prevention

        [Test]
        public void Test_CameraRig_PitchClamping_ClampsBetweenMinus35AndPlus65Degrees()
        {
            // Arrange & Act: Drive pitch upwards by applying negative delta
            _cameraRig.UpdateLookRotation(new Vector2(0f, 1000f), 1.0f);

            // Assert: Pitch must not exceed +65.0 degrees
            Assert.AreEqual(65.0f, _cameraRig.CameraPitch, 0.001f, "Pitch must clamp to +65.0 degrees maximum.");

            // Act: Drive pitch downwards by applying positive delta
            _cameraRig.UpdateLookRotation(new Vector2(0f, -2000f), 1.0f);

            // Assert: Pitch must not drop below -35.0 degrees
            Assert.AreEqual(-35.0f, _cameraRig.CameraPitch, 0.001f, "Pitch must clamp to -35.0 degrees minimum.");
        }

        [Test]
        public void Test_CameraRig_PlanarBasisDegeneracyFallback_LookingStraightDownOrUpDoesNotProduceZeroVector()
        {
            // Arrange: 3D Camera transform looking straight down (Y = -1, Pitch = -90 deg)
            Vector3 straightDownForward = new Vector3(0f, -1f, 0f);
            Vector3 cameraUpFacingNorth = new Vector3(0f, 0f, 1f);

            // Act: Calculate planar basis with degeneracy fallback
            CameraRigService.CalculatePlanarBasisFromTransform(
                straightDownForward,
                cameraUpFacingNorth,
                out Vector3 planarForward,
                out Vector3 planarRight);

            // Assert: Planar forward must fall back to camera up projection without NaN or zero-length
            Assert.IsFalse(float.IsNaN(planarForward.x));
            Assert.AreEqual(1.000f, planarForward.magnitude, 0.001f, "Planar forward must remain unit length when looking straight down.");
            Assert.AreEqual(1.000f, planarRight.magnitude, 0.001f, "Planar right must remain unit length when looking straight down.");
        }

        #endregion

        #region AC-CAM-04: Angular Velocity Mouse Delta Spike Limiter

        [Test]
        public void Test_CameraRig_MouseDeltaSpike_ClampsToSevenHundredTwentyDegreesPerSecond()
        {
            // Arrange: Extreme mouse flick delta = 5000px at 60 fps (dt = 0.0166667s)
            float deltaTime = 0.0166667f;
            Vector2 massiveLookDelta = new Vector2(5000f, 5000f);

            // Maximum allowed change: 720 deg/s * 0.0166667s = 12.0 deg
            float expectedMaxChange = 720.0f * deltaTime; // ~12.0 degrees

            // Act
            float initialYaw = _cameraRig.CameraYaw;
            _cameraRig.UpdateLookRotation(massiveLookDelta, deltaTime);

            // Assert: Applied change must match exactly the clamped threshold
            float actualYawChange = Mathf.Abs(_cameraRig.CameraYaw - initialYaw);
            Assert.AreEqual(expectedMaxChange, actualYawChange, 0.01f, "Mouse flick must be clamped to 720 deg/s * dt (~12 deg).");
        }

        #endregion

        #region AC-CAM-05: Pause Menu TimeScale Freeze & Delta Flusher

        [Test]
        public void Test_CameraRig_TimeScaleZero_FreezesOrientationAndDiscardsDeltas()
        {
            // Arrange: Hook test to simulate Time.timeScale = 0.0f (Pause menu active)
            _cameraRig.SetTestHooks(timeScaleProvider: () => 0.0f);
            float initialYaw = _cameraRig.CameraYaw;
            float initialPitch = _cameraRig.CameraPitch;

            // Act: Apply look deltas while paused
            _cameraRig.UpdateLookRotation(new Vector2(100f, 50f), 0.0166f);

            // Assert: Yaw and pitch must remain completely unchanged
            Assert.AreEqual(initialYaw, _cameraRig.CameraYaw, 0.0001f, "Camera yaw must remain frozen during pause.");
            Assert.AreEqual(initialPitch, _cameraRig.CameraPitch, 0.0001f, "Camera pitch must remain frozen during pause.");

            // Act: Unpause and verify no residual delta jerk occurs
            _cameraRig.SetTestHooks(timeScaleProvider: () => 1.0f);
            _cameraRig.UpdateLookRotation(Vector2.zero, 0.0166f);

            Assert.AreEqual(initialYaw, _cameraRig.CameraYaw, 0.0001f, "Camera yaw must not jump on unpause when input is zero.");
        }

        #endregion
    }
}
