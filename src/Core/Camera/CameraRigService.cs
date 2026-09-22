using System;
using UnityEngine;
using WhisperWard.Core.Contracts;

namespace WhisperWard.Core.Camera
{
    /// <summary>
    /// Delegate for non-allocating SphereCast operations, enabling deterministic EditMode testing.
    /// </summary>
    public delegate int SphereCastNonAllocDelegate(
        Vector3 origin,
        float radius,
        Vector3 direction,
        RaycastHit[] results,
        float maxDistance,
        int layerMask,
        QueryTriggerInteraction queryTriggerInteraction);

    /// <summary>
    /// Authoritative camera rig service implementing ICameraRigService.
    /// Governed by ADR-0008, GDD #20, and control-manifest.md.
    /// Manages right-shoulder orbit framing, pitch clamping [-35.0 deg, +65.0 deg],
    /// angular look spike limiting (720.0 deg/s), camera-relative planar basis vectors,
    /// pause TimeScale freeze invariants, real-time spherecast deocclusion (AC-CAM-06),
    /// exponential recovery damping (AC-CAM-07), character dither opacity (AC-CAM-08),
    /// and vertical leash hard override constraints (AC-CAM-09).
    /// </summary>
    public sealed class CameraRigService : ICameraRigService
    {
        public const float DefaultNominalDistance = 2.80f;
        public const float DefaultMinDistance = 0.40f;
        public const float DefaultSpherecastRadius = 0.20f;
        public const float DefaultRecoveryTau = 0.25f;
        public const float DefaultDitherThreshold = 0.60f;
        public const float DefaultMinDitherOpacity = 0.15f;
        public const float DefaultMaxVerticalLeash = 2.50f;
        public const float DefaultTargetHeight = 1.35f;
        public const float DefaultRightShoulderOffset = 0.35f;
        public const float DefaultMinPitch = -35.0f;
        public const float DefaultMaxPitch = 65.0f;
        public const float DefaultMaxAngularVelocity = 720.0f;
        public const float DefaultExplorationFov = 60.0f;
        public const float DefaultChaseFov = 68.0f;
        public const float DefaultMouseSensitivity = 1.0f;

        private float _cameraYaw;
        private float _cameraPitch;
        private float _currentFov;
        private float _currentDistance;
        private float _ditherOpacity;
        private bool _isInHideSpotView;
        private bool _isChaseFovActive;
        private readonly float _minPitch;
        private readonly float _maxPitch;
        private readonly float _maxAngularVelocity;
        private readonly float _mouseSensitivity;
        private readonly bool _invertY;
        private readonly int _worldLayerMask;
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[2];
        private Func<float> _timeScaleProvider;
        private SphereCastNonAllocDelegate _sphereCastOverride;

        /// <summary>
        /// Gets the current horizontal camera azimuth angle in degrees [0, 360).
        /// </summary>
        public float CameraYaw => _cameraYaw;

        /// <summary>
        /// Gets the current vertical camera pitch angle in degrees [-35.0, +65.0].
        /// </summary>
        public float CameraPitch => _cameraPitch;

        /// <summary>
        /// Gets the normalized camera forward vector projected onto the horizontal XZ plane.
        /// Maps azimuth theta to (sin theta, 0, cos theta).
        /// </summary>
        public Vector3 PlanarForward
        {
            get
            {
                float rad = _cameraYaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            }
        }

        /// <summary>
        /// Gets the normalized camera right vector projected onto the horizontal XZ plane.
        /// Maps azimuth theta to (cos theta, 0, -sin theta).
        /// </summary>
        public Vector3 PlanarRight
        {
            get
            {
                float rad = _cameraYaw * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
            }
        }

        /// <summary>
        /// Gets the active vertical field of view in degrees.
        /// </summary>
        public float CurrentFov => _currentFov;

        /// <summary>
        /// Gets a value indicating whether the camera is actively framed inside a hide spot aperture.
        /// </summary>
        public bool IsInHideSpotView => _isInHideSpotView;

        /// <summary>
        /// Gets a value indicating whether chase FOV expansion is currently engaged.
        /// </summary>
        public bool IsChaseFovActive => _isChaseFovActive;

        /// <summary>
        /// Gets the current evaluated camera distance from target in meters.
        /// </summary>
        public float CurrentDistance => _currentDistance;

        /// <summary>
        /// Gets the normalized dither opacity [0.15, 1.0] to prevent character geometry occlusion.
        /// </summary>
        public float DitherOpacity => _ditherOpacity;

        /// <summary>
        /// Initializes a new instance of CameraRigService.
        /// </summary>
        public CameraRigService(
            float initialYaw = 0.0f,
            float initialPitch = 10.0f,
            float mouseSensitivity = DefaultMouseSensitivity,
            float maxAngularVelocity = DefaultMaxAngularVelocity,
            float minPitch = DefaultMinPitch,
            float maxPitch = DefaultMaxPitch,
            bool invertY = false,
            int worldLayerMask = -1)
        {
            _cameraYaw = (initialYaw % 360f + 360f) % 360f;
            _minPitch = minPitch;
            _maxPitch = maxPitch;
            _cameraPitch = Mathf.Clamp(initialPitch, _minPitch, _maxPitch);
            _mouseSensitivity = mouseSensitivity;
            _maxAngularVelocity = maxAngularVelocity;
            _invertY = invertY;
            _worldLayerMask = worldLayerMask != -1 ? worldLayerMask : LayerMask.GetMask("World");
            _currentFov = DefaultExplorationFov;
            _currentDistance = DefaultNominalDistance;
            _ditherOpacity = 1.0f;
            _isInHideSpotView = false;
            _isChaseFovActive = false;
        }

        /// <summary>
        /// Sets custom test hooks for TimeScale and SphereCast delegates to support headless unit testing.
        /// </summary>
        public void SetTestHooks(Func<float> timeScaleProvider, SphereCastNonAllocDelegate sphereCastDelegate = null)
        {
            if (timeScaleProvider != null) _timeScaleProvider = timeScaleProvider;
            if (sphereCastDelegate != null) _sphereCastOverride = sphereCastDelegate;
        }

        /// <summary>
        /// Updates the orbit rotation from mouse look input deltas while enforcing pitch clamping.
        /// Satisfies AC-CAM-03, AC-CAM-04, and AC-CAM-05.
        /// </summary>
        public void UpdateLookRotation(Vector2 lookDelta, float deltaTime)
        {
            float timeScale = _timeScaleProvider != null ? _timeScaleProvider() : Time.timeScale;
            if (timeScale <= 0f || deltaTime <= 0f)
            {
                // AC-CAM-05: When paused or deltaTime is zero, freeze rotation and discard deltas
                return;
            }

            // AC-CAM-04: Clamp maximum angular change to prevent mouse flick disorientation
            float maxAngleThisFrame = _maxAngularVelocity * deltaTime;

            float rawYawDelta = lookDelta.x * _mouseSensitivity;
            float rawPitchDelta = lookDelta.y * _mouseSensitivity * (_invertY ? -1f : 1f);

            float appliedYawDelta = Mathf.Clamp(rawYawDelta, -maxAngleThisFrame, maxAngleThisFrame);
            float appliedPitchDelta = Mathf.Clamp(rawPitchDelta, -maxAngleThisFrame, maxAngleThisFrame);

            // Update Yaw with continuous wrap [0, 360)
            _cameraYaw = (_cameraYaw + appliedYawDelta) % 360f;
            if (_cameraYaw < 0f) _cameraYaw += 360f;

            // Update Pitch clamped to [-35.0 deg, +65.0 deg] (AC-CAM-03)
            _cameraPitch = Mathf.Clamp(_cameraPitch - appliedPitchDelta, _minPitch, _maxPitch);
        }

        /// <summary>
        /// Sets the yaw angle directly, wrapping to [0, 360).
        /// </summary>
        public void SetYaw(float yawDegrees)
        {
            _cameraYaw = (yawDegrees % 360f + 360f) % 360f;
        }

        /// <summary>
        /// Sets the pitch angle directly, clamping to valid range.
        /// </summary>
        public void SetPitch(float pitchDegrees)
        {
            _cameraPitch = Mathf.Clamp(pitchDegrees, _minPitch, _maxPitch);
        }

        /// <summary>
        /// Evaluates camera occlusion via non-allocating SphereCast, executing instant collapse on collision
        /// and exponential damped recovery pull-out when clearance opens.
        /// Satisfies AC-CAM-06 (instant collapse) and AC-CAM-07 (exponential recovery).
        /// Zero managed allocations (0 B GC).
        /// </summary>
        public float EvaluateCameraDistance(Vector3 targetPosition, Vector3 cameraDirection, float currentDistance, float deltaTime)
        {
            float targetDistance = DefaultNominalDistance;
            Vector3 dir = cameraDirection.sqrMagnitude > 1e-8f ? cameraDirection.normalized : -PlanarForward;

            int hits;
            if (_sphereCastOverride != null)
            {
                hits = _sphereCastOverride(
                    targetPosition,
                    DefaultSpherecastRadius,
                    dir,
                    _hitBuffer,
                    DefaultNominalDistance,
                    _worldLayerMask,
                    QueryTriggerInteraction.Ignore);
            }
            else
            {
                hits = Physics.SphereCastNonAlloc(
                    targetPosition,
                    DefaultSpherecastRadius,
                    dir,
                    _hitBuffer,
                    DefaultNominalDistance,
                    _worldLayerMask,
                    QueryTriggerInteraction.Ignore);
            }

            if (hits > 0)
            {
                float closestHit = _hitBuffer[0].distance;
                if (hits > 1 && _hitBuffer[1].distance < closestHit)
                {
                    closestHit = _hitBuffer[1].distance;
                }
                // AC-CAM-06: D_actual = max(D_min, d_hit - R_cam_col)
                targetDistance = Mathf.Max(DefaultMinDistance, closestHit - DefaultSpherecastRadius);
            }

            float newDistance;
            if (targetDistance < currentDistance)
            {
                // AC-CAM-06: Instantaneous collapse on collision within 0 ms (same frame)
                newDistance = targetDistance;
            }
            else if (deltaTime <= 0f)
            {
                newDistance = currentDistance;
            }
            else
            {
                // AC-CAM-07: Exponential damped recovery pull-out: D(t + dt) = D(t) + (D_target - D(t)) * (1 - e^(-dt / tau))
                float alpha = 1.0f - Mathf.Exp(-deltaTime / DefaultRecoveryTau);
                newDistance = currentDistance + (targetDistance - currentDistance) * alpha;
            }

            _currentDistance = newDistance;
            _ditherOpacity = CalculateDitherOpacity(newDistance);
            return newDistance;
        }

        /// <summary>
        /// Calculates character dither opacity based on actual camera distance.
        /// Satisfies AC-CAM-08: Opacity_dither = clamp((D_actual - 0.40) / 0.20, 0.15, 1.0).
        /// </summary>
        public float CalculateDitherOpacity(float actualDistance)
        {
            if (actualDistance > DefaultDitherThreshold)
            {
                return 1.0f;
            }
            return Mathf.Clamp(
                (actualDistance - DefaultMinDistance) / (DefaultDitherThreshold - DefaultMinDistance),
                DefaultMinDitherOpacity,
                1.0f);
        }

        /// <summary>
        /// Applies the vertical leash override constraint on rapid descent.
        /// Satisfies AC-CAM-09.
        /// </summary>
        public Vector3 ApplyVerticalLeash(Vector3 cameraPosition, Vector3 targetPosition, float maxVerticalLeash = DefaultMaxVerticalLeash)
        {
            return EnforceVerticalLeash(cameraPosition, targetPosition, maxVerticalLeash);
        }

        /// <summary>
        /// Static utility to enforce vertical leash constraint.
        /// Satisfies AC-CAM-09: snaps vertical displacement to leash limit when delta exceeds threshold.
        /// </summary>
        public static Vector3 EnforceVerticalLeash(Vector3 cameraPosition, Vector3 targetPosition, float maxVerticalLeash = DefaultMaxVerticalLeash)
        {
            float verticalDelta = cameraPosition.y - targetPosition.y;
            if (verticalDelta > maxVerticalLeash)
            {
                cameraPosition.y = targetPosition.y + maxVerticalLeash;
            }
            else if (verticalDelta < -maxVerticalLeash)
            {
                cameraPosition.y = targetPosition.y - maxVerticalLeash;
            }
            return cameraPosition;
        }

        /// <summary>
        /// Computes camera-relative planar basis from a 3D forward and up vector with pitch degeneracy fallback.
        /// Satisfies ADR-0008 Section 7.2.
        /// </summary>
        public static void CalculatePlanarBasisFromTransform(
            Vector3 cameraForward,
            Vector3 cameraUp,
            out Vector3 planarForward,
            out Vector3 planarRight)
        {
            Vector3 projForward = new Vector3(cameraForward.x, 0f, cameraForward.z);
            if (projForward.sqrMagnitude < 1e-8f)
            {
                // Degeneracy fallback when looking straight up or down (+/- 90 degrees)
                projForward = cameraForward.y > 0f
                    ? new Vector3(-cameraUp.x, 0f, -cameraUp.z)
                    : new Vector3(cameraUp.x, 0f, cameraUp.z);
            }

            planarForward = projForward.sqrMagnitude > 1e-8f ? projForward.normalized : Vector3.forward;
            planarRight = new Vector3(planarForward.z, 0f, -planarForward.x);
        }

        /// <summary>
        /// Calculates the normalized movement direction on the XZ plane from camera-relative basis vectors.
        /// Satisfies AC-CAM-01: prevents sqrt(2) diagonal speed glitch.
        /// </summary>
        public static Vector3 CalculateNormalizedMovement(Vector2 inputAxes, Vector3 planarForward, Vector3 planarRight)
        {
            if (inputAxes.sqrMagnitude < 1e-8f)
            {
                return Vector3.zero;
            }

            Vector2 normalizedInput = inputAxes.sqrMagnitude > 1.0f ? inputAxes.normalized : inputAxes;
            Vector3 worldMove = (planarForward * normalizedInput.y) + (planarRight * normalizedInput.x);
            return worldMove.sqrMagnitude > 1e-8f ? worldMove.normalized : Vector3.zero;
        }

        /// <summary>
        /// Modulates the camera field of view between exploration (60 deg) and pursuit tension (68 deg).
        /// </summary>
        public void SetChaseFovActive(bool isChaseActive)
        {
            _isChaseFovActive = isChaseActive;
            _currentFov = isChaseActive ? DefaultChaseFov : DefaultExplorationFov;
        }

        /// <summary>
        /// Transitions the camera blend to the constrained interior peep-hole virtual camera.
        /// </summary>
        public void SwitchToHideSpotView(Vector3 apertureTarget, Vector3 outwardFacing)
        {
            _isInHideSpotView = true;
        }

        /// <summary>
        /// Transitions the camera blend back to the primary free orbital follow virtual camera.
        /// </summary>
        public void SwitchToOrbitView()
        {
            _isInHideSpotView = false;
        }
    }
}
