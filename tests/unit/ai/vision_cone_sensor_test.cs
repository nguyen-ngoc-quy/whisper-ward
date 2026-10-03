using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Perception;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.Tests.Unit.AI
{
    [TestFixture]
    public class VisionConeSensorTest
    {
        private class MockPhysicsQueryService : IPhysicsQueryService
        {
            public bool ReturnOccluded { get; set; } = false;
            public System.Func<Vector3, Vector3, bool> CustomOcclusionPredicate { get; set; }
            public Vector3 LastRayStart { get; private set; }
            public Vector3 LastRayEnd { get; private set; }
            public int LastLayerMask { get; private set; }

            public int DefaultBufferCapacity => 64;

            public bool CheckOcclusionLine(Vector3 start, Vector3 end, int layerMask, out RaycastHit hit)
            {
                LastRayStart = start;
                LastRayEnd = end;
                LastLayerMask = layerMask;
                hit = new RaycastHit();
                if (CustomOcclusionPredicate != null)
                {
                    return CustomOcclusionPredicate(start, end);
                }
                return ReturnOccluded;
            }

            public int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDistance, int layerMask) => 0;
            public int SphereCastNonAlloc(Ray ray, float radius, RaycastHit[] results, float maxDistance, int layerMask) => 0;
            public int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask) => 0;
            public bool CheckOriginEmbedded(Vector3 origin, int layerMask, float testRadius = 0.05f) => false;
            public bool TrySyncTransformsBatch(float currentTime) => true;
        }

        private GameObject _guardObject;
        private GameObject _targetObject;
        private VisionConeSensor _sensor;
        private MockPhysicsQueryService _mockPhysics;

        [SetUp]
        public void SetUp()
        {
            _guardObject = new GameObject("Guard_Test");
            _guardObject.transform.position = Vector3.zero;
            _guardObject.transform.rotation = Quaternion.identity; // forward is (0, 0, 1)

            _targetObject = new GameObject("Player_Test");
            _targetObject.transform.position = new Vector3(0, 0, 5.0f); // 5m directly ahead

            _sensor = _guardObject.AddComponent<VisionConeSensor>();
            _mockPhysics = new MockPhysicsQueryService();
            _sensor.Configure(_mockPhysics, visionRange: 12.0f, visionFOV: 100.0f, tickInterval: 0.20f, occlusionMask: 1 << 20);
            _sensor.SetTarget(_targetObject.transform, isCrouching: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_guardObject != null) Object.DestroyImmediate(_guardObject);
            if (_targetObject != null) Object.DestroyImmediate(_targetObject);
        }

        [Test]
        public void test_vision_sensor_within_range_and_fov_returns_los_true()
        {
            // Arrange: Target 5m directly ahead, unoccluded
            _targetObject.transform.position = new Vector3(0, 0, 5.0f);
            _mockPhysics.ReturnOccluded = false;

            // Act
            _sensor.Tick(0.20f);

            // Assert
            Assert.IsTrue(_sensor.HasLOS, "Target directly ahead within 12m must have LOS.");
            Assert.IsTrue(_sensor.InCone, "Target must be inside cone.");
            Assert.LessOrEqual(_sensor.TargetAngle, 1.0f, "Target angle should be ~0 deg.");
        }

        [Test]
        public void test_vision_sensor_beyond_range_returns_false()
        {
            // Arrange: Target 13.0m ahead (> 12.0m range)
            _targetObject.transform.position = new Vector3(0, 0, 13.0f);

            // Act
            _sensor.Tick(0.20f);

            // Assert
            Assert.IsFalse(_sensor.HasLOS, "Target beyond 12m must NOT have LOS.");
            Assert.IsFalse(_sensor.InCone, "Target beyond range must not be inside cone.");
        }

        [Test]
        public void test_vision_sensor_outside_fov_returns_false()
        {
            // Arrange: Target 5m away at 60 degrees (exceeding 50 deg half-angle of 100 deg FOV)
            // 5m at 60 deg -> x = 5 * sin(60) = 4.33, z = 5 * cos(60) = 2.5
            _targetObject.transform.position = new Vector3(4.33f, 0, 2.5f);

            // Act
            _sensor.Tick(0.20f);

            // Assert
            Assert.IsFalse(_sensor.HasLOS, "Target outside 100 deg FOV must NOT have LOS.");
            Assert.IsFalse(_sensor.InCone, "Target outside FOV must not be in cone.");
            Assert.Greater(_sensor.TargetAngle, 50.0f, "Angle must exceed 50 deg.");
        }

        [Test]
        public void test_vision_sensor_boundary_angle_inside_fov_returns_true()
        {
            // Arrange: Target 5m away at 45 degrees (< 50 deg half-angle)
            // 5m at 45 deg -> x = 5 * sin(45) = 3.535, z = 5 * cos(45) = 3.535
            _targetObject.transform.position = new Vector3(3.535f, 0, 3.535f);

            // Act
            _sensor.Tick(0.20f);

            // Assert
            Assert.IsTrue(_sensor.HasLOS, "Target at 45 deg must be visible inside 100 deg FOV.");
            Assert.IsTrue(_sensor.InCone, "Target at 45 deg must be in cone.");
            Assert.LessOrEqual(_sensor.TargetAngle, 50.0f, "Angle must be <= 50 deg.");
        }

        [Test]
        public void test_vision_sensor_behind_guard_returns_false()
        {
            // Arrange: Target directly behind guard at (0, 0, -5m)
            _targetObject.transform.position = new Vector3(0, 0, -5.0f);

            // Act
            _sensor.Tick(0.20f);

            // Assert
            Assert.IsFalse(_sensor.HasLOS, "Target behind guard must NOT have LOS.");
            Assert.IsFalse(_sensor.InCone, "Target behind guard must NOT be in cone.");
            Assert.GreaterOrEqual(_sensor.TargetAngle, 175.0f, "Angle must be ~180 deg.");
        }

        [Test]
        public void test_vision_sensor_occluded_by_world_layer_returns_los_false()
        {
            // Arrange: Target in FOV, but physics mock reports occlusion
            _targetObject.transform.position = new Vector3(0, 0, 5.0f);
            _mockPhysics.ReturnOccluded = true;

            // Act
            _sensor.Tick(0.20f);

            // Assert
            Assert.IsTrue(_sensor.InCone, "Target is geometrically in cone.");
            Assert.IsFalse(_sensor.HasLOS, "Occluded linecast must result in hasLOS == false.");
            Assert.AreEqual(1 << 20, _mockPhysics.LastLayerMask, "Linecast must query Layer 20 World.");
        }

        [Test]
        public void test_vision_sensor_stance_adaptive_height_targets()
        {
            // Arrange
            _targetObject.transform.position = new Vector3(0, 0, 5.0f);

            // Act 1: Standing
            _sensor.SetTargetCrouching(false);
            _sensor.Tick(0.20f);
            Vector3 standTarget = _mockPhysics.LastRayEnd;

            // Act 2: Crouching
            _sensor.SetTargetCrouching(true);
            _sensor.Tick(0.20f);
            Vector3 crouchTarget = _mockPhysics.LastRayEnd;

            // Assert: Eye is at Y=1.60m, Stand target at Y=1.35m, Crouch target at Y=0.80m
            Assert.AreEqual(1.60f, _mockPhysics.LastRayStart.y, 0.01f, "Guard eye must be at 1.60m.");
            Assert.AreEqual(1.35f, standTarget.y, 0.01f, "Stand target point must be at 1.35m.");
            Assert.AreEqual(0.80f, crouchTarget.y, 0.01f, "Crouch target point must be at 0.80m.");
            Assert.Greater(standTarget.y, crouchTarget.y, "Stand target must be significantly higher than crouch target.");
        }

        [Test]
        public void test_vision_sensor_stance_adaptive_low_cover_blocks_crouch_allows_stand()
        {
            // Arrange: Low cover at z = 2.5m with top at Y = 1.25m
            // Ray from (0, 1.60, 0) to stand (0, 1.35, 5.0) passes at Y = 1.475m (> 1.25m -> clear)
            // Ray from (0, 1.60, 0) to crouch (0, 0.80, 5.0) passes at Y = 1.200m (<= 1.25m -> occluded)
            _targetObject.transform.position = new Vector3(0, 0, 5.0f);
            _mockPhysics.CustomOcclusionPredicate = (start, end) =>
            {
                // Line equation at midpoint z = 2.5m
                float yAtMidpoint = (start.y + end.y) * 0.5f;
                float coverHeight = 1.25f;
                return yAtMidpoint <= coverHeight;
            };

            // Act 1: Standing player behind low cover
            _sensor.SetTargetCrouching(false);
            _sensor.Tick(0.20f);
            bool standLOS = _sensor.HasLOS;

            // Act 2: Crouching player behind low cover
            _sensor.SetTargetCrouching(true);
            _sensor.Tick(0.20f);
            bool crouchLOS = _sensor.HasLOS;

            // Assert: Stand is visible over cover, Crouch is occluded (AC-GUARD-03)
            Assert.IsTrue(standLOS, "Standing player must be visible over 1.25m low cover.");
            Assert.IsFalse(crouchLOS, "Crouching player must be occluded by 1.25m low cover.");
        }

        [Test]
        public void test_vision_sensor_tick_cadence_sub_interval_does_not_evaluate()
        {
            // Arrange: Target initially at 5m
            _sensor.Tick(0.20f);
            Assert.IsTrue(_sensor.HasLOS);

            // Move target out of range
            _targetObject.transform.position = new Vector3(0, 0, 25.0f);

            // Act: Advance time by only 0.10s (< 0.20s tick interval)
            _sensor.Tick(0.10f);

            // Assert: Sensor should still retain previous tick's state (5 Hz cadence)
            Assert.IsTrue(_sensor.HasLOS, "Sub-interval tick must NOT change LOS state before cadence elapses.");

            // Act 2: Advance remaining 0.10s
            _sensor.Tick(0.10f);

            // Assert: Now cadence elapsed, state should update to false
            Assert.IsFalse(_sensor.HasLOS, "Cadence elapsed must update state.");
        }

        [Test]
        public void test_vision_sensor_los_gained_and_lost_events_fire_on_transitions()
        {
            // Arrange
            int gainedCount = 0;
            int lostCount = 0;
            Vector3 gainedPos = Vector3.zero;
            Vector3 lostPos = Vector3.zero;

            _sensor.OnLOSGained += pos => { gainedCount++; gainedPos = pos; };
            _sensor.OnLOSLost += pos => { lostCount++; lostPos = pos; };

            // Step 1: Target placed inside cone -> OnLOSGained fires
            _targetObject.transform.position = new Vector3(0, 0, 4.0f);
            _sensor.Tick(0.20f);

            Assert.AreEqual(1, gainedCount, "OnLOSGained must fire on initial sighting.");
            Assert.AreEqual(0, lostCount, "OnLOSLost must not fire.");
            Assert.AreEqual(new Vector3(0, 0, 4.0f), gainedPos);

            // Step 2: Second tick with sustained sight -> no duplicate event
            _sensor.Tick(0.20f);
            Assert.AreEqual(1, gainedCount, "Sustained sight must not fire duplicate OnLOSGained.");
            Assert.AreEqual(0, lostCount);

            // Step 3: Move target behind a wall -> OnLOSLost fires
            _mockPhysics.ReturnOccluded = true;
            _sensor.Tick(0.20f);

            Assert.AreEqual(1, gainedCount);
            Assert.AreEqual(1, lostCount, "OnLOSLost must fire when LOS breaks.");
            Assert.AreEqual(new Vector3(0, 0, 4.0f), lostPos, "OnLOSLost must report last known position.");
        }
    }
}
