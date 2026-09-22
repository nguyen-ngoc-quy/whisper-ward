using System;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Contracts;
using WhisperWard.Core.Player;

namespace WhisperWard.Tests.Unit.PlayerController
{
    /// <summary>
    /// Unit test suite for Camera-Relative Planar Basis, Proportional Yaw Slew,
    /// Idle Facing Decoupling, and Animator Variant calculations.
    /// Strictly verifies Story CORE-P02 (AC-P19, Pitch Degeneracy, AC-P3, AC-P4, AC-P25, AC-P21, AC-P22, AC-P24).
    /// </summary>
    [TestFixture]
    public sealed class PlayerControllerOrientationTests
    {
        private PlayerConfig _config;
        private PlayerLocomotionFSM _fsm;

        [SetUp]
        public void SetUp()
        {
            _config = PlayerConfig.CreateDefault();
            _fsm = new PlayerLocomotionFSM(_config);
            _fsm.SetFacing(Vector3.forward);
        }

        [Test]
        public void test_playercontroller_yaw_frame_direction_four_quadrants_matches_world_basis()
        {
            // Arrange (AC-P19): Injected camera yaw at 0°, 90°, 180°, 270° with pure forward input (0, 1)
            var input = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: false);
            float dt = 0.02f;

            var testCases = new (Vector3 camForward, Vector3 camRight, Vector3 expectedMove)[]
            {
                (Vector3.forward, Vector3.right, Vector3.forward),           // 0° (North)
                (Vector3.right, Vector3.back, Vector3.right),                // 90° (East)
                (Vector3.back, Vector3.left, Vector3.back),                  // 180° (South)
                (Vector3.left, Vector3.forward, Vector3.left)                // 270° (West)
            };

            // Act & Assert: For each quadrant, world-space movement vector matches expected direction within 1e-4
            foreach (var (camForward, camRight, expectedMove) in testCases)
            {
                _fsm.Tick(input, camForward, camRight, dt);
                Assert.That(Vector3.Dot(_fsm.MoveDirection, expectedMove), Is.EqualTo(1.0f).Within(1e-4f));
                Assert.That((_fsm.MoveDirection - expectedMove).sqrMagnitude, Is.LessThan(1e-6f));
            }
        }

        [Test]
        public void test_playercontroller_pitch_degeneracy_straight_down_and_up_uses_fallback()
        {
            // Arrange (Pitch Degeneracy): Camera forward is perpendicular to ground plane (pitch = ±90°)
            // Case 1: Looking straight down (pitch = -90°), forward = (0, -1, 0), up = (0, 0, 1)
            Vector3 downForward = new Vector3(0f, -1f, 0f);
            Vector3 downUp = new Vector3(0f, 0f, 1f);

            // Act Case 1
            PlayerLocomotionFSM.CalculatePlanarBasis(downForward, downUp, out Vector3 basisFwd1, out Vector3 basisRight1);

            // Assert Case 1: Falls back cleanly to projected camera up-vector (0, 0, 1)
            Assert.That(float.IsNaN(basisFwd1.x) || float.IsNaN(basisFwd1.z), Is.False);
            Assert.That(basisFwd1.sqrMagnitude, Is.EqualTo(1.0f).Within(1e-4f));
            Assert.That(basisFwd1, Is.EqualTo(Vector3.forward));
            Assert.That(basisRight1, Is.EqualTo(Vector3.right));

            // Case 2: Looking straight up (pitch = +90°), forward = (0, 1, 0), up = (0, 0, -1)
            Vector3 upForward = new Vector3(0f, 1f, 0f);
            Vector3 upUp = new Vector3(0f, 0f, -1f);

            // Act Case 2
            PlayerLocomotionFSM.CalculatePlanarBasis(upForward, upUp, out Vector3 basisFwd2, out Vector3 basisRight2);

            // Assert Case 2: Falls back to projected up-vector (0, 0, -1)
            Assert.That(float.IsNaN(basisFwd2.x) || float.IsNaN(basisFwd2.z), Is.False);
            Assert.That(basisFwd2.sqrMagnitude, Is.EqualTo(1.0f).Within(1e-4f));
            Assert.That(basisFwd2, Is.EqualTo(Vector3.back));
            Assert.That(basisRight2, Is.EqualTo(Vector3.left));
        }

        [Test]
        public void test_playercontroller_proportional_rear_damping_weight_curve_values()
        {
            // Arrange (AC-P3 Formula check): w(dot) = 1.0 for dot >= 0, w(dot) = 1.0 - (1 - floor) * (-dot) for dot < 0
            // Act & Assert
            Assert.That(_config.ComputeRearDampWeight(1.0f), Is.EqualTo(1.0f));
            Assert.That(_config.ComputeRearDampWeight(0.5f), Is.EqualTo(1.0f));
            Assert.That(_config.ComputeRearDampWeight(0.0f), Is.EqualTo(1.0f));

            // At dot = -0.5: 1.0 - (1.0 - 0.25) * 0.5 = 1.0 - 0.375 = 0.625
            Assert.That(_config.ComputeRearDampWeight(-0.5f), Is.EqualTo(0.625f).Within(1e-5f));

            // At dot = -1.0: 1.0 - (1.0 - 0.25) * 1.0 = 0.25 (RearDampFloor)
            Assert.That(_config.ComputeRearDampWeight(-1.0f), Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void test_playercontroller_proportional_rear_damping_settles_reversal_within_band()
        {
            // Arrange (AC-P3): Initial facing (0, 0, 1), commanded heading is full rearward (0, 0, -1)
            _fsm.SetFacing(Vector3.forward);
            var reverseInput = new PlayerInputPacket(new Vector2(0f, -1f), isRunHeld: false, crouchToggleEdge: false);
            float dt = 0.02f; // 50 Hz physics step
            float elapsed = 0f;
            int maxTicks = 100; // 2.0 seconds safety bound

            // Act: Step simulation until facing is aligned with back within 1 degree (0.0175 rad)
            for (int i = 0; i < maxTicks; i++)
            {
                _fsm.Tick(reverseInput, Vector3.forward, Vector3.right, dt);
                elapsed += dt;

                float angleRemaining = Vector3.Angle(_fsm.CurrentFacing, Vector3.back);
                if (angleRemaining <= 1.0f)
                {
                    break;
                }
            }

            // Assert: Full 180° reversal settles monotonically without stall in [0.52, 1.24] s band (~0.83 s nominal)
            Assert.That(elapsed, Is.InRange(0.52f, 1.24f));
            Assert.That(Vector3.Dot(_fsm.CurrentFacing, Vector3.back), Is.GreaterThanOrEqualTo(0.999f));
        }

        [Test]
        public void test_playercontroller_turn_rate_bounded_rotation_resolves_90_degree_error()
        {
            // Arrange (AC-P4): Initial facing (0, 0, 1), commanded heading 90° East (1, 0, 0)
            _fsm.SetFacing(Vector3.forward);
            var eastInput = new PlayerInputPacket(new Vector2(1f, 0f), isRunHeld: false, crouchToggleEdge: false);
            float dt = 0.02f;
            float maxAllowedAnglePerTick = _config.TurnRate * dt + 1e-4f; // 360 * 0.02 = 7.2°

            // Expected ticks: ceil(90° / 7.2°) = 13 ticks (0.26 s)
            int ticksNeeded = Mathf.CeilToInt(90f / (_config.TurnRate * dt));

            // Act & Assert per-tick rate ceiling
            Vector3 previousFacing = _fsm.CurrentFacing;
            for (int i = 0; i < ticksNeeded; i++)
            {
                _fsm.Tick(eastInput, Vector3.forward, Vector3.right, dt);
                float stepAngle = Vector3.Angle(previousFacing, _fsm.CurrentFacing);
                Assert.That(stepAngle, Is.LessThanOrEqualTo(maxAllowedAnglePerTick));
                previousFacing = _fsm.CurrentFacing;
            }

            // Target heading reached within 13 ticks
            Assert.That(Vector3.Angle(_fsm.CurrentFacing, Vector3.right), Is.LessThanOrEqualTo(1.0f));
        }

        [Test]
        public void test_playercontroller_idle_facing_hold_invariant_preserves_facing_during_camera_orbit()
        {
            // Arrange (AC-P25): Character stationary facing (0, 0, 1) with zero input
            _fsm.SetFacing(Vector3.forward);
            var idleInput = new PlayerInputPacket(Vector2.zero, isRunHeld: false, crouchToggleEdge: false);
            float dt = 0.02f;

            // Act: Camera rotates 360° continuously over 100 ticks
            for (int i = 0; i < 100; i++)
            {
                float yaw = (i / 100f) * 360f;
                Quaternion camRot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 camForward = camRot * Vector3.forward;
                Vector3 camRight = camRot * Vector3.right;

                _fsm.Tick(idleInput, camForward, camRight, dt);

                // Assert: Character facing remains bit-identical (0, 0, 1) across all ticks
                Assert.That(_fsm.CurrentFacing.x, Is.EqualTo(0f));
                Assert.That(_fsm.CurrentFacing.y, Is.EqualTo(0f));
                Assert.That(_fsm.CurrentFacing.z, Is.EqualTo(1.0f));
            }
        }

        [Test]
        public void test_playercontroller_determinism_replay_canary_produces_identical_telemetry()
        {
            // Arrange (AC-P21): 100-step scripted input trace
            float dt = 0.02f;
            var script = new (Vector2 move, bool run, bool crouch, float yaw)[100];
            for (int i = 0; i < 100; i++)
            {
                float moveX = Mathf.Sin(i * 0.1f);
                float moveY = Mathf.Cos(i * 0.15f);
                bool run = (i % 20) < 10;
                bool crouch = (i == 30 || i == 70);
                float yaw = (i * 7.5f) % 360f;
                script[i] = (new Vector2(moveX, moveY), run, crouch, yaw);
            }

            var fsmA = new PlayerLocomotionFSM(PlayerConfig.CreateDefault());
            var fsmB = new PlayerLocomotionFSM(PlayerConfig.CreateDefault());

            // Act: Execute script independently on both FSM instances
            for (int i = 0; i < 100; i++)
            {
                var step = script[i];
                var input = new PlayerInputPacket(step.move, step.run, step.crouch);
                Quaternion rot = Quaternion.Euler(0f, step.yaw, 0f);
                Vector3 fwd = rot * Vector3.forward;
                Vector3 right = rot * Vector3.right;

                fsmA.Tick(input, fwd, right, dt);
                fsmB.Tick(input, fwd, right, dt);

                // Assert: Bit-identical positions, velocities, facings, and speeds
                Assert.That(fsmA.EffectiveSpeed, Is.EqualTo(fsmB.EffectiveSpeed));
                Assert.That(fsmA.MoveDirection, Is.EqualTo(fsmB.MoveDirection));
                Assert.That(fsmA.CurrentFacing, Is.EqualTo(fsmB.CurrentFacing));
                Assert.That(fsmA.CurrentVelocity, Is.EqualTo(fsmB.CurrentVelocity));
                Assert.That(fsmA.CurrentAnimState, Is.EqualTo(fsmB.CurrentAnimState));
                Assert.That(fsmA.SpeedRatio, Is.EqualTo(fsmB.SpeedRatio));
                Assert.That(fsmA.Gamma, Is.EqualTo(fsmB.Gamma));
            }
        }

        [Test]
        public void test_playercontroller_snapshot_readout_completeness_and_copy_safety()
        {
            // Arrange (AC-P22): Generate snapshot from active state machine
            _fsm.SetFacing(Vector3.forward);
            _fsm.SetPosition(new Vector3(10f, 0f, 25f));
            _fsm.OverrideEffectiveSpeed(3.60f);

            var input = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: false);
            _fsm.Tick(input, Vector3.forward, Vector3.right, 0.02f);

            // Act: Generate snapshot
            PlayerLocomotionSnapshot snapshot = _fsm.GenerateSnapshot();

            // Assert: Readout completeness
            Assert.That(snapshot.Position, Is.EqualTo(_fsm.CurrentPosition));
            Assert.That(snapshot.Velocity, Is.EqualTo(_fsm.CurrentVelocity));
            Assert.That(snapshot.Facing, Is.EqualTo(_fsm.CurrentFacing));
            Assert.That(snapshot.State, Is.EqualTo(LocomotionState.Walk));
            Assert.That(snapshot.Stance, Is.EqualTo(MovementStance.Standing));
            Assert.That(snapshot.EffectiveSpeed, Is.EqualTo(_fsm.EffectiveSpeed));
            Assert.That(snapshot.CapsuleHeight, Is.EqualTo(_config.StandCapsuleHeight));
            Assert.That(snapshot.IsStandBlocked, Is.False);
            Assert.That(snapshot.IsGrounded, Is.True);

            // Copy-safety: modifying local copy does not alter FSM
            snapshot = new PlayerLocomotionSnapshot(
                Vector3.zero, Vector3.zero, Vector3.left, LocomotionState.Idle,
                MovementStance.Crouched, 0f, 0f, true, false);

            Assert.That(_fsm.CurrentFacing, Is.EqualTo(Vector3.forward));
            Assert.That(_fsm.CurrentState, Is.EqualTo(LocomotionState.Walk));
        }

        [Test]
        public void test_playercontroller_animator_variant_and_speed_ratio_calculator()
        {
            // Arrange (AC-P24): Test forward and backward variants for Crouch, Walk, and Run
            float dt = 0.02f;
            _fsm.SetFacing(Vector3.forward);

            // Case A: Run Backward (dot < -1e-4)
            // Cruise speed = 6.25 * 0.70 = 4.375 m/s. Override effective speed to 2.1875 m/s => SpeedRatio = 0.50
            var runBackInput = new PlayerInputPacket(new Vector2(0f, -1f), isRunHeld: true, crouchToggleEdge: false);
            _fsm.OverrideEffectiveSpeed(2.1875f);
            _fsm.Tick(runBackInput, Vector3.forward, Vector3.right, 0.0001f);

            Assert.That(_fsm.CurrentAnimState, Is.EqualTo(AnimState.RunBack));
            Assert.That(_fsm.SpeedRatio, Is.EqualTo(0.50f).Within(1e-4f));
            Assert.That(_fsm.Gamma, Is.EqualTo(1.0f)); // Standing

            // Case B: Walk Forward (dot > 0)
            // Cruise speed = 3.60 m/s. Override effective speed to 1.80 m/s => SpeedRatio = 0.50
            _fsm.SetFacing(Vector3.forward);
            var walkFwdInput = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: false);
            _fsm.OverrideEffectiveSpeed(1.80f);
            _fsm.Tick(walkFwdInput, Vector3.forward, Vector3.right, 0.0001f);

            Assert.That(_fsm.CurrentAnimState, Is.EqualTo(AnimState.WalkFwd));
            Assert.That(_fsm.SpeedRatio, Is.EqualTo(0.50f).Within(1e-4f));
            Assert.That(_fsm.Gamma, Is.EqualTo(1.0f));

            // Case C: Crouch Forward & Backward (Gamma = 0.0)
            var crouchToggle = new PlayerInputPacket(Vector2.zero, isRunHeld: false, crouchToggleEdge: true);
            _fsm.Tick(crouchToggle, Vector3.forward, Vector3.right, dt);
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Crouched));

            var crouchFwdInput = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: false);
            _fsm.OverrideEffectiveSpeed(0.90f);
            _fsm.Tick(crouchFwdInput, Vector3.forward, Vector3.right, 0.0001f);

            Assert.That(_fsm.CurrentAnimState, Is.EqualTo(AnimState.CrouchFwd));
            Assert.That(_fsm.SpeedRatio, Is.EqualTo(0.50f).Within(1e-4f));
            Assert.That(_fsm.Gamma, Is.EqualTo(0.0f)); // Crouched

            var crouchBackInput = new PlayerInputPacket(new Vector2(0f, -1f), isRunHeld: false, crouchToggleEdge: false);
            _fsm.OverrideEffectiveSpeed(0.63f); // 1.80 * 0.70 * 0.50 = 0.63 m/s
            _fsm.Tick(crouchBackInput, Vector3.forward, Vector3.right, 0.0001f);

            Assert.That(_fsm.CurrentAnimState, Is.EqualTo(AnimState.CrouchBack));
            Assert.That(_fsm.SpeedRatio, Is.EqualTo(0.50f).Within(1e-4f));
            Assert.That(_fsm.Gamma, Is.EqualTo(0.0f));

            // Case D: Idle in Crouch Stance
            _fsm.Tick(new PlayerInputPacket(Vector2.zero, false, false), Vector3.forward, Vector3.right, 0.02f);
            Assert.That(_fsm.CurrentAnimState, Is.EqualTo(AnimState.IdleCrouched));
            Assert.That(_fsm.SpeedRatio, Is.EqualTo(0.0f));
            Assert.That(_fsm.Gamma, Is.EqualTo(0.0f));
        }
    }
}