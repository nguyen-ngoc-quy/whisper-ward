using System;

namespace WhisperWard.AI.SuspicionGrade
{
    /// <summary>
    /// View operations required by the read-only Suspicion Meter presenter.
    /// Implementations should expose the semantic region and unavailable state as
    /// readable UI state in addition to any visual treatment.
    /// </summary>
    public interface ISuspicionMeterView
    {
        /// <summary>
        /// Shows that authoritative meter input is unavailable. This state must not
        /// be represented as a fabricated zero percent value.
        /// </summary>
        void ShowUnavailable();

        /// <summary>
        /// Sets the live accumulator display percentage.
        /// </summary>
        /// <param name="percent">Clamped display percentage from the read model.</param>
        void SetMeterPercent(int percent);

        /// <summary>
        /// Sets the semantic region, including its readable state representation.
        /// </summary>
        /// <param name="region">Authoritative semantic meter region.</param>
        void SetRegion(SuspicionMeterRegion region);

        /// <summary>
        /// Sets the separate residual-wariness presentation value.
        /// </summary>
        /// <param name="residual">Authoritative residual value.</param>
        void SetResidual(float residual);

        /// <summary>
        /// Sets the latest causal event explanation.
        /// </summary>
        /// <param name="causalEvent">Causal event text or identifier.</param>
        void SetCausalEvent(string causalEvent);
    }

    /// <summary>
    /// Stateless presentation adapter for an authoritative suspicion meter
    /// read-model projection.
    /// </summary>
    /// <remarks>
    /// The presenter does not calculate suspicion, infer causes, alter thresholds,
    /// own score state, or send gameplay commands. A missing model is rendered as
    /// unavailable. View failures are isolated so presentation cannot become a
    /// gameplay failure path.
    /// </remarks>
    public sealed class SuspicionMeterPresenter : ISuspicionMeterPresenter
    {
        private readonly ISuspicionMeterView _view;

        /// <summary>
        /// Creates a presenter for an injected meter view.
        /// </summary>
        /// <param name="view">View sink receiving read-model presentation calls.</param>
        public SuspicionMeterPresenter(ISuspicionMeterView view)
        {
            _view = view;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Null models are treated as unavailable. Each view operation is isolated
        /// independently so a throwing UI sink cannot prevent the remaining
        /// authoritative fields from being offered to the sink or escape into the
        /// gameplay caller.
        /// </remarks>
        public void Present(SuspicionMeterReadModel readModel)
        {
            if (_view == null)
            {
                return;
            }

            if (readModel == null || !readModel.IsAvailable)
            {
                InvokeSafely(_view.ShowUnavailable);
                return;
            }

            InvokeSafely(() => _view.SetMeterPercent(readModel.MeterPercent));
            InvokeSafely(() => _view.SetRegion(readModel.Region));
            InvokeSafely(() => _view.SetResidual(readModel.RCurrent));
            InvokeSafely(() => _view.SetCausalEvent(readModel.LastCausalEvent));
        }

        private static void InvokeSafely(Action operation)
        {
            try
            {
                operation();
            }
            catch (Exception)
            {
                // A view is an optional presentation sink and cannot fail gameplay.
            }
        }
    }
}
