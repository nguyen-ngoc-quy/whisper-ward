using NUnit.Framework;
using UnityEngine;
using WhisperWard.Core.Rendering;

namespace WhisperWard.Tests.Unit.Rendering
{
    [TestFixture]
    public class ColdWatchPostProcessTest
    {
        [Test]
        public void test_cold_watch_profile_generates_valid_vignette_parameters()
        {
            // Arrange
            var config = ColdWatchPostProcessConfig.CreateDefault();

            // Act
            bool isValid = config.Validate(out string error);

            // Assert
            Assert.IsTrue(isValid, error);
            Assert.AreEqual(0.35f, config.VignetteIntensity, 0.001f);
            Assert.AreEqual(0.50f, config.VignetteSmoothness, 0.001f);
            Assert.AreEqual(Color.black, config.VignetteColor);
        }

        [Test]
        public void test_cold_watch_bloom_threshold_preserves_shadow_depth()
        {
            // Arrange
            var config = ColdWatchPostProcessConfig.CreateDefault();

            // Act & Assert
            // Bloom threshold must be >= 1.0 to ensure normal lit surfaces do not bloom
            Assert.GreaterOrEqual(config.BloomThreshold, 1.00f);
            Assert.LessOrEqual(config.BloomIntensity, 1.00f);

            // Setting invalid bloom threshold below 1.0 should fail validation
            config.BloomThreshold = 0.85f;
            bool isInvalid = config.Validate(out string error);
            Assert.IsFalse(isInvalid);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void test_cold_watch_color_tint_matches_moonlit_palette()
        {
            // Arrange
            var config = ColdWatchPostProcessConfig.CreateDefault();

            // Assert: Tint matches #A0C4E2 (0.627, 0.769, 0.886)
            Assert.AreEqual(0.627f, config.ColorFilter.r, 0.01f);
            Assert.AreEqual(0.769f, config.ColorFilter.g, 0.01f);
            Assert.AreEqual(0.886f, config.ColorFilter.b, 0.01f);
            Assert.AreEqual(15.0f, config.Contrast, 0.001f);
        }
    }
}
