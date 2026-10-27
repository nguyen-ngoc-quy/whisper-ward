using NUnit.Framework;
using UnityEngine;
using WhisperWard.Foundation.Performance;

namespace WhisperWard.Tests.Unit.Performance
{
    [TestFixture]
    public class PerformanceBudgetValidatorTest
    {
        [Test]
        public void test_performance_budget_validates_60fps_envelope()
        {
            // Case 1: Healthy 14.5ms frame (approx 69 FPS) -> Passes
            bool passValid = PerformanceBudgetValidator.ValidateFrametime(14.5f, out string err1);
            Assert.IsTrue(passValid, err1);

            // Case 2: Degraded 18.2ms frame (approx 55 FPS) -> Fails
            bool passDegraded = PerformanceBudgetValidator.ValidateFrametime(18.2f, out string err2);
            Assert.IsFalse(passDegraded);
            Assert.IsNotEmpty(err2);
        }

        [Test]
        public void test_draw_call_limits_enforced_by_platform_target()
        {
            // Case 1: 250 draw calls on WebGL -> Passes (<= 300)
            bool passWebGL = PerformanceBudgetValidator.ValidateDrawCalls(250, RuntimePlatform.WebGLPlayer, out string errWebGL);
            Assert.IsTrue(passWebGL, errWebGL);

            // Case 2: 350 draw calls on WebGL -> Fails (> 300)
            bool failWebGL = PerformanceBudgetValidator.ValidateDrawCalls(350, RuntimePlatform.WebGLPlayer, out string errWebGLFail);
            Assert.IsFalse(failWebGL);
            Assert.IsNotEmpty(errWebGLFail);

            // Case 3: 450 draw calls on PC -> Passes (<= 1000)
            bool passPC = PerformanceBudgetValidator.ValidateDrawCalls(450, RuntimePlatform.WindowsPlayer, out string errPC);
            Assert.IsTrue(passPC, errPC);

            // Case 4: 1200 draw calls on PC -> Fails (> 1000)
            bool failPC = PerformanceBudgetValidator.ValidateDrawCalls(1200, RuntimePlatform.WindowsPlayer, out string errPCFail);
            Assert.IsFalse(failPC);
            Assert.IsNotEmpty(errPCFail);
        }

        [Test]
        public void test_ai_update_budget_enforces_2ms_threshold()
        {
            // Case 1: 0.85ms AI update across guards -> Passes
            bool passAI = PerformanceBudgetValidator.ValidateAIBudget(0.85f, out string errAI);
            Assert.IsTrue(passAI, errAI);

            // Case 2: 2.35ms AI update -> Fails (> 2.0ms)
            bool failAI = PerformanceBudgetValidator.ValidateAIBudget(2.35f, out string errAIFail);
            Assert.IsFalse(failAI);
            Assert.IsNotEmpty(errAIFail);
        }

        [Test]
        public void test_zero_gc_allocation_verified_on_simulation_tick()
        {
            // Case 1: Zero allocation -> Passes
            bool passGC = PerformanceBudgetValidator.ValidateZeroGC(0, out string errGC);
            Assert.IsTrue(passGC, errGC);

            // Case 2: 128 bytes garbage allocation -> Fails
            bool failGC = PerformanceBudgetValidator.ValidateZeroGC(128, out string errGCFail);
            Assert.IsFalse(failGC);
            Assert.IsNotEmpty(errGCFail);
        }

        [Test]
        public void test_comprehensive_metrics_evaluation()
        {
            // Arrange compliant metrics
            var healthyMetrics = new FrameMetrics(
                frameTimeMs: 15.0f,
                drawCalls: 220,
                aiTickDurationMs: 1.1f,
                gcAllocBytes: 0,
                targetPlatform: RuntimePlatform.WebGLPlayer);

            // Act & Assert
            bool passed = PerformanceBudgetValidator.EvaluateMetrics(healthyMetrics, out string failure);
            Assert.IsTrue(passed, failure);
        }
    }
}
