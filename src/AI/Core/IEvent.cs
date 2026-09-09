using System;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Base contract for immutable or adapter-backed events flowing through the AI.
    /// Gameplay events are valid only when they carry a session, epoch, timestamp,
    /// publisher and owner-assigned identity.
    /// </summary>
    public interface IEvent
    {
        string SessionId { get; }
        long AttemptEpoch { get; }
        float Timestamp { get; }
        string Publisher { get; }
        string Identity { get; }
    }

    /// <summary>
    /// Optional seam used only by compatibility event adapters. Authoritative
    /// immutable events do not implement this interface.
    /// </summary>
    public interface IEventMetadataWriter
    {
        void SetEnvelope(string sessionId, long attemptEpoch, string identity);
    }

    /// <summary>
    /// Optional event-owned transport identity policy. Raw facts use this seam to
    /// declare their exact session/epoch/fact deduplication identity without making
    /// Core depend on a Perception event type.
    /// </summary>
    public interface IEventDeduplicationKey
    {
        string DeduplicationIdentity { get; }
    }

    /// <summary>
    /// Immutable transport envelope. The payload owns its identity; the bus never
    /// allocates a gameplay fact or episode identifier.
    /// </summary>
    public sealed class EventEnvelope
    {
        public EventEnvelope(string sessionId, long attemptEpoch, float timestamp,
            string publisher, string identity, EventBusPhase phase, IEvent payload)
        {
            SessionId = sessionId ?? string.Empty;
            AttemptEpoch = attemptEpoch;
            Timestamp = timestamp;
            Publisher = publisher ?? string.Empty;
            Identity = identity ?? string.Empty;
            Phase = phase;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public string SessionId { get; }
        public long AttemptEpoch { get; }
        public float Timestamp { get; }
        public string Publisher { get; }
        public string Identity { get; }
        public EventBusPhase Phase { get; }
        public IEvent Payload { get; }
    }

    /// <summary>
    /// Optional routing metadata supplied by authoritative event types. Events that
    /// omit this adapter remain on the ingress phase and are never guessed into a
    /// gameplay phase by the transport.
    /// </summary>
    public interface IEventPhase
    {
        EventBusPhase Phase { get; }
    }

    /// <summary>
    /// A stable key for ingress and listener-side exactly-once checks.
    /// </summary>
    public struct EventIdentityKey : IEquatable<EventIdentityKey>
    {
        public EventIdentityKey(string sessionId, long attemptEpoch, string identity)
        {
            SessionId = sessionId ?? string.Empty;
            AttemptEpoch = attemptEpoch;
            Identity = identity ?? string.Empty;
        }

        public string SessionId { get; }
        public long AttemptEpoch { get; }
        public string Identity { get; }

        public bool Equals(EventIdentityKey other)
        {
            return AttemptEpoch == other.AttemptEpoch
                && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
                && string.Equals(Identity, other.Identity, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EventIdentityKey && Equals((EventIdentityKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (SessionId == null ? 0 : SessionId.GetHashCode());
                hash = hash * 31 + AttemptEpoch.GetHashCode();
                hash = hash * 31 + (Identity == null ? 0 : Identity.GetHashCode());
                return hash;
            }
        }
    }

    /// <summary>
    /// Result of an ingress or downstream handoff attempt.
    /// </summary>
    public enum EventAdmission
    {
        Accepted,
        Duplicate,
        Retry,
        Rejected
    }

    /// <summary>
    /// Typed admission result with a stable diagnostic code.
    /// </summary>
    public struct EventPublishResult
    {
        public EventPublishResult(EventAdmission admission, string code, int retryCount)
        {
            Admission = admission;
            Code = code ?? string.Empty;
            RetryCount = retryCount;
        }

        public EventAdmission Admission { get; }
        public string Code { get; }
        public int RetryCount { get; }

        public bool IsAccepted
        {
            get { return Admission == EventAdmission.Accepted || Admission == EventAdmission.Duplicate; }
        }

        public static EventPublishResult Accepted()
        {
            return new EventPublishResult(EventAdmission.Accepted, "accepted", 0);
        }

        public static EventPublishResult Duplicate(string code = "event-bus-duplicate-identity")
        {
            return new EventPublishResult(EventAdmission.Duplicate, code, 0);
        }

        public static EventPublishResult Retry(string code, int retryCount)
        {
            return new EventPublishResult(EventAdmission.Retry, code, retryCount);
        }

        public static EventPublishResult Rejected(string code, int retryCount = 0)
        {
            return new EventPublishResult(EventAdmission.Rejected, code, retryCount);
        }
    }

    /// <summary>
    /// Result of a typed downstream handoff. Accepted and Duplicate complete one
    /// listener obligation; Retry retains the envelope; Rejected is terminal for
    /// that listener and must be serialized by the transport.
    /// </summary>
    public enum EventHandoffAdmission
    {
        Accepted,
        Duplicate,
        Retry,
        Rejected
    }

    /// <summary>
    /// Immutable result returned by a transactional typed handoff listener.
    /// </summary>
    public struct EventHandoffResult
    {
        public EventHandoffResult(EventHandoffAdmission admission, string code)
        {
            Admission = admission;
            Code = code ?? string.Empty;
        }

        public EventHandoffAdmission Admission { get; }
        public string Code { get; }

        public static EventHandoffResult Accepted()
        {
            return new EventHandoffResult(EventHandoffAdmission.Accepted, "accepted");
        }

        public static EventHandoffResult Duplicate()
        {
            return new EventHandoffResult(EventHandoffAdmission.Duplicate,
                "event-bus-duplicate-identity");
        }

        public static EventHandoffResult Retry(string code)
        {
            return new EventHandoffResult(EventHandoffAdmission.Retry, code);
        }

        public static EventHandoffResult Rejected(string code)
        {
            return new EventHandoffResult(EventHandoffAdmission.Rejected, code);
        }
    }

    /// <summary>
    /// Sink for serialized transport diagnostics. Implementations may write a test
    /// trace, telemetry record, or production diagnostics stream; the bus never logs
    /// directly and never depends on a presentation system.
    /// </summary>
    public interface IEventDiagnosticSink
    {
        void Record(string code, EventEnvelope envelope, int retryCount,
            int queueDepth, int queueCapacity);
    }

    /// <summary>
    /// Optional extension for traces that must distinguish queue ownership and
    /// preserve the deterministic ordering key. Existing sinks may implement only
    /// the compact Record contract; detailed sinks receive the same record plus the
    /// queue and serialized key fields.
    /// </summary>
    public interface IEventDiagnosticDetailSink : IEventDiagnosticSink
    {
        void RecordDetailed(string code, EventEnvelope envelope, int retryCount,
            int queueDepth, int queueCapacity, string queueName,
            string orderingKey);
    }

    /// <summary>
    /// Token returned by a typed subscription. Tokens make component teardown
    /// reliable even when callbacks are wrapped or subscribed more than once.
    /// </summary>
    public struct SubscriptionToken : IEquatable<SubscriptionToken>
    {
        internal SubscriptionToken(long value)
        {
            Value = value;
        }

        internal long Value { get; }

        public bool IsValid
        {
            get { return Value != 0; }
        }

        public bool Equals(SubscriptionToken other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is SubscriptionToken && Equals((SubscriptionToken)obj);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Named phase used by the deterministic session bus. The bus itself only
    /// transports; the session orchestrator decides when each phase drains.
    /// </summary>
    public enum EventBusPhase
    {
        GameplayIngress,
        Hearing,
        FsmDecision,
        Presentation
    }

    /// <summary>
    /// Injected virtual time authority. Render-frame time is merely an optional
    /// host adapter and is not part of this interface.
    /// </summary>
    public interface IVirtualTickClock
    {
        long CurrentTick { get; }
        float CurrentTime { get; }
        float TickInterval { get; }
        bool IsPaused { get; }

        SubscriptionToken Subscribe(Action<long, float> listener);
        bool Unsubscribe(SubscriptionToken token);
        void Advance(float gameplayDelta);
        void SetPaused(bool paused);
    }

    /// <summary>
    /// Session lifecycle boundary. Only the lifecycle owner may advance the epoch.
    /// </summary>
    public interface ISessionBoundary
    {
        string SessionId { get; }
        long AttemptEpoch { get; }
        void BeginSession(string sessionId, long initialEpoch);
        void BeginEpoch(long nextEpoch);
    }

    /// <summary>
    /// Injectable event transport used by gameplay systems. The static EventBus
    /// facade is an adapter for legacy composition only.
    /// </summary>
    public interface IEventBus
    {
        string SessionId { get; }
        long AttemptEpoch { get; }

        SubscriptionToken Subscribe<T>(Action<T> handler) where T : IEvent;
        SubscriptionToken SubscribeHandoff<T>(Func<EventEnvelope, EventHandoffResult> handler)
            where T : IEvent;
        bool Unsubscribe(SubscriptionToken token);
        EventPublishResult Publish(IEvent evt);
        int Drain(EventBusPhase phase);
        int PendingEnvelopeCount { get; }
        int PendingEnvelopeCapacity { get; }
        int RetryCount { get; }
        int RejectedCount { get; }
        int StaleCount { get; }
        void BeginSession(string sessionId, long initialEpoch);
        void BeginEpoch(string sessionId, long nextEpoch);
    }
}
