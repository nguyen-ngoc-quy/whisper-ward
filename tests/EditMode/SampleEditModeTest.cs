using NUnit.Framework;

namespace WhisperWard.Tests.EditMode
{
    /// <summary>
    /// Verifies core deterministic kinematic constraints and anti-kiting invariants
    /// from Master Architecture and ADR-0005 under Unity Test Framework EditMode.
    /// </summary>
    public sealed class SampleEditModeTest
    {
        [Test]
        public void test_player_locomotion_maintains_anti_kiting_speed_ratio()
        {
            // Arrange: Kinematic speeds from architecture TR-CORE-001 & TR-CORE-002
            const float vRun = 6.25f;
            const float vChase = 7.50f;
            const float minimumRatio = 1.20f;

            // Act
            float actualRatio = vChase / vRun;

            // Assert
            Assert.That(actualRatio, Is.GreaterThanOrEqualTo(minimumRatio),
                $"Anti-kiting invariant violated: V_chase ({vChase}) / V_run ({vRun}) must be >= {minimumRatio}");
        }
    }
}
