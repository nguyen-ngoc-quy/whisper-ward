using System;
using UnityEngine;
using WhisperWard.AI.Alert;
using WhisperWard.AI.FSM;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Core.Audio
{
    /// <summary>
    /// Guard awareness audio presentation controller conforming to GDD #13 and ADR-0003.
    /// Drives audio cues for AI suspicion escalation:
    /// - Investigate state: Diegetic curiosity murmur / alert sound at guard position.
    /// - Chase state: High-priority non-diegetic alert stinger + ambient audio ducking (-6 dB).
    /// - Radio Bark: 3D spatialized radio squelch / transmission bark.
    /// - Captured: Final defeat arrest stinger.
    /// Zero managed heap allocations in hot-path.
    /// </summary>
    /// <example>
    /// <code>
    /// var audioController = guardObj.AddComponent<GuardAwarenessAudioController>();
    /// audioController.Configure(fsm, transceiver);
    /// </code>
    /// </example>
    public class GuardAwarenessAudioController : MonoBehaviour
    {
        [Header("Audio Clips")]
        [SerializeField] private AudioClip _investigateClip;
        [SerializeField] private AudioClip _chaseStingerClip;
        [SerializeField] private AudioClip _radioBarkClip;
        [SerializeField] private AudioClip _capturedClip;

        [Header("Volume Tuning")]
        [Range(0f, 1f)] [SerializeField] private float _investigateVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float _chaseStingerVolume = 1.00f;
        [Range(0f, 1f)] [SerializeField] private float _radioBarkVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float _capturedVolume = 1.00f;

        [Header("Component References")]
        [SerializeField] private GuardFSMRuntimeController _fsm;
        [SerializeField] private GuardRadioAlertTransceiver _transceiver;

        public AudioClip InvestigateClip { get => _investigateClip; set => _investigateClip = value; }
        public AudioClip ChaseStingerClip { get => _chaseStingerClip; set => _chaseStingerClip = value; }
        public AudioClip RadioBarkClip { get => _radioBarkClip; set => _radioBarkClip = value; }
        public AudioClip CapturedClip { get => _capturedClip; set => _capturedClip = value; }

        public float InvestigateVolume => _investigateVolume;
        public float ChaseStingerVolume => _chaseStingerVolume;
        public float RadioBarkVolume => _radioBarkVolume;
        public float CapturedVolume => _capturedVolume;

        private void Awake()
        {
            if (_fsm == null)
            {
                _fsm = GetComponent<GuardFSMRuntimeController>();
            }

            if (_transceiver == null)
            {
                _transceiver = GetComponent<GuardRadioAlertTransceiver>();
            }
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        /// <summary>
        /// Explicitly binds component dependencies.
        /// </summary>
        public void Configure(GuardFSMRuntimeController fsm, GuardRadioAlertTransceiver transceiver = null)
        {
            UnsubscribeEvents();
            _fsm = fsm;
            _transceiver = transceiver;
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (_fsm != null)
            {
                _fsm.OnStateChanged += HandleStateChanged;
            }

            if (_transceiver != null)
            {
                _transceiver.OnAlertBroadcast += HandleAlertBroadcast;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_fsm != null)
            {
                _fsm.OnStateChanged -= HandleStateChanged;
            }

            if (_transceiver != null)
            {
                _transceiver.OnAlertBroadcast -= HandleAlertBroadcast;
            }
        }

        /// <summary>
        /// Handles guard state transitions and triggers corresponding audio feedback.
        /// </summary>
        public void HandleStateChanged(GuardState oldState, GuardState newState)
        {
            if (oldState == newState) return;

            // 1. Escalation to Investigate
            if (newState == GuardState.Investigate && oldState == GuardState.Patrol)
            {
                PlayInvestigateCue();
            }
            // 2. Escalation to Chase
            else if (newState == GuardState.Chase)
            {
                PlayChaseStinger();
            }
            // 3. Capture decisive termination
            else if (newState == GuardState.Captured)
            {
                PlayCapturedCue();
            }

            // Ambience ducking control: Engage ducking while in Chase; release when leaving Chase
            if (oldState == GuardState.Chase && newState != GuardState.Chase)
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetAmbienceDucking(false);
                }
            }
        }

        /// <summary>
        /// Handles outgoing radio alert bark transmissions.
        /// </summary>
        public void HandleAlertBroadcast(GuardAlertEvent alertEvent)
        {
            PlayRadioBarkCue(alertEvent.AlertOrigin);
        }

        /// <summary>Plays diegetic curiosity audio at the guard's position.</summary>
        public void PlayInvestigateCue()
        {
            if (AudioManager.Instance != null && _investigateClip != null)
            {
                AudioManager.Instance.PlaySFX3D(
                    _investigateClip,
                    transform.position,
                    _investigateVolume,
                    pitch: 1.0f,
                    minDistance: 2.0f,
                    maxDistance: 20.0f,
                    priority: 120);
            }
        }

        /// <summary>Plays dramatic non-diegetic alert stinger and ducks background audio.</summary>
        public void PlayChaseStinger()
        {
            if (AudioManager.Instance != null)
            {
                if (_chaseStingerClip != null)
                {
                    AudioManager.Instance.PlayFeedback(
                        _chaseStingerClip,
                        _chaseStingerVolume,
                        pitch: 1.0f,
                        priority: 30); // High priority
                }

                AudioManager.Instance.SetAmbienceDucking(true);
            }
        }

        /// <summary>Plays spatialized radio squelch sound at the transmitter position.</summary>
        public void PlayRadioBarkCue(Vector3 origin)
        {
            if (AudioManager.Instance != null && _radioBarkClip != null)
            {
                AudioManager.Instance.PlaySFX3D(
                    _radioBarkClip,
                    origin,
                    _radioBarkVolume,
                    pitch: 1.0f,
                    minDistance: 3.0f,
                    maxDistance: 25.0f,
                    priority: 60);
            }
        }

        /// <summary>Plays non-diegetic capture arrest stinger.</summary>
        public void PlayCapturedCue()
        {
            if (AudioManager.Instance != null && _capturedClip != null)
            {
                AudioManager.Instance.PlayFeedback(
                    _capturedClip,
                    _capturedVolume,
                    pitch: 1.0f,
                    priority: 10); // Highest priority
            }
        }
    }
}
