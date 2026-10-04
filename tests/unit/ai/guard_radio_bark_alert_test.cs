using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.Alert;
using WhisperWard.AI.FSM;

namespace WhisperWard.Tests.Unit.AI
{
    [TestFixture]
    public class GuardRadioBarkAlertTest
    {
        private GameObject _guardAObject;
        private GameObject _guardBObject;
        private GameObject _playerObject;

        private GuardFSMRuntimeController _fsmA;
        private GuardFSMRuntimeController _fsmB;
        private GuardRadioAlertTransceiver _transceiverA;
        private GuardRadioAlertTransceiver _transceiverB;

        private NavMeshAgent _agentA;
        private NavMeshAgent _agentB;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player_Test");
            _playerObject.transform.position = new Vector3(10f, 0f, 0f);

            // Guard A setup
            _guardAObject = new GameObject("Guard_A");
            _guardAObject.transform.position = Vector3.zero;
            _agentA = _guardAObject.AddComponent<NavMeshAgent>();
            _fsmA = _guardAObject.AddComponent<GuardFSMRuntimeController>();
            _transceiverA = _guardAObject.AddComponent<GuardRadioAlertTransceiver>();

            _fsmA.Configure(
                _agentA,
                sensor: null,
                accumulator: null,
                patrolDriver: null,
                playerTransform: _playerObject.transform,
                patrolSpeed: 2.30f,
                investigateSpeed: 5.00f,
                chaseSpeed: 7.50f);

            _transceiverA.Configure(_fsmA, guardId: 101, radioRadius: 25.00f, cooldownDuration: 5.00f, investigateErrorRadius: 1.50f);

            // Guard B setup
            _guardBObject = new GameObject("Guard_B");
            _guardBObject.transform.position = new Vector3(15f, 0f, 0f); // 15m from Guard A (< 25m)
            _agentB = _guardBObject.AddComponent<NavMeshAgent>();
            _fsmB = _guardBObject.AddComponent<GuardFSMRuntimeController>();
            _transceiverB = _guardBObject.AddComponent<GuardRadioAlertTransceiver>();

            _fsmB.Configure(
                _agentB,
                sensor: null,
                accumulator: null,
                patrolDriver: null,
                playerTransform: _playerObject.transform,
                patrolSpeed: 2.30f,
                investigateSpeed: 5.00f,
                chaseSpeed: 7.50f);

            _transceiverB.Configure(_fsmB, guardId: 102, radioRadius: 25.00f, cooldownDuration: 5.00f, investigateErrorRadius: 1.50f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
            if (_guardAObject != null) Object.DestroyImmediate(_guardAObject);
            if (_guardBObject != null) Object.DestroyImmediate(_guardBObject);
        }

        [Test]
        public void test_guard_radio_transceiver_initial_parameters()
        {
            // Assert (AC-ALERT-01, AC-ALERT-02)
            Assert.AreEqual(25.00f, _transceiverA.RadioRadius, 0.01f);
            Assert.AreEqual(5.00f, _transceiverA.CooldownDuration, 0.01f);
            Assert.AreEqual(1.50f, _transceiverA.InvestigateErrorRadius, 0.01f);
            Assert.AreEqual(101, _transceiverA.GuardId);
        }

        [Test]
        public void test_guard_chase_broadcasts_radio_bark_alert()
        {
            // Arrange
            bool globalAlertFired = false;
            GuardAlertEvent receivedAlert = default;

            GuardRadioAlertTransceiver.OnAnyGuardAlertBroadcast += alert =>
            {
                globalAlertFired = true;
                receivedAlert = alert;
            };

            // Act: Guard A triggers Chase
            _fsmA.TriggerChase(_playerObject.transform.position);

            // Assert
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _fsmA.CurrentState);
            Assert.IsTrue(globalAlertFired, "Radio bark must be broadcast when guard enters Chase state.");
            Assert.AreEqual(101, receivedAlert.EmitterId);
            Assert.AreEqual(_guardAObject.transform.position, receivedAlert.EmitterPosition);
            Assert.AreEqual(_playerObject.transform.position, receivedAlert.TargetLkp);
            Assert.AreEqual(25.00f, receivedAlert.RadioRadius, 0.01f);
        }

        [Test]
        public void test_radio_bark_rate_limit_cooldown()
        {
            // Arrange
            Vector3 target = new Vector3(5f, 0f, 5f);

            // Act 1: Initial broadcast at t = 10.0s
            bool firstSuccess = _transceiverA.TryBroadcastRadioBark(target, currentTime: 10.0f, out string reason1);

            // Assert 1
            Assert.IsTrue(firstSuccess);
            Assert.IsNull(reason1);
            Assert.AreEqual(10.0f, _transceiverA.LastBroadcastTime, 0.01f);

            // Act 2: Attempt broadcast at t = 12.0s (delta = 2.0s < 5.0s cooldown)
            bool rapidSuccess = _transceiverA.TryBroadcastRadioBark(target, currentTime: 12.0f, out string reason2);

            // Assert 2
            Assert.IsFalse(rapidSuccess, "Broadcast must be rejected during 5.0s cooldown window.");
            Assert.AreEqual("ALERT_COOLDOWN_ACTIVE", reason2);

            // Act 3: Attempt broadcast at t = 15.5s (delta = 5.5s >= 5.0s cooldown)
            bool postCooldownSuccess = _transceiverA.TryBroadcastRadioBark(target, currentTime: 15.5f, out string reason3);

            // Assert 3
            Assert.IsTrue(postCooldownSuccess, "Broadcast must succeed after cooldown expires.");
            Assert.IsNull(reason3);
            Assert.AreEqual(15.5f, _transceiverA.LastBroadcastTime, 0.01f);
        }

        [Test]
        public void test_nearby_patrol_guard_receives_alert_and_investigates()
        {
            // Arrange (AC-ALERT-03)
            // Guard B is at (15, 0, 0), in Patrol state
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _fsmB.CurrentState);

            bool guardBAlertReceived = false;
            Vector3 investigateTarget = Vector3.zero;

            _transceiverB.OnAlertReceived += (alert, target) =>
            {
                guardBAlertReceived = true;
                investigateTarget = target;
            };

            // Act: Guard A emits radio bark
            _transceiverA.TryBroadcastRadioBark(_playerObject.transform.position, currentTime: 0f, out _);

            // Assert
            Assert.IsTrue(guardBAlertReceived, "Guard B within 25.0m must receive Guard A's alert.");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _fsmB.CurrentState,
                "Patrolling Guard B must transition to Investigate upon receiving peer radio alert.");

            // Verify investigate target is close to player LKP (within error offset radius)
            float distToLkp = Vector3.Distance(_playerObject.transform.position, _fsmB.InvestigationTarget);
            Assert.LessOrEqual(distToLkp, 1.51f, "Investigation target distance to LKP must be within 1.50m error radius.");
        }

        [Test]
        public void test_investigation_target_includes_bounded_spatial_error()
        {
            // Arrange: Emitter at (0, 0, 0), LKP at (10, 0, 0), errorRadius = 1.50m
            Vector3 emitterPos = Vector3.zero;
            Vector3 lkp = new Vector3(10f, 0f, 0f);
            float errorRadius = 1.50f;

            // Act
            Vector3 computedTarget = GuardRadioAlertTransceiver.ComputeInvestigateTarget(lkp, emitterPos, errorRadius);

            // Assert
            float offsetMagnitude = Vector3.Distance(lkp, computedTarget);
            Assert.AreEqual(1.50f, offsetMagnitude, 0.01f, "Offset magnitude must equal errorRadius.");

            // Lateral offset perpendicular to (1, 0, 0) in XZ is along Z axis (0, 0, 1)
            Assert.AreEqual(10f, computedTarget.x, 0.01f);
            Assert.AreEqual(0f, computedTarget.y, 0.01f);
            Assert.AreNotEqual(0f, computedTarget.z);
        }

        [Test]
        public void test_chasing_guard_ignores_peer_alerts()
        {
            // Arrange (AC-ALERT-04: R13 Chase-wins)
            // Guard B is already chasing a target at (20, 0, 0)
            Vector3 playerTargetB = new Vector3(20f, 0f, 0f);
            _fsmB.TriggerChase(playerTargetB);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _fsmB.CurrentState);

            bool alertHandledByB = false;
            _transceiverB.OnAlertReceived += (alert, target) => alertHandledByB = true;

            // Act: Guard A transmits alert for player at (10, 0, 0)
            _transceiverA.TryBroadcastRadioBark(new Vector3(10f, 0f, 0f), currentTime: 0f, out _);

            // Assert
            Assert.IsFalse(alertHandledByB, "Guard in active Chase must not process peer alert.");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _fsmB.CurrentState,
                "Guard in Chase state must remain in Chase without switching to Investigate.");
        }

        [Test]
        public void test_guard_beyond_radio_radius_ignores_alert()
        {
            // Arrange: Move Guard B to (30, 0, 0) -> distance to Guard A is 30m > 25m radio radius
            _guardBObject.transform.position = new Vector3(30f, 0f, 0f);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _fsmB.CurrentState);

            bool alertReceived = false;
            _transceiverB.OnAlertReceived += (alert, target) => alertReceived = true;

            // Act: Guard A transmits alert
            _transceiverA.TryBroadcastRadioBark(_playerObject.transform.position, currentTime: 0f, out _);

            // Assert
            Assert.IsFalse(alertReceived, "Guard B beyond 25.0m radio coverage must not receive alert.");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _fsmB.CurrentState,
                "Out-of-range guard must remain in Patrol state.");
        }

        [Test]
        public void test_guard_ignores_own_broadcast()
        {
            // Arrange
            bool selfAlertProcessed = false;
            _transceiverA.OnAlertReceived += (alert, target) => selfAlertProcessed = true;

            // Act: Guard A transmits alert
            _transceiverA.TryBroadcastRadioBark(_playerObject.transform.position, currentTime: 0f, out _);

            // Assert
            Assert.IsFalse(selfAlertProcessed, "Guard must reject its own transmitted alert.");
        }
    }
}
