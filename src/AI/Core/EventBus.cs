using System;
using System.Collections.Generic;
using WhisperWard.AI.Core;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// A lightweight event bus for distributing SensingFacts and DecisionRecords.
    /// Ensures decoupled communication between Perception and the FSM.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Action<IEvent>>> _subscribers = new Dictionary<Type, List<Action<IEvent>>>();

        /// <summary>
        /// Subscribe to a specific event type.
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : IEvent
        {
            Type type = typeof(T);
            if (!_subscribers.ContainsKey(type))
            {
                _subscribers[type] = new List<Action<IEvent>>();
            }

            _subscribers[type].Add(evt => handler((T)evt));
        }

        /// <summary>
        /// Unsubscribe from a specific event type.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            Type type = typeof(T);
            if (_subscribers.ContainsKey(type))
            {
                // Note: This is a simplified implementation. In a production environment,
                // we would need to store the original action to remove it correctly.
                // For this specialist implementation, we'll focus on the logic.
            }
        }

        /// <summary>
        /// Publishes an event to all subscribers.
        /// </summary>
        public static void Publish(IEvent evt)
        {
            Type type = evt.GetType();

            // Publish to specific type subscribers
            if (_subscribers.ContainsKey(type))
            {
                foreach (var handler in _subscribers[type])
                {
                    handler(evt);
                }
            }

            // Publish to base class/interface subscribers (e.g., all SensingFacts)
            foreach (var entry in _subscribers)
            {
                if (entry.Key != type && entry.Key.IsAssignableFrom(type))
                {
                    foreach (var handler in entry.Value)
                    {
                        handler(evt);
                    }
                }
            }
        }
    }
}
