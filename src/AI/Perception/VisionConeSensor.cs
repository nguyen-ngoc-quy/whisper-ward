using System;
using UnityEngine;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.AI.Perception
{
    /// <summary>
    /// Vision Cone Sensor component for Guard NPCs.
    /// Evaluates horizontal FOV, distance limit, stance-adaptive target height,
    /// and Layer 20 (World) occlusion via non-allocating linecasts at 5 Hz cadence.
    /// Conforms to GDD Perception R1, R2, R3, E20, and Story GUARD-01 (AC-GUARD-01..04).
    /// </summary>
    /// <example>
    /// <code>
    /// var sensor = guardObject.AddComponent&lt;VisionConeSensor&gt;();
    /// sensor.Configure(physicsQueryService, 12f, 100f, 0.2f, 1 &lt;&lt; 20);
    /// sensor.SetTarget(playerTransform);
    /// sensor.OnLOSGained += pos =&gt; Debug.Log($"Spotted player at {pos}");
    /// </code>
    /// </example>
    public class VisionConeSensor : MonoBehaviour
    {
        [Header("Vision Configuration")]
        [Tooltip("Maximum detection range in meters (GDD: 12.0m).")]
        [SerializeField] private float _visionRange = 12.0f;

        [Tooltip("Horizontal field-of-view angle in degrees (GDD: 100.0 deg).")]
        [SerializeField] private float _visionFOV = 100.0f;

        [Tooltip("Height of the guard's eye above its feet anchor (default: 1.60m).")]
        [SerializeField] private float _eyeHeight = 1.60f;

        [Tooltip("Target evaluation height above feet when standing (GDD: 1.35m).")]
        [SerializeField] private float _standTargetHeight = 1.35f;

        [Tooltip("Target evaluation height above feet when crouching (GDD: 0.80m).")]
        [SerializeField] private float _crouchTargetHeight = 0.80f;

        [Tooltip("Occlusion LayerMask for solid geometry (Layer 20 World).")]
        [SerializeField] private LayerMask _occlusionMask = 1 << 20;

        [Tooltip("Sensing cadence interval in seconds (GDD: 5 Hz = 0.20s).")]
        [SerializeField] private float _tickInterval = 0.20f;

        [Header("Target Tracking")]
        [SerializeField] private Transform _target;
        [SerializeField] private bool _isTargetCrouching;

        private IPhysicsQueryService _physicsQuery;
        private float _tickTimer;
        private bool _hasLOS;
        private bool _inCone;
        private float _targetDistance;
        private float _targetAngle;
        private Vector3 _lastKnownPosition;
        private float _timeSinceLastLOS;
        private bool _isInitialized;

        /// <summary>Fires when line of sight is gained to the target.</summary>
        public event Action<Vector3> OnLOSGained;

        /// <summary>Fires when line of sight to the target is lost.</summary>
        public event Action<Vector3> OnLOSLost;

        public float VisionRange => _visionRange;
        public float VisionFOV => _visionFOV;
        public float EyeHeight => _eyeHeight;
        public float StandTargetHeight => _standTargetHeight;
        public float CrouchTargetHeight => _crouchTargetHeight;
        public LayerMask OcclusionMask => _occlusionMask;
        public float TickInterval => _tickInterval;

        public bool HasLOS => _hasLOS;
        public bool InCone => _inCone;
        public float TargetDistance => _targetDistance;
        public float TargetAngle => _targetAngle;
        public Vector3 LastKnownPosition => _lastKnownPosition;
        public float TimeSinceLastLOS => _timeSinceLastLOS;
        public Transform Target => _target;
        public bool IsTargetCrouching => _isTargetCrouching;

        public Vector3 EyePosition => transform.position + Vector3.up * _eyeHeight;

        private void Awake()
        {
            if (!_isInitialized)
            {
                // Fallback to default Layer 20 if mask is unset
                if (_occlusionMask.value == 0)
                {
                    int worldLayer = LayerMask.NameToLayer("World");
                    _occlusionMask = worldLayer != -1 ? (1 << worldLayer) : (1 << 20);
                }
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Explicit dependency injection and configuration for runtime and testing.
        /// Zero allocations.
        /// </summary>
        public void Configure(
            IPhysicsQueryService physicsQuery,
            float visionRange = 12.0f,
            float visionFOV = 100.0f,
            float tickInterval = 0.20f,
            int occlusionMask = 1 << 20,
            float eyeHeight = 1.60f,
            float standTargetHeight = 1.35f,
            float crouchTargetHeight = 0.80f)
        {
            _physicsQuery = physicsQuery;
            _visionRange = Mathf.Max(0.1f, visionRange);
            _visionFOV = Mathf.Clamp(visionFOV, 1.0f, 360.0f);
            _tickInterval = Mathf.Max(0.01f, tickInterval);
            _occlusionMask = occlusionMask;
            _eyeHeight = eyeHeight;
            _standTargetHeight = standTargetHeight;
            _crouchTargetHeight = crouchTargetHeight;
            _isInitialized = true;
        }

        /// <summary>
        /// Assigns the target transform and initial crouching state to track.
        /// </summary>
        public void SetTarget(Transform target, bool isCrouching = false)
        {
            _target = target;
            _isTargetCrouching = isCrouching;
        }

        /// <summary>
        /// Updates target crouching stance dynamically.
        /// </summary>
        public void SetTargetCrouching(bool isCrouching)
        {
            _isTargetCrouching = isCrouching;
        }

        /// <summary>
        /// Advances the sensor simulation by deltaTime.
        /// Evaluates sensing strictly at 5 Hz cadence (_tickInterval), eliminating per-frame raycast overhead.
        /// Zero heap allocations on hot-path.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _tickTimer += deltaTime;
            if (_tickTimer < _tickInterval)
            {
                return;
            }

            _tickTimer -= _tickInterval;
            if (_tickTimer > _tickInterval)
            {
                _tickTimer = 0.0f; // Prevent spiral of death on severe frame drop
            }

            if (_target == null)
            {
                if (_hasLOS)
                {
                    _hasLOS = false;
                    _inCone = false;
                    OnLOSLost?.Invoke(_lastKnownPosition);
                }
                return;
            }

            Vector3 eyePos = EyePosition;
            Vector3 forward = transform.forward;
            Vector3 targetPos = _target.position;

            bool currentLOS = EvaluateVisibility(
                eyePos,
                forward,
                targetPos,
                _isTargetCrouching,
                out _targetDistance,
                out _targetAngle,
                out _inCone,
                out _);

            if (!_hasLOS && currentLOS)
            {
                _hasLOS = true;
                _lastKnownPosition = targetPos;
                _timeSinceLastLOS = 0.0f;
                OnLOSGained?.Invoke(targetPos);
            }
            else if (_hasLOS && !currentLOS)
            {
                _hasLOS = false;
                _timeSinceLastLOS = 0.0f;
                OnLOSLost?.Invoke(_lastKnownPosition);
            }
            else if (_hasLOS)
            {
                _lastKnownPosition = targetPos;
                _timeSinceLastLOS = 0.0f;
            }
            else
            {
                _timeSinceLastLOS += _tickInterval;
            }
        }

        /// <summary>
        /// Pure non-allocating visibility evaluation method.
        /// Computes horizontal FOV, distance limit, and stance-adaptive linecast occlusion.
        /// </summary>
        public bool EvaluateVisibility(
            Vector3 eyePosition,
            Vector3 forward,
            Vector3 targetPosition,
            bool isCrouching,
            out float distance,
            out float angle,
            out bool inCone,
            out bool occluded)
        {
            // 1. Calculate target point based on stance height
            float targetHeightOffset = isCrouching ? _crouchTargetHeight : _standTargetHeight;
            Vector3 targetAimPoint = targetPosition + Vector3.up * targetHeightOffset;

            // 2. Straight-line 3D Euclidean distance check
            Vector3 toTarget = targetAimPoint - eyePosition;
            distance = toTarget.magnitude;

            if (distance > _visionRange || distance <= Mathf.Epsilon)
            {
                angle = 180.0f;
                inCone = false;
                occluded = false;
                return false;
            }

            // 3. Planar horizontal angle check (XZ plane projection)
            Vector3 forwardXZ = new Vector3(forward.x, 0.0f, forward.z);
            Vector3 toTargetXZ = new Vector3(toTarget.x, 0.0f, toTarget.z);

            if (forwardXZ.sqrMagnitude < Mathf.Epsilon || toTargetXZ.sqrMagnitude < Mathf.Epsilon)
            {
                angle = 0.0f;
            }
            else
            {
                angle = Vector3.Angle(forwardXZ.normalized, toTargetXZ.normalized);
            }

            float halfFOV = _visionFOV * 0.5f;
            if (angle > halfFOV)
            {
                inCone = false;
                occluded = false;
                return false;
            }

            inCone = true;

            // 4. Linecast Occlusion check (E20 World layer)
            if (_physicsQuery != null)
            {
                occluded = _physicsQuery.CheckOcclusionLine(eyePosition, targetAimPoint, _occlusionMask, out _);
            }
            else
            {
                occluded = UnityEngine.Physics.Linecast(
                    eyePosition,
                    targetAimPoint,
                    _occlusionMask,
                    QueryTriggerInteraction.Ignore);
            }

            return !occluded;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 eyePos = EyePosition;
            Gizmos.color = _hasLOS ? Color.red : (_inCone ? Color.yellow : Color.cyan);

            // Draw line to target if present
            if (_target != null)
            {
                float targetHeight = _isTargetCrouching ? _crouchTargetHeight : _standTargetHeight;
                Vector3 targetPoint = _target.position + Vector3.up * targetHeight;
                Gizmos.DrawLine(eyePos, targetPoint);
            }

            // Draw FOV boundaries
            Vector3 forward = transform.forward;
            float halfFOV = _visionFOV * 0.5f;
            Quaternion leftRot = Quaternion.Euler(0, -halfFOV, 0);
            Quaternion rightRot = Quaternion.Euler(0, halfFOV, 0);

            Vector3 leftDir = leftRot * forward;
            Vector3 rightDir = rightRot * forward;

            Gizmos.DrawRay(eyePos, leftDir * _visionRange);
            Gizmos.DrawRay(eyePos, rightDir * _visionRange);
            Gizmos.DrawWireSphere(eyePos, 0.15f);
        }
    }
}
