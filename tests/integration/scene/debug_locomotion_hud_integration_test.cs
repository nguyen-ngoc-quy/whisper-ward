using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Camera;
using WhisperWard.Core.Contracts;
using WhisperWard.Core.Player;

namespace WhisperWard.Tests.Integration.Scene
{
    /// <summary>
    /// Integration test suite for Story SCENE-05 (On-Screen Locomotion Debug HUD & Playtest Verification Suite).
    /// Validates real-time telemetry polling for speed, stance, headroom clearance, camera distance, and FOV.
    /// Governed by ADR-0006, ADR-0008, AC-SCENE-16, and AC-SCENE-17.
    /// </summary>
    [TestFixture]
    public sealed class DebugLocomotionHUDIntegrationTest
    {
        private GameObject _playerGo;
        private CharacterController _cc;
        private PlayerThirdPersonController _controller;
        private GameObject _camGo;
        private UnityEngine.Camera _cam;
        private CameraOrbitDriver _orbitDriver;
        private GameObject _hudGo;
        private DebugLocomotionHUD _hud;

        [SetUp]
        public void SetUp()
        {
            // 1. Create Player with CharacterController & ThirdPersonController
            _playerGo = new GameObject("Test_Player");
            _playerGo.tag = "Player";
            _playerGo.layer = 6;

            _cc = _playerGo.AddComponent<CharacterController>();
            _cc.radius = 0.30f;
            _cc.height = 1.80f;
            _cc.center = new Vector3(0f, 0.90f, 0f);

            _controller = _playerGo.AddComponent<PlayerThirdPersonController>();
            _controller.InitializeController();

            // 2. Create Camera with CameraOrbitDriver
            _camGo = new GameObject("Test_Camera");
            _camGo.tag = "MainCamera";
            _cam = _camGo.AddComponent<UnityEngine.Camera>();
            _cam.fieldOfView = 60.0f;

            _orbitDriver = _camGo.AddComponent<CameraOrbitDriver>();
            _orbitDriver.Configure(_playerGo.transform, _cam);

            // 3. Create DebugLocomotionHUD
            _hudGo = new GameObject("Test_DebugLocomotionHUD");
            _hud = _hudGo.AddComponent<DebugLocomotionHUD>();
            _hud.Configure(_controller, _orbitDriver);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hudGo != null) Object.DestroyImmediate(_hudGo);
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
        }

        [Test]
        public void test_debug_locomotion_hud_initial_telemetry_matches_default_metrics()
        {
            // Act: refresh telemetry
            _hud.RefreshTelemetry(force: true);

            // Assert: verify initial telemetry matches idle stand state (AC-SCENE-16)
            Assert.That(_hud.LastSpeed, Is.EqualTo(0.0f).Within(1e-3f));
            Assert.That(_hud.LastStance, Is.EqualTo(MovementStance.Stand));
            Assert.That(_hud.LastCapsuleHeight, Is.EqualTo(1.80f).Within(1e-3f));
            Assert.That(_hud.LastHeadroomBlocked, Is.False);
            Assert.That(_hud.LastCameraDistance, Is.EqualTo(2.80f).Within(1e-3f));
            Assert.That(_hud.LastFov, Is.EqualTo(60.0f).Within(1e-3f));

            string text = _hud.CachedTelemetryText;
            Assert.That(text, Does.Contain("Locomotion Speed : 0.00 m/s"));
            Assert.That(text, Does.Contain("Physical Stance  : Stand"));
            Assert.That(text, Does.Contain("CLEAR (Full Height)"));
            Assert.That(text, Does.Contain("Camera Distance  : 2.80 m"));
            Assert.That(text, Does.Contain("FOV 60.0°"));
        }

        [Test]
        public void test_debug_locomotion_hud_player_movement_updates_speed_telemetry()
        {
            // Arrange: Apply continuous forward run input over several simulation ticks
            PlayerInputPacket runInput = new PlayerInputPacket(
                moveAxes: new Vector2(0f, 1f),
                isRunHeld: true,
                crouchToggleEdge: false
            );

            for (int i = 0; i < 20; i++)
            {
                _controller.Tick(runInput, 0.016f);
            }

            // Act: Refresh telemetry
            _hud.RefreshTelemetry(force: true);

            // Assert: Speed should be strictly positive (accelerated toward cruise speed)
            Assert.That(_hud.LastSpeed, Is.GreaterThan(1.0f));
            Assert.That(_hud.CachedTelemetryText, Does.Contain(_hud.LastSpeed.ToString("F2")));
        }

        [Test]
        public void test_debug_locomotion_hud_crouch_toggle_updates_stance_and_height_telemetry()
        {
            // Arrange: Trigger crouch toggle edge
            PlayerInputPacket crouchInput = new PlayerInputPacket(
                moveAxes: Vector2.zero,
                isRunHeld: false,
                crouchToggleEdge: true
            );
            _controller.Tick(crouchInput, 0.016f);

            // Act: Advance stance blend simulation
            for (int i = 0; i < 20; i++)
            {
                _controller.Tick(new PlayerInputPacket(Vector2.zero, false, false), 0.02f);
            }
            _hud.RefreshTelemetry(force: true);

            // Assert: Stance must register as Crouch and capsule height reduced towards 1.20m
            Assert.That(_hud.LastStance, Is.EqualTo(MovementStance.Crouch));
            Assert.That(_hud.LastCapsuleHeight, Is.LessThan(1.50f));
            Assert.That(_hud.CachedTelemetryText, Does.Contain("Physical Stance  : Crouch"));
        }

        [Test]
        public void test_debug_locomotion_hud_chase_fov_and_orbit_updates_camera_telemetry()
        {
            // Arrange: Trigger chase tension on camera orbit rig
            _orbitDriver.RigService.SetChaseTension(true);
            for (int i = 0; i < 15; i++)
            {
                _orbitDriver.RigService.UpdateFov(0.02f);
            }

            // Act: Refresh telemetry
            _hud.RefreshTelemetry(force: true);

            // Assert: Camera FOV must expand smoothly toward 68 degrees
            Assert.That(_hud.LastFov, Is.GreaterThan(61.0f));
            Assert.That(_hud.CachedTelemetryText, Does.Contain("FOV " + _hud.LastFov.ToString("F1")));
        }

        [Test]
        public void test_debug_locomotion_hud_toggle_visibility_alters_show_hud_state()
        {
            // Assert: default is visible
            Assert.That(_hud.ShowHUD, Is.True);

            // Act: toggle off
            _hud.ShowHUD = false;
            Assert.That(_hud.ShowHUD, Is.False);

            // Act: toggle on
            _hud.ShowHUD = true;
            Assert.That(_hud.ShowHUD, Is.True);
        }
    }
}
