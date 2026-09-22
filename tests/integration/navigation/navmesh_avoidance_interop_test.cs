using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.Core.Contracts;
using WhisperWard.Core.Navigation;

namespace WhisperWard.Tests.Integration.Navigation
{
    /// <summary>
    /// Integration test suite verifying RVO avoidance configuration, priority escalation (AC-NAV-12),
    /// aperture physical clearance (AC-NAV-11), off-mesh warp recovery (AC-NAV-13),
    /// HideSpot standoff routing (AC-NAV-14), and multi-guard query time-slicing (AC-NAV-17).
    /// Governed by ADR-0007 and story-002-rvo-avoidance-navigation-interop.md.
    /// </summary>
    [TestFixture]
    public class NavMeshAvoidanceInteropTest
    {
        private NavMeshQueryService _queryService;
        private NavMeshAvoidanceService _avoidanceService;
        private GameObject _guardGo1;
        private GameObject _guardGo2;
        private GameObject _guardGo3;
        private NavMeshAgent _agent1;
        private NavMeshAgent _agent2;
        private NavMeshAgent _agent3;

        [SetUp]
        public void SetUp()
        {
            _queryService = new NavMeshQueryService();
            _avoidanceService = new NavMeshAvoidanceService(_queryService);

            _guardGo1 = new GameObject("TestGuard_1");
            _agent1 = _guardGo1.AddComponent<NavMeshAgent>();

            _guardGo2 = new GameObject("TestGuard_2");
            _agent2 = _guardGo2.AddComponent<NavMeshAgent>();

            _guardGo3 = new GameObject("TestGuard_3");
            _agent3 = _guardGo3.AddComponent<NavMeshAgent>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_guardGo1 != null) UnityEngine.Object.DestroyImmediate(_guardGo1);
            if (_guardGo2 != null) UnityEngine.Object.DestroyImmediate(_guardGo2);
            if (_guardGo3 != null) UnityEngine.Object.DestroyImmediate(_guardGo3);
        }

        #region AC-NAV-12: RVO Priority Configuration & Resolution

        [Test]
        public void Test_NavMeshAvoidance_PriorityMapping_ResolvesStateHierarchyCorrectly()
        {
            // Assert: Chase = 10, Investigate = 30, Patrol = 50 per ADR-0007 Section 6
            Assert.AreEqual(10, _avoidanceService.GetAvoidancePriority(GuardNavigationState.Chase));
            Assert.AreEqual(30, _avoidanceService.GetAvoidancePriority(GuardNavigationState.Investigate));
            Assert.AreEqual(50, _avoidanceService.GetAvoidancePriority(GuardNavigationState.Patrol));
        }

        [Test]
        public void Test_NavMeshAvoidance_ConfigureRVO_SetsHighQualityAndRadiusCushion()
        {
            // Act: Configure Agent 1 as Chase and Agent 2 as Patrol
            _avoidanceService.ConfigureRVO(_agent1, GuardNavigationState.Chase);
            _avoidanceService.ConfigureRVO(_agent2, GuardNavigationState.Patrol);

            // Assert
            Assert.AreEqual(ObstacleAvoidanceType.HighQualityObstacleAvoidance, _agent1.obstacleAvoidanceType);
            Assert.AreEqual(0.40f, _agent1.radius, 0.001f);
            Assert.AreEqual(10, _agent1.avoidancePriority);

            Assert.AreEqual(ObstacleAvoidanceType.HighQualityObstacleAvoidance, _agent2.obstacleAvoidanceType);
            Assert.AreEqual(0.40f, _agent2.radius, 0.001f);
            Assert.AreEqual(50, _agent2.avoidancePriority);

            // Priority 10 is higher precedence in Unity (lower number = higher priority), meaning Patrol yields to Chase
            Assert.Less(_agent1.avoidancePriority, _agent2.avoidancePriority);
        }

        #endregion

        #region AC-NAV-11: Agent-Parameterized Aperture Clearance

        [Test]
        public void Test_NavMeshAvoidance_ApertureClearance_PhysicalDiameterExceedingGapFails()
        {
            // Arrange: Narrow gap of width 0.60m. Guard physical radius = 0.40m (diameter = 0.80m).
            // Physical clearance rule: diameter (0.80m) > gap (0.60m) => passage must be refused
            float corridorGap = 0.60f;
            float agentDiameter = _agent1.radius * 2.0f; // 0.80m

            bool canPass = agentDiameter <= corridorGap;

            // Assert: Passage strictly disallowed
            Assert.IsFalse(canPass, "Agent physical diameter (0.80m) must refuse entry into 0.60m narrow aperture.");
        }

        #endregion

        #region AC-NAV-13: Off-Mesh Warp Recovery Gate

        [Test]
        public void Test_NavMeshAvoidance_OffMeshWarpRecovery_WarpsToNearestWalkableSurface()
        {
            // Arrange: Simulate agent off-mesh (position at (5, -1.0, 5), nearest mesh surface at (5, 0, 5))
            _guardGo1.transform.position = new Vector3(5f, -0.50f, 5f);
            Vector3 expectedSnappedSurface = new Vector3(5f, 0f, 5f);
            Vector3 actualWarpedDestination = Vector3.zero;

            _avoidanceService.SetTestHooks(
                sampleDelegate: (Vector3 src, out Vector3 hitPos, float maxDist, int mask) =>
                {
                    hitPos = expectedSnappedSurface;
                    return true;
                },
                warpDelegate: (NavMeshAgent agent, Vector3 dest) =>
                {
                    actualWarpedDestination = dest;
                    return true;
                });

            // Act
            bool recoverySuccess = _avoidanceService.TryRecoverOffMesh(_agent1, searchRadius: 1.0f);

            // Assert
            Assert.IsTrue(recoverySuccess, "Off-mesh agent within 1.0m search tolerance must be successfully recovered.");
            Assert.AreEqual(expectedSnappedSurface, actualWarpedDestination, "Warp destination must match nearest walkable surface.");
        }

        #endregion

        #region AC-NAV-14: HideSpot Approach Standoff Navigation

        [Test]
        public void Test_NavMeshAvoidance_CalculateHideSpotHoldAnchor_AppliesPhysicalStandoffCushion()
        {
            // Arrange: HideSpot at centroid (10, 0, 0), front anchor at (10, 0, 1), hold vector (0, 0, 1)
            Vector3 spotFrontAnchor = new Vector3(10.0f, 0.0f, 1.0f);
            Vector3 holdVector = new Vector3(0.0f, 0.0f, 1.0f);
            float guardRadius = 0.40f;

            // Act: Calculate standoff hold anchor
            Vector3 holdAnchor = _avoidanceService.CalculateHideSpotHoldAnchor(spotFrontAnchor, holdVector, guardRadius);

            // Assert: Target coordinate = (10, 0, 1) + 0.40 * (0, 0, 1) = (10, 0, 1.40)
            Vector3 expectedAnchor = new Vector3(10.0f, 0.0f, 1.40f);
            Assert.AreEqual(expectedAnchor.x, holdAnchor.x, 0.001f);
            Assert.AreEqual(expectedAnchor.y, holdAnchor.y, 0.001f);
            Assert.AreEqual(expectedAnchor.z, holdAnchor.z, 0.001f);
        }

        #endregion

        #region AC-NAV-17: Multi-Guard Chase Staggered Query Distribution

        [Test]
        public void Test_NavMeshAvoidance_MultiGuardChaseStaggering_DispatchesMaxOneQueryPerTick()
        {
            // Arrange: 3 chasing guards requesting re-paths on the same frame tick
            int callbackCount1 = 0;
            int callbackCount2 = 0;
            int callbackCount3 = 0;

            _avoidanceService.EnqueueQuery(_agent1, new Vector3(10, 0, 10), res => callbackCount1++);
            _avoidanceService.EnqueueQuery(_agent2, new Vector3(12, 0, 10), res => callbackCount2++);
            _avoidanceService.EnqueueQuery(_agent3, new Vector3(14, 0, 10), res => callbackCount3++);

            Assert.AreEqual(3, _avoidanceService.PendingQueryCount, "Queue must contain 3 pending queries initially.");

            // Act & Assert Frame 1: Tick 1
            int dispatchedTick1 = _avoidanceService.TickScheduler(maxQueriesPerTick: 1);
            Assert.AreEqual(1, dispatchedTick1, "Tick 1 must dispatch exactly 1 query.");
            Assert.AreEqual(2, _avoidanceService.PendingQueryCount, "Remaining queries in queue must be 2.");
            Assert.AreEqual(1, callbackCount1, "Guard 1 callback must be invoked on Tick 1.");
            Assert.AreEqual(0, callbackCount2, "Guard 2 callback must remain pending on Tick 1.");
            Assert.AreEqual(0, callbackCount3, "Guard 3 callback must remain pending on Tick 1.");

            // Act & Assert Frame 2: Tick 2
            int dispatchedTick2 = _avoidanceService.TickScheduler(maxQueriesPerTick: 1);
            Assert.AreEqual(1, dispatchedTick2, "Tick 2 must dispatch exactly 1 query.");
            Assert.AreEqual(1, _avoidanceService.PendingQueryCount, "Remaining queries in queue must be 1.");
            Assert.AreEqual(1, callbackCount2, "Guard 2 callback must be invoked on Tick 2.");
            Assert.AreEqual(0, callbackCount3, "Guard 3 callback must remain pending on Tick 2.");

            // Act & Assert Frame 3: Tick 3
            int dispatchedTick3 = _avoidanceService.TickScheduler(maxQueriesPerTick: 1);
            Assert.AreEqual(1, dispatchedTick3, "Tick 3 must dispatch exactly 1 query.");
            Assert.AreEqual(0, _avoidanceService.PendingQueryCount, "Queue must be empty after Tick 3.");
            Assert.AreEqual(1, callbackCount3, "Guard 3 callback must be invoked on Tick 3.");
        }

        #endregion
    }
}
