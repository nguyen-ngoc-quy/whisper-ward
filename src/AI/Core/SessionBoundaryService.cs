using System;
using System.Collections.Generic;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Lifecycle-owned session and attempt-epoch barrier. Consumers are notified
    /// before the bus generation advances so an FSM can publish its old-epoch
    /// stale closure; the bus then invalidates that closure and all other old work
    /// atomically before the new generation becomes active.
    /// </summary>
    public sealed class SessionBoundaryService : ISessionBoundary
    {
        private readonly IEventBus _eventBus;
        private readonly Dictionary<long, Action<string, long>> _listeners =
            new Dictionary<long, Action<string, long>>();
        private long _nextSubscription = 1;

        public SessionBoundaryService(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            SessionId = _eventBus.SessionId;
            AttemptEpoch = _eventBus.AttemptEpoch;
        }

        public string SessionId { get; private set; }
        public long AttemptEpoch { get; private set; }

        /// <summary>Stable code for the most recent listener failure, if any.</summary>
        public string LastListenerFailureCode { get; private set; }

        /// <summary>
        /// Identifies the transition currently being synchronously dispatched.
        /// This avoids inferring a new session from a potentially reused ID.
        /// </summary>
        public bool IsNewSessionTransition { get; private set; }

        /// <summary>
        /// Subscribes a listener to lifecycle barriers. Notifications are
        /// snapshot-dispatched before the bus invalidates the old generation so
        /// state owners can emit old-epoch stale closures.
        /// </summary>
        public SubscriptionToken Subscribe(Action<string, long> listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            long id = _nextSubscription++;
            _listeners.Add(id, listener);
            return new SubscriptionToken(id);
        }

        /// <summary>
        /// Removes a lifecycle listener.
        /// </summary>
        public bool Unsubscribe(SubscriptionToken token)
        {
            if (!token.IsValid) return false;
            return _listeners.Remove(token.Value);
        }

        /// <summary>
        /// Starts a new session and initial attempt epoch.
        /// </summary>
        public void BeginSession(string sessionId, long initialEpoch)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A session id is required", nameof(sessionId));
            if (initialEpoch < 0)
                throw new ArgumentOutOfRangeException(nameof(initialEpoch));

            IsNewSessionTransition = true;
            NotifyListeners(sessionId, initialEpoch);
            _eventBus.BeginSession(sessionId, initialEpoch);
            SessionId = sessionId;
            AttemptEpoch = initialEpoch;
        }

        /// <summary>
        /// Advances the current session to a new attempt epoch.
        /// </summary>
        public void BeginEpoch(long nextEpoch)
        {
            if (nextEpoch <= AttemptEpoch)
                throw new ArgumentOutOfRangeException(nameof(nextEpoch));

            IsNewSessionTransition = false;
            NotifyListeners(SessionId, nextEpoch);
            _eventBus.BeginEpoch(SessionId, nextEpoch);
            AttemptEpoch = nextEpoch;
        }

        private void NotifyListeners(string activeSessionId, long activeEpoch)
        {
            LastListenerFailureCode = string.Empty;
            var snapshot = new List<KeyValuePair<long, Action<string, long>>>(
                _listeners);
            for (int i = 0; i < snapshot.Count; i++)
            {
                try
                {
                    snapshot[i].Value(activeSessionId, activeEpoch);
                }
                catch (Exception)
                {
                    // One broken consumer must not prevent the bus generation
                    // barrier or the remaining lifecycle consumers from running.
                    LastListenerFailureCode =
                        "session-boundary-listener-failed:" + snapshot[i].Key;
                }
            }
        }
    }
}
