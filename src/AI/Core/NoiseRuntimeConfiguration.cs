using System;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Immutable registry-backed values shared by the noise, hearing, and FSM
    /// stages. A composition root must validate one instance before its first tick.
    /// </summary>
    public sealed class NoiseRuntimeConfiguration
    {
        /// <summary>Registered independent hearing cadence in virtual seconds.</summary>
        public const float RegisteredHearingIntervalSeconds = 0.2f;

        /// <summary>Registered F12 movement-origin lift in metres.</summary>
        public const float RegisteredF12NoiseOriginOffset = 0.25f;

        /// <summary>Registered soft vertical hearing cutoff in metres.</summary>
        public const float RegisteredHearingYHardCutoff = 4.0f;

        /// <summary>Registered vision/FSM cadence in virtual seconds.</summary>
        public const float RegisteredFsmIntervalSeconds = 0.5f;

        /// <summary>Registered post-resolution noise recommit cooldown.</summary>
        public const float RegisteredNoiseRecommitCooldownSeconds = 2.0f;

        /// <summary>Registered difficulty scalar used by Investigate timing.</summary>
        public const float RegisteredDifficultyScalar = 1.0f;

        /// <summary>
        /// Creates a validated runtime contract. Values are supplied by the
        /// registry adapter; this type never silently selects another profile.
        /// </summary>
        public NoiseRuntimeConfiguration(NoiseResponseProfile responseProfile,
            float hearingIntervalSeconds, int maxHearingGuards,
            int maxHearingFacts, int maxHearingPairs, int hearingWorkCapacity,
            int maxRetries, float compareToleranceMeters,
            float mathRelativeTolerance, float f12NoiseOriginOffset,
            float investigateBaseSeconds, float noiseReanchorExtendSeconds,
            float reanchorMaxMeters, float thoroughnessCoefficient)
            : this(responseProfile, hearingIntervalSeconds, maxHearingGuards,
                maxHearingFacts, maxHearingPairs, hearingWorkCapacity,
                128, hearingWorkCapacity, 4, 1, maxRetries,
                compareToleranceMeters, mathRelativeTolerance,
                f12NoiseOriginOffset, investigateBaseSeconds,
                noiseReanchorExtendSeconds, reanchorMaxMeters,
                thoroughnessCoefficient)
        {
        }

        /// <summary>
        /// Creates a profile with independent raw-fact, deferred-work, relay, and
        /// catch-up capacities. These capacities are not interchangeable queues.
        /// </summary>
        public NoiseRuntimeConfiguration(NoiseResponseProfile responseProfile,
            float hearingIntervalSeconds, int maxHearingGuards,
            int maxHearingFacts, int maxHearingPairs, int hearingWorkCapacity,
            int rawFactQueueCapacity, int relayQueueCapacity,
            int maxDeferredHearingBoundaries, int maxHearingBoundariesPerFrame,
            int maxRetries, float compareToleranceMeters,
            float mathRelativeTolerance, float f12NoiseOriginOffset,
            float investigateBaseSeconds, float noiseReanchorExtendSeconds,
            float reanchorMaxMeters, float thoroughnessCoefficient)
        {
            string errorCode;
            if (!TryValidate(responseProfile, hearingIntervalSeconds,
                maxHearingGuards, maxHearingFacts, maxHearingPairs,
                hearingWorkCapacity, rawFactQueueCapacity, relayQueueCapacity,
                maxDeferredHearingBoundaries, maxHearingBoundariesPerFrame,
                maxRetries, compareToleranceMeters, mathRelativeTolerance,
                f12NoiseOriginOffset, investigateBaseSeconds,
                noiseReanchorExtendSeconds, reanchorMaxMeters,
                thoroughnessCoefficient, out errorCode))
            {
                throw new ArgumentException(errorCode, nameof(responseProfile));
            }

            ResponseProfile = responseProfile;
            HearingIntervalSeconds = hearingIntervalSeconds;
            MaxHearingGuards = maxHearingGuards;
            MaxHearingFacts = maxHearingFacts;
            MaxHearingPairs = maxHearingPairs;
            HearingWorkCapacity = hearingWorkCapacity;
            RawFactQueueCapacity = rawFactQueueCapacity;
            RelayQueueCapacity = relayQueueCapacity;
            MaxDeferredHearingBoundaries = maxDeferredHearingBoundaries;
            MaxHearingBoundariesPerFrame = maxHearingBoundariesPerFrame;
            MaxRetries = maxRetries;
            CompareToleranceMeters = compareToleranceMeters;
            MathRelativeTolerance = mathRelativeTolerance;
            F12NoiseOriginOffset = f12NoiseOriginOffset;
            InvestigateBaseSeconds = investigateBaseSeconds;
            NoiseReanchorExtendSeconds = noiseReanchorExtendSeconds;
            ReanchorMaxMeters = reanchorMaxMeters;
            ThoroughnessCoefficient = thoroughnessCoefficient;
            NoiseRecommitCooldownSeconds = RegisteredNoiseRecommitCooldownSeconds;
            DifficultyScalar = RegisteredDifficultyScalar;
            FsmIntervalSeconds = RegisteredFsmIntervalSeconds;
        }

        /// <summary>Selected immutable MVP or Target behavior profile.</summary>
        public NoiseResponseProfile ResponseProfile { get; }

        /// <summary>Virtual hearing cadence in seconds.</summary>
        public float HearingIntervalSeconds { get; }

        /// <summary>Maximum guards included in one hearing boundary.</summary>
        public int MaxHearingGuards { get; }

        /// <summary>Maximum raw facts included in one hearing boundary.</summary>
        public int MaxHearingFacts { get; }

        /// <summary>Maximum guard/fact pairs evaluated in one boundary.</summary>
        public int MaxHearingPairs { get; }

        /// <summary>Capacity of retained and deferred hearing work.</summary>
        public int HearingWorkCapacity { get; }

        /// <summary>Capacity of the raw facts accepted by Perception.</summary>
        public int RawFactQueueCapacity { get; }

        /// <summary>Capacity of relays retained while the Event Bus is full.</summary>
        public int RelayQueueCapacity { get; }

        /// <summary>Maximum due hearing boundaries retained during catch-up.</summary>
        public int MaxDeferredHearingBoundaries { get; }

        /// <summary>Maximum due hearing boundaries evaluated per clock callback.</summary>
        public int MaxHearingBoundariesPerFrame { get; }

        /// <summary>Maximum retries for bounded transport and handoff work.</summary>
        public int MaxRetries { get; }

        /// <summary>Gameplay comparison tolerance in metres.</summary>
        public float CompareToleranceMeters { get; }

        /// <summary>Relative tolerance for pure mathematical comparisons.</summary>
        public float MathRelativeTolerance { get; }

        /// <summary>Controller-authored raised feet origin offset in metres.</summary>
        public float F12NoiseOriginOffset { get; }

        /// <summary>Base Investigate give-up budget in seconds.</summary>
        public float InvestigateBaseSeconds { get; }

        /// <summary>Registered additive re-anchor extension in seconds.</summary>
        public float NoiseReanchorExtendSeconds { get; }

        /// <summary>Maximum residual distance used by the re-anchor budget.</summary>
        public float ReanchorMaxMeters { get; }

        /// <summary>Registered thoroughness coefficient for re-anchor timing.</summary>
        public float ThoroughnessCoefficient { get; }

        /// <summary>Registered cooldown before noise can recommit after resolution.</summary>
        public float NoiseRecommitCooldownSeconds { get; }

        /// <summary>Registered difficulty scalar for Investigate timing.</summary>
        public float DifficultyScalar { get; }

        /// <summary>Virtual cadence for vision/FSM state updates.</summary>
        public float FsmIntervalSeconds { get; }

        /// <summary>
        /// Validates a complete registry profile before any gameplay tick. The
        /// profile enum is deliberately not coerced when values are contradictory.
        /// </summary>
        public static bool TryValidate(NoiseResponseProfile responseProfile,
            float hearingIntervalSeconds, int maxHearingGuards,
            int maxHearingFacts, int maxHearingPairs, int hearingWorkCapacity,
            int maxRetries, float compareToleranceMeters,
            float mathRelativeTolerance, float f12NoiseOriginOffset,
            float investigateBaseSeconds, float noiseReanchorExtendSeconds,
            float reanchorMaxMeters, float thoroughnessCoefficient,
            out string errorCode)
        {
            return TryValidate(responseProfile, hearingIntervalSeconds,
                maxHearingGuards, maxHearingFacts, maxHearingPairs,
                hearingWorkCapacity, 128, hearingWorkCapacity, 4, 1,
                maxRetries, compareToleranceMeters, mathRelativeTolerance,
                f12NoiseOriginOffset, investigateBaseSeconds,
                noiseReanchorExtendSeconds, reanchorMaxMeters,
                thoroughnessCoefficient, out errorCode);
        }

        /// <summary>Validates all independent queue and scheduler capacities.</summary>
        public static bool TryValidate(NoiseResponseProfile responseProfile,
            float hearingIntervalSeconds, int maxHearingGuards,
            int maxHearingFacts, int maxHearingPairs, int hearingWorkCapacity,
            int rawFactQueueCapacity, int relayQueueCapacity,
            int maxDeferredHearingBoundaries, int maxHearingBoundariesPerFrame,
            int maxRetries, float compareToleranceMeters,
            float mathRelativeTolerance, float f12NoiseOriginOffset,
            float investigateBaseSeconds, float noiseReanchorExtendSeconds,
            float reanchorMaxMeters, float thoroughnessCoefficient,
            out string errorCode)
        {
            errorCode = string.Empty;
            if (responseProfile != NoiseResponseProfile.MVP
                && responseProfile != NoiseResponseProfile.Target)
            {
                errorCode = "noise-config-profile-invalid";
                return false;
            }
            if (!IsFinitePositive(hearingIntervalSeconds))
            {
                errorCode = "noise-config-hearing-interval-invalid";
                return false;
            }
            if (Math.Abs(hearingIntervalSeconds
                    - RegisteredHearingIntervalSeconds) > 0.000001f)
            {
                errorCode = "noise-config-hearing-interval-not-registered";
                return false;
            }
            if (maxHearingGuards <= 0 || maxHearingFacts <= 0
                || maxHearingPairs <= 0 || hearingWorkCapacity <= 0
                || rawFactQueueCapacity <= 0 || relayQueueCapacity <= 0
                || maxDeferredHearingBoundaries <= 0
                || maxHearingBoundariesPerFrame <= 0)
            {
                errorCode = "noise-config-hearing-cap-invalid";
                return false;
            }
            if (maxRetries < 0)
            {
                errorCode = "noise-config-retry-cap-invalid";
                return false;
            }
            if (!IsFinitePositive(compareToleranceMeters)
                || compareToleranceMeters > 0.005f)
            {
                errorCode = "noise-config-gameplay-tolerance-invalid";
                return false;
            }
            if (!IsFinitePositive(mathRelativeTolerance)
                || mathRelativeTolerance > 0.000001f)
            {
                errorCode = "noise-config-math-tolerance-invalid";
                return false;
            }
            if (!IsFiniteNonNegative(f12NoiseOriginOffset))
            {
                errorCode = "noise-config-origin-offset-invalid";
                return false;
            }
            if (Math.Abs(f12NoiseOriginOffset
                    - RegisteredF12NoiseOriginOffset) > 0.000001f)
            {
                errorCode = "noise-config-origin-offset-not-registered";
                return false;
            }
            if (!IsFinitePositive(investigateBaseSeconds)
                || !IsFiniteNonNegative(noiseReanchorExtendSeconds)
                || !IsFinitePositive(reanchorMaxMeters)
                || !IsFiniteNonNegative(thoroughnessCoefficient))
            {
                errorCode = "noise-config-tuning-invalid";
                return false;
            }
            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
