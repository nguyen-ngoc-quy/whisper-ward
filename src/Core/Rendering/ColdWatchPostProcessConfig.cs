using System;
using UnityEngine;

namespace WhisperWard.Core.Rendering
{
    /// <summary>
    /// Configuration data and parameter validation for the Cold Watch moonlit post-processing aesthetic.
    /// Conforms to Tuần 7 URP Post-Processing and Lighting standards.
    /// Provides settings for Vignette, Bloom, and Color Adjustments ensuring high visual contrast
    /// and stealth focus while strictly abiding by WebGL/PC performance budgets.
    /// </summary>
    [Serializable]
    public class ColdWatchPostProcessConfig
    {
        [Header("Vignette Settings")]
        [Tooltip("Darkens edges of the screen to guide player focus towards stealth zones.")]
        [Range(0f, 1f)] public float VignetteIntensity = 0.35f;
        [Range(0.01f, 1f)] public float VignetteSmoothness = 0.50f;
        public Color VignetteColor = Color.black;

        [Header("Bloom Settings")]
        [Tooltip("Threshold above 1.0 ensures only emissive alert markers and keycards bloom.")]
        [Range(1.0f, 3f)] public float BloomThreshold = 1.10f;
        [Range(0f, 2f)] public float BloomIntensity = 0.65f;
        [Range(0f, 1f)] public float BloomScatter = 0.70f;

        [Header("Color Adjustments (Moonlit Cyan Tint)")]
        [Tooltip("Cold Watch signature cool blue-cyan moonlit tint (#A0C4E2).")]
        public Color ColorFilter = new Color(0.627f, 0.769f, 0.886f, 1.0f); // #A0C4E2
        [Range(-100f, 100f)] public float Contrast = 15.0f;
        [Range(-2f, 2f)] public float PostExposure = 0.10f;

        [Header("Lighting Settings")]
        public Color DirectionalMoonlightColor = new Color(0.627f, 0.769f, 0.886f, 1.0f);
        public float DirectionalMoonlightIntensity = 0.85f;

        /// <summary>
        /// Validates that the post-processing configuration satisfies stealth readability and performance limits.
        /// </summary>
        /// <param name="errorMessage">Output explanation if validation fails.</param>
        /// <returns>True if within compliant envelopes.</returns>
        public bool Validate(out string errorMessage)
        {
            if (VignetteIntensity < 0.15f || VignetteIntensity > 0.60f)
            {
                errorMessage = $"Vignette intensity {VignetteIntensity} outside comfortable stealth range [0.15, 0.60].";
                return false;
            }

            if (BloomThreshold < 1.00f)
            {
                errorMessage = $"Bloom threshold {BloomThreshold} must be >= 1.0 to prevent shadow wash-out.";
                return false;
            }

            if (BloomIntensity > 1.50f)
            {
                errorMessage = $"Bloom intensity {BloomIntensity} exceeds maximum allowable threshold for WebGL draw budget.";
                return false;
            }

            if (DirectionalMoonlightIntensity < 0.20f || DirectionalMoonlightIntensity > 2.00f)
            {
                errorMessage = $"Moonlight intensity {DirectionalMoonlightIntensity} outside safe visibility bounds [0.20, 2.00].";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Returns a factory instance pre-configured to canonical Cold Watch standards.
        /// </summary>
        public static ColdWatchPostProcessConfig CreateDefault()
        {
            return new ColdWatchPostProcessConfig();
        }
    }
}
