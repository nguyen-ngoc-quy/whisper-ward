using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.Alert;
using WhisperWard.AI.FSM;

namespace WhisperWard.Tests.Unit.AI
{
    [TestFixture]
    public class MultiGuardSearchStandoffTest
    {
        private GameObject _coordinatorObject;
        private MultiGuardSearchCoordinator _coordinator;

        private GameObject _guardAObject;
        private GameObject _guardBObject;
        private GuardFSMRuntimeController _fsmA;
        private GuardFSMRuntimeController _fsmB;

        [SetUp]
        public void SetUp()
        {
            _coordinatorObject = new GameObject("Coordinator_Test");
            _coordinator = _coordinatorObject.AddComponent<MultiGuardSearchCoordinator>();
            _coordinator.Configure(minSeparation: 2.00f, clusterRadius: 3.00f, maxAssignments: 16);

            _guardAObject = new GameObject("Guard_A_Test");
            _guardAObject.transform.position = Vector3.zero;
            _fsmA = _guardAObject.AddComponent<GuardFSMRuntimeController>();
            _fsmA.Configure(null, null, null, null, null);

            _guardBObject = new GameObject("Guard_B_Test");
            _guardBObject.transform.position = new Vector3(5f, 0f, 0f);
            _fsmB = _guardBObject.AddComponent<GuardFSMRuntimeController>();
            _fsmB.Configure(null, null, null, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_coordinatorObject != null) Object.DestroyImmediate(_coordinatorObject);
            if (_guardAObject != null) Object.DestroyImmediate(_guardAObject);
            if (_guardBObject != null) Object.DestroyImmediate(_guardBObject);
        }

        [Test]
        public void test_multi_guard_two_guards_investigating_same_target_apply_standoff_offsets()
        {
            // Arrange
            Vector3 lkp = new Vector3(10f, 0f, 5f);
            float minSeparation = 2.00f;

            // Act
            Vector3 pos0 = MultiGuardSearchCoordinator.CalculateStandoffPosition(lkp, 0, 2, minSeparation);
            Vector3 pos1 = MultiGuardSearchCoordinator.CalculateStandoffPosition(lkp, 1, 2, minSeparation);
            float dist = Vector3.Distance(pos0, pos1);

            // Assert
            Assert.AreEqual(lkp.x, pos0.x, 0.001f);
            Assert.AreEqual(lkp.z, pos0.z, 0.001f);
            Assert.GreaterOrEqual(dist, minSeparation - 0.001f);
            Assert.AreEqual(2.00f, dist, 0.001f);
        }

        [Test]
        public void test_multi_guard_standoff_separation_is_at_least_2m_between_all_guards()
        {
            // Arrange
            Vector3 lkp = new Vector3(20f, 0f, -10f);
            int totalGuards = 4;
            float minSeparation = 2.00f;
            Vector3[] positions = new Vector3[totalGuards];

            // Act
            for (int i = 0; i < totalGuards; i++)
            {
                positions[i] = MultiGuardSearchCoordinator.CalculateStandoffPosition(lkp, i, totalGuards, minSeparation);
            }

            // Assert - Check all pairwise distances >= 2.0m
            for (int i = 0; i < totalGuards; i++)
            {
                for (int j = i + 1; j < totalGuards; j++)
                {
                    float dist = Vector3.Distance(positions[i], positions[j]);
                    Assert.GreaterOrEqual(dist, minSeparation - 0.001f,
                        $"Distance between guard {i} and guard {j} must be >= {minSeparation}m, but was {dist}m");
                }
            }
        }

        [Test]
        public void test_multi_guard_dwell_lookaround_angles_are_divergent_for_two_guards()
        {
            // Arrange & Act
            float yaw0 = MultiGuardSearchCoordinator.CalculateDivergentYawOffset(0, 2);
            float yaw1 = MultiGuardSearchCoordinator.CalculateDivergentYawOffset(1, 2);

            // Assert
            Assert.AreEqual(0.0f, yaw0, 0.001f);
            Assert.AreEqual(180.0f, yaw1, 0.001f);
            float angleDelta = Mathf.Abs(yaw1 - yaw0);
            Assert.AreEqual(180.0f, angleDelta, 0.001f);
        }

        [Test]
        public void test_multi_guard_dwell_lookaround_angles_diverge_equally_for_three_guards()
        {
            // Arrange & Act
            float yaw0 = MultiGuardSearchCoordinator.CalculateDivergentYawOffset(0, 3);
            float yaw1 = MultiGuardSearchCoordinator.CalculateDivergentYawOffset(1, 3);
            float yaw2 = MultiGuardSearchCoordinator.CalculateDivergentYawOffset(2, 3);

            // Assert
            Assert.AreEqual(0.0f, yaw0, 0.001f);
            Assert.AreEqual(120.0f, yaw1, 0.001f);
            Assert.AreEqual(240.0f, yaw2, 0.001f);
        }

        [Test]
        public void test_multi_guard_coordinator_registers_and_releases_guards_without_leak()
        {
            // Arrange
            Vector3 lkp = new Vector3(15f, 0f, 10f);
            int guard1Id = 101;
            int guard2Id = 102;

            // Act - Register two guards
            Vector3 pos1 = _coordinator.RegisterGuardInvestigation(guard1Id, lkp, out float yaw1);
            Vector3 pos2 = _coordinator.RegisterGuardInvestigation(guard2Id, lkp, out float yaw2);

            // Assert
            Assert.AreEqual(2, _coordinator.ActiveCount);
            Assert.GreaterOrEqual(Vector3.Distance(pos1, pos2), 2.00f - 0.001f);
            Assert.AreEqual(0f, yaw1, 0.001f);
            Assert.AreEqual(180f, yaw2, 0.001f);

            // Act - Release guard 1
            _coordinator.ReleaseGuard(guard1Id);

            // Assert
            Assert.AreEqual(1, _coordinator.ActiveCount);
            Assert.IsFalse(_coordinator.TryGetGuardAssignment(guard1Id, out _));
            Assert.IsTrue(_coordinator.TryGetGuardAssignment(guard2Id, out var assign2));
            Assert.AreEqual(guard2Id, assign2.GuardId);

            // Act - Clear all
            _coordinator.ClearAll();
            Assert.AreEqual(0, _coordinator.ActiveCount);
        }

        [Test]
        public void test_multi_guard_fsm_integration_applies_divergent_dwell_sweep()
        {
            // Arrange
            Vector3 lkp = new Vector3(5f, 0f, 5f);
            _coordinator.CoordinateGuardInvestigation(1, _fsmA, lkp);
            _coordinator.CoordinateGuardInvestigation(2, _fsmB, lkp);

            // Assert - FSM states and offsets
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _fsmA.CurrentState);
            Assert.AreEqual(GuardFSMRuntimeController.GuardState.Investigate, _fsmB.CurrentState);

            Assert.AreEqual(0f, _fsmA.DwellYawOffset, 0.001f);
            Assert.AreEqual(180f, _fsmB.DwellYawOffset, 0.001f);

            // Verify investigate destinations are separated by >= 2.0m
            float destDistance = Vector3.Distance(_fsmA.InvestigationTarget, _fsmB.InvestigationTarget);
            Assert.GreaterOrEqual(destDistance, 2.00f - 0.001f);
        }
    }
}
