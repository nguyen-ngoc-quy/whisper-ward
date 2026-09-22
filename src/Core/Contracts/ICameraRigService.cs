using UnityEngine;

namespace WhisperWard.Core.Contracts
{
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
        /// Updates the orbit rotation from mouse look input deltas while enforcing pitch clamping.
        /// Complies with AC-P25: zero character turning when input vector is zero.
        /// </summary>
        /// <param name="lookDelta">Raw or filtered look vector from the input system.</param>
        /// <param name="deltaTime">Elapsed frame time.</param>
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
        /// Modulates the camera field of view between exploration (60 deg) and pursuit tension (68 deg).
        /// </summary>
        /// <param name="isChaseActive">True if guard pursuit is active.</param>
        void SetChaseFovActive(bool isChaseActive);

        /// <summary>
        /// Transitions the camera blend to the constrained interior peep-hole virtual camera.
        /// </summary>
        /// <param name="apertureTarget">Position of the hide spot viewing portal.</param>
        /// <param name="outwardFacing">Outward normal direction through the doorway.</param>
        void SwitchToHideSpotView(Vector3 apertureTarget, Vector3 outwardFacing);

        /// <summary>
        /// Transitions the camera blend back to the primary free orbital follow virtual camera.
        /// </summary>
        void SwitchToOrbitView();
    }
}
