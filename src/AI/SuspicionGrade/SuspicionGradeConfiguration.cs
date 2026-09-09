using System;

namespace WhisperWard.AI.SuspicionGrade
{
    /// <summary>
    /// Immutable registry-backed contract for the Suspicion Meter and room Grade
    /// projections. Existing Perception values are supplied by the composition
    /// root; this type does not provide fallback tuning values.
    /// </summary>
    public sealed class SuspicionGradeConfiguration
    {
        /// <summary>
        /// Creates a validated Suspicion Meter and Grade configuration.
        /// </summary>
        /// <param name="tBase">Registry-owned base entry threshold.</param>
        /// <param name="kResidual">Registry-owned residual threshold coefficient.</param>
        /// <param name="tFloor">Registry-owned minimum entry threshold.</param>
        /// <param name="tChase">Registry-owned Chase threshold.</param>
        /// <param name="rMax">Registry-owned maximum residual value.</param>
        /// <param name="gradeWeightChase">Penalty for each distinct Chase entry.</param>
        /// <param name="gradeWeightFruitless">Penalty for each distinct fruitless resolution.</param>
        /// <param name="gradeWeightCapture">Diagnostic penalty weight for Capture.</param>
        /// <param name="gradeWeightResidual">Weight applied to normalized final residual.</param>
        /// <param name="gradePenaltyCap">Maximum incident penalty.</param>
        /// <param name="gradeThresholdS">Minimum quality score for grade S.</param>
        /// <param name="gradeThresholdA">Minimum quality score for grade A.</param>
        /// <param name="gradeThresholdB">Minimum quality score for grade B.</param>
        /// <param name="meterDisplayScale">Display scale for a normalized meter ratio.</param>
        /// <param name="gradeOperatorSchemaVersion">Version of the grade output contract.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when a value is non-finite, outside its contract domain, or the
        /// grade thresholds are not monotonic.
        /// </exception>
        public SuspicionGradeConfiguration(
            float tBase,
            float kResidual,
            float tFloor,
            float tChase,
            float rMax,
            float gradeWeightChase,
            float gradeWeightFruitless,
            float gradeWeightCapture,
            float gradeWeightResidual,
            float gradePenaltyCap,
            float gradeThresholdS,
            float gradeThresholdA,
            float gradeThresholdB,
            float meterDisplayScale,
            int gradeOperatorSchemaVersion)
        {
            RequireFiniteNonNegative(tBase, nameof(tBase));
            RequireFiniteNonNegative(kResidual, nameof(kResidual));
            RequireFiniteNonNegative(tFloor, nameof(tFloor));
            RequireFinitePositive(tChase, nameof(tChase));
            RequireFinitePositive(rMax, nameof(rMax));
            RequireFiniteNonNegative(gradeWeightChase, nameof(gradeWeightChase));
            RequireFiniteNonNegative(gradeWeightFruitless, nameof(gradeWeightFruitless));
            RequireFiniteNonNegative(gradeWeightCapture, nameof(gradeWeightCapture));
            RequireFiniteNonNegative(gradeWeightResidual, nameof(gradeWeightResidual));
            RequireFiniteNonNegative(gradePenaltyCap, nameof(gradePenaltyCap));
            RequireThreshold(gradeThresholdS, nameof(gradeThresholdS));
            RequireThreshold(gradeThresholdA, nameof(gradeThresholdA));
            RequireThreshold(gradeThresholdB, nameof(gradeThresholdB));
            if (gradeThresholdS < gradeThresholdA)
            {
                throw new ArgumentOutOfRangeException(nameof(gradeThresholdS),
                    "Grade thresholds must satisfy S >= A >= B.");
            }

            if (gradeThresholdA < gradeThresholdB)
            {
                throw new ArgumentOutOfRangeException(nameof(gradeThresholdA),
                    "Grade thresholds must satisfy S >= A >= B.");
            }

            RequireFinitePositive(meterDisplayScale, nameof(meterDisplayScale));
            if (gradeOperatorSchemaVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gradeOperatorSchemaVersion),
                    "The grade operator schema version must be positive.");
            }

            TBase = tBase;
            KResidual = kResidual;
            TFloor = tFloor;
            TChase = tChase;
            RMax = rMax;
            GradeWeightChase = gradeWeightChase;
            GradeWeightFruitless = gradeWeightFruitless;
            GradeWeightCapture = gradeWeightCapture;
            GradeWeightResidual = gradeWeightResidual;
            GradePenaltyCap = gradePenaltyCap;
            GradeThresholdS = gradeThresholdS;
            GradeThresholdA = gradeThresholdA;
            GradeThresholdB = gradeThresholdB;
            MeterDisplayScale = meterDisplayScale;
            GradeOperatorSchemaVersion = gradeOperatorSchemaVersion;
        }

        /// <summary>Registry-owned base entry threshold.</summary>
        public float TBase { get; }

        /// <summary>Registry-owned residual threshold coefficient.</summary>
        public float KResidual { get; }

        /// <summary>Registry-owned minimum entry threshold.</summary>
        public float TFloor { get; }

        /// <summary>Registry-owned Chase threshold consumed by the meter.</summary>
        public float TChase { get; }

        /// <summary>Registry-owned maximum residual value.</summary>
        public float RMax { get; }

        /// <summary>Penalty weight for each distinct Chase entry.</summary>
        public float GradeWeightChase { get; }

        /// <summary>Penalty weight for each distinct fruitless resolution.</summary>
        public float GradeWeightFruitless { get; }

        /// <summary>Diagnostic penalty weight for each distinct Capture.</summary>
        public float GradeWeightCapture { get; }

        /// <summary>Weight applied to normalized final residual.</summary>
        public float GradeWeightResidual { get; }

        /// <summary>Maximum incident penalty.</summary>
        public float GradePenaltyCap { get; }

        /// <summary>Minimum quality score for grade S.</summary>
        public float GradeThresholdS { get; }

        /// <summary>Minimum quality score for grade A.</summary>
        public float GradeThresholdA { get; }

        /// <summary>Minimum quality score for grade B.</summary>
        public float GradeThresholdB { get; }

        /// <summary>Display scale applied to the normalized meter ratio.</summary>
        public float MeterDisplayScale { get; }

        /// <summary>Version of the room-grade output contract.</summary>
        public int GradeOperatorSchemaVersion { get; }

        private static void RequireThreshold(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f || value > 100f)
            {
                throw new ArgumentOutOfRangeException(parameterName,
                    "Grade thresholds must be finite values in the range [0, 100].");
            }
        }

        private static void RequireFinitePositive(float value, string parameterName)
        {
            if (!IsFinite(value) || value <= 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName,
                    "The value must be finite and greater than zero.");
            }
        }

        private static void RequireFiniteNonNegative(float value, string parameterName)
        {
            if (!IsFinite(value) || value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName,
                    "The value must be finite and non-negative.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
