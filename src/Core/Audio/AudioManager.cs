using System;
using UnityEngine;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Core.Audio
{
    /// <summary>
    /// Core audio management system providing 4-bus hierarchical volume routing,
    /// zero-allocation voice pooling, dynamic ambient ducking, and deterministic voice cleanup.
    /// Conforms to ADR-0003 and GDD #13 Audio & UI Feedback specifications.
    /// </summary>
    /// <example>
    /// <code>
    /// AudioManager audio = AudioManager.Instance;
    /// int voiceId = audio.PlaySFX3D(footstepClip, transform.position, volume: 0.8f);
    /// audio.SetAmbienceDucking(true);
    /// </code>
    /// </example>
    public class AudioManager : MonoBehaviour, IAudioService
    {
        private const int DefaultPoolCapacity = 16;
        private const float DefaultDuckFactor = 0.5012f; // -6 dB attenuation (10^(-6/20))

        /// <summary>
        /// Internal struct tracking state for each pre-allocated voice slot.
        /// </summary>
        [Serializable]
        public struct VoiceSlot
        {
            public AudioSource Source;
            public bool InUse;
            public AudioBusType Bus;
            public float BaseVolume;
            public float StartTime;
            public float Duration;
            public int Priority;
            public int VoiceId;
        }

        private static IAudioService _instance;

        [Header("Master & Bus Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float _feedbackVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float _ambienceVolume = 0.8f;

        [Header("Pool Configuration")]
        [SerializeField] private int _poolCapacity = DefaultPoolCapacity;
        [SerializeField] private float _duckFactor = DefaultDuckFactor;

        [Header("Dedicated Ambience")]
        [SerializeField] private AudioSource _ambienceSource;

        private VoiceSlot[] _voicePool;
        private int _nextVoiceId = 1;
        private bool _isAmbienceDucked;
        private float _ambienceBaseVolume = 1.0f;

        /// <summary>Static accessor for the active audio service instance.</summary>
        public static IAudioService Instance
        {
            get => _instance;
            set => _instance = value;
        }

        /// <inheritdoc />
        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                ApplyBusVolumes();
            }
        }

        /// <inheritdoc />
        public float SFXVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                ApplyBusVolumes();
            }
        }

        /// <inheritdoc />
        public float FeedbackVolume
        {
            get => _feedbackVolume;
            set
            {
                _feedbackVolume = Mathf.Clamp01(value);
                ApplyBusVolumes();
            }
        }

        /// <inheritdoc />
        public float AmbienceVolume
        {
            get => _ambienceVolume;
            set
            {
                _ambienceVolume = Mathf.Clamp01(value);
                UpdateAmbienceVolume();
            }
        }

        /// <inheritdoc />
        public bool IsAmbienceDucked => _isAmbienceDucked;

        /// <inheritdoc />
        public int ActiveVoiceCount
        {
            get
            {
                if (_voicePool == null) return 0;
                int count = 0;
                for (int i = 0; i < _voicePool.Length; i++)
                {
                    if (_voicePool[i].InUse) count++;
                }
                return count;
            }
        }

        /// <inheritdoc />
        public int PoolCapacity => _voicePool != null ? _voicePool.Length : _poolCapacity;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }

            InitializePool(_poolCapacity);
        }

        private void OnDestroy()
        {
            if (_instance == (IAudioService)this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Explicitly initializes or resizes the pre-allocated audio source voice pool.
        /// </summary>
        /// <param name="capacity">Number of pre-allocated voice channels.</param>
        public void InitializePool(int capacity)
        {
            if (capacity <= 0) capacity = DefaultPoolCapacity;
            _poolCapacity = capacity;

            if (_voicePool != null && _voicePool.Length == capacity)
            {
                return;
            }

            _voicePool = new VoiceSlot[capacity];
            for (int i = 0; i < capacity; i++)
            {
                GameObject voiceObj = new GameObject($"Voice_{i:00}");
                voiceObj.transform.SetParent(transform, false);
                AudioSource source = voiceObj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;

                _voicePool[i] = new VoiceSlot
                {
                    Source = source,
                    InUse = false,
                    Bus = AudioBusType.SFXDiegetic,
                    BaseVolume = 1.0f,
                    StartTime = 0f,
                    Duration = 0f,
                    Priority = 128,
                    VoiceId = 0
                };
            }

            if (_ambienceSource == null)
            {
                GameObject ambienceObj = new GameObject("Voice_Ambience");
                ambienceObj.transform.SetParent(transform, false);
                _ambienceSource = ambienceObj.AddComponent<AudioSource>();
                _ambienceSource.playOnAwake = false;
                _ambienceSource.loop = true;
                _ambienceSource.spatialBlend = 0f;
            }
        }

        /// <summary>
        /// Frame update polling voice states to recycle finished slots.
        /// </summary>
        private void Update()
        {
            Tick(Time.time);
        }

        /// <summary>
        /// Advances voice simulation time, recycling inactive sources. Zero allocations.
        /// </summary>
        /// <param name="currentTime">The current game or virtual timestamp in seconds.</param>
        public void Tick(float currentTime)
        {
            if (_voicePool == null) return;

            for (int i = 0; i < _voicePool.Length; i++)
            {
                if (!_voicePool[i].InUse) continue;

                AudioSource src = _voicePool[i].Source;
                bool isPlaying = src != null && src.isPlaying;
                bool durationExpired = _voicePool[i].Duration > 0f &&
                                       (currentTime >= _voicePool[i].StartTime + _voicePool[i].Duration);

                if (!isPlaying || durationExpired)
                {
                    RecycleSlot(i);
                }
            }
        }

        /// <inheritdoc />
        public int PlaySFX2D(AudioClip clip, float volume = 1.0f, float pitch = 1.0f, int priority = 128)
        {
            if (clip == null) return -1;

            int slotIndex = AcquireSlot(priority);
            if (slotIndex < 0) return -1;

            ConfigureAndPlaySlot(slotIndex, clip, Vector3.zero, 0f, volume, pitch, 1f, 25f, priority, AudioBusType.SFXDiegetic);
            return _voicePool[slotIndex].VoiceId;
        }

        /// <inheritdoc />
        public int PlaySFX3D(AudioClip clip, Vector3 position, float volume = 1.0f, float pitch = 1.0f, float minDistance = 1.0f, float maxDistance = 25.0f, int priority = 128)
        {
            if (clip == null) return -1;

            int slotIndex = AcquireSlot(priority);
            if (slotIndex < 0) return -1;

            ConfigureAndPlaySlot(slotIndex, clip, position, 1f, volume, pitch, minDistance, maxDistance, priority, AudioBusType.SFXDiegetic);
            return _voicePool[slotIndex].VoiceId;
        }

        /// <inheritdoc />
        public int PlayFeedback(AudioClip clip, float volume = 1.0f, float pitch = 1.0f, int priority = 128)
        {
            if (clip == null) return -1;

            int slotIndex = AcquireSlot(priority);
            if (slotIndex < 0) return -1;

            ConfigureAndPlaySlot(slotIndex, clip, Vector3.zero, 0f, volume, pitch, 1f, 25f, priority, AudioBusType.FeedbackNonDiegetic);
            return _voicePool[slotIndex].VoiceId;
        }

        /// <inheritdoc />
        public void SetAmbience(AudioClip clip, float volume = 1.0f, bool loop = true)
        {
            if (_ambienceSource == null) return;

            _ambienceBaseVolume = Mathf.Clamp01(volume);
            _ambienceSource.clip = clip;
            _ambienceSource.loop = loop;
            UpdateAmbienceVolume();

            if (clip != null)
            {
                try
                {
                    _ambienceSource.Play();
                }
                catch
                {
                    // Safe fallback for headless test environments without active audio hardware
                }
            }
            else
            {
                _ambienceSource.Stop();
            }
        }

        /// <inheritdoc />
        public void SetAmbienceDucking(bool isDucked)
        {
            if (_isAmbienceDucked == isDucked) return;

            _isAmbienceDucked = isDucked;
            UpdateAmbienceVolume();
        }

        /// <inheritdoc />
        public void StopVoice(int voiceId)
        {
            if (_voicePool == null || voiceId <= 0) return;

            for (int i = 0; i < _voicePool.Length; i++)
            {
                if (_voicePool[i].InUse && _voicePool[i].VoiceId == voiceId)
                {
                    RecycleSlot(i);
                    break;
                }
            }
        }

        /// <inheritdoc />
        public void StopAll()
        {
            if (_voicePool != null)
            {
                for (int i = 0; i < _voicePool.Length; i++)
                {
                    if (_voicePool[i].InUse)
                    {
                        RecycleSlot(i);
                    }
                }
            }

            if (_ambienceSource != null)
            {
                _ambienceSource.Stop();
            }

            _isAmbienceDucked = false;
        }

        /// <summary>
        /// Computes the final audible volume for a given bus type.
        /// </summary>
        public float GetEffectiveVolume(AudioBusType bus, float baseVolume)
        {
            float busMult = bus switch
            {
                AudioBusType.SFXDiegetic => _sfxVolume,
                AudioBusType.FeedbackNonDiegetic => _feedbackVolume,
                AudioBusType.Ambience => _ambienceVolume * (_isAmbienceDucked ? _duckFactor : 1.0f),
                _ => 1.0f
            };

            return Mathf.Clamp01(baseVolume * busMult * _masterVolume);
        }

        private int AcquireSlot(int priority)
        {
            if (_voicePool == null || _voicePool.Length == 0) return -1;

            // 1. Look for free slot
            for (int i = 0; i < _voicePool.Length; i++)
            {
                if (!_voicePool[i].InUse)
                {
                    return i;
                }
            }

            // 2. Pool full: Voice Stealing
            // In Unity audio: 0 is highest priority, 256 is lowest.
            int stealCandidate = -1;
            int lowestPriorityFound = -1;
            float oldestStartTime = float.MaxValue;

            for (int i = 0; i < _voicePool.Length; i++)
            {
                int currentPrio = _voicePool[i].Priority;

                // Priority stealing candidate check (lower priority = larger numerical value)
                if (currentPrio > lowestPriorityFound ||
                    (currentPrio == lowestPriorityFound && _voicePool[i].StartTime < oldestStartTime))
                {
                    lowestPriorityFound = currentPrio;
                    oldestStartTime = _voicePool[i].StartTime;
                    stealCandidate = i;
                }
            }

            // Can steal if incoming sound has equal or higher priority (numerical value <= lowestPriorityFound)
            if (stealCandidate >= 0 && priority <= lowestPriorityFound)
            {
                RecycleSlot(stealCandidate);
                return stealCandidate;
            }

            return -1;
        }

        private void ConfigureAndPlaySlot(int slotIndex, AudioClip clip, Vector3 position, float spatialBlend,
            float volume, float pitch, float minDistance, float maxDistance, int priority, AudioBusType bus)
        {
            AudioSource src = _voicePool[slotIndex].Source;
            if (src != null)
            {
                src.clip = clip;
                src.spatialBlend = spatialBlend;
                src.pitch = pitch;
                src.minDistance = minDistance;
                src.maxDistance = maxDistance;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.priority = priority;
                src.volume = GetEffectiveVolume(bus, volume);

                if (spatialBlend > 0f)
                {
                    src.transform.position = position;
                }

                try
                {
                    src.Play();
                }
                catch
                {
                    // Safe fallback for headless test execution
                }
            }

            int assignedId = _nextVoiceId++;
            if (_nextVoiceId <= 0) _nextVoiceId = 1;

            float duration = clip != null ? (clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch))) : 0f;

            _voicePool[slotIndex].InUse = true;
            _voicePool[slotIndex].Bus = bus;
            _voicePool[slotIndex].BaseVolume = volume;
            _voicePool[slotIndex].StartTime = Time.time;
            _voicePool[slotIndex].Duration = duration;
            _voicePool[slotIndex].Priority = priority;
            _voicePool[slotIndex].VoiceId = assignedId;
        }

        private void RecycleSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _voicePool.Length) return;

            if (_voicePool[slotIndex].Source != null)
            {
                try
                {
                    _voicePool[slotIndex].Source.Stop();
                    _voicePool[slotIndex].Source.clip = null;
                }
                catch
                {
                    // Ignore headless stop errors
                }
            }

            _voicePool[slotIndex].InUse = false;
            _voicePool[slotIndex].VoiceId = 0;
            _voicePool[slotIndex].Duration = 0f;
            _voicePool[slotIndex].BaseVolume = 1.0f;
        }

        private void ApplyBusVolumes()
        {
            if (_voicePool == null) return;

            for (int i = 0; i < _voicePool.Length; i++)
            {
                if (!_voicePool[i].InUse || _voicePool[i].Source == null) continue;

                _voicePool[i].Source.volume = GetEffectiveVolume(_voicePool[i].Bus, _voicePool[i].BaseVolume);
            }

            UpdateAmbienceVolume();
        }

        private void UpdateAmbienceVolume()
        {
            if (_ambienceSource == null) return;

            float effective = GetEffectiveVolume(AudioBusType.Ambience, _ambienceBaseVolume);
            _ambienceSource.volume = effective;
        }
    }
}
