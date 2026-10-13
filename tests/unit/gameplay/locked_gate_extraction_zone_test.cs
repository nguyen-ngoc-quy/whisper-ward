using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Environment;

namespace WhisperWard.Tests.Unit.Gameplay
{
    [TestFixture]
    public class LockedGateExtractionZoneTest
    {
        private GameObject _gateHost;
        private LockedSecurityGate _gate;
        private BoxCollider _barrierCollider;
        private GameObject _gateVisual;

        private GameObject _zoneHost;
        private ExtractionZone _zone;
        private BoxCollider _zoneCollider;

        private GameObject _playerObject;

        [SetUp]
        public void SetUp()
        {
            // Setup Locked Security Gate
            _gateHost = new GameObject("LockedGate_Test");
            _gate = _gateHost.AddComponent<LockedSecurityGate>();

            _barrierCollider = _gateHost.AddComponent<BoxCollider>();
            _barrierCollider.isTrigger = false;

            _gateVisual = new GameObject("GateDoor_Visual");
            _gateVisual.transform.SetParent(_gateHost.transform);

            var colField = typeof(LockedSecurityGate).GetField("_barrierCollider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            colField?.SetValue(_gate, _barrierCollider);

            var visualField = typeof(LockedSecurityGate).GetField("_gateDoorVisual",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            visualField?.SetValue(_gate, _gateVisual);

            // Setup Extraction Zone
            _zoneHost = new GameObject("ExtractionZone_Test");
            _zone = _zoneHost.AddComponent<ExtractionZone>();
            _zoneCollider = _zoneHost.AddComponent<BoxCollider>();
            _zoneCollider.isTrigger = true;

            var zoneColField = typeof(ExtractionZone).GetField("_zoneCollider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            zoneColField?.SetValue(_zone, _zoneCollider);

            // Setup Player
            _playerObject = new GameObject("Player_TestCapsule");
            _playerObject.tag = "Player";
            _playerObject.transform.position = Vector3.zero;

            _gateHost.transform.position = new Vector3(1.5f, 0f, 0f); // 1.5m away (< 2.5m)
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
            if (_gateHost != null) Object.DestroyImmediate(_gateHost);
            if (_zoneHost != null) Object.DestroyImmediate(_zoneHost);
        }

        [Test]
        public void test_locked_gate_rejects_interaction_without_keycard()
        {
            // Arrange
            bool failedFired = false;
            _gate.OnGateUnlockFailed += () => failedFired = true;

            // Act
            bool success = _gate.TryUnlockAndOpen(hasKeycard: false, interactor: _playerObject);

            // Assert
            Assert.IsFalse(success);
            Assert.IsFalse(_gate.IsUnlocked);
            Assert.IsFalse(_gate.IsOpen);
            Assert.IsTrue(_barrierCollider.enabled, "Barrier collider must remain blocking");
            Assert.IsTrue(failedFired, "OnGateUnlockFailed event must be dispatched");
        }

        [Test]
        public void test_locked_gate_unlocks_and_opens_with_keycard()
        {
            // Arrange
            bool unlockFired = false;
            bool globalUnlockFired = false;
            _gate.OnGateUnlocked += () => unlockFired = true;
            LockedSecurityGate.OnAnyGateUnlocked += () => globalUnlockFired = true;

            try
            {
                // Act
                bool success = _gate.TryUnlockAndOpen(hasKeycard: true, interactor: _playerObject);

                // Assert
                Assert.IsTrue(success);
                Assert.IsTrue(_gate.IsUnlocked);
                Assert.IsTrue(_gate.IsOpen);
                Assert.IsFalse(_barrierCollider.enabled, "Barrier collider must be disabled to allow passage");
                Assert.IsFalse(_gateVisual.activeSelf, "Door visual should be hidden/cleared");
                Assert.IsTrue(unlockFired);
                Assert.IsTrue(globalUnlockFired);
            }
            finally
            {
                LockedSecurityGate.OnAnyGateUnlocked -= () => globalUnlockFired = true;
            }
        }

        [Test]
        public void test_locked_gate_idempotent_when_already_open()
        {
            // Arrange
            _gate.TryUnlockAndOpen(hasKeycard: true, interactor: _playerObject);
            Assert.IsTrue(_gate.IsOpen);

            // Act: Call unlock again
            bool secondCall = _gate.TryUnlockAndOpen(hasKeycard: true, interactor: _playerObject);

            // Assert
            Assert.IsTrue(secondCall);
            Assert.IsTrue(_gate.IsOpen);
        }

        [Test]
        public void test_locked_gate_interaction_distance_validation()
        {
            // Arrange: Move player far away (> 2.5m)
            _playerObject.transform.position = new Vector3(20.0f, 0f, 0f);
            bool failedFired = false;
            _gate.OnGateUnlockFailed += () => failedFired = true;

            // Act
            bool success = _gate.TryUnlockAndOpen(hasKeycard: true, interactor: _playerObject, bypassDistanceCheck: false);

            // Assert
            Assert.IsFalse(success, "Interaction beyond distance must fail");
            Assert.IsFalse(_gate.IsOpen);
            Assert.IsTrue(failedFired);
        }

        [Test]
        public void test_locked_gate_reset_restores_locked_barrier_state()
        {
            // Arrange
            _gate.TryUnlockAndOpen(hasKeycard: true, interactor: _playerObject);
            Assert.IsTrue(_gate.IsOpen);
            Assert.IsFalse(_barrierCollider.enabled);

            // Act
            _gate.ResetGate();

            // Assert
            Assert.IsFalse(_gate.IsUnlocked);
            Assert.IsFalse(_gate.IsOpen);
            Assert.IsTrue(_barrierCollider.enabled);
            Assert.IsTrue(_gateVisual.activeSelf);
        }

        [Test]
        public void test_extraction_zone_triggers_completion_event_when_player_enters()
        {
            // Arrange
            GameObject extractedActor = null;
            bool globalExtractionFired = false;

            _zone.OnExtractionCompleted += actor => extractedActor = actor;
            ExtractionZone.OnAnyExtractionCompleted += () => globalExtractionFired = true;

            try
            {
                // Act
                bool success = _zone.TriggerExtraction(_playerObject);

                // Assert
                Assert.IsTrue(success);
                Assert.IsTrue(_zone.IsExtracted);
                Assert.AreSame(_playerObject, extractedActor);
                Assert.IsTrue(globalExtractionFired);
            }
            finally
            {
                ExtractionZone.OnAnyExtractionCompleted -= () => globalExtractionFired = true;
            }
        }

        [Test]
        public void test_extraction_zone_prevents_duplicate_triggers()
        {
            // Act
            bool first = _zone.TriggerExtraction(_playerObject);
            bool second = _zone.TriggerExtraction(_playerObject);

            // Assert
            Assert.IsTrue(first);
            Assert.IsFalse(second, "Duplicate extraction must be rejected");
        }

        [Test]
        public void test_extraction_zone_reset_restores_state()
        {
            // Arrange
            _zone.TriggerExtraction(_playerObject);
            Assert.IsTrue(_zone.IsExtracted);

            // Act
            _zone.ResetZone();

            // Assert
            Assert.IsFalse(_zone.IsExtracted);
        }
    }
}
