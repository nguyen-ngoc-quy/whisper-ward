using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Audio;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Tests.Unit.Audio
{
    [TestFixture]
    public class AudioManagerTest
    {
        private GameObject _audioHost;
        private AudioManager _audioManager;
        private AudioClip _dummyClip;

        [SetUp]
        public void SetUp()
        {
            _audioHost = new GameObject("AudioManager_TestHost");
            _audioManager = _audioHost.AddComponent<AudioManager>();
            _audioManager.InitializePool(4); // Small pool for predictable test assertions

            // Create 1-second mono dummy audio clip for testing
            _dummyClip = AudioClip.Create("TestClip", 44100, 1, 44100, false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_dummyClip != null)
            {
                Object.DestroyImmediate(_dummyClip);
            }

            if (_audioHost != null)
            {
                Object.DestroyImmediate(_audioHost);
            }
        }

        [Test]
        public void test_audio_manager_routes_cues_to_specified_bus()
        {
            // Arrange
            _audioManager.MasterVolume = 1.0f;
            _audioManager.SFXVolume = 0.8f;
            _audioManager.FeedbackVolume = 0.5f;

            // Act
            float effectiveSFX = _audioManager.GetEffectiveVolume(AudioBusType.SFXDiegetic, 1.0f);
            float effectiveFeedback = _audioManager.GetEffectiveVolume(AudioBusType.FeedbackNonDiegetic, 1.0f);

            // Assert
            Assert.AreEqual(0.8f, effectiveSFX, 0.001f);
            Assert.AreEqual(0.5f, effectiveFeedback, 0.001f);
        }

        [Test]
        public void test_audio_manager_reuses_pooled_audio_sources_without_allocation()
        {
            // Arrange: Pool capacity is 4
            Assert.AreEqual(4, _audioManager.PoolCapacity);
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);

            // Act: Play 2 sounds
            int voice1 = _audioManager.PlaySFX2D(_dummyClip, 0.7f);
            int voice2 = _audioManager.PlayFeedback(_dummyClip, 0.9f);

            // Assert
            Assert.Greater(voice1, 0);
            Assert.Greater(voice2, 0);
            Assert.AreNotEqual(voice1, voice2);
            Assert.AreEqual(2, _audioManager.ActiveVoiceCount);

            // Stop voice1 and check release
            _audioManager.StopVoice(voice1);
            Assert.AreEqual(1, _audioManager.ActiveVoiceCount);
        }

        [Test]
        public void test_audio_manager_voice_stealing_respects_playback_priority()
        {
            // Arrange: Fill all 4 slots with low priority (200)
            int v1 = _audioManager.PlaySFX2D(_dummyClip, 1f, 1f, priority: 200);
            int v2 = _audioManager.PlaySFX2D(_dummyClip, 1f, 1f, priority: 200);
            int v3 = _audioManager.PlaySFX2D(_dummyClip, 1f, 1f, priority: 200);
            int v4 = _audioManager.PlaySFX2D(_dummyClip, 1f, 1f, priority: 200);

            Assert.AreEqual(4, _audioManager.ActiveVoiceCount);

            // Act: Play high-priority sound (priority 50, higher than 200)
            int v5 = _audioManager.PlayFeedback(_dummyClip, 1f, 1f, priority: 50);

            // Assert: v5 should successfully steal a slot
            Assert.Greater(v5, 0);
            Assert.AreEqual(4, _audioManager.ActiveVoiceCount);
        }

        [Test]
        public void test_audio_manager_stop_all_clears_active_voices_cleanly()
        {
            // Arrange: Play multiple sounds
            _audioManager.PlaySFX2D(_dummyClip);
            _audioManager.PlayFeedback(_dummyClip);
            _audioManager.SetAmbience(_dummyClip, 0.6f);
            Assert.AreEqual(2, _audioManager.ActiveVoiceCount);

            // Act
            _audioManager.StopAll();

            // Assert: All voices recycled, active count is 0
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);
            Assert.IsFalse(_audioManager.IsAmbienceDucked);
        }

        [Test]
        public void test_audio_manager_null_clip_returns_negative_id()
        {
            // Act
            int voiceId = _audioManager.PlaySFX2D(null);

            // Assert
            Assert.AreEqual(-1, voiceId);
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);
        }

        [Test]
        public void test_audio_manager_ambience_ducking_attenuates_volume()
        {
            // Arrange
            _audioManager.AmbienceVolume = 1.0f;
            float normalVolume = _audioManager.GetEffectiveVolume(AudioBusType.Ambience, 1.0f);

            // Act
            _audioManager.SetAmbienceDucking(true);
            float duckedVolume = _audioManager.GetEffectiveVolume(AudioBusType.Ambience, 1.0f);

            // Assert: Volume is attenuated by ~6 dB (~50%)
            Assert.IsTrue(_audioManager.IsAmbienceDucked);
            Assert.AreEqual(1.0f, normalVolume, 0.001f);
            Assert.Less(duckedVolume, normalVolume);
            Assert.AreEqual(0.5012f, duckedVolume, 0.01f);
        }

        [Test]
        public void test_audio_manager_tick_recycles_expired_voices()
        {
            // Arrange: Play voice starting at virtual time 10.0s
            _audioManager.PlaySFX2D(_dummyClip, 1f, 1f);
            Assert.AreEqual(1, _audioManager.ActiveVoiceCount);

            // Act: Advance time beyond clip duration (clip is 1.0s long)
            _audioManager.Tick(100.0f);

            // Assert
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);
        }
    }
}
