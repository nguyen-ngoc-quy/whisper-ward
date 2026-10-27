using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Audio;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Tests.Unit.Audio
{
    [TestFixture]
    public class PlayerFootstepAudioTest
    {
        private GameObject _playerHost;
        private PlayerFootstepAudioController _controller;
        private GameObject _audioHost;
        private AudioManager _audioManager;
        private AudioClip _dummyWalkClip;
        private AudioClip _dummyRunClip;
        private AudioClip _dummyCrouchClip;

        [SetUp]
        public void SetUp()
        {
            _audioHost = new GameObject("AudioManager_TestHost");
            _audioManager = _audioHost.AddComponent<AudioManager>();
            _audioManager.InitializePool(8);
            AudioManager.Instance = _audioManager;

            _playerHost = new GameObject("Player_TestHost");
            _controller = _playerHost.AddComponent<PlayerFootstepAudioController>();

            _dummyWalkClip = AudioClip.Create("WalkClip", 44100, 1, 44100, false);
            _dummyRunClip = AudioClip.Create("RunClip", 44100, 1, 44100, false);
            _dummyCrouchClip = AudioClip.Create("CrouchClip", 44100, 1, 44100, false);

            _controller.WalkClip = _dummyWalkClip;
            _controller.RunClip = _dummyRunClip;
            _controller.CrouchClip = _dummyCrouchClip;
        }

        [TearDown]
        public void TearDown()
        {
            AudioManager.Instance = null;

            if (_dummyWalkClip != null) Object.DestroyImmediate(_dummyWalkClip);
            if (_dummyRunClip != null) Object.DestroyImmediate(_dummyRunClip);
            if (_dummyCrouchClip != null) Object.DestroyImmediate(_dummyCrouchClip);

            if (_playerHost != null) Object.DestroyImmediate(_playerHost);
            if (_audioHost != null) Object.DestroyImmediate(_audioHost);
        }

        [Test]
        public void test_player_footstep_audio_plays_on_step_event()
        {
            // Arrange
            bool eventTriggered = false;
            MovementStance triggeredStance = MovementStance.Standing;
            float triggeredVolume = 0f;

            _controller.OnFootstepAudioTriggered += (stance, volume) =>
            {
                eventTriggered = true;
                triggeredStance = stance;
                triggeredVolume = volume;
            };

            // Act: Trigger footstep for walking stance
            _controller.TriggerFootstep(MovementStance.Standing, 2.5f, Vector3.zero);

            // Assert
            Assert.IsTrue(eventTriggered);
            Assert.AreEqual(MovementStance.Standing, triggeredStance);
            Assert.AreEqual(_controller.WalkVolume, triggeredVolume, 0.001f);
            Assert.AreEqual(1, _audioManager.ActiveVoiceCount);
        }

        [Test]
        public void test_footstep_volume_scales_with_player_locomotion_stance()
        {
            // Act & Assert
            float crouchVol = _controller.GetVolumeForStance(MovementStance.Crouched, 1.5f);
            float walkVol = _controller.GetVolumeForStance(MovementStance.Standing, 2.5f);
            float runVol = _controller.GetVolumeForStance(MovementStance.Standing, 5.0f);

            Assert.Less(crouchVol, walkVol);
            Assert.Less(walkVol, runVol);
            Assert.AreEqual(_controller.CrouchVolume, crouchVol, 0.001f);
            Assert.AreEqual(_controller.WalkVolume, walkVol, 0.001f);
            Assert.AreEqual(_controller.RunVolume, runVol, 0.001f);
        }

        [Test]
        public void test_footstep_stride_length_varies_by_stance()
        {
            // Act
            float crouchStride = _controller.GetStrideLengthForStance(MovementStance.Crouched, 1.5f);
            float walkStride = _controller.GetStrideLengthForStance(MovementStance.Standing, 2.5f);
            float runStride = _controller.GetStrideLengthForStance(MovementStance.Standing, 5.0f);

            // Assert
            Assert.Less(crouchStride, walkStride);
            Assert.Less(walkStride, runStride);
        }

        [Test]
        public void test_crouch_step_volume_attenuates_appropriately()
        {
            // Arrange
            float recordedVolume = 0f;
            _controller.OnFootstepAudioTriggered += (s, v) => recordedVolume = v;

            // Act
            _controller.TriggerFootstep(MovementStance.Crouched, 1.2f, Vector3.zero);

            // Assert: Crouch volume should be quieter than 0.35f
            Assert.AreEqual(_controller.CrouchVolume, recordedVolume, 0.001f);
            Assert.LessOrEqual(recordedVolume, 0.30f);
        }

        [Test]
        public void test_stationary_player_does_not_accumulate_stride()
        {
            // Arrange
            _playerHost.transform.position = Vector3.zero;
            _controller.Tick(0.02f);

            // Act: Stay stationary
            _playerHost.transform.position = Vector3.zero;
            _controller.Tick(0.02f);

            // Assert
            Assert.AreEqual(0f, _controller.StrideAccumulator, 0.001f);
            Assert.AreEqual(0, _audioManager.ActiveVoiceCount);
        }
    }
}
