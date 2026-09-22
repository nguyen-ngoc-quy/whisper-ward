using System;
using UnityEngine;
using WhisperWard.Foundation.Events;

namespace WhisperWard.Core.Contracts
{
    /// <summary>
    /// Event triggered when a guard captures the player.
    /// Used by CameraRigService to immediately abort active transitions and cut to capture focus (AC-CAM-12).
    /// </summary>
    public readonly struct PlayerCapturedEvent : IEvent, IEquatable<PlayerCapturedEvent>
    {
        public string EventType => "PlayerCaptured";

        /// <summary>
        /// World coordinates where the capture took place.
        /// </summary>
        public Vector3 CaptureLocation { get; }

        /// <summary>
        /// World coordinates of the capturing guard.
        /// </summary>
        public Vector3 GuardLocation { get; }

        /// <summary>
        /// Initializes a new instance of the PlayerCapturedEvent struct.
        /// </summary>
        /// <param name="captureLocation">World position of player when captured.</param>
        /// <param name="guardLocation">World position of capturing guard.</param>
        /// <example>
        /// <code>
        /// var evt = new PlayerCapturedEvent(playerTransform.position, guardTransform.position);
        /// cameraRig.OnPlayerCaptured(evt);
        /// </code>
        /// </example>
        public PlayerCapturedEvent(Vector3 captureLocation, Vector3 guardLocation)
        {
            CaptureLocation = captureLocation;
            GuardLocation = guardLocation;
        }

        public bool Equals(PlayerCapturedEvent other) =>
            CaptureLocation.Equals(other.CaptureLocation) && GuardLocation.Equals(other.GuardLocation);

        public override bool Equals(object obj) => obj is PlayerCapturedEvent other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(CaptureLocation, GuardLocation);

        public static bool operator ==(PlayerCapturedEvent left, PlayerCapturedEvent right) => left.Equals(right);
        public static bool operator !=(PlayerCapturedEvent left, PlayerCapturedEvent right) => !left.Equals(right);
    }

    /// <summary>
    /// Authoritative contract for the third-person camera rig and spatial orientation basis.
    /// Governed by ADR-0008, GDD #20, and control-manifest.md.
    /// </summary>
    public interface ICameraRigService
    {
        /// <summary>
        /// Gets the current horizontal camera azimuth angle in degrees.
        /// </summary>
        float CameraYaw { get; }

        /// <summary>
        /// Gets the normalized camera forward vector projected onto the horizontal XZ plane.
        /// Consumed by IPlayerController for camera-relative locomotion.
        /// </summary>
        Vector3 PlanarForward { get; }

        /// <summary>
        /// Gets the normalized camera right vector projected onto the horizontal XZ plane.
        /// Consumed by IPlayerController for camera-relative locomotion.
        /// </summary>
        Vector3 PlanarRight { get; }

        /// <summary>
        /// Gets the active vertical field of view in degrees.
        /// </summary>
        float CurrentFov { get; }

        /// <summary>
        /// Gets a value indicating whether chase FOV expansion is currently active.
        /// </summary>
        bool IsChaseFovActive { get; }

        /// <summary>
        /// Gets a value indicating whether the camera is actively framed inside a hide spot aperture.
        /// </summary>
        bool IsInHideSpotView { get; }

        /// <summary>
        /// Gets the current evaluated camera distance from target in meters.
        /// </summary>
        float CurrentDistance { get; }

        /// <summary>
        /// Gets the normalized dither opacity [0.15, 1.0] to prevent character geometry occlusion when compressed.
        /// Satisfies AC-CAM-08.
        /// </summary>
        float DitherOpacity { get; }

        /// <summary>
        /// Priority of the free orbit camera (CM_FreeOrbit, nominal 10).
        /// </summary>
        int OrbitPriority { get; }

        /// <summary>
        /// Priority of the hide spot camera (CM_HideSpot, 20 when inside, 5 when in orbit).
        /// </summary>
        int HideSpotPriority { get; }

        /// <summary>
        /// Priority of the capture focus camera (CM_CaptureFocus, 100 on capture).
        /// </summary>
        int CapturePriority { get; }

        /// <summary>
        /// Gets a value indicating whether a Cinemachine priority blend is currently actively transitioning.
        /// </summary>
        bool IsBlendActive { get; }

        /// <summary>
        /// Gets the normalized progress [0.0, 1.0] of the active blend.
        /// </summary>
        float BlendProgress { get; }

        /// <summary>
        /// Gets a value indicating whether an instantaneous capture focus cut has occurred.
        /// </summary>
        bool IsCaptureCutActive { get; }

        /// <summary>
        /// Gets the base outward normal direction of the current hide spot aperture.
        /// </summary>
        Vector3 BaseOutwardFacing { get; }

        /// <summary>
        /// Gets the target world position of the hide spot aperture.
        /// </summary>
        Vector3 ApertureTarget { get; }

        /// <summary>
        /// Updates the orbit rotation from mouse look input deltas while enforcing pitch clamping.
        /// Complies with AC-P25: zero character turning when input vector is zero.
        /// In HideSpot view, clamps yaw to a +/- 30 deg cone around the aperture outward normal (AC-CAM-11).
        /// </summary>
        /// <param name="lookDelta">Raw or filtered look vector from the input system.</param>
        /// <param name="deltaTime">Elapsed frame time.</param>
        /// <example>
        /// <code>
        /// cameraRig.UpdateLookRotation(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")), Time.deltaTime);
        /// </code>
        /// </example>
        void UpdateLookRotation(Vector2 lookDelta, float deltaTime);

        /// <summary>
        /// Evaluates camera occlusion via non-allocating SphereCast, executing instant collapse on collision
        /// and exponential damped recovery pull-out when clearance opens.
        /// Satisfies AC-CAM-06 and AC-CAM-07.
        /// </summary>
        /// <param name="targetPosition">Pivot origin position (player chest target).</param>
        /// <param name="cameraDirection">Normalized direction vector pointing from target toward camera eye.</param>
        /// <param name="currentDistance">Current distance before evaluation.</param>
        /// <param name="deltaTime">Elapsed frame time.</param>
        /// <returns>Evaluated actual camera distance in meters.</returns>
        float EvaluateCameraDistance(Vector3 targetPosition, Vector3 cameraDirection, float currentDistance, float deltaTime);

        /// <summary>
        /// Calculates character dither opacity based on actual camera distance.
        /// Returns 1.0 if distance > 0.60m, and scales down to 0.15 at 0.40m.
        /// Satisfies AC-CAM-08.
        /// </summary>
        /// <param name="actualDistance">Evaluated camera distance.</param>
        /// <returns>Dither opacity in range [0.15, 1.0].</returns>
        float CalculateDitherOpacity(float actualDistance);

        /// <summary>
        /// Applies the vertical leash override constraint on rapid descent.
        /// Satisfies AC-CAM-09.
        /// </summary>
        /// <param name="cameraPosition">Current camera position.</param>
        /// <param name="targetPosition">Target player position.</param>
        /// <param name="maxVerticalLeash">Maximum allowed vertical displacement (default 2.50m).</param>
        /// <returns>Adjusted camera position clamped within leash boundaries.</returns>
        Vector3 ApplyVerticalLeash(Vector3 cameraPosition, Vector3 targetPosition, float maxVerticalLeash = 2.50f);

        /// <summary>
        /// Sets whether pursuit tension is active, selecting target FOV (68 deg chase vs 60 deg exploration).
        /// Satisfies AC-CAM-10.
        /// </summary>
        /// <param name="isChaseActive">True if guard pursuit is active.</param>
        /// <example>
        /// <code>
        /// cameraRig.SetChaseFovActive(true);
        /// </code>
        /// </example>
        void SetChaseFovActive(bool isChaseActive);

        /// <summary>
        /// Updates dynamic field of view interpolation based on chase state and elapsed frame time.
        /// Satisfies AC-CAM-10.
        /// </summary>
        /// <param name="deltaTime">Elapsed frame time in seconds.</param>
        /// <returns>Evaluated field of view in degrees.</returns>
        /// <example>
        /// <code>
        /// float fov = cameraRig.UpdateFov(Time.deltaTime);
        /// </code>
        /// </example>
        float UpdateFov(float deltaTime);

        /// <summary>
        /// Transitions the camera blend to the constrained interior peep-hole virtual camera.
        /// Elevates CM_HideSpot priority to 20 and starts 0.35s EaseInOut blend.
        /// Satisfies AC-CAM-11.
        /// </summary>
        /// <param name="apertureTarget">Position of the hide spot viewing portal.</param>
        /// <param name="outwardFacing">Outward normal direction through the doorway.</param>
        /// <example>
        /// <code>
        /// cameraRig.SwitchToHideSpotView(spot.AperturePosition, spot.OutwardNormal);
        /// </code>
        /// </example>
        void SwitchToHideSpotView(Vector3 apertureTarget, Vector3 outwardFacing);

        /// <summary>
        /// Transitions the camera blend back to the primary free orbital follow virtual camera.
        /// Resets CM_HideSpot priority to 5 and initiates 0.35s return blend.
        /// </summary>
        /// <example>
        /// <code>
        /// cameraRig.SwitchToOrbitView();
        /// </code>
        /// </example>
        void SwitchToOrbitView();

        /// <summary>
        /// Updates virtual camera blend progress over frame time.
        /// </summary>
        /// <param name="deltaTime">Elapsed frame time in seconds.</param>
        /// <example>
        /// <code>
        /// cameraRig.UpdateBlend(Time.deltaTime);
        /// </code>
        /// </example>
        void UpdateBlend(float deltaTime);

        /// <summary>
        /// Aborts and resets any active virtual camera blend immediately (0 ms cut).
        /// Satisfies AC-CAM-12.
        /// </summary>
        /// <example>
        /// <code>
        /// cameraRig.CancelActiveBlend();
        /// </code>
        /// </example>
        void CancelActiveBlend();

        /// <summary>
        /// Handles guard capture event, immediately aborting active blends and prioritizing capture camera.
        /// Satisfies AC-CAM-12.
        /// </summary>
        /// <param name="evt">Capture event carrying player and guard locations.</param>
        /// <example>
        /// <code>
        /// cameraRig.OnPlayerCaptured(new PlayerCapturedEvent(playerPos, guardPos));
        /// </code>
        /// </example>
        void OnPlayerCaptured(PlayerCapturedEvent evt);
    }
}
