using System;
using UnityEngine;

namespace WhisperWard.Core.Player
{
    /// <summary>
    /// Configuration data and kinematic tuning constants for player locomotion.
    /// Values strictly adhere to ADR-0006, GDD #11, and control-manifest.md.
    /// </summary>
    [Serializable]
    public sealed class PlayerConfig
    {
        /// <summary>
        /// Maximum horizontal speed while crouched (1.80 m/s).
        /// </summary>
        public float CrouchSpeed = 1.80f;

        /// <summary>
        /// Maximum horizontal speed while upright walking (3.60 m/s).
        /// </summary>
        public float WalkSpeed = 3.60f;

        /// <summary>
        /// Maximum horizontal sprint speed (6.25 m/s).
        /// </summary>
        public float RunSpeed = 6.25f;

        /// <summary>
        /// Guard chase speed reference for anti-kiting validation (7.50 m/s).
        /// </summary>
        public float GuardChaseSpeed = 7.50f;

        /// <summary>
        /// Guard patrol speed ceiling reference for crouch speed validation (2.30 m/s).
        /// </summary>
        public float GuardPatrolSpeed = 2.30f;

        /// <summary>
        /// Guard investigate speed ceiling reference for walk speed validation (5.00 m/s).
        /// </summary>
        public float GuardInvestigateSpeed = 5.00f;

        /// <summary>
        /// Time required to accelerate from zero to RunSpeed (0.08 s).
        /// </summary>
        public float AccelTime = 0.08f;

        /// <summary>
        /// Speed reduction multiplier applied when moving backward (0.70).
        /// </summary>
        public float BackpedalFactor = 0.70f;

        /// <summary>
        /// Threshold for dot product between move direction and facing vector to trigger backpedal penalty (-1e-4).
        /// </summary>
        public float BackpedalThreshold = -0.0001f;

        /// <summary>
        /// Minimum input vector magnitude below which input is treated as deadzone zero (1e-5).
        /// </summary>
        public float MinInputThreshold = 0.00001f;

        /// <summary>
        /// Continuous downward bias velocity to prevent false negative grounding (-3.0 m/s).
        /// </summary>
        public float DownwardGroundSnap = -3.0f;

        /// <summary>
        /// Upright standing capsule height (1.80 m).
        /// </summary>
        public float StandCapsuleHeight = 1.80f;

        /// <summary>
        /// Crouched capsule height (0.95 m).
        /// </summary>
        public float CrouchCapsuleHeight = 0.95f;

        /// <summary>
        /// Player capsule radius (0.35 m).
        /// </summary>
        public float CapsuleRadius = 0.35f;

        /// <summary>
        /// CharacterController physical skin width (0.025 m).
        /// </summary>
        public float SkinWidth = 0.025f;

        /// <summary>
        /// Upward stand clearance probe top inflation margin (0.03 m).
        /// </summary>
        public float ProbeTopMargin = 0.03f;

        /// <summary>
        /// CharacterController step offset while upright standing (0.30 m).
        /// </summary>
        public float StandStepOffset = 0.30f;

        /// <summary>
        /// CharacterController step offset while crouched (0.15 m).
        /// </summary>
        public float CrouchStepOffset = 0.15f;

        /// <summary>
        /// Standardized standoff displacement from hide spot entrance portal upon exit (1.20 m).
        /// </summary>
        public float HideSpotStandoffDistance = 1.20f;

        /// <summary>
        /// Maximum horizontal yaw clamp deviation in degrees relative to hide spot portal normal (+/- 60 deg).
        /// </summary>
        public float HideSpotYawClampDegrees = 60.0f;

        /// <summary>
        /// Upward offset from feet to lower hemisphere center for stand probe: R + skinWidth (0.375 m).
        /// Insets probe lower sphere above floor to prevent false floor collisions.
        /// </summary>
        public float StandProbeBottomOffset => CapsuleRadius + SkinWidth;

        /// <summary>
        /// Upward offset from feet to upper hemisphere center for stand probe: H_stand - R + ProbeTopMargin (1.48 m).
        /// </summary>
        public float StandProbeTopOffset => StandCapsuleHeight - CapsuleRadius + ProbeTopMargin;

        /// <summary>
        /// Base angular rotation speed in degrees per second (360 deg/s).
        /// Governed by GDD #11 and F3.
        /// </summary>
        public float TurnRate = 360.0f;

        /// <summary>
        /// Minimum turning authority multiplier surviving full-rear retreat (0.25).
        /// Governed by GDD #11 and F3.
        /// </summary>
        public float RearDampFloor = 0.25f;

        /// <summary>
        /// Maximum linear acceleration rate in m/s^2 (RunSpeed / AccelTime = 78.125 m/s^2).
        /// </summary>
        public float MaxAcceleration => AccelTime > 0f ? RunSpeed / AccelTime : 0f;

        /// <summary>
        /// Computes the proportional rear damping weight w(dot) per GDD #11 F3.
        /// </summary>
        /// <param name="dot">Dot product between move direction and character facing.</param>
        /// <returns>Damping factor in [RearDampFloor, 1.0].</returns>
        public float ComputeRearDampWeight(float dot)
        {
            if (dot >= 0f) return 1.0f;
            return 1.0f - (1.0f - RearDampFloor) * (-dot);
        }

        /// <summary>
        /// Creates a default player configuration matching design specifications.
        /// </summary>
        public static PlayerConfig CreateDefault() => new PlayerConfig();

        /// <summary>
        /// Validates configuration against all GDD #11 and ADR-0006 architectural constraints.
        /// </summary>
        /// <param name="errorMessage">Output description of the first detected violation, if any.</param>
        /// <returns>True if configuration satisfies all invariants; false otherwise.</returns>
        public bool ValidateConfig(out string errorMessage)
        {
            if (AccelTime <= 0f || AccelTime > 0.10f)
            {
                errorMessage = $"AccelTime ({AccelTime}s) must be in range (0, 0.10s].";
                return false;
            }

            if (BackpedalFactor <= 0f || BackpedalFactor > 1.0f)
            {
                errorMessage = $"BackpedalFactor ({BackpedalFactor}) must be in range (0, 1.0].";
                return false;
            }

            if (CrouchSpeed <= 0f || WalkSpeed <= CrouchSpeed || RunSpeed <= WalkSpeed)
            {
                errorMessage = $"Speed hierarchy violated: require 0 < Crouch ({CrouchSpeed}) < Walk ({WalkSpeed}) < Run ({RunSpeed}).";
                return false;
            }

            if (WalkSpeed >= GuardInvestigateSpeed)
            {
                errorMessage = $"WalkSpeed ({WalkSpeed} m/s) must be strictly less than GuardInvestigateSpeed ({GuardInvestigateSpeed} m/s).";
                return false;
            }

            if (CrouchSpeed >= GuardPatrolSpeed)
            {
                errorMessage = $"CrouchSpeed ({CrouchSpeed} m/s) must be strictly less than GuardPatrolSpeed ({GuardPatrolSpeed} m/s).";
                return false;
            }

            if (TurnRate <= 0f)
            {
                errorMessage = $"TurnRate ({TurnRate} deg/s) must be strictly positive.";
                return false;
            }

            if (RearDampFloor <= 0f || RearDampFloor >= 1.0f)
            {
                errorMessage = $"RearDampFloor ({RearDampFloor}) must be in range (0, 1.0).";
                return false;
            }

            float chaseRunRatio = GuardChaseSpeed / RunSpeed;
            if (chaseRunRatio < 1.20f - 1e-4f)
            {
                errorMessage = $"Anti-kiting invariant violated: V_chase / V_run ({chaseRunRatio:F3}) must be >= 1.20.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
