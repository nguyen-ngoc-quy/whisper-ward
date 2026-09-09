using System;
using System.Collections.Generic;
using NUnit.Framework;
using WhisperWard.AI.SuspicionGrade;

namespace WhisperWard.AI.Testing
{
    /// <summary>
    /// Verifies the read-only Suspicion Meter presentation boundary.
    /// </summary>
    public sealed class SuspicionMeterPresenterTests
    {
        [Test]
        public void test_suspicion_meter_present_unavailable_model_does_not_render_zero_percent()
        {
            // Arrange — SG-AC11: unavailable is distinct from a zero reading.
            var view = new FakeSuspicionMeterView();
            var presenter = new SuspicionMeterPresenter(view);

            // Act
            presenter.Present(CreateUnavailableReadModel());

            // Assert
            Assert.That(view.State, Is.EqualTo("unavailable"));
            Assert.That(view.PercentText, Is.EqualTo("unavailable"));
            Assert.That(view.PercentSetCount, Is.EqualTo(0));
            Assert.That(view.CallOrder, Is.EqualTo(new[] { "unavailable" }));
        }

        [Test]
        public void test_suspicion_meter_present_available_model_forwards_meter_state_residual_and_cause()
        {
            // Arrange
            var view = new FakeSuspicionMeterView();
            var presenter = new SuspicionMeterPresenter(view);
            var readModel = CreateAvailableReadModel(
                percent: 65,
                region: SuspicionMeterRegion.Investigate,
                residual: 20f,
                causalEvent: "resolve-fruitless");

            // Act
            presenter.Present(readModel);

            // Assert
            Assert.That(view.Percent, Is.EqualTo(65));
            Assert.That(view.Region, Is.EqualTo(SuspicionMeterRegion.Investigate));
            Assert.That(view.Residual, Is.EqualTo(20f));
            Assert.That(view.CausalEvent, Is.EqualTo("resolve-fruitless"));
            Assert.That(view.State, Is.EqualTo("available"));
            Assert.That(view.CallOrder, Is.EqualTo(new[]
            {
                "percent", "region", "residual", "causal-event"
            }));
        }

        [Test]
        public void test_suspicion_meter_present_null_model_shows_unavailable()
        {
            // Arrange
            var view = new FakeSuspicionMeterView();
            var presenter = new SuspicionMeterPresenter(view);

            // Act
            presenter.Present(null);

            // Assert
            Assert.That(view.State, Is.EqualTo("unavailable"));
            Assert.That(view.PercentText, Is.EqualTo("unavailable"));
        }

        [Test]
        public void test_suspicion_meter_present_null_view_does_not_throw()
        {
            // Arrange
            var presenter = new SuspicionMeterPresenter(null);
            var readModel = CreateAvailableReadModel(
                percent: 25,
                region: SuspicionMeterRegion.Quiet,
                residual: 0f,
                causalEvent: "");

            // Act
            TestDelegate present = () => presenter.Present(readModel);

            // Assert
            Assert.DoesNotThrow(present);
        }

        [Test]
        public void test_suspicion_meter_present_throwing_view_isolated_from_remaining_operations()
        {
            // Arrange
            var view = new ThrowingMeterView();
            var presenter = new SuspicionMeterPresenter(view);

            // Act
            TestDelegate present = () => presenter.Present(CreateAvailableReadModel(
                percent: 65,
                region: SuspicionMeterRegion.Chase,
                residual: 20f,
                causalEvent: "chase-entry"));

            // Assert
            Assert.DoesNotThrow(present);
            Assert.That(view.AttemptedOperations, Is.EqualTo(new[]
            {
                "percent", "region", "residual", "causal-event"
            }));
        }

        [Test]
        public void test_suspicion_meter_present_repeated_model_uses_stable_operation_order()
        {
            // Arrange
            var view = new FakeSuspicionMeterView();
            var presenter = new SuspicionMeterPresenter(view);
            var readModel = CreateAvailableReadModel(
                percent: 40,
                region: SuspicionMeterRegion.Investigate,
                residual: 5f,
                causalEvent: "noise-heard");

            // Act
            presenter.Present(readModel);
            var firstOrder = new List<string>(view.CallOrder);
            view.CallOrder.Clear();
            presenter.Present(readModel);

            // Assert
            Assert.That(view.CallOrder, Is.EqualTo(firstOrder));
        }

        private static SuspicionMeterReadModel CreateUnavailableReadModel()
        {
            return new SuspicionMeterReadModel(
                new SuspicionGradeIdentity("session", 1L, "room", "guard-1"),
                false, 0f, 0f, 0f, 0,
                SuspicionMeterRegion.Unavailable, 0f, "Patrol", "", "");
        }

        private static SuspicionMeterReadModel CreateAvailableReadModel(
            int percent, SuspicionMeterRegion region, float residual,
            string causalEvent)
        {
            return new SuspicionMeterReadModel(
                new SuspicionGradeIdentity("session", 1L, "room", "guard-1"),
                true, percent, 100f, percent / 100f, percent,
                region, residual, "Investigate", causalEvent, "entry-1");
        }

        private sealed class FakeSuspicionMeterView : ISuspicionMeterView
        {
            public readonly List<string> CallOrder = new List<string>();

            public string State { get; private set; } = "unset";
            public string PercentText { get; private set; } = "unset";
            public int Percent { get; private set; }
            public int PercentSetCount { get; private set; }
            public SuspicionMeterRegion Region { get; private set; }
            public float Residual { get; private set; }
            public string CausalEvent { get; private set; }

            public void ShowUnavailable()
            {
                State = "unavailable";
                PercentText = "unavailable";
                CallOrder.Add("unavailable");
            }

            public void SetMeterPercent(int percent)
            {
                State = "available";
                Percent = percent;
                PercentText = percent + "%";
                PercentSetCount++;
                CallOrder.Add("percent");
            }

            public void SetRegion(SuspicionMeterRegion region)
            {
                Region = region;
                CallOrder.Add("region");
            }

            public void SetResidual(float residual)
            {
                Residual = residual;
                CallOrder.Add("residual");
            }

            public void SetCausalEvent(string causalEvent)
            {
                CausalEvent = causalEvent;
                CallOrder.Add("causal-event");
            }
        }

        private sealed class ThrowingMeterView : ISuspicionMeterView
        {
            public readonly List<string> AttemptedOperations = new List<string>();

            public void ShowUnavailable()
            {
                AttemptedOperations.Add("unavailable");
                throw new InvalidOperationException("view-unavailable-failure");
            }

            public void SetMeterPercent(int percent)
            {
                AttemptedOperations.Add("percent");
                throw new InvalidOperationException("view-percent-failure");
            }

            public void SetRegion(SuspicionMeterRegion region)
            {
                AttemptedOperations.Add("region");
                throw new InvalidOperationException("view-region-failure");
            }

            public void SetResidual(float residual)
            {
                AttemptedOperations.Add("residual");
                throw new InvalidOperationException("view-residual-failure");
            }

            public void SetCausalEvent(string causalEvent)
            {
                AttemptedOperations.Add("causal-event");
                throw new InvalidOperationException("view-causal-failure");
            }
        }
    }
}
