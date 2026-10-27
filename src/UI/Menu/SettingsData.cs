using System;
using UnityEngine;

namespace WhisperWard.UI.Menu
{
    /// <summary>
    /// User preferences and configuration payload for Audio, Graphics, and Controls.
    /// Conforms to Tuần 7/8 Settings UI standards.
    /// Zero heap allocation on queries.
    /// </summary>
    [Serializable]
    public struct SettingsData
    {
        [Range(0f, 1f)] public float MasterVolume;
        [Range(0f, 1f)] public float SFXVolume;
        [Range(0f, 1f)] public float AmbienceVolume;
        [Range(0.1f, 5f)] public float MouseSensitivity;
        public bool Fullscreen;
        public bool PostProcessEnabled;

        public SettingsData(
            float masterVolume = 1.0f,
            float sfxVolume = 1.0f,
            float ambienceVolume = 0.8f,
            float mouseSensitivity = 1.0f,
            bool fullscreen = true,
            bool postProcessEnabled = true)
        {
            MasterVolume = Mathf.Clamp01(masterVolume);
            SFXVolume = Mathf.Clamp01(sfxVolume);
            AmbienceVolume = Mathf.Clamp01(ambienceVolume);
            MouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.1f, 5.0f);
            Fullscreen = fullscreen;
            PostProcessEnabled = postProcessEnabled;
        }

        /// <summary>
        /// Factory providing default baseline configuration.
        /// </summary>
        public static SettingsData Default => new SettingsData(
            masterVolume: 1.0f,
            sfxVolume: 1.0f,
            ambienceVolume: 0.8f,
            mouseSensitivity: 1.0f,
            fullscreen: true,
            postProcessEnabled: true
        );
    }
}
