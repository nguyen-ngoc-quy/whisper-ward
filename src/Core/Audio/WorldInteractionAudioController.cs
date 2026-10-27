using System;
using UnityEngine;
using WhisperWard.Core.Environment;
using WhisperWard.Core.Objectives;
using WhisperWard.Core.Player;

namespace WhisperWard.Core.Audio
{
    /// <summary>
    /// World interaction and environmental ambient audio controller conforming to GDD #13 and ADR-0003.
    /// Handles audio feedback for key objectives and world events:
    /// - Keycard pickup confirmation chime (Feedback 2D).
    /// - Security gate mechanical unlatch & sliding motor audio (SFX Diegetic 3D).
    /// - Burst noise projectile landing impact clang (SFX Diegetic 3D).
    /// - Continuous background ambient room tone (Ambience loop).
    /// Zero managed heap allocations in hot-path.
    /// </summary>
    /// <example>
    /// <code>
    /// var worldAudio = gameObject.AddComponent<WorldInteractionAudioController>();
    /// worldAudio.StartAmbienceLoop();
    /// </code>
    /// </example>
    public class WorldInteractionAudioController : MonoBehaviour
    {
        [Header("Audio Clips")]
        [SerializeField] private AudioClip _keycardPickupClip;
        [SerializeField] private AudioClip _gateUnlockClip;
        [SerializeField] private AudioClip _burstImpactClip;
        [SerializeField] private AudioClip _ambientLoopClip;

        [Header("Volume Settings")]
        [Range(0f, 1f)] [SerializeField] private float _keycardVolume = 0.90f;
        [Range(0f, 1f)] [SerializeField] private float _gateUnlockVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float _burstImpactVolume = 0.95f;
        [Range(0f, 1f)] [SerializeField] private float _ambientVolume = 0.40f;

        [Header("Auto-start Ambience")]
        [SerializeField] private bool _autoStartAmbience = true;

        public AudioClip KeycardPickupClip { get => _keycardPickupClip; set => _keycardPickupClip = value; }
        public AudioClip GateUnlockClip { get => _gateUnlockClip; set => _gateUnlockClip = value; }
        public AudioClip BurstImpactClip { get => _burstImpactClip; set => _burstImpactClip = value; }
        public AudioClip AmbientLoopClip { get => _ambientLoopClip; set => _ambientLoopClip = value; }

        public float KeycardVolume => _keycardVolume;
        public float GateUnlockVolume => _gateUnlockVolume;
        public float BurstImpactVolume => _burstImpactVolume;
        public float AmbientVolume => _ambientVolume;

        private void OnEnable()
        {
            ObjectiveKeycardPickup.OnAnyObjectiveAcquired += HandleObjectiveAcquired;
            LockedSecurityGate.OnAnyGateUnlocked += HandleGateUnlocked;
            BurstDistractionNoiseMaker.OnAnyBurstNoiseEmitted += HandleBurstNoiseEmitted;

            if (_autoStartAmbience && _ambientLoopClip != null)
            {
                StartAmbienceLoop();
            }
        }

        private void OnDisable()
        {
            ObjectiveKeycardPickup.OnAnyObjectiveAcquired -= HandleObjectiveAcquired;
            LockedSecurityGate.OnAnyGateUnlocked -= HandleGateUnlocked;
            BurstDistractionNoiseMaker.OnAnyBurstNoiseEmitted -= HandleBurstNoiseEmitted;
        }

        /// <summary>
        /// Starts the continuous ambient audio loop via the central AudioManager.
        /// </summary>
        public void StartAmbienceLoop()
        {
            if (AudioManager.Instance != null && _ambientLoopClip != null)
            {
                AudioManager.Instance.SetAmbience(_ambientLoopClip, _ambientVolume, loop: true);
            }
        }

        /// <summary>
        /// Handles objective keycard pickup events.
        /// </summary>
        public void HandleObjectiveAcquired(string objectiveId)
        {
            PlayKeycardPickupCue();
        }

        /// <summary>
        /// Handles security gate unlock events.
        /// </summary>
        public void HandleGateUnlocked()
        {
            PlayGateUnlockCue(transform.position);
        }

        /// <summary>
        /// Handles burst projectile landing impact events.
        /// </summary>
        public void HandleBurstNoiseEmitted(Vector3 impactPoint, float radius)
        {
            PlayBurstImpactCue(impactPoint);
        }

        /// <summary>Plays crisp non-diegetic pickup confirmation chime.</summary>
        public void PlayKeycardPickupCue()
        {
            if (AudioManager.Instance != null && _keycardPickupClip != null)
            {
                AudioManager.Instance.PlayFeedback(
                    _keycardPickupClip,
                    _keycardVolume,
                    pitch: 1.0f,
                    priority: 50);
            }
        }

        /// <summary>Plays 3D spatialized gate unlatching & sliding motor audio.</summary>
        public void PlayGateUnlockCue(Vector3 gatePosition)
        {
            if (AudioManager.Instance != null && _gateUnlockClip != null)
            {
                AudioManager.Instance.PlaySFX3D(
                    _gateUnlockClip,
                    gatePosition,
                    _gateUnlockVolume,
                    pitch: 1.0f,
                    minDistance: 2.0f,
                    maxDistance: 30.0f,
                    priority: 80);
            }
        }

        /// <summary>Plays 3D spatialized ceramic/metallic impact clang at projectile landing point.</summary>
        public void PlayBurstImpactCue(Vector3 impactPoint)
        {
            if (AudioManager.Instance != null && _burstImpactClip != null)
            {
                AudioManager.Instance.PlaySFX3D(
                    _burstImpactClip,
                    impactPoint,
                    _burstImpactVolume,
                    pitch: 1.0f,
                    minDistance: 3.0f,
                    maxDistance: 25.0f,
                    priority: 70);
            }
        }
    }
}
