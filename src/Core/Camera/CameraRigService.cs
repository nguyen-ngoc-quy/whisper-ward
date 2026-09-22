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
    /// vertical leash hard override constraints (AC-CAM-09), dynamic chase FOV scaling (AC-CAM-10),
    /// HideSpot aperture yaw cone clamping (AC-CAM-11), and capture event blend cancellation (AC-CAM-12).
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
        public const float DefaultChaseTau = 0.18f;
        public const float DefaultRelaxTau = 0.45f;
        public const float DefaultBlendDuration = 0.35f;
        public const float DefaultHideSpotYawHalfCone = 30.0f;
        public const int DefaultOrbitPriority = 10;
        public const int DefaultHideSpotActivePriority = 20;
        public const int DefaultHideSpotInactivePriority = 5;
        public const int DefaultCapturePriority = 100;
        public const float DefaultMouseSensitivity = 1.0f;

        private float _cameraYaw;
        private float _cameraPitch;
        private float _currentFov;
        private float _currentDistance;
        private float _ditherOpacity;
        private bool _isInHideSpotView;
        private bool _isChaseFovActive;
        private int _orbitPriority;
        private int _hideSpotPriority;
        private int _capturePriority;
        private bool _isBlendActive;
        private float _blendProgress;
        private bool _isCaptureCutActive;
        private Vector3 _baseOutwardFacing;
        private Vector3 _apertureTarget;
        private Vector3 _captureLocation;
        private Vector3 _guardLocation;

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
        /// Gets a value indicating whether chase FOV expansion is currently engaged.
        /// </summary>
        public bool IsChaseFovActive => _isChaseFovActive;

        /// <summary>
        /// Gets a value indicating whether the camera is actively framed inside a hide spot aperture.
        /// </summary>
        public bool IsInHideSpotView => _isInHideSpotView;

        /// <summary>
        /// Gets the current evaluated camera distance from target in meters.
        /// </summary>
        public float CurrentDistance => _currentDistance;

        /// <summary>
        /// Gets the normalized dither opacity [0.15, 1.0] to prevent character geometry occlusion.
        /// </summary>
        public float DitherOpacity => _ditherOpacity;

        /// <summary>
        /// Priority of the free orbit camera (CM_FreeOrbit, nominal 10).
        /// </summary>
        public int OrbitPriority => _orbitPriority;

        /// <summary>
        /// Priority of the hide spot camera (CM_HideSpot, 20 when inside, 5 when in orbit).
        /// </summary>
        public int HideSpotPriority => _hideSpotPriority;

        /// <summary>
        /// Priority of the capture focus camera (CM_CaptureFocus, 100 on capture).
        /// </summary>
        public int CapturePriority => _capturePriority;

        /// <summary>
        /// Gets a value indicating whether a Cinemachine priority blend is currently actively transitioning.
        /// </summary>
        public bool IsBlendActive => _isBlendActive;

        /// <summary>
        /// Gets the normalized progress [0.0, 1.0] of the active blend.
        /// </summary>
        public float BlendProgress => _blendProgress;

        /// <summary>
        /// Gets a value indicating whether an instantaneous capture focus cut has occurred.
        /// </summary>
        public bool IsCaptureCutActive => _isCaptureCutActive;

        /// <summary>
        /// Gets the base outward normal direction of the current hide spot aperture.
        /// </summary>
        public Vector3 BaseOutwardFacing => _baseOutwardFacing;

        /// <summary>
        /// Gets the target world position of the hide spot aperture.
        /// </summary>
        public Vector3 ApertureTarget => _apertureTarget;

        /// <summary>
        /// Gets the recorded capture location in world space.
        /// </summary>
        public Vector3 CaptureLocation => _captureLocation;

        /// <summary>
        /// Gets the recorded guard location at time of capture in world space.
        /// </summary>
        public Vector3 GuardLocation => _guardLocation;

        /// <summary>
        /// Initializes a new instance of CameraRigService.
        /// </summary>
        /// <example>
        /// <code>
        /// var cameraService = new CameraRigService(initialYaw: 0f, initialPitch: 10f);
        /// </code>
        /// </example>
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
            _orbitPriority = DefaultOrbitPriority;
            _hideSpotPriority = DefaultHideSpotInactivePriority;
            _capturePriority = 0;
            _isBlendActive = false;
            _blendProgress = 0.0f;
            _isCaptureCutActive = false;
            _baseOutwardFacing = Vector3.forward;
            _apertureTarget = Vector3.zero;
            _captureLocation = Vector3.zero;
            _guardLocation = Vector3.zero;
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
        /// Satisfies AC-CAM-03, AC-CAM-04, AC-CAM-05, and AC-CAM-11 (HideSpot yaw cone clamp).
        /// </summary>
        /// <example>
        /// <code>
        /// cameraRig.UpdateLookRotation(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")), Time.deltaTime);
        /// </code>
        /// </example>
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

            // AC-CAM-11: In HideSpot view, clamp camera yaw to +/- 30 deg cone facing outward
            if (_isInHideSpotView)
            {
                float baseYaw = Mathf.Atan2(_baseOutwardFacing.x, _baseOutwardFacing.z) * Mathf.Rad2Deg;
                baseYaw = (baseYaw % 360f + 360f) % 360f;

                float deltaAngle = Mathf.DeltaAngle(baseYaw, _cameraYaw);
                float clampedDelta = Mathf.Clamp(deltaAngle, -DefaultHideSpotYawHalfCone, DefaultHideSpotYawHalfCone);

                _cameraYaw = (baseYaw + clampedDelta) % 360f;
                if (_cameraYaw < 0f) _cameraYaw += 360f;
            }

            // Update Pitch clamped to [-35.0 deg, +65.0 deg] (AC-CAM-03)
            _cameraPitch = Mathf.Clamp(_cameraPitch - appliedPitchDelta, _minPitch, _maxPitch);
        }

        /// <summary>
        /// Sets the yaw angle directly, wrapping to [0, 360).
        /// In HideSpot view, strictly clamps to +/- 30 deg cone around aperture outward normal.
        /// </summary>
        /// <example>
        /// <code>
        /// cameraRig.SetYaw(45.0f);
        /// </code>
        /// </example>
        public void SetYaw(float yawDegrees)
        {
            _cameraYaw = (yawDegrees % 360f + 360f) % 360f;
            if (_isInHideSpotView)
            {
                float baseYaw = Mathf.Atan2(_baseOutwardFacing.x, _baseOutwardFacing.z) * Mathf.Rad2Deg;
                baseYaw = (baseYaw % 360f + 360f) % 360f;
                float deltaAngle = Mathf.DeltaAngle(baseYaw, _cameraYaw);
                float clampedDelta = Mathf.Clamp(deltaAngle, -DefaultHideSpotYawHalfCone, DefaultHideSpotYawHalfCone);
                _cameraYaw = (baseYaw + clampedDelta) % 360f;
                if (_cameraYaw < 0f) _cameraYaw += 360f;
            }
        }

        /// <summary>
        /// Sets the pitch angle directly, clamping to valid range.
        /// </summary>
        /// <example>
        /// <code>
        /// cameraRig.SetPitch(15.0f);
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// float distance = cameraRig.EvaluateCameraDistance(chestPos, camDir, curDist, Time.deltaTime);
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// float opacity = cameraRig.CalculateDitherOpacity(distance);
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// Vector3 clampedPos = cameraRig.ApplyVerticalLeash(camPos, playerPos, 2.50f);
        /// </code>
        /// </example>
        public Vector3 ApplyVerticalLeash(Vector3 cameraPosition, Vector3 targetPosition, float maxVerticalLeash = DefaultMaxVerticalLeash)
        {
            return EnforceVerticalLeash(cameraPosition, targetPosition, maxVerticalLeash);
        }

        /// <summary>
        /// Static utility to enforce vertical leash constraint.
        /// Satisfies AC-CAM-09: snaps vertical displacement to leash limit when delta exceeds threshold.
        /// </summary>
        /// <example>
        /// <code>
        /// Vector3 clamped = CameraRigService.EnforceVerticalLeash(camPos, targetPos);
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// CameraRigService.CalculatePlanarBasisFromTransform(cam.forward, cam.up, out var fwd, out var right);
        /// </code>
        /// </example>
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
        /// <example>
        /// <code>
        /// Vector3 move = CameraRigService.CalculateNormalizedMovement(wasd, planarFwd, planarRight);
        /// </code>
        /// </example>
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
        /// Sets whether pursuit tension is active, selecting target FOV (68 deg chase vs 60 deg exploration).
        /// Satisfies AC-CAM-10.
        /// </summary>
        /// <example>
        /// <code>
        /// cameraRig.SetChaseFovActive(true);
        /// </code>
        /// </example>
        public void SetChaseFovActive(bool isChaseActive)
        {
            _isChaseFovActive = isChaseActive;
        }

        /// <summary>
        /// Updates dynamic field of view interpolation based on chase state and elapsed frame time.
        /// Smoothly expands from 60 deg to 68 deg with tau = 0.18s during chase.
        /// Smoothly contracts back to 60 deg with tau = 0.45s upon evasion.
        /// Satisfies AC-CAM-10.
        /// </summary>
        /// <param name="deltaTime">Elapsed frame time in seconds.</param>
        /// <returns>Evaluated field of view in degrees.</returns>
        /// <example>
        /// <code>
        /// float fov = cameraRig.UpdateFov(Time.deltaTime);
        /// </code>
        /// </example>
        public float UpdateFov(float deltaTime)
        {
            _currentFov = EvaluateFov(_currentFov, _isChaseFovActive, deltaTime);
            return _currentFov;
        }

        /// <summary>
        /// Evaluates dynamic FOV scaling with exponential smoothing.
        /// Satisfies AC-CAM-10.
        /// </summary>
        /// <param name="currentFov">Starting FOV in degrees.</param>
        /// <param name="isChaseActive">Whether chase tension is active.</param>
        /// <param name="deltaTime">Elapsed frame time in seconds.</param>
        /// <returns>Interpolated FOV in degrees.</returns>
        /// <example>
        /// <code>
        /// float fov = CameraRigService.EvaluateFov(60.0f, true, 0.54f);
        /// </code>
        /// </example>
        public static float EvaluateFov(float currentFov, bool isChaseActive, float deltaTime)
        {
            float targetFov = isChaseActive ? DefaultChaseFov : DefaultExplorationFov;
            float tau = isChaseActive ? DefaultChaseTau : DefaultRelaxTau;
            if (deltaTime <= 0f)
            {
                return currentFov;
            }
            float alpha = 1.0f - Mathf.Exp(-deltaTime / tau);
            return currentFov + (targetFov - currentFov) * alpha;
        }

        /// <summary>
        /// Transitions the camera blend to the constrained interior peep-hole virtual camera.
        /// Elevates CM_HideSpot priority to 20, aligns yaw with portal outward normal, and initiates 0.35s EaseInOut blend.
        /// Satisfies AC-CAM-11.
        /// </summary>
        /// <param name="apertureTarget">Position of the hide spot viewing portal.</param>
        /// <param name="outwardFacing">Outward normal direction through the doorway.</param>
        /// <example>
        /// <code>
        /// cameraRig.SwitchToHideSpotView(spot.AperturePosition, spot.OutwardNormal);
        /// </code>
        /// </example>
        public void SwitchToHideSpotView(Vector3 apertureTarget, Vector3 outwardFacing)
        {
            _isInHideSpotView = true;
            _apertureTarget = apertureTarget;
            _baseOutwardFacing = outwardFacing.sqrMagnitude > 1e-8f ? outwardFacing.normalized : Vector3.forward;
            _hideSpotPriority = DefaultHideSpotActivePriority;
            _isBlendActive = true;
            _blendProgress = 0.0f;

            // Align camera yaw to the outward facing vector
            float baseYaw = Mathf.Atan2(_baseOutwardFacing.x, _baseOutwardFacing.z) * Mathf.Rad2Deg;
            _cameraYaw = (baseYaw % 360f + 360f) % 360f;
        }

        /// <summary>
        /// Transitions the camera blend back to the primary free orbital follow virtual camera.
        /// Resets CM_HideSpot priority to 5, initiates 0.35s return blend, and restores 360 deg orbit.
        /// </summary>
        /// <example>
        /// <code>
        /// cameraRig.SwitchToOrbitView();
        /// </code>
        /// </example>
        public void SwitchToOrbitView()
        {
            _isInHideSpotView = false;
            _hideSpotPriority = DefaultHideSpotInactivePriority;
            _isBlendActive = true;
            _blendProgress = 0.0f;
        }

        /// <summary>
        /// Updates virtual camera blend progress over frame time.
        /// </summary>
        /// <param name="deltaTime">Elapsed frame time in seconds.</param>
        /// <example>
        /// <code>
        /// cameraRig.UpdateBlend(Time.deltaTime);
        /// </code>
        /// </example>
        public void UpdateBlend(float deltaTime)
        {
            if (!_isBlendActive || deltaTime <= 0f)
            {
                return;
            }

            _blendProgress = Mathf.Clamp01(_blendProgress + (deltaTime / DefaultBlendDuration));
            if (_blendProgress >= 1.0f)
            {
                _isBlendActive = false;
            }
        }

        /// <summary>
        /// Aborts and resets any active virtual camera blend immediately (0 ms cut).
        /// Satisfies AC-CAM-12.
        /// </summary>
        /// <example>
        /// <code>
        /// cameraRig.CancelActiveBlend();
        /// </code>
        /// </example>
        public void CancelActiveBlend()
        {
            _isBlendActive = false;
            _blendProgress = 0.0f;
        }

        /// <summary>
        /// Handles guard capture event, immediately aborting active blends and prioritizing capture camera (Priority 100).
        /// Satisfies AC-CAM-12.
        /// </summary>
        /// <param name="evt">Capture event carrying player and guard locations.</param>
        /// <example>
        /// <code>
        /// cameraRig.OnPlayerCaptured(new PlayerCapturedEvent(playerPos, guardPos));
        /// </code>
        /// </example>
        public void OnPlayerCaptured(PlayerCapturedEvent evt)
        {
            // AC-CAM-12: Abort active blend immediately
            CancelActiveBlend();

            _capturePriority = DefaultCapturePriority;
            _isCaptureCutActive = true;
            _captureLocation = evt.CaptureLocation;
            _guardLocation = evt.GuardLocation;

            // Orient camera directly toward capturing guard
            Vector3 lookDir = evt.GuardLocation - evt.CaptureLocation;
            if (lookDir.sqrMagnitude > 1e-8f)
            {
                lookDir.Normalize();
                float targetYaw = Mathf.Atan2(lookDir.x, lookDir.z) * Mathf.Rad2Deg;
                _cameraYaw = (targetYaw % 360f + 360f) % 360f;

                float planarDist = Mathf.Sqrt(lookDir.x * lookDir.x + lookDir.z * lookDir.z);
                float targetPitch = -Mathf.Atan2(lookDir.y, planarDist) * Mathf.Rad2Deg;
                _cameraPitch = Mathf.Clamp(targetPitch, _minPitch, _maxPitch);
            }
        }

        /// <summary>
        /// Resets the capture focus cut override, returning camera control to normal priorities.
        /// </summary>
        /// <example>
        /// <code>
        /// cameraRig.ResetCaptureCut();
        /// </code>
        /// </example>
        public void ResetCaptureCut()
        {
            _isCaptureCutActive = false;
            _capturePriority = 0;
        }
    }
}
