using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Environment;
using WhisperWard.Core.Grade;
using WhisperWard.Core.Objectives;

namespace WhisperWard.Tests.Unit.Gameplay
{
    [TestFixture]
    public class MissionLifecycleTest
    {
        private GameObject _controllerHost;
        private MissionLifecycleController _controller;
        private StealthGradeOperator _gradeOperator;
        private ObjectiveKeycardPickup _pickup;
        private LockedSecurityGate _gate;
        private ExtractionZone _zone;

        [SetUp]
        public void SetUp()
        {
            _controllerHost = new GameObject("MissionController_TestHost");
            _controller = _controllerHost.AddComponent<MissionLifecycleController>();
            _gradeOperator = _controllerHost.AddComponent<StealthGradeOperator>();

            var pickupHost = new GameObject("Keycard_Host");
            _pickup = pickupHost.AddComponent<ObjectiveKeycardPickup>();

            var gateHost = new GameObject("Gate_Host");
            _gate = gateHost.AddComponent<LockedSecurityGate>();

            var zoneHost = new GameObject("Zone_Host");
            _zone = zoneHost.AddComponent<ExtractionZone>();

            _controller.BindSystems(_gradeOperator, _pickup, _gate, _zone);
            _controller.StartMission();
        }

        [TearDown]
        public void TearDown()
        {
            if (_controllerHost != null) Object.DestroyImmediate(_controllerHost);
            if (_pickup != null) Object.DestroyImmediate(_pickup.gameObject);
            if (_gate != null) Object.DestroyImmediate(_gate.gameObject);
            if (_zone != null) Object.DestroyImmediate(_zone.gameObject);
        }

        [Test]
        public void test_mission_lifecycle_starts_in_infiltration_state()
        {
            Assert.AreEqual(MissionState.Infiltration, _controller.CurrentState);
            Assert.IsFalse(_controller.IsFinished);
        }

        [Test]
        public void test_mission_lifecycle_advances_to_objective_acquired()
        {
            _controller.HandleObjectiveAcquired("Keycard_SecurityLevel1");
            Assert.AreEqual(MissionState.ObjectiveAcquired, _controller.CurrentState);
        }

        [Test]
        public void test_mission_lifecycle_advances_to_gate_unlocked()
        {
            _controller.HandleObjectiveAcquired("Keycard_SecurityLevel1");
            _controller.HandleGateUnlocked();
            Assert.AreEqual(MissionState.GateUnlocked, _controller.CurrentState);
        }

        [Test]
        public void test_mission_lifecycle_extraction_completes_mission_and_evaluates_grade()
        {
            _controller.HandleObjectiveAcquired("Keycard_SecurityLevel1");
            _controller.HandleGateUnlocked();
            _controller.HandleExtractionCompleted();

            Assert.AreEqual(MissionState.MissionCompleted, _controller.CurrentState);
            Assert.IsTrue(_controller.IsFinished);
            Assert.AreEqual(GradeRank.S, _gradeOperator.LastResult.Rank);
            Assert.AreEqual(MissionCompletionStatus.Completed, _gradeOperator.LastResult.Status);
        }

        [Test]
        public void test_mission_lifecycle_player_capture_fails_mission_and_triggers_rank_f()
        {
            _controller.HandlePlayerCaptured();

            Assert.AreEqual(MissionState.MissionFailed, _controller.CurrentState);
            Assert.IsTrue(_controller.IsFinished);
            Assert.AreEqual(GradeRank.F, _gradeOperator.LastResult.Rank);
            Assert.AreEqual(MissionCompletionStatus.Failed, _gradeOperator.LastResult.Status);
        }

        [Test]
        public void test_mission_lifecycle_restart_resets_to_infiltration()
        {
            _controller.HandleObjectiveAcquired("Keycard_SecurityLevel1");
            _controller.HandleGateUnlocked();
            _controller.HandleExtractionCompleted();

            _controller.RestartMission();

            Assert.AreEqual(MissionState.Infiltration, _controller.CurrentState);
            Assert.IsFalse(_controller.IsFinished);
            Assert.IsFalse(_gradeOperator.HasEvaluated);
        }
    }
}
