using System;
using UnityEngine;

namespace WhisperWard.AI.Perception
{
    /// <summary>
    /// Suspicion Accumulator and Residual Wariness Engine for Guard NPCs.
    /// Implements continuous distance-inverse charge (F2), residual wariness accumulation (F3),
    /// exponential residual decay (F4), dynamic Investigate threshold coupling (F5),
    /// confirm window timing with player-caused cancellation counter (F7, R7, R11),
    /// and Chase threshold preempt (F9).
    /// Conforms to GDD Perception R6, R7, R8, R9, R11, and Story GUARD-02 (AC-GUARD-05..08).
    /// </summary>
    /// <example>
    /// <code>
    /// var accumulator = guardObject.AddComponent&lt;SuspicionAccumulator&gt;();
    /// accumulator.Configure(kRate: 1.5f, maxRate: 0.60f, tBase: 0.30f, kRes: 0.24f);
    /// accumulator.OnConfirmWindowElapsed += () =&gt; Debug.Log("Investigate committed!");
    /// accumulator.Tick(deltaTime: 0.20f, hasLOS: true, distance: 3.0f, isReachable: true);
    /// </code>
    /// </example>
    public class SuspicionAccumulator : MonoBehaviour
    {
        [Header("Accumulator Parameters (F2)")]
        [Tooltip("Distance-inverse scaling constant k (GDD: 1.5).")]
        [SerializeField] private float _kRate = 1.5f;

        [Tooltip("Maximum charge rate in A/s (GDD: 0.60 A/s).")]
        [SerializeField] private float _maxRate = 0.60f;

        [Tooltip("Chase engagement threshold (GDD: 1.00).")]
        [SerializeField] private float _tChase = 1.00f;

        [Tooltip("Unreachable safety margin below T_chase (GDD: 0.02).")]
        [SerializeField] private float _deltaReach = 0.02f;

        [Header("Residual Wariness Parameters (F3, F4, F5)")]
        [Tooltip("Residual wariness charge rate per second of LOS (GDD: 0.10 /s).")]
        [SerializeField] private float _residualRate = 0.10f;

        [Tooltip("First-order residual decay time constant in seconds (GDD: 16.0s).")]
        [SerializeField] private float _residualTau = 16.0f;

        [Tooltip("Base Investigate entry threshold at R=0 (GDD: 0.30).")]
        [SerializeField] private float _tBase = 0.30f;

        [Tooltip("Residual-to-threshold coupling factor (GDD: 0.24).")]
        [SerializeField] private float _kRes = 0.24f;

        [Tooltip("Threshold floor multiplier (GDD: 0.20 * T_base = 0.06).")]
        [SerializeField] private float _floorMultiplier = 0.20f;

        [Header("Confirm Window & Cancel Cap (F7, R7, R11)")]
        [Tooltip("Confirm window duration in seconds (GDD: 1.2s).")]
        [SerializeField] private float _confirmWindowDuration = 1.2f;

        [Tooltip("Cap of player-caused cancellations triggering forced Investigate (GDD: 3).")]
        [SerializeField] private int _cancelCap = 3;

        [Tooltip("Fruitless investigation residual share increment (GDD: 0.15).")]
        [SerializeField] private float _fruitlessNoiseShare = 0.15f;

        [Tooltip("Fruitless investigation residual cap (GDD: 0.20).")]
        [SerializeField] private float _fruitlessNoiseCap = 0.20f;

        // Runtime state
        private float _accumulator;
        private float _residual;
        private bool _isWindowOpen;
        private float _windowTimer;
        private int _cancelCounter;
        private bool _isInChase;
        private bool _wasLOSPreviousTick;

        // Events
        /// <summary>Fires when current accumulator A value changes.</summary>
        public event Action<float> OnAccumulatorChanged;

        /// <summary>Fires when residual wariness R value changes.</summary>
        public event Action<float> OnResidualChanged;

        /// <summary>Fires when accumulator crosses T_entry and confirm window opens.</summary>
        public event Action<float, float> OnConfirmWindowOpened; // (accumulator, entryThreshold)

        /// <summary>Fires when confirm window elapses with sustained LOS, committing to Investigate.</summary>
        public event Action OnConfirmWindowElapsed;

        /// <summary>Fires when confirm window cancels due to LOS break.</summary>
        public event Action<bool, int> OnConfirmWindowCancelled; // (isPlayerCaused, currentCancelCounter)

        /// <summary>Fires when accumulator reaches T_chase, preempting to Chase state.</summary>
        public event Action OnChaseReached;

        /// <summary>Fires when cancel counter hits cancel cap, forcing immediate Investigate.</summary>
        public event Action OnCapReached;

        public float Accumulator => _accumulator;
        public float Residual => _residual;
        public bool IsWindowOpen => _isWindowOpen;
        public float WindowTimer => _windowTimer;
        public int CancelCounter => _cancelCounter;
        public bool IsInChase => _isInChase;

        public float KRate => _kRate;
        public float MaxRate => _maxRate;
        public float TChase => _tChase;
        public float TBase => _tBase;
        public float KRes => _kRes;
        public float ResidualTau => _residualTau;
        public float ConfirmWindowDuration => _confirmWindowDuration;
        public int CancelCap => _cancelCap;

        /// <summary>
        /// Calculates the dynamic Investigate entry threshold based on residual wariness:
        /// T_entry(R) = max(T_base - k_res * R, T_floor).
        /// </summary>
        public float EntryThreshold
        {
            get
            {
                float tFloor = _tBase * _floorMultiplier;
                return Mathf.Max(_tBase - _kRes * _residual, tFloor);
            }
        }

        /// <summary>
        /// Explicit dependency injection and configuration for runtime and unit tests.
        /// </summary>
        public void Configure(
            float kRate = 1.5f,
            float maxRate = 0.60f,
            float tBase = 0.30f,
            float kRes = 0.24f,
            float residualRate = 0.10f,
            float residualTau = 16.0f,
            float confirmWindowDuration = 1.2f,
            int cancelCap = 3,
            float tChase = 1.00f,
            float deltaReach = 0.02f)
        {
            _kRate = Mathf.Max(0.01f, kRate);
            _maxRate = Mathf.Max(0.01f, maxRate);
            _tBase = Mathf.Clamp(tBase, 0.05f, 1.0f);
            _kRes = Mathf.Max(0f, kRes);
            _residualRate = Mathf.Max(0f, residualRate);
            _residualTau = Mathf.Max(0.1f, residualTau);
            _confirmWindowDuration = Mathf.Max(0.1f, confirmWindowDuration);
            _cancelCap = Mathf.Max(1, cancelCap);
            _tChase = Mathf.Max(0.1f, tChase);
            _deltaReach = Mathf.Clamp(deltaReach, 0.001f, 0.2f);
        }

        /// <summary>
        /// Sets whether the guard is currently in Chase mode.
        /// During Chase, Accumulator and Residual are inert.
        /// When exiting Chase, Accumulator resets to 0 and Residual resumes decay.
        /// </summary>
        public void SetChaseMode(bool inChase)
        {
            _isInChase = inChase;
            if (_isInChase)
            {
                _accumulator = 0f;
                _isWindowOpen = false;
                _windowTimer = 0f;
            }
        }

        /// <summary>
        /// Advances the suspicion integration simulation.
        /// Evaluates continuous charge, threshold crossing, confirm window, and residual decay.
        /// Zero allocations.
        /// </summary>
        /// <param name="deltaTime">Elapsed time slice in seconds.</param>
        /// <param name="hasLOS">Whether line-of-sight to the player is currently maintained.</param>
        /// <param name="distance">Euclidean or path distance to player in meters.</param>
        /// <param name="isReachable">Whether the player is on a reachable navmesh surface.</param>
        /// <param name="isPlayerCausedBreak">If LOS broke this tick, whether the break was player-caused (e.g. displacement > deadzone).</param>
        public void Tick(
            float deltaTime,
            bool hasLOS,
            float distance,
            bool isReachable = true,
            bool isPlayerCausedBreak = true)
        {
            if (_isInChase)
            {
                // In Chase, Accumulator and Residual are both inert (GDD R8, F9).
                _wasLOSPreviousTick = hasLOS;
                return;
            }

            if (hasLOS)
            {
                // 1. Accumulator Charge (F2)
                float safeDist = Mathf.Max(0.1f, distance);
                float chargeRate = Mathf.Min(_kRate / safeDist, _maxRate);
                _accumulator += chargeRate * deltaTime;

                // Clamp accumulator
                float maxA = isReachable ? _tChase : (_tChase - _deltaReach);
                _accumulator = Mathf.Clamp(_accumulator, 0f, maxA);
                OnAccumulatorChanged?.Invoke(_accumulator);

                // 2. Residual Wariness Charge (F3)
                _residual += _residualRate * deltaTime;
                _residual = Mathf.Clamp01(_residual);
                OnResidualChanged?.Invoke(_residual);

                // 3. Chase Threshold Check (F9)
                if (_accumulator >= _tChase && isReachable)
                {
                    _isWindowOpen = false;
                    _windowTimer = 0f;
                    _accumulator = 0f;
                    OnChaseReached?.Invoke();
                    _wasLOSPreviousTick = true;
                    return;
                }

                // 4. Confirm Window Check (F7)
                float currentEntryThreshold = EntryThreshold;
                if (!_isWindowOpen && _accumulator >= currentEntryThreshold)
                {
                    _isWindowOpen = true;
                    _windowTimer = 0f;
                    OnConfirmWindowOpened?.Invoke(_accumulator, currentEntryThreshold);
                }

                if (_isWindowOpen)
                {
                    _windowTimer += deltaTime;
                    if (_windowTimer >= _confirmWindowDuration)
                    {
                        // Commit to Investigate!
                        _isWindowOpen = false;
                        _windowTimer = 0f;
                        _cancelCounter = 0; // Reset cancel counter on committed escalation
                        OnConfirmWindowElapsed?.Invoke();
                    }
                }
            }
            else
            {
                // No LOS this tick
                if (_wasLOSPreviousTick || _isWindowOpen)
                {
                    // Handle LOS break
                    if (_isWindowOpen)
                    {
                        _isWindowOpen = false;
                        _windowTimer = 0f;

                        if (isPlayerCausedBreak)
                        {
                            _cancelCounter++;
                            OnConfirmWindowCancelled?.Invoke(true, _cancelCounter);

                            if (_cancelCounter >= _cancelCap)
                            {
                                _cancelCounter = 0;
                                OnCapReached?.Invoke();
                            }
                        }
                        else
                        {
                            OnConfirmWindowCancelled?.Invoke(false, _cancelCounter);
                        }
                    }

                    // Reset accumulator to 0 immediately on LOS break (R6)
                    if (_accumulator > 0f)
                    {
                        _accumulator = 0f;
                        OnAccumulatorChanged?.Invoke(0f);
                    }
                }

                // Residual first-order decay in absence of LOS (F4): R <- R * exp(-dt / tau)
                if (_residual > 0f)
                {
                    float decayFactor = Mathf.Exp(-deltaTime / _residualTau);
                    _residual *= decayFactor;
                    if (_residual < 0.001f)
                    {
                        _residual = 0f;
                    }
                    OnResidualChanged?.Invoke(_residual);
                }
            }

            _wasLOSPreviousTick = hasLOS;
        }

        /// <summary>
        /// Accrues residual wariness from a fruitless investigation (F6).
        /// Applied when a noise or unverified investigation resolves with the player unfound.
        /// </summary>
        public void ApplyFruitlessInvestigationResidual()
        {
            float deltaR = Mathf.Min(_fruitlessNoiseShare, _fruitlessNoiseCap);
            _residual = Mathf.Clamp01(_residual + deltaR);
            OnResidualChanged?.Invoke(_residual);
        }

        /// <summary>
        /// Resets the accumulator immediately to 0 (e.g. on LOS break).
        /// </summary>
        public void ResetAccumulator()
        {
            _accumulator = 0f;
            _isWindowOpen = false;
            _windowTimer = 0f;
            OnAccumulatorChanged?.Invoke(0f);
        }

        /// <summary>
        /// Full reset of both accumulator, residual, confirm window, and cancel counter.
        /// Called on segment transitions or room resets.
        /// </summary>
        public void ResetAll()
        {
            _accumulator = 0f;
            _residual = 0f;
            _isWindowOpen = false;
            _windowTimer = 0f;
            _cancelCounter = 0;
            _isInChase = false;
            _wasLOSPreviousTick = false;
            OnAccumulatorChanged?.Invoke(0f);
            OnResidualChanged?.Invoke(0f);
        }

        /// <summary>
        /// Manually set residual value (used by tests and state restoration).
        /// </summary>
        public void SetResidual(float residual)
        {
            _residual = Mathf.Clamp01(residual);
            OnResidualChanged?.Invoke(_residual);
        }

        /// <summary>
        /// Manually set cancel counter (used by tests and state restoration).
        /// </summary>
        public void SetCancelCounter(int count)
        {
            _cancelCounter = Mathf.Max(0, count);
        }
    }
}
