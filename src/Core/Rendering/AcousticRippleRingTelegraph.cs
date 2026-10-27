using System;
using UnityEngine;

namespace WhisperWard.Core.Rendering
{
    /// <summary>
    /// Telemetry evaluation payload for acoustic floor ripple rings.
    /// Pure value type with zero heap allocations.
    /// </summary>
    public readonly struct AcousticRippleData
    {
        public readonly float Radius;
        public readonly float Alpha;

        public AcousticRippleData(float radius, float alpha)
        {
            Radius = radius;
            Alpha = alpha;
        }
    }

    /// <summary>
    /// Evaluates expansion kinematics and alpha dissipation for acoustic sound wave rings.
    /// Used for representing footstep noise pulses and burst distraction impacts on the facility floor.
    /// </summary>
    public static class AcousticRippleRingTelegraph
    {
        /// <summary>
        /// Evaluates current ring radius and transparency at given elapsed time.
        /// </summary>
        /// <param name="elapsedTime">Seconds elapsed since acoustic emission.</param>
        /// <param name="maxRadius">Maximum acoustic range in meters.</param>
        /// <param name="lifetime">Total visual duration before full dissipation.</param>
        public static AcousticRippleData Evaluate(float elapsedTime, float maxRadius, float lifetime)
        {
            if (lifetime <= 0.001f)
            {
                return new AcousticRippleData(maxRadius, 0.0f);
            }

            float normalizedTime = Mathf.Clamp01(elapsedTime / lifetime);
            float currentRadius = maxRadius * normalizedTime;
            float currentAlpha = 1.0f - normalizedTime;

            return new AcousticRippleData(currentRadius, currentAlpha);
        }
    }
}
