using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Deterministic virtual clock service. Gameplay owns when Advance is called;
    /// the service never reads a singleton or silently resets on pooling.
    /// </summary>
    public sealed class VirtualTickClockService : IVirtualTickClock
    {
        /// <summary>
        /// Canonical high-frequency session step. Hearing and FSM schedules are
        /// derived boundaries; Burst consumes each step for its 1/120 s solver.
        /// </summary>
        public const float CanonicalTickInterval =
            BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds;

        private readonly Dictionary<long, Action<long, float>> _listeners =
            new Dictionary<long, Action<long, float>>();
        private long _nextSubscription = 1;
        private float _accumulator;
        private readonly float _maxFrameDeltaSeconds;

        public VirtualTickClockService(float tickInterval = CanonicalTickInterval)
            : this(tickInterval, NoiseRuntimeConfiguration.RegisteredMaxFrameDeltaSeconds)
        {
        }

        /// <summary>Creates a clock with registry-backed frame-delta clamping.</summary>
        public VirtualTickClockService(float tickInterval, float maxFrameDeltaSeconds)
        {
            if (tickInterval <= 0f || float.IsNaN(tickInterval)
                || float.IsInfinity(tickInterval))
                throw new ArgumentOutOfRangeException(nameof(tickInterval));
            if (Mathf.Abs(tickInterval - CanonicalTickInterval) > 0.000001f)
                throw new ArgumentException(
                    "clock-cadence-must-be-canonical-1-120", nameof(tickInterval));
            if (maxFrameDeltaSeconds <= 0f || float.IsNaN(maxFrameDeltaSeconds)
                || float.IsInfinity(maxFrameDeltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(maxFrameDeltaSeconds));
            if (Mathf.Abs(maxFrameDeltaSeconds
                    - NoiseRuntimeConfiguration.RegisteredMaxFrameDeltaSeconds)
                > 0.000001f)
                throw new ArgumentException(
                    "clock-max-frame-delta-not-registered",
                    nameof(maxFrameDeltaSeconds));
            TickInterval = CanonicalTickInterval;
            _maxFrameDeltaSeconds = maxFrameDeltaSeconds;
        }

        public long CurrentTick { get; private set; }
        public float CurrentTime { get { return CurrentTick * TickInterval; } }
        public float TickInterval { get; private set; }
        public bool IsPaused { get; private set; }

        public SubscriptionToken Subscribe(Action<long, float> listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            foreach (var item in _listeners)
            {
                if (item.Value == listener)
                    return new SubscriptionToken(item.Key);
            }

            long id = _nextSubscription++;
            _listeners.Add(id, listener);
            return new SubscriptionToken(id);
        }

        public bool Unsubscribe(SubscriptionToken token)
        {
            if (!token.IsValid) return false;
            return _listeners.Remove(token.Value);
        }

        public void Unsubscribe(Action<long, float> listener)
        {
            if (listener == null) return;
            long remove = 0;
            foreach (var item in _listeners)
            {
                if (item.Value == listener)
                {
                    remove = item.Key;
                    break;
                }
            }
            if (remove != 0) _listeners.Remove(remove);
        }

        public void Advance(float gameplayDelta)
        {
            if (float.IsNaN(gameplayDelta) || float.IsInfinity(gameplayDelta)
                || gameplayDelta < 0f)
                throw new ArgumentException("Gameplay delta must be finite and non-negative",
                    nameof(gameplayDelta));
            if (IsPaused || gameplayDelta == 0f) return;

            // Clamp per-call advance to the validated registry maximum; an
            // anomalous first-frame delta can never enqueue a large catch-up batch.
            float maxFrameDelta = _maxFrameDeltaSeconds;
            if (gameplayDelta > maxFrameDelta)
            {
                gameplayDelta = maxFrameDelta;
                Debug.Log($"[VirtualTickClock] clock-delta-clamped: gameplayDelta {gameplayDelta:F4}s clamped to {maxFrameDelta:F4}s");
            }

            _accumulator += gameplayDelta;
            while (_accumulator >= TickInterval)
            {
                _accumulator -= TickInterval;
                CurrentTick++;
                var snapshot = new List<Action<long, float>>(_listeners.Values);
                for (int i = 0; i < snapshot.Count; i++)
                    snapshot[i](CurrentTick, TickInterval);
            }
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
        }
    }

    /// <summary>
    /// Unity host adapter for the injected clock service. Render-frame driving is
    /// opt-in for tooling only; production gameplay must call AdvanceGameplayTime
    /// from the session owner using gameplay virtual time.
    /// </summary>
    public class VirtualTickClock : MonoBehaviour, IVirtualTickClock
    {
        [Header("Clock Settings")]
        [SerializeField] private float tickInterval = VirtualTickClockService.CanonicalTickInterval;
        [SerializeField] private bool driveFromRenderFrameForPreview = false;

        private VirtualTickClockService _service;

        private void Awake()
        {
            _service = new VirtualTickClockService(tickInterval);
        }

        private void Update()
        {
            if (driveFromRenderFrameForPreview)
                _service.Advance(Time.deltaTime);
        }

        public long CurrentTick { get { return _service == null ? 0 : _service.CurrentTick; } }
        public float CurrentTime { get { return _service == null ? 0f : _service.CurrentTime; } }
        public float TickInterval { get { return tickInterval; } }
        public bool IsPaused { get { return _service != null && _service.IsPaused; } }

        /// <summary>
        /// Supplies gameplay virtual time from the session/lifecycle owner.
        /// </summary>
        public void AdvanceGameplayTime(float gameplayDelta)
        {
            EnsureService();
            _service.Advance(gameplayDelta);
        }

        public SubscriptionToken Subscribe(Action<long, float> listener)
        {
            EnsureService();
            return _service.Subscribe(listener);
        }

        public bool Unsubscribe(SubscriptionToken token)
        {
            if (_service == null) return false;
            return _service.Unsubscribe(token);
        }

        public void Unsubscribe(Action<long, float> listener)
        {
            if (_service != null) _service.Unsubscribe(listener);
        }

        public void Advance(float gameplayDelta)
        {
            AdvanceGameplayTime(gameplayDelta);
        }

        public void SetPaused(bool paused)
        {
            EnsureService();
            _service.SetPaused(paused);
        }

        private void EnsureService()
        {
            if (_service == null)
                _service = new VirtualTickClockService(tickInterval);
        }
    }
}
