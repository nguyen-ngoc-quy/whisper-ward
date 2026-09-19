using System;
using System.Collections.Generic;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Deterministic, session-scoped event transport. It queues immutable event
    /// payloads and dispatches a subscriber snapshot only when the owner drains a
    /// virtual-clock phase.
    /// </summary>
    public sealed class SessionEventBus : IEventBus
    {
        private sealed class Subscription
        {
            public SubscriptionToken Token;
            public Type EventType;
            public Action<IEvent> Handler;
            public Func<EventEnvelope, EventHandoffResult> Handoff;
            public bool Active;

            public bool IsHandoff
            {
                get { return Handoff != null; }
            }
        }

        private sealed class QueuedEvent
        {
            public EventEnvelope Envelope;
            public long Sequence;
            public int RetryCount;
            public bool HandoffSnapshotTaken;
            public readonly List<SubscriptionToken> HandoffObligations =
                new List<SubscriptionToken>();
            public readonly HashSet<long> CompletedObligations =
                new HashSet<long>();
            public readonly Dictionary<long, int> HandoffRetries =
                new Dictionary<long, int>();
        }

        private readonly List<Subscription> _subscriptions = new List<Subscription>();
        private readonly List<QueuedEvent> _pending = new List<QueuedEvent>();
        private readonly HashSet<EventIdentityKey> _admitted =
            new HashSet<EventIdentityKey>();
        private readonly HashSet<string> _delivered =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<EventIdentityKey, int> _retryCounts =
            new Dictionary<EventIdentityKey, int>();
        private readonly int _capacity;
        private readonly int _maxRetries;
        private readonly IEventDiagnosticSink _diagnosticSink;
        private long _nextSubscription = 1;
        private long _nextSequence = 1;

        public SessionEventBus(int capacity = 512, int maxRetries = 3,
            IEventDiagnosticSink diagnosticSink = null)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));
            _capacity = capacity;
            _maxRetries = maxRetries;
            _diagnosticSink = diagnosticSink;
            SessionId = "default";
            AttemptEpoch = 0;
        }

        public string SessionId { get; private set; }
        public long AttemptEpoch { get; private set; }
        public int PendingCount { get { return _pending.Count; } }
        public int PendingEnvelopeCount { get { return _pending.Count; } }
        public int PendingEnvelopeCapacity { get { return _capacity; } }
        public int Capacity { get { return _capacity; } }
        public int RetryCount { get; private set; }
        public int RejectedCount { get; private set; }
        public int StaleCount { get; private set; }

        /// <summary>
        /// Registers a normal typed callback. The returned token is the only
        /// authoritative teardown mechanism; callbacks are snapshot-dispatched.
        /// </summary>
        public SubscriptionToken Subscribe<T>(Action<T> handler) where T : IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var token = new SubscriptionToken(_nextSubscription++);
            _subscriptions.Add(new Subscription
            {
                Token = token,
                EventType = typeof(T),
                Handler = evt => handler((T)evt),
                Active = true
            });
            return token;
        }

        /// <summary>
        /// Registers a transactional typed handoff. The bus retains the source
        /// envelope until every snapshot listener acknowledges it.
        /// </summary>
        public SubscriptionToken SubscribeHandoff<T>(
            Func<EventEnvelope, EventHandoffResult> handler) where T : IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var token = new SubscriptionToken(_nextSubscription++);
            _subscriptions.Add(new Subscription
            {
                Token = token,
                EventType = typeof(T),
                Handoff = handler,
                Active = true
            });
            return token;
        }

        public bool Unsubscribe(SubscriptionToken token)
        {
            if (!token.IsValid) return false;

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                if (_subscriptions[i].Token.Equals(token) && _subscriptions[i].Active)
                {
                    _subscriptions[i].Active = false;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Validates and admits an event without invoking listeners. Duplicate
        /// identities are idempotent. A full queue retries before rejecting the
        /// newest source and never records it as admitted.
        /// </summary>
        public EventPublishResult Publish(IEvent evt)
        {
            if (evt == null)
                return EventPublishResult.Rejected("event-bus-invalid-envelope");

            if (string.IsNullOrWhiteSpace(evt.SessionId)
                || evt.AttemptEpoch != AttemptEpoch
                || !string.Equals(evt.SessionId, SessionId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(evt.Publisher)
                || string.IsNullOrWhiteSpace(evt.Identity)
                || float.IsNaN(evt.Timestamp)
                || float.IsInfinity(evt.Timestamp))
            {
                return EventPublishResult.Rejected("event-bus-invalid-envelope");
            }

            var identityPolicy = evt as IEventDeduplicationKey;
            string transportIdentity = identityPolicy == null
                ? evt.GetType().FullName + ":" + evt.Identity
                : identityPolicy.DeduplicationIdentity;
            if (string.IsNullOrWhiteSpace(transportIdentity))
                return EventPublishResult.Rejected("event-bus-invalid-identity");
            var key = new EventIdentityKey(evt.SessionId, evt.AttemptEpoch,
                transportIdentity);
            if (_admitted.Contains(key))
                return EventPublishResult.Duplicate();

            if (_pending.Count >= _capacity)
            {
                int retries;
                _retryCounts.TryGetValue(key, out retries);
                if (retries < _maxRetries)
                {
                    retries++;
                    _retryCounts[key] = retries;
                    RetryCount++;
                    return EventPublishResult.Retry("event-bus-queue-full-retry", retries);
                }

                RejectedCount++;
                _retryCounts.Remove(key);
                RecordDiagnostic("event-bus-queue-overflow-rejected", evt,
                    retries);
                return EventPublishResult.Rejected("event-bus-queue-overflow-rejected",
                    retries);
            }

            var phase = evt is IEventPhase phased
                ? phased.Phase
                : EventBusPhase.GameplayIngress;
            var envelope = new EventEnvelope(evt.SessionId, evt.AttemptEpoch,
                evt.Timestamp, evt.Publisher, evt.Identity, phase, evt);
            _pending.Add(new QueuedEvent
            {
                Envelope = envelope,
                Sequence = _nextSequence++
            });
            _admitted.Add(key);
            _retryCounts.Remove(key);
            return EventPublishResult.Accepted();
        }

        /// <summary>
        /// Drains one phase using one listener snapshot. A transactional handoff
        /// remains queued while a listener requests retry; callbacks published from
        /// a listener wait for a later explicit drain.
        /// </summary>
        public int Drain(EventBusPhase phase)
        {
            if (_pending.Count == 0) return 0;

            var batch = new List<QueuedEvent>();
            var remaining = new List<QueuedEvent>();
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].Envelope.Phase == phase)
                    batch.Add(_pending[i]);
                else
                    remaining.Add(_pending[i]);
            }
            _pending.Clear();
            _pending.AddRange(remaining);
            if (batch.Count == 0) return 0;
            batch.Sort(CompareQueuedEvents);

            var snapshot = new List<Subscription>();
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                if (_subscriptions[i].Active)
                    snapshot.Add(_subscriptions[i]);
            }

            int dispatched = 0;
            var blockedPublishers = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < batch.Count; i++)
            {
                QueuedEvent queued = batch[i];
                IEvent payload = queued.Envelope.Payload;
                bool retain = false;

                // A retained handoff is a predecessor barrier for every later
                // event from the same publisher. Do not invoke a later payload
                // merely because it was already present in this drain batch.
                // Re-queueing it with its original sequence lets the next drain
                // resume the publisher's canonical order.
                if (blockedPublishers.Contains(queued.Envelope.Publisher))
                {
                    _pending.Add(queued);
                    continue;
                }

                for (int j = 0; j < snapshot.Count; j++)
                {
                    Subscription subscription = snapshot[j];
                    if (!subscription.EventType.IsAssignableFrom(payload.GetType()))
                        continue;

                    if (subscription.IsHandoff)
                    {
                        if (!queued.HandoffSnapshotTaken)
                            queued.HandoffObligations.Add(subscription.Token);
                        continue;
                    }

                    if (!subscription.Active)
                        continue;
                    string deliveryKey = subscription.Token.Value + "|"
                        + payload.GetType().FullName + "|"
                        + queued.Envelope.SessionId + "|"
                        + queued.Envelope.AttemptEpoch + "|"
                        + queued.Envelope.Identity;
                    if (_delivered.Add(deliveryKey))
                    {
                        try
                        {
                            subscription.Handler(payload);
                            dispatched++;
                        }
                        catch (Exception)
                        {
                            // Ordinary callbacks have no retry/ack contract;
                            // serialize the terminal callback failure and keep
                            // draining the remaining deterministic batch.
                            RecordDiagnostic("event-bus-downstream-exception",
                                queued.Envelope, 0);
                        }
                    }
                }

                if (!queued.HandoffSnapshotTaken)
                    queued.HandoffSnapshotTaken = true;

                for (int j = 0; j < queued.HandoffObligations.Count; j++)
                {
                    SubscriptionToken token = queued.HandoffObligations[j];
                    if (queued.CompletedObligations.Contains(token.Value))
                        continue;

                    Subscription subscription = FindSubscription(token);
                    if (subscription == null || !subscription.Active)
                    {
                        queued.CompletedObligations.Add(token.Value);
                        continue;
                    }

                    EventHandoffResult result;
                    try
                    {
                        result = subscription.Handoff(queued.Envelope);
                    }
                    catch (Exception)
                    {
                        result = EventHandoffResult.Rejected(
                            "event-bus-downstream-exception");
                    }

                    switch (result.Admission)
                    {
                        case EventHandoffAdmission.Accepted:
                        case EventHandoffAdmission.Duplicate:
                            queued.CompletedObligations.Add(token.Value);
                            dispatched++;
                            break;
                        case EventHandoffAdmission.Retry:
                            int retries;
                            queued.HandoffRetries.TryGetValue(token.Value, out retries);
                            if (retries >= _maxRetries)
                            {
                                RecordDiagnostic(
                                    "event-bus-downstream-handoff-retry-exhausted",
                                    queued.Envelope, retries);
                                queued.CompletedObligations.Add(token.Value);
                                RejectedCount++;
                            }
                            else
                            {
                                retries++;
                                queued.HandoffRetries[token.Value] = retries;
                                queued.RetryCount = Math.Max(queued.RetryCount, retries);
                                RetryCount++;
                                blockedPublishers.Add(queued.Envelope.Publisher);
                                retain = true;
                            }
                            break;
                        case EventHandoffAdmission.Rejected:
                            RecordDiagnostic(string.IsNullOrWhiteSpace(result.Code)
                                    ? "event-bus-downstream-rejected" : result.Code,
                                queued.Envelope, 0);
                            queued.CompletedObligations.Add(token.Value);
                            RejectedCount++;
                            break;
                    }
                }

                for (int j = 0; j < queued.HandoffObligations.Count; j++)
                {
                    SubscriptionToken token = queued.HandoffObligations[j];
                    if (!queued.CompletedObligations.Contains(token.Value))
                    {
                        retain = true;
                        break;
                    }
                }

                if (retain)
                    _pending.Add(queued);
            }

            PruneInactiveSubscriptions();
            return dispatched;
        }

        public void BeginSession(string sessionId, long initialEpoch)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A session id is required", nameof(sessionId));
            if (initialEpoch < 0)
                throw new ArgumentOutOfRangeException(nameof(initialEpoch));

            RecordStale("event-bus-stale-session");
            SessionId = sessionId;
            AttemptEpoch = initialEpoch;
            ClearQueuedGeneration();
        }

        public void BeginEpoch(string sessionId, long nextEpoch)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A session id is required", nameof(sessionId));
            if (!string.Equals(sessionId, SessionId, StringComparison.Ordinal))
                throw new InvalidOperationException("Epoch transitions cannot change the session id");
            if (nextEpoch <= AttemptEpoch)
                throw new ArgumentOutOfRangeException(nameof(nextEpoch));

            RecordStale("event-bus-stale-epoch");
            AttemptEpoch = nextEpoch;
            ClearQueuedGeneration();
        }

        private Subscription FindSubscription(SubscriptionToken token)
        {
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                if (_subscriptions[i].Token.Equals(token))
                    return _subscriptions[i];
            }
            return null;
        }

        private void ClearQueuedGeneration()
        {
            _pending.Clear();
            _admitted.Clear();
            _delivered.Clear();
            _retryCounts.Clear();
            RetryCount = 0;
            RejectedCount = 0;
        }

        private void RecordStale(string code)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                StaleCount++;
                RecordDiagnostic(code, _pending[i].Envelope,
                    _pending[i].RetryCount);
            }
        }

        private void RecordDiagnostic(string code, IEvent evt, int retryCount)
        {
            if (_diagnosticSink == null || evt == null) return;
            var envelope = new EventEnvelope(evt.SessionId, evt.AttemptEpoch,
                evt.Timestamp, evt.Publisher, evt.Identity,
                evt is IEventPhase phased ? phased.Phase : EventBusPhase.GameplayIngress,
                evt);
            RecordDiagnostic(code, envelope, retryCount);
        }

        private void RecordDiagnostic(string code, EventEnvelope envelope,
            int retryCount)
        {
            if (_diagnosticSink == null) return;
            int depth = _pending.Count;
            string queueName = "event-bus";
            string orderingKey = envelope == null ? string.Empty
                : envelope.Timestamp.ToString("R") + "|"
                    + envelope.Publisher + "|" + envelope.Identity;
            IEventDiagnosticDetailSink detailed =
                _diagnosticSink as IEventDiagnosticDetailSink;
            if (detailed != null)
            {
                detailed.RecordDetailed(code, envelope, retryCount, depth,
                    _capacity, queueName, orderingKey);
                return;
            }
            _diagnosticSink.Record(code, envelope, retryCount, depth, _capacity);
        }

        private void PruneInactiveSubscriptions()
        {
            for (int i = _subscriptions.Count - 1; i >= 0; i--)
            {
                if (!_subscriptions[i].Active)
                    _subscriptions.RemoveAt(i);
            }
        }

        private static int CompareQueuedEvents(QueuedEvent left, QueuedEvent right)
        {
            // Preserve per-publisher ingress order. Across publishers, timestamp,
            // publisher, identity, then sequence provide deterministic ordering.
            if (string.Equals(left.Envelope.Publisher, right.Envelope.Publisher,
                StringComparison.Ordinal))
                return left.Sequence.CompareTo(right.Sequence);

            int compare = left.Envelope.Timestamp.CompareTo(right.Envelope.Timestamp);
            if (compare != 0) return compare;
            compare = string.Compare(left.Envelope.Publisher, right.Envelope.Publisher,
                StringComparison.Ordinal);
            if (compare != 0) return compare;
            compare = string.Compare(left.Envelope.Identity, right.Envelope.Identity,
                StringComparison.Ordinal);
            return compare != 0 ? compare : left.Sequence.CompareTo(right.Sequence);
        }
    }

    /// <summary>
    /// Transitional static facade. New systems receive an IEventBus and call Drain
    /// from the session phase owner; this facade exists only for legacy composition.
    /// </summary>
    public static class EventBus
    {
        private static readonly SessionEventBus _default = new SessionEventBus();
        private static readonly Dictionary<Delegate, List<SubscriptionToken>> _legacyTokens =
            new Dictionary<Delegate, List<SubscriptionToken>>();

        public static string SessionId { get { return _default.SessionId; } }
        public static long AttemptEpoch { get { return _default.AttemptEpoch; } }

        public static SubscriptionToken Subscribe<T>(Action<T> handler) where T : IEvent
        {
            var token = _default.Subscribe(handler);
            List<SubscriptionToken> tokens;
            if (!_legacyTokens.TryGetValue(handler, out tokens))
            {
                tokens = new List<SubscriptionToken>();
                _legacyTokens.Add(handler, tokens);
            }
            tokens.Add(token);
            return token;
        }

        public static SubscriptionToken SubscribeHandoff<T>(
            Func<EventEnvelope, EventHandoffResult> handler) where T : IEvent
        {
            return _default.SubscribeHandoff<T>(handler);
        }

        public static bool Unsubscribe(SubscriptionToken token)
        {
            return _default.Unsubscribe(token);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            List<SubscriptionToken> tokens;
            if (handler == null || !_legacyTokens.TryGetValue(handler, out tokens))
                return;

            for (int i = 0; i < tokens.Count; i++)
                _default.Unsubscribe(tokens[i]);
            _legacyTokens.Remove(handler);
        }

        public static EventPublishResult Publish(IEvent evt)
        {
            // Stale metadata is rejected by the session bus. The compatibility
            // facade must not relabel a mutable event into the active generation.
            return _default.Publish(evt);
        }

        public static int Drain(EventBusPhase phase)
        {
            return _default.Drain(phase);
        }

        public static void BeginSession(string sessionId, long initialEpoch)
        {
            _default.BeginSession(sessionId, initialEpoch);
        }

        public static void BeginEpoch(string sessionId, long nextEpoch)
        {
            _default.BeginEpoch(sessionId, nextEpoch);
        }

        public static IEventBus Default { get { return _default; } }
    }
}
