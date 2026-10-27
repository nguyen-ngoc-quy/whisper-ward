using System;
using UnityEngine;
using WhisperWard.AI.FSM;
using WhisperWard.Gameplay.Hide;

namespace WhisperWard.Core.Rendering
{
    /// <summary>
    /// Coordinates game feel, camera impulse shakes, and adaptive post-processing focus.
    /// Responds to guard Chase states by requesting camera impulse trauma,
    /// and deepens vignette darkness when the player enters a Hide Spot sanctuary.
    /// Zero heap allocation on update loops.
    /// </summary>
    /// <example>
    /// <code>
    /// var feelController = gameObject.AddComponent&lt;StealthGameFeelController&gt;();
    /// feelController.OnCameraShakeRequested += (intensity, dur) =&gt; TriggerCinemachineImpulse(intensity);
    /// feelController.OnHidingVignetteFocus += targetVignette =&gt; volumeProfile.vignette.intensity.value = targetVignette;
    /// </code>
    /// </example>
    public class StealthGameFeelController : MonoBehaviour
    {
        [Header("Camera Shake Tuning")]
        [SerializeField] private float _chaseShakeIntensity = 0.40f;
        [SerializeField] private float _chaseShakeDuration = 0.60f;

        [Header("Vignette Depth Tuning")]
        [SerializeField] private float _baselineVignette = 0.35f;
        [SerializeField] private float _hidingVignette = 0.55f;

        /// <summary>Dispatched when a screen shake impulse is requested (intensity, duration).</summary>
        public event Action<float, float> OnCameraShakeRequested;

        /// <summary>Dispatched when vignette target intensity should transition (targetIntensity).</summary>
        public event Action<float> OnHidingVignetteFocus;

        public float ChaseShakeIntensity => _chaseShakeIntensity;
        public float ChaseShakeDuration => _chaseShakeDuration;
        public float BaselineVignette => _baselineVignette;
        public float HidingVignette => _hidingVignette;

        private void OnEnable()
        {
            HideSpotSanctuary.OnAnyPlayerHiddenStateChanged += HandleHidingStateChanged;
        }

        private void OnDisable()
        {
            HideSpotSanctuary.OnAnyPlayerHiddenStateChanged -= HandleHidingStateChanged;
        }

        /// <summary>
        /// Responds to guard FSM state transitions to request camera shake on sudden Chase initiation.
        /// </summary>
        public void HandleGuardStateChanged(GuardState previousState, GuardState newState)
        {
            if (previousState == newState) return;

            if (newState == GuardState.Chase)
            {
                OnCameraShakeRequested?.Invoke(_chaseShakeIntensity, _chaseShakeDuration);
            }
        }

        /// <summary>
        /// Responds to player entering or leaving a hiding sanctuary, dynamically modulating vignette focus.
        /// </summary>
        public void HandleHidingStateChanged(bool isHiding)
        {
            float targetVignette = isHiding ? _hidingVignette : _baselineVignette;
            OnHidingVignetteFocus?.Invoke(targetVignette);
        }
    }
}
