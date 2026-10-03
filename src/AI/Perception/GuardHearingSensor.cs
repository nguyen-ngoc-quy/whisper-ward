using System;
using UnityEngine;
using WhisperWard.AI.FSM;
using WhisperWard.Core.Player;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.AI.Perception
{
    /// <summary>
    /// Guard Hearing Perception Sensor component.
    /// Evaluates incoming footstep noise events against planar distance ($d_{XZ} \le R_{eff}$),
    /// vertical soft-falloff ($Y_{hard\_cutoff} = 4.0\text{ m}$), and Layer 20 World Linecast occlusion.
    /// When unalerted/patrolling, a heard noise triggers state transition to Investigate at noise origin.
    /// Conforms to GDD Player Noise, GDD Perception (R5, F12), and Story NOISE-01 (AC-NOISE-02..04).
    /// </summary>
    public sealed class GuardHearingSensor : MonoBehaviour
    {
        [Header("Hearing Parameters")]
        [Tooltip("Guard eye height offset above feet in meters (GDD: 1.60m).")]
        [SerializeField] private float _eyeHeight = 1.60f;

        [Tooltip("Vertical hard cutoff distance in meters where sound attenuates to 0 (GDD: 4.0m).")]
        [SerializeField] private float _verticalHardCutoff = 4.0f;

        [Tooltip("Layer mask for acoustic obstacle occlusion linecasts (Default: Layer 20 World).")]
        [SerializeField] private LayerMask _occlusionMask = 1 << 20;

        [Header("Component References")]
        [SerializeField] private GuardFSMRuntimeController _fsm;

        private IPhysicsQueryService _physicsQuery;

        /// <summary>
        /// Fires when guard successfully hears a noise event.
        /// (noiseOrigin, nominalRadius)
        /// </summary>
        public event Action<Vector3, float> OnNoiseHeard;

        public float EyeHeight => _eyeHeight;
        public float VerticalHardCutoff => _verticalHardCutoff;
        public LayerMask OcclusionMask => _occlusionMask;

        private void Awake()
        {
            if (_fsm == null)
            {
                _fsm = GetComponent<GuardFSMRuntimeController>();
            }
        }

        private void OnEnable()
        {
            PlayerFootstepNoiseEmitter.OnAnyFootstepNoiseEmitted += HandleFootstepNoise;
        }

        private void OnDisable()
        {
            PlayerFootstepNoiseEmitter.OnAnyFootstepNoiseEmitted -= HandleFootstepNoise;
        }

        /// <summary>
        /// Callback receiver for global footstep noise broadcasts.
        /// Zero managed allocations.
        /// </summary>
        public void HandleFootstepNoise(Vector3 noiseOrigin, float nominalRadius)
        {
            if (_fsm == null) return;

            // Unalerted/patrolling guard reacts to fresh footstep noise (AC-NOISE-04)
            // (If already in Chase or Captured, sight or pursuit takes precedence)
            if (_fsm.CurrentState != GuardFSMRuntimeController.GuardState.Patrol)
            {
                return;
            }

            if (EvaluateHearing(transform.position, noiseOrigin, nominalRadius, out bool withinRadius, out bool occluded))
            {
                // Noise detected and verified! Transition to Investigate
                _fsm.TriggerInvestigate(noiseOrigin, "footstep_noise");
                OnNoiseHeard?.Invoke(noiseOrigin, nominalRadius);
            }
        }

        /// <summary>
        /// Evaluates whether a sound emitted at noiseOrigin reaches the guard.
        /// Computes planar horizontal distance $d_{XZ}$, applies vertical falloff,
        /// and conducts a Linecast from noiseOrigin to guardEye against Layer 20 World.
        /// Zero managed allocations.
        /// </summary>
        public bool EvaluateHearing(
            Vector3 guardFeetPosition,
            Vector3 noiseOrigin,
            float nominalRadius,
            out bool withinRadius,
            out bool occluded)
        {
            if (nominalRadius <= 0.001f)
            {
                withinRadius = false;
                occluded = false;
                return false;
            }

            // 1. Planar horizontal distance evaluation (AC-NOISE-02)
            float dx = noiseOrigin.x - guardFeetPosition.x;
            float dz = noiseOrigin.z - guardFeetPosition.z;
            float planarDistSq = dx * dx + dz * dz;

            // 2. Vertical falloff evaluation
            float deltaY = Mathf.Abs(noiseOrigin.y - guardFeetPosition.y);
            if (deltaY >= _verticalHardCutoff)
            {
                withinRadius = false;
                occluded = false;
                return false;
            }

            float verticalFactor = 1.0f - (deltaY / _verticalHardCutoff);
            float effectiveRadius = nominalRadius * verticalFactor;
            float effectiveRadiusSq = effectiveRadius * effectiveRadius;

            if (planarDistSq > effectiveRadiusSq)
            {
                withinRadius = false;
                occluded = false;
                return false;
            }

            withinRadius = true;

            // 3. Occlusion Linecast between noise origin and guard eye (AC-NOISE-03)
            Vector3 guardEyePosition = guardFeetPosition + Vector3.up * _eyeHeight;

            if (_physicsQuery != null)
            {
                occluded = _physicsQuery.CheckOcclusionLine(noiseOrigin, guardEyePosition, _occlusionMask, out _);
            }
            else
            {
                occluded = UnityEngine.Physics.Linecast(noiseOrigin, guardEyePosition, _occlusionMask, QueryTriggerInteraction.Ignore);
            }

            return !occluded;
        }

        /// <summary>
        /// Explicit dependency injection and configuration for runtime and testing.
        /// </summary>
        public void Configure(
            GuardFSMRuntimeController fsm,
            IPhysicsQueryService physicsQuery = null,
            float eyeHeight = 1.60f,
            float verticalHardCutoff = 4.0f,
            int occlusionMask = 1 << 20)
        {
            _fsm = fsm;
            _physicsQuery = physicsQuery;
            _eyeHeight = Mathf.Max(0.1f, eyeHeight);
            _verticalHardCutoff = Mathf.Max(0.5f, verticalHardCutoff);
            _occlusionMask = occlusionMask;
        }
    }
}
