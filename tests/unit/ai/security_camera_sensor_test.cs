using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Perception;

namespace WhisperWard.Tests.Unit.AI
{
    [TestFixture]
    public class SecurityCameraSensorTest
    {
        private GameObject _cameraHost;
        private SecurityCameraSensor _sensor;

        [SetUp]
        public void SetUp()
        {
            _cameraHost = new GameObject("SecurityCamera_TestHost");
            _sensor = _cameraHost.AddComponent<SecurityCameraSensor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_cameraHost != null)
            {
                Object.DestroyImmediate(_cameraHost);
            }
        }

        [Test]
        public void test_camera_sweep_kinematics_oscillates_within_configured_half_angle()
        {
            // Arrange
            _sensor.ConfigureSweep(centerAngle: 0f, sweepHalfAngle: 45f, sweepSpeedDegPerSec: 90f);

            // Act - sweep for 1 second at 90 deg/s
            _sensor.SimulateTick(0.5f); // reaches +45 deg
            float yawHalfWay = _sensor.CurrentYaw;

            _sensor.SimulateTick(0.5f); // reverses towards center
            float yawReversed = _sensor.CurrentYaw;

            // Assert
            Assert.IsTrue(yawHalfWay >= 0f && yawHalfWay <= 45.001f);
            Assert.IsTrue(yawReversed <= 45.001f && yawReversed >= -45.001f);
        }

        [Test]
        public void test_camera_target_in_los_accumulates_until_alarm_triggered()
        {
            // Arrange
            _sensor.ConfigurePerception(fov: 60f, range: 12f, confirmTime: 0.5f);
            bool alarmFired = false;
            Vector3 spottedPos = Vector3.zero;
            _sensor.OnCameraAlarmTriggered += pos =>
            {
                alarmFired = true;
                spottedPos = pos;
            };

            // Act - Step 0.3s (less than 0.5s confirm)
            _sensor.SetTargetInConeAndLOS(true, isInSanctuary: false, new Vector3(5f, 0f, 5f));
            _sensor.SimulateTick(0.3f);

            // Assert intermediate state
            Assert.AreEqual(SecurityCameraState.TargetAcquiring, _sensor.State);
            Assert.IsFalse(alarmFired);

            // Act - Step another 0.3s (total 0.6s >= 0.5s)
            _sensor.SimulateTick(0.3f);

            // Assert triggered state
            Assert.AreEqual(SecurityCameraState.AlarmTriggered, _sensor.State);
            Assert.IsTrue(alarmFired);
            Assert.AreEqual(5f, spottedPos.x, 0.001f);
        }

        [Test]
        public void test_camera_target_in_hide_sanctuary_is_immune_to_camera_detection()
        {
            // Arrange
            _sensor.ConfigurePerception(fov: 60f, range: 12f, confirmTime: 0.5f);

            // Act - in cone and LOS but in Sanctuary
            _sensor.SetTargetInConeAndLOS(true, isInSanctuary: true, new Vector3(3f, 0f, 3f));
            _sensor.SimulateTick(1.0f);

            // Assert
            Assert.AreEqual(SecurityCameraState.IdleSweeping, _sensor.State);
            Assert.AreEqual(0f, _sensor.AcquisitionProgress);
        }

        [Test]
        public void test_camera_breaking_los_decays_acquisition_timer_back_to_idle()
        {
            // Arrange
            _sensor.ConfigurePerception(fov: 60f, range: 12f, confirmTime: 0.5f);
            _sensor.SetTargetInConeAndLOS(true, isInSanctuary: false, new Vector3(2f, 0f, 2f));
            _sensor.SimulateTick(0.3f);
            Assert.AreEqual(SecurityCameraState.TargetAcquiring, _sensor.State);

            // Act - break LOS
            _sensor.SetTargetInConeAndLOS(false, isInSanctuary: false, Vector3.zero);
            _sensor.SimulateTick(1.0f); // decay duration

            // Assert
            Assert.AreEqual(SecurityCameraState.IdleSweeping, _sensor.State);
            Assert.AreEqual(0f, _sensor.AcquisitionProgress);
        }
    }
}
