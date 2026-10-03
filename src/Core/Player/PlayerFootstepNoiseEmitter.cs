using System;
using UnityEngine;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Core.Player
{
    /// <summary>
    /// Stance-aware Player Footstep Noise Emitter.
    /// Emits discrete noise events based on player locomotion speed, stance, and stride rhythm.
    /// Crouch ($V \le 1.8\text{ m/s}$): $R = 0.0\text{ m}$ (Completely silent).
    /// Walk ($V \in (1.8, 3.5]\text{ m/s}$): $R = 4.0\text{ m}$, stride length $L = 1.9\text{ m}$ ($\approx 0.45\text{ s}$).
    /// Run ($V > 3.5\text{ m/s}$): $R = 6.0\text{ m}$, stride length $L = 2.6\text{ m}$ ($\approx 0.30\text{ s}$).
    /// Stationary player ($V \le 0.1\text{ m/s}$): $R = 0.0\text{ m}$.
    /// Conforms to GDD Player Noise, ADR-0001, and Story NOISE-01 (AC-NOISE-01).
    /// </summary>
    public sealed class PlayerFootstepNoiseEmitter : MonoBehaviour
    {
        [Header("Footstep Noise Tuning")]
        [Tooltip("Walk noise radius in meters (GDD: 4.0m).")]
        [SerializeField] private float _walkRadius = 4.0f;

        [Tooltip("Run noise radius in meters (GDD: 6.0m).")]
        [SerializeField] private float _runRadius = 6.0f;

        [Tooltip("Walk stride distance in meters (GDD: 1.9m).")]
        [SerializeField] private float _walkStrideLength = 1.9f;

        [Tooltip("Run stride distance in meters (GDD: 2.6m).")]
        [SerializeField] private float _runStrideLength = 2.6f;

        [Tooltip("Vertical noise origin offset above player feet in meters (GDD F12: 0.25m).")]
        [SerializeField] private float _noiseOriginOffset = 0.25f;

        [Tooltip("Speed threshold differentiating Walk and Run in m/s (GDD: 3.50 m/s).")]
        [SerializeField] private float _runSpeedThreshold = 3.50f;

        [Header("Component References")]
        [SerializeField] private PlayerThirdPersonController _playerController;

        private float _strideDistanceAccumulator;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;

        /// <summary>
        /// Global event broadcast for any active hearing listener in the level.
        /// (noiseOrigin, noiseRadius)
        /// Zero managed heap allocations.
        /// </summary>
        public static event Action<Vector3, float> OnAnyFootstepNoiseEmitted;

        /// <summary>
        /// Instance-level event broadcast for this specific emitter.
        /// </summary>
        public event Action<Vector3, float> OnFootstepNoiseEmitted;

        public float WalkRadius => _walkRadius;
        public float RunRadius => _runRadius;
        public float WalkStrideLength => _walkStrideLength;
        public float RunStrideLength => _runStrideLength;
        public float NoiseOriginOffset => _noiseOriginOffset;
        public float StrideDistanceAccumulator => _strideDistanceAccumulator;

        private void Awake()
        {
            if (_playerController == null)
            {
                _playerController = GetComponent<PlayerThirdPersonController>();
            }
        }

        private void OnEnable()
        {
            _hasLastPosition = false;
            _strideDistanceAccumulator = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Advances locomotion distance integration and stride emission.
        /// Zero heap allocations.
        /// </summary>
        public void Tick(float deltaTime)
        {
            Vector3 currentPos = transform.position;
            if (!_hasLastPosition)
            {
                _lastPosition = currentPos;
                _hasLastPosition = true;
                return;
            }

            // Calculate horizontal planar displacement
            float dx = currentPos.x - _lastPosition.x;
            float dz = currentPos.z - _lastPosition.z;
            float displacement = Mathf.Sqrt(dx * dx + dz * dz);
            _lastPosition = currentPos;

            // Determine effective speed and stance
            float speed = 0f;
            bool isCrouched = false;

            if (_playerController != null && _playerController.Fsm != null)
            {
                speed = _playerController.Fsm.EffectiveSpeed;
                isCrouched = _playerController.Fsm.CurrentStance == MovementStance.Crouch;
            }
            else if (deltaTime > 0.0001f)
            {
                speed = displacement / deltaTime;
            }

            // Evaluate noise radius for current movement state (AC-NOISE-01)
            float nominalRadius = CalculateNominalNoiseRadius(speed, isCrouched);
            float strideThreshold = (speed > _runSpeedThreshold) ? _runStrideLength : _walkStrideLength;

            if (nominalRadius <= 0f)
            {
                // Silent state (Crouch or Stationary) -> freeze or clear accumulator
                if (speed <= 0.1f)
                {
                    _strideDistanceAccumulator = 0f;
                }
                return;
            }

            // Accumulate audible displacement
            _strideDistanceAccumulator += displacement;

            if (_strideDistanceAccumulator >= strideThreshold)
            {
                // Stride threshold committed! Retain remainder
                _strideDistanceAccumulator -= strideThreshold;

                EmitNoise(currentPos, nominalRadius);
            }
        }

        /// <summary>
        /// Calculates the nominal noise radius based on current planar speed and stance.
        /// Crouch: 0m
        /// Walk: 4.0m
        /// Run: 6.0m
        /// Stationary: 0m
        /// </summary>
        public float CalculateNominalNoiseRadius(float planarSpeed, bool isCrouched)
        {
            if (isCrouched || planarSpeed <= 0.1f)
            {
                return 0.0f;
            }

            if (planarSpeed > _runSpeedThreshold)
            {
                return _runRadius; // 6.0m
            }

            return _walkRadius; // 4.0m
        }

        /// <summary>
        /// Manually triggers a footstep noise emission at specified position and radius.
        /// Useful for headless unit testing and animation events.
        /// </summary>
        public void EmitNoise(Vector3 feetPosition, float radius)
        {
            if (radius <= 0f) return;

            Vector3 noiseOrigin = feetPosition + Vector3.up * _noiseOriginOffset;
            OnFootstepNoiseEmitted?.Invoke(noiseOrigin, radius);
            OnAnyFootstepNoiseEmitted?.Invoke(noiseOrigin, radius);
        }

        /// <summary>
        /// Explicit dependency injection and configuration for unit testing.
        /// </summary>
        public void Configure(
            PlayerThirdPersonController controller,
            float walkRadius = 4.0f,
            float runRadius = 6.0f,
            float walkStride = 1.9f,
            float runStride = 2.6f)
        {
            _playerController = controller;
            _walkRadius = Mathf.Max(0.1f, walkRadius);
            _runRadius = Mathf.Max(0.1f, runRadius);
            _walkStrideLength = Mathf.Max(0.1f, walkStride);
            _runStrideLength = Mathf.Max(0.1f, runStride);
            _strideDistanceAccumulator = 0f;
            _hasLastPosition = false;
        }
    }
}
