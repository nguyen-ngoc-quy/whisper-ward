using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Contracts;
using WhisperWard.Core.Player;

namespace WhisperWard.Tests.Integration.Scene
{
    /// <summary>
    /// Integration test suite for Story SCENE-02 (Player Capsule Prefab & Runtime Input Driver Integration).
    /// Verifies capsule dimensions, input injection, camera planar basis rotation, stance toggling,
    /// and low-headroom stand clearance rejection against AC-SCENE-05 through AC-SCENE-08 and ADR-0006.
    /// </summary>
    [TestFixture]
    public sealed class PlayerRuntimeDriverIntegrationTest
    {
        private GameObject _playerGo;
        private CharacterController _characterController;
        private PlayerThirdPersonController _controller;
        private PlayerRuntimeDriver _driver;
        private GameObject _cameraGo;
        private Camera _camera;
        private GameObject _visualGo;

        [SetUp]
        public void SetUp()
        {
            _playerGo = new GameObject("Test_Player_Capsule");
            _playerGo.tag = "Player";
            _playerGo.layer = 6; // Layer 6: Player

            _characterController = _playerGo.AddComponent<CharacterController>();
            _characterController.radius = 0.30f;
            _characterController.height = 1.80f;
            _characterController.center = new Vector3(0f, 0.90f, 0f);
            _characterController.skinWidth = 0.03f;
            _characterController.stepOffset = 0.30f;

            _controller = _playerGo.AddComponent<PlayerThirdPersonController>();
            _controller.InitializeController();

            _visualGo = new GameObject("Visual_Capsule");
            _visualGo.transform.SetParent(_playerGo.transform);
            _visualGo.transform.localPosition = new Vector3(0f, 0.90f, 0f);
            _visualGo.transform.localScale = new Vector3(0.6f, 0.90f, 0.6f);

            _cameraGo = new GameObject("Test_Reference_Camera");
            _camera = _cameraGo.AddComponent<Camera>();
            _cameraGo.transform.position = new Vector3(0f, 2f, -5f);
            _cameraGo.transform.rotation = Quaternion.identity; // Facing +Z forward

            _driver = _playerGo.AddComponent<PlayerRuntimeDriver>();
            _driver.Configure(_controller, _camera, _visualGo.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
        }

        [Test]
        public void test_player_capsule_initial_dimensions_conform_to_ac_scene_05()
        {
            // Assert: verify CharacterController conforms to AC-SCENE-05 and ADR-0006
            Assert.That(_characterController.radius, Is.EqualTo(0.30f));
            Assert.That(_characterController.height, Is.EqualTo(1.80f));
            Assert.That(_characterController.center, Is.EqualTo(new Vector3(0f, 0.90f, 0f)));
            Assert.That(_characterController.skinWidth, Is.EqualTo(0.03f));
            Assert.That(_playerGo.layer, Is.EqualTo(6));
        }

        [Test]
        public void test_runtime_driver_forward_input_advances_locomotion_along_camera_facing()
        {
            // Arrange: Camera oriented facing +Z
            _cameraGo.transform.rotation = Quaternion.identity;
            _controller.SetPlanarBasisFromCamera(_cameraGo.transform.forward, _cameraGo.transform.up);

            // Act: Inject forward move input (0, 1) and tick simulation for 0.5s
            for (int i = 0; i < 30; i++)
            {
                PlayerInputPacket packet = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: false, isControlSevered: false);
                _controller.Tick(packet, 1f / 60f);
            }

            // Assert: Velocity should align with +Z forward and accelerate towards Walk Speed (2.5 m/s)
            Vector3 velocity = _controller.Fsm.CurrentVelocity;
            Assert.That(velocity.z, Is.GreaterThan(1.0f), "Velocity Z should accelerate along camera forward basis.");
            Assert.That(Mathf.Abs(velocity.x), Is.LessThan(0.05f), "Velocity X should remain near zero for pure forward input.");
            Assert.That(_controller.Fsm.CurrentStance, Is.EqualTo(MovementStance.Standing));
        }

        [Test]
        public void test_runtime_driver_camera_yaw_rotates_planar_displacement()
        {
            // Arrange: Camera rotated 90 degrees yaw (facing +X world)
            _cameraGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            _controller.SetPlanarBasisFromCamera(_cameraGo.transform.forward, _cameraGo.transform.up);

            // Act: Inject forward move input (0, 1) relative to camera
            for (int i = 0; i < 30; i++)
            {
                PlayerInputPacket packet = new PlayerInputPacket(new Vector2(0f, 1f), isRunHeld: false, crouchToggleEdge: false, isControlSevered: false);
                _controller.Tick(packet, 1f / 60f);
            }

            // Assert: World velocity should now be directed along +X
            Vector3 velocity = _controller.Fsm.CurrentVelocity;
            Assert.That(velocity.x, Is.GreaterThan(1.0f), "Forward input with 90-deg yaw camera must produce +X world motion.");
            Assert.That(Mathf.Abs(velocity.z), Is.LessThan(0.05f), "Forward input with 90-deg yaw camera must have zero Z velocity.");
        }

        [Test]
        public void test_player_locomotion_stance_toggle_shrinks_and_restores_capsule_dimensions()
        {
            // Arrange: Initial standing stance
            Assert.That(_controller.Fsm.CurrentStance, Is.EqualTo(MovementStance.Standing));
            Assert.That(_controller.Fsm.CurrentCapsuleHeight, Is.EqualTo(1.80f));

            // Act: Edge-triggered crouch toggle input
            PlayerInputPacket crouchPacket = new PlayerInputPacket(Vector2.zero, isRunHeld: false, crouchToggleEdge: true, isControlSevered: false);
            _controller.Tick(crouchPacket, 1f / 60f);

            // Advance slewing transition (0.2s duration)
            for (int i = 0; i < 20; i++)
            {
                _controller.Tick(new PlayerInputPacket(Vector2.zero, false, false, false), 1f / 60f);
            }

            // Assert: Transition to Crouched stance (height 1.20m)
            Assert.That(_controller.Fsm.CurrentStance, Is.EqualTo(MovementStance.Crouched));
            Assert.That(_controller.Fsm.CurrentCapsuleHeight, Is.EqualTo(1.20f).Within(1e-3f));
            Assert.That(_characterController.height, Is.EqualTo(1.20f).Within(1e-3f));
            Assert.That(_characterController.center.y, Is.EqualTo(0.60f).Within(1e-3f));

            // Act 2: Uncrouch toggle
            _controller.Tick(crouchPacket, 1f / 60f);
            for (int i = 0; i < 20; i++)
            {
                _controller.Tick(new PlayerInputPacket(Vector2.zero, false, false, false), 1f / 60f);
            }

            // Assert 2: Restored to Standing stance (height 1.80m)
            Assert.That(_controller.Fsm.CurrentStance, Is.EqualTo(MovementStance.Standing));
            Assert.That(_controller.Fsm.CurrentCapsuleHeight, Is.EqualTo(1.80f).Within(1e-3f));
            Assert.That(_characterController.height, Is.EqualTo(1.80f).Within(1e-3f));
            Assert.That(_characterController.center.y, Is.EqualTo(0.90f).Within(1e-3f));
        }

        [Test]
        public void test_player_headroom_stand_clearance_query_rejects_uncrouch_under_low_headroom()
        {
            // Arrange: Force controller into crouch stance
            _controller.Fsm.ForceCrouchImmediate();
            Assert.That(_controller.Fsm.CurrentStance, Is.EqualTo(MovementStance.Crouched));

            // Setup a mock probe that reports overhead obstruction (e.g. simulated alcove ceiling at 1.40m)
            MockObstructedHeadroomProbe blockingProbe = new MockObstructedHeadroomProbe(clearanceGranted: false);
            _controller.Fsm.SetHeadroomProbe(blockingProbe);

            // Act: Player attempts to stand up under the low ceiling
            PlayerInputPacket standRequest = new PlayerInputPacket(Vector2.zero, isRunHeld: false, crouchToggleEdge: true, isControlSevered: false);
            _controller.Tick(standRequest, 1f / 60f);

            // Assert: Stance must remain Crouched due to rejected headroom query (AC-SCENE-07, AC-P12)
            Assert.That(_controller.Fsm.CurrentStance, Is.EqualTo(MovementStance.Crouched));
            Assert.That(_controller.Fsm.CurrentCapsuleHeight, Is.EqualTo(1.20f).Within(1e-3f));
        }

        private sealed class MockObstructedHeadroomProbe : IStandHeadroomProbe
        {
            private readonly bool _clearanceGranted;

            public MockObstructedHeadroomProbe(bool clearanceGranted)
            {
                _clearanceGranted = clearanceGranted;
            }

            public bool QueryStandClearance(Vector3 feetPosition, out int hitCount)
            {
                hitCount = _clearanceGranted ? 0 : 1;
                return _clearanceGranted;
            }
        }
    }
}
