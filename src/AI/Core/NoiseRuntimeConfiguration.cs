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

        /// <summary>
        /// Registered maximum per-call gameplay-time advance in virtual seconds.
        /// The registry serializes this as max_frame_delta_s (0.0667 s).
        /// </summary>
        public const float RegisteredMaxFrameDeltaSeconds = 0.0667f;

        /// <summary>Registered F12 movement-origin lift in metres.</summary>
        public const float RegisteredF12NoiseOriginOffset = 0.25f;

        /// <summary>Registered Walk movement hearing radius in metres.</summary>
        public const float RegisteredWalkNoiseRadiusMeters = 4.0f;

        /// <summary>Registered Run movement hearing radius in metres.</summary>
        public const float RegisteredRunNoiseRadiusMeters = 6.0f;

        /// <summary>Registered soft vertical hearing cutoff in metres.</summary>
        public const float RegisteredHearingYHardCutoff = 4.0f;

        /// <summary>Registered standalone FSM response cadence in virtual seconds.</summary>
        public const float RegisteredFsmIntervalSeconds = 0.3f;

        /// <summary>Registered guard eye height in metres.</summary>
        public const float RegisteredGuardEyeHeightMeters = 1.6f;

        /// <summary>Registered Event Bus pending envelope capacity.</summary>
        public const int RegisteredEventBusCapacity = 256;

        /// <summary>Registered Event Bus retry cap.</summary>
        public const int RegisteredEventBusRetryAttempts = 3;

        /// <summary>Locked MVP guard-selection cap per hearing boundary.</summary>
        public const int RegisteredMvpMaxHearingGuards = 1;

        /// <summary>Registered Target guard-selection cap per hearing boundary.</summary>
        public const int RegisteredTargetMaxHearingGuards = 30;

        /// <summary>
        /// Compatibility alias for the Target multi-guard ceiling. MVP must use
        /// RegisteredMvpMaxHearingGuards and is rejected at any larger cap.
        /// </summary>
        [Obsolete("Use the profile-specific guard-cap constants.")]
        public const int RegisteredMaxHearingGuards = RegisteredTargetMaxHearingGuards;

        /// <summary>Locked MVP raw-fact evaluation cap per boundary.</summary>
        public const int RegisteredMvpMaxHearingFacts = 8;

        /// <summary>Locked MVP guard/fact pair cap per boundary.</summary>
        public const int RegisteredMvpMaxHearingPairs = 30;

        /// <summary>Locked MVP boundaries evaluated per callback.</summary>
        public const int RegisteredMvpMaxBoundariesPerFrame = 1;

        /// <summary>Locked MVP retry cap for bounded transport.</summary>
        public const int RegisteredMvpRetryAttempts = 3;

        /// <summary>Minimum supported retry cap for Target profiles.</summary>
        public const int RegisteredMinRetryAttempts = 1;

        /// <summary>Maximum supported retry cap for Target profiles.</summary>
        public const int RegisteredMaxRetryAttempts = 8;

        /// <summary>Minimum supported raw-fact evaluation cap.</summary>
        public const int RegisteredMinHearingFacts = 1;

        /// <summary>Maximum supported raw-fact evaluation cap.</summary>
        public const int RegisteredMaxHearingFacts = 32;

        /// <summary>Minimum supported guard/fact pair cap.</summary>
        public const int RegisteredMinHearingPairs = 1;

        /// <summary>Maximum supported guard/fact pair cap.</summary>
        public const int RegisteredMaxHearingPairs = 128;

        /// <summary>Minimum deferred hearing-work capacity.</summary>
        public const int RegisteredMinHearingWorkCapacity = 64;

        /// <summary>Maximum deferred hearing-work capacity.</summary>
        public const int RegisteredMaxHearingWorkCapacity = 4096;

        /// <summary>Registered raw-fact queue capacity.</summary>
        public const int RegisteredRawFactQueueCapacity = 128;

        /// <summary>Registered retained relay queue capacity.</summary>
        public const int RegisteredRelayQueueCapacity = 512;

        /// <summary>Minimum raw-fact queue capacity.</summary>
        public const int RegisteredMinRawFactQueueCapacity = 32;

        /// <summary>Maximum raw-fact queue capacity.</summary>
        public const int RegisteredMaxRawFactQueueCapacity = 512;

        /// <summary>Minimum retained relay queue capacity.</summary>
        public const int RegisteredMinRelayQueueCapacity = 64;

        /// <summary>Maximum retained relay queue capacity.</summary>
        public const int RegisteredMaxRelayQueueCapacity = 1024;

        /// <summary>Minimum deferred-boundary queue capacity.</summary>
        public const int RegisteredMinDeferredBoundaryCapacity = 1;

        /// <summary>Maximum deferred-boundary queue capacity.</summary>
        public const int RegisteredMaxDeferredBoundaryCapacity = 16;

        /// <summary>Maximum boundaries evaluated by one callback.</summary>
        public const int RegisteredMaxBoundariesPerFrame = 4;

        /// <summary>
        /// Registry value for <c>initial_overlap_result_capacity</c>: the
        /// bounded result buffer for the pre-spend initial-overlap probe.
        /// </summary>
        public const int RegisteredInitialOverlapBufferCapacity = 64;

        /// <summary>Minimum registered initial-overlap result capacity.</summary>
        public const int RegisteredMinInitialOverlapBufferCapacity = 16;

        /// <summary>Maximum registered initial-overlap result capacity.</summary>
        public const int RegisteredMaxInitialOverlapBufferCapacity = 128;

        /// <summary>Registered post-resolution noise recommit cooldown.</summary>
        public const float RegisteredNoiseRecommitCooldownSeconds = 2.0f;

        /// <summary>Registered suppressed micro-tell rate limit in seconds.</summary>
        public const float RegisteredMicroTellCooldownSeconds = 2.0f;

        /// <summary>Registered Chase give-up window in virtual seconds.</summary>
        public const float RegisteredChaseGiveupSeconds = 8.0f;

        /// <summary>Registered Chase duration cap in virtual seconds.</summary>
        public const float RegisteredChaseCapSeconds = 30.0f;

        /// <summary>Registered maximum Chase re-sight count.</summary>
        public const int RegisteredResightCap = 2;

        /// <summary>Registered minimum re-sight interval in virtual seconds.</summary>
        public const float RegisteredResightMinimumSeconds = 1.0f;

        /// <summary>Registered catch confirmation duration in virtual seconds.</summary>
        public const float RegisteredCatchSeconds = 1.0f;

        /// <summary>Registered catch range in metres.</summary>
        public const float RegisteredCatchRangeMeters = 5.5f;

        /// <summary>Registered vertical catch tolerance in metres.</summary>
        public const float RegisteredDeltaYToleranceMeters = 1.0f;

        /// <summary>Registered catch hysteresis margin in metres.</summary>
        public const float RegisteredHysteresisMeters = 0.5f;

        /// <summary>Registry-locked NavMesh sample distance in metres.</summary>
        public const float RegisteredNavMeshSampleMaxDistanceMeters = 0.4f;

        /// <summary>Registered per-arc Investigate sweep duration.</summary>
        public const float RegisteredSweepBaseSeconds = 0.8f;

        /// <summary>Registry-locked Investigate sweep reference count.</summary>
        public const int RegisteredSweepReferenceCount = 4;

        /// <summary>Registered Investigate target error radius in metres.</summary>
        public const float RegisteredInvestigateErrorRadiusMeters = 1.5f;

        /// <summary>Registered XZ tolerance for the Investigate arrival arm.</summary>
        public const float RegisteredInvestigateArrivalToleranceMeters = 0.3f;

        /// <summary>Registered remaining-distance tolerance for path-end arming.</summary>
        public const float RegisteredInvestigatePathEndToleranceMeters = 0.1f;

        /// <summary>Registered speed threshold for path-end arming.</summary>
        public const float RegisteredInvestigatePathEndSpeedMetersPerSecond = 0.1f;

        /// <summary>Registered consecutive path-end observations required to arm.</summary>
        public const int RegisteredInvestigatePathEndTicks = 2;

        /// <summary>
        /// Registered maximum continuous navigation suppression before the
        /// Investigate search arm force-fires fail-closed toward give-up.
        /// </summary>
        public const float RegisteredInvestigatePathEndSuppressMaxSeconds = 1.0f;

        /// <summary>Registered HideSpot-front verification dwell.</summary>
        public const float RegisteredSpotFrontVerifySeconds = 1.5f;

        /// <summary>Registered patrol movement speed in metres per second.</summary>
        public const float RegisteredPatrolSpeedMetersPerSecond = 2.3f;

        /// <summary>Registered Investigate movement speed in metres per second.</summary>
        public const float RegisteredInvestigateSpeedMetersPerSecond = 5.0f;

        /// <summary>Registered Chase movement speed in metres per second.</summary>
        public const float RegisteredChaseSpeedMetersPerSecond = 7.5f;

        /// <summary>Registry-locked NavMesh stopping distance in metres.</summary>
        public const float RegisteredStoppingDistanceMeters = 0f;

        /// <summary>Registered same-episode corroboration window.</summary>
        public const float RegisteredNoiseCorroborateWindowSeconds = 6.0f;

        /// <summary>Registered same-area corroboration radius in metres.</summary>
        public const float RegisteredNoiseCorroborateRadiusMeters = 1.5f;

        /// <summary>Registered base Investigate give-up budget.</summary>
        public const float RegisteredInvestigateBaseSeconds = 4.0f;

        /// <summary>Registered additive noise re-anchor extension.</summary>
        public const float RegisteredNoiseReanchorExtendSeconds = 2.0f;

        /// <summary>Registered maximum re-anchor residual distance.</summary>
        public const float RegisteredReanchorMaxMeters = 1.0f;

        /// <summary>Registered Investigate thoroughness coefficient.</summary>
        public const float RegisteredThoroughnessCoefficient = 0.50f;

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
                RegisteredRawFactQueueCapacity, RegisteredRelayQueueCapacity,
                4, 1,
                maxRetries,
                compareToleranceMeters, mathRelativeTolerance,
                f12NoiseOriginOffset, investigateBaseSeconds,
                noiseReanchorExtendSeconds, reanchorMaxMeters,
                thoroughnessCoefficient, RegisteredNoiseCorroborateWindowSeconds,
                RegisteredNoiseCorroborateRadiusMeters)
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
            float reanchorMaxMeters, float thoroughnessCoefficient,
            float noiseCorroborateWindowSeconds,
            float noiseCorroborateRadiusMeters)
        {
            string errorCode;
            if (!TryValidate(responseProfile, hearingIntervalSeconds,
                maxHearingGuards, maxHearingFacts, maxHearingPairs,
                hearingWorkCapacity, rawFactQueueCapacity, relayQueueCapacity,
                maxDeferredHearingBoundaries, maxHearingBoundariesPerFrame,
                maxRetries, compareToleranceMeters, mathRelativeTolerance,
                f12NoiseOriginOffset, investigateBaseSeconds,
                noiseReanchorExtendSeconds, reanchorMaxMeters,
                thoroughnessCoefficient, noiseCorroborateWindowSeconds,
                noiseCorroborateRadiusMeters, out errorCode))
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
            NoiseCorroborateWindowSeconds = noiseCorroborateWindowSeconds;
            NoiseCorroborateRadiusMeters = noiseCorroborateRadiusMeters;
            HearingYHardCutoffMeters = RegisteredHearingYHardCutoff;
            WalkNoiseRadiusMeters = RegisteredWalkNoiseRadiusMeters;
            RunNoiseRadiusMeters = RegisteredRunNoiseRadiusMeters;
            NoiseRecommitCooldownSeconds = RegisteredNoiseRecommitCooldownSeconds;
            MicroTellCooldownSeconds = RegisteredMicroTellCooldownSeconds;
            DifficultyScalar = RegisteredDifficultyScalar;
            FsmIntervalSeconds = RegisteredFsmIntervalSeconds;
            GuardEyeHeightMeters = RegisteredGuardEyeHeightMeters;
            ChaseGiveupSeconds = RegisteredChaseGiveupSeconds;
            ChaseCapSeconds = RegisteredChaseCapSeconds;
            ResightCap = RegisteredResightCap;
            ResightMinimumSeconds = RegisteredResightMinimumSeconds;
            CatchSeconds = RegisteredCatchSeconds;
            CatchRangeMeters = RegisteredCatchRangeMeters;
            DeltaYToleranceMeters = RegisteredDeltaYToleranceMeters;
            HysteresisMeters = RegisteredHysteresisMeters;
            NavMeshSampleMaxDistanceMeters = RegisteredNavMeshSampleMaxDistanceMeters;
            SweepBaseSeconds = RegisteredSweepBaseSeconds;
            SweepReferenceCount = RegisteredSweepReferenceCount;
            InvestigateErrorRadiusMeters = RegisteredInvestigateErrorRadiusMeters;
            SpotFrontVerifySeconds = RegisteredSpotFrontVerifySeconds;
            PatrolSpeedMetersPerSecond = RegisteredPatrolSpeedMetersPerSecond;
            InvestigateSpeedMetersPerSecond = RegisteredInvestigateSpeedMetersPerSecond;
            ChaseSpeedMetersPerSecond = RegisteredChaseSpeedMetersPerSecond;
            StoppingDistanceMeters = RegisteredStoppingDistanceMeters;
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

        /// <summary>Registered vertical hearing cutoff in metres.</summary>
        public float HearingYHardCutoffMeters { get; }

        /// <summary>Registered Walk movement hearing radius in metres.</summary>
        public float WalkNoiseRadiusMeters { get; }

        /// <summary>Registered Run movement hearing radius in metres.</summary>
        public float RunNoiseRadiusMeters { get; }

        /// <summary>Registered cooldown before noise can recommit after resolution.</summary>
        public float NoiseRecommitCooldownSeconds { get; }

        /// <summary>Registered suppressed micro-tell rate limit in seconds.</summary>
        public float MicroTellCooldownSeconds { get; }

        /// <summary>Same-episode corroboration window in virtual seconds.</summary>
        public float NoiseCorroborateWindowSeconds { get; }

        /// <summary>Same-area corroboration radius in metres.</summary>
        public float NoiseCorroborateRadiusMeters { get; }

        /// <summary>Registered difficulty scalar for Investigate timing.</summary>
        public float DifficultyScalar { get; }

        /// <summary>Virtual cadence for standalone FSM response updates.</summary>
        public float FsmIntervalSeconds { get; }

        /// <summary>Registered guard eye height in metres.</summary>
        public float GuardEyeHeightMeters { get; }

        /// <summary>Chase give-up window in virtual seconds.</summary>
        public float ChaseGiveupSeconds { get; }

        /// <summary>Chase duration cap in virtual seconds.</summary>
        public float ChaseCapSeconds { get; }

        /// <summary>Maximum Chase re-sight count.</summary>
        public int ResightCap { get; }

        /// <summary>Minimum re-sight interval in virtual seconds.</summary>
        public float ResightMinimumSeconds { get; }

        /// <summary>Catch confirmation duration in virtual seconds.</summary>
        public float CatchSeconds { get; }

        /// <summary>Catch range in metres.</summary>
        public float CatchRangeMeters { get; }

        /// <summary>Vertical catch tolerance in metres.</summary>
        public float DeltaYToleranceMeters { get; }

        /// <summary>Catch hysteresis margin in metres.</summary>
        public float HysteresisMeters { get; }

        /// <summary>NavMesh sample distance in metres.</summary>
        public float NavMeshSampleMaxDistanceMeters { get; }

        /// <summary>Per-arc Investigate sweep duration.</summary>
        public float SweepBaseSeconds { get; }

        /// <summary>Reference count for Investigate sweep thoroughness.</summary>
        public int SweepReferenceCount { get; }

        /// <summary>Investigate target error radius in metres.</summary>
        public float InvestigateErrorRadiusMeters { get; }

        /// <summary>HideSpot-front verification dwell in virtual seconds.</summary>
        public float SpotFrontVerifySeconds { get; }

        /// <summary>Patrol movement speed in metres per second.</summary>
        public float PatrolSpeedMetersPerSecond { get; }

        /// <summary>Investigate movement speed in metres per second.</summary>
        public float InvestigateSpeedMetersPerSecond { get; }

        /// <summary>Chase movement speed in metres per second.</summary>
        public float ChaseSpeedMetersPerSecond { get; }

        /// <summary>NavMesh stopping distance in metres.</summary>
        public float StoppingDistanceMeters { get; }

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
                hearingWorkCapacity, RegisteredRawFactQueueCapacity,
                RegisteredRelayQueueCapacity, 4, 1,
                maxRetries, compareToleranceMeters, mathRelativeTolerance,
                f12NoiseOriginOffset, investigateBaseSeconds,
                noiseReanchorExtendSeconds, reanchorMaxMeters,
                thoroughnessCoefficient, RegisteredNoiseCorroborateWindowSeconds,
                RegisteredNoiseCorroborateRadiusMeters, out errorCode);
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
            float noiseCorroborateWindowSeconds,
            float noiseCorroborateRadiusMeters, out string errorCode)
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
            int registeredGuardCap = responseProfile == NoiseResponseProfile.MVP
                ? RegisteredMvpMaxHearingGuards
                : RegisteredTargetMaxHearingGuards;
            if (maxHearingGuards != registeredGuardCap)
            {
                errorCode = responseProfile == NoiseResponseProfile.MVP
                    ? "noise-config-mvp-guard-cap-not-registered"
                    : "noise-config-target-guard-cap-not-registered";
                return false;
            }
            if (maxHearingFacts < RegisteredMinHearingFacts
                || maxHearingFacts > RegisteredMaxHearingFacts)
            {
                errorCode = "noise-config-fact-cap-invalid";
                return false;
            }
            if (maxHearingPairs < RegisteredMinHearingPairs
                || maxHearingPairs > RegisteredMaxHearingPairs)
            {
                errorCode = "noise-config-pair-cap-invalid";
                return false;
            }
            if (hearingWorkCapacity < RegisteredMinHearingWorkCapacity
                || hearingWorkCapacity > RegisteredMaxHearingWorkCapacity)
            {
                errorCode = "noise-config-work-cap-invalid";
                return false;
            }
            if (rawFactQueueCapacity < RegisteredMinRawFactQueueCapacity
                || rawFactQueueCapacity > RegisteredMaxRawFactQueueCapacity)
            {
                errorCode = "noise-config-raw-queue-cap-invalid";
                return false;
            }
            if (relayQueueCapacity < RegisteredMinRelayQueueCapacity
                || relayQueueCapacity > RegisteredMaxRelayQueueCapacity)
            {
                errorCode = "noise-config-relay-queue-cap-invalid";
                return false;
            }
            if (maxDeferredHearingBoundaries
                    < RegisteredMinDeferredBoundaryCapacity
                || maxDeferredHearingBoundaries
                    > RegisteredMaxDeferredBoundaryCapacity)
            {
                errorCode = "noise-config-deferred-boundary-cap-invalid";
                return false;
            }
            if (maxHearingBoundariesPerFrame <= 0
                || maxHearingBoundariesPerFrame
                    > RegisteredMaxBoundariesPerFrame)
            {
                errorCode = "noise-config-boundary-frame-cap-invalid";
                return false;
            }
            if (maxRetries < RegisteredMinRetryAttempts
                || maxRetries > RegisteredMaxRetryAttempts)
            {
                errorCode = "noise-config-retry-cap-invalid";
                return false;
            }
            if (responseProfile == NoiseResponseProfile.MVP
                && maxHearingFacts != RegisteredMvpMaxHearingFacts)
            {
                errorCode = "noise-config-fact-cap-not-registered";
                return false;
            }
            if (responseProfile == NoiseResponseProfile.MVP
                && maxHearingPairs != RegisteredMvpMaxHearingPairs)
            {
                errorCode = "noise-config-pair-cap-not-registered";
                return false;
            }
            if (responseProfile == NoiseResponseProfile.MVP
                && maxHearingBoundariesPerFrame
                    != RegisteredMvpMaxBoundariesPerFrame)
            {
                errorCode = "noise-config-boundary-frame-cap-not-registered";
                return false;
            }
            if (responseProfile == NoiseResponseProfile.MVP
                && maxRetries != RegisteredMvpRetryAttempts)
            {
                errorCode = "noise-config-retry-cap-not-registered";
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
            if (!IsFinitePositive(noiseCorroborateWindowSeconds)
                || Math.Abs(noiseCorroborateWindowSeconds
                    - RegisteredNoiseCorroborateWindowSeconds) > 0.000001f
                || !IsFinitePositive(noiseCorroborateRadiusMeters)
                || Math.Abs(noiseCorroborateRadiusMeters
                    - RegisteredNoiseCorroborateRadiusMeters) > 0.000001f)
            {
                errorCode = "noise-config-corroboration-not-registered";
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
