using UnityEngine;

namespace WhisperWard.AI.Perception
{
    /// <summary>
    /// Visual representation component for Security Camera AI Sensor.
    /// Updates spotlight/lens indicators and cone colors reflecting sensor state:
    /// Green/Cyan (IdleSweeping) -> Amber (TargetAcquiring) -> Alert Red (AlarmTriggered).
    /// </summary>
    [RequireComponent(typeof(SecurityCameraSensor))]
    public class SecurityCameraVisualizer : MonoBehaviour
    {
        [Header("Color States")]
        [SerializeField] private Color _idleColor = new Color(0.0f, 0.9f, 1.0f, 0.4f); // Cyan
        [SerializeField] private Color _acquiringColor = new Color(1.0f, 0.72f, 0.0f, 0.7f); // Amber #FFB800
        [SerializeField] private Color _alarmColor = new Color(1.0f, 0.15f, 0.15f, 0.9f); // Alert Red #FF2222

        [Header("Components")]
        [SerializeField] private SecurityCameraSensor _sensor;
        [SerializeField] private Light _spotlight;
        [SerializeField] private Renderer _lensRenderer;

        public Color CurrentColor { get; private set; }

        private void Awake()
        {
            if (_sensor == null)
            {
                _sensor = GetComponent<SecurityCameraSensor>();
            }

            CurrentColor = _idleColor;
        }

        private void OnEnable()
        {
            if (_sensor != null)
            {
                _sensor.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_sensor != null)
            {
                _sensor.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(SecurityCameraState oldState, SecurityCameraState newState)
        {
            switch (newState)
            {
                case SecurityCameraState.IdleSweeping:
                    CurrentColor = _idleColor;
                    break;
                case SecurityCameraState.TargetAcquiring:
                    CurrentColor = _acquiringColor;
                    break;
                case SecurityCameraState.AlarmTriggered:
                    CurrentColor = _alarmColor;
                    break;
            }

            if (_spotlight != null)
            {
                _spotlight.color = CurrentColor;
            }

            if (_lensRenderer != null && _lensRenderer.material != null)
            {
                _lensRenderer.material.color = CurrentColor;
            }
        }
    }
}
