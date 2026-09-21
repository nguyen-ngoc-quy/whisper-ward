using UnityEngine;

namespace WhisperWard.Foundation.Input
{
    /// <summary>
    /// Processes analog 2D stick inputs through dual radial deadzones.
    /// Conforms to ADR-0005 and TR-FOUND-009.
    /// </summary>
    public static class RadialDeadzoneProcessor
    {
        public const float DefaultInnerDeadzone = 0.10f;
        public const float DefaultOuterDeadzone = 0.95f;

        /// <summary>
        /// Applies radial deadzone filtering:
        /// - Magnitude <= innerDeadzone returns Vector2.zero.
        /// - Magnitude >= outerDeadzone clamps magnitude to 1.0f preserving heading direction.
        /// - Magnitude between inner and outer scales linearly into [0.0, 1.0].
        /// </summary>
        public static Vector2 Process(Vector2 rawInput, float innerDeadzone = DefaultInnerDeadzone, float outerDeadzone = DefaultOuterDeadzone)
        {
            float magnitude = rawInput.magnitude;

            if (magnitude <= innerDeadzone)
            {
                return Vector2.zero;
            }

            Vector2 direction = rawInput / magnitude;

            if (magnitude >= outerDeadzone)
            {
                return direction;
            }

            float normalizedMagnitude = (magnitude - innerDeadzone) / (outerDeadzone - innerDeadzone);
            return direction * normalizedMagnitude;
        }
    }
}
