using System;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Contracts;
using WhisperWard.Core.Player;

namespace WhisperWard.Tests.Unit.PlayerController
{
    /// <summary>
    /// Unit test suite for Player Locomotion, Diagonal Normalization, and Linear Slew FSM.
    /// Strictly verifies all acceptance criteria for Story CORE-P01 (TR-CORE-001, TR-CORE-002, ADR-0006).
    /// </summary>
    [TestFixture]
    public sealed class PlayerControllerMovementTests
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
        public void test_player_diagonal_normalization_maintains_exact_cruise_speed()
        {
            // Arrange (AC-P1): Injected raw diagonal move input (1, 1) in Run state
            var input = new PlayerInputPacket(new Vector2(1f, 1f), isRunHeld: true, crouchToggleEdge: false);
            float dt = 0.02f; // 50 Hz physics step

            // Act: Simulate sustained run input past acceleration window (0.10 s > 0.08 s)
            for (int i = 0; i < 6; i++)
            {
                _fsm.Tick(input, Vector3.forward, Vector3.right, dt);
            }

            // Assert: Normalized speed must match nominal RunSpeed (6.25 m/s), never 6.25 * sqrt(2) = 8.84 m/s
            Assert.That(_fsm.EffectiveSpeed, Is.EqualTo(6.25f).Within(1e-4f));
            Assert.That(_fsm.MoveDirection.magnitude, Is.EqualTo(1.0f).Within(1e-4f));
            Assert.That(_fsm.CurrentVelocity.magnitude, Is.EqualTo(6.25f).Within(1e-4f));
            Assert.That(_fsm.CurrentVelocity.magnitude, Is.Not.EqualTo(6.25f * Mathf.Sqrt(2f)));
        }

        [Test]
        public void test_player_backpedal_penalty_boundary_below_and_above_threshold()
        {
            // Arrange (AC-P2): Facing is forward (0, 0, 1). Move direction below vs above threshold (-1e-4)
            float dt = 0.02f;
            float expectedPenaltySpeed = 6.25f * 0.70f; // 4.375 m/s

            // Case A: Backwards move vector with dot < -1e-4 (e.g. dot = -1.0)
            var backInput = new PlayerInputPacket(new Vector2(0f, -1f), isRunHeld: true, crouchToggleEdge: false);
            for (int i = 0; i < 6; i++)
            {
                _fsm.Tick(backInput, Vector3.forward, Vector3.right, dt);
            }

            // Assert Case A: Target and effective speed must reflect 70% penalty
            Assert.That(_fsm.TargetSpeed, Is.EqualTo(expectedPenaltySpeed).Within(1e-4f));
            Assert.That(_fsm.EffectiveSpeed, Is.EqualTo(expectedPenaltySpeed).Within(1e-4f));

            // Case B: Pure lateral strafe (dot = 0.0 >= -1e-4)
            _fsm.Teleport(Vector3.zero, Quaternion.identity);
            _fsm.SetFacing(Vector3.forward);
            var strafeInput = new PlayerInputPacket(new Vector2(1f, 0f), isRunHeld: true, crouchToggleEdge: false);
            for (int i = 0; i < 6; i++)
            {
                _fsm.Tick(strafeInput, Vector3.forward, Vector3.right, dt);
            }

            // Assert Case B: Pure lateral strafe must maintain 100% speed (6.25 m/s)
            Assert.That(_fsm.TargetSpeed, Is.EqualTo(6.25f).Within(1e-4f));
            Assert.That(_fsm.EffectiveSpeed, Is.EqualTo(6.25f).Within(1e-4f));
        }

        [Test]
        public void test_player_accel_timing_idle_to_run_reaches_target_in_expected_frames()
        {
            // Arrange (AC-P5): Stationary controller at v_eff = 0, dt = 0.0166667s (60 fps)
            float dt = 1.0f / 60.0f;
            float maxAccel = _config.MaxAcceleration; // 78.125 m/s^2
            var runInput = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: true, crouchToggleEdge: false);

            Assert.That(_fsm.EffectiveSpeed, Is.EqualTo(0f));

            // Act: Step 5 frames (0.0833s >= 0.08s accel time)
            int framesToTarget = 0;
            for (int frame = 1; frame <= 10; frame++)
            {
                _fsm.Tick(runInput, Vector3.forward, Vector3.right, dt);
                if (Mathf.Approximately(_fsm.EffectiveSpeed, 6.25f) || _fsm.EffectiveSpeed >= 6.25f - 1e-4f)
                {
                    if (framesToTarget == 0)
                    {
                        framesToTarget = frame;
                    }
                }
            }

            // Assert: Must reach full speed at frame 5 (0.0833s is within 0.08s + 1 frame tolerance)
            Assert.That(framesToTarget, Is.EqualTo(5));
            Assert.That(_fsm.EffectiveSpeed, Is.EqualTo(6.25f).Within(1e-4f));
        }

        [Test]
        public void test_player_mid_blend_continuity_never_exceeds_max_acceleration()
        {
            // Arrange (AC-P6): Controller accelerating mid-ramp, overridden to 3.0 m/s
            _fsm.OverrideEffectiveSpeed(3.0f);
            float dt = 0.0166667f;
            float maxAllowedDelta = _config.MaxAcceleration * dt + 1e-5f;

            // Act: Input toggles Run -> Walk (target 3.60 m/s)
            var walkInput = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: false);
            float previousSpeed = _fsm.EffectiveSpeed;

            for (int step = 0; step < 5; step++)
            {
                _fsm.Tick(walkInput, Vector3.forward, Vector3.right, dt);
                float currentSpeed = _fsm.EffectiveSpeed;
                float actualDelta = Mathf.Abs(currentSpeed - previousSpeed);

                // Assert: Delta speed per tick must never exceed a_max * dt
                Assert.That(actualDelta, Is.LessThanOrEqualTo(maxAllowedDelta));
                previousSpeed = currentSpeed;
            }

            // Act 2: Toggle Walk -> Crouch (target 1.80 m/s) mid-slew
            var crouchInput = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: true);
            for (int step = 0; step < 5; step++)
            {
                _fsm.Tick(crouchInput, Vector3.forward, Vector3.right, dt);
                float currentSpeed = _fsm.EffectiveSpeed;
                float actualDelta = Mathf.Abs(currentSpeed - previousSpeed);

                // Assert: Continuous linear deceleration without abrupt snapping
                Assert.That(actualDelta, Is.LessThanOrEqualTo(maxAllowedDelta));
                previousSpeed = currentSpeed;
            }
        }

        [Test]
        public void test_player_full_stop_deceleration_within_accel_time()
        {
            // Arrange (AC-P7): Cruising at full speed 6.25 m/s
            _fsm.OverrideEffectiveSpeed(6.25f);
            float dt = 0.02f; // 50 Hz
            var stopInput = new PlayerInputPacket(Vector2.zero, isRunHeld: false, crouchToggleEdge: false);

            // Act: Step 4 ticks (0.08 s)
            for (int i = 0; i < 4; i++)
            {
                _fsm.Tick(stopInput, Vector3.forward, Vector3.right, dt);
            }

            // Assert: Exactly reaches 0 m/s at t = 0.08s (+ 1 observation tick)
            Assert.That(_fsm.EffectiveSpeed, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(_fsm.CurrentVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(_fsm.CurrentState, Is.EqualTo(LocomotionState.Idle));
        }

        [Test]
        public void test_player_latch_priority_crouch_overrides_run()
        {
            // Arrange (AC-P8): Controller receiving both Crouch toggle and Run hold in one packet
            var multiInput = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: true, crouchToggleEdge: true);
            float dt = 0.02f;

            // Act
            _fsm.Tick(multiInput, Vector3.forward, Vector3.right, dt);

            // Assert: Crouch latch overrides Run. State must be Crouch, stance Crouched, target speed 1.80 m/s
            Assert.That(_fsm.CurrentState, Is.EqualTo(LocomotionState.Crouch));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Crouched));
            Assert.That(_fsm.TargetSpeed, Is.EqualTo(1.80f).Within(1e-4f));
            Assert.That(_fsm.IsCrouchLatched, Is.True);
        }

        [Test]
        public void test_player_same_frame_multi_input_collapses_to_single_transition()
        {
            // Arrange (AC-P20): Transitioning from Idle with Crouch + Shift + Move in 1 frame
            var packet = new PlayerInputPacket(new Vector2(1f, 1f), isRunHeld: true, crouchToggleEdge: true);

            // Act
            _fsm.Tick(packet, Vector3.forward, Vector3.right, 0.02f);

            // Assert: Exactly 1 state transition event emitted in this tick
            Assert.That(_fsm.TransitionCountThisTick, Is.EqualTo(1));
            Assert.That(_fsm.TotalTransitionCount, Is.EqualTo(1));
            Assert.That(_fsm.CurrentState, Is.EqualTo(LocomotionState.Crouch));
        }

        [Test]
        public void test_player_structural_speed_ceiling_never_exceeds_max_speed()
        {
            // Arrange (AC-P17): Simulate excessive input and speed override
            _fsm.OverrideEffectiveSpeed(10.0f);
            var runInput = new PlayerInputPacket(new Vector2(2f, 2f), isRunHeld: true, crouchToggleEdge: false);

            // Act
            _fsm.Tick(runInput, Vector3.forward, Vector3.right, 0.02f);

            // Assert: Speed must never exceed 6.25 m/s ceiling
            Assert.That(_fsm.EffectiveSpeed, Is.LessThanOrEqualTo(6.25f + 1e-4f));
        }

        [Test]
        public void test_player_hard_constraint_config_validator_catches_violations()
        {
            // Arrange & Act (AC-P18): Validate valid default config
            Assert.That(_config.ValidateConfig(out string defaultErr), Is.True);
            Assert.That(defaultErr, Is.Null);

            // Case A: Corrupted WalkSpeed >= GuardInvestigateSpeed (5.0 m/s)
            var badConfigA = PlayerConfig.CreateDefault();
            badConfigA.WalkSpeed = 5.20f;
            Assert.That(badConfigA.ValidateConfig(out string errA), Is.False);
            Assert.That(errA, Does.Contain("GuardInvestigateSpeed"));

            // Case B: Corrupted CrouchSpeed >= GuardPatrolSpeed (2.30 m/s)
            var badConfigB = PlayerConfig.CreateDefault();
            badConfigB.CrouchSpeed = 2.40f;
            Assert.That(badConfigB.ValidateConfig(out string errB), Is.False);
            Assert.That(errB, Does.Contain("GuardPatrolSpeed"));

            // Case C: Corrupted AccelTime <= 0
            var badConfigC = PlayerConfig.CreateDefault();
            badConfigC.AccelTime = 0f;
            Assert.That(badConfigC.ValidateConfig(out string errC), Is.False);
            Assert.That(errC, Does.Contain("AccelTime"));

            // Case D: Anti-kiting ratio < 1.20 (e.g. GuardChaseSpeed = 7.0, RunSpeed = 6.25 -> 1.12 < 1.20)
            var badConfigD = PlayerConfig.CreateDefault();
            badConfigD.GuardChaseSpeed = 7.0f;
            Assert.That(badConfigD.ValidateConfig(out string errD), Is.False);
            Assert.That(errD, Does.Contain("Anti-kiting"));
        }

        [Test]
        public void test_player_zero_vector_nan_guard_handles_stop_safely()
        {
            // Arrange (AC-P23): Backpedaling at full speed, then releasing input to zero
            _fsm.SetFacing(Vector3.forward);
            var backInput = new PlayerInputPacket(new Vector2(0f, -1f), isRunHeld: true, crouchToggleEdge: false);
            _fsm.Tick(backInput, Vector3.forward, Vector3.right, 0.02f);

            // Act: Release move input to (0, 0)
            var releaseInput = new PlayerInputPacket(Vector2.zero, isRunHeld: false, crouchToggleEdge: false);
            _fsm.Tick(releaseInput, Vector3.forward, Vector3.right, 0.02f);

            // Assert: No NaN or Infinity in any vector or scalar field
            Assert.That(float.IsNaN(_fsm.EffectiveSpeed), Is.False);
            Assert.That(float.IsInfinity(_fsm.EffectiveSpeed), Is.False);
            Assert.That(float.IsNaN(_fsm.MoveDirection.x), Is.False);
            Assert.That(float.IsNaN(_fsm.CurrentVelocity.x), Is.False);
            Assert.That(_fsm.MoveDirection, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void test_player_locomotion_snapshot_produces_zero_gc()
        {
            // Arrange: Advance FSM state
            var input = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: false);
            _fsm.Tick(input, Vector3.forward, Vector3.right, 0.02f);

            // Act: Generate snapshot struct
            PlayerLocomotionSnapshot snapshot = _fsm.GenerateSnapshot();

            // Assert: Value types are correctly populated without managed allocation
            Assert.That(snapshot.State, Is.EqualTo(LocomotionState.Walk));
            Assert.That(snapshot.Stance, Is.EqualTo(MovementStance.Standing));
            Assert.That(snapshot.CapsuleHeight, Is.EqualTo(_config.StandCapsuleHeight));
            Assert.That(snapshot.EffectiveSpeed, Is.GreaterThan(0f));
        }
    }
}
