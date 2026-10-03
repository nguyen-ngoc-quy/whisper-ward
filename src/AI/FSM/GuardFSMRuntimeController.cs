using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.Perception;
using WhisperWard.AI.Navigation;

namespace WhisperWard.AI.FSM
{
    /// <summary>
    /// Runtime FSM Controller for Guard NPCs in 3D level environments.
    /// Manages high-level transitions between Patrol, Investigate, and Chase states,
    /// synchronizing NavMeshAgent speeds (2.3m/s -> 5.0m/s -> 7.5m/s),
    /// driving target pursuit and pathfinding, and evaluating the 1.0s / 5.50m player catch contract.
    /// Conforms to GDD Guard AI FSM, ADR-0007, and Story GUARD-04 (AC-GUARD-09..12).
    /// </summary>
    /// <example>
    /// <code>
    /// var controller = guardObject.AddComponent&lt;GuardFSMRuntimeController&gt;();
    /// controller.Configure(agent, sensor, accumulator, patrolDriver, playerTransform);
    /// controller.OnPlayerCaptured += pos =&gt; Debug.Log($"Caught player at {pos}!");
    /// </code>
    /// </example>
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuardFSMRuntimeController : MonoBehaviour
    {
        public enum GuardState
        {
            Patrol,
            Investigate,
            Chase,
            Captured
        }

        [Header("Movement Speeds (GDD Speed Coupling)")]
        [Tooltip("Patrol movement speed in m/s (GDD: 2.30 m/s).")]
        [SerializeField] private float _patrolSpeed = 2.30f;

        [Tooltip("Investigation movement speed in m/s (GDD: 5.00 m/s).")]
        [SerializeField] private float _investigateSpeed = 5.00f;

        [Tooltip("Chase pursuit speed in m/s (GDD: 7.50 m/s = 1.2x player sprint).")]
        [SerializeField] private float _chaseSpeed = 7.50f;

        [Header("Catch Contract Parameters")]
        [Tooltip("Maximum distance to player for catch qualification in meters (GDD: 5.50m).")]
        [SerializeField] private float _catchDistance = 5.50f;

        [Tooltip("Required continuous proximity duration in seconds for catch commit (GDD: 1.0s).")]
        [SerializeField] private float _catchDuration = 1.00f;

        [Header("Investigation Tuning")]
        [Tooltip("Arrival distance threshold for reaching investigation point in meters.")]
        [SerializeField] private float _arrivalTolerance = 0.50f;

        [Tooltip("Give-up dwell search duration at investigation target in seconds (GDD: 4.0s).")]
        [SerializeField] private float _giveupTimeout = 4.00f;

        [Tooltip("Horizontal look-around scan sweep angle in degrees during dwell search (GDD: 45 deg).")]
        [SerializeField] private float _scanArcAngle = 45.00f;

        [Header("Component References")]
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private VisionConeSensor _sensor;
        [SerializeField] private SuspicionAccumulator _accumulator;
        [SerializeField] private SimplePatrolDriver _patrolDriver;
        [SerializeField] private Transform _playerTransform;

        // Runtime FSM state
        private GuardState _currentState = GuardState.Patrol;
        private Vector3 _investigationTarget;
        private float _catchTimer;
        private float _giveupTimer;
        private bool _isDwellingAtLKP;
        private float _lkpBaseYaw;
        private bool _isInitialized;

        // Events
        /// <summary>Fires whenever guard transitions between FSM states.</summary>
        public event Action<GuardState, GuardState> OnStateChanged;

        /// <summary>Fires when guard begins investigation of a suspicious point.</summary>
        public event Action<Vector3, string> OnInvestigationStarted; // (targetPosition, cause)

        /// <summary>Fires when investigation times out and guard gives up, returning to patrol.</summary>
        public event Action<Vector3> OnInvestigateGivenUp;

        /// <summary>Fires when guard catches player during Chase.</summary>
        public event Action<Vector3> OnPlayerCaptured;

        public GuardState CurrentState => _currentState;
        public float PatrolSpeed => _patrolSpeed;
        public float InvestigateSpeed => _investigateSpeed;
        public float ChaseSpeed => _chaseSpeed;
        public float CatchDistance => _catchDistance;
        public float CatchDuration => _catchDuration;
        public float CatchTimer => _catchTimer;
        public float GiveupTimer => _giveupTimer;
        public float GiveupTimeout => _giveupTimeout;
        public float ScanArcAngle => _scanArcAngle;
        public bool IsDwellingAtLKP => _isDwellingAtLKP;
        public Vector3 InvestigationTarget => _investigationTarget;
        public Transform PlayerTransform => _playerTransform;

        private void Awake()
        {
            if (!_isInitialized)
            {
                InitializeComponents();
            }
        }

        private void Start()
        {
            // Bind sensor and accumulator events if present
            BindPerceptionEvents();
        }

        private void OnDestroy()
        {
            UnbindPerceptionEvents();
        }

        private void InitializeComponents()
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_sensor == null) _sensor = GetComponent<VisionConeSensor>();
            if (_accumulator == null) _accumulator = GetComponent<SuspicionAccumulator>();
            if (_patrolDriver == null) _patrolDriver = GetComponent<SimplePatrolDriver>();

            SetSpeedForState(_currentState);
            _isInitialized = true;
        }

        /// <summary>
        /// Explicit dependency injection and configuration for runtime and testing.
        /// Zero allocations.
        /// </summary>
        public void Configure(
            NavMeshAgent agent,
            VisionConeSensor sensor,
            SuspicionAccumulator accumulator,
            SimplePatrolDriver patrolDriver,
            Transform playerTransform,
            float patrolSpeed = 2.30f,
            float investigateSpeed = 5.00f,
            float chaseSpeed = 7.50f,
            float catchDistance = 5.50f,
            float catchDuration = 1.00f,
            float giveupTimeout = 4.00f)
        {
            UnbindPerceptionEvents();

            _agent = agent;
            _sensor = sensor;
            _accumulator = accumulator;
            _patrolDriver = patrolDriver;
            _playerTransform = playerTransform;

            _patrolSpeed = Mathf.Max(0.1f, patrolSpeed);
            _investigateSpeed = Mathf.Max(0.1f, investigateSpeed);
            _chaseSpeed = Mathf.Max(0.1f, chaseSpeed);
            _catchDistance = Mathf.Max(0.5f, catchDistance);
            _catchDuration = Mathf.Max(0.1f, catchDuration);
            _giveupTimeout = Mathf.Max(0.5f, giveupTimeout);

            _currentState = GuardState.Patrol;
            SetSpeedForState(_currentState);
            _isInitialized = true;

            BindPerceptionEvents();
        }

        private void BindPerceptionEvents()
        {
            if (_accumulator != null)
            {
                _accumulator.OnConfirmWindowElapsed += HandleConfirmWindowElapsed;
                _accumulator.OnChaseReached += HandleChaseReached;
                _accumulator.OnCapReached += HandleCapReached;
            }
        }

        private void UnbindPerceptionEvents()
        {
            if (_accumulator != null)
            {
                _accumulator.OnConfirmWindowElapsed -= HandleConfirmWindowElapsed;
                _accumulator.OnChaseReached -= HandleChaseReached;
                _accumulator.OnCapReached -= HandleCapReached;
            }
        }

        private void HandleConfirmWindowElapsed()
        {
            // Sight commitment -> Investigate last known position
            Vector3 target = _sensor != null ? _sensor.LastKnownPosition : (_playerTransform != null ? _playerTransform.position : transform.position);
            TriggerInvestigate(target, "sight-committed");
        }

        private void HandleChaseReached()
        {
            // Full suspicion reached -> Chase player
            Vector3 target = _playerTransform != null ? _playerTransform.position : transform.position;
            TriggerChase(target);
        }

        private void HandleCapReached()
        {
            // Cancel cap reached (3 peek evasions) -> forced Investigate
            Vector3 target = _sensor != null ? _sensor.LastKnownPosition : (_playerTransform != null ? _playerTransform.position : transform.position);
            TriggerInvestigate(target, "cap-forced");
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Advances the FSM runtime simulation by deltaTime.
        /// Zero managed GC allocations per frame.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_currentState == GuardState.Captured)
            {
                return;
            }

            switch (_currentState)
            {
                case GuardState.Patrol:
                    TickPatrol(deltaTime);
                    break;

                case GuardState.Investigate:
                    TickInvestigate(deltaTime);
                    break;

                case GuardState.Chase:
                    TickChase(deltaTime);
                    break;
            }
        }

        private void TickPatrol(float deltaTime)
        {
            // SimplePatrolDriver handles waypoint traversal
            if (_patrolDriver != null && !_patrolDriver.enabled)
            {
                _patrolDriver.enabled = true;
            }
        }

        private void TickInvestigate(float deltaTime)
        {
            if (_isDwellingAtLKP)
            {
                _giveupTimer += deltaTime;

                // Look-around scan sweep (+/- 45 deg sinusoidal oscillation around arrival heading)
                float scanPhase = Mathf.Sin((_giveupTimer / _giveupTimeout) * Mathf.PI * 2f);
                float yawOffset = scanPhase * _scanArcAngle;
                transform.rotation = Quaternion.Euler(0f, _lkpBaseYaw + yawOffset, 0f);

                if (_giveupTimer >= _giveupTimeout)
                {
                    // Give-up search completed without sighting player -> return to Patrol (GUARD-05)
                    if (_accumulator != null)
                    {
                        _accumulator.ApplyFruitlessInvestigationResidual();
                    }

                    Vector3 lkp = _investigationTarget;
                    OnInvestigateGivenUp?.Invoke(lkp);
                    TransitionTo(GuardState.Patrol);
                }
            }
            else
            {
                // Moving toward investigation target
                if (_agent != null && _agent.isOnNavMesh)
                {
                    if (!_agent.pathPending && _agent.remainingDistance <= _arrivalTolerance)
                    {
                        // Arrived at LKP -> initiate dwell search
                        _isDwellingAtLKP = true;
                        _giveupTimer = 0f;
                        _lkpBaseYaw = transform.eulerAngles.y;
                    }
                }
                else
                {
                    // Fallback Euclidean distance check
                    Vector3 curPos = transform.position;
                    float dx = curPos.x - _investigationTarget.x;
                    float dz = curPos.z - _investigationTarget.z;
                    if ((dx * dx + dz * dz) <= (_arrivalTolerance * _arrivalTolerance))
                    {
                        _isDwellingAtLKP = true;
                        _giveupTimer = 0f;
                        _lkpBaseYaw = transform.eulerAngles.y;
                    }
                }
            }
        }

        private void TickChase(float deltaTime)
        {
            if (_playerTransform == null) return;

            // Continually update destination to player position
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.SetDestination(_playerTransform.position);
            }

            // Evaluate Catch Condition (AC-GUARD-11): d <= 5.50m continuously for 1.0s
            Vector3 guardPos = transform.position;
            Vector3 playerPos = _playerTransform.position;
            float distSq = (guardPos - playerPos).sqrMagnitude;
            float catchDistSq = _catchDistance * _catchDistance;

            if (distSq <= catchDistSq)
            {
                _catchTimer += deltaTime;
                if (_catchTimer >= _catchDuration)
                {
                    // Player captured!
                    ExecuteCapture();
                }
            }
            else
            {
                // Reset catch timer if player escapes catch radius
                _catchTimer = 0f;
            }
        }

        private void ExecuteCapture()
        {
            GuardState previous = _currentState;
            _currentState = GuardState.Captured;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }

            Vector3 catchPos = _playerTransform != null ? _playerTransform.position : transform.position;
            OnPlayerCaptured?.Invoke(catchPos);
            OnStateChanged?.Invoke(previous, _currentState);
        }

        /// <summary>
        /// Initiates an investigation at the specified world coordinate.
        /// Switches state to Investigate and applies InvestigateSpeed (5.0 m/s).
        /// </summary>
        public void TriggerInvestigate(Vector3 targetPosition, string cause = "generic")
        {
            if (_currentState == GuardState.Chase || _currentState == GuardState.Captured)
            {
                return; // Chase takes precedence over Investigate (Chase-wins rule R13)
            }

            _investigationTarget = targetPosition;
            _isDwellingAtLKP = false;
            _giveupTimer = 0f;

            if (_patrolDriver != null)
            {
                _patrolDriver.enabled = false;
            }

            TransitionTo(GuardState.Investigate);

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(_investigationTarget);
            }

            OnInvestigationStarted?.Invoke(_investigationTarget, cause);
        }

        /// <summary>
        /// Initiates pursuit of the player.
        /// Switches state to Chase and applies ChaseSpeed (7.50 m/s).
        /// Freezes suspicion accumulator in Chase mode.
        /// </summary>
        public void TriggerChase(Vector3 targetPosition)
        {
            if (_currentState == GuardState.Captured)
            {
                return;
            }

            _catchTimer = 0f;
            _isDwellingAtLKP = false;

            if (_patrolDriver != null)
            {
                _patrolDriver.enabled = false;
            }

            if (_accumulator != null)
            {
                _accumulator.SetChaseMode(true);
            }

            TransitionTo(GuardState.Chase);

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(targetPosition);
            }
        }

        /// <summary>
        /// Transitions to a new FSM state, synchronously updating agent speed.
        /// </summary>
        public void TransitionTo(GuardState newState)
        {
            if (_currentState == newState) return;

            GuardState previous = _currentState;
            _currentState = newState;

            SetSpeedForState(_currentState);

            if (_currentState == GuardState.Patrol)
            {
                _catchTimer = 0f;
                _giveupTimer = 0f;
                _isDwellingAtLKP = false;

                if (_accumulator != null)
                {
                    _accumulator.SetChaseMode(false);
                }

                if (_patrolDriver != null)
                {
                    _patrolDriver.enabled = true;
                    _patrolDriver.AdvanceToNextWaypoint();
                }
            }

            OnStateChanged?.Invoke(previous, _currentState);
        }

        private void SetSpeedForState(GuardState state)
        {
            if (_agent == null) return;

            switch (state)
            {
                case GuardState.Patrol:
                    _agent.speed = _patrolSpeed;
                    break;
                case GuardState.Investigate:
                    _agent.speed = _investigateSpeed;
                    break;
                case GuardState.Chase:
                    _agent.speed = _chaseSpeed;
                    break;
                case GuardState.Captured:
                    _agent.speed = 0f;
                    break;
            }
        }
    }
}
