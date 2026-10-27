using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.Tests.Unit.AI
{
    [TestFixture]
    public class AIPerceptionTuningTest
    {
        private class MockPhysicsQueryService : IPhysicsQueryService
        {
            public bool Linecast(Vector3 start, Vector3 end, int layerMask) => false;
            public bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance, int layerMask)
            {
                hit = default;
                return false;
            }
            public int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask) => 0;
            public int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit[] results, float maxDistance, int layerMask) => 0;
        }

        private GameObject _guardObj;
        private VisionConeSensor _sensor;
        private SuspicionAccumulator _accumulator;
        private GuardFSMRuntimeController _fsm;
        private GameObject _targetObj;

        [SetUp]
        public void SetUp()
        {
            _guardObj = new GameObject("Guard_AITuningHost");
            _guardObj.transform.position = Vector3.zero;
            _guardObj.transform.rotation = Quaternion.identity;

            _sensor = _guardObj.AddComponent<VisionConeSensor>();
            _sensor.Configure(new MockPhysicsQueryService());

            _accumulator = _guardObj.AddComponent<SuspicionAccumulator>();
            _fsm = _guardObj.AddComponent<GuardFSMRuntimeController>();

            _targetObj = new GameObject("Player_TargetHost");
            _targetObj.transform.position = new Vector3(0f, 0f, 5f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_targetObj != null) Object.DestroyImmediate(_targetObj);
            if (_guardObj != null) Object.DestroyImmediate(_guardObj);
        }

        [Test]
        public void test_vision_cone_fov_calibrated_to_90_degrees()
        {
            // Assert calibrated default FOV is exactly 90.0 degrees (half-angle 45.0 degrees)
            Assert.AreEqual(90.0f, _sensor.VisionFOV, 0.001f);

            _sensor.SetTarget(_targetObj.transform);

            // Act 1: Place target at 40 degrees yaw (inside 45 deg half-angle)
            Quaternion rotInside = Quaternion.Euler(0f, 40f, 0f);
            _targetObj.transform.position = rotInside * (Vector3.forward * 5f);
            _sensor.Tick(0.20f);

            // Assert: Detected inside cone
            Assert.IsTrue(_sensor.InCone, "Target at 40 degrees should be inside the 90-degree cone (half-angle 45 deg).");

            // Act 2: Place target at 48 degrees yaw (outside 45 deg half-angle, which previously was inside 100 deg cone)
            Quaternion rotOutside = Quaternion.Euler(0f, 48f, 0f);
            _targetObj.transform.position = rotOutside * (Vector3.forward * 5f);
            _sensor.Tick(0.20f);

            // Assert: Excluded from cone, creating the fair peripheral stealth window
            Assert.IsFalse(_sensor.InCone, "Target at 48 degrees must be outside the calibrated 90-degree cone.");
        }

        [Test]
        public void test_glancing_vision_accumulation_respects_confirm_window()
        {
            // Arrange
            bool confirmElapsed = false;
            _accumulator.OnConfirmWindowElapsed += () => confirmElapsed = true;

            // Act: Simulate a quick 0.3s peripheral glance at distance 6m
            // Charge rate at 6m = min(1.5 / 6.0, 0.60) = 0.25 A/s
            // In 0.3s, accumulator gains 0.075 A (below T_base 0.30)
            _accumulator.Tick(0.30f, hasLOS: true, distance: 6.0f);

            // Assert: Confirm window did not elapse; suspicion remains in subtle awareness range
            Assert.IsFalse(confirmElapsed);
            Assert.Less(_accumulator.AccumulatorValue, _accumulator.EntryThreshold);
            Assert.Greater(_accumulator.AccumulatorValue, 0.05f);

            // Break LOS before entry threshold
            _accumulator.Tick(0.20f, hasLOS: false, distance: 6.0f);
            Assert.IsFalse(confirmElapsed);
        }

        [Test]
        public void test_giveup_pacing_cleanly_resumes_patrol_route()
        {
            // Arrange
            bool giveUpFired = false;
            Vector3 givenUpLKP = Vector3.zero;

            _fsm.OnInvestigateGivenUp += lkp =>
            {
                giveUpFired = true;
                givenUpLKP = lkp;
            };

            Vector3 lkpTarget = new Vector3(0f, 0f, 0.5f); // Near guard so arrival tolerance met immediately
            _fsm.TriggerInvestigate(lkpTarget, "test-investigate");

            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _fsm.CurrentState);

            // Act: Tick dwell time step by step up to giveup timeout (4.0 seconds)
            _fsm.Tick(1.0f);
            Assert.IsFalse(giveUpFired);

            _fsm.Tick(1.5f);
            Assert.IsFalse(giveUpFired);

            // Complete the dwell timeout (total >= 4.0s)
            _fsm.Tick(2.0f);

            // Assert: Guard gave up cleanly, fired give-up event, and returned to Patrol
            Assert.IsTrue(giveUpFired);
            Assert.AreEqual(lkpTarget, givenUpLKP);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _fsm.CurrentState);
        }
    }
}
