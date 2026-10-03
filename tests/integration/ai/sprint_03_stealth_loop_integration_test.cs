using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;
using WhisperWard.AI.Navigation;
using WhisperWard.Core.Player;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.Tests.Integration.AI
{
    /// <summary>
    /// Integration Test Suite for Sprint 03 Stealth Loop:
    /// Vision Cone Sensor, Suspicion Accumulator, Guard FSM, Search Dwell Give-up, and Player Footstep Noise.
    /// Verifies end-to-end integration across all Sprint 03 systems.
    /// </summary>
    [TestFixture]
    public class Sprint03StealthLoopIntegrationTest
    {
        private GameObject _playerObject;
        private GameObject _guardObject;
        private PlayerFootstepNoiseEmitter _noiseEmitter;
        private VisionConeSensor _visionSensor;
        private SuspicionAccumulator _accumulator;
        private VisionConeVisualizer _coneVisualizer;
        private GuardFSMRuntimeController _guardFsm;
        private GuardHearingSensor _hearingSensor;
        private SimplePatrolDriver _patrolDriver;
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
            _playerObject = new GameObject("Player_Integration");
            _playerObject.transform.position = new Vector3(0, 0, 0);
            _noiseEmitter = _playerObject.AddComponent<PlayerFootstepNoiseEmitter>();

            _guardObject = new GameObject("Guard_Integration");
            _guardObject.transform.position = new Vector3(0, 0, 8.0f);
            _guardObject.transform.rotation = Quaternion.Euler(0, 180f, 0); // facing south toward player at origin

            _guardAgent = _guardObject.AddComponent<NavMeshAgent>();
            _visionSensor = _guardObject.AddComponent<VisionConeSensor>();
            _accumulator = _guardObject.AddComponent<SuspicionAccumulator>();
            _coneVisualizer = _guardObject.AddComponent<VisionConeVisualizer>();
            _patrolDriver = _guardObject.AddComponent<SimplePatrolDriver>();
            _guardFsm = _guardObject.AddComponent<GuardFSMRuntimeController>();
            _hearingSensor = _guardObject.AddComponent<GuardHearingSensor>();

            _mockPhysics = new MockPhysicsQueryService();

            _visionSensor.Configure(_guardObject.transform, _playerObject.transform, _mockPhysics, visionFOV: 100f, visionRange: 12.0f);
            _accumulator.Configure(tBase: 0.30f, tChase: 1.00f, confirmWindowDuration: 1.20f);
            _coneVisualizer.Configure(_guardObject.transform, _visionSensor, _accumulator, _mockPhysics);
            _guardFsm.Configure(_guardAgent, _visionSensor, _accumulator, _patrolDriver, _playerObject.transform,
                patrolSpeed: 2.30f, investigateSpeed: 5.00f, chaseSpeed: 7.50f, catchDistance: 5.50f, catchDuration: 1.00f, giveupTimeout: 4.00f);
            _hearingSensor.Configure(_guardFsm, _mockPhysics);
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
            if (_guardObject != null) Object.DestroyImmediate(_guardObject);
        }

        [Test]
        public void test_stealth_loop_patrol_to_investigate_on_footstep_noise()
        {
            // Guard starts in Patrol at 2.30 m/s
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _guardFsm.CurrentState);
            Assert.AreEqual(2.30f, _guardAgent.speed, 0.01f);

            // Player runs 3.0m away from guard (within 6.0m run noise radius)
            Vector3 noiseOrigin = new Vector3(0, 0.25f, 5.0f); // 3m from guard at (0, 0, 8)
            _mockPhysics.IsOccluded = false;

            // Player emits run noise
            _noiseEmitter.EmitNoise(new Vector3(0, 0, 5.0f), radius: 6.0f);

            // Assert: Guard heard footstep and transitioned to Investigate (5.00 m/s) at noise origin
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _guardFsm.CurrentState);
            Assert.AreEqual(5.00f, _guardAgent.speed, 0.01f);
            Assert.AreEqual(noiseOrigin, _guardFsm.InvestigationTarget);
        }

        [Test]
        public void test_stealth_loop_investigate_dwell_and_giveup_resumes_patrol()
        {
            // Guard investigates a point
            Vector3 investigatePos = new Vector3(2.0f, 0.0f, 8.0f);
            _guardFsm.TriggerInvestigate(investigatePos, "test_noise");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _guardFsm.CurrentState);

            // Guard arrives at target -> begins dwell search
            _guardObject.transform.position = investigatePos;
            _guardFsm.Tick(0.10f);
            Assert.IsTrue(_guardFsm.IsDwellingAtLKP, "Guard should dwell upon arrival at investigation target.");

            // Dwell for 4.0s without sighting player
            _guardFsm.Tick(4.00f);

            // Assert: Give up search, return to Patrol (2.30 m/s), resume patrol driver
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _guardFsm.CurrentState);
            Assert.AreEqual(2.30f, _guardAgent.speed, 0.01f);
            Assert.IsTrue(_patrolDriver.enabled);
            Assert.GreaterOrEqual(_accumulator.ResidualWariness, 0.14f, "Fruitless investigation residual (+0.15) applied.");
        }

        [Test]
        public void test_stealth_loop_vision_los_triggers_suspicion_and_confirm_window()
        {
            // Guard at (0, 0, 8) facing south (0, 0, -1). Player at (0, 0, 2) -> dist = 6.0m, angle = 0 deg (in cone)
            _playerObject.transform.position = new Vector3(0, 0, 2.0f);
            _mockPhysics.IsOccluded = false;

            // Tick vision sensor
            _visionSensor.Tick(0.20f);
            Assert.IsTrue(_visionSensor.HasLineOfSight, "Guard should have line of sight to player.");

            // Accumulator charges suspicion
            _accumulator.Tick(0.50f, hasLOS: true, distance: 6.0f);
            Assert.Greater(_accumulator.SuspicionLevel, 0.05f);

            // Sustain LOS to commit confirm window (1.20s)
            _accumulator.Tick(1.20f, hasLOS: true, distance: 6.0f);

            // Assert: Confirm window commit triggers Investigate at Last Known Position
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _guardFsm.CurrentState);
            Assert.AreEqual(5.00f, _guardAgent.speed, 0.01f);
        }

        [Test]
        public void test_stealth_loop_full_suspicion_triggers_chase_and_catch_resolution()
        {
            // Player very close in open view (dist = 1.0m)
            _playerObject.transform.position = new Vector3(0, 0, 7.0f);
            _mockPhysics.IsOccluded = false;

            bool capturedFired = false;
            _guardFsm.OnPlayerCaptured += pos => capturedFired = true;

            // High rate charge reaches A >= 1.0
            _accumulator.Tick(2.0f, hasLOS: true, distance: 1.0f);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _guardFsm.CurrentState);
            Assert.AreEqual(7.50f, _guardAgent.speed, 0.01f);

            // Sustain catch proximity (dist = 1.0m <= 5.50m) for 1.0s
            _guardFsm.Tick(1.05f);

            // Assert: Player captured!
            Assert.IsTrue(capturedFired, "Catch condition must trigger capture event.");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Captured, _guardFsm.CurrentState);
            Assert.AreEqual(0f, _guardAgent.speed);
        }

        [Test]
        public void test_stealth_loop_crouch_behind_wall_maintains_stealth()
        {
            // Player is crouched
            float noiseRadius = _noiseEmitter.CalculateNominalNoiseRadius(planarSpeed: 1.2f, isCrouched: true);
            Assert.AreEqual(0.0f, noiseRadius, "Crouch movement is completely silent.");

            // Wall occludes vision cone
            _mockPhysics.IsOccluded = true;
            _visionSensor.Tick(0.20f);
            Assert.IsFalse(_visionSensor.HasLineOfSight, "Solid wall occludes guard vision.");

            _accumulator.Tick(0.20f, hasLOS: false, distance: 4.0f);
            Assert.AreEqual(0f, _accumulator.SuspicionLevel);

            // Guard remains on Patrol
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _guardFsm.CurrentState);
            Assert.AreEqual(2.30f, _guardAgent.speed, 0.01f);
        }
    }
}
