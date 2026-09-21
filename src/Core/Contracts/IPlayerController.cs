using UnityEngine;

namespace WhisperWard.Core.Contracts
{
    /// <summary>
    /// Stance state of the player character.
    /// </summary>
    public enum MovementStance
    {
        /// <summary>
        /// Crouched posture with lowered capsule height (0.95m).
        /// </summary>
        Crouched = 0,

        /// <summary>
        /// Upright standing posture with standard capsule height (1.80m).
        /// </summary>
        Standing = 1
    }

    /// <summary>
    /// Fine-grained locomotion state governing kinematic speeds and animation sets.
    /// </summary>
    public enum LocomotionState
    {
        /// <summary>
        /// Zero planar movement velocity; character preserves last facing direction.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Low-profile crouched movement at 1.80 m/s.
        /// </summary>
        Crouch = 1,

        /// <summary>
        /// Standard upright walking movement at 3.60 m/s.
        /// </summary>
        Walk = 2,

        /// <summary>
        /// High-speed sprint movement at 6.25 m/s.
        /// </summary>
        Run = 3,

        /// <summary>
        /// Stationary concealment inside a designated HideSpot volume.
        /// </summary>
        InHideSpot = 4
    }

    /// <summary>
    /// Immutable zero-allocation state snapshot emitted by the player controller.
    /// Consumed on hot paths by Perception and Player Noise systems.
    /// </summary>
    public readonly struct PlayerLocomotionSnapshot
    {
        /// <summary>
        /// World position of the player capsule base.
        /// </summary>
        public readonly Vector3 Position;

        /// <summary>
        /// Current linear velocity vector in world space.
        /// </summary>
        public readonly Vector3 Velocity;

        /// <summary>
        /// Normalized horizontal facing vector of the character model.
        /// </summary>
        public readonly Vector3 Facing;

        /// <summary>
        /// Active locomotion state.
        /// </summary>
        public readonly LocomotionState State;

        /// <summary>
        /// Active physical stance.
        /// </summary>
        public readonly MovementStance Stance;

        /// <summary>
        /// Effective horizontal speed magnitude in meters per second.
        /// </summary>
        public readonly float EffectiveSpeed;

        /// <summary>
        /// Current vertical height of the CharacterController capsule.
        /// </summary>
        public readonly float CapsuleHeight;

        /// <summary>
        /// True if stand headroom query detected an overhead ceiling obstacle.
        /// </summary>
        public readonly bool IsStandBlocked;

        /// <summary>
        /// True if the character is grounded against floor geometry.
        /// </summary>
        public readonly bool IsGrounded;

        /// <summary>
        /// Initializes a new immutable locomotion snapshot.
        /// </summary>
        public PlayerLocomotionSnapshot(
            Vector3 position,
            Vector3 velocity,
            Vector3 facing,
            LocomotionState state,
            MovementStance stance,
            float effectiveSpeed,
            float capsuleHeight,
            bool isStandBlocked,
            bool isGrounded)
        {
            Position = position;
            Velocity = velocity;
            Facing = facing;
            State = state;
            Stance = stance;
            EffectiveSpeed = effectiveSpeed;
            CapsuleHeight = capsuleHeight;
            IsStandBlocked = isStandBlocked;
            IsGrounded = isGrounded;
        }
    }

    /// <summary>
    /// Primary contract for the kinematic player locomotion controller.
    /// Governed by ADR-0006.
    /// </summary>
    public interface IPlayerController
    {
        /// <summary>
        /// Gets the current immutable locomotion snapshot without managed allocations.
        /// </summary>
        PlayerLocomotionSnapshot CurrentSnapshot { get; }

        /// <summary>
        /// Teleports the character instantaneously to a world transform, resetting velocity.
        /// </summary>
        /// <param name="worldPosition">Target world coordinates.</param>
        /// <param name="worldRotation">Target world orientation.</param>
        void Teleport(Vector3 worldPosition, Quaternion worldRotation);

        /// <summary>
        /// Transitions the controller into the InHideSpot containment state.
        /// Locks horizontal translation and aligns with the interior anchor.
        /// </summary>
        /// <param name="interiorAnchor">Interior focal position.</param>
        /// <param name="portalForward">Outward normal vector from the hide spot opening.</param>
        void EnterHideSpot(Vector3 interiorAnchor, Vector3 portalForward);

        /// <summary>
        /// Exits hide spot containment and places the character at the specified standoff point.
        /// </summary>
        /// <param name="exitPosition">World position outside the hide spot threshold.</param>
        void ExitHideSpot(Vector3 exitPosition);

        /// <summary>
        /// Suspends player input processing (e.g. during cutscenes or capture sequences).
        /// </summary>
        void SeverControl();

        /// <summary>
        /// Restores active player input processing.
        /// </summary>
        void RestoreControl();
    }
}
