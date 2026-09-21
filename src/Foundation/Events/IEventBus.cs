using System;

namespace WhisperWard.Foundation.Events
{
    /// <summary>
    /// Contract for deterministic event delivery, epoch barriers, and subscription lifecycle.
    /// Defined in ADR-0001 (System #15).
    /// </summary>
    public interface IEventBus
    {
        int Capacity { get; }
        int PendingCount { get; }
        long CurrentEpoch { get; }
        string CurrentSessionId { get; }

        SubscriptionToken Subscribe<T>(Action<T> handler, VirtualClockPhase phase) where T : class, IEvent;
        SubscriptionToken SubscribeHandoff(Func<EventEnvelope, EventHandoffResult> handoff, VirtualClockPhase phase);
        bool Unsubscribe(SubscriptionToken token);

        EventHandoffResult Publish(in EventEnvelope envelope);
        int DrainPhase(VirtualClockPhase phase);

        void BeginEpoch(long newEpoch);
        void BeginSession(string newSessionId);
    }
}
