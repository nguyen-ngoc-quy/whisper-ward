using UnityEngine;

namespace WhisperWard.Core.Contracts
{
    /// <summary>
    /// Authoritative contract for the third-person camera rig and spatial orientation basis.
    /// Governed by ADR-0008.
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
        /// Updates the orbit rotation from mouse look input deltas while enforcing pitch clamping.
        /// Complies with AC-P25: zero character turning when input vector is zero.
        /// </summary>
        /// <param name="lookDelta">Raw or filtered look vector from the input system.</param>
        /// <param name="deltaTime">Elapsed frame time.</param>
        void UpdateLookRotation(Vector2 lookDelta, float deltaTime);

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
