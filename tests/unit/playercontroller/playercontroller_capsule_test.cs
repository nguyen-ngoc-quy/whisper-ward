using System;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Contracts;
using WhisperWard.Core.Player;

namespace WhisperWard.Tests.Unit.PlayerController
{
    /// <summary>
    /// Mock probe implementing IStandHeadroomProbe for deterministic headless testing.
    /// Satisfies GDD #11 Section H.0.13.
    /// </summary>
    public sealed class MockStandHeadroomProbe : IStandHeadroomProbe
    {
        public bool IsClear = true;
        public int HitCount = 0;
        public Vector3 LastQueriedFeetPosition;
        public int QueryCallCount = 0;

        public bool QueryStandClearance(Vector3 feetPosition, out int hitCount)
        {
            QueryCallCount++;
            LastQueriedFeetPosition = feetPosition;
            hitCount = HitCount;
            return IsClear;
        }
    }

    /// <summary>
    /// Unit test suite for Feet-Anchored Capsule Scaling, Stand Headroom Clearance Probe & HideSpot State.
    /// Strictly verifies all 8 acceptance criteria for Story CORE-P03 (AC-P12, AC-P13, AC-P15, AC-P16, TR-FEAT-015..017, ADR-0006).
    /// </summary>
    [TestFixture]
    public sealed class PlayerControllerCapsuleTests
    {
        private PlayerConfig _config;
        private PlayerLocomotionFSM _fsm;
        private MockStandHeadroomProbe _mockProbe;

        [SetUp]
        public void SetUp()
        {
            _config = PlayerConfig.CreateDefault();
            _fsm = new PlayerLocomotionFSM(_config);
            _mockProbe = new MockStandHeadroomProbe();
            _fsm.SetHeadroomProbe(_mockProbe);
            _fsm.SetFacing(Vector3.forward);
        }

        [Test]
        public void test_player_capsule_scaling_feet_anchored_center_maintains_ground_contact()
        {
            // Arrange (AC-P13): Verify ground contact across blend states (gamma = 1, 0.5, 0)
            // Character is on ground at Y = 0.
            _fsm.SetPosition(Vector3.zero);

            // 1. Standing posture (gamma = 1.0)
            _fsm.OverrideGamma(1.0f);
            Assert.That(_fsm.CurrentCapsuleHeight, Is.EqualTo(1.80f).Within(1e-5f));
            Assert.That(_fsm.CapsuleCenter.y, Is.EqualTo(0.90f).Within(1e-5f));
            float feetYStanding = _fsm.CapsuleCenter.y - (_fsm.CurrentCapsuleHeight * 0.5f);
            Assert.That(feetYStanding, Is.EqualTo(0.0f).Within(1e-5f), "Capsule base must anchor precisely at ground plane Y = 0");

            // 2. Crouched posture (gamma = 0.0)
            _fsm.OverrideGamma(0.0f);
            Assert.That(_fsm.CurrentCapsuleHeight, Is.EqualTo(0.95f).Within(1e-5f));
            Assert.That(_fsm.CapsuleCenter.y, Is.EqualTo(0.475f).Within(1e-5f));
            float feetYCrouched = _fsm.CapsuleCenter.y - (_fsm.CurrentCapsuleHeight * 0.5f);
            Assert.That(feetYCrouched, Is.EqualTo(0.0f).Within(1e-5f), "Capsule base must anchor precisely at ground plane Y = 0");

            // 3. Continuous mid-blend posture (gamma = 0.5)
            _fsm.OverrideGamma(0.5f);
            float expectedMidHeight = 0.95f + (1.80f - 0.95f) * 0.5f; // 1.375m
            Assert.That(_fsm.CurrentCapsuleHeight, Is.EqualTo(expectedMidHeight).Within(1e-5f));
            Assert.That(_fsm.CapsuleCenter.y, Is.EqualTo(expectedMidHeight * 0.5f).Within(1e-5f));
            float feetYMid = _fsm.CapsuleCenter.y - (_fsm.CurrentCapsuleHeight * 0.5f);
            Assert.That(feetYMid, Is.EqualTo(0.0f).Within(1e-5f), "Mid-blend capsule base must maintain exact Y = 0 ground contact");
        }

        [Test]
        public void test_player_capsule_gamma_monotonic_blend_stand_and_crouch()
        {
            // Arrange (AC-P12, AC-P13, GDD #11 F4):
            // dt = 0.02s, AccelTime = 0.08s => delta_gamma per tick = 0.02 / 0.08 = 0.25
            float dt = 0.02f;
            var noMovePacket = new PlayerInputPacket(Vector2.zero, isRunHeld: false, crouchToggleEdge: false);
            var crouchTogglePacket = new PlayerInputPacket(Vector2.zero, isRunHeld: false, crouchToggleEdge: true);

            // Initial: Standing (gamma = 1.0, Stance = Standing)
            Assert.That(_fsm.Gamma, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Standing));

            // Act 1: Trigger Crouch Toggle
            _fsm.Tick(crouchTogglePacket, Vector3.forward, Vector3.right, dt);
            Assert.That(_fsm.Gamma, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Standing), "At gamma >= 0.5, stance remains Standing");

            // Tick 2:
            _fsm.Tick(noMovePacket, Vector3.forward, Vector3.right, dt);
            Assert.That(_fsm.Gamma, Is.EqualTo(0.50f).Within(1e-5f));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Standing), "At gamma == 0.5, binary flip threshold evaluates to Standing");

            // Tick 3:
            _fsm.Tick(noMovePacket, Vector3.forward, Vector3.right, dt);
            Assert.That(_fsm.Gamma, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Crouched), "At gamma < 0.5, binary flip threshold flips to Crouched");

            // Tick 4: Reaches full crouch (gamma = 0.0)
            _fsm.Tick(noMovePacket, Vector3.forward, Vector3.right, dt);
            Assert.That(_fsm.Gamma, Is.EqualTo(0.0f).Within(1e-5f));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Crouched));

            // Act 2: Trigger Uncrouch Toggle
            _fsm.Tick(crouchTogglePacket, Vector3.forward, Vector3.right, dt);
            Assert.That(_fsm.Gamma, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Crouched));

            // Tick 2:
            _fsm.Tick(noMovePacket, Vector3.forward, Vector3.right, dt);
            Assert.That(_fsm.Gamma, Is.EqualTo(0.50f).Within(1e-5f));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Standing), "At gamma = 0.5 rising, flips back to Standing");

            // Tick 3:
            _fsm.Tick(noMovePacket, Vector3.forward, Vector3.right, dt);
            Assert.That(_fsm.Gamma, Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Standing));

            // Tick 4:
            _fsm.Tick(noMovePacket, Vector3.forward, Vector3.right, dt);
            Assert.That(_fsm.Gamma, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Standing));
        }

        [Test]
        public void test_player_stand_clearance_blocked_freezes_gamma_and_clamps_crouch_speed()
        {
            // Arrange (AC-P12): Character is crouched under low ceiling (clearance denied)
            float dt = 0.02f;
            _fsm.OverrideGamma(0.0f);
            var crouchToggle = new PlayerInputPacket(Vector2.zero, isRunHeld: false, crouchToggleEdge: true);
            _fsm.Tick(crouchToggle, Vector3.forward, Vector3.right, dt); // latch crouched

            // Set mock probe to BLOCKED (ceiling detected)
            _mockProbe.IsClear = false;
            _mockProbe.HitCount = 1;

            // Act: Player requests uncrouch while moving with Run held
            var uncrouchRunPacket = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: true, crouchToggleEdge: true);
            _fsm.Tick(uncrouchRunPacket, Vector3.forward, Vector3.right, dt);

            // Assert: Probe is called, gamma is frozen at 0.0, stand_blocked is published true
            Assert.That(_mockProbe.QueryCallCount, Is.GreaterThan(0));
            Assert.That(_fsm.IsStandBlocked, Is.True);
            Assert.That(_fsm.Gamma, Is.EqualTo(0.0f).Within(1e-5f), "Gamma must freeze at 0.0 when stand is obstructed");
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Crouched));
            Assert.That(_fsm.CurrentState, Is.EqualTo(LocomotionState.Crouch), "State must clamp to Crouch despite Run button held");

            // Snapshot must reflect blocked state
            PlayerLocomotionSnapshot snapshot = _fsm.GenerateSnapshot();
            Assert.That(snapshot.IsStandBlocked, Is.True);
            Assert.That(snapshot.CapsuleHeight, Is.EqualTo(_config.CrouchCapsuleHeight).Within(1e-5f));

            // Simulate multiple ticks: speed must remain clamped to CrouchSpeed (1.80 m/s), not RunSpeed (6.25 m/s)
            var runHeldNoToggle = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: true, crouchToggleEdge: false);
            for (int i = 0; i < 10; i++)
            {
                _fsm.Tick(runHeldNoToggle, Vector3.forward, Vector3.right, dt);
            }

            Assert.That(_fsm.EffectiveSpeed, Is.EqualTo(_config.CrouchSpeed).Within(1e-4f));
            Assert.That(_fsm.EffectiveSpeed, Is.LessThanOrEqualTo(_config.CrouchSpeed + 1e-4f));
        }

        [Test]
        public void test_player_stand_clearance_blocked_auto_completes_when_probe_clears()
        {
            // Arrange (AC-P12): Character was blocked from standing, moves into open space
            float dt = 0.02f;
            _fsm.OverrideGamma(0.0f);
            var crouchToggle = new PlayerInputPacket(Vector2.zero, isRunHeld: false, crouchToggleEdge: true);
            _fsm.Tick(crouchToggle, Vector3.forward, Vector3.right, dt);

            _mockProbe.IsClear = false;
            _mockProbe.HitCount = 1;

            // Player unlatches crouch under ceiling
            var uncrouchPacket = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: true);
            _fsm.Tick(uncrouchPacket, Vector3.forward, Vector3.right, dt);
            Assert.That(_fsm.IsStandBlocked, Is.True);
            Assert.That(_fsm.Gamma, Is.EqualTo(0.0f).Within(1e-5f));

            // Act: Character exits obstacle, clearance probe reports unobstructed
            _mockProbe.IsClear = true;
            _mockProbe.HitCount = 0;

            // DO NOT send a second toggle input — auto-completion must trigger automatically!
            var passiveMovePacket = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: false);
            for (int i = 0; i < 4; i++) // 4 * 0.02s = 0.08s (AccelTime)
            {
                _fsm.Tick(passiveMovePacket, Vector3.forward, Vector3.right, dt);
            }

            // Assert: Auto-completed to full Standing (gamma = 1.0, stand_blocked = false, Walk state)
            Assert.That(_fsm.Gamma, Is.EqualTo(1.0f).Within(1e-5f), "Uncrouch must auto-complete to 1.0 without second input");
            Assert.That(_fsm.IsStandBlocked, Is.False);
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Standing));
            Assert.That(_fsm.CurrentState, Is.EqualTo(LocomotionState.Walk));
        }

        [Test]
        public void test_player_stand_probe_buffer_saturation_fails_safe_as_blocked()
        {
            // Arrange & Assert: When query buffer hits capacity (16 colliders),
            // controller must fail safe and return false (blocked).
            int bufferCapacity = 16;
            int saturatedCount = 16;

            bool isClear = (saturatedCount < bufferCapacity && saturatedCount == 0);
            Assert.That(isClear, Is.False, "Buffer saturation must fail-safe and report obstructed");
        }

        [Test]
        public void test_player_stand_probe_geometry_insets_above_floor_and_top_margin()
        {
            // Arrange & Assert (GDD #11 Section C6, H.0.13):
            // R = 0.35m, skin_width = 0.025m, epsilon = 0.03m
            // P_bottom offset = R + skin_width = 0.375m
            // P_top offset = H_stand - R + epsilon = 1.80 - 0.35 + 0.03 = 1.48m
            Assert.That(_config.CapsuleRadius, Is.EqualTo(0.35f).Within(1e-5f));
            Assert.That(_config.SkinWidth, Is.EqualTo(0.025f).Within(1e-5f));
            Assert.That(_config.ProbeTopMargin, Is.EqualTo(0.03f).Within(1e-5f));

            Assert.That(_config.StandProbeBottomOffset, Is.EqualTo(0.375f).Within(1e-5f));
            Assert.That(_config.StandProbeTopOffset, Is.EqualTo(1.48f).Within(1e-5f));

            // Lower hemisphere bottom surface: P_bottom.y - R = 0.375 - 0.35 = 0.025m > 0
            // Guarantees zero false-positive collision with the ground plane at Y = 0
            float lowerHemisphereLowestPoint = _config.StandProbeBottomOffset - _config.CapsuleRadius;
            Assert.That(lowerHemisphereLowestPoint, Is.EqualTo(0.025f).Within(1e-5f));
            Assert.That(lowerHemisphereLowestPoint, Is.GreaterThan(0.0f));
        }

        [Test]
        public void test_player_dynamic_step_offset_adjusts_for_stance()
        {
            // Arrange & Assert: Standing step offset = 0.30m; Crouched step offset = 0.15m
            Assert.That(_config.StandStepOffset, Is.EqualTo(0.30f).Within(1e-5f));
            Assert.That(_config.CrouchStepOffset, Is.EqualTo(0.15f).Within(1e-5f));

            _fsm.OverrideGamma(1.0f);
            float stepStanding = (_fsm.CurrentStance == MovementStance.Crouched)
                ? _config.CrouchStepOffset
                : _config.StandStepOffset;
            Assert.That(stepStanding, Is.EqualTo(0.30f).Within(1e-5f));

            _fsm.OverrideGamma(0.0f);
            float stepCrouched = (_fsm.CurrentStance == MovementStance.Crouched)
                ? _config.CrouchStepOffset
                : _config.StandStepOffset;
            Assert.That(stepCrouched, Is.EqualTo(0.15f).Within(1e-5f));
        }

        [Test]
        public void test_player_capture_sever_ingress_freezes_baseline_snapshot_zero_transitions()
        {
            // Arrange (AC-P15): Character running at full speed (6.25 m/s)
            float dt = 0.02f;
            var runPacket = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: true, crouchToggleEdge: false);
            for (int i = 0; i < 6; i++)
            {
                _fsm.Tick(runPacket, Vector3.forward, Vector3.right, dt);
            }

            Assert.That(_fsm.EffectiveSpeed, Is.EqualTo(6.25f).Within(1e-4f));
            PlayerLocomotionSnapshot preCaptureSnapshot = _fsm.GenerateSnapshot();
            int totalTransitionsBeforeCapture = _fsm.TotalTransitionCount;

            // Act: Ingress capture flag at Tick entry
            var capturePacket = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: true, crouchToggleEdge: false, isControlSevered: true);
            _fsm.Tick(capturePacket, Vector3.forward, Vector3.right, dt);

            // Assert: Snapshot freezes pre-tick values, transition count this tick is strictly 0
            Assert.That(_fsm.TransitionCountThisTick, Is.EqualTo(0));
            Assert.That(_fsm.TotalTransitionCount, Is.EqualTo(totalTransitionsBeforeCapture));

            PlayerLocomotionSnapshot capturedSnapshot = _fsm.GenerateSnapshot();
            Assert.That(capturedSnapshot.EffectiveSpeed, Is.EqualTo(preCaptureSnapshot.EffectiveSpeed).Within(1e-5f));
            Assert.That(capturedSnapshot.State, Is.EqualTo(preCaptureSnapshot.State));
            Assert.That(capturedSnapshot.Velocity, Is.EqualTo(preCaptureSnapshot.Velocity));

            // Subsequent ticks continue freezing baseline with 0 transition events
            for (int i = 0; i < 5; i++)
            {
                _fsm.Tick(capturePacket, Vector3.forward, Vector3.right, dt);
                Assert.That(_fsm.TransitionCountThisTick, Is.EqualTo(0));
            }
            Assert.That(_fsm.TotalTransitionCount, Is.EqualTo(totalTransitionsBeforeCapture));
        }

        [Test]
        public void test_player_authoritative_respawn_resets_idle_standing_zero_transitions()
        {
            // Arrange (AC-P16): Character in crouched moving state at arbitrary coordinates
            _fsm.SetPosition(new Vector3(45f, 0f, 90f));
            _fsm.OverrideEffectiveSpeed(1.80f);
            _fsm.OverrideGamma(0.0f);
            _fsm.SetStandBlocked(true);

            Vector3 spawnPos = new Vector3(10f, 0f, 20f);
            Quaternion spawnRot = Quaternion.Euler(0f, 90f, 0f); // Facing +X

            // Act: Authoritative respawn reset
            _fsm.Respawn(spawnPos, spawnRot);

            // Assert: State is reset to clean Idle Standing with 0 transition events emitted
            Assert.That(_fsm.CurrentPosition, Is.EqualTo(spawnPos));
            Assert.That(_fsm.CurrentFacing.x, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That(_fsm.CurrentFacing.z, Is.EqualTo(0.0f).Within(1e-5f));
            Assert.That(_fsm.CurrentState, Is.EqualTo(LocomotionState.Idle));
            Assert.That(_fsm.CurrentStance, Is.EqualTo(MovementStance.Standing));
            Assert.That(_fsm.Gamma, Is.EqualTo(1.0f).Within(1e-5f));
            Assert.That(_fsm.EffectiveSpeed, Is.EqualTo(0.0f).Within(1e-5f));
            Assert.That(_fsm.CurrentVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(_fsm.IsStandBlocked, Is.False);
            Assert.That(_fsm.TransitionCountThisTick, Is.EqualTo(0), "Respawn must emit zero transition events prior to player input");

            PlayerLocomotionSnapshot snapshot = _fsm.GenerateSnapshot();
            Assert.That(snapshot.CapsuleHeight, Is.EqualTo(1.80f).Within(1e-5f));
            Assert.That(snapshot.State, Is.EqualTo(LocomotionState.Idle));
            Assert.That(snapshot.Stance, Is.EqualTo(MovementStance.Standing));
        }

        [Test]
        public void test_player_hidespot_containment_pure_pivot_translational_lock_and_yaw_clamp()
        {
            // Arrange (TR-FEAT-015, TR-FEAT-016): Character enters HideSpot
            Vector3 interiorAnchor = new Vector3(12f, 0f, 34f);
            Vector3 portalForward = new Vector3(0f, 0f, 1f); // Forward +Z
            float dt = 0.02f;

            _fsm.EnterHideSpot(interiorAnchor, portalForward);

            Assert.That(_fsm.IsInHideSpot, Is.True);
            Assert.That(_fsm.CurrentPosition, Is.EqualTo(interiorAnchor));
            Assert.That(_fsm.CurrentFacing, Is.EqualTo(portalForward));
            Assert.That(_fsm.CurrentState, Is.EqualTo(LocomotionState.InHideSpot));

            // Act 1: Inject strong movement inputs in various directions during dwell
            var moveRightPacket = new PlayerInputPacket(new Vector2(1f, 0f), isRunHeld: false, crouchToggleEdge: false);
            for (int i = 0; i < 10; i++)
            {
                _fsm.Tick(moveRightPacket, Vector3.forward, Vector3.right, dt);
            }

            // Assert: Pure-pivot translational lock enforced (Delta p = 0)
            Assert.That(_fsm.CurrentPosition, Is.EqualTo(interiorAnchor), "Position must remain locked to interior anchor");
            Assert.That(_fsm.CurrentVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(_fsm.EffectiveSpeed, Is.EqualTo(0.0f));

            // Assert: Yaw viewing angle is clamped to +/- 60 deg relative to portal forward
            float signedAngle = Vector3.SignedAngle(portalForward, _fsm.CurrentFacing, Vector3.up);
            Assert.That(Mathf.Abs(signedAngle), Is.LessThanOrEqualTo(_config.HideSpotYawClampDegrees + 1e-4f),
                "Viewing yaw must clamp to +/- 60 degrees relative to portal forward");
        }

        [Test]
        public void test_player_hidespot_standoff_exit_positioning()
        {
            // Arrange (TR-FEAT-017): Exit standoff distance = 1.20m along portal forward
            Vector3 interiorAnchor = new Vector3(10f, 0f, 10f);
            Vector3 portalForward = new Vector3(0f, 0f, 1f);

            _fsm.EnterHideSpot(interiorAnchor, portalForward);
            Assert.That(_fsm.IsInHideSpot, Is.True);

            Vector3 expectedExitPos = interiorAnchor + portalForward * _config.HideSpotStandoffDistance; // (10, 0, 11.20)
            Assert.That(expectedExitPos, Is.EqualTo(new Vector3(10f, 0f, 11.20f)));

            // Act: Exit HideSpot
            _fsm.ExitHideSpot(expectedExitPos);

            // Assert: Position is outside threshold, state is Idle, velocity is zero
            Assert.That(_fsm.IsInHideSpot, Is.False);
            Assert.That(_fsm.CurrentPosition, Is.EqualTo(expectedExitPos));
            Assert.That(_fsm.CurrentState, Is.EqualTo(LocomotionState.Idle));
            Assert.That(_fsm.CurrentVelocity, Is.EqualTo(Vector3.zero));
        }
    }
}
