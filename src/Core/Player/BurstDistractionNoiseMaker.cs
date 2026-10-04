using System;
using UnityEngine;
using WhisperWard.Foundation.Physics;

namespace WhisperWard.Core.Player
{
    /// <summary>
    /// Lifecycle state of a limited Burst distraction projectile item.
    /// </summary>
    public enum BurstItemState
    {
        Placed = 0,
        Carried = 1,
        InFlight = 2,
        Landed = 3,
        Consumed = 4
    }

    /// <summary>
    /// Burst Noise-Maker Distraction Tool.
    /// Implements ballistic flight, stationary throw gate, collision detection,
    /// and 10.0m acoustic impact pulse for guard distraction.
    /// Conforms to GDD #3 Player Noise (AC6..AC15b) and Story NOISE-02 (AC-NOISE-05..08).
    /// </summary>
    /// <example>
    /// <code>
    /// var noiseMaker = gameObject.AddComponent&lt;BurstDistractionNoiseMaker&gt;();
    /// noiseMaker.Configure(12.0f, 30.0f, 1.50f, 9.81f, 10.0f);
    /// noiseMaker.Pickup();
    /// bool thrown = noiseMaker.TryThrow(playerFeet, cameraForward, playerPlanarSpeed, out string reason);
    /// </code>
    /// </example>
    public sealed class BurstDistractionNoiseMaker : MonoBehaviour
    {
        [Header("Ballistic Parameters")]
        [Tooltip("Initial launch speed in meters per second (GDD: 12.0 m/s).")]
        [SerializeField] private float _initialSpeed = 12.00f;

        [Tooltip("Launch elevation angle in degrees (GDD: 30.0 deg).")]
        [SerializeField] private float _launchAngle = 30.00f;

        [Tooltip("Release height above thrower feet in meters (GDD: 1.50m).")]
        [SerializeField] private float _releaseHeight = 1.50f;

        [Tooltip("Gravity acceleration magnitude in m/s^2 (GDD: 9.81 m/s^2).")]
        [SerializeField] private float _gravity = 9.81f;

        [Header("Acoustic Pulse")]
        [Tooltip("Acoustic impact radius in meters emitted on collision (GDD: 10.0m).")]
        [SerializeField] private float _acousticRadius = 10.00f;

        [Tooltip("Maximum allowed thrower planar speed in m/s (GDD: < 1.0 m/s stationary constraint).")]
        [SerializeField] private float _movingSpeedThreshold = 1.00f;

        [Tooltip("Occlusion and collision LayerMask for solid geometry (Layer 20 World).")]
        [SerializeField] private LayerMask _collisionMask = 1 << 20;

        [Header("Runtime State")]
        [SerializeField] private BurstItemState _state = BurstItemState.Placed;

        private Vector3 _currentFlightPosition;
        private Vector3 _currentFlightVelocity;
        private float _flightTime;

        /// <summary>
        /// Global event broadcast when any burst projectile lands and emits acoustic impact.
        /// (impactPoint, acousticRadius)
        /// Zero heap allocations on hot-path.
        /// </summary>
        public static event Action<Vector3, float> OnAnyBurstNoiseEmitted;

        /// <summary>Instance event fired when this projectile impacts and lands.</summary>
        public event Action<Vector3, float> OnBurstLanded;

        /// <summary>Instance event fired when this projectile successfully launches into flight.</summary>
        public event Action OnBurstLaunched;

        /// <summary>Instance event fired when throw is rejected due to movement.</summary>
        public event Action OnThrowRejectedMoving;

        public BurstItemState State => _state;
        public float InitialSpeed => _initialSpeed;
        public float LaunchAngle => _launchAngle;
        public float ReleaseHeight => _releaseHeight;
        public float Gravity => _gravity;
        public float AcousticRadius => _acousticRadius;
        public float MovingSpeedThreshold => _movingSpeedThreshold;
        public LayerMask CollisionMask => _collisionMask;
        public Vector3 CurrentFlightPosition => _currentFlightPosition;
        public Vector3 CurrentFlightVelocity => _currentFlightVelocity;
        public float FlightTime => _flightTime;

        /// <summary>
        /// Configures ballistic and acoustic parameters.
        /// </summary>
        public void Configure(
            float initialSpeed = 12.00f,
            float launchAngle = 30.00f,
            float releaseHeight = 1.50f,
            float gravity = 9.81f,
            float acousticRadius = 10.00f,
            float movingSpeedThreshold = 1.00f,
            int collisionMask = 1 << 20)
        {
            _initialSpeed = Mathf.Max(0.1f, initialSpeed);
            _launchAngle = Mathf.Clamp(launchAngle, 1.0f, 89.0f);
            _releaseHeight = Mathf.Max(0.1f, releaseHeight);
            _gravity = Mathf.Max(0.1f, gravity);
            _acousticRadius = Mathf.Max(0.1f, acousticRadius);
            _movingSpeedThreshold = Mathf.Max(0.01f, movingSpeedThreshold);
            _collisionMask = collisionMask;
            _flightTime = 0f;
        }

        /// <summary>
        /// Picks up the distraction item into inventory.
        /// </summary>
        public bool Pickup()
        {
            if (_state != BurstItemState.Placed)
            {
                return false;
            }

            _state = BurstItemState.Carried;
            return true;
        }

        /// <summary>
        /// Sets the state to Carried directly (e.g. for spawning with item in hand).
        /// </summary>
        public void SetCarried()
        {
            _state = BurstItemState.Carried;
        }

        /// <summary>
        /// Attempts to throw the distraction item along camera azimuth.
        /// </summary>
        /// <param name="throwerFeetPosition">World position of thrower's feet.</param>
        /// <param name="throwDirection">Forward view/aim direction.</param>
        /// <param name="throwerPlanarSpeed">Current horizontal velocity magnitude of thrower.</param>
        /// <param name="rejectionReason">Output failure reason code.</param>
        /// <returns>True if throw launched; false if rejected.</returns>
        public bool TryThrow(
            Vector3 throwerFeetPosition,
            Vector3 throwDirection,
            float throwerPlanarSpeed,
            out string rejectionReason)
        {
            if (_state != BurstItemState.Carried)
            {
                rejectionReason = "ITEM_NOT_CARRIED";
                return false;
            }

            // AC-NOISE-06: Stationary launch constraint (< 1.0 m/s)
            if (throwerPlanarSpeed >= _movingSpeedThreshold)
            {
                rejectionReason = "THROW_WHILE_MOVING_REJECTED";
                OnThrowRejectedMoving?.Invoke();
                return false;
            }

            Vector3 planarDir = new Vector3(throwDirection.x, 0f, throwDirection.z);
            if (planarDir.sqrMagnitude < 1e-4f)
            {
                planarDir = Vector3.forward;
            }
            planarDir.Normalize();

            float angleRad = _launchAngle * Mathf.Deg2Rad;
            float horizontalSpeed = _initialSpeed * Mathf.Cos(angleRad);
            float verticalSpeed = _initialSpeed * Mathf.Sin(angleRad);

            _currentFlightVelocity = planarDir * horizontalSpeed + Vector3.up * verticalSpeed;
            _currentFlightPosition = throwerFeetPosition + Vector3.up * _releaseHeight;
            _flightTime = 0f;
            _state = BurstItemState.InFlight;

            rejectionReason = null;
            OnBurstLaunched?.Invoke();
            return true;
        }

        /// <summary>
        /// Advances the projectile flight simulation by deltaTime.
        /// Tests for collisions against geometry (Layer 20 World) and floor plane.
        /// Zero managed allocations.
        /// </summary>
        public void TickFlight(float deltaTime, IPhysicsQueryService physicsQuery = null)
        {
            if (_state != BurstItemState.InFlight)
            {
                return;
            }

            Vector3 startPos = _currentFlightPosition;
            Vector3 stepVelocity = _currentFlightVelocity + Vector3.down * (_gravity * deltaTime);
            Vector3 nextPos = startPos + _currentFlightVelocity * deltaTime + 0.5f * Vector3.down * (_gravity * deltaTime * deltaTime);

            _flightTime += deltaTime;
            _currentFlightVelocity = stepVelocity;

            // 1. Raycast / Linecast check against solid geometry (Layer 20 World)
            bool hitObstacle = false;
            Vector3 impactPoint = Vector3.zero;

            if (physicsQuery != null)
            {
                if (physicsQuery.CheckOcclusionLine(startPos, nextPos, _collisionMask, out RaycastHit hit))
                {
                    hitObstacle = true;
                    impactPoint = hit.point;
                }
            }
            else
            {
                if (UnityEngine.Physics.Linecast(startPos, nextPos, out RaycastHit hit, _collisionMask, QueryTriggerInteraction.Ignore))
                {
                    hitObstacle = true;
                    impactPoint = hit.point;
                }
            }

            // 2. Fallback ground plane collision (y <= 0)
            if (!hitObstacle && nextPos.y <= 0.001f)
            {
                hitObstacle = true;
                impactPoint = new Vector3(nextPos.x, 0f, nextPos.z);
            }

            if (hitObstacle)
            {
                CommitImpact(impactPoint);
            }
            else
            {
                _currentFlightPosition = nextPos;
            }
        }

        /// <summary>
        /// Handles terminal collision impact, emits acoustic pulse, and transitions to Consumed.
        /// </summary>
        public void CommitImpact(Vector3 impactPoint)
        {
            _currentFlightPosition = impactPoint;
            _currentFlightVelocity = Vector3.zero;
            _state = BurstItemState.Landed;

            // AC-NOISE-07: Emit acoustic pulse
            OnBurstLanded?.Invoke(impactPoint, _acousticRadius);
            OnAnyBurstNoiseEmitted?.Invoke(impactPoint, _acousticRadius);

            // Deplete single-carry inventory
            _state = BurstItemState.Consumed;
        }

        /// <summary>
        /// Pure analytical ballistic range solver for unequal launch/landing heights.
        /// Solves: y(t) = y0 + v_y0 * t - 0.5 * g * t^2 = targetY
        /// </summary>
        public static bool SolveAnalyticalLanding(
            Vector3 releasePos,
            Vector3 planarDirection,
            float initialSpeed,
            float launchAngleDeg,
            float gravity,
            float targetGroundY,
            out Vector3 landingPos,
            out float timeOfFlight)
        {
            landingPos = releasePos;
            timeOfFlight = 0f;

            if (gravity <= 0f || initialSpeed <= 0f)
            {
                return false;
            }

            float rad = launchAngleDeg * Mathf.Deg2Rad;
            float vx = initialSpeed * Mathf.Cos(rad);
            float vy = initialSpeed * Mathf.Sin(rad);

            float deltaY = releasePos.y - targetGroundY; // y0 - y_target

            // 0.5 * g * t^2 - vy * t - deltaY = 0
            // Quadratic equation: A * t^2 + B * t + C = 0
            float a = 0.5f * gravity;
            float b = -vy;
            float c = -deltaY;

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return false;
            }

            float sqrtDisc = Mathf.Sqrt(discriminant);
            float t1 = (-b + sqrtDisc) / (2f * a);
            float t2 = (-b - sqrtDisc) / (2f * a);

            timeOfFlight = Mathf.Max(t1, t2);
            if (timeOfFlight <= 0f)
            {
                return false;
            }

            Vector3 pDir = new Vector3(planarDirection.x, 0f, planarDirection.z).normalized;
            landingPos = new Vector3(
                releasePos.x + pDir.x * vx * timeOfFlight,
                targetGroundY,
                releasePos.z + pDir.z * vx * timeOfFlight);

            return true;
        }

        /// <summary>
        /// Immediately resets state to Placed.
        /// </summary>
        public void ResetState()
        {
            _state = BurstItemState.Placed;
            _flightTime = 0f;
            _currentFlightVelocity = Vector3.zero;
        }
    }
}
