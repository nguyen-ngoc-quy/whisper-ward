using NUnit.Framework;
using UnityEngine;
using WhisperWard.AI.Perception;

namespace WhisperWard.Tests.Unit.AI
{
    [TestFixture]
    public class SuspicionAccumulatorTest
    {
        private GameObject _guardObject;
        private SuspicionAccumulator _accumulator;

        [SetUp]
        public void SetUp()
        {
            _guardObject = new GameObject("Guard_Accumulator_Test");
            _accumulator = _guardObject.AddComponent<SuspicionAccumulator>();
            _accumulator.Configure(
                kRate: 1.5f,
                maxRate: 0.60f,
                tBase: 0.30f,
                kRes: 0.24f,
                residualRate: 0.10f,
                residualTau: 16.0f,
                confirmWindowDuration: 1.2f,
                cancelCap: 3,
                tChase: 1.00f,
                deltaReach: 0.02f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_guardObject != null)
            {
                Object.DestroyImmediate(_guardObject);
            }
        }

        [Test]
        public void test_suspicion_accumulator_distance_inverse_charge_rate()
        {
            // Arrange: d = 3.0m -> rate = 1.5 / 3.0 = 0.50 A/s (AC-GUARD-05)
            float dt = 0.40f;

            // Act
            _accumulator.Tick(dt, hasLOS: true, distance: 3.0f, isReachable: true);

            // Assert: A = 0.50 * 0.40 = 0.20
            Assert.AreEqual(0.20f, _accumulator.Accumulator, 0.001f, "At 3m, charge rate must be 0.50 A/s.");
        }

        [Test]
        public void test_suspicion_accumulator_close_range_rate_clamped_to_max_rate()
        {
            // Arrange: d = 2.0m -> k/d = 1.5 / 2.0 = 0.75 A/s > maxRate 0.60 A/s (AC-GUARD-05)
            float dt = 0.50f;

            // Act
            _accumulator.Tick(dt, hasLOS: true, distance: 2.0f, isReachable: true);

            // Assert: A = 0.60 * 0.50 = 0.30
            Assert.AreEqual(0.30f, _accumulator.Accumulator, 0.001f, "Close range charge rate must clamp to 0.60 A/s.");
        }

        [Test]
        public void test_suspicion_accumulator_los_break_resets_accumulator_immediately()
        {
            // Arrange: Charge accumulator to 0.25 (AC-GUARD-06)
            _accumulator.Tick(0.50f, hasLOS: true, distance: 3.0f);
            Assert.Greater(_accumulator.Accumulator, 0f);

            // Act: LOS breaks
            _accumulator.Tick(0.20f, hasLOS: false, distance: 3.0f);

            // Assert: Accumulator immediately resets to 0.0
            Assert.AreEqual(0.0f, _accumulator.Accumulator, 0.0001f, "Accumulator must immediately reset to 0 on LOS break.");
        }

        [Test]
        public void test_suspicion_accumulator_los_break_preserves_residual_wariness()
        {
            // Arrange: Expose guard for 2 seconds (AC-GUARD-06)
            // Residual charges at 0.10 /s -> R = 0.20
            _accumulator.Tick(2.0f, hasLOS: true, distance: 5.0f);
            float residualAtBreak = _accumulator.Residual;
            Assert.AreEqual(0.20f, residualAtBreak, 0.005f);

            // Act: LOS breaks for 1 tick (0.10s)
            _accumulator.Tick(0.10f, hasLOS: false, distance: 5.0f);

            // Assert: Residual is preserved and decays slightly: R = 0.20 * exp(-0.1 / 16) = 0.19875
            Assert.Greater(_accumulator.Residual, 0.19f, "Residual must not reset to 0 on LOS break.");
            Assert.LessOrEqual(_accumulator.Residual, residualAtBreak, "Residual should begin exponential decay.");
        }

        [Test]
        public void test_suspicion_accumulator_confirm_window_opens_and_commits_on_sustained_los()
        {
            // Arrange: T_base = 0.30 (AC-GUARD-07)
            bool windowOpened = false;
            bool windowCommitted = false;
            _accumulator.OnConfirmWindowOpened += (a, t) => windowOpened = true;
            _accumulator.OnConfirmWindowElapsed += () => windowCommitted = true;

            // Step 1: Charge to 0.30 at max rate (0.60 A/s * 0.5s = 0.30)
            _accumulator.Tick(0.50f, hasLOS: true, distance: 2.0f);

            Assert.IsTrue(windowOpened, "Window must open when A reaches T_entry (0.30).");
            Assert.IsTrue(_accumulator.IsWindowOpen);
            Assert.IsFalse(windowCommitted, "Commit must not fire immediately.");

            // Step 2: Sustain LOS for 1.2s confirm window duration
            _accumulator.Tick(0.60f, hasLOS: true, distance: 2.0f);
            Assert.IsFalse(windowCommitted, "Commit must not fire halfway through window.");

            _accumulator.Tick(0.60f, hasLOS: true, distance: 2.0f);
            Assert.IsTrue(windowCommitted, "Commit must fire after full 1.2s sustained LOS.");
            Assert.IsFalse(_accumulator.IsWindowOpen, "Window closes upon commit.");
        }

        [Test]
        public void test_suspicion_accumulator_confirm_window_cancels_on_los_break_and_increments_counter()
        {
            // Arrange (AC-GUARD-07)
            bool windowCancelled = false;
            int cancelCountReported = 0;
            _accumulator.OnConfirmWindowCancelled += (isPlayer, count) =>
            {
                windowCancelled = true;
                cancelCountReported = count;
            };

            // Open window
            _accumulator.Tick(0.50f, hasLOS: true, distance: 2.0f);
            Assert.IsTrue(_accumulator.IsWindowOpen);

            // Act: Break LOS before window finishes (player ducked cover)
            _accumulator.Tick(0.20f, hasLOS: false, distance: 2.0f, isReachable: true, isPlayerCausedBreak: true);

            // Assert
            Assert.IsTrue(windowCancelled, "Window must cancel on LOS break.");
            Assert.IsFalse(_accumulator.IsWindowOpen);
            Assert.AreEqual(1, _accumulator.CancelCounter, "Player-caused cancellation must increment counter.");
            Assert.AreEqual(1, cancelCountReported);
            Assert.AreEqual(0f, _accumulator.Accumulator, "A must reset on cancel.");
        }

        [Test]
        public void test_suspicion_accumulator_guard_sweep_does_not_increment_cancel_counter()
        {
            // Arrange: Guard turned away (not player-caused)
            _accumulator.Tick(0.50f, hasLOS: true, distance: 2.0f);
            Assert.IsTrue(_accumulator.IsWindowOpen);

            // Act: Break LOS with isPlayerCausedBreak = false
            _accumulator.Tick(0.20f, hasLOS: false, distance: 2.0f, isReachable: true, isPlayerCausedBreak: false);

            // Assert
            Assert.AreEqual(0, _accumulator.CancelCounter, "Guard-sweep break must NOT increment cancel counter.");
        }

        [Test]
        public void test_suspicion_accumulator_residual_sinks_entry_threshold_to_floor()
        {
            // Arrange: T_base = 0.30, k_res = 0.24, T_floor = 0.06 (AC-GUARD-08)
            // At R = 0.0: T_entry = 0.30
            Assert.AreEqual(0.30f, _accumulator.EntryThreshold, 0.001f);

            // At R = 0.5: T_entry = 0.30 - 0.24 * 0.5 = 0.18
            _accumulator.SetResidual(0.50f);
            Assert.AreEqual(0.18f, _accumulator.EntryThreshold, 0.001f);

            // At R = 1.0: T_entry = max(0.30 - 0.24 * 1.0, 0.06) = 0.06 (floor)
            _accumulator.SetResidual(1.00f);
            Assert.AreEqual(0.06f, _accumulator.EntryThreshold, 0.001f, "Entry threshold must sink to 0.06 floor at full residual.");
        }

        [Test]
        public void test_suspicion_accumulator_cancel_cap_triggers_forced_investigate()
        {
            // Arrange (AC-GUARD-08): Cancel cap = 3
            bool capReachedFired = false;
            _accumulator.OnCapReached += () => capReachedFired = true;

            // Cancel 1
            _accumulator.Tick(0.50f, hasLOS: true, distance: 2.0f);
            _accumulator.Tick(0.20f, hasLOS: false, distance: 2.0f, isReachable: true, isPlayerCausedBreak: true);
            Assert.AreEqual(1, _accumulator.CancelCounter);
            Assert.IsFalse(capReachedFired);

            // Cancel 2
            _accumulator.Tick(0.50f, hasLOS: true, distance: 2.0f);
            _accumulator.Tick(0.20f, hasLOS: false, distance: 2.0f, isReachable: true, isPlayerCausedBreak: true);
            Assert.AreEqual(2, _accumulator.CancelCounter);
            Assert.IsFalse(capReachedFired);

            // Cancel 3 -> Hits cap!
            _accumulator.Tick(0.50f, hasLOS: true, distance: 2.0f);
            _accumulator.Tick(0.20f, hasLOS: false, distance: 2.0f, isReachable: true, isPlayerCausedBreak: true);

            Assert.IsTrue(capReachedFired, "Reaching cancel cap (3) must trigger OnCapReached forced investigate.");
            Assert.AreEqual(0, _accumulator.CancelCounter, "Cancel counter should reset upon cap reached.");
        }

        [Test]
        public void test_suspicion_accumulator_chase_threshold_preempts_immediately()
        {
            // Arrange: T_chase = 1.00 (F9)
            bool chaseFired = false;
            _accumulator.OnChaseReached += () => chaseFired = true;

            // Act: Continuous close-range exposure for 1.8s (0.60 A/s * 1.8s = 1.08 >= 1.00)
            _accumulator.Tick(1.80f, hasLOS: true, distance: 1.5f, isReachable: true);

            // Assert
            Assert.IsTrue(chaseFired, "Reaching T_chase must fire OnChaseReached.");
            Assert.AreEqual(0f, _accumulator.Accumulator, "A resets upon Chase engagement.");
        }

        [Test]
        public void test_suspicion_accumulator_unreachable_target_clamps_below_chase_threshold()
        {
            // Arrange: Player is unreachable on high ledge (isReachable = false)
            bool chaseFired = false;
            _accumulator.OnChaseReached += () => chaseFired = true;

            // Act: Continuous exposure for 3.0s
            _accumulator.Tick(3.0f, hasLOS: true, distance: 1.5f, isReachable: false);

            // Assert: A is clamped to T_chase - deltaReach = 1.00 - 0.02 = 0.98
            Assert.IsFalse(chaseFired, "Unreachable player must NEVER trigger Chase!");
            Assert.AreEqual(0.98f, _accumulator.Accumulator, 0.001f, "A must clamp to 0.98 below T_chase.");
        }

        [Test]
        public void test_suspicion_accumulator_fruitless_investigation_increments_residual()
        {
            // Arrange: Initial R = 0.10 (F6)
            _accumulator.SetResidual(0.10f);

            // Act: Apply fruitless noise investigation
            _accumulator.ApplyFruitlessInvestigationResidual();

            // Assert: Delta R = 0.15 -> R = 0.25
            Assert.AreEqual(0.25f, _accumulator.Residual, 0.001f, "Fruitless investigation must add 0.15 residual wariness.");
        }

        [Test]
        public void test_suspicion_accumulator_chase_mode_freezes_accumulation()
        {
            // Arrange: Set in Chase mode
            _accumulator.SetChaseMode(true);
            Assert.IsTrue(_accumulator.IsInChase);

            // Act: Tick with LOS
            _accumulator.Tick(1.0f, hasLOS: true, distance: 2.0f);

            // Assert: A and R remain 0
            Assert.AreEqual(0f, _accumulator.Accumulator, "Accumulator must remain inert in Chase mode.");
            Assert.AreEqual(0f, _accumulator.Residual, "Residual must remain inert in Chase mode.");
        }
    }
}
