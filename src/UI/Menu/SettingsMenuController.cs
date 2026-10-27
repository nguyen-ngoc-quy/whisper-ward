using System;
using UnityEngine;
using WhisperWard.Core.Audio;

namespace WhisperWard.UI.Menu
{
    /// <summary>
    /// In-game Settings and Preferences Controller.
    /// Manages player-configured audio bus volumes (Master, SFX, Ambience),
    /// mouse look sensitivity, and display toggles with zero heap allocation.
    /// Directs volume modifications to IAudioService / AudioManager instance when active.
    /// </summary>
    /// <example>
    /// <code>
    /// var controller = gameObject.AddComponent&lt;SettingsMenuController&gt;();
    /// controller.SetMasterVolume(0.8f);
    /// controller.OnSettingsChanged += data =&gt; Debug.Log($"Sensitivity: {data.MouseSensitivity}");
    /// </code>
    /// </example>
    public class SettingsMenuController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private SettingsData _currentSettings = SettingsData.Default;

        /// <summary>Fired whenever any setting option is modified.</summary>
        public event Action<SettingsData> OnSettingsChanged;

        public SettingsData CurrentSettings => _currentSettings;

        private void Awake()
        {
            ApplySettings();
        }

        /// <summary>
        /// Updates the Master volume bus [0.0, 1.0].
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            _currentSettings.MasterVolume = Mathf.Clamp01(volume);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.MasterVolume = _currentSettings.MasterVolume;
            }

            NotifySettingsChanged();
        }

        /// <summary>
        /// Updates the SFX diegetic volume bus [0.0, 1.0].
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            _currentSettings.SFXVolume = Mathf.Clamp01(volume);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SFXVolume = _currentSettings.SFXVolume;
            }

            NotifySettingsChanged();
        }

        /// <summary>
        /// Updates the Ambience volume bus [0.0, 1.0].
        /// </summary>
        public void SetAmbienceVolume(float volume)
        {
            _currentSettings.AmbienceVolume = Mathf.Clamp01(volume);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.AmbienceVolume = _currentSettings.AmbienceVolume;
            }

            NotifySettingsChanged();
        }

        /// <summary>
        /// Updates the mouse look sensitivity [0.1, 5.0].
        /// </summary>
        public void SetMouseSensitivity(float sensitivity)
        {
            _currentSettings.MouseSensitivity = Mathf.Clamp(sensitivity, 0.1f, 5.0f);
            NotifySettingsChanged();
        }

        /// <summary>
        /// Updates fullscreen mode.
        /// </summary>
        public void SetFullscreen(bool fullscreen)
        {
            _currentSettings.Fullscreen = fullscreen;
            Screen.fullScreen = fullscreen;
            NotifySettingsChanged();
        }

        /// <summary>
        /// Toggles post-processing visual effects.
        /// </summary>
        public void SetPostProcessing(bool enabled)
        {
            _currentSettings.PostProcessEnabled = enabled;
            NotifySettingsChanged();
        }

        /// <summary>
        /// Resets all settings to factory baseline defaults.
        /// </summary>
        public void ResetToDefaults()
        {
            _currentSettings = SettingsData.Default;
            ApplySettings();
            NotifySettingsChanged();
        }

        /// <summary>
        /// Applies current settings payload to runtime services.
        /// </summary>
        public void ApplySettings()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.MasterVolume = _currentSettings.MasterVolume;
                AudioManager.Instance.SFXVolume = _currentSettings.SFXVolume;
                AudioManager.Instance.AmbienceVolume = _currentSettings.AmbienceVolume;
            }
        }

        private void NotifySettingsChanged()
        {
            OnSettingsChanged?.Invoke(_currentSettings);
        }
    }
}
