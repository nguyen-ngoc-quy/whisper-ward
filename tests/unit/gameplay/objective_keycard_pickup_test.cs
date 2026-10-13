using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Objectives;

namespace WhisperWard.Tests.Unit.Gameplay
{
    [TestFixture]
    public class ObjectiveKeycardPickupTest
    {
        private GameObject _pickupHost;
        private ObjectiveKeycardPickup _pickup;
        private GameObject _visualChild;
        private BoxCollider _collider;
        private GameObject _playerObject;

        [SetUp]
        public void SetUp()
        {
            _pickupHost = new GameObject("KeycardPickup_Test");
            _pickup = _pickupHost.AddComponent<ObjectiveKeycardPickup>();

            _visualChild = new GameObject("Keycard_Visual");
            _visualChild.transform.SetParent(_pickupHost.transform);

            _collider = _pickupHost.AddComponent<BoxCollider>();
            _collider.isTrigger = true;

            // Reflectively initialize or rely on Awake
            var visualField = typeof(ObjectiveKeycardPickup).GetField("_visualModel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            visualField?.SetValue(_pickup, _visualChild);

            var colField = typeof(ObjectiveKeycardPickup).GetField("_interactionCollider",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            colField?.SetValue(_pickup, _collider);

            _playerObject = new GameObject("Player_TestCapsule");
            _playerObject.tag = "Player";
            _playerObject.transform.position = Vector3.zero;
            _pickupHost.transform.position = new Vector3(1.0f, 0f, 0f); // 1m away
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
            if (_pickupHost != null) Object.DestroyImmediate(_pickupHost);
        }

        [Test]
        public void test_keycard_pickup_initial_state_not_acquired()
        {
            // Assert
            Assert.IsFalse(_pickup.IsAcquired);
            Assert.AreEqual("Keycard_SecurityLevel1", _pickup.ObjectiveId);
            Assert.AreEqual(2.00f, _pickup.InteractionRadius, 0.001f);
            Assert.IsTrue(_visualChild.activeSelf);
            Assert.IsTrue(_collider.enabled);
        }

        [Test]
        public void test_keycard_pickup_interaction_sets_is_acquired_true()
        {
            // Act
            bool success = _pickup.TryPickup(_playerObject);

            // Assert
            Assert.IsTrue(success);
            Assert.IsTrue(_pickup.IsAcquired);
        }

        [Test]
        public void test_keycard_pickup_dispatches_objective_acquired_event()
        {
            // Arrange
            string instanceEventId = null;
            string globalEventId = null;

            _pickup.OnObjectiveAcquired += id => instanceEventId = id;
            ObjectiveKeycardPickup.OnAnyObjectiveAcquired += id => globalEventId = id;

            try
            {
                // Act
                _pickup.TryPickup(_playerObject);

                // Assert
                Assert.AreEqual("Keycard_SecurityLevel1", instanceEventId);
                Assert.AreEqual("Keycard_SecurityLevel1", globalEventId);
            }
            finally
            {
                ObjectiveKeycardPickup.OnAnyObjectiveAcquired -= id => globalEventId = id;
            }
        }

        [Test]
        public void test_keycard_pickup_deactivates_visuals_and_prevents_duplicate_pickup()
        {
            // Act
            bool firstAttempt = _pickup.TryPickup(_playerObject);
            bool secondAttempt = _pickup.TryPickup(_playerObject);

            // Assert
            Assert.IsTrue(firstAttempt);
            Assert.IsFalse(secondAttempt, "Duplicate pickup attempt must be rejected");
            Assert.IsFalse(_visualChild.activeSelf, "Visual mesh must be deactivated upon pickup");
            Assert.IsFalse(_collider.enabled, "Collider must be disabled to prevent further trigger hits");
        }

        [Test]
        public void test_keycard_pickup_distance_validation_rejects_out_of_range()
        {
            // Arrange: Move player far outside 2.0m interaction radius (e.g. 10m away)
            _playerObject.transform.position = new Vector3(10.0f, 0f, 0f);

            // Act
            bool success = _pickup.TryPickup(_playerObject, bypassDistanceCheck: false);

            // Assert
            Assert.IsFalse(success, "Pickup from 10m away must be rejected");
            Assert.IsFalse(_pickup.IsAcquired);
            Assert.IsTrue(_visualChild.activeSelf);
        }

        [Test]
        public void test_keycard_pickup_reset_restores_pickup_state()
        {
            // Arrange: Pick up item
            _pickup.TryPickup(_playerObject);
            Assert.IsTrue(_pickup.IsAcquired);
            Assert.IsFalse(_visualChild.activeSelf);

            // Act: Reset
            _pickup.ResetPickup();

            // Assert
            Assert.IsFalse(_pickup.IsAcquired);
            Assert.IsTrue(_visualChild.activeSelf);
            Assert.IsTrue(_collider.enabled);
        }
    }
}
