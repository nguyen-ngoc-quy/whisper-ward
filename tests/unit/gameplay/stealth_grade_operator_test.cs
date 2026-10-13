using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Grade;

namespace WhisperWard.Tests.Unit.Gameplay
{
    [TestFixture]
    public class StealthGradeOperatorTest
    {
        private GameObject _operatorHost;
        private StealthGradeOperator _operator;

        [SetUp]
        public void SetUp()
        {
            _operatorHost = new GameObject("StealthGradeOperator_TestHost");
            _operator = _operatorHost.AddComponent<StealthGradeOperator>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_operatorHost != null)
            {
                Object.DestroyImmediate(_operatorHost);
            }
        }

        [Test]
        public void test_grade_operator_clean_run_awards_rank_s()
        {
            // Arrange: Perfect infiltration with zero chase and zero residual wariness
            int chaseCount = 0;
            float avgResidual = 0.0f;
            int burstCount = 0;

            // Act
            StealthGradeResult result = StealthGradeOperator.CalculateGrade(
                chaseCount,
                avgResidual,
                MissionCompletionStatus.Completed,
                burstCount);

            // Assert
            Assert.AreEqual(100.0f, result.BaseScore, 0.001f);
            Assert.AreEqual(0.0f, result.IncidentPenalty, 0.001f);
            Assert.AreEqual(0.0f, result.ResidualPenalty, 0.001f);
            Assert.AreEqual(0.0f, result.TotalPenalty, 0.001f);
            Assert.AreEqual(100.0f, result.FinalScore, 0.001f);
            Assert.AreEqual(GradeRank.S, result.Rank);
            Assert.IsTrue(result.IsZeroChaseMastery);
            Assert.AreEqual(MissionCompletionStatus.Completed, result.Status);
        }

        [Test]
        public void test_grade_operator_single_chase_deducts_25_pts_and_disqualifies_rank_s()
        {
            // Arrange: 1 Chase escalation detected in room
            int chaseCount = 1;
            float avgResidual = 0.0f;

            // Act
            StealthGradeResult result = StealthGradeOperator.CalculateGrade(
                chaseCount,
                avgResidual,
                MissionCompletionStatus.Completed);

            // Assert
            Assert.AreEqual(25.0f, result.IncidentPenalty, 0.001f);
            Assert.AreEqual(0.0f, result.ResidualPenalty, 0.001f);
            Assert.AreEqual(25.0f, result.TotalPenalty, 0.001f);
            Assert.AreEqual(75.0f, result.FinalScore, 0.001f);
            Assert.AreEqual(GradeRank.A, result.Rank, "Score of 75.0 must award Rank A");
            Assert.IsFalse(result.IsZeroChaseMastery, "Any Chase must revoke ZeroChaseMastery");
        }

        [Test]
        public void test_grade_operator_high_score_with_chase_strictly_prohibits_rank_s()
        {
            // Arrange: Score is above S threshold (>= 90), but chaseCount >= 1
            // e.g., low chase weight test case (chaseWeight = 5.0f -> score = 95.0f)
            int chaseCount = 1;
            float avgResidual = 0.0f;

            // Act
            StealthGradeResult result = StealthGradeOperator.CalculateGrade(
                chaseCount: chaseCount,
                avgResidual: avgResidual,
                status: MissionCompletionStatus.Completed,
                burstCount: 0,
                baseScore: 100.0f,
                chaseWeight: 5.0f,
                burstWeight: 0.0f,
                maxResidualPenalty: 10.0f,
                rMax: 1.0f,
                penaltyCap: 90.0f,
                completionFloor: 10.0f,
                thresholdS: 90.0f,
                thresholdA: 75.0f);

            // Assert: Despite FinalScore being 95.0f >= 90.0f, Rank S is strictly forbidden!
            Assert.AreEqual(95.0f, result.FinalScore, 0.001f);
            Assert.IsFalse(result.IsZeroChaseMastery);
            Assert.AreEqual(GradeRank.A, result.Rank, "Mastery Invariant: N_chase >= 1 strictly bars Rank S regardless of score!");
        }

        [Test]
        public void test_grade_operator_clean_burst_distraction_incurs_zero_score_deduction()
        {
            // Arrange: 5 burst noise distractions deployed, 0 chase incidents
            int burstCount = 5;
            int chaseCount = 0;

            // Act
            StealthGradeResult result = StealthGradeOperator.CalculateGrade(
                chaseCount,
                avgResidual: 0.0f,
                MissionCompletionStatus.Completed,
                burstCount);

            // Assert: D4 invariant Clean Distraction Exemption (W_fruitless = 0.0)
            Assert.AreEqual(0.0f, result.IncidentPenalty, 0.001f);
            Assert.AreEqual(0.0f, result.TotalPenalty, 0.001f);
            Assert.AreEqual(100.0f, result.FinalScore, 0.001f);
            Assert.AreEqual(GradeRank.S, result.Rank);
            Assert.AreEqual(5, result.BurstDistractionCount);
        }

        [Test]
        public void test_grade_operator_global_penalty_cap_enforces_10pt_completion_floor()
        {
            // Arrange: Extreme penalties (5 chases = 125 pts, residual = 10 pts -> raw total = 135 pts)
            int chaseCount = 5;
            float avgResidual = 1.0f; // max residual

            // Act
            StealthGradeResult result = StealthGradeOperator.CalculateGrade(
                chaseCount,
                avgResidual,
                MissionCompletionStatus.Completed);

            // Assert: Penalty is capped at 90.0 pts; Completed score is floored at 10.0 pts
            Assert.AreEqual(90.0f, result.IncidentPenalty, 0.001f, "Incident penalty capped at 90.0");
            Assert.AreEqual(90.0f, result.TotalPenalty, 0.001f, "Total penalty capped at 90.0");
            Assert.AreEqual(10.0f, result.FinalScore, 0.001f, "Completed room score must enforce 10.0 pt floor");
            Assert.AreEqual(GradeRank.C, result.Rank);
        }

        [Test]
        public void test_grade_operator_capture_terminates_with_score_zero_and_rank_f()
        {
            // Arrange: Player captured by guard
            _operator.RecordChaseIncident(1);
            _operator.RecordPlayerCaptured();

            // Act
            StealthGradeResult result = _operator.EvaluateFinalGrade(MissionCompletionStatus.Completed);

            // Assert: Capture forces Status = Failed, Score = 0.0, Rank = F
            Assert.IsTrue(_operator.IsCaptured);
            Assert.AreEqual(MissionCompletionStatus.Failed, result.Status);
            Assert.AreEqual(0.0f, result.FinalScore, 0.001f);
            Assert.AreEqual(GradeRank.F, result.Rank);
        }

        [Test]
        public void test_grade_operator_residual_wariness_penalty_applied_correctly()
        {
            // Arrange: 0 Chase, avg residual = 0.50 with rMax = 1.0 (50% residual -> 5.0 pts penalty)
            int chaseCount = 0;
            float avgResidual = 0.50f;

            // Act
            StealthGradeResult result = StealthGradeOperator.CalculateGrade(
                chaseCount,
                avgResidual,
                MissionCompletionStatus.Completed);

            // Assert
            Assert.AreEqual(0.0f, result.IncidentPenalty, 0.001f);
            Assert.AreEqual(5.0f, result.ResidualPenalty, 0.001f);
            Assert.AreEqual(5.0f, result.TotalPenalty, 0.001f);
            Assert.AreEqual(95.0f, result.FinalScore, 0.001f);
            Assert.AreEqual(GradeRank.S, result.Rank, "Score of 95.0 with zero chase qualifies for Rank S");
            Assert.IsTrue(result.IsZeroChaseMastery);
        }

        [Test]
        public void test_grade_operator_grade_rank_boundaries_a_b_c()
        {
            // Test Rank A lower boundary: exactly 75.0f
            StealthGradeResult resultA = StealthGradeOperator.CalculateGrade(
                chaseCount: 1, // -25 pts -> 75.0
                avgResidual: 0.0f,
                MissionCompletionStatus.Completed);
            Assert.AreEqual(75.0f, resultA.FinalScore, 0.001f);
            Assert.AreEqual(GradeRank.A, resultA.Rank);

            // Test Rank B upper boundary: 74.9f
            StealthGradeResult resultBHigh = StealthGradeOperator.CalculateGrade(
                chaseCount: 1, // -25 pts
                avgResidual: 0.01f, // -0.1 pts -> 74.9
                MissionCompletionStatus.Completed);
            Assert.AreEqual(74.9f, resultBHigh.FinalScore, 0.01f);
            Assert.AreEqual(GradeRank.B, resultBHigh.Rank);

            // Test Rank B lower boundary: exactly 60.0f
            StealthGradeResult resultBLow = StealthGradeOperator.CalculateGrade(
                chaseCount: 1, // -25 pts
                avgResidual: 1.50f, // residual capped at rMax (1.0) -> -10 pts residual
                MissionCompletionStatus.Completed,
                burstCount: 0,
                baseScore: 95.0f); // 95 - 25 - 10 = 60.0
            Assert.AreEqual(60.0f, resultBLow.FinalScore, 0.001f);
            Assert.AreEqual(GradeRank.B, resultBLow.Rank);

            // Test Rank C: 59.9f
            StealthGradeResult resultC = StealthGradeOperator.CalculateGrade(
                chaseCount: 1,
                avgResidual: 1.50f,
                MissionCompletionStatus.Completed,
                burstCount: 0,
                baseScore: 94.9f); // 94.9 - 25 - 10 = 59.9
            Assert.AreEqual(59.9f, resultC.FinalScore, 0.01f);
            Assert.AreEqual(GradeRank.C, resultC.Rank);
        }

        [Test]
        public void test_grade_operator_component_tracking_and_event_dispatch()
        {
            // Arrange
            StealthGradeResult receivedResult = default;
            bool eventFired = false;
            _operator.OnGradeEvaluated += res =>
            {
                receivedResult = res;
                eventFired = true;
            };

            // Act
            _operator.RecordChaseIncident(1);
            _operator.RecordChaseIncident(2);
            _operator.RecordBurstDistraction();
            _operator.RecordBurstDistraction();
            _operator.SetResidualWariness(0.30f);

            StealthGradeResult evalResult = _operator.EvaluateFinalGrade(MissionCompletionStatus.Completed);

            // Assert
            Assert.IsTrue(eventFired, "OnGradeEvaluated event must fire upon evaluation");
            Assert.IsTrue(_operator.HasEvaluated);
            Assert.AreEqual(2, _operator.ChaseIncidentCount);
            Assert.AreEqual(2, _operator.BurstDistractionCount);
            Assert.AreEqual(50.0f, evalResult.IncidentPenalty, 0.001f); // 2 * 25
            Assert.AreEqual(3.0f, evalResult.ResidualPenalty, 0.001f); // 0.3 * 10
            Assert.AreEqual(47.0f, evalResult.FinalScore, 0.001f); // 100 - 53
            Assert.AreEqual(GradeRank.C, evalResult.Rank);
            Assert.AreEqual(evalResult.FinalScore, receivedResult.FinalScore, 0.001f);
            Assert.AreEqual(evalResult.Rank, receivedResult.Rank);

            // Verify Reset
            _operator.ResetRun();
            Assert.AreEqual(0, _operator.ChaseIncidentCount);
            Assert.AreEqual(0, _operator.BurstDistractionCount);
            Assert.IsFalse(_operator.IsCaptured);
            Assert.IsFalse(_operator.HasEvaluated);
        }
    }
}
