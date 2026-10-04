using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;
using WhisperWard.Core.Player;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.Tests.Integration.Noise
{
    [TestFixture]
    public class BurstDistractionLureTest
    {
        private GameObject _playerObject;
        private GameObject _guardObject;
        private GameObject _projectileObject;

        private BurstDistractionNoiseMaker _noiseMaker;
        private GuardFSMRuntimeController _guardFsm;
        private GuardHearingSensor _hearingSensor;
        private NavMeshAgent _guardAgent;
        private MockPhysicsQueryService _mockPhysics;

        private class MockPhysicsQueryService : IPhysicsQueryService
        {
            public bool IsOccluded { get; set; } = false;

            public bool CheckOcclusionLine(Vector3 start, Vector3 end, int layerMask, out RaycastHit hit)
            {
                hit = default;
                return IsOccluded;
            }

            public bool SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance, int layerMask, out RaycastHit hit)
            {
                hit = default;
                return false;
            }

            public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out RaycastHit hit)
            {
                hit = default;
                return false;
            }

            public int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask)
            {
                return 0;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player_Lure_Test");
            _guardObject = new GameObject("Guard_Lure_Test");
            _projectileObject = new GameObject("BurstNoiseMaker_Lure_Test");

            _noiseMaker = _projectileObject.AddComponent<BurstDistractionNoiseMaker>();
            _noiseMaker.Configure(
                initialSpeed: 12.00f,
                launchAngle: 30.00f,
                releaseHeight: 1.50f,
                gravity: 9.81f,
                acousticRadius: 10.00f,
                movingSpeedThreshold: 1.00f);

            _guardAgent = _guardObject.AddComponent<NavMeshAgent>();
            _guardFsm = _guardObject.AddComponent<GuardFSMRuntimeController>();
            _hearingSensor = _guardObject.AddComponent<GuardHearingSensor>();

            _guardFsm.Configure(
                _guardAgent,
                sensor: null,
                accumulator: null,
                patrolDriver: null,
                playerTransform: _playerObject.transform,
                patrolSpeed: 2.30f,
                investigateSpeed: 5.00f,
                chaseSpeed: 7.50f);

            _mockPhysics = new MockPhysicsQueryService();
            _hearingSensor.Configure(_guardFsm, _mockPhysics, eyeHeight: 1.60f, verticalHardCutoff: 4.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
            if (_guardObject != null) Object.DestroyImmediate(_guardObject);
            if (_projectileObject != null) Object.DestroyImmediate(_projectileObject);
        }

        [Test]
        public void test_guard_hears_burst_impact_and_investigates_impact_point()
        {
            // Arrange (AC-NOISE-08)
            // Guard patrolling at (0, 0, 0)
            _guardObject.transform.position = Vector3.zero;
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _guardFsm.CurrentState);

            Vector3 impactPoint = new Vector3(7.0f, 0f, 0f); // 7.0m <= 10.0m acoustic radius
            _mockPhysics.IsOccluded = false;

            bool guardHeardEventFired = false;
            Vector3 heardPos = Vector3.zero;
            _hearingSensor.OnNoiseHeard += (pos, rad) =>
            {
                guardHeardEventFired = true;
                heardPos = pos;
            };

            // Act: Projectile impacts at (7, 0, 0)
            _noiseMaker.CommitImpact(impactPoint);

            // Assert
            Assert.IsTrue(guardHeardEventFired, "Guard hearing sensor must register burst noise.");
            Assert.AreEqual(impactPoint, heardPos);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _guardFsm.CurrentState,
                "Guard must transition from Patrol to Investigate upon hearing burst impact.");
            Assert.AreEqual(impactPoint, _guardFsm.LastKnownPosition,
                "Guard LastKnownPosition must be set to burst impact coordinates.");
        }

        [Test]
        public void test_guard_in_chase_ignores_burst_distraction_chase_wins()
        {
            // Arrange: Guard actively chasing player (R13 Chase-wins invariant)
            _guardObject.transform.position = Vector3.zero;
            _playerObject.transform.position = new Vector3(5.0f, 0f, 0f);
            _guardFsm.TriggerChase(_playerObject.transform.position);

            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _guardFsm.CurrentState);

            Vector3 burstImpactPoint = new Vector3(3.0f, 0f, 0f); // close by
            _mockPhysics.IsOccluded = false;

            // Act: Burst lands near guard while chasing
            _noiseMaker.CommitImpact(burstImpactPoint);

            // Assert
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _guardFsm.CurrentState,
                "Guard in active Chase must ignore distraction sound (R13 Chase-wins).");
            Assert.AreEqual(new Vector3(5.0f, 0f, 0f), _guardFsm.LastKnownPosition,
                "Guard LKP must remain locked on chased player, not diverted to burst impact.");
        }

        [Test]
        public void test_burst_distraction_beyond_10m_ignored_by_guard()
        {
            // Arrange: Guard at (0, 0, 0), burst at (15, 0, 0) (> 10.0m)
            _guardObject.transform.position = Vector3.zero;
            Vector3 impactPoint = new Vector3(15.0f, 0f, 0f);
            _mockPhysics.IsOccluded = false;

            // Act
            _noiseMaker.CommitImpact(impactPoint);

            // Assert
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _guardFsm.CurrentState,
                "Guard must remain in Patrol when noise is outside 10.0m acoustic pulse radius.");
        }

        [Test]
        public void test_burst_distraction_occluded_by_layer20_wall_ignored()
        {
            // Arrange: Burst within 5.0m, but occluded by solid obstacle
            _guardObject.transform.position = Vector3.zero;
            Vector3 impactPoint = new Vector3(5.0f, 0f, 0f);
            _mockPhysics.IsOccluded = true; // Wall blocks sound linecast

            // Act
            _noiseMaker.CommitImpact(impactPoint);

            // Assert
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _guardFsm.CurrentState,
                "Guard must remain in Patrol when noise line of sound is occluded by Layer 20 obstacle.");
        }
    }
}
