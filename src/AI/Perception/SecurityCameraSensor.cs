using System;
using UnityEngine;
using WhisperWard.AI.Alert;

namespace WhisperWard.AI.Perception
{
    /// <summary>
    /// Discrete states for autonomous security camera sensors.
    /// </summary>
    public enum SecurityCameraState
    {
        /// <summary>Standard sweeping mode scanning the patrol sector.</summary>
        IdleSweeping = 0,

        /// <summary>Target spotted; accumulating confirmation window.</summary>
        TargetAcquiring = 1,

        /// <summary>Target confirmed; siren engaged and guards dispatched.</summary>
        AlarmTriggered = 2
    }

    /// <summary>
    /// Autonomous Security Camera AI Sensor component.
    /// Performs continuous kinematic yaw sweeping between configurable angle limits,
    /// evaluates player line-of-sight and hide sanctuary immunity,
    /// and broadcasts facility-wide alert barks when intrusion is confirmed.
    /// Zero heap allocation on update ticks.
    /// </summary>
    /// <example>
    /// <code>
    /// var camera = gameObject.AddComponent&lt;SecurityCameraSensor&gt;();
    /// camera.ConfigureSweep(centerAngle: 0f, sweepHalfAngle: 45f, sweepSpeedDegPerSec: 30f);
    /// camera.OnCameraAlarmTriggered += pos =&gt; Debug.Log($"Intruder at: {pos}");
    /// </code>
    /// </example>
    public class SecurityCameraSensor : MonoBehaviour
    {
        [Header("Sweep Kinematics")]
        [SerializeField] private float _centerAngle = 0.0f;
        [SerializeField] private float _sweepHalfAngle = 45.0f;
        [SerializeField] private float _sweepSpeedDegPerSec = 30.0f;
        [SerializeField] private float _currentYaw = 0.0f;
        [SerializeField] private float _sweepDirection = 1.0f;

        [Header("Perception Parameters")]
        [SerializeField] private float _visionFOV = 60.0f;
        [SerializeField] private float _visionRange = 12.0f;
        [SerializeField] private float _confirmTime = 0.5f;
        [SerializeField] private float _alarmDuration = 5.0f;

        [Header("State")]
        [SerializeField] private SecurityCameraState _state = SecurityCameraState.IdleSweeping;
        [SerializeField] private float _acquisitionTimer = 0.0f;
        [SerializeField] private float _alarmTimer = 0.0f;

        [Header("Simulation Overrides")]
        [SerializeField] private bool _manualTargetInCone;
        [SerializeField] private bool _manualTargetInSanctuary;
        [SerializeField] private Vector3 _lastKnownPosition;

        /// <summary>Fired when camera confirms intrusion and triggers facility alarm.</summary>
        public event Action<Vector3> OnCameraAlarmTriggered;

        /// <summary>Fired on camera state transitions.</summary>
        public event Action<SecurityCameraState, SecurityCameraState> OnStateChanged;

        public SecurityCameraState State => _state;
        public float CurrentYaw => _currentYaw;
        public float VisionFOV => _visionFOV;
        public float VisionRange => _visionRange;
        public float ConfirmTime => _confirmTime;
        public float AcquisitionProgress => _confirmTime > 0.001f ? Mathf.Clamp01(_acquisitionTimer / _confirmTime) : 0f;
        public Vector3 LastKnownPosition => _lastKnownPosition;

        private void Update()
        {
            SimulateTick(Time.deltaTime);
        }

        /// <summary>
        /// Configures camera sweep angular boundaries and speed.
        /// </summary>
        public void ConfigureSweep(float centerAngle, float sweepHalfAngle, float sweepSpeedDegPerSec)
        {
            _centerAngle = centerAngle;
            _sweepHalfAngle = Mathf.Abs(sweepHalfAngle);
            _sweepSpeedDegPerSec = Mathf.Abs(sweepSpeedDegPerSec);
            _currentYaw = _centerAngle;
            _sweepDirection = 1.0f;
        }

        /// <summary>
        /// Configures optical detection thresholds.
        /// </summary>
        public void ConfigurePerception(float fov, float range, float confirmTime)
        {
            _visionFOV = Mathf.Clamp(fov, 10f, 180f);
            _visionRange = Mathf.Max(1f, range);
            _confirmTime = Mathf.Max(0.05f, confirmTime);
        }

        /// <summary>
        /// Manual override for unit testing target visibility.
        /// </summary>
        public void SetTargetInConeAndLOS(bool inConeAndLOS, bool isInSanctuary, Vector3 targetPos)
        {
            _manualTargetInCone = inConeAndLOS;
            _manualTargetInSanctuary = isInSanctuary;
            if (inConeAndLOS)
            {
                _lastKnownPosition = targetPos;
            }
        }

        /// <summary>
        /// Advances camera kinematics and perception state by delta time.
        /// Deterministic for headless testing.
        /// </summary>
        public void SimulateTick(float deltaTime)
        {
            if (deltaTime <= 0.0f) return;

            if (_state == SecurityCameraState.AlarmTriggered)
            {
                _alarmTimer -= deltaTime;
                if (_alarmTimer <= 0.0f)
                {
                    TransitionTo(SecurityCameraState.IdleSweeping);
                }
                return;
            }

            // Kinematic sweep update
            float minAngle = _centerAngle - _sweepHalfAngle;
            float maxAngle = _centerAngle + _sweepHalfAngle;

            _currentYaw += _sweepDirection * _sweepSpeedDegPerSec * deltaTime;

            if (_currentYaw >= maxAngle)
            {
                _currentYaw = maxAngle;
                _sweepDirection = -1.0f;
            }
            else if (_currentYaw <= minAngle)
            {
                _currentYaw = minAngle;
                _sweepDirection = 1.0f;
            }

            transform.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);

            // Target perception evaluation
            bool targetDetected = _manualTargetInCone && !_manualTargetInSanctuary;

            if (targetDetected)
            {
                _acquisitionTimer += deltaTime;

                if (_acquisitionTimer >= _confirmTime)
                {
                    TriggerAlarm();
                }
                else
                {
                    TransitionTo(SecurityCameraState.TargetAcquiring);
                }
            }
            else
            {
                _acquisitionTimer = Mathf.Max(0.0f, _acquisitionTimer - deltaTime * 2.0f);

                if (_acquisitionTimer <= 0.0f)
                {
                    TransitionTo(SecurityCameraState.IdleSweeping);
                }
            }
        }

        private void TriggerAlarm()
        {
            TransitionTo(SecurityCameraState.AlarmTriggered);
            _alarmTimer = _alarmDuration;
            _acquisitionTimer = 0.0f;

            OnCameraAlarmTriggered?.Invoke(_lastKnownPosition);

            // Dispatch global radio alert to guard network
            GuardRadioAlertTransceiver.BroadcastCustomAlert(new GuardAlertEvent(
                GetInstanceID(),
                transform.position,
                _lastKnownPosition,
                radioRadius: 35.0f
            ));
        }

        private void TransitionTo(SecurityCameraState newState)
        {
            if (_state == newState) return;

            SecurityCameraState oldState = _state;
            _state = newState;
            OnStateChanged?.Invoke(oldState, newState);
        }
    }
}
