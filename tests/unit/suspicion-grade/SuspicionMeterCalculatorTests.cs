using NUnit.Framework;
using WhisperWard.AI.SuspicionGrade;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Verifies the pure per-guard Suspicion Meter projection.
    /// </summary>
    public sealed class SuspicionMeterCalculatorTests
    {
        [TestCase(0f, 0)] // SG-AC1: lower clamp boundary.
        [TestCase(50f, 50)] // SG-AC1: normalized meter projection.
        [TestCase(100f, 100)] // SG-AC1: Chase threshold boundary.
        [TestCase(125f, 100)] // SG-AC1: upper clamp boundary.
        public void test_suspicion_meter_calculate_clamps_percent_to_zero_through_one_hundred(
            float accumulator, int expectedPercent)
        {
            // Arrange
            var snapshot = CreateSnapshot(
                accumulator, residual: 0f, tChase: 100f,
                entryThreshold: 25f, guardState: "Patrol");
            var configuration = SuspicionGradeTestFixtures.CreateStarterConfiguration();

            // Act
            var model = SuspicionMeterCalculator.Calculate(snapshot, configuration);

            // Assert
            Assert.That(model.IsAvailable, Is.True);
            Assert.That(model.MeterPercent, Is.EqualTo(expectedPercent));
        }

        [Test]
        public void test_suspicion_meter_calculate_uses_snapshot_chase_threshold()
        {
            // Arrange — SG-AC2: the authoritative threshold is supplied by the snapshot.
            var snapshot = CreateSnapshot(
                accumulator: 40f, residual: 0f, tChase: 80f,
                entryThreshold: 20f, guardState: "Investigate");
            var configuration = SuspicionGradeTestFixtures.CreateStarterConfiguration(
                tChase: 100f);

            // Act
            var model = SuspicionMeterCalculator.Calculate(snapshot, configuration);

            // Assert
            Assert.That(model.MeterPercent, Is.EqualTo(50));
            Assert.That(model.TChase, Is.EqualTo(80f));
        }

        [Test]
        public void test_suspicion_meter_calculate_marks_missing_accumulator_or_threshold_unavailable()
        {
            // Arrange
            var missingAccumulator = CreateSnapshotWithoutAccumulator();
            var missingThreshold = CreateSnapshotWithoutChaseThreshold();
            var configuration = SuspicionGradeTestFixtures.CreateStarterConfiguration();

            // Act
            var missingAccumulatorModel = SuspicionMeterCalculator.Calculate(
                missingAccumulator, configuration);
            var missingThresholdModel = SuspicionMeterCalculator.Calculate(
                missingThreshold, configuration);

            // Assert
            Assert.That(missingAccumulatorModel.IsAvailable, Is.False);
            Assert.That(missingAccumulatorModel.Region, Is.EqualTo(SuspicionMeterRegion.Unavailable));
            Assert.That(missingThresholdModel.IsAvailable, Is.False);
            Assert.That(missingThresholdModel.Region, Is.EqualTo(SuspicionMeterRegion.Unavailable));
        }

        [Test]
        public void test_suspicion_meter_calculate_separates_residual_and_selects_investigate_region()
        {
            // Arrange
            var configuration = SuspicionGradeTestFixtures.CreateStarterConfiguration(
                tChase: 100f, tBase: 30f, tFloor: 10f, kResidual: 0.5f);
            var snapshot = CreateSnapshot(
                accumulator: 20f, residual: 20f, tChase: 100f,
                entryThreshold: 20f, guardState: "Investigate");

            // Act
            var model = SuspicionMeterCalculator.Calculate(snapshot, configuration);

            // Assert
            Assert.That(model.MeterPercent, Is.EqualTo(20));
            Assert.That(model.RCurrent, Is.EqualTo(20f));
            Assert.That(model.Region, Is.EqualTo(SuspicionMeterRegion.Investigate));
        }

        [Test]
        public void test_suspicion_meter_calculate_selects_chase_for_authoritative_chase_state()
        {
            // Arrange
            var snapshot = CreateSnapshot(
                accumulator: 10f, residual: 4f, tChase: 100f,
                entryThreshold: 20f, guardState: "Chase");
            var configuration = SuspicionGradeTestFixtures.CreateStarterConfiguration();

            // Act
            var model = SuspicionMeterCalculator.Calculate(snapshot, configuration);

            // Assert
            Assert.That(model.Region, Is.EqualTo(SuspicionMeterRegion.Chase));
            Assert.That(model.GuardState, Is.EqualTo("Chase"));
        }

        [Test]
        public void test_suspicion_meter_calculate_copies_causal_fields_without_mutation()
        {
            // Arrange
            var snapshot = CreateSnapshot(
                accumulator: 15f, residual: 7f, tChase: 100f,
                entryThreshold: 20f, guardState: "Patrol",
                lastCausalEvent: "noise-heard", sourceEntryId: "entry-1");
            var configuration = SuspicionGradeTestFixtures.CreateStarterConfiguration();

            // Act
            var model = SuspicionMeterCalculator.Calculate(snapshot, configuration);

            // Assert
            Assert.That(model.LastCausalEvent, Is.EqualTo("noise-heard"));
            Assert.That(model.SourceEntryId, Is.EqualTo("entry-1"));
            Assert.That(model.RCurrent, Is.EqualTo(7f));
            Assert.That(snapshot.ACurrent, Is.EqualTo(15f));
            Assert.That(snapshot.RCurrent, Is.EqualTo(7f));
        }

        private static GuardSuspicionSnapshot CreateSnapshot(
            float accumulator, float residual, float tChase,
            float entryThreshold, string guardState,
            string lastCausalEvent = "", string sourceEntryId = "")
        {
            return new GuardSuspicionSnapshot(
                new SuspicionGradeIdentity("session", 1L, "room", "guard-1"),
                true, accumulator,
                true, residual,
                true, tChase,
                entryThreshold, guardState, lastCausalEvent, sourceEntryId);
        }

        private static GuardSuspicionSnapshot CreateSnapshotWithoutAccumulator()
        {
            return new GuardSuspicionSnapshot(
                new SuspicionGradeIdentity("session", 1L, "room", "guard-1"),
                false, 0f,
                true, 0f,
                true, 100f,
                25f, "Patrol", "", "");
        }

        private static GuardSuspicionSnapshot CreateSnapshotWithoutChaseThreshold()
        {
            return new GuardSuspicionSnapshot(
                new SuspicionGradeIdentity("session", 1L, "room", "guard-1"),
                true, 0f,
                true, 0f,
                false, 0f,
                25f, "Patrol", "", "");
        }
    }
}
