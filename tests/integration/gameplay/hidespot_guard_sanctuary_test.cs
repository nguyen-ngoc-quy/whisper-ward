using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;
using WhisperWard.Core.Environment;

namespace WhisperWard.Tests.Integration.Gameplay
{
    /// <summary>
    /// Integration test suite verifying multi-system interoperability between:
    /// - HideSpot (Sanctuary / Witnessed Trap interaction volume)
    /// - VisionConeSensor (Detection suppression / Sanctuary immunity)
    /// - GuardFSMRuntimeController (Witnessed pursuit, guard_hold navigation, 1.5s dwell capture)
    /// Conforms to GDD #5 Player Movement &amp; Hide, AC-HIDE-01..04.
    /// </summary>
    [TestFixture]
    public sealed class HideSpotGuardSanctuaryIntegrationTests
    {
        private GameObject _guardGo;
        private VisionConeSensor _sensor;
        private GuardFSMRuntimeController _fsm;
        private GameObject _playerGo;
        private GameObject _spotGo;
        private HideSpot _hideSpot;

        [SetUp]
        public void SetUp()
        {
            _playerGo = new GameObject("TestPlayer");
            _playerGo.transform.position = new Vector3(0f, 0f, 6f);

            _spotGo = new GameObject("TestHideSpot");
            _hideSpot = _spotGo.AddComponent<HideSpot>();
            _hideSpot.Configure(
                spotId: "locker_integration_01",
                interiorPosition: new Vector3(0f, 0f, 6f),
                frontAnchor: new Vector3(0f, 0f, 5f),
                holdVector: new Vector3(0f, 0f, -1f),
                dwellVerifyTime: 1.50f,
                guardRadius: 0.40f);

            _guardGo = new GameObject("TestGuard");
            _guardGo.transform.position = Vector3.zero;
            _guardGo.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            _sensor = _guardGo.AddComponent<VisionConeSensor>();
            _sensor.Configure(
                physicsQuery: null,
                visionRange: 12.0f,
                visionFOV: 100.0f,
                tickInterval: 0.05f);
            _sensor.SetTarget(_playerGo.transform, isCrouching: false);

            _fsm = _guardGo.AddComponent<GuardFSMRuntimeController>();
            _fsm.Configure(
                agent: null,
                sensor: _sensor,
                accumulator: null,
                patrolDriver: null,
                playerTransform: _playerGo.transform,
                patrolSpeed: 2.30f,
                investigateSpeed: 5.00f,
                chaseSpeed: 7.50f,
                catchDistance: 5.50f,
                catchDuration: 1.00f,
                giveupTimeout: 4.00f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_playerGo);
            }

            if (_spotGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_spotGo);
            }

            if (_guardGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_guardGo);
            }
        }

        [Test]
        public void test_hidespot_unwitnessed_entry_suppresses_guard_vision_sensor_los()
        {
            // Arrange (AC-HIDE-02): Player is directly in front of guard (d = 6.0m < 12.0m, angle = 0 deg)
            _sensor.Tick(0.10f);
            Assert.That(_sensor.HasLOS, Is.True, "Guard must have clear LOS to player outside hide spot.");
            Assert.That(_sensor.InCone, Is.True);

            // Act: Player enters hide spot unwitnessed (Sanctuary)
            bool enterSuccess = _hideSpot.TryEnter(_playerGo.transform, isWitnessed: false, currentTime: 1.0f);
            Assert.That(enterSuccess, Is.True);

            // Wire Sanctuary state to sensor
            _sensor.SetTargetSanctuary(true);

            // Act 2: Advance perception sensor
            _sensor.Tick(0.10f);

            // Assert: Complete sanctuary immunity
            Assert.That(_sensor.HasLOS, Is.False, "Sanctuary entry must break and suppress line of sight.");
            Assert.That(_sensor.InCone, Is.False, "Player in sanctuary must not register in vision cone.");

            // Act 3: Player exits sanctuary
            _hideSpot.TryExit(out _, currentTime: 1.10f);
            _sensor.SetTargetSanctuary(false);
            _sensor.Tick(0.10f);

            // Assert: Normal vision restored upon exit
            Assert.That(_sensor.HasLOS, Is.True, "Exiting sanctuary must restore guard vision detection.");
        }

        [Test]
        public void test_hidespot_witnessed_chase_entry_causes_guard_fsm_to_approach_and_capture()
        {
            // Arrange (AC-HIDE-03 & GDD #5 AC3, AC16): Guard is actively chasing player
            _fsm.TriggerChase(_playerGo.transform.position);
            Assert.That(_fsm.CurrentState, Is.EqualTo(GuardFSMRuntimeController.GuardState.Chase));

            // Player enters hide spot while being witnessed
            bool enterSuccess = _hideSpot.TryEnter(_playerGo.transform, isWitnessed: true, currentTime: 1.0f);
            Assert.That(enterSuccess, Is.True);

            bool capturedFired = false;
            _fsm.OnPlayerCaptured += pos => capturedFired = true;

            // Direct guard to pursue witnessed hide spot
            _fsm.ChaseWitnessedHideSpot(_hideSpot.GuardHoldPosition, _hideSpot.DwellVerifyTime);
            Assert.That(_fsm.IsChasingHideSpot, Is.True);

            // Move guard to guard_hold position (d <= arrivalTolerance)
            _guardGo.transform.position = _hideSpot.GuardHoldPosition;

            // Act 1: Tick FSM during dwell (0.5s < 1.5s)
            _fsm.Tick(0.50f);
            Assert.That(_fsm.CurrentState, Is.EqualTo(GuardFSMRuntimeController.GuardState.Chase));
            Assert.That(_fsm.HideSpotDwellTimer, Is.EqualTo(0.50f).Within(1e-3f));
            Assert.That(capturedFired, Is.False);

            // Act 2: Tick remaining dwell (total 1.5s)
            _fsm.Tick(1.00f);

            // Assert: Capture committed!
            Assert.That(_fsm.CurrentState, Is.EqualTo(GuardFSMRuntimeController.GuardState.Captured));
            Assert.That(capturedFired, Is.True);
        }

        [Test]
        public void test_hidespot_witnessed_chase_exit_clears_dwell_and_aborts_premature_capture()
        {
            // Arrange: Guard chasing witnessed hide spot at hold position
            _fsm.TriggerChase(_playerGo.transform.position);
            _hideSpot.TryEnter(_playerGo.transform, isWitnessed: true, currentTime: 1.0f);
            _fsm.ChaseWitnessedHideSpot(_hideSpot.GuardHoldPosition, _hideSpot.DwellVerifyTime);
            _guardGo.transform.position = _hideSpot.GuardHoldPosition;

            // Guard dwells for 0.8s
            _fsm.Tick(0.80f);
            Assert.That(_fsm.HideSpotDwellTimer, Is.EqualTo(0.80f).Within(1e-3f));

            // Act: Player exits before 1.5s expiration (dt = 0.10s > 0.06s hysteresis)
            bool exited = _hideSpot.TryExit(out _, currentTime: 1.10f);
            Assert.That(exited, Is.True);

            // Clear hide spot pursuit on guard
            _fsm.ClearWitnessedHideSpot();
            Assert.That(_fsm.IsChasingHideSpot, Is.False);

            // Move player away
            _playerGo.transform.position = new Vector3(10f, 0f, 10f);

            // Advance FSM further
            _fsm.Tick(1.00f);

            // Assert: Guard does not capture via hide spot dwell
            Assert.That(_fsm.CurrentState, Is.EqualTo(GuardFSMRuntimeController.GuardState.Chase));
        }
    }
}
