using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.Alert;
using WhisperWard.AI.FSM;

namespace WhisperWard.Tests.Integration.AI
{
    [TestFixture]
    public class GuardAlertPropagationTest
    {
        private GameObject _playerObject;
        private GameObject _guardAObject;
        private GameObject _guardBObject;
        private GameObject _guardCObject;

        private GuardFSMRuntimeController _fsmA;
        private GuardFSMRuntimeController _fsmB;
        private GuardFSMRuntimeController _fsmC;

        private GuardRadioAlertTransceiver _transceiverA;
        private GuardRadioAlertTransceiver _transceiverB;
        private GuardRadioAlertTransceiver _transceiverC;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player_Integration_Test");
            _playerObject.transform.position = new Vector3(10f, 0f, 0f);

            // Guard A (Alert Emitter): position (0, 0, 0)
            _guardAObject = new GameObject("Guard_A");
            _guardAObject.transform.position = Vector3.zero;
            var agentA = _guardAObject.AddComponent<NavMeshAgent>();
            _fsmA = _guardAObject.AddComponent<GuardFSMRuntimeController>();
            _transceiverA = _guardAObject.AddComponent<GuardRadioAlertTransceiver>();
            _fsmA.Configure(agentA, null, null, null, _playerObject.transform, 2.30f, 5.00f, 7.50f);
            _transceiverA.Configure(_fsmA, guardId: 1, radioRadius: 25.0f, cooldownDuration: 5.0f, investigateErrorRadius: 1.50f);

            // Guard B (Nearby Peer in radio range): position (12, 0, 0) -> dist = 12m <= 25m
            _guardBObject = new GameObject("Guard_B");
            _guardBObject.transform.position = new Vector3(12f, 0f, 0f);
            var agentB = _guardBObject.AddComponent<NavMeshAgent>();
            _fsmB = _guardBObject.AddComponent<GuardFSMRuntimeController>();
            _transceiverB = _guardBObject.AddComponent<GuardRadioAlertTransceiver>();
            _fsmB.Configure(agentB, null, null, null, _playerObject.transform, 2.30f, 5.00f, 7.50f);
            _transceiverB.Configure(_fsmB, guardId: 2, radioRadius: 25.0f, cooldownDuration: 5.0f, investigateErrorRadius: 1.50f);

            // Guard C (Far Peer outside radio range): position (35, 0, 0) -> dist = 35m > 25m
            _guardCObject = new GameObject("Guard_C");
            _guardCObject.transform.position = new Vector3(35f, 0f, 0f);
            var agentC = _guardCObject.AddComponent<NavMeshAgent>();
            _fsmC = _guardCObject.AddComponent<GuardFSMRuntimeController>();
            _transceiverC = _guardCObject.AddComponent<GuardRadioAlertTransceiver>();
            _fsmC.Configure(agentC, null, null, null, _playerObject.transform, 2.30f, 5.00f, 7.50f);
            _transceiverC.Configure(_fsmC, guardId: 3, radioRadius: 25.0f, cooldownDuration: 5.0f, investigateErrorRadius: 1.50f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
            if (_guardAObject != null) Object.DestroyImmediate(_guardAObject);
            if (_guardBObject != null) Object.DestroyImmediate(_guardBObject);
            if (_guardCObject != null) Object.DestroyImmediate(_guardCObject);
        }

        [Test]
        public void test_coordinated_alert_propagation_multi_guard_network()
        {
            // Arrange
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _fsmA.CurrentState);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _fsmB.CurrentState);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _fsmC.CurrentState);

            bool guardBHeardAlert = false;
            bool guardCHeardAlert = false;

            _transceiverB.OnAlertReceived += (alert, target) => guardBHeardAlert = true;
            _transceiverC.OnAlertReceived += (alert, target) => guardCHeardAlert = true;

            // Act: Guard A detects player and triggers Chase
            _fsmA.TriggerChase(_playerObject.transform.position);

            // Assert:
            // 1. Guard A enters Chase
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _fsmA.CurrentState);

            // 2. Guard B (in range) receives alert and transitions to Investigate
            Assert.IsTrue(guardBHeardAlert, "Guard B (12m away) must receive Guard A's radio bark alert.");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _fsmB.CurrentState,
                "Guard B must transition to Investigate upon receiving coordinated alert.");

            // 3. Guard C (out of range at 35m) does NOT receive alert and remains in Patrol
            Assert.IsFalse(guardCHeardAlert, "Guard C (35m away) must be outside 25m radio range.");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Patrol, _fsmC.CurrentState,
                "Guard C must remain in Patrol state.");
        }

        [Test]
        public void test_alert_propagation_respects_chase_wins_when_all_guards_active()
        {
            // Arrange: Guard B is already actively pursuing player
            _fsmB.TriggerChase(_playerObject.transform.position);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _fsmB.CurrentState);

            bool alertProcessedByB = false;
            _transceiverB.OnAlertReceived += (alert, target) => alertProcessedByB = true;

            // Act: Guard A also initiates Chase and broadcasts alert
            _fsmA.TriggerChase(_playerObject.transform.position);

            // Assert
            Assert.IsFalse(alertProcessedByB, "Guard B in active Chase must ignore incoming peer alert.");
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, _fsmB.CurrentState,
                "Guard B must maintain Chase state without interruption (R13 Chase-wins).");
        }

        [Test]
        public void test_alert_cooldown_prevents_rapid_fire_spam()
        {
            // Arrange
            _transceiverA.ResetCooldown();

            // Act 1: Initial broadcast
            bool broadcast1 = _transceiverA.TryBroadcastRadioBark(_playerObject.transform.position, currentTime: 5.0f, out _);
            Assert.IsTrue(broadcast1);

            // Act 2: Immediate follow-up attempt at t = 6.0s (1.0s elapsed < 5.0s cooldown)
            bool broadcast2 = _transceiverA.TryBroadcastRadioBark(_playerObject.transform.position, currentTime: 6.0f, out string reason2);
            Assert.IsFalse(broadcast2);
            Assert.AreEqual("ALERT_COOLDOWN_ACTIVE", reason2);

            // Act 3: Attempt at t = 10.1s (5.1s elapsed > 5.0s cooldown)
            bool broadcast3 = _transceiverA.TryBroadcastRadioBark(_playerObject.transform.position, currentTime: 10.1f, out string reason3);
            Assert.IsTrue(broadcast3);
            Assert.IsNull(reason3);
        }
    }
}
