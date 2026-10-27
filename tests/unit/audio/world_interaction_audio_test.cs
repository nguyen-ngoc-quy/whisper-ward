using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Audio;

namespace WhisperWard.Tests.Unit.Audio
{
    [TestFixture]
    public class WorldInteractionAudioTest
    {
        private GameObject _audioHost;
        private AudioManager _audioManager;
        private GameObject _worldAudioHost;
        private WorldInteractionAudioController _controller;

        private AudioClip _keycardClip;
        private AudioClip _gateUnlockClip;
        private AudioClip _burstImpactClip;
        private AudioClip _ambientLoopClip;

        [SetUp]
        public void SetUp()
        {
            _audioHost = new GameObject("AudioManager_TestHost");
            _audioManager = _audioHost.AddComponent<AudioManager>();
            _audioManager.InitializePool(8);
            AudioManager.Instance = _audioManager;

            _worldAudioHost = new GameObject("WorldAudio_TestHost");
            _controller = _worldAudioHost.AddComponent<WorldInteractionAudioController>();

            _keycardClip = AudioClip.Create("KeycardClip", 44100, 1, 44100, false);
            _gateUnlockClip = AudioClip.Create("GateUnlockClip", 44100, 1, 44100, false);
            _burstImpactClip = AudioClip.Create("BurstImpactClip", 44100, 1, 44100, false);
            _ambientLoopClip = AudioClip.Create("AmbientLoopClip", 44100, 1, 44100, false);

            _controller.KeycardPickupClip = _keycardClip;
            _controller.GateUnlockClip = _gateUnlockClip;
            _controller.BurstImpactClip = _burstImpactClip;
            _controller.AmbientLoopClip = _ambientLoopClip;
        }

        [TearDown]
        public void TearDown()
        {
            AudioManager.Instance = null;

            if (_keycardClip != null) Object.DestroyImmediate(_keycardClip);
            if (_gateUnlockClip != null) Object.DestroyImmediate(_gateUnlockClip);
            if (_burstImpactClip != null) Object.DestroyImmediate(_burstImpactClip);
            if (_ambientLoopClip != null) Object.DestroyImmediate(_ambientLoopClip);

            if (_worldAudioHost != null) Object.DestroyImmediate(_worldAudioHost);
            if (_audioHost != null) Object.DestroyImmediate(_audioHost);
        }

        [Test]
        public void test_keycard_pickup_plays_objective_sound()
        {
            // Arrange
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);

            // Act: Trigger keycard pickup
            _controller.HandleObjectiveAcquired("Keycard_Alpha");

            // Assert: Feedback voice should be active
            Assert.AreEqual(1, _audioManager.ActiveVoiceCount);
        }

        [Test]
        public void test_security_gate_unlock_plays_sliding_door_sound()
        {
            // Arrange
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);

            // Act: Trigger gate unlock
            _controller.HandleGateUnlocked();

            // Assert: 3D SFX voice should be active
            Assert.AreEqual(1, _audioManager.ActiveVoiceCount);
        }

        [Test]
        public void test_burst_projectile_landing_plays_impact_sound()
        {
            // Arrange
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);
            Vector3 impactPos = new Vector3(5f, 0f, 15f);

            // Act: Trigger projectile landing impact
            _controller.HandleBurstNoiseEmitted(impactPos, 10f);

            // Assert: 3D impact SFX should be active
            Assert.AreEqual(1, _audioManager.ActiveVoiceCount);
        }

        [Test]
        public void test_ambient_sound_loops_seamlessly()
        {
            // Act: Start ambient audio loop
            _controller.StartAmbienceLoop();

            // Assert: Ambience audio source should have clip assigned and be playing
            AudioSource ambientSource = _audioManager.AmbientSource;
            Assert.IsNotNull(ambientSource);
            Assert.AreEqual(_ambientLoopClip, ambientSource.clip);
            Assert.IsTrue(ambientSource.loop);
        }
    }
}
