using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;
using WhisperWard.AI.Navigation;

namespace WhisperWard.Tests.Unit.AI
{
    [TestFixture]
    public class GuardSearchDwellGiveupTest
    {
        private GameObject _guardObject;
        private GameObject _playerObject;
        private NavMeshAgent _agent;
        private VisionConeSensor _sensor;
        private SuspicionAccumulator _accumulator;
        private SimplePatrolDriver _patrolDriver;
        private GuardFSMRuntimeController _controller;

        [SetUp]
        public void SetUp()
        {
            _guardObject = new GameObject("Guard_SearchDwell_Test");
            _playerObject = new GameObject("Player_SearchDwell_Test");
            _playerObject.transform.position = new Vector3(0, 0, 20.0f);

            _agent = _guardObject.AddComponent<NavMeshAgent>();
            _sensor = _guardObject.AddComponent<VisionConeSensor>();
            _accumulator = _guardObject.AddComponent<SuspicionAccumulator>();
            _patrolDriver = _guardObject.AddComponent<SimplePatrolDriver>();
            _controller = _guardObject.AddComponent<GuardFSMRuntimeController>();

            _controller.Configure(
                _agent,
                _sensor,
                _accumulator,
                _patrolDriver,
                _playerObject.transform,
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
            if (_guardObject != null) Object.DestroyImmediate(_guardObject);
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
        }

        [Test]
        public void test_guard_dwell_search_initiates_upon_arrival_at_investigation_target()
        {
            // Arrange (AC-GUARD-15)
            Vector3 investigatePos = new Vector3(3.0f, 0.0f, 4.0f);
            _controller.TriggerInvestigate(investigatePos, "test_noise");

            // Guard is currently at (0, 0, 0), target is (3, 0, 4) -> distance = 5.0m (> 0.5m arrival tolerance)
            _controller.Tick(0.10f);
            Assert.IsFalse(_controller.IsDwellingAtLKP, "Guard should not be dwelling while traveling to LKP.");
            Assert.AreEqual(0.0f, _controller.GiveupTimer, 0.001f);

            // Act: Guard arrives within arrival tolerance of LKP
            _guardObject.transform.position = investigatePos;
            _controller.Tick(0.10f);

            // Assert: Guard switches to dwell search
            Assert.IsTrue(_controller.IsDwellingAtLKP, "Guard must enter dwell search mode upon reaching LKP.");
            Assert.AreEqual(0.10f, _controller.GiveupTimer, 0.01f);
        }

        [Test]
        public void test_guard_dwell_search_performs_look_around_sweep()
        {
            // Arrange: Guard reaches LKP (AC-GUARD-15)
            Vector3 investigatePos = new Vector3(2.0f, 0.0f, 0.0f);
            _guardObject.transform.position = investigatePos;
            _guardObject.transform.rotation = Quaternion.Euler(0, 90.0f, 0); // facing east (90 deg)

            _controller.TriggerInvestigate(investigatePos, "sight_committed");
            _controller.Tick(0.05f); // triggers dwell switch

            Assert.IsTrue(_controller.IsDwellingAtLKP);

            // Act: Advance simulation by 1.0s (1/4 of 4.0s timeout -> sin(2*pi*0.25) = sin(pi/2) = 1.0)
            _controller.Tick(0.95f); // total timer = 1.0s

            // Assert: Yaw should be shifted by +45 deg (90 + 45 = 135 deg)
            float expectedYaw = 90.0f + 45.0f;
            Assert.AreEqual(expectedYaw, _guardObject.transform.eulerAngles.y, 1.0f,
                "Guard heading should scan to +45 degrees from base arrival heading during 1st quarter of dwell.");
        }

        [Test]
        public void test_guard_giveup_timer_expires_and_returns_to_patrol()
        {
            // Arrange (AC-GUARD-16)
            Vector3 investigatePos = new Vector3(5.0f, 0.0f, 0.0f);
            _guardObject.transform.position = investigatePos;
            _controller.TriggerInvestigate(investigatePos, "test_target");
            _controller.Tick(0.05f); // enters dwell

            bool givenUpFired = false;
            Vector3 reportedLKP = Vector3.zero;
            _controller.OnInvestigateGivenUp += lkp =>
            {
                givenUpFired = true;
                reportedLKP = lkp;
            };

            // Act: Advance timer past 4.0s (e.g. 4.05s)
            _controller.Tick(4.00f);

            // Assert: Gives up and transitions back to Patrol
            Assert.IsTrue(givenUpFired, "OnInvestigateGivenUp event must fire upon 4.0s timeout.");
            Assert.AreEqual(investigatePos, reportedLKP);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _controller.CurrentState,
                "Guard state must return to Patrol after dwell search times out.");
            Assert.AreEqual(2.30f, _agent.speed, 0.01f, "Agent speed must revert to Patrol speed (2.30 m/s).");
            Assert.IsTrue(_patrolDriver.enabled, "PatrolDriver must be re-enabled on return to Patrol.");
            Assert.IsFalse(_controller.IsDwellingAtLKP, "IsDwellingAtLKP must reset when returning to Patrol.");
        }

        [Test]
        public void test_guard_giveup_applies_fruitless_investigation_residual_bump()
        {
            // Arrange (GDD R15 Fruitless investigation residual addition)
            _accumulator.Configure(residualTau: 16.0f);
            Assert.AreEqual(0.0f, _accumulator.ResidualWariness, 0.001f);

            Vector3 investigatePos = new Vector3(0.0f, 0.0f, 0.0f);
            _guardObject.transform.position = investigatePos;
            _controller.TriggerInvestigate(investigatePos, "test_target");
            _controller.Tick(0.05f);

            // Act: Complete 4.0s dwell give-up
            _controller.Tick(4.00f);

            // Assert: Residual wariness was bumped by +0.15 (decayed slightly over 0s since applied at completion)
            Assert.GreaterOrEqual(_accumulator.ResidualWariness, 0.14f,
                "Residual wariness must increase by +0.15 after fruitless investigation give-up.");
        }
    }
}
