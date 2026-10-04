using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.FSM;
using WhisperWard.UI.Feedback;

namespace WhisperWard.Tests.Unit.UI
{
    [TestFixture]
    public class GuardOverheadMarkerTest
    {
        private GameObject _guardObject;
        private GuardFSMRuntimeController _fsm;
        private GuardOverheadMarkerController _markerController;

        [SetUp]
        public void SetUp()
        {
            _guardObject = new GameObject("Guard_With_Marker");
            _fsm = _guardObject.AddComponent<GuardFSMRuntimeController>();
            _fsm.Configure(null, null, null, null, null);

            _markerController = _guardObject.AddComponent<GuardOverheadMarkerController>();
            _markerController.Configure(_fsm, _guardObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_guardObject != null)
            {
                Object.DestroyImmediate(_guardObject);
            }
        }

        [Test]
        public void test_overhead_marker_hidden_on_patrol_state()
        {
            // Arrange & Act
            _markerController.UpdateMarkerForState(GuardFSMRuntimeController.GuardState.Patrol);

            // Assert
            Assert.AreEqual(GuardOverheadMarkerController.MarkerType.None, _markerController.CurrentMarkerType);
            Assert.IsFalse(_markerController.IsVisible);
        }

        [Test]
        public void test_overhead_marker_shows_question_mark_on_investigate_state()
        {
            // Arrange & Act
            _markerController.UpdateMarkerForState(GuardFSMRuntimeController.GuardState.Investigate);

            // Assert
            Assert.AreEqual(GuardOverheadMarkerController.MarkerType.Question, _markerController.CurrentMarkerType);
            Assert.IsTrue(_markerController.IsVisible);
            Assert.AreEqual(1.0f, _markerController.CurrentColor.r, 0.01f);
            Assert.AreEqual(0.72f, _markerController.CurrentColor.g, 0.01f);
            Assert.AreEqual(0.0f, _markerController.CurrentColor.b, 0.01f);
        }

        [Test]
        public void test_overhead_marker_shows_exclamation_mark_on_chase_state()
        {
            // Arrange & Act
            _markerController.UpdateMarkerForState(GuardFSMRuntimeController.GuardState.Chase);

            // Assert
            Assert.AreEqual(GuardOverheadMarkerController.MarkerType.Exclamation, _markerController.CurrentMarkerType);
            Assert.IsTrue(_markerController.IsVisible);
            Assert.AreEqual(1.0f, _markerController.CurrentColor.r, 0.01f);
            Assert.AreEqual(0.20f, _markerController.CurrentColor.g, 0.01f);
            Assert.AreEqual(0.20f, _markerController.CurrentColor.b, 0.01f);
        }

        [Test]
        public void test_overhead_marker_shows_captured_mark_on_captured_state()
        {
            // Arrange & Act
            _markerController.UpdateMarkerForState(GuardFSMRuntimeController.GuardState.Captured);

            // Assert
            Assert.AreEqual(GuardOverheadMarkerController.MarkerType.Captured, _markerController.CurrentMarkerType);
            Assert.IsTrue(_markerController.IsVisible);
        }

        [Test]
        public void test_overhead_marker_billboards_toward_camera()
        {
            // Arrange
            _guardObject.transform.position = Vector3.zero;
            Vector3 cameraPosition = new Vector3(0f, 5f, 10f); // Camera located North

            // Act
            _markerController.UpdateBillboard(cameraPosition);

            // Assert - Marker anchor forward should point along +Z (North)
            Vector3 forward = _guardObject.transform.forward;
            Assert.AreEqual(0f, forward.x, 0.01f);
            Assert.AreEqual(0f, forward.y, 0.01f);
            Assert.AreEqual(1.0f, forward.z, 0.01f);
        }

        [Test]
        public void test_overhead_marker_dispatches_telegraph_event_on_chase_transition()
        {
            // Arrange
            GuardOverheadMarkerController.MarkerType receivedMarker = GuardOverheadMarkerController.MarkerType.None;
            GuardFSMRuntimeController.GuardState receivedState = GuardFSMRuntimeController.GuardState.Patrol;
            int callbackCount = 0;

            _markerController.OnTelegraphTriggered += (marker, state) =>
            {
                receivedMarker = marker;
                receivedState = state;
                callbackCount++;
            };

            // Act - Trigger state change to Chase
            _fsm.TriggerChase(new Vector3(10f, 0f, 10f));

            // Assert
            Assert.AreEqual(1, callbackCount);
            Assert.AreEqual(GuardOverheadMarkerController.MarkerType.Exclamation, receivedMarker);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Chase, receivedState);
        }

        [Test]
        public void test_overhead_marker_clears_when_guard_returns_to_patrol()
        {
            // Arrange - First enter investigate
            _fsm.TriggerInvestigate(new Vector3(5f, 0f, 0f));
            Assert.IsTrue(_markerController.IsVisible);

            // Act - Return to patrol
            _fsm.TransitionTo(GuardFSMRuntimeController.GuardState.Patrol);

            // Assert
            Assert.AreEqual(GuardOverheadMarkerController.MarkerType.None, _markerController.CurrentMarkerType);
            Assert.IsFalse(_markerController.IsVisible);
        }
    }
}
