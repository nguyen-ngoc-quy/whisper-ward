using System;

namespace WhisperWard.Foundation.Events
{
    /// <summary>
    /// Five-phase virtual clock pipeline defined in ADR-0001.
    /// Guarantees strict sequential execution across subsystems.
    /// </summary>
    public enum VirtualClockPhase
    {
        Input = 0,
        Locomotion = 1,
        Sensor = 2,
        Decision = 3,
        Actuation = 4
    }

    /// <summary>
    /// Marker contract for all payload types carried by an EventEnvelope.
    /// </summary>
    public interface IEvent
    {
        string EventType { get; }
    }

    /// <summary>
    /// Transactional handoff status codes across subsystem boundaries (e.g. Bus -> Perception).
    /// </summary>
    public enum EventHandoffStatus
    {
        Accepted,
        Duplicate,
        Retry,
        Rejected
    }

    /// <summary>
    /// Immutable result of a two-phase transactional handoff.
    /// </summary>
    public readonly struct EventHandoffResult : IEquatable<EventHandoffResult>
    {
        public readonly EventHandoffStatus Status;
        public readonly string RejectionReason;

        public EventHandoffResult(EventHandoffStatus status, string rejectionReason = null)
        {
            Status = status;
            RejectionReason = rejectionReason ?? string.Empty;
        }

        public static EventHandoffResult Accepted => new EventHandoffResult(EventHandoffStatus.Accepted);
        public static EventHandoffResult Duplicate => new EventHandoffResult(EventHandoffStatus.Duplicate, "event-bus-duplicate-identity");
        public static EventHandoffResult Retry => new EventHandoffResult(EventHandoffStatus.Retry, "event-bus-backpressure-retry");
        public static EventHandoffResult Rejected(string code) => new EventHandoffResult(EventHandoffStatus.Rejected, code);

        public bool Equals(EventHandoffResult other) => Status == other.Status && string.Equals(RejectionReason, other.RejectionReason, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is EventHandoffResult other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Status, RejectionReason);
        public static bool operator ==(EventHandoffResult left, EventHandoffResult right) => left.Equals(right);
        public static bool operator !=(EventHandoffResult left, EventHandoffResult right) => !left.Equals(right);
    }

    /// <summary>
    /// Deterministic subscription token providing safe unsubscribe lifecycle.
    /// </summary>
    public readonly struct SubscriptionToken : IEquatable<SubscriptionToken>
    {
        public readonly long Id;

        public SubscriptionToken(long id)
        {
            Id = id;
        }

        public bool IsValid => Id > 0;

        public bool Equals(SubscriptionToken other) => Id == other.Id;
        public override bool Equals(object obj) => obj is SubscriptionToken other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public static bool operator ==(SubscriptionToken left, SubscriptionToken right) => left.Equals(right);
        public static bool operator !=(SubscriptionToken left, SubscriptionToken right) => !left.Equals(right);
        public override string ToString() => $"SubToken({Id})";
    }

    /// <summary>
    /// Immutable event envelope defined in ADR-0001 and TR-FOUND-001.
    /// Strictly encapsulates all transmission and ordering metadata.
    /// </summary>
    public readonly struct EventEnvelope : IEquatable<EventEnvelope>
    {
        public readonly string SessionId;
        public readonly long AttemptEpoch;
        public readonly double Timestamp;
        public readonly string Publisher;
        public readonly VirtualClockPhase Phase;
        public readonly string OwnerNamespace;
        public readonly ulong EventIdentity;
        public readonly int SourceClassRank;
        public readonly string SourceEventId;
        public readonly IEvent Payload;

        public EventEnvelope(
            string sessionId,
            long attemptEpoch,
            double timestamp,
            string publisher,
            VirtualClockPhase phase,
            string ownerNamespace,
            ulong eventIdentity,
            IEvent payload,
            int sourceClassRank = 0,
            string sourceEventId = "")
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            if (attemptEpoch < 0) throw new ArgumentOutOfRangeException(nameof(attemptEpoch));
            if (string.IsNullOrEmpty(publisher)) throw new ArgumentNullException(nameof(publisher));
            if (string.IsNullOrEmpty(ownerNamespace)) throw new ArgumentNullException(nameof(ownerNamespace));
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));

            SessionId = sessionId;
            AttemptEpoch = attemptEpoch;
            Timestamp = timestamp;
            Publisher = publisher;
            Phase = phase;
            OwnerNamespace = ownerNamespace;
            EventIdentity = eventIdentity;
            SourceClassRank = sourceClassRank;
            SourceEventId = sourceEventId ?? string.Empty;
        }

        public bool Equals(EventEnvelope other)
        {
            return string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
                && AttemptEpoch == other.AttemptEpoch
                && Timestamp.Equals(other.Timestamp)
                && string.Equals(Publisher, other.Publisher, StringComparison.Ordinal)
                && Phase == other.Phase
                && string.Equals(OwnerNamespace, other.OwnerNamespace, StringComparison.Ordinal)
                && EventIdentity == other.EventIdentity
                && SourceClassRank == other.SourceClassRank
                && string.Equals(SourceEventId, other.SourceEventId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is EventEnvelope other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(SessionId, StringComparer.Ordinal);
            hash.Add(AttemptEpoch);
            hash.Add(Timestamp);
            hash.Add(Publisher, StringComparer.Ordinal);
            hash.Add((int)Phase);
            hash.Add(OwnerNamespace, StringComparer.Ordinal);
            hash.Add(EventIdentity);
            return hash.ToHashCode();
        }

        public static bool operator ==(EventEnvelope left, EventEnvelope right) => left.Equals(right);
        public static bool operator !=(EventEnvelope left, EventEnvelope right) => !left.Equals(right);
    }
}
