using System;

namespace WhisperWard.AI.SuspicionGrade
{
    /// <summary>
    /// Pure calculator for projecting authoritative per-guard suspicion data into
    /// a read-only meter model.
    /// </summary>
    public static class SuspicionMeterCalculator
    {
        /// <summary>
        /// Calculates a read-only meter projection without mutating or recalculating
        /// Perception or Guard FSM state.
        /// </summary>
        /// <param name="snapshot">Authoritative per-guard suspicion snapshot.</param>
        /// <param name="configuration">Injected Suspicion/Grade configuration.</param>
        /// <returns>A meter read model, or an unavailable model when required input is missing.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configuration" /> is null.
        /// </exception>
        public static SuspicionMeterReadModel Calculate(
            GuardSuspicionSnapshot snapshot,
            SuspicionGradeConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (!snapshot.HasAccumulator
                || !snapshot.HasChaseThreshold
                || !IsFinite(snapshot.ACurrent)
                || !IsFinite(snapshot.TChase)
                || snapshot.TChase <= 0f)
            {
                return CreateUnavailableModel(snapshot);
            }

            float ratio = snapshot.ACurrent / snapshot.TChase;
            ratio = Clamp01(ratio);

            double scaledMeter = ratio * configuration.MeterDisplayScale;
            int meterPercent = (int)Math.Round(scaledMeter, MidpointRounding.AwayFromZero);
            SuspicionMeterRegion region = SelectRegion(snapshot);

            return new SuspicionMeterReadModel(
                snapshot.Identity,
                true,
                snapshot.ACurrent,
                snapshot.TChase,
                ratio,
                meterPercent,
                region,
                snapshot.RCurrent,
                snapshot.GuardState,
                snapshot.LastCausalEvent,
                snapshot.SourceEntryId);
        }

        private static SuspicionMeterReadModel CreateUnavailableModel(
            GuardSuspicionSnapshot snapshot)
        {
            return new SuspicionMeterReadModel(
                snapshot.Identity,
                false,
                snapshot.ACurrent,
                snapshot.TChase,
                0f,
                0,
                SuspicionMeterRegion.Unavailable,
                snapshot.RCurrent,
                snapshot.GuardState,
                snapshot.LastCausalEvent,
                snapshot.SourceEntryId);
        }

        private static SuspicionMeterRegion SelectRegion(GuardSuspicionSnapshot snapshot)
        {
            if (string.Equals(snapshot.GuardState, "Chase", StringComparison.Ordinal)
                || snapshot.ACurrent >= snapshot.TChase)
            {
                return SuspicionMeterRegion.Chase;
            }

            if (snapshot.ACurrent >= snapshot.InvestigationEntryThreshold)
            {
                return SuspicionMeterRegion.Investigate;
            }

            return SuspicionMeterRegion.Quiet;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
