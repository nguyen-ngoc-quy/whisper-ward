using System;
using NUnit.Framework;
using WhisperWard.AI.SuspicionGrade;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Verifies the immutable Suspicion/Grade configuration contract using
    /// injected fixture values that mirror the registry starter entries. These
    /// tests do not load the YAML registry; the registry-to-runtime wiring is
    /// implemented by the Task 5 composition root.
    /// </summary>
    public sealed class SuspicionGradeConfigurationTests
    {
        [Test]
        public void test_suspicion_grade_constructor_accepts_injected_starter_values()
        {
            // Arrange
            SuspicionGradeConfiguration configuration =
                SuspicionGradeTestFixtures.CreateStarterConfiguration();

            // Act and assert
            Assert.That(configuration.GradeWeightChase, Is.EqualTo(25f));
            Assert.That(configuration.GradeWeightFruitless, Is.EqualTo(10f));
            Assert.That(configuration.GradeWeightCapture, Is.EqualTo(50f));
            Assert.That(configuration.GradeWeightResidual, Is.EqualTo(10f));
            Assert.That(configuration.GradePenaltyCap, Is.EqualTo(90f));
            Assert.That(configuration.GradeThresholdS, Is.EqualTo(90f));
            Assert.That(configuration.GradeThresholdA, Is.EqualTo(75f));
            Assert.That(configuration.GradeThresholdB, Is.EqualTo(60f));
            Assert.That(configuration.MeterDisplayScale, Is.EqualTo(100f));
            Assert.That(configuration.GradeOperatorSchemaVersion, Is.EqualTo(1));
        }

        [Test]
        public void test_suspicion_grade_constructor_rejects_non_monotonic_grade_thresholds()
        {
            // Arrange, act, and assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(60f, 75f, 90f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(90f, 60f, 75f));
        }

        [Test]
        public void test_suspicion_grade_constructor_rejects_non_positive_chase_or_residual_max()
        {
            // Arrange, act, and assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    gradeThresholdS: 90f, gradeThresholdA: 75f,
                    gradeThresholdB: 60f, tChase: 0f));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    gradeThresholdS: 90f, gradeThresholdA: 75f,
                    gradeThresholdB: 60f, rMax: 0f));
        }

        [Test]
        public void test_suspicion_grade_constructor_rejects_negative_dependency_values()
        {
            // Arrange, act, and assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, tBase: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, kResidual: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, tFloor: -1f));
        }

        [Test]
        public void test_suspicion_grade_constructor_rejects_negative_grade_weights_or_penalty_cap()
        {
            // Arrange, act, and assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, gradeWeightChase: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, gradeWeightFruitless: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, gradeWeightCapture: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, gradeWeightResidual: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, gradePenaltyCap: -1f));
        }

        [Test]
        public void test_suspicion_grade_constructor_rejects_grade_thresholds_outside_zero_to_hundred()
        {
            // Arrange, act, and assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(-1f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(101f, 100f, 100f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(100f, 101f, 100f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(100f, 100f, -1f));
        }

        [Test]
        public void test_suspicion_grade_constructor_rejects_non_positive_meter_display_scale()
        {
            // Arrange, act, and assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, meterDisplayScale: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, meterDisplayScale: -1f));
        }

        [Test]
        public void test_suspicion_grade_constructor_rejects_non_finite_float_input()
        {
            // Arrange, act, and assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, tBase: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, tChase: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, gradeWeightCapture: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    float.PositiveInfinity, 75f, 60f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, meterDisplayScale: float.NaN));
        }

        [Test]
        public void test_suspicion_grade_constructor_rejects_non_positive_schema_version()
        {
            // Arrange, act, and assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, gradeOperatorSchemaVersion: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SuspicionGradeTestFixtures.CreateConfiguration(
                    90f, 75f, 60f, gradeOperatorSchemaVersion: -1));
        }
    }
}
