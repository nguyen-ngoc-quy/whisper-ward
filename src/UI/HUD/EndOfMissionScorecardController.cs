using System;
using UnityEngine;
using WhisperWard.Core.Grade;

namespace WhisperWard.UI.HUD
{
    /// <summary>
    /// UI Controller for the End-of-Mission Scorecard HUD modal dialog.
    /// Displays letter grade badge (S/A/B/C/F), final score, incident & residual penalties,
    /// and handles restart / proceed actions.
    /// Conforms to GDD Suspicion Meter / Grade Operator (UI Requirements) and Story UI-RESULT-01.
    /// </summary>
    public class EndOfMissionScorecardController : MonoBehaviour
    {
        [Header("Modal Root")]
        [SerializeField] private GameObject _modalRoot;

        [Header("Grade Color Scheme")]
        [SerializeField] private Color _colorRankS = new Color(1.0f, 0.843f, 0.0f);     // Gold #FFD700
        [SerializeField] private Color _colorRankA = new Color(0.298f, 0.686f, 0.314f); // Green #4CAF50
        [SerializeField] private Color _colorRankB = new Color(0.129f, 0.588f, 0.953f); // Blue #2196F3
        [SerializeField] private Color _colorRankC = new Color(1.0f, 0.596f, 0.0f);     // Orange #FF9800
        [SerializeField] private Color _colorRankF = new Color(0.718f, 0.110f, 0.110f); // Crimson #B71C1C

        // Runtime State Cache
        private bool _isVisible;
        private StealthGradeResult _cachedResult;
        private string _formattedRank = string.Empty;
        private Color _currentBadgeColor = Color.white;
        private string _formattedScore = string.Empty;
        private string _formattedIncidentPenalty = string.Empty;
        private string _formattedResidualPenalty = string.Empty;
        private string _formattedTotalPenalty = string.Empty;

        /// <summary>Dispatched when the scorecard modal is displayed.</summary>
        public event Action<StealthGradeResult> OnScorecardPresented;

        /// <summary>Dispatched when the player clicks the restart button.</summary>
        public event Action OnRestartRequested;

        /// <summary>Dispatched when the player clicks the next mission or continue button.</summary>
        public event Action OnContinueRequested;

        /// <summary>Dispatched when the scorecard modal is dismissed/hidden.</summary>
        public event Action OnScorecardDismissed;

        public bool IsVisible => _isVisible;
        public StealthGradeResult CachedResult => _cachedResult;
        public string FormattedRank => _formattedRank;
        public Color CurrentBadgeColor => _currentBadgeColor;
        public string FormattedScore => _formattedScore;
        public string FormattedIncidentPenalty => _formattedIncidentPenalty;
        public string FormattedResidualPenalty => _formattedResidualPenalty;
        public string FormattedTotalPenalty => _formattedTotalPenalty;

        public Color ColorRankS => _colorRankS;
        public Color ColorRankA => _colorRankA;
        public Color ColorRankB => _colorRankB;
        public Color ColorRankC => _colorRankC;
        public Color ColorRankF => _colorRankF;

        private void Awake()
        {
            if (_modalRoot == null)
            {
                _modalRoot = gameObject;
            }

            // Hidden by default during active stealth gameplay
            HideModal();
        }

        /// <summary>
        /// Binds to a StealthGradeOperator instance to automatically present on evaluation.
        /// </summary>
        public void BindOperator(StealthGradeOperator gradeOperator)
        {
            if (gradeOperator != null)
            {
                gradeOperator.OnGradeEvaluated += PresentGrade;
            }
        }

        /// <summary>
        /// Unbinds from a StealthGradeOperator instance.
        /// </summary>
        public void UnbindOperator(StealthGradeOperator gradeOperator)
        {
            if (gradeOperator != null)
            {
                gradeOperator.OnGradeEvaluated -= PresentGrade;
            }
        }

        /// <summary>
        /// Formats and presents the scorecard HUD with evaluated grade metrics.
        /// </summary>
        public void PresentGrade(StealthGradeResult result)
        {
            _cachedResult = result;
            _formattedRank = FormatRank(result.Rank);
            _currentBadgeColor = GetColorForRank(result.Rank);
            _formattedScore = $"{result.FinalScore:F1} pts";
            _formattedIncidentPenalty = $"-{result.IncidentPenalty:F1} pts";
            _formattedResidualPenalty = $"-{result.ResidualPenalty:F1} pts";
            _formattedTotalPenalty = $"-{result.TotalPenalty:F1} pts";

            _isVisible = true;
            if (_modalRoot != null)
            {
                _modalRoot.SetActive(true);
            }

            OnScorecardPresented?.Invoke(result);
        }

        /// <summary>
        /// Dismisses and hides the scorecard HUD modal.
        /// </summary>
        public void HideModal()
        {
            _isVisible = false;
            if (_modalRoot != null)
            {
                _modalRoot.SetActive(false);
            }

            OnScorecardDismissed?.Invoke();
        }

        /// <summary>
        /// Invoked by UI button when user requests a mission restart.
        /// </summary>
        public void RequestRestart()
        {
            HideModal();
            OnRestartRequested?.Invoke();
        }

        /// <summary>
        /// Invoked by UI button when user requests continuation / next segment.
        /// </summary>
        public void RequestContinue()
        {
            HideModal();
            OnContinueRequested?.Invoke();
        }

        public Color GetColorForRank(GradeRank rank)
        {
            switch (rank)
            {
                case GradeRank.S: return _colorRankS;
                case GradeRank.A: return _colorRankA;
                case GradeRank.B: return _colorRankB;
                case GradeRank.C: return _colorRankC;
                case GradeRank.F:
                default:
                    return _colorRankF;
            }
        }

        public static string FormatRank(GradeRank rank)
        {
            switch (rank)
            {
                case GradeRank.S: return "S";
                case GradeRank.A: return "A";
                case GradeRank.B: return "B";
                case GradeRank.C: return "C";
                case GradeRank.F: return "F (FAIL)";
                default: return "-";
            }
        }
    }
}
