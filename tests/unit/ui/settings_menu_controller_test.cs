using NUnit.Framework;
using UnityEngine;
using WhisperWard.UI.Menu;

namespace WhisperWard.Tests.Unit.UI
{
    [TestFixture]
    public class SettingsMenuControllerTest
    {
        private GameObject _hostObj;
        private SettingsMenuController _controller;

        [SetUp]
        public void SetUp()
        {
            _hostObj = new GameObject("SettingsMenu_TestHost");
            _controller = _hostObj.AddComponent<SettingsMenuController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hostObj != null)
            {
                Object.DestroyImmediate(_hostObj);
            }
        }

        [Test]
        public void test_settings_data_defaults_fall_within_valid_ranges()
        {
            // Arrange & Act
            var defaults = SettingsData.Default;

            // Assert
            Assert.AreEqual(1.0f, defaults.MasterVolume, 0.001f);
            Assert.AreEqual(1.0f, defaults.SFXVolume, 0.001f);
            Assert.AreEqual(0.8f, defaults.AmbienceVolume, 0.001f);
            Assert.AreEqual(1.0f, defaults.MouseSensitivity, 0.001f);
            Assert.IsTrue(defaults.Fullscreen);
            Assert.IsTrue(defaults.PostProcessEnabled);
        }

        [Test]
        public void test_settings_controller_volume_sliders_clamp_to_unit_range()
        {
            // Arrange
            _controller.ResetToDefaults();

            // Act - Over range
            _controller.SetMasterVolume(1.5f);
            _controller.SetSFXVolume(2.0f);
            _controller.SetAmbienceVolume(10.0f);

            // Assert
            Assert.AreEqual(1.0f, _controller.CurrentSettings.MasterVolume, 0.001f);
            Assert.AreEqual(1.0f, _controller.CurrentSettings.SFXVolume, 0.001f);
            Assert.AreEqual(1.0f, _controller.CurrentSettings.AmbienceVolume, 0.001f);

            // Act - Negative range
            _controller.SetMasterVolume(-0.5f);
            _controller.SetSFXVolume(-1.0f);
            _controller.SetAmbienceVolume(-10.0f);

            // Assert
            Assert.AreEqual(0.0f, _controller.CurrentSettings.MasterVolume, 0.001f);
            Assert.AreEqual(0.0f, _controller.CurrentSettings.SFXVolume, 0.001f);
            Assert.AreEqual(0.0f, _controller.CurrentSettings.AmbienceVolume, 0.001f);
        }

        [Test]
        public void test_settings_controller_mouse_sensitivity_clamps_to_supported_range()
        {
            // Arrange
            _controller.ResetToDefaults();

            // Act
            _controller.SetMouseSensitivity(0.05f); // Below min 0.1
            Assert.AreEqual(0.1f, _controller.CurrentSettings.MouseSensitivity, 0.001f);

            _controller.SetMouseSensitivity(10.0f); // Above max 5.0
            Assert.AreEqual(5.0f, _controller.CurrentSettings.MouseSensitivity, 0.001f);

            _controller.SetMouseSensitivity(2.5f); // Valid
            Assert.AreEqual(2.5f, _controller.CurrentSettings.MouseSensitivity, 0.001f);
        }

        [Test]
        public void test_settings_controller_reset_defaults_restores_baseline_values()
        {
            // Arrange
            _controller.SetMasterVolume(0.2f);
            _controller.SetSFXVolume(0.3f);
            _controller.SetAmbienceVolume(0.4f);
            _controller.SetMouseSensitivity(3.0f);
            _controller.SetFullscreen(false);
            _controller.SetPostProcessing(false);

            // Act
            _controller.ResetToDefaults();

            // Assert
            Assert.AreEqual(1.0f, _controller.CurrentSettings.MasterVolume, 0.001f);
            Assert.AreEqual(1.0f, _controller.CurrentSettings.SFXVolume, 0.001f);
            Assert.AreEqual(0.8f, _controller.CurrentSettings.AmbienceVolume, 0.001f);
            Assert.AreEqual(1.0f, _controller.CurrentSettings.MouseSensitivity, 0.001f);
            Assert.IsTrue(_controller.CurrentSettings.Fullscreen);
            Assert.IsTrue(_controller.CurrentSettings.PostProcessEnabled);
        }

        [Test]
        public void test_settings_controller_fullscreen_and_postprocess_toggles_update_state()
        {
            // Arrange
            bool eventFired = false;
            SettingsData receivedData = default;
            _controller.OnSettingsChanged += data =>
            {
                eventFired = true;
                receivedData = data;
            };

            // Act
            _controller.SetFullscreen(false);
            _controller.SetPostProcessing(false);

            // Assert
            Assert.IsTrue(eventFired);
            Assert.IsFalse(receivedData.Fullscreen);
            Assert.IsFalse(receivedData.PostProcessEnabled);
        }
    }
}
