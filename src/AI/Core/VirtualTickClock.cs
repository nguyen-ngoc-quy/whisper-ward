using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Manages the shared virtual tick clock for the Guard AI system.
    /// Ensures determinism by triggering system updates on discrete boundaries.
    /// </summary>
    public class VirtualTickClock : MonoBehaviour
    {
        public static VirtualTickClock Instance { get; private set; }

        [Header("Clock Settings")]
        [SerializeField] private float tickInterval = 0.5f; // Default 2Hz

        private float _timer = 0f;
        private long _currentTick = 0;

        public float TickInterval => tickInterval;
        public long CurrentTick => _currentTick;

        private readonly List<Action<long, float>> _tickListeners = new List<Action<long, float>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            if (_timer >= tickInterval)
            {
                _timer -= tickInterval;
                _currentTick++;

                NotifyListeners();
            }
        }

        public void Subscribe(Action<long, float> listener)
        {
            if (!_tickListeners.Contains(listener))
                _tickListeners.Add(listener);
        }

        public void Unsubscribe(Action<long, float> listener)
        {
            _tickListeners.Remove(listener);
        }

        private void NotifyListeners()
        {
            // Create a copy to avoid modification during iteration
            var listenersCopy = new List<Action<long, float>>(_tickListeners);
            foreach (var listener in listenersCopy)
            {
                listener(_currentTick, tickInterval);
            }
        }
    }
}
