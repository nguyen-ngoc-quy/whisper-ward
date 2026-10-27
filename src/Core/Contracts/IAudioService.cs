using System;
using UnityEngine;

namespace WhisperWard.Core.Contracts
{
    /// <summary>
    /// Logical audio bus classification conforming to ADR-0003 and GDD #13 4-bus hierarchy.
    /// </summary>
    public enum AudioBusType
    {
        /// <summary>Global output bus controlling all sounds with hardware peak limiting.</summary>
        Master = 0,

        /// <summary>3D spatialized in-world sounds (footsteps, projectiles, doors, guard vocals).</summary>
        SFXDiegetic = 1,

        /// <summary>2D psychological and interface feedback cues (stingers, heartbeat, UI clicks).</summary>
        FeedbackNonDiegetic = 2,

        /// <summary>Environmental background room tone with dynamic ducking capability.</summary>
        Ambience = 3
    }

    /// <summary>
    /// Contract for the core audio service providing zero-allocation voice pooling,
    /// 4-bus hierarchy volume management, and deterministic life-cycle voice cleanup.
    /// </summary>
    /// <example>
    /// <code>
    /// IAudioService audio = AudioManager.Instance;
    /// audio.PlaySFX3D(footstepClip, playerPosition, volume: 0.8f, pitch: 1.0f);
    /// audio.SetAmbienceDucking(true); // Duck background by -6 dB during Chase
    /// </code>
    /// </example>
    public interface IAudioService
    {
        /// <summary>Gets or sets the master volume multiplier [0.0, 1.0].</summary>
        float MasterVolume { get; set; }

        /// <summary>Gets or sets the diegetic SFX bus volume multiplier [0.0, 1.0].</summary>
        float SFXVolume { get; set; }

        /// <summary>Gets or sets the non-diegetic feedback bus volume multiplier [0.0, 1.0].</summary>
        float FeedbackVolume { get; set; }

        /// <summary>Gets or sets the ambient tone bus volume multiplier [0.0, 1.0].</summary>
        float AmbienceVolume { get; set; }

        /// <summary>Gets whether ambient audio is actively ducked (e.g. during active chase).</summary>
        bool IsAmbienceDucked { get; }

        /// <summary>Gets the current count of active playing voices in the pool.</summary>
        int ActiveVoiceCount { get; }

        /// <summary>Gets the maximum capacity of the pre-allocated voice pool.</summary>
        int PoolCapacity { get; }

        /// <summary>
        /// Plays a non-spatialized 2D sound effect through the SFX diegetic bus.
        /// </summary>
        /// <param name="clip">The audio clip to play.</param>
        /// <param name="volume">Normalized volume [0.0, 1.0].</param>
        /// <param name="pitch">Playback pitch modifier (default 1.0).</param>
        /// <param name="priority">Playback priority [0 = highest, 256 = lowest].</param>
        /// <returns>Unique voice ID assigned to the playback, or -1 if rejected.</returns>
        int PlaySFX2D(AudioClip clip, float volume = 1.0f, float pitch = 1.0f, int priority = 128);

        /// <summary>
        /// Plays a 3D spatialized sound effect at a specified world-space position.
        /// </summary>
        /// <param name="clip">The audio clip to play.</param>
        /// <param name="position">World position of the sound emission.</param>
        /// <param name="volume">Normalized volume [0.0, 1.0].</param>
        /// <param name="pitch">Playback pitch modifier (default 1.0).</param>
        /// <param name="minDistance">Attenuation start distance in meters.</param>
        /// <param name="maxDistance">Attenuation cutoff distance in meters.</param>
        /// <param name="priority">Playback priority [0 = highest, 256 = lowest].</param>
        /// <returns>Unique voice ID assigned to the playback, or -1 if rejected.</returns>
        int PlaySFX3D(AudioClip clip, Vector3 position, float volume = 1.0f, float pitch = 1.0f, float minDistance = 1.0f, float maxDistance = 25.0f, int priority = 128);

        /// <summary>
        /// Plays a 2D feedback sound effect through the Feedback non-diegetic bus.
        /// </summary>
        /// <param name="clip">The feedback clip to play.</param>
        /// <param name="volume">Normalized volume [0.0, 1.0].</param>
        /// <param name="pitch">Playback pitch modifier (default 1.0).</param>
        /// <param name="priority">Playback priority [0 = highest, 256 = lowest].</param>
        /// <returns>Unique voice ID assigned to the playback, or -1 if rejected.</returns>
        int PlayFeedback(AudioClip clip, float volume = 1.0f, float pitch = 1.0f, int priority = 128);

        /// <summary>
        /// Sets and initiates looping background ambient audio.
        /// </summary>
        /// <param name="clip">The ambient loop audio clip.</param>
        /// <param name="volume">Base volume multiplier [0.0, 1.0].</param>
        /// <param name="loop">Whether the audio should repeat continuously.</param>
        void SetAmbience(AudioClip clip, float volume = 1.0f, bool loop = true);

        /// <summary>
        /// Controls ambient audio ducking (reduces ambient level by -6 dB during intense gameplay).
        /// </summary>
        /// <param name="isDucked">True to engage ducking; false to restore normal volume.</param>
        void SetAmbienceDucking(bool isDucked);

        /// <summary>
        /// Stops an active voice immediately by its unique voice ID.
        /// </summary>
        /// <param name="voiceId">The voice identifier returned by play methods.</param>
        void StopVoice(int voiceId);

        /// <summary>
        /// Immediately stops and resets all active voices in the pool and ambience source.
        /// </summary>
        void StopAll();
    }
}
