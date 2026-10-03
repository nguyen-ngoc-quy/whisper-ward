using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Camera;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Tests.Integration.Scene
{
    /// <summary>
    /// Integration test suite for Story SCENE-03 (Cinemachine Camera Rig & Orbit Follow Integration).
    /// Verifies camera follow rig metrics, mouse look rotation, real-time spherecast deocclusion,
    /// dynamic chase FOV scaling, and HideSpot aperture yaw cone clamping.
    /// Governed by ADR-0008, GDD #20, and AC-SCENE-09 through AC-SCENE-12.
    /// </summary>
    [TestFixture]
    public sealed class CameraOrbitDriverIntegrationTest
    {
        private GameObject _playerGo;
        private GameObject _cameraGo;
        private UnityEngine.Camera _camera;
        private CameraOrbitDriver _driver;
        private GameObject _triggerGo;
        private HideSpotTriggerZone _triggerZone;

        [SetUp]
        public void SetUp()
        {
            // 1. Setup simulated Player capsule
            _playerGo = new GameObject("Test_Player");
            _playerGo.tag = "Player";
            _playerGo.layer = 6; // Layer 6: Player
            _playerGo.transform.position = Vector3.zero;

            // 2. Setup Camera and CameraOrbitDriver
            _cameraGo = new GameObject("Test_MainCamera");
            _cameraGo.tag = "MainCamera";
            _camera = _cameraGo.AddComponent<UnityEngine.Camera>();
            _camera.fieldOfView = 60.0f;
            _camera.nearClipPlane = 0.10f;
            _camera.farClipPlane = 100.0f;

            _driver = _cameraGo.AddComponent<CameraOrbitDriver>();
            _driver.Configure(_playerGo.transform, _camera);

            // 3. Setup HideSpot trigger volume
            _triggerGo = new GameObject("Test_HideSpot_Trigger");
            _triggerGo.layer = 10; // Layer 10: HideSpotTrigger
            BoxCollider col = _triggerGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2f, 2f, 2f);

            _triggerZone = _triggerGo.AddComponent<HideSpotTriggerZone>();
            _triggerZone.Configure(Vector3.right, Vector3.zero, _driver);
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_triggerGo != null) Object.DestroyImmediate(_triggerGo);
        }

        [Test]
        public void test_camera_orbit_driver_initial_configuration_matches_adr_metrics()
        {
            // Assert: verify initial driver state conforms to ADR-0008
            Assert.That(_driver.FollowTarget, Is.EqualTo(_playerGo.transform));
            Assert.That(_driver.TargetCamera, Is.EqualTo(_camera));
            Assert.That(_driver.RigService, Is.Not.Null);
            Assert.That(_driver.CurrentDistance, Is.EqualTo(CameraRigService.DefaultNominalDistance));
            Assert.That(_driver.RigService.CurrentFov, Is.EqualTo(CameraRigService.DefaultExplorationFov));
            Assert.That(_driver.RigService.IsInHideSpotView, Is.False);
        }

        [Test]
        public void test_camera_orbit_driver_injected_look_delta_rotates_camera_yaw_and_pitch()
        {
            // Arrange: Inject 45 deg yaw delta and -15 deg pitch delta
            Vector2 lookInput = new Vector2(45.0f, -15.0f);
            _driver.InjectLookDelta(lookInput);

            // Act: Update look rotation via RigService for 1.0s
            _driver.RigService.UpdateLookRotation(lookInput, 1.0f);

            // Assert: Yaw should advance to 45 deg, pitch should advance to 25 deg (10 initial + 15)
            Assert.That(_driver.RigService.CameraYaw, Is.EqualTo(45.0f).Within(1e-3f));
            Assert.That(_driver.RigService.CameraPitch, Is.EqualTo(25.0f).Within(1e-3f));
            Assert.That(_driver.RigService.PlanarForward.x, Is.GreaterThan(0.5f));
            Assert.That(_driver.RigService.PlanarForward.z, Is.GreaterThan(0.5f));
        }

        [Test]
        public void test_camera_orbit_driver_spherecast_deocclusion_snaps_distance_on_wall_contact()
        {
            // Arrange: Setup mock spherecast hit at distance 1.50m (wall 1.5m behind target)
            Vector3 chestPivot = _playerGo.transform.position + (Vector3.up * CameraRigService.DefaultTargetHeight);
            Vector3 camBack = Vector3.back;

            _driver.RigService.SetTestHooks(
                timeScaleProvider: () => 1.0f,
                sphereCastDelegate: (origin, radius, direction, results, maxDistance, layerMask, queryTriggers) =>
                {
                    results[0] = new RaycastHit();
                    // Simulate RaycastHit distance = 1.50m
                    return 1;
                }
            );

            // Directly evaluate distance formula with hit distance 1.50m:
            // D_actual = max(D_min, d_hit - R_cam) = max(0.40, 1.50 - 0.20) = 1.30m
            float hitDistance = 1.50f;
            float expectedDistance = Mathf.Max(CameraRigService.DefaultMinDistance, hitDistance - CameraRigService.DefaultSpherecastRadius);

            // Act: In-snap evaluation
            float evaluatedDist = _driver.RigService.EvaluateCameraDistance(chestPivot, camBack, 2.80f, 0.016f);

            // Assert: Instantaneous distance reduction without damping delay (AC-CAM-06, AC-SCENE-11)
            Assert.That(evaluatedDist, Is.EqualTo(expectedDistance).Within(0.01f));
            Assert.That(_driver.RigService.CurrentDistance, Is.LessThan(2.80f));
        }

        [Test]
        public void test_camera_orbit_driver_spherecast_recovery_smoothly_expands_with_exponential_tau()
        {
            // Arrange: Starting from compressed distance 1.0m, no obstruction present
            float startDist = 1.0f;
            float dt = CameraRigService.DefaultRecoveryTau; // dt = 0.25s (1 tau)

            // Act: Evaluate recovery over 1 tau
            float newDistance = _driver.RigService.EvaluateCameraDistance(Vector3.zero, Vector3.back, startDist, dt);

            // Assert: D(t + dt) = D(t) + (D_nom - D(t)) * (1 - e^-1)
            // 1.0 + (2.8 - 1.0) * (1 - 0.367879) = 1.0 + 1.8 * 0.63212 = 2.1378m
            float expectedDist = startDist + (CameraRigService.DefaultNominalDistance - startDist) * (1.0f - Mathf.Exp(-1.0f));
            Assert.That(newDistance, Is.EqualTo(expectedDist).Within(0.01f));
        }

        [Test]
        public void test_camera_orbit_driver_chase_fov_smoothly_expands_to_sixty_eight_degrees()
        {
            // Arrange: Initial 60 deg FOV
            Assert.That(_driver.RigService.CurrentFov, Is.EqualTo(60.0f).Within(1e-3f));

            // Act: Engage chase FOV and simulate 3*tau = 0.54s
            _driver.SetChaseFov(true);
            Assert.That(_driver.RigService.IsChaseFovActive, Is.True);

            float dt = 0.01666f;
            for (int i = 0; i < 33; i++) // ~0.55s
            {
                _driver.RigService.UpdateFov(dt);
            }

            // Assert: FOV expands smoothly to >= 67.6 deg (AC-SCENE-12, AC-CAM-10)
            Assert.That(_driver.RigService.CurrentFov, Is.GreaterThanOrEqualTo(67.6f));
            Assert.That(_driver.RigService.CurrentFov, Is.LessThanOrEqualTo(68.0f));

            // Act 2: Disengage chase and simulate relaxation (3*tau = 1.35s)
            _driver.SetChaseFov(false);
            for (int i = 0; i < 90; i++) // ~1.5s
            {
                _driver.RigService.UpdateFov(dt);
            }

            // Assert 2: FOV contracts back to <= 60.4 deg
            Assert.That(_driver.RigService.CurrentFov, Is.LessThanOrEqualTo(60.4f));
            Assert.That(_driver.RigService.CurrentFov, Is.GreaterThanOrEqualTo(60.0f));
        }

        [Test]
        public void test_hidespot_trigger_zone_player_entry_clamps_camera_yaw_to_aperture_cone()
        {
            // Arrange: Trigger zone aperture outward facing is +X (Yaw = 90 deg)
            Vector3 aperturePos = new Vector3(-7f, 1.2f, -5f);
            Vector3 outwardDir = Vector3.right; // 90 degrees yaw

            // Act 1: Player enters HideSpot trigger
            _driver.SwitchToHideSpotView(aperturePos, outwardDir);

            // Assert 1: In HideSpot view, camera yaw automatically aligns with portal normal (90 deg)
            Assert.That(_driver.RigService.IsInHideSpotView, Is.True);
            Assert.That(_driver.RigService.CameraYaw, Is.EqualTo(90.0f).Within(1e-3f));

            // Act 2: Player attempts extreme right look rotation (+90 deg delta)
            _driver.RigService.UpdateLookRotation(new Vector2(90.0f, 0f), 0.5f);

            // Assert 2: Yaw must be strictly clamped to +/- 30 deg cone around 90 deg: [60 deg, 120 deg]
            Assert.That(_driver.RigService.CameraYaw, Is.EqualTo(120.0f).Within(1e-3f));

            // Act 3: Player attempts extreme left look rotation (-180 deg delta)
            _driver.RigService.UpdateLookRotation(new Vector2(-180.0f, 0f), 0.5f);

            // Assert 3: Yaw clamped to lower boundary (90 - 30 = 60 deg)
            Assert.That(_driver.RigService.CameraYaw, Is.EqualTo(60.0f).Within(1e-3f));

            // Act 4: Exit HideSpot view
            _driver.SwitchToOrbitView();
            Assert.That(_driver.RigService.IsInHideSpotView, Is.False);

            // Assert 4: Free orbit restored; can rotate freely past 120 deg
            _driver.RigService.UpdateLookRotation(new Vector2(100.0f, 0f), 0.5f);
            Assert.That(_driver.RigService.CameraYaw, Is.GreaterThan(120.0f));
        }
    }
}
