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
        private sealed class PhaseParticipant
        {
            public string Key;
            public int Order;
            public Action<long, float> Callback;
        }

        private readonly List<PhaseParticipant> _ingressSources =
            new List<PhaseParticipant>();
        private readonly List<PhaseParticipant> _hearingParticipants =
            new List<PhaseParticipant>();
        private readonly List<PhaseParticipant> _fsmParticipants =
            new List<PhaseParticipant>();
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
        /// Registers a source with an explicit stable key and phase order. The key
        /// and order, rather than registration sequence, determine execution order.
        /// </summary>
        public void RegisterIngressSource(string participantKey, int order,
            Action<long, float> source)
        {
            Register(_ingressSources, participantKey, order, source);
        }

        /// <summary>Compatibility registration for legacy fixtures.</summary>
        public void RegisterIngressSource(Action<long, float> source)
        {
            RegisterLegacy(_ingressSources, source, "ingress");
        }

        public void UnregisterIngressSource(Action<long, float> source)
        {
            Unregister(_ingressSources, source);
        }

        /// <summary>
        /// Registers the independent hearing scheduler between ingress and FSM
        /// decision drains. This explicit phase avoids clock-listener ordering.
        /// </summary>
        public void RegisterHearingParticipant(string participantKey, int order,
            Action<long, float> participant)
        {
            Register(_hearingParticipants, participantKey, order, participant);
        }

        /// <summary>Compatibility registration for legacy fixtures.</summary>
        public void RegisterHearingParticipant(Action<long, float> participant)
        {
            RegisterLegacy(_hearingParticipants, participant, "hearing");
        }

        public void UnregisterHearingParticipant(Action<long, float> participant)
        {
            Unregister(_hearingParticipants, participant);
        }

        /// <summary>
        /// Registers an FSM or other state consumer that runs after sensing facts are
        /// delivered and before presentation records are drained.
        /// </summary>
        public void RegisterFsmParticipant(string participantKey, int order,
            Action<long, float> participant)
        {
            Register(_fsmParticipants, participantKey, order, participant);
        }

        /// <summary>Compatibility registration for legacy fixtures.</summary>
        public void RegisterFsmParticipant(Action<long, float> participant)
        {
            RegisterLegacy(_fsmParticipants, participant, "fsm");
        }

        public void UnregisterFsmParticipant(Action<long, float> participant)
        {
            Unregister(_fsmParticipants, participant);
        }

        private static void Register(List<PhaseParticipant> participants,
            string participantKey, int order, Action<long, float> callback)
        {
            if (string.IsNullOrWhiteSpace(participantKey))
                throw new ArgumentException("A participant key is required",
                    nameof(participantKey));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            for (int i = 0; i < participants.Count; i++)
            {
                if (string.Equals(participants[i].Key, participantKey,
                    StringComparison.Ordinal))
                {
                    if (participants[i].Callback == callback) return;
                    throw new InvalidOperationException(
                        "Duplicate session phase participant key: " + participantKey);
                }
            }
            participants.Add(new PhaseParticipant
            {
                Key = participantKey,
                Order = order,
                Callback = callback
            });
        }

        private static void RegisterLegacy(List<PhaseParticipant> participants,
            Action<long, float> callback, string phase)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            for (int i = 0; i < participants.Count; i++)
                if (participants[i].Callback == callback) return;
            Register(participants, "legacy:" + phase + ":" + participants.Count,
                participants.Count, callback);
        }

        private static void Unregister(List<PhaseParticipant> participants,
            Action<long, float> callback)
        {
            if (callback == null) return;
            for (int i = participants.Count - 1; i >= 0; i--)
                if (participants[i].Callback == callback)
                    participants.RemoveAt(i);
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
            // FSM participants may publish liveness and decision records while
            // consuming the boundary. Drain again so those records are admitted
            // before Presentation and are visible to the next boundary's readers.
            _eventBus.Drain(EventBusPhase.FsmDecision);
            _eventBus.Drain(EventBusPhase.Presentation);
        }

        private static void InvokeSnapshot(List<PhaseParticipant> participants,
            long tick, float delta)
        {
            var snapshot = new List<PhaseParticipant>(participants);
            snapshot.Sort(CompareParticipants);
            for (int i = 0; i < snapshot.Count; i++)
                snapshot[i].Callback(tick, delta);
        }

        private static int CompareParticipants(PhaseParticipant left,
            PhaseParticipant right)
        {
            int compare = left.Order.CompareTo(right.Order);
            if (compare != 0) return compare;
            return string.Compare(left.Key, right.Key, StringComparison.Ordinal);
        }
    }
}
