using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Alert;
using WhisperWard.AI.FSM;
using WhisperWard.Core.Audio;

namespace WhisperWard.Tests.Unit.Audio
{
    [TestFixture]
    public class GuardAwarenessAudioTest
    {
        private GameObject _audioHost;
        private AudioManager _audioManager;
        private GameObject _guardHost;
        private GuardAwarenessAudioController _controller;

        private AudioClip _investigateClip;
        private AudioClip _chaseStingerClip;
        private AudioClip _radioBarkClip;
        private AudioClip _capturedClip;

        [SetUp]
        public void SetUp()
        {
            _audioHost = new GameObject("AudioManager_TestHost");
            _audioManager = _audioHost.AddComponent<AudioManager>();
            _audioManager.InitializePool(8);
            AudioManager.Instance = _audioManager;

            _guardHost = new GameObject("Guard_TestHost");
            _controller = _guardHost.AddComponent<GuardAwarenessAudioController>();

            _investigateClip = AudioClip.Create("InvestigateClip", 44100, 1, 44100, false);
            _chaseStingerClip = AudioClip.Create("ChaseStingerClip", 44100, 1, 44100, false);
            _radioBarkClip = AudioClip.Create("RadioBarkClip", 44100, 1, 44100, false);
            _capturedClip = AudioClip.Create("CapturedClip", 44100, 1, 44100, false);

            _controller.InvestigateClip = _investigateClip;
            _controller.ChaseStingerClip = _chaseStingerClip;
            _controller.RadioBarkClip = _radioBarkClip;
            _controller.CapturedClip = _capturedClip;
        }

        [TearDown]
        public void TearDown()
        {
            AudioManager.Instance = null;

            if (_investigateClip != null) Object.DestroyImmediate(_investigateClip);
            if (_chaseStingerClip != null) Object.DestroyImmediate(_chaseStingerClip);
            if (_radioBarkClip != null) Object.DestroyImmediate(_radioBarkClip);
            if (_capturedClip != null) Object.DestroyImmediate(_capturedClip);

            if (_guardHost != null) Object.DestroyImmediate(_guardHost);
            if (_audioHost != null) Object.DestroyImmediate(_audioHost);
        }

        [Test]
        public void test_guard_investigate_state_triggers_curiosity_sfx()
        {
            // Arrange
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);

            // Act: Transition from Patrol to Investigate
            _controller.HandleStateChanged(GuardState.Patrol, GuardState.Investigate);

            // Assert: Active voice started for curiosity sound
            Assert.AreEqual(1, _audioManager.ActiveVoiceCount);
        }

        [Test]
        public void test_guard_chase_state_triggers_alert_stinger_and_ducking()
        {
            // Arrange
            Assert.IsFalse(_audioManager.IsAmbienceDucked);

            // Act: Transition to Chase
            _controller.HandleStateChanged(GuardState.Patrol, GuardState.Chase);

            // Assert: Voice played and ambience ducking engaged
            Assert.AreEqual(1, _audioManager.ActiveVoiceCount);
            Assert.IsTrue(_audioManager.IsAmbienceDucked);

            // Act 2: Transition out of Chase to Investigate
            _controller.HandleStateChanged(GuardState.Chase, GuardState.Investigate);

            // Assert: Ambience ducking released
            Assert.IsFalse(_audioManager.IsAmbienceDucked);
        }

        [Test]
        public void test_radio_bark_alert_plays_spatial_audio_cue()
        {
            // Arrange
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);
            GuardAlertEvent alertEvent = new GuardAlertEvent(1, new Vector3(10f, 0f, 10f), 25f);

            // Act: Radio alert broadcast
            _controller.HandleAlertBroadcast(alertEvent);

            // Assert
            Assert.AreEqual(1, _audioManager.ActiveVoiceCount);
        }

        [Test]
        public void test_capture_state_triggers_defeat_audio_cue()
        {
            // Arrange
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);

            // Act: Transition to Captured
            _controller.HandleStateChanged(GuardState.Chase, GuardState.Captured);

            // Assert
            Assert.AreEqual(1, _audioManager.ActiveVoiceCount);
        }
    }
}
