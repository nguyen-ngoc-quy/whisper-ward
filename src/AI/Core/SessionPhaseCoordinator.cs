using System;
using System.Collections.Generic;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Owns the virtual-clock phase order for one gameplay session. Sources publish
    /// before the hearing drain, sensing callbacks are delivered before FSM updates,
    /// and presentation callbacks observe decisions from the same boundary.
    /// </summary>
    public sealed class SessionPhaseCoordinator
    {
        private readonly IEventBus _eventBus;
        private readonly IVirtualTickClock _clock;
        private readonly List<Action<long, float>> _ingressSources =
            new List<Action<long, float>>();
        private readonly List<Action<long, float>> _hearingParticipants =
            new List<Action<long, float>>();
        private readonly List<Action<long, float>> _fsmParticipants =
            new List<Action<long, float>>();
        private SubscriptionToken _clockToken;
        private bool _isBound;

        public SessionPhaseCoordinator(IEventBus eventBus, IVirtualTickClock clock)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public void Bind()
        {
            if (_isBound) return;
            _clockToken = _clock.Subscribe(OnTick);
            _isBound = true;
        }

        public void Unbind()
        {
            if (!_isBound) return;
            _clock.Unsubscribe(_clockToken);
            _isBound = false;
        }

        /// <summary>
        /// Registers a source that publishes committed gameplay facts at a boundary.
        /// </summary>
        public void RegisterIngressSource(Action<long, float> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!_ingressSources.Contains(source)) _ingressSources.Add(source);
        }

        public void UnregisterIngressSource(Action<long, float> source)
        {
            if (source != null) _ingressSources.Remove(source);
        }

        /// <summary>
        /// Registers the independent hearing scheduler between ingress and FSM
        /// decision drains. This explicit phase avoids clock-listener ordering.
        /// </summary>
        public void RegisterHearingParticipant(Action<long, float> participant)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            if (!_hearingParticipants.Contains(participant))
                _hearingParticipants.Add(participant);
        }

        public void UnregisterHearingParticipant(Action<long, float> participant)
        {
            if (participant != null) _hearingParticipants.Remove(participant);
        }

        /// <summary>
        /// Registers an FSM or other state consumer that runs after sensing facts are
        /// delivered and before presentation records are drained.
        /// </summary>
        public void RegisterFsmParticipant(Action<long, float> participant)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            if (!_fsmParticipants.Contains(participant)) _fsmParticipants.Add(participant);
        }

        public void UnregisterFsmParticipant(Action<long, float> participant)
        {
            if (participant != null) _fsmParticipants.Remove(participant);
        }

        private void OnTick(long tick, float delta)
        {
            // Facts already queued by non-clock sources are admitted first. Sources
            // then publish this boundary's raw work before Hearing is evaluated.
            _eventBus.Drain(EventBusPhase.GameplayIngress);
            InvokeSnapshot(_ingressSources, tick, delta);
            _eventBus.Drain(EventBusPhase.GameplayIngress);
            _eventBus.Drain(EventBusPhase.Hearing);
            InvokeSnapshot(_hearingParticipants, tick, delta);
            _eventBus.Drain(EventBusPhase.FsmDecision);
            InvokeSnapshot(_fsmParticipants, tick, delta);
            _eventBus.Drain(EventBusPhase.Presentation);
        }

        private static void InvokeSnapshot(List<Action<long, float>> participants,
            long tick, float delta)
        {
            var snapshot = new List<Action<long, float>>(participants);
            for (int i = 0; i < snapshot.Count; i++)
                snapshot[i](tick, delta);
        }
    }
}
