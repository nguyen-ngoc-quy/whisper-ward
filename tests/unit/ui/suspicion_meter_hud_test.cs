using NUnit.Framework;
using UnityEngine;
using WhisperWard.UI.HUD;

namespace WhisperWard.Tests.Unit.UI
{
    [TestFixture]
    public class SuspicionMeterHUDTest
    {
        private GameObject _hudObject;
        private SuspicionMeterHUDController _controller;

        [SetUp]
        public void SetUp()
        {
            _hudObject = new GameObject("SuspicionMeterHUD_Test");
            _controller = _hudObject.AddComponent<SuspicionMeterHUDController>();
            _controller.Configure(ellipseRadiusX: 240.0f, ellipseRadiusY: 160.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hudObject != null)
            {
                Object.DestroyImmediate(_hudObject);
            }
        }

        [Test]
        public void test_dominant_threat_selection_picks_highest_ratio()
        {
            // Arrange (AC-UI-01)
            // Guard 1: A = 0.20, T_entry = 0.30 -> ratio = 0.20 / 0.30 = 0.667
            // Guard 2: A = 0.25, T_entry = 0.25 (R=0.25) -> ratio = 0.25 / 0.25 = 1.000
            // Guard 3: A = 0.15, T_entry = 0.30 -> ratio = 0.15 / 0.30 = 0.500
            var candidates = new GuardThreatCandidate[]
            {
                GuardThreatCandidate.Create(1, new Vector3(0, 0, 10), accumulator: 0.20f, residualWariness: 0f),
                GuardThreatCandidate.Create(2, new Vector3(5, 0, 10), accumulator: 0.25f, residualWariness: 0.25f),
                GuardThreatCandidate.Create(3, new Vector3(-5, 0, 10), accumulator: 0.15f, residualWariness: 0f)
            };

            // Act
            bool hasThreat = SuspicionMeterHUDController.EvaluateDominantThreat(
                candidates,
                candidates.Length,
                playerPosition: Vector3.zero,
                out GuardThreatCandidate dominant);

            // Assert
            Assert.IsTrue(hasThreat);
            Assert.AreEqual(2, dominant.GuardId, "Guard 2 has the highest threat ratio (1.0 vs 0.667) and must be selected.");
            Assert.AreEqual(1.0f, dominant.ThreatRatio, 0.01f);
        }

        [Test]
        public void test_dominant_threat_tie_breaker_accumulator_priority()
        {
            // Arrange: Guard 1 and 2 have identical threat ratios (0.50), but Guard 2 has higher absolute accumulator A
            // Guard 1: A = 0.10, T_entry = 0.20 -> ratio = 0.50
            // Guard 2: A = 0.15, T_entry = 0.30 -> ratio = 0.50
            var candidates = new GuardThreatCandidate[]
            {
                GuardThreatCandidate.Create(1, new Vector3(0, 0, 10), accumulator: 0.10f, baseThreshold: 0.20f),
                GuardThreatCandidate.Create(2, new Vector3(0, 0, 10), accumulator: 0.15f, baseThreshold: 0.30f)
            };

            // Act
            bool hasThreat = SuspicionMeterHUDController.EvaluateDominantThreat(
                candidates,
                candidates.Length,
                playerPosition: Vector3.zero,
                out GuardThreatCandidate dominant);

            // Assert
            Assert.IsTrue(hasThreat);
            Assert.AreEqual(2, dominant.GuardId, "When threat ratios are equal, higher absolute accumulator wins.");
            Assert.AreEqual(0.15f, dominant.Accumulator, 0.01f);
        }

        [Test]
        public void test_dominant_threat_tie_breaker_distance_priority()
        {
            // Arrange: Same threat ratio and same accumulator, but Guard 1 is closer (5m vs 12m)
            var candidates = new GuardThreatCandidate[]
            {
                GuardThreatCandidate.Create(1, new Vector3(0, 0, 5), accumulator: 0.20f),
                GuardThreatCandidate.Create(2, new Vector3(0, 0, 12), accumulator: 0.20f)
            };

            // Act
            bool hasThreat = SuspicionMeterHUDController.EvaluateDominantThreat(
                candidates,
                candidates.Length,
                playerPosition: Vector3.zero,
                out GuardThreatCandidate dominant);

            // Assert
            Assert.IsTrue(hasThreat);
            Assert.AreEqual(1, dominant.GuardId, "When ratio and accumulator tie, closer guard to player wins.");
        }

        [Test]
        public void test_dominant_threat_tie_breaker_id_priority()
        {
            // Arrange: Exact same ratio, accumulator, and distance
            var candidates = new GuardThreatCandidate[]
            {
                GuardThreatCandidate.Create(20, new Vector3(0, 0, 10), accumulator: 0.20f),
                GuardThreatCandidate.Create(5, new Vector3(0, 0, 10), accumulator: 0.20f)
            };

            // Act
            bool hasThreat = SuspicionMeterHUDController.EvaluateDominantThreat(
                candidates,
                candidates.Length,
                playerPosition: Vector3.zero,
                out GuardThreatCandidate dominant);

            // Assert
            Assert.IsTrue(hasThreat);
            Assert.AreEqual(5, dominant.GuardId, "When all parameters tie, lower ID wins deterministically.");
        }

        [Test]
        public void test_quiet_region_hides_hud_when_all_suspicion_zero()
        {
            // Arrange (AC-UI-02: Zero-Threat Invariant)
            var candidates = new GuardThreatCandidate[]
            {
                GuardThreatCandidate.Create(1, new Vector3(5, 0, 10), accumulator: 0.0f, residualWariness: 0.5f),
                GuardThreatCandidate.Create(2, new Vector3(-5, 0, 10), accumulator: 0.0f, residualWariness: 0.0f)
            };

            // Act
            _controller.UpdateHUD(
                candidates,
                candidates.Length,
                playerPosition: Vector3.zero,
                cameraPosition: Vector3.zero,
                cameraRotation: Quaternion.identity);

            // Assert
            Assert.IsFalse(_controller.CurrentState.IsVisible, "HUD must be completely suppressed when all guards have A == 0.");
            Assert.AreEqual(0f, _controller.CurrentState.FillPercent);
            Assert.AreEqual(Vector2.zero, _controller.CurrentState.ChevronScreenOffset);
        }

        [Test]
        public void test_hud_becomes_visible_when_suspicion_positive()
        {
            // Arrange
            var candidates = new GuardThreatCandidate[]
            {
                GuardThreatCandidate.Create(1, new Vector3(0, 0, 10), accumulator: 0.15f, residualWariness: 0f)
            };

            // Act
            _controller.UpdateHUD(
                candidates,
                candidates.Length,
                playerPosition: Vector3.zero,
                cameraPosition: Vector3.zero,
                cameraRotation: Quaternion.identity);

            // Assert
            Assert.IsTrue(_controller.CurrentState.IsVisible, "HUD must become visible when any guard has positive suspicion.");
            Assert.AreEqual(1, _controller.CurrentState.DominantGuardId);
            Assert.AreEqual(15.0f, _controller.CurrentState.FillPercent, 0.01f);
        }

        [Test]
        public void test_planar_azimuth_chevron_handles_front_and_behind_camera()
        {
            // Arrange (AC-UI-03: 360 Azimuth No Singularity)
            Vector3 camPos = Vector3.zero;
            Quaternion camRot = Quaternion.identity; // Facing forward +Z

            // Case 1: Guard directly in front at (0, 0, 10)
            SuspicionMeterHUDController.CalculateChevronProjection(
                new Vector3(0, 0, 10),
                camPos,
                camRot,
                radiusX: 240f,
                radiusY: 160f,
                out float azFront,
                out Vector2 offsetFront);

            Assert.AreEqual(0.0f, azFront, 0.01f, "Front guard azimuth must be 0 degrees.");
            Assert.AreEqual(0.0f, offsetFront.x, 0.01f);
            Assert.AreEqual(160.0f, offsetFront.y, 0.01f, "Front guard maps to top of screen ellipse (0, Ry).");

            // Case 2: Guard directly to the right at (10, 0, 0)
            SuspicionMeterHUDController.CalculateChevronProjection(
                new Vector3(10, 0, 0),
                camPos,
                camRot,
                radiusX: 240f,
                radiusY: 160f,
                out float azRight,
                out Vector2 offsetRight);

            Assert.AreEqual(90.0f, azRight, 0.01f, "Right guard azimuth must be +90 degrees.");
            Assert.AreEqual(240.0f, offsetRight.x, 0.01f, "Right guard maps to right edge (Rx, 0).");
            Assert.AreEqual(0.0f, offsetRight.y, 0.01f);

            // Case 3: Guard directly behind camera at (0, 0, -10) (Negative Z, no singularity!)
            SuspicionMeterHUDController.CalculateChevronProjection(
                new Vector3(0, 0, -10),
                camPos,
                camRot,
                radiusX: 240f,
                radiusY: 160f,
                out float azBehind,
                out Vector2 offsetBehind);

            Assert.AreEqual(180.0f, Mathf.Abs(azBehind), 0.01f, "Behind guard azimuth must be +/-180 degrees.");
            Assert.AreEqual(0.0f, offsetBehind.x, 0.01f);
            Assert.AreEqual(-160.0f, offsetBehind.y, 0.01f, "Behind guard maps cleanly to bottom of screen (0, -Ry).");

            // Case 4: Guard to the left at (-10, 0, 0)
            SuspicionMeterHUDController.CalculateChevronProjection(
                new Vector3(-10, 0, 0),
                camPos,
                camRot,
                radiusX: 240f,
                radiusY: 160f,
                out float azLeft,
                out Vector2 offsetLeft);

            Assert.AreEqual(-90.0f, azLeft, 0.01f, "Left guard azimuth must be -90 degrees.");
            Assert.AreEqual(-240.0f, offsetLeft.x, 0.01f, "Left guard maps to left edge (-Rx, 0).");
            Assert.AreEqual(0.0f, offsetLeft.y, 0.01f);
        }

        [Test]
        public void test_dynamic_threshold_notch_sinks_with_residual_wariness()
        {
            // Arrange (AC-UI-04)
            // T_base = 0.30, k_res = 0.20, T_floor = 0.10
            // Case 1: R = 0.0 -> T_entry = 0.30 (Notch at 30%)
            var candR0 = new GuardThreatCandidate[]
            {
                GuardThreatCandidate.Create(1, new Vector3(0, 0, 10), accumulator: 0.10f, residualWariness: 0.0f)
            };

            _controller.UpdateHUD(candR0, 1, Vector3.zero, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(30.0f, _controller.CurrentState.ThresholdNotchPercent, 0.01f);

            // Case 2: R = 0.5 -> T_entry = 0.30 - 0.20 * 0.5 = 0.20 (Notch sinks to 20%)
            var candR05 = new GuardThreatCandidate[]
            {
                GuardThreatCandidate.Create(1, new Vector3(0, 0, 10), accumulator: 0.10f, residualWariness: 0.5f)
            };

            _controller.UpdateHUD(candR05, 1, Vector3.zero, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(20.0f, _controller.CurrentState.ThresholdNotchPercent, 0.01f);

            // Case 3: R = 1.0 -> T_entry = max(0.30 - 0.20, 0.10) = 0.10 (Floor clamp at 10%)
            var candR10 = new GuardThreatCandidate[]
            {
                GuardThreatCandidate.Create(1, new Vector3(0, 0, 10), accumulator: 0.10f, residualWariness: 1.0f)
            };

            _controller.UpdateHUD(candR10, 1, Vector3.zero, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(10.0f, _controller.CurrentState.ThresholdNotchPercent, 0.01f);
        }

        [Test]
        public void test_chase_mode_locks_hud_gauge()
        {
            // Arrange (AC-UI-04: Chase lock)
            var candidates = new GuardThreatCandidate[]
            {
                GuardThreatCandidate.Create(1, new Vector3(0, 0, 5), accumulator: 1.0f, isInChase: true)
            };

            // Act
            _controller.UpdateHUD(candidates, 1, Vector3.zero, Vector3.zero, Quaternion.identity);

            // Assert
            Assert.IsTrue(_controller.CurrentState.IsVisible);
            Assert.IsTrue(_controller.CurrentState.IsChaseLocked, "HUD state must record IsChaseLocked == true during Chase.");
            Assert.AreEqual(100.0f, _controller.CurrentState.FillPercent, 0.01f);
        }
    }
}
