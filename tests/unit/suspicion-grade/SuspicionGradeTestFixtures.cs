using WhisperWard.AI.SuspicionGrade;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Deterministic test factories for the Suspicion/Grade configuration contract.
    /// Starter tuning belongs to this fixture as injected test data mirroring the
    /// registry entries; it is not a production fallback. Registry-to-runtime
    /// wiring is implemented by the Task 5 composition root.
    /// </summary>
    public static class SuspicionGradeTestFixtures
    {
        /// <summary>
        /// Creates the approved starter configuration using percentage-scaled test inputs
        /// for the existing accumulator and residual dependencies.
        /// </summary>
        public static SuspicionGradeConfiguration CreateStarterConfiguration(
            float tChase = 100f, float rMax = 100f, float tBase = 30f,
            float kResidual = 0.5f, float tFloor = 10f)
        {
            return new SuspicionGradeConfiguration(tBase, kResidual, tFloor,
                tChase, rMax, 25f, 10f, 50f, 10f, 90f, 90f, 75f, 60f,
                100f, 1);
        }

        /// <summary>
        /// Creates a starter configuration with caller-supplied grade thresholds.
        /// </summary>
        public static SuspicionGradeConfiguration CreateConfiguration(
            float gradeThresholdS, float gradeThresholdA, float gradeThresholdB,
            float tChase = 100f, float rMax = 100f, float tBase = 30f,
            float kResidual = 0.5f, float tFloor = 10f,
            float gradeWeightChase = 25f, float gradeWeightFruitless = 10f,
            float gradeWeightCapture = 50f, float gradeWeightResidual = 10f,
            float gradePenaltyCap = 90f, float meterDisplayScale = 100f,
            int gradeOperatorSchemaVersion = 1)
        {
            return new SuspicionGradeConfiguration(tBase, kResidual, tFloor,
                tChase, rMax, gradeWeightChase, gradeWeightFruitless,
                gradeWeightCapture, gradeWeightResidual, gradePenaltyCap,
                gradeThresholdS, gradeThresholdA, gradeThresholdB,
                meterDisplayScale, gradeOperatorSchemaVersion);
        }
    }
}
