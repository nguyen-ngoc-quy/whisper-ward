using System;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Immutable registry-backed Burst runtime contract shared by the flight
    /// simulator, the preview, and the LevelFixture adapters. A composition
    /// root must validate one instance before the first launch or spend; no
    /// value silently falls back to another profile.
    /// </summary>
    public sealed class BurstRuntimeConfiguration
    {
        /// <summary>Canonical registry fixed-step cadence for Burst simulation.</summary>
        public const float CanonicalFixedSubstepSeconds = 1f / 120f;

        /// <summary>
        /// Creates a validated Burst contract from registry data. Starter
        /// values are the registry BurstThrowConfig entries; the caller owns
        /// per-segment tuning, not this type.
        /// </summary>
        public BurstRuntimeConfiguration(float initialSpeedMetersPerSecond,
            float launchAngleDegrees, float releaseHeightMeters,
            float gravityMagnitude, float fixedSubstepSeconds,
            float flightTimeoutSeconds, float projectileRadiusMeters,
            float epsilonContactMeters, int maxBacklogTicks,
            float hearingRadiusMeters, float pickupReachRadiusMeters)
        {
            string errorCode;
            if (!TryValidate(initialSpeedMetersPerSecond, launchAngleDegrees,
                releaseHeightMeters, gravityMagnitude, fixedSubstepSeconds,
                flightTimeoutSeconds, projectileRadiusMeters,
                epsilonContactMeters, maxBacklogTicks, hearingRadiusMeters,
                pickupReachRadiusMeters, out errorCode))
            {
                throw new ArgumentException(errorCode, "burstConfiguration");
            }

            InitialSpeedMetersPerSecond = initialSpeedMetersPerSecond;
            LaunchAngleDegrees = launchAngleDegrees;
            ReleaseHeightMeters = releaseHeightMeters;
            GravityMagnitude = gravityMagnitude;
            FixedSubstepSeconds = fixedSubstepSeconds;
            FlightTimeoutSeconds = flightTimeoutSeconds;
            ProjectileRadiusMeters = projectileRadiusMeters;
            EpsilonContactMeters = epsilonContactMeters;
            MaxBacklogTicks = maxBacklogTicks;
            TimeoutTick = (int)Math.Ceiling(flightTimeoutSeconds / FixedSubstepSeconds);
            HearingRadiusMeters = hearingRadiusMeters;
            PickupReachRadiusMeters = pickupReachRadiusMeters;
        }

        /// <summary>Initial launch speed magnitude in metres per second.</summary>
        public float InitialSpeedMetersPerSecond { get; }

        /// <summary>Launch angle in degrees within the F4 domain (0, 45].</summary>
        public float LaunchAngleDegrees { get; }

        /// <summary>Release height above the thrower's feet in metres.</summary>
        public float ReleaseHeightMeters { get; }

        /// <summary>Positive gravity magnitude in metres per second squared.</summary>
        public float GravityMagnitude { get; }

        /// <summary>Locked fixed integration substep in seconds (registry 1/120 s).</summary>
        public float FixedSubstepSeconds { get; }

        /// <summary>Flight timeout boundary in seconds; contact wins on equality.</summary>
        public float FlightTimeoutSeconds { get; }

        /// <summary>Fixed Burst collision sphere radius in metres.</summary>
        public float ProjectileRadiusMeters { get; }

        /// <summary>Post-hit projectile-center push-out margin in metres.</summary>
        public float EpsilonContactMeters { get; }

        /// <summary>Maximum retained fixed-step catch-up ticks per flight batch.</summary>
        public int MaxBacklogTicks { get; }

        /// <summary>Authoritative inclusive timeout tick derived from the fixed cadence.</summary>
        public int TimeoutTick { get; }

        /// <summary>Published Burst hearing radius in metres (registry R_burst).</summary>
        public float HearingRadiusMeters { get; }

        /// <summary>
        /// Reach radius that arms a Placed Burst, consumed by the pickup
        /// interaction adapter rather than the flight simulator.
        /// </summary>
        public float PickupReachRadiusMeters { get; }

        /// <summary>
        /// Validates one complete registry profile before any launch or spend.
        /// The angle domain is the F4 authored domain; the INVALID_ANGLE and
        /// INVALID_ROOT config gates of the registry resolve to these checks.
        /// </summary>
        public static bool TryValidate(float initialSpeedMetersPerSecond,
            float launchAngleDegrees, float releaseHeightMeters,
            float gravityMagnitude, float fixedSubstepSeconds,
            float flightTimeoutSeconds, float projectileRadiusMeters,
            float epsilonContactMeters, int maxBacklogTicks,
            float hearingRadiusMeters, float pickupReachRadiusMeters,
            out string errorCode)
        {
            errorCode = string.Empty;
            if (!IsFinitePositive(initialSpeedMetersPerSecond))
            {
                errorCode = "burst-config-speed-invalid";
                return false;
            }
            if (!IsFinite(launchAngleDegrees) || launchAngleDegrees <= 0f
                || launchAngleDegrees > 45f)
            {
                errorCode = "burst-config-angle-invalid";
                return false;
            }
            if (!IsFinitePositive(releaseHeightMeters))
            {
                errorCode = "burst-config-release-height-invalid";
                return false;
            }
            if (!IsFinitePositive(gravityMagnitude))
            {
                errorCode = "burst-config-gravity-invalid";
                return false;
            }
            if (!IsFinitePositive(fixedSubstepSeconds))
            {
                errorCode = "burst-config-substep-invalid";
                return false;
            }
            // The registry stores 0.0083333333 as the serialized form of the
            // rational 1/120 second step. Reject a valid-but-different cadence so
            // preview, replay, and runtime cannot silently diverge.
            if (Math.Abs(fixedSubstepSeconds - CanonicalFixedSubstepSeconds) > 0.000001f)
            {
                errorCode = "burst-config-substep-not-canonical";
                return false;
            }
            if (!IsFinitePositive(flightTimeoutSeconds))
            {
                errorCode = "burst-config-timeout-invalid";
                return false;
            }
            if (!IsFinitePositive(projectileRadiusMeters))
            {
                errorCode = "burst-config-projectile-radius-invalid";
                return false;
            }
            if (!IsFiniteNonNegative(epsilonContactMeters))
            {
                errorCode = "burst-config-epsilon-invalid";
                return false;
            }
            if (maxBacklogTicks <= 0)
            {
                errorCode = "burst-config-backlog-invalid";
                return false;
            }
            if (!IsFinitePositive(hearingRadiusMeters))
            {
                errorCode = "burst-config-hearing-radius-invalid";
                return false;
            }
            if (!IsFinitePositive(pickupReachRadiusMeters))
            {
                errorCode = "burst-config-pickup-reach-invalid";
                return false;
            }
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }
    }
}
