using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;
using WhisperWard.AI.Navigation;
using WhisperWard.Core.Player;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.Tests.Unit.Noise
{
    [TestFixture]
    public class FootstepNoiseEmitterTest
    {
        private GameObject _playerObject;
        private GameObject _guardObject;
        private PlayerFootstepNoiseEmitter _emitter;
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
            _playerObject = new GameObject("Player_Noise_Test");
            _emitter = _playerObject.AddComponent<PlayerFootstepNoiseEmitter>();

            _guardObject = new GameObject("Guard_Hearing_Test");
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
        }

        [Test]
        public void test_footstep_emitter_crouch_is_completely_silent()
        {
            // Arrange & Act (AC-NOISE-01)
            float radiusWalkingCrouched = _emitter.CalculateNominalNoiseRadius(planarSpeed: 1.5f, isCrouched: true);
            float radiusFastCrouched = _emitter.CalculateNominalNoiseRadius(planarSpeed: 1.8f, isCrouched: true);

            // Assert
            Assert.AreEqual(0.0f, radiusWalkingCrouched, "Crouch movement must emit 0.0m noise radius.");
            Assert.AreEqual(0.0f, radiusFastCrouched, "Fast crouch movement must emit 0.0m noise radius.");
        }

        [Test]
        public void test_footstep_emitter_stationary_is_completely_silent()
        {
            // Arrange & Act (AC-NOISE-01)
            float radiusStationary = _emitter.CalculateNominalNoiseRadius(planarSpeed: 0.0f, isCrouched: false);
            float radiusCreep = _emitter.CalculateNominalNoiseRadius(planarSpeed: 0.05f, isCrouched: false);

            // Assert
            Assert.AreEqual(0.0f, radiusStationary, "Stationary player must emit 0.0m noise radius.");
            Assert.AreEqual(0.0f, radiusCreep, "Sub-threshold stationary creep must emit 0.0m noise radius.");
        }

        [Test]
        public void test_footstep_emitter_walk_produces_4m_radius()
        {
            // Arrange & Act (AC-NOISE-01)
            float radiusWalk = _emitter.CalculateNominalNoiseRadius(planarSpeed: 2.5f, isCrouched: false);

            // Assert
            Assert.AreEqual(4.0f, radiusWalk, 0.01f, "Upright walk movement must emit 4.0m noise radius.");
        }

        [Test]
        public void test_footstep_emitter_run_produces_6m_radius()
        {
            // Arrange & Act (AC-NOISE-01)
            float radiusRun = _emitter.CalculateNominalNoiseRadius(planarSpeed: 5.0f, isCrouched: false);

            // Assert
            Assert.AreEqual(6.0f, radiusRun, 0.01f, "Upright run movement must emit 6.0m noise radius.");
        }

        [Test]
        public void test_footstep_emitter_emits_event_on_stride_threshold()
        {
            // Arrange: Walk mode (threshold = 1.9m) (AC-NOISE-01)
            _playerObject.transform.position = Vector3.zero;
            _emitter.Tick(0.1f); // Initialize baseline

            bool noiseFired = false;
            Vector3 firedOrigin = Vector3.zero;
            float firedRadius = 0f;

            _emitter.OnFootstepNoiseEmitted += (pos, rad) =>
            {
                noiseFired = true;
                firedOrigin = pos;
                firedRadius = rad;
            };

            // Act 1: Move 1.0m (< 1.9m stride)
            _playerObject.transform.position = new Vector3(1.0f, 0, 0);
            _emitter.Tick(0.4f); // speed = 2.5 m/s

            Assert.IsFalse(noiseFired, "Noise must not fire before stride length threshold is reached.");
            Assert.AreEqual(1.0f, _emitter.StrideDistanceAccumulator, 0.01f);

            // Act 2: Move another 1.0m (total 2.0m > 1.9m)
            _playerObject.transform.position = new Vector3(2.0f, 0, 0);
            _emitter.Tick(0.4f); // speed = 2.5 m/s

            // Assert: Stride emitted
            Assert.IsTrue(noiseFired, "Noise must fire when stride threshold is crossed.");
            Assert.AreEqual(4.0f, firedRadius, 0.01f);
            Assert.AreEqual(new Vector3(2.0f, 0.25f, 0), firedOrigin, "Noise origin must be feet + 0.25m offset.");
            Assert.AreEqual(0.1f, _emitter.StrideDistanceAccumulator, 0.02f, "Remainder must be retained.");
        }

        [Test]
        public void test_guard_hearing_detects_sound_within_planar_radius_unoccluded()
        {
            // Arrange (AC-NOISE-02)
            Vector3 guardPos = new Vector3(0, 0, 0);
            Vector3 noiseOrigin = new Vector3(3.0f, 0.25f, 0); // planar dist = 3.0m < 4.0m
            _mockPhysics.IsOccluded = false;

            // Act
            bool heard = _hearingSensor.EvaluateHearing(guardPos, noiseOrigin, 4.0f, out bool withinRadius, out bool occluded);

            // Assert
            Assert.IsTrue(withinRadius, "Noise at 3.0m must be within 4.0m planar radius.");
            Assert.IsFalse(occluded, "Noise must not be occluded when physics reports clear.");
            Assert.IsTrue(heard, "Guard must hear unoccluded sound within radius.");
        }

        [Test]
        public void test_guard_hearing_ignores_sound_outside_planar_radius()
        {
            // Arrange: planar distance = 5.0m > 4.0m walk radius (AC-NOISE-02)
            Vector3 guardPos = new Vector3(0, 0, 0);
            Vector3 noiseOrigin = new Vector3(5.0f, 0.25f, 0);
            _mockPhysics.IsOccluded = false;

            // Act
            bool heard = _hearingSensor.EvaluateHearing(guardPos, noiseOrigin, 4.0f, out bool withinRadius, out bool occluded);

            // Assert
            Assert.IsFalse(withinRadius, "Noise at 5.0m must be outside 4.0m walk radius.");
            Assert.IsFalse(heard, "Guard must not hear sound outside planar radius.");
        }

        [Test]
        public void test_guard_hearing_blocked_by_solid_world_wall()
        {
            // Arrange: distance = 2.0m (< 4.0m), but blocked by Layer 20 World wall (AC-NOISE-03)
            Vector3 guardPos = new Vector3(0, 0, 0);
            Vector3 noiseOrigin = new Vector3(2.0f, 0.25f, 0);
            _mockPhysics.IsOccluded = true; // Linecast hits Layer 20 solid wall

            // Act
            bool heard = _hearingSensor.EvaluateHearing(guardPos, noiseOrigin, 4.0f, out bool withinRadius, out bool occluded);

            // Assert
            Assert.IsTrue(withinRadius, "Noise at 2.0m is within radius.");
            Assert.IsTrue(occluded, "Occlusion Linecast must detect solid wall.");
            Assert.IsFalse(heard, "Solid wall must completely block sound detection.");
        }

        [Test]
        public void test_guard_hearing_triggers_investigation_at_noise_origin()
        {
            // Arrange (AC-NOISE-04)
            _guardObject.transform.position = new Vector3(0, 0, 0);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _guardFsm.CurrentState);

            Vector3 noiseOrigin = new Vector3(2.5f, 0.25f, 0);
            _mockPhysics.IsOccluded = false;

            bool noiseHeardFired = false;
            Vector3 reportedOrigin = Vector3.zero;
            _hearingSensor.OnNoiseHeard += (origin, rad) =>
            {
                noiseHeardFired = true;
                reportedOrigin = origin;
            };

            // Act: Guard receives footstep noise
            _hearingSensor.HandleFootstepNoise(noiseOrigin, nominalRadius: 4.0f);

            // Assert: Guard transitions to Investigate and target is set to noise origin
            Assert.IsTrue(noiseHeardFired);
            Assert.AreEqual(noiseOrigin, reportedOrigin);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _guardFsm.CurrentState,
                "Patrolling guard must transition to Investigate upon hearing noise.");
            Assert.AreEqual(noiseOrigin, _guardFsm.InvestigationTarget,
                "Investigation target must be set to noise origin.");
            Assert.AreEqual(5.00f, _guardAgent.speed, 0.01f,
                "Agent speed must switch to Investigate speed (5.00 m/s).");
        }

        [Test]
        public void test_guard_in_chase_ignores_footstep_noise()
        {
            // Arrange: Guard is in Chase (R13 Chase-wins rule)
            _guardFsm.TriggerChase(new Vector3(10, 0, 10));
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _guardFsm.CurrentState);

            Vector3 noiseOrigin = new Vector3(2.0f, 0.25f, 0);
            _mockPhysics.IsOccluded = false;

            // Act: Noise occurs
            _hearingSensor.HandleFootstepNoise(noiseOrigin, nominalRadius: 4.0f);

            // Assert: Guard remains in Chase (does not divert to Investigate)
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _guardFsm.CurrentState,
                "Guard in Chase must ignore footstep noise and maintain chase.");
            Assert.AreEqual(7.50f, _guardAgent.speed, 0.01f);
        }
    }
}
