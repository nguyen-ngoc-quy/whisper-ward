using System;
using UnityEngine;
using WhisperWard.Core.Environment;
using WhisperWard.Core.Grade;

namespace WhisperWard.Core.Objectives
{
    /// <summary>
    /// Discrete states composing the mission progression lifecycle.
    /// </summary>
    public enum MissionState
    {
        Infiltration,
        ObjectiveAcquired,
        GateUnlocked,
        MissionCompleted,
        MissionFailed
    }

    /// <summary>
    /// Central gameplay coordinator governing the full objective lifecycle:
    /// Infiltration -> ObjectiveAcquired -> GateUnlocked -> MissionCompleted (or MissionFailed).
    /// </summary>
    public class MissionLifecycleController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private StealthGradeOperator _gradeOperator;
        [SerializeField] private ObjectiveKeycardPickup _objectivePickup;
        [SerializeField] private LockedSecurityGate _securityGate;
        [SerializeField] private ExtractionZone _extractionZone;

        [Header("State")]
        [SerializeField] private MissionState _currentState = MissionState.Infiltration;

        /// <summary>Dispatched whenever the mission transitions to a new state.</summary>
        public event Action<MissionState> OnStateChanged;

        /// <summary>Dispatched when the mission finishes with final status and stealth scorecard.</summary>
        public event Action<MissionCompletionStatus, StealthGradeResult> OnMissionFinished;

        /// <summary>Dispatched when a new mission attempt starts or restarts.</summary>
        public event Action OnMissionStarted;

        /// <summary>Gets the current lifecycle progression state.</summary>
        public MissionState CurrentState => _currentState;

        /// <summary>Gets whether the mission has concluded (Completed or Failed).</summary>
        public bool IsFinished => _currentState == MissionState.MissionCompleted || _currentState == MissionState.MissionFailed;

        /// <summary>Gets the associated stealth grade evaluation operator.</summary>
        public StealthGradeOperator GradeOperator => _gradeOperator;

        private void OnEnable()
        {
            ObjectiveKeycardPickup.OnAnyObjectiveAcquired += HandleObjectiveAcquired;
            LockedSecurityGate.OnAnyGateUnlocked += HandleGateUnlocked;
            ExtractionZone.OnAnyExtractionCompleted += HandleExtractionCompleted;
        }

        private void OnDisable()
        {
            ObjectiveKeycardPickup.OnAnyObjectiveAcquired -= HandleObjectiveAcquired;
            LockedSecurityGate.OnAnyGateUnlocked -= HandleGateUnlocked;
            ExtractionZone.OnAnyExtractionCompleted -= HandleExtractionCompleted;
        }

        /// <summary>
        /// Explicitly binds references to participating scene systems (useful for tests and dynamic setups).
        /// </summary>
        public void BindSystems(
            StealthGradeOperator gradeOperator,
            ObjectiveKeycardPickup pickup = null,
            LockedSecurityGate gate = null,
            ExtractionZone zone = null)
        {
            _gradeOperator = gradeOperator;
            _objectivePickup = pickup;
            _securityGate = gate;
            _extractionZone = zone;
        }

        /// <summary>
        /// Initiates or initializes the mission in the initial Infiltration state.
        /// </summary>
        public void StartMission()
        {
            _currentState = MissionState.Infiltration;
            OnMissionStarted?.Invoke();
            OnStateChanged?.Invoke(_currentState);
        }

        /// <summary>
        /// Handles transition when any objective item is acquired.
        /// </summary>
        public void HandleObjectiveAcquired(string objectiveId)
        {
            if (_currentState != MissionState.Infiltration)
            {
                return;
            }

            _currentState = MissionState.ObjectiveAcquired;
            OnStateChanged?.Invoke(_currentState);
        }

        /// <summary>
        /// Handles transition when the security gate is unlocked.
        /// </summary>
        public void HandleGateUnlocked()
        {
            if (_currentState != MissionState.ObjectiveAcquired)
            {
                return;
            }

            _currentState = MissionState.GateUnlocked;
            OnStateChanged?.Invoke(_currentState);
        }

        /// <summary>
        /// Handles extraction completion and evaluates final stealth scorecard.
        /// </summary>
        public void HandleExtractionCompleted()
        {
            if (_currentState != MissionState.GateUnlocked)
            {
                return;
            }

            _currentState = MissionState.MissionCompleted;
            OnStateChanged?.Invoke(_currentState);

            StealthGradeResult grade = default;
            if (_gradeOperator != null)
            {
                grade = _gradeOperator.EvaluateFinalGrade(MissionCompletionStatus.Completed);
            }

            OnMissionFinished?.Invoke(MissionCompletionStatus.Completed, grade);
        }

        /// <summary>
        /// Handles immediate mission failure upon guard capture.
        /// </summary>
        public void HandlePlayerCaptured(Vector3 capturePosition = default)
        {
            if (IsFinished)
            {
                return;
            }

            _currentState = MissionState.MissionFailed;
            OnStateChanged?.Invoke(_currentState);

            StealthGradeResult grade = default;
            if (_gradeOperator != null)
            {
                _gradeOperator.RecordPlayerCaptured();
                grade = _gradeOperator.EvaluateFinalGrade(MissionCompletionStatus.Failed);
            }

            OnMissionFinished?.Invoke(MissionCompletionStatus.Failed, grade);
        }

        /// <summary>
        /// Resets all bound entities and resets the mission back to initial state.
        /// </summary>
        public void RestartMission()
        {
            if (_objectivePickup != null)
            {
                _objectivePickup.ResetPickup();
            }

            if (_securityGate != null)
            {
                _securityGate.ResetGate();
            }

            if (_extractionZone != null)
            {
                _extractionZone.ResetZone();
            }

            if (_gradeOperator != null)
            {
                _gradeOperator.ResetRun();
            }

            StartMission();
        }
    }
}
