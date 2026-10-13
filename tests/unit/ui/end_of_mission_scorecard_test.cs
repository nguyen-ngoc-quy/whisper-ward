using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Grade;
using WhisperWard.UI.HUD;

namespace WhisperWard.Tests.Unit.UI
{
    [TestFixture]
    public class EndOfMissionScorecardTest
    {
        private GameObject _hudHost;
        private GameObject _modalRoot;
        private EndOfMissionScorecardController _controller;
        private StealthGradeOperator _operator;

        [SetUp]
        public void SetUp()
        {
            _hudHost = new GameObject("ScorecardHUD_TestHost");
            _modalRoot = new GameObject("ModalRoot");
            _modalRoot.transform.SetParent(_hudHost.transform);

            _controller = _hudHost.AddComponent<EndOfMissionScorecardController>();
            _operator = _hudHost.AddComponent<StealthGradeOperator>();

            var rootField = typeof(EndOfMissionScorecardController).GetField("_modalRoot",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            rootField?.SetValue(_controller, _modalRoot);

            _controller.HideModal();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hudHost != null)
            {
                Object.DestroyImmediate(_hudHost);
            }
        }

        [Test]
        public void test_scorecard_hidden_by_default()
        {
            Assert.IsFalse(_controller.IsVisible);
            Assert.IsFalse(_modalRoot.activeSelf);
        }

        [Test]
        public void test_scorecard_presents_rank_s_badge_and_score()
        {
            // Arrange
            var result = new StealthGradeResult(
                baseScore: 100f,
                incidentPenalty: 0f,
                residualPenalty: 0f,
                totalPenalty: 0f,
                finalScore: 100f,
                rank: GradeRank.S,
                status: MissionCompletionStatus.Completed,
                chaseIncidentCount: 0,
                burstDistractionCount: 0,
                averageResidualWariness: 0f,
                isZeroChaseMastery: true);

            // Act
            _controller.PresentGrade(result);

            // Assert
            Assert.IsTrue(_controller.IsVisible);
            Assert.IsTrue(_modalRoot.activeSelf);
            Assert.AreEqual("S", _controller.FormattedRank);
            Assert.AreEqual(_controller.ColorRankS, _controller.CurrentBadgeColor);
            Assert.AreEqual("100.0 pts", _controller.FormattedScore);
            Assert.AreEqual("-0.0 pts", _controller.FormattedTotalPenalty);
        }

        [Test]
        public void test_scorecard_presents_rank_f_for_failed_capture_mission()
        {
            // Arrange
            var result = new StealthGradeResult(
                baseScore: 100f,
                incidentPenalty: 0f,
                residualPenalty: 0f,
                totalPenalty: 100f,
                finalScore: 0f,
                rank: GradeRank.F,
                status: MissionCompletionStatus.Failed,
                chaseIncidentCount: 1,
                burstDistractionCount: 0,
                averageResidualWariness: 0.5f,
                isZeroChaseMastery: false);

            // Act
            _controller.PresentGrade(result);

            // Assert
            Assert.IsTrue(_controller.IsVisible);
            Assert.AreEqual("F (FAIL)", _controller.FormattedRank);
            Assert.AreEqual(_controller.ColorRankF, _controller.CurrentBadgeColor);
            Assert.AreEqual("0.0 pts", _controller.FormattedScore);
        }

        [Test]
        public void test_scorecard_formats_breakdown_metrics_accurately()
        {
            // Arrange
            var result = new StealthGradeResult(
                baseScore: 100f,
                incidentPenalty: 25f,
                residualPenalty: 5f,
                totalPenalty: 30f,
                finalScore: 70f,
                rank: GradeRank.B,
                status: MissionCompletionStatus.Completed,
                chaseIncidentCount: 1,
                burstDistractionCount: 2,
                averageResidualWariness: 0.5f,
                isZeroChaseMastery: false);

            // Act
            _controller.PresentGrade(result);

            // Assert
            Assert.AreEqual("-25.0 pts", _controller.FormattedIncidentPenalty);
            Assert.AreEqual("-5.0 pts", _controller.FormattedResidualPenalty);
            Assert.AreEqual("-30.0 pts", _controller.FormattedTotalPenalty);
            Assert.AreEqual("70.0 pts", _controller.FormattedScore);
            Assert.AreEqual("B", _controller.FormattedRank);
            Assert.AreEqual(_controller.ColorRankB, _controller.CurrentBadgeColor);
        }

        [Test]
        public void test_scorecard_dismiss_or_restart_event_fired()
        {
            bool restarted = false;
            bool continued = false;
            bool dismissed = false;

            _controller.OnRestartRequested += () => restarted = true;
            _controller.OnContinueRequested += () => continued = true;
            _controller.OnScorecardDismissed += () => dismissed = true;

            var result = new StealthGradeResult(100f, 0f, 0f, 0f, 100f, GradeRank.S, MissionCompletionStatus.Completed, 0, 0, 0f, true);
            _controller.PresentGrade(result);

            Assert.IsTrue(_controller.IsVisible);

            _controller.RequestRestart();

            Assert.IsTrue(restarted);
            Assert.IsTrue(dismissed);
            Assert.IsFalse(_controller.IsVisible);

            _controller.PresentGrade(result);
            _controller.RequestContinue();

            Assert.IsTrue(continued);
            Assert.IsFalse(_controller.IsVisible);
        }
    }
}
