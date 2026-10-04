using System;
using UnityEngine;

namespace WhisperWard.UI.HUD
{
    /// <summary>
    /// Candidate guard threat entry passed to HUD for dominant threat evaluation.
    /// Zero heap allocations.
    /// </summary>
    public struct GuardThreatCandidate
    {
        public int GuardId;
        public Vector3 Position;
        public float Accumulator; // A in [0.0, 1.0]
        public float ResidualWariness; // R in [0.0, 1.0]
        public float BaseThreshold; // T_base (GDD default 0.30)
        public float ResidualRate; // k_res (GDD default 0.20)
        public float FloorThreshold; // T_floor (GDD default 0.10)
        public float ChaseThreshold; // T_chase (GDD default 1.00)
        public bool IsInChase;

        public float EntryThreshold => Mathf.Max(BaseThreshold - ResidualRate * ResidualWariness, FloorThreshold);

        public float ThreatRatio => EntryThreshold > 0.0001f ? Accumulator / EntryThreshold : 0f;

        public static GuardThreatCandidate Create(
            int guardId,
            Vector3 position,
            float accumulator,
            float residualWariness = 0f,
            bool isInChase = false,
            float baseThreshold = 0.30f,
            float residualRate = 0.20f,
            float floorThreshold = 0.10f,
            float chaseThreshold = 1.00f)
        {
            return new GuardThreatCandidate
            {
                GuardId = guardId,
                Position = position,
                Accumulator = Mathf.Clamp01(accumulator),
                ResidualWariness = Mathf.Clamp01(residualWariness),
                BaseThreshold = Mathf.Max(0.01f, baseThreshold),
                ResidualRate = Mathf.Max(0.0f, residualRate),
                FloorThreshold = Mathf.Max(0.01f, floorThreshold),
                ChaseThreshold = Mathf.Max(0.1f, chaseThreshold),
                IsInChase = isInChase
            };
        }
    }

    /// <summary>
    /// Evaluated presentation state of the Suspicion Meter HUD.
    /// Zero heap allocations.
    /// </summary>
    public struct SuspicionHUDState
    {
        public bool IsVisible;
        public bool IsChaseLocked;
        public int DominantGuardId;
        public float FillPercent; // 0..100% (A / T_chase)
        public float ThresholdNotchPercent; // 0..100% (T_entry / T_chase)
        public float ThreatRatio; // A / T_entry
        public float ResidualWariness; // R
        public float ChevronAzimuthDeg; // -180..180 degrees
        public Vector2 ChevronScreenOffset; // (x, y) pixels relative to screen center
    }

    /// <summary>
    /// Suspicion Meter & Threat Chevron HUD Controller.
    /// Determines the dominant threat candidate, enforces zero-threat quiet region suppression,
    /// calculates 360-degree planar azimuth chevron projection without camera singularities,
    /// and positions the dynamic threshold notch based on residual wariness.
    /// Conforms to GDD #6 Suspicion Meter / Grade Operator (Core Rules 1..3) and Story UI-SUSP-01.
    /// </summary>
    /// <example>
    /// <code>
    /// var hud = gameObject.AddComponent&lt;SuspicionMeterHUDController&gt;();
    /// hud.UpdateHUD(candidates, count, playerPos, cameraPos, cameraRot);
    /// bool active = hud.CurrentState.IsVisible;
    /// </code>
    /// </example>
    public sealed class SuspicionMeterHUDController : MonoBehaviour
    {
        [Header("Screen Ellipse Parameters")]
        [Tooltip("Horizontal radius of the HUD chevron screen ellipse in pixels (GDD: 240px).")]
        [SerializeField] private float _ellipseRadiusX = 240.0f;

        [Tooltip("Vertical radius of the HUD chevron screen ellipse in pixels (GDD: 160px).")]
        [SerializeField] private float _ellipseRadiusY = 160.0f;

        [Header("Runtime State")]
        [SerializeField] private SuspicionHUDState _currentState;

        /// <summary>Fires whenever the evaluated HUD state changes.</summary>
        public event Action<SuspicionHUDState> OnHUDStateChanged;

        public float EllipseRadiusX => _ellipseRadiusX;
        public float EllipseRadiusY => _ellipseRadiusY;
        public SuspicionHUDState CurrentState => _currentState;

        /// <summary>
        /// Configures chevron projection radii.
        /// </summary>
        public void Configure(float ellipseRadiusX = 240.0f, float ellipseRadiusY = 160.0f)
        {
            _ellipseRadiusX = Mathf.Max(10.0f, ellipseRadiusX);
            _ellipseRadiusY = Mathf.Max(10.0f, ellipseRadiusY);
        }

        /// <summary>
        /// Evaluates all guard candidates and updates the HUD state.
        /// Zero managed GC allocations per frame.
        /// </summary>
        public void UpdateHUD(
            GuardThreatCandidate[] candidates,
            int candidateCount,
            Vector3 playerPosition,
            Vector3 cameraPosition,
            Quaternion cameraRotation)
        {
            bool hasThreat = EvaluateDominantThreat(
                candidates,
                candidateCount,
                playerPosition,
                out GuardThreatCandidate dominant);

            if (!hasThreat)
            {
                // AC-UI-02: Zero-Threat Quiet Region Suppression
                _currentState = new SuspicionHUDState
                {
                    IsVisible = false,
                    IsChaseLocked = false,
                    DominantGuardId = 0,
                    FillPercent = 0f,
                    ThresholdNotchPercent = 0f,
                    ThreatRatio = 0f,
                    ResidualWariness = 0f,
                    ChevronAzimuthDeg = 0f,
                    ChevronScreenOffset = Vector2.zero
                };
                OnHUDStateChanged?.Invoke(_currentState);
                return;
            }

            // Calculate Planar Azimuth & Ellipse Offset (AC-UI-03)
            CalculateChevronProjection(
                dominant.Position,
                cameraPosition,
                cameraRotation,
                _ellipseRadiusX,
                _ellipseRadiusY,
                out float azimuthDeg,
                out Vector2 screenOffset);

            // Calculate Gauge fill and Dynamic Threshold Notch (AC-UI-04)
            float fillNormalized = Mathf.Clamp01(dominant.Accumulator / dominant.ChaseThreshold);
            float notchNormalized = Mathf.Clamp01(dominant.EntryThreshold / dominant.ChaseThreshold);
            bool isChase = dominant.IsInChase || dominant.Accumulator >= dominant.ChaseThreshold;

            _currentState = new SuspicionHUDState
            {
                IsVisible = true,
                IsChaseLocked = isChase,
                DominantGuardId = dominant.GuardId,
                FillPercent = fillNormalized * 100.0f,
                ThresholdNotchPercent = notchNormalized * 100.0f,
                ThreatRatio = dominant.ThreatRatio,
                ResidualWariness = dominant.ResidualWariness,
                ChevronAzimuthDeg = azimuthDeg,
                ChevronScreenOffset = screenOffset
            };

            OnHUDStateChanged?.Invoke(_currentState);
        }

        /// <summary>
        /// Evaluates the dominant threat guard using threat ratio and the GDD tie-breaking cascade.
        /// Zero managed allocations.
        /// </summary>
        /// <returns>True if a threat exists; false if in Zero-Threat Quiet Region.</returns>
        public static bool EvaluateDominantThreat(
            GuardThreatCandidate[] candidates,
            int candidateCount,
            Vector3 playerPosition,
            out GuardThreatCandidate dominant)
        {
            dominant = default;
            if (candidates == null || candidateCount <= 0)
            {
                return false;
            }

            int bestIndex = -1;
            float maxAccumulator = 0f;
            float bestThreatRatio = -1f;
            float bestDistanceSq = float.MaxValue;
            int bestGuardId = int.MaxValue;

            for (int i = 0; i < candidateCount; i++)
            {
                ref readonly GuardThreatCandidate c = ref candidates[i];

                if (c.Accumulator > maxAccumulator)
                {
                    maxAccumulator = c.Accumulator;
                }

                // If in active chase, threat ratio is maximum
                float currentThreatRatio = c.IsInChase ? 1000f + c.Accumulator : c.ThreatRatio;
                float currentDistSq = (c.Position - playerPosition).sqrMagnitude;

                if (bestIndex < 0)
                {
                    bestIndex = i;
                    bestThreatRatio = currentThreatRatio;
                    bestDistanceSq = currentDistSq;
                    bestGuardId = c.GuardId;
                    continue;
                }

                // Tie-breaking cascade:
                // 1. Threat ratio (r_threat = A / T_entry)
                if (currentThreatRatio > bestThreatRatio + 1e-4f)
                {
                    bestIndex = i;
                    bestThreatRatio = currentThreatRatio;
                    bestDistanceSq = currentDistSq;
                    bestGuardId = c.GuardId;
                }
                else if (Mathf.Abs(currentThreatRatio - bestThreatRatio) <= 1e-4f)
                {
                    // 2. Higher absolute accumulator A
                    if (c.Accumulator > candidates[bestIndex].Accumulator + 1e-4f)
                    {
                        bestIndex = i;
                        bestThreatRatio = currentThreatRatio;
                        bestDistanceSq = currentDistSq;
                        bestGuardId = c.GuardId;
                    }
                    else if (Mathf.Abs(c.Accumulator - candidates[bestIndex].Accumulator) <= 1e-4f)
                    {
                        // 3. Closer physical proximity (d_Euclid)
                        if (currentDistSq < bestDistanceSq - 1e-4f)
                        {
                            bestIndex = i;
                            bestThreatRatio = currentThreatRatio;
                            bestDistanceSq = currentDistSq;
                            bestGuardId = c.GuardId;
                        }
                        else if (Mathf.Abs(currentDistSq - bestDistanceSq) <= 1e-4f)
                        {
                            // 4. Ordinal ID priority
                            if (c.GuardId < bestGuardId)
                            {
                                bestIndex = i;
                                bestThreatRatio = currentThreatRatio;
                                bestDistanceSq = currentDistSq;
                                bestGuardId = c.GuardId;
                            }
                        }
                    }
                }
            }

            // Zero-threat invariant: If all guards have A == 0, quiet region
            if (maxAccumulator <= 0.0001f && !candidates[bestIndex].IsInChase)
            {
                dominant = default;
                return false;
            }

            dominant = candidates[bestIndex];
            return true;
        }

        /// <summary>
        /// Computes camera-local planar azimuth angle and maps it to a screen ellipse.
        /// Guarantees continuous 360-degree rotation without flipping when the guard is behind camera.
        /// Zero managed allocations.
        /// </summary>
        public static void CalculateChevronProjection(
            Vector3 guardPosition,
            Vector3 cameraPosition,
            Quaternion cameraRotation,
            float radiusX,
            float radiusY,
            out float azimuthDeg,
            out Vector2 screenOffset)
        {
            Vector3 delta = guardPosition - cameraPosition;
            Vector3 camLocal = Quaternion.Inverse(cameraRotation) * delta;

            // Compute azimuth in camera local X-Z plane
            // Forward (z > 0) -> 0 deg
            // Right (x > 0)   -> +90 deg
            // Behind (z < 0)  -> +/-180 deg
            // Left (x < 0)    -> -90 deg
            float azimuthRad = Mathf.Atan2(camLocal.x, camLocal.z);
            azimuthDeg = azimuthRad * Mathf.Rad2Deg;

            // Map continuous azimuth to screen ellipse
            // Sin maps horizontal X displacement, Cos maps vertical Y displacement
            float screenX = radiusX * Mathf.Sin(azimuthRad);
            float screenY = radiusY * Mathf.Cos(azimuthRad);
            screenOffset = new Vector2(screenX, screenY);
        }
    }
}
