using System;
using UnityEngine;
using WhisperWard.Core.Contracts;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WhisperWard.Core.Camera
{
    /// <summary>
    /// Runtime MonoBehaviour driver bridging Unity hardware input, transforms, and rendering
    /// with the authoritative pure C# CameraRigService.
    /// Executes strictly in LateUpdate to guarantee zero-jitter tracking behind CharacterController physics.
    /// Governed by ADR-0008, GDD #20, and AC-SCENE-09 through AC-SCENE-12.
    /// </summary>
    /// <example>
    /// <code>
    /// var driver = mainCamera.AddComponent&lt;CameraOrbitDriver&gt;();
    /// driver.Configure(playerTransform, mainCamera);
    /// </code>
    /// </example>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class CameraOrbitDriver : MonoBehaviour
    {
        [Header("Target Tracking")]
        [Tooltip("Transform of the character root to follow (e.g. Player_Capsule).")]
        [SerializeField] private Transform _followTarget;

        [Tooltip("Vertical height above target root to place the look pivot (nominally 1.35m chest height).")]
        [SerializeField] private float _targetHeight = CameraRigService.DefaultTargetHeight;

        [Tooltip("Horizontal right-shoulder cinematography offset (nominally +0.35m).")]
        [SerializeField] private float _rightShoulderOffset = CameraRigService.DefaultRightShoulderOffset;

        [Header("Rig Distance & Occlusion")]
        [Tooltip("Default unoccluded camera orbit distance.")]
        [SerializeField] private float _nominalDistance = CameraRigService.DefaultNominalDistance;

        [Tooltip("Layer mask for static world geometry evaluated for spherecast deocclusion (Layer 20: World).")]
        [SerializeField] private LayerMask _occlusionMask = 1 << 20;

        [Header("Look Controls")]
        [Tooltip("Mouse sensitivity multiplier for yaw and pitch look rotation.")]
        [SerializeField] private float _mouseSensitivity = CameraRigService.DefaultMouseSensitivity;

        [Tooltip("Maximum allowed angular look speed in degrees per second (clamps mouse flicks).")]
        [SerializeField] private float _maxAngularVelocity = CameraRigService.DefaultMaxAngularVelocity;

        [Tooltip("Minimum pitch angle in degrees (looking up, nominal -35 deg).")]
        [SerializeField] private float _minPitch = CameraRigService.DefaultMinPitch;

        [Tooltip("Maximum pitch angle in degrees (looking down, nominal +65 deg).")]
        [SerializeField] private float _maxPitch = CameraRigService.DefaultMaxPitch;

        [Tooltip("Inverts vertical pitch control when true.")]
        [SerializeField] private bool _invertY = false;

        [Header("Output Camera")]
        [SerializeField] private UnityEngine.Camera _camera;

        [Header("Debug Visualization")]
        [SerializeField] private bool _drawDebugGizmos = true;

        private CameraRigService _rigService;
        private float _currentDistance;
        private Vector2 _injectedLookDelta;
        private bool _hasInjectedLookDelta;
        private bool _isInitialized;

        /// <summary>
        /// Gets the underlying pure C# camera rig service instance.
        /// </summary>
        public CameraRigService RigService => _rigService;

        /// <summary>
        /// Gets or sets the target transform followed by this camera rig.
        /// </summary>
        public Transform FollowTarget
        {
            get => _followTarget;
            set => _followTarget = value;
        }

        /// <summary>
        /// Gets the evaluated actual distance from pivot to camera eye in meters.
        /// </summary>
        public float CurrentDistance => _currentDistance;

        /// <summary>
        /// Gets the target camera component driven by this rig.
        /// </summary>
        public UnityEngine.Camera TargetCamera => _camera;

        private void Awake()
        {
            InitializeDriver();
        }

        /// <summary>
        /// Initializes the camera rig service and resolves component references.
        /// </summary>
        /// <example>
        /// <code>
        /// driver.InitializeDriver();
        /// </code>
        /// </example>
        public void InitializeDriver()
        {
            if (_isInitialized) return;

            if (_camera == null)
            {
                _camera = GetComponent<UnityEngine.Camera>();
                if (_camera == null) _camera = UnityEngine.Camera.main;
            }

            if (_followTarget == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) _followTarget = player.transform;
            }

            float initialYaw = transform.eulerAngles.y;
            float initialPitch = 10.0f;

            _rigService = new CameraRigService(
                initialYaw: initialYaw,
                initialPitch: initialPitch,
                mouseSensitivity: _mouseSensitivity,
                maxAngularVelocity: _maxAngularVelocity,
                minPitch: _minPitch,
                maxPitch: _maxPitch,
                invertY: _invertY,
                worldLayerMask: _occlusionMask.value
            );

            _currentDistance = _nominalDistance;
            _isInitialized = true;
        }

        /// <summary>
        /// Configures the driver dependencies explicitly for dependency injection and automated testing.
        /// </summary>
        /// <param name="followTarget">Target transform to follow.</param>
        /// <param name="targetCamera">Unity Camera component to update.</param>
        /// <param name="rigService">Optional custom CameraRigService instance.</param>
        /// <example>
        /// <code>
        /// driver.Configure(playerGo.transform, cameraComponent);
        /// </code>
        /// </example>
        public void Configure(Transform followTarget, UnityEngine.Camera targetCamera, CameraRigService rigService = null)
        {
            _followTarget = followTarget;
            _camera = targetCamera;
            if (rigService != null)
            {
                _rigService = rigService;
            }
            else
            {
                _isInitialized = false;
                InitializeDriver();
            }
        }

        /// <summary>
        /// Injects an artificial look delta vector for headless simulation and automated integration tests.
        /// </summary>
        /// <param name="lookDelta">Look delta in pixels or mouse units (X = yaw, Y = pitch).</param>
        /// <example>
        /// <code>
        /// driver.InjectLookDelta(new Vector2(15f, 0f));
        /// </code>
        /// </example>
        public void InjectLookDelta(Vector2 lookDelta)
        {
            _injectedLookDelta = lookDelta;
            _hasInjectedLookDelta = true;
        }

        /// <summary>
        /// Sets chase FOV expansion state (68 deg during chase/sprint vs 60 deg exploration).
        /// Satisfies AC-SCENE-12 and AC-CAM-10.
        /// </summary>
        /// <param name="active">True to engage chase FOV.</param>
        /// <example>
        /// <code>
        /// driver.SetChaseFov(true);
        /// </code>
        /// </example>
        public void SetChaseFov(bool active)
        {
            if (_rigService != null)
            {
                _rigService.SetChaseFovActive(active);
            }
        }

        /// <summary>
        /// Transitions to hide spot viewport with aperture constraints.
        /// Satisfies AC-SCENE-12 and AC-CAM-11.
        /// </summary>
        /// <param name="apertureTarget">Position of the hide spot portal.</param>
        /// <param name="outwardFacing">Outward-facing normal direction.</param>
        /// <example>
        /// <code>
        /// driver.SwitchToHideSpotView(alcoveCenter, Vector3.forward);
        /// </code>
        /// </example>
        public void SwitchToHideSpotView(Vector3 apertureTarget, Vector3 outwardFacing)
        {
            if (_rigService != null)
            {
                _rigService.SwitchToHideSpotView(apertureTarget, outwardFacing);
            }
        }

        /// <summary>
        /// Restores standard free orbit camera view when exiting a hide spot.
        /// Satisfies AC-SCENE-12.
        /// </summary>
        /// <example>
        /// <code>
        /// driver.SwitchToOrbitView();
        /// </code>
        /// </example>
        public void SwitchToOrbitView()
        {
            if (_rigService != null)
            {
                _rigService.SwitchToOrbitView();
            }
        }

        /// <summary>
        /// Orchestrates camera rotation, deocclusion spherecast, vertical leash, and FOV update.
        /// Executes strictly in LateUpdate to eliminate physics tracking jitter (AC-SCENE-09).
        /// Zero managed heap allocations per frame (0 B GC).
        /// </summary>
        private void LateUpdate()
        {
            if (_rigService == null) return;

            float dt = Time.deltaTime;
            Vector2 lookDelta = PollLookDelta();

            // 1. Update rotation with angular limits and pitch clamping [-35 deg, +65 deg]
            _rigService.UpdateLookRotation(lookDelta, dt);

            // 2. Compute target chest pivot and right-shoulder offset
            Vector3 targetBasePos = _followTarget != null ? _followTarget.position : transform.position;
            Vector3 chestPivot = targetBasePos + (Vector3.up * _targetHeight);

            Quaternion rot = Quaternion.Euler(_rigService.CameraPitch, _rigService.CameraYaw, 0f);
            Vector3 shoulderRight = rot * Vector3.right;
            Vector3 camBack = rot * Vector3.back;

            Vector3 shoulderPivot = chestPivot + (shoulderRight * _rightShoulderOffset);

            // 3. Real-time spherecast deocclusion against Layer 20 (World) with asymmetric recovery
            _currentDistance = _rigService.EvaluateCameraDistance(shoulderPivot, camBack, _currentDistance, dt);

            // 4. Compute camera world position and enforce vertical leash
            Vector3 desiredPosition = shoulderPivot + (camBack * _currentDistance);
            desiredPosition = _rigService.ApplyVerticalLeash(desiredPosition, targetBasePos);

            transform.position = desiredPosition;
            transform.rotation = Quaternion.LookRotation(shoulderPivot - desiredPosition, Vector3.up);

            // 5. Update dynamic field of view (60 deg to 68 deg)
            if (_camera != null)
            {
                _camera.fieldOfView = _rigService.UpdateFov(dt);
            }
        }

        private Vector2 PollLookDelta()
        {
            if (_hasInjectedLookDelta)
            {
                Vector2 injected = _injectedLookDelta;
                _injectedLookDelta = Vector2.zero;
                _hasInjectedLookDelta = false;
                return injected;
            }

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue();
            }
#endif
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawDebugGizmos) return;

            Vector3 targetBasePos = _followTarget != null ? _followTarget.position : transform.position;
            Vector3 chestPivot = targetBasePos + (Vector3.up * _targetHeight);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(chestPivot, 0.10f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(chestPivot, transform.position);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, CameraRigService.DefaultSpherecastRadius);
        }
    }
}
