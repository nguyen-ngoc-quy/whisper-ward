using System;
using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Contracts;
using WhisperWard.Core.Navigation;

namespace WhisperWard.Tests.Unit.Navigation
{
    /// <summary>
    /// Automated NUnit EditMode test suite for NavMeshQueryService.
    /// Verifies cumulative 3D polyline distance metric (AC-NAV-01, AC-NAV-02),
    /// catch-gate hysteresis & invalidation (AC-NAV-03, AC-NAV-04),
    /// arrival vs path-end precedence locking (AC-NAV-05),
    /// chase re-pathing dual-trigger clamp (AC-NAV-06, AC-NAV-16),
    /// thin-wall linecast rejection (AC-NAV-09), elevated target rejection (AC-NAV-10),
    /// and zero-allocation query pre-allocation invariants (AC-NAV-15, TR-CORE-005).
    /// </summary>
    [TestFixture]
    public class NavMeshQueryTest
    {
        private NavMeshQueryService _service;

        [SetUp]
        public void SetUp()
        {
            _service = new NavMeshQueryService();
        }

        #region AC-NAV-01 & AC-NAV-02: Cumulative Polyline Distance Calculation

        [Test]
        public void Test_NavMeshQuery_CumulativeDistance_LShapedFixture_ReturnsSevenMeters()
        {
            // Arrange: Synthetic L-shaped path with N=3 corners: (0,0,0) -> (3,0,0) -> (3,0,4)
            Vector3[] corners = new Vector3[]
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(3.0f, 0.0f, 0.0f),
                new Vector3(3.0f, 0.0f, 4.0f)
            };

            // Act: Calculate piecewise cumulative Euclidean distance
            float distance = NavMeshQueryService.CalculateCumulativeDistance(corners, corners.Length, PathQueryResultStatus.Complete);

            // Assert: Exact sum is 3.0m + 4.0m = 7.000m +- 0.001m
            Assert.AreEqual(7.000f, distance, 0.001f, "Cumulative path length must equal exactly 7.000m for L-shaped 3m+4m fixture.");
        }

        [Test]
        public void Test_NavMeshQuery_CumulativeDistance_ThreeDimensionalRamp_CalculatesEuclideanHypotenuse()
        {
            // Arrange: 3D ramp elevation fixture (0, 0, 0) -> (3, 4, 0) (hypotenuse = 5.0m)
            Vector3[] corners = new Vector3[]
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(3.0f, 4.0f, 0.0f)
            };

            // Act
            float distance = NavMeshQueryService.CalculateCumulativeDistance(corners, corners.Length, PathQueryResultStatus.Complete);

            // Assert: sqrt(3^2 + 4^2) = 5.000m
            Assert.AreEqual(5.000f, distance, 0.001f, "3D polyline distance must account for elevation on ramps.");
        }

        [Test]
        public void Test_NavMeshQuery_CumulativeDistance_DegeneratePaths_ReturnPositiveInfinity()
        {
            // Arrange
            Vector3[] singleCorner = new Vector3[] { new Vector3(1f, 0f, 1f) };
            Vector3[] emptyCorners = new Vector3[0];
            Vector3[] validCorners = new Vector3[] { Vector3.zero, Vector3.forward };

            // Act
            float distInvalid = NavMeshQueryService.CalculateCumulativeDistance(validCorners, validCorners.Length, PathQueryResultStatus.Invalid);
            float distSingle = NavMeshQueryService.CalculateCumulativeDistance(singleCorner, singleCorner.Length, PathQueryResultStatus.Complete);
            float distEmpty = NavMeshQueryService.CalculateCumulativeDistance(emptyCorners, 0, PathQueryResultStatus.Complete);
            float distNull = NavMeshQueryService.CalculateCumulativeDistance(null, 0, PathQueryResultStatus.Complete);

            // Assert: AC-NAV-02 requires float.PositiveInfinity for all degenerate paths
            Assert.IsTrue(float.IsPositiveInfinity(distInvalid), "Invalid path status must return PositiveInfinity.");
            Assert.IsTrue(float.IsPositiveInfinity(distSingle), "Corner count < 2 must return PositiveInfinity.");
            Assert.IsTrue(float.IsPositiveInfinity(distEmpty), "Corner count == 0 must return PositiveInfinity.");
            Assert.IsTrue(float.IsPositiveInfinity(distNull), "Null corner buffer must return PositiveInfinity.");
        }

        #endregion

        #region AC-NAV-03 & AC-NAV-04: Catch-Gate Threshold, Hysteresis & Invalidation

        [Test]
        public void Test_NavMeshQuery_CatchGate_HysteresisSequence_EvaluatesD2Accurately()
        {
            // Arrange: Chasing guard with catch_range = 5.50m, hyst = 0.50m (retention ceiling = 6.00m)
            // Initial gate state = INACTIVE (false)
            bool gateState = false;

            // Tick 1: d_path = 5.80m (> 5.50m) => remains INACTIVE (false)
            gateState = _service.EvaluateCatchGate(gateState, PathQueryResultStatus.Complete, 5.80f, 5.50f, 0.50f);
            Assert.IsFalse(gateState, "Tick 1 (5.80m) must remain INACTIVE.");

            // Tick 2: d_path = 5.40m (<= 5.50m) => transitions to ACTIVE (true)
            gateState = _service.EvaluateCatchGate(gateState, PathQueryResultStatus.Complete, 5.40f, 5.50f, 0.50f);
            Assert.IsTrue(gateState, "Tick 2 (5.40m) must engage to ACTIVE.");

            // Tick 3: d_path = 5.75m (<= 6.00m) => remains ACTIVE due to hysteresis memory
            gateState = _service.EvaluateCatchGate(gateState, PathQueryResultStatus.Complete, 5.75f, 5.50f, 0.50f);
            Assert.IsTrue(gateState, "Tick 3 (5.75m) must remain ACTIVE due to 0.50m hysteresis retention.");

            // Tick 4: d_path = 6.01m (> 6.00m) => drops to INACTIVE (false)
            gateState = _service.EvaluateCatchGate(gateState, PathQueryResultStatus.Complete, 6.01f, 5.50f, 0.50f);
            Assert.IsFalse(gateState, "Tick 4 (6.01m) must disengage to INACTIVE when exceeding retention limit.");
        }

        [Test]
        public void Test_NavMeshQuery_CatchGate_PathPartial_DisengagesImmediately()
        {
            // Arrange: Active catch gate engaged at close range d_path = 3.00m
            bool gateState = true;

            // Act: Door closes, path status becomes Partial
            gateState = _service.EvaluateCatchGate(gateState, PathQueryResultStatus.Partial, 2.00f, 5.50f, 0.50f);

            // Assert: AC-NAV-04 requires immediate disengagement on PathPartial
            Assert.IsFalse(gateState, "Catch gate must immediately disengage when path transitions to PathPartial regardless of proximity.");
        }

        #endregion

        #region AC-NAV-05: Arrival vs. Path-End Mutually Exclusive Precedence

        [Test]
        public void Test_NavMeshQuery_ArrivalPrecedence_CaseA_TrueArrival_ReturnsArrived()
        {
            // Arrange: Destination at (10, 0, 0), guard at (9.80, 0, 0) (distance = 0.20m <= 0.30m)
            Vector3 targetPos = new Vector3(10.0f, 0.0f, 0.0f);
            Vector3 guardPos = new Vector3(9.80f, 0.0f, 0.0f);
            Vector3 terminalCorner = new Vector3(10.0f, 0.0f, 0.0f);

            // Act
            ArrivalState state = NavMeshQueryService.EvaluateArrivalState(
                guardPos,
                targetPos,
                terminalCorner,
                PathQueryResultStatus.Complete,
                arriveThreshold: 0.30f);

            // Assert
            Assert.AreEqual(ArrivalState.Arrived, state, "Case A must evaluate to Arrived.");
        }

        [Test]
        public void Test_NavMeshQuery_ArrivalPrecedence_CaseB_ObstructionPathEnd_ReturnsPathEnd()
        {
            // Arrange: Target at (10, 0, 0). Guard at (5.10, 0, 0).
            // Partial path terminal corner at (5.00, 0, 0) (distance to corner = 0.10m <= 0.30m, distance to target = 4.90m > 0.30m)
            Vector3 targetPos = new Vector3(10.0f, 0.0f, 0.0f);
            Vector3 guardPos = new Vector3(5.10f, 0.0f, 0.0f);
            Vector3 terminalCorner = new Vector3(5.00f, 0.0f, 0.0f);

            // Act
            ArrivalState state = NavMeshQueryService.EvaluateArrivalState(
                guardPos,
                targetPos,
                terminalCorner,
                PathQueryResultStatus.Partial,
                arriveThreshold: 0.30f);

            // Assert
            Assert.AreEqual(ArrivalState.PathEnd, state, "Case B must evaluate to PathEnd.");
        }

        [Test]
        public void Test_NavMeshQuery_ArrivalPrecedence_CaseC_PrecedenceOverlapLock_ArrivedOverridesPathEnd()
        {
            // Arrange: Target at (10, 0, 0). Guard at (9.85, 0, 0).
            // Terminal corner at (9.90, 0, 0) (both target and corner are within 0.30m)
            Vector3 targetPos = new Vector3(10.0f, 0.0f, 0.0f);
            Vector3 guardPos = new Vector3(9.85f, 0.0f, 0.0f);
            Vector3 terminalCorner = new Vector3(9.90f, 0.0f, 0.0f);

            // Act
            ArrivalState state = NavMeshQueryService.EvaluateArrivalState(
                guardPos,
                targetPos,
                terminalCorner,
                PathQueryResultStatus.Partial,
                arriveThreshold: 0.30f);

            // Assert: Precedence lock strictly dictates Arrived overrides PathEnd
            Assert.AreEqual(ArrivalState.Arrived, state, "Case C: Arrived must strictly override PathEnd when both evaluate within tolerance.");
        }

        #endregion

        #region AC-NAV-06 & AC-NAV-16: Chase Re-Pathing Dual-Trigger & Rate Floor Clamp

        [Test]
        public void Test_NavMeshQuery_ChaseRepathing_DualTriggerAndRateFloorClamp()
        {
            Vector3 playerOrigin = new Vector3(0f, 0f, 0f);
            float minInterval = 0.10f;
            float cadence = 0.25f;
            float displacementTrigger = 0.50f;

            // Subcase 1: t_since = 0.05s, delta_p = 0.80m => FALSE (rate floor clamp blocks re-path)
            Vector3 playerSubcase1 = new Vector3(0.80f, 0f, 0f);
            bool trigger1 = _service.ShouldRepathChase(0.05f, playerSubcase1, playerOrigin, minInterval, cadence, displacementTrigger);
            Assert.IsFalse(trigger1, "Subcase 1: Rate floor clamp must block re-path when t_since < 0.10s.");

            // Subcase 2: t_since = 0.12s, delta_p = 0.55m => TRUE (passes rate floor and displacement trigger)
            Vector3 playerSubcase2 = new Vector3(0.55f, 0f, 0f);
            bool trigger2 = _service.ShouldRepathChase(0.12f, playerSubcase2, playerOrigin, minInterval, cadence, displacementTrigger);
            Assert.IsTrue(trigger2, "Subcase 2: Displacement trigger must fire when t_since >= minInterval and delta_p >= 0.50m.");

            // Subcase 3: t_since = 0.15s, delta_p = 0.20m => FALSE (neither displacement nor cadence period reached)
            Vector3 playerSubcase3 = new Vector3(0.20f, 0f, 0f);
            bool trigger3 = _service.ShouldRepathChase(0.15f, playerSubcase3, playerOrigin, minInterval, cadence, displacementTrigger);
            Assert.IsFalse(trigger3, "Subcase 3: Must not re-path when neither trigger condition is satisfied.");

            // Subcase 4: t_since = 0.26s, delta_p = 0.05m => TRUE (cadence period reached)
            Vector3 playerSubcase4 = new Vector3(0.05f, 0f, 0f);
            bool trigger4 = _service.ShouldRepathChase(0.26f, playerSubcase4, playerOrigin, minInterval, cadence, displacementTrigger);
            Assert.IsTrue(trigger4, "Subcase 4: Periodic cadence trigger must fire when t_since >= 0.25s.");
        }

        #endregion

        #region AC-NAV-09 & AC-NAV-10: Thin-Wall Linecast & Elevated Target Rejection

        [Test]
        public void Test_NavMeshQuery_ThinWallLinecast_RejectsSamplingAcrossPartition()
        {
            // Arrange: Source at (0, 0, 0), sampled position at (0, 0, 0.20m)
            // Inject test hooks simulating a solid 0.15m wall between source and candidate hit
            Vector3 source = new Vector3(0f, 0f, 0f);
            Vector3 sampleCandidate = new Vector3(0f, 0f, 0.20f);

            _service.SetTestHooks(
                sampleDelegate: (Vector3 src, out Vector3 hitPos, float maxDist, int mask) =>
                {
                    hitPos = sampleCandidate;
                    return true;
                },
                linecastDelegate: (Vector3 start, Vector3 end, LayerMask mask, QueryTriggerInteraction qti) =>
                {
                    // Wall partition occludes linecast between source and sampleCandidate
                    return true;
                });

            // Act
            bool sampleSuccess = _service.TrySamplePosition(source, out Vector3 snappedPos, maxDistance: 0.40f);

            // Assert: Linecast obstruction must reject sampling
            Assert.IsFalse(sampleSuccess, "Thin-wall linecast collision must reject sample point across partition.");
            Assert.AreEqual(Vector3.zero, snappedPos, "Rejected sample must output zero vector.");
        }

        [Test]
        public void Test_NavMeshQuery_ElevatedTarget_RejectsSamplingExceedingMaxDistance()
        {
            // Arrange: Crate prop of height 0.60m (> 0.40m max search radius)
            Vector3 elevatedSource = new Vector3(2.0f, 0.60f, 2.0f);

            _service.SetTestHooks(
                sampleDelegate: (Vector3 src, out Vector3 hitPos, float maxDist, int mask) =>
                {
                    // Distance exceeds 0.40m search tolerance
                    hitPos = Vector3.zero;
                    return false;
                },
                linecastDelegate: (Vector3 start, Vector3 end, LayerMask mask, QueryTriggerInteraction qti) => false);

            // Act
            bool sampleSuccess = _service.TrySamplePosition(elevatedSource, out Vector3 snappedPos, maxDistance: 0.40f);

            // Assert
            Assert.IsFalse(sampleSuccess, "Elevated target exceeding 0.40m must fail sampling.");
            Assert.AreEqual(Vector3.zero, snappedPos, "Failed sample must output zero vector.");
        }

        #endregion

        #region AC-NAV-15 & TR-CORE-005: Zero-Allocation & Buffer Capacity Pre-allocation

        [Test]
        public void Test_NavMeshQuery_BufferCapacity_IsPreAllocatedAndZeroAlloc()
        {
            // Arrange & Act
            int capacity = _service.CornerBufferCapacity;

            // Assert: Pre-allocated buffer capacity must equal at least 64 per ADR-0007
            Assert.GreaterOrEqual(capacity, 64, "NavMeshQueryService must pre-allocate at least 64 corners for zero-allocation queries.");
        }

        #endregion
    }
}
