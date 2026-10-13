using System;
using UnityEngine;

namespace WhisperWard.Core.Grade
{
    /// <summary>
    /// Letter grade ranking assigned to a stealth mission attempt.
    /// </summary>
    public enum GradeRank
    {
        None,
        S,
        A,
        B,
        C,
        F
    }

    /// <summary>
    /// Mission lifecycle completion status.
    /// </summary>
    public enum MissionCompletionStatus
    {
        InProgress,
        Completed,
        Failed
    }

    /// <summary>
    /// Immutable result snapshot produced by the Stealth Grade Operator.
    /// </summary>
    [Serializable]
    public struct StealthGradeResult
    {
        public float BaseScore;
        public float IncidentPenalty;
        public float ResidualPenalty;
        public float TotalPenalty;
        public float FinalScore;
        public GradeRank Rank;
        public MissionCompletionStatus Status;
        public int ChaseIncidentCount;
        public int BurstDistractionCount;
        public float AverageResidualWariness;
        public bool IsZeroChaseMastery;

        public StealthGradeResult(
            float baseScore,
            float incidentPenalty,
            float residualPenalty,
            float totalPenalty,
            float finalScore,
            GradeRank rank,
            MissionCompletionStatus status,
            int chaseIncidentCount,
            int burstDistractionCount,
            float averageResidualWariness,
            bool isZeroChaseMastery)
        {
            BaseScore = baseScore;
            IncidentPenalty = incidentPenalty;
            ResidualPenalty = residualPenalty;
            TotalPenalty = totalPenalty;
            FinalScore = finalScore;
            Rank = rank;
            Status = status;
            ChaseIncidentCount = chaseIncidentCount;
            BurstDistractionCount = burstDistractionCount;
            AverageResidualWariness = averageResidualWariness;
            IsZeroChaseMastery = isZeroChaseMastery;
        }
    }

    /// <summary>
    /// Deterministic Stealth Grade Operator implementing the scoring and evaluation
    /// contract from GDD suspicion-meter-grade.md (D4..D8).
    /// </summary>
    public class StealthGradeOperator : MonoBehaviour
    {
        [Header("Base Scoring & Floor")]
        [SerializeField] private float _baseScore = 100.0f;
        [SerializeField] private float _completionFloor = 10.0f;
        [SerializeField] private float _penaltyCap = 90.0f;

        [Header("Incident Weights")]
        [SerializeField] private float _chasePenaltyPerIncident = 25.0f;
        [SerializeField] private float _burstDistractionPenalty = 0.0f; // Clean Distraction Exemption (D4 invariant)

        [Header("Residual Wariness")]
        [SerializeField] private float _maxResidualPenalty = 10.0f;
        [SerializeField] private float _rMax = 1.0f;

        [Header("Grade Rank Thresholds")]
        [SerializeField] private float _thresholdS = 90.0f;
        [SerializeField] private float _thresholdA = 75.0f;
        [SerializeField] private float _thresholdB = 60.0f;

        // Runtime Tracking State
        private int _chaseIncidentCount;
        private int _burstDistractionCount;
        private bool _isCaptured;
        private float _averageResidualWariness;
        private StealthGradeResult _lastResult;
        private bool _hasEvaluated;

        /// <summary>
        /// Dispatched synchronously when a grade evaluation concludes.
        /// </summary>
        public event Action<StealthGradeResult> OnGradeEvaluated;

        /// <summary>Gets the current total number of Chase incidents recorded.</summary>
        public int ChaseIncidentCount => _chaseIncidentCount;

        /// <summary>Gets the current total number of Burst distraction deployments recorded.</summary>
        public int BurstDistractionCount => _burstDistractionCount;

        /// <summary>Gets whether the player has been captured during this run.</summary>
        public bool IsCaptured => _isCaptured;

        /// <summary>Gets the last calculated evaluation result.</summary>
        public StealthGradeResult LastResult => _lastResult;

        /// <summary>Gets whether an evaluation has been performed for the current run.</summary>
        public bool HasEvaluated => _hasEvaluated;

        public float BaseScore => _baseScore;
        public float ChasePenaltyPerIncident => _chasePenaltyPerIncident;
        public float MaxResidualPenalty => _maxResidualPenalty;
        public float PenaltyCap => _penaltyCap;
        public float CompletionFloor => _completionFloor;
        public float ThresholdS => _thresholdS;
        public float ThresholdA => _thresholdA;
        public float ThresholdB => _thresholdB;

        /// <summary>
        /// Records an escalation to Chase state across any guard in the room.
        /// </summary>
        public void RecordChaseIncident(int guardId = 0)
        {
            _chaseIncidentCount++;
        }

        /// <summary>
        /// Records deployment of a Burst noisemaker (incurs 0.0 pts deduction per D4 invariant).
        /// </summary>
        public void RecordBurstDistraction()
        {
            _burstDistractionCount++;
        }

        /// <summary>
        /// Records that the player was physically captured by a guard.
        /// </summary>
        public void RecordPlayerCaptured()
        {
            _isCaptured = true;
        }

        /// <summary>
        /// Updates the authoritative average residual wariness observed at mission boundary.
        /// </summary>
        public void SetResidualWariness(float averageResidual)
        {
            _averageResidualWariness = Mathf.Max(0.0f, averageResidual);
        }

        /// <summary>
        /// Resets all counters and state for a fresh mission attempt.
        /// </summary>
        public void ResetRun()
        {
            _chaseIncidentCount = 0;
            _burstDistractionCount = 0;
            _isCaptured = false;
            _averageResidualWariness = 0.0f;
            _hasEvaluated = false;
            _lastResult = default;
        }

        /// <summary>
        /// Finalizes the current attempt and evaluates the stealth scorecard.
        /// </summary>
        public StealthGradeResult EvaluateFinalGrade(MissionCompletionStatus completionStatus)
        {
            if (_isCaptured)
            {
                completionStatus = MissionCompletionStatus.Failed;
            }

            _lastResult = CalculateGrade(
                _chaseIncidentCount,
                _averageResidualWariness,
                completionStatus,
                _burstDistractionCount,
                _baseScore,
                _chasePenaltyPerIncident,
                _burstDistractionPenalty,
                _maxResidualPenalty,
                _rMax,
                _penaltyCap,
                _completionFloor,
                _thresholdS,
                _thresholdA,
                _thresholdB);

            _hasEvaluated = true;
            OnGradeEvaluated?.Invoke(_lastResult);
            return _lastResult;
        }

        /// <summary>
        /// Pure deterministic calculation of stealth score and grade rank.
        /// </summary>
        public static StealthGradeResult CalculateGrade(
            int chaseCount,
            float avgResidual,
            MissionCompletionStatus status,
            int burstCount = 0,
            float baseScore = 100.0f,
            float chaseWeight = 25.0f,
            float burstWeight = 0.0f,
            float maxResidualPenalty = 10.0f,
            float rMax = 1.0f,
            float penaltyCap = 90.0f,
            float completionFloor = 10.0f,
            float thresholdS = 90.0f,
            float thresholdA = 75.0f,
            float thresholdB = 60.0f)
        {
            chaseCount = Mathf.Max(0, chaseCount);
            burstCount = Mathf.Max(0, burstCount);
            avgResidual = Mathf.Max(0.0f, avgResidual);
            rMax = Mathf.Max(0.0001f, rMax);

            // 1. Incident Penalty: P_incident = min(Cap, chaseCount * W_chase + burstCount * W_burst)
            float rawIncident = (chaseCount * chaseWeight) + (burstCount * burstWeight);
            float incidentPenalty = Mathf.Min(penaltyCap, rawIncident);

            // 2. Residual Wariness Penalty: P_residual = W_residual * clamp(R_avg / R_max, 0, 1)
            float normalizedResidual = Mathf.Clamp01(avgResidual / rMax);
            float residualPenalty = maxResidualPenalty * normalizedResidual;

            // 3. Total Penalty Cap: P_total = min(Cap, P_incident + P_residual)
            float totalPenalty = Mathf.Min(penaltyCap, incidentPenalty + residualPenalty);

            // 4. Score & Rank Evaluation
            float finalScore;
            GradeRank rank;
            bool zeroChaseMastery = (chaseCount == 0);

            if (status == MissionCompletionStatus.Failed)
            {
                finalScore = 0.0f;
                rank = GradeRank.F;
            }
            else
            {
                // Completed run enforces completion floor
                finalScore = Mathf.Max(completionFloor, baseScore - totalPenalty);

                // Grade Letter Mapping (D8)
                if (finalScore >= thresholdS && zeroChaseMastery)
                {
                    rank = GradeRank.S;
                }
                else if (finalScore >= thresholdA)
                {
                    rank = GradeRank.A;
                }
                else if (finalScore >= thresholdB)
                {
                    rank = GradeRank.B;
                }
                else
                {
                    rank = GradeRank.C;
                }
            }

            return new StealthGradeResult(
                baseScore,
                incidentPenalty,
                residualPenalty,
                totalPenalty,
                finalScore,
                rank,
                status,
                chaseCount,
                burstCount,
                avgResidual,
                zeroChaseMastery);
        }
    }
}
