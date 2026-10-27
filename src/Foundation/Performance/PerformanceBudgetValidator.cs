using System;
using UnityEngine;

namespace WhisperWard.Foundation.Performance
{
    /// <summary>
    /// Encapsulates measured runtime frame metrics.
    /// </summary>
    public struct FrameMetrics
    {
        public float FrameTimeMs;
        public int DrawCalls;
        public float AITickDurationMs;
        public long GCHotPathAllocBytes;
        public RuntimePlatform TargetPlatform;

        public FrameMetrics(
            float frameTimeMs,
            int drawCalls,
            float aiTickDurationMs,
            long gcAllocBytes,
            RuntimePlatform targetPlatform = RuntimePlatform.WindowsPlayer)
        {
            FrameTimeMs = frameTimeMs;
            DrawCalls = drawCalls;
            AITickDurationMs = aiTickDurationMs;
            GCHotPathAllocBytes = gcAllocBytes;
            TargetPlatform = targetPlatform;
        }
    }

    /// <summary>
    /// Validates runtime execution against hard performance budgets defined in technical-preferences.md:
    /// - 60 FPS target framerate (<= 16.67ms frame time budget)
    /// - Draw calls <= 1000 on PC, <= 300 on WebGL demo
    /// - AI Update Budget <= 2.0ms per frame
    /// - Zero GC allocations on simulation tick hot paths
    /// Zero managed heap allocations during evaluation.
    /// </summary>
    public static class PerformanceBudgetValidator
    {
        public const float MaxFrameTimeMs = 16.67f; // 60 FPS target
        public const int MaxDrawCallsPC = 1000;
        public const int MaxDrawCallsWebGL = 300;
        public const float MaxAITickMs = 2.00f; // AI code rule: 2ms max update budget

        /// <summary>
        /// Validates that measured frame time meets the 60 FPS budget.
        /// </summary>
        public static bool ValidateFrametime(float frameTimeMs, out string error)
        {
            if (frameTimeMs > MaxFrameTimeMs)
            {
                error = $"Frame time {frameTimeMs:F2}ms exceeds 60 FPS budget ({MaxFrameTimeMs:F2}ms).";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates that draw calls are within platform limits.
        /// </summary>
        public static bool ValidateDrawCalls(int drawCalls, RuntimePlatform platform, out string error)
        {
            int limit = (platform == RuntimePlatform.WebGLPlayer) ? MaxDrawCallsWebGL : MaxDrawCallsPC;
            if (drawCalls > limit)
            {
                error = $"Draw calls {drawCalls} exceed platform limit ({limit}) for {platform}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates that aggregate AI tick duration is within the 2ms budget.
        /// </summary>
        public static bool ValidateAIBudget(float aiDurationMs, out string error)
        {
            if (aiDurationMs > MaxAITickMs)
            {
                error = $"AI update duration {aiDurationMs:F2}ms exceeds 2.0ms budget.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Validates that zero GC allocations occurred on hot paths.
        /// </summary>
        public static bool ValidateZeroGC(long gcAllocBytes, out string error)
        {
            if (gcAllocBytes > 0)
            {
                error = $"Hot-path allocated {gcAllocBytes} bytes of GC memory. Zero-allocation rule violated.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Comprehensive evaluation of a frame metrics sample.
        /// </summary>
        public static bool EvaluateMetrics(FrameMetrics metrics, out string failureReason)
        {
            if (!ValidateFrametime(metrics.FrameTimeMs, out failureReason))
            {
                return false;
            }

            if (!ValidateDrawCalls(metrics.DrawCalls, metrics.TargetPlatform, out failureReason))
            {
                return false;
            }

            if (!ValidateAIBudget(metrics.AITickDurationMs, out failureReason))
            {
                return false;
            }

            if (!ValidateZeroGC(metrics.GCHotPathAllocBytes, out failureReason))
            {
                return false;
            }

            failureReason = string.Empty;
            return true;
        }
    }
}
