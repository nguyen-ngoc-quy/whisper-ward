using System;
using UnityEngine;
using WhisperWard.Core.Contracts;
using WhisperWard.Core.Player;

namespace WhisperWard.Core.Audio
{
    /// <summary>
    /// Stance-aware Player Footstep Audio Controller conforming to GDD #13 and ADR-0003.
    /// Emits audible 3D footstep sound cues scaled by movement stance (Crouch / Walk / Run),
    /// with subtle pitch randomization (+/- 5%) to eliminate acoustic monotony.
    /// Zero managed heap allocations in hot-path.
    /// </summary>
    /// <example>
    /// <code>
    /// PlayerFootstepAudioController footstepAudio = playerObj.AddComponent<PlayerFootstepAudioController>();
    /// footstepAudio.Tick(deltaTime);
    /// </code>
    /// </example>
    public class PlayerFootstepAudioController : MonoBehaviour
    {
        [Header("Audio Clips")]
        [SerializeField] private AudioClip _walkClip;
        [SerializeField] private AudioClip _runClip;
        [SerializeField] private AudioClip _crouchClip;

        [Header("Stance Volume Scales")]
        [Range(0f, 1f)] [SerializeField] private float _crouchVolume = 0.25f;
        [Range(0f, 1f)] [SerializeField] private float _walkVolume = 0.65f;
        [Range(0f, 1f)] [SerializeField] private float _runVolume = 1.00f;

        [Header("Pitch Modulation")]
        [SerializeField] private float _basePitch = 1.0f;
        [SerializeField] private float _pitchVariance = 0.05f; // +/- 5%

        [Header("Stride Distances")]
        [SerializeField] private float _crouchStrideLength = 1.4f;
        [SerializeField] private float _walkStrideLength = 1.9f;
        [SerializeField] private float _runStrideLength = 2.6f;

        [Header("Speed Thresholds")]
        [SerializeField] private float _runSpeedThreshold = 3.50f;
        [SerializeField] private float _minMotionThreshold = 0.10f;

        [Header("Component References")]
        [SerializeField] private PlayerThirdPersonController _playerController;

        private float _strideAccumulator;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private int _stepCounter;

        /// <summary>Dispatched whenever a physical footstep audio cue is triggered.</summary>
        public event Action<MovementStance, float> OnFootstepAudioTriggered;

        public float CrouchVolume => _crouchVolume;
        public float WalkVolume => _walkVolume;
        public float RunVolume => _runVolume;
        public float StrideAccumulator => _strideAccumulator;

        public AudioClip WalkClip { get => _walkClip; set => _walkClip = value; }
        public AudioClip RunClip { get => _runClip; set => _runClip = value; }
        public AudioClip CrouchClip { get => _crouchClip; set => _crouchClip = value; }

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
            _strideAccumulator = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Frame-by-frame distance integration triggering footsteps at stance-specific stride lengths.
        /// Zero managed allocations.
        /// </summary>
        /// <param name="deltaTime">Delta time step.</param>
        public void Tick(float deltaTime)
        {
            Vector3 currentPos = transform.position;
            if (!_hasLastPosition)
            {
                _lastPosition = currentPos;
                _hasLastPosition = true;
                return;
            }

            float dx = currentPos.x - _lastPosition.x;
            float dz = currentPos.z - _lastPosition.z;
            float displacement = Mathf.Sqrt(dx * dx + dz * dz);
            _lastPosition = currentPos;

            // Determine effective movement speed & stance
            float speed = 0f;
            MovementStance stance = MovementStance.Standing;

            if (_playerController != null && _playerController.Fsm != null)
            {
                speed = _playerController.Fsm.EffectiveSpeed;
                stance = _playerController.Fsm.CurrentStance;
            }
            else if (deltaTime > 0.0001f)
            {
                speed = displacement / deltaTime;
            }

            if (speed < _minMotionThreshold)
            {
                _strideAccumulator = 0f;
                return;
            }

            float strideThreshold = GetStrideLengthForStance(stance, speed);
            _strideAccumulator += displacement;

            if (_strideAccumulator >= strideThreshold)
            {
                _strideAccumulator -= strideThreshold;
                TriggerFootstep(stance, speed, currentPos);
            }
        }

        /// <summary>
        /// Manually triggers a footstep audio cue for a given stance and position.
        /// </summary>
        public void TriggerFootstep(MovementStance stance, float speed, Vector3 position)
        {
            _stepCounter++;

            AudioClip clipToPlay = _walkClip;
            float volume = _walkVolume;

            if (stance == MovementStance.Crouched)
            {
                clipToPlay = _crouchClip != null ? _crouchClip : _walkClip;
                volume = _crouchVolume;
            }
            else if (speed > _runSpeedThreshold)
            {
                clipToPlay = _runClip != null ? _runClip : _walkClip;
                volume = _runVolume;
            }

            // Pseudo-random pitch alternation without system allocations: alternating slight up/down shifts
            float pitchOffset = ((_stepCounter % 2 == 0) ? 1.0f : -1.0f) * _pitchVariance;
            float effectivePitch = Mathf.Clamp(_basePitch + pitchOffset, 0.5f, 2.0f);

            if (AudioManager.Instance != null && clipToPlay != null)
            {
                AudioManager.Instance.PlaySFX3D(
                    clipToPlay,
                    position,
                    volume,
                    effectivePitch,
                    minDistance: 1.0f,
                    maxDistance: 15.0f,
                    priority: 128);
            }

            OnFootstepAudioTriggered?.Invoke(stance, volume);
        }

        /// <summary>
        /// Returns the required distance traveled to trigger a step based on current stance.
        /// </summary>
        public float GetStrideLengthForStance(MovementStance stance, float speed)
        {
            if (stance == MovementStance.Crouched)
            {
                return _crouchStrideLength;
            }

            return (speed > _runSpeedThreshold) ? _runStrideLength : _walkStrideLength;
        }

        /// <summary>
        /// Returns the playback volume associated with a movement stance.
        /// </summary>
        public float GetVolumeForStance(MovementStance stance, float speed)
        {
            if (stance == MovementStance.Crouched)
            {
                return _crouchVolume;
            }

            return (speed > _runSpeedThreshold) ? _runVolume : _walkVolume;
        }
    }
}
