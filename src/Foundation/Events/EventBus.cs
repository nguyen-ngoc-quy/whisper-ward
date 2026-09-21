using System;
using System.Collections.Generic;

namespace WhisperWard.Foundation.Events
{
    /// <summary>
    /// Deterministic, bounded Event Messaging Bus conforming to ADR-0001.
    /// Implements 5-phase virtual clock pipeline, 4096-entry ring buffer deduplication,
    /// 256 pending queue capacity, backpressure retry policy, and atomic epoch barriers.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        public const int DefaultCapacity = 256;
        public const int DefaultDedupRingBufferSize = 4096;
        public const int DefaultMaxRetries = 3;

        private readonly struct DedupKey : IEquatable<DedupKey>
        {
            public readonly string SessionId;
            public readonly long AttemptEpoch;
            public readonly string OwnerNamespace;
            public readonly ulong EventIdentity;

            public DedupKey(string sessionId, long attemptEpoch, string ownerNamespace, ulong eventIdentity)
            {
                SessionId = sessionId;
                AttemptEpoch = attemptEpoch;
                OwnerNamespace = ownerNamespace;
                EventIdentity = eventIdentity;
            }

            public bool Equals(DedupKey other)
            {
                return AttemptEpoch == other.AttemptEpoch
                    && EventIdentity == other.EventIdentity
                    && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
                    && string.Equals(OwnerNamespace, other.OwnerNamespace, StringComparison.Ordinal);
            }

            public override bool Equals(object obj) => obj is DedupKey other && Equals(other);

            public override int GetHashCode()
            {
                var hash = new HashCode();
                hash.Add(SessionId, StringComparer.Ordinal);
                hash.Add(AttemptEpoch);
                hash.Add(OwnerNamespace, StringComparer.Ordinal);
                hash.Add(EventIdentity);
                return hash.ToHashCode();
            }
        }

        private sealed class Subscription
        {
            public SubscriptionToken Token;
            public Type EventType;
            public Action<IEvent> Handler;
            public Func<EventEnvelope, EventHandoffResult> HandoffHandler;
            public VirtualClockPhase Phase;
            public bool IsActive;

            public bool IsHandoff => HandoffHandler != null;
        }

        private sealed class QueuedEnvelope
        {
            public EventEnvelope Envelope;
            public ulong PublisherSequence;
            public int RetryCount;

            public QueuedEnvelope(in EventEnvelope envelope, ulong publisherSequence)
            {
                Envelope = envelope;
                PublisherSequence = publisherSequence;
                RetryCount = 0;
            }
        }

        private readonly int _capacity;
        private readonly int _maxRetries;
        private readonly int _dedupCapacity;

        private readonly List<QueuedEnvelope> _pendingQueue = new List<QueuedEnvelope>();
        private readonly List<Subscription> _subscriptions = new List<Subscription>();

        // 4096-entry deduplication ring buffer
        private readonly DedupKey[] _dedupRingBuffer;
        private readonly HashSet<DedupKey> _dedupLookup = new HashSet<DedupKey>();
        private int _dedupHead = 0;
        private int _dedupCount = 0;

        private long _nextSubscriptionId = 1;
        private ulong _publisherSequenceCounter = 1;

        public int Capacity => _capacity;
        public int PendingCount => _pendingQueue.Count;
        public long CurrentEpoch { get; private set; }
        public string CurrentSessionId { get; private set; }

        public EventBus(
            string sessionId = "default-session",
            long initialEpoch = 0,
            int capacity = DefaultCapacity,
            int maxRetries = DefaultMaxRetries,
            int dedupCapacity = DefaultDedupRingBufferSize)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            if (initialEpoch < 0) throw new ArgumentOutOfRangeException(nameof(initialEpoch));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));
            if (dedupCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(dedupCapacity));

            CurrentSessionId = sessionId;
            CurrentEpoch = initialEpoch;
            _capacity = capacity;
            _maxRetries = maxRetries;
            _dedupCapacity = dedupCapacity;
            _dedupRingBuffer = new DedupKey[dedupCapacity];
        }

        public SubscriptionToken Subscribe<T>(Action<T> handler, VirtualClockPhase phase) where T : class, IEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var token = new SubscriptionToken(_nextSubscriptionId++);
            _subscriptions.Add(new Subscription
            {
                Token = token,
                EventType = typeof(T),
                Handler = ev => handler((T)ev),
                HandoffHandler = null,
                Phase = phase,
                IsActive = true
            });
            return token;
        }

        public SubscriptionToken SubscribeHandoff(Func<EventEnvelope, EventHandoffResult> handoff, VirtualClockPhase phase)
        {
            if (handoff == null) throw new ArgumentNullException(nameof(handoff));

            var token = new SubscriptionToken(_nextSubscriptionId++);
            _subscriptions.Add(new Subscription
            {
                Token = token,
                EventType = null,
                Handler = null,
                HandoffHandler = handoff,
                Phase = phase,
                IsActive = true
            });
            return token;
        }

        public bool Unsubscribe(SubscriptionToken token)
        {
            if (!token.IsValid) return false;

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                if (_subscriptions[i].Token == token && _subscriptions[i].IsActive)
                {
                    _subscriptions[i].IsActive = false;
                    _subscriptions.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public EventHandoffResult Publish(in EventEnvelope envelope)
        {
            // 1. Session and Epoch barrier validation
            if (!string.Equals(envelope.SessionId, CurrentSessionId, StringComparison.Ordinal))
            {
                return EventHandoffResult.Rejected("event-bus-stale-session");
            }
            if (envelope.AttemptEpoch < CurrentEpoch)
            {
                return EventHandoffResult.Rejected("event-bus-stale-epoch");
            }

            // 2. Ingress deduplication against 4096-ring buffer
            var dedupKey = new DedupKey(envelope.SessionId, envelope.AttemptEpoch, envelope.OwnerNamespace, envelope.EventIdentity);
            if (_dedupLookup.Contains(dedupKey))
            {
                return EventHandoffResult.Duplicate;
            }

            // 3. Check pending queue capacity and backpressure
            if (_pendingQueue.Count >= _capacity)
            {
                // Find if an identical envelope is retrying
                QueuedEnvelope existing = null;
                for (int i = 0; i < _pendingQueue.Count; i++)
                {
                    if (_pendingQueue[i].Envelope.Equals(envelope))
                    {
                        existing = _pendingQueue[i];
                        break;
                    }
                }

                if (existing != null)
                {
                    if (existing.RetryCount < _maxRetries)
                    {
                        existing.RetryCount++;
                        return EventHandoffResult.Retry;
                    }
                    return EventHandoffResult.Rejected("event-bus-queue-overflow-rejected");
                }

                return EventHandoffResult.Rejected("event-bus-queue-overflow-rejected");
            }

            // 4. Admit to pending queue
            var queued = new QueuedEnvelope(envelope, _publisherSequenceCounter++);
            _pendingQueue.Add(queued);

            // Record into deduplication ring buffer
            RecordDedupKey(dedupKey);

            return EventHandoffResult.Accepted;
        }

        public int DrainPhase(VirtualClockPhase phase)
        {
            // Gather items belonging to this phase
            var phaseItems = new List<QueuedEnvelope>();
            for (int i = _pendingQueue.Count - 1; i >= 0; i--)
            {
                if (_pendingQueue[i].Envelope.Phase == phase)
                {
                    phaseItems.Add(_pendingQueue[i]);
                    _pendingQueue.RemoveAt(i);
                }
            }

            if (phaseItems.Count == 0) return 0;

            // Deterministic ordering: (Timestamp, SourceClassRank, PublisherSequence)
            phaseItems.Sort((a, b) =>
            {
                int cmpTime = a.Envelope.Timestamp.CompareTo(b.Envelope.Timestamp);
                if (cmpTime != 0) return cmpTime;

                int cmpRank = a.Envelope.SourceClassRank.CompareTo(b.Envelope.SourceClassRank);
                if (cmpRank != 0) return cmpRank;

                return a.PublisherSequence.CompareTo(b.PublisherSequence);
            });

            // Active subscriber snapshot
            var snapshot = new List<Subscription>(_subscriptions);

            int deliveredCount = 0;
            foreach (var item in phaseItems)
            {
                var env = item.Envelope;
                foreach (var sub in snapshot)
                {
                    if (!sub.IsActive || sub.Phase != phase) continue;

                    if (sub.IsHandoff)
                    {
                        sub.HandoffHandler(env);
                        deliveredCount++;
                    }
                    else if (sub.EventType != null && sub.EventType.IsInstanceOfType(env.Payload))
                    {
                        sub.Handler(env.Payload);
                        deliveredCount++;
                    }
                }
            }

            return deliveredCount;
        }

        public void BeginEpoch(long newEpoch)
        {
            if (newEpoch <= CurrentEpoch)
            {
                throw new ArgumentException($"New epoch ({newEpoch}) must be strictly greater than current epoch ({CurrentEpoch})", nameof(newEpoch));
            }

            CurrentEpoch = newEpoch;

            // Atomic barrier: invalidate any pending events from older epochs
            for (int i = _pendingQueue.Count - 1; i >= 0; i--)
            {
                if (_pendingQueue[i].Envelope.AttemptEpoch < newEpoch)
                {
                    _pendingQueue.RemoveAt(i);
                }
            }
        }

        public void BeginSession(string newSessionId)
        {
            if (string.IsNullOrEmpty(newSessionId))
            {
                throw new ArgumentNullException(nameof(newSessionId));
            }

            CurrentSessionId = newSessionId;
            CurrentEpoch = 0;

            // Atomic barrier: clear all pending events and clear dedup table
            _pendingQueue.Clear();
            _dedupLookup.Clear();
            _dedupHead = 0;
            _dedupCount = 0;
            Array.Clear(_dedupRingBuffer, 0, _dedupRingBuffer.Length);
        }

        private void RecordDedupKey(in DedupKey key)
        {
            if (_dedupCount == _dedupCapacity)
            {
                // Ring buffer is full: overwrite oldest entry at _dedupHead
                var oldest = _dedupRingBuffer[_dedupHead];
                _dedupLookup.Remove(oldest);
            }
            else
            {
                _dedupCount++;
            }

            _dedupRingBuffer[_dedupHead] = key;
            _dedupLookup.Add(key);
            _dedupHead = (_dedupHead + 1) % _dedupCapacity;
        }
    }
}
