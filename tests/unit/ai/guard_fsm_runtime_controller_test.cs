using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.FSM;
using WhisperWard.AI.Perception;
using WhisperWard.AI.Navigation;

namespace WhisperWard.Tests.Unit.AI
{
    [TestFixture]
    public class GuardFSMRuntimeControllerTest
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
            _guardObject = new GameObject("Guard_FSM_Test");
            _playerObject = new GameObject("Player_FSM_Test");
            _playerObject.transform.position = new Vector3(0, 0, 10.0f);

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
        public void test_guard_fsm_initial_state_is_patrol_with_patrol_speed()
        {
            // Assert (AC-GUARD-09, AC-GUARD-10)
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _controller.CurrentState);
            Assert.AreEqual(2.30f, _agent.speed, 0.01f, "Initial speed must be 2.30 m/s for Patrol.");
        }

        [Test]
        public void test_guard_fsm_transitions_to_investigate_and_updates_speed()
        {
            // Arrange (AC-GUARD-09, AC-GUARD-10)
            Vector3 investigatePos = new Vector3(5, 0, 5);

            // Act
            _controller.TriggerInvestigate(investigatePos, "suspicious_noise");

            // Assert
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _controller.CurrentState);
            Assert.AreEqual(5.00f, _agent.speed, 0.01f, "Investigate speed must be 5.00 m/s.");
            Assert.AreEqual(investigatePos, _controller.InvestigationTarget);
            Assert.IsFalse(_patrolDriver.enabled, "PatrolDriver must be disabled during Investigate.");
        }

        [Test]
        public void test_guard_fsm_transitions_to_chase_and_updates_speed()
        {
            // Arrange (AC-GUARD-09, AC-GUARD-10)
            Vector3 playerPos = _playerObject.transform.position;

            // Act
            _controller.TriggerChase(playerPos);

            // Assert
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _controller.CurrentState);
            Assert.AreEqual(7.50f, _agent.speed, 0.01f, "Chase speed must be 7.50 m/s.");
            Assert.IsTrue(_accumulator.IsInChase, "SuspicionAccumulator must be set to Chase mode.");
            Assert.IsFalse(_patrolDriver.enabled, "PatrolDriver must be disabled during Chase.");
        }

        [Test]
        public void test_guard_fsm_catch_condition_triggers_capture_event_and_halts()
        {
            // Arrange: Guard and player within 5.50m (e.g. 4.0m) (AC-GUARD-11)
            _guardObject.transform.position = Vector3.zero;
            _playerObject.transform.position = new Vector3(0, 0, 4.0f);

            bool capturedFired = false;
            Vector3 capturePosition = Vector3.zero;
            _controller.OnPlayerCaptured += pos =>
            {
                capturedFired = true;
                capturePosition = pos;
            };

            // Switch to Chase
            _controller.TriggerChase(_playerObject.transform.position);

            // Act: Sustain catch distance for 0.5s (< 1.0s)
            _controller.Tick(0.50f);
            Assert.IsFalse(capturedFired, "Catch must not trigger before 1.0s duration.");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _controller.CurrentState);

            // Act: Sustain remaining 0.50s (total 1.0s)
            _controller.Tick(0.50f);

            // Assert: Player captured!
            Assert.IsTrue(capturedFired, "Catch must fire after 1.0s sustained proximity.");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Captured, _controller.CurrentState);
            Assert.AreEqual(new Vector3(0, 0, 4.0f), capturePosition);
            Assert.AreEqual(0f, _agent.speed, "Speed must be set to 0 on Captured.");
        }

        [Test]
        public void test_guard_fsm_catch_timer_resets_if_player_escapes_distance()
        {
            // Arrange (AC-GUARD-11)
            _guardObject.transform.position = Vector3.zero;
            _playerObject.transform.position = new Vector3(0, 0, 4.0f);
            _controller.TriggerChase(_playerObject.transform.position);

            // Close proximity for 0.8s
            _controller.Tick(0.80f);
            Assert.AreEqual(0.80f, _controller.CatchTimer, 0.01f);

            // Act: Player dashes away to 8.0m (> 5.50m)
            _playerObject.transform.position = new Vector3(0, 0, 8.0f);
            _controller.Tick(0.10f);

            // Assert: Catch timer resets to 0
            Assert.AreEqual(0.0f, _controller.CatchTimer, 0.001f, "Catch timer must reset when player exceeds 5.50m.");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _controller.CurrentState);
        }

        [Test]
        public void test_guard_fsm_perception_events_automatically_drive_transitions()
        {
            // Arrange: Confirm window commit event from Accumulator
            _controller.TransitionTo(GuardFSMRuntimeController.GuardState.Patrol);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _controller.CurrentState);

            // Act: Confirm window elapses in accumulator
            _accumulator.Configure(tBase: 0.30f);
            _accumulator.Tick(0.50f, hasLOS: true, distance: 2.0f); // opens window
            _accumulator.Tick(1.20f, hasLOS: true, distance: 2.0f); // commits window

            // Assert: Transitioned to Investigate automatically
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _controller.CurrentState);

            // Act 2: Chase threshold reached in accumulator
            _accumulator.Tick(2.0f, hasLOS: true, distance: 1.5f);

            // Assert: Transitioned to Chase automatically
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _controller.CurrentState);
        }
    }
}
