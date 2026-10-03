using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WhisperWard.AI.Navigation
{
    /// <summary>
    /// Lightweight patrol driver for guard NPCs in the CorePlayground arena.
    /// Drives endless waypoint-to-waypoint navigation across corridors with configurable dwell pauses.
    /// Governed by ADR-0007, GDD #12, AC-SCENE-14, and AC-SCENE-15.
    /// </summary>
    /// <example>
    /// <code>
    /// var driver = guardGo.AddComponent&lt;SimplePatrolDriver&gt;();
    /// driver.Configure(agent, waypoints, dwellDuration: 2.0f, speed: 2.30f);
    /// </code>
    /// </example>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SimplePatrolDriver : MonoBehaviour
    {
        public enum PatrolState
        {
            MovingToWaypoint,
            DwellingAtWaypoint
        }

        [Header("Patrol Route")]
        [Tooltip("Ordered waypoint transforms forming the continuous loop.")]
        [SerializeField] private List<Transform> _waypoints = new List<Transform>();

        [Header("Motion Tuning")]
        [Tooltip("Walking speed along patrol route in m/s (GDD #12 nominal: 2.30 m/s).")]
        [SerializeField] private float _patrolSpeed = 2.30f;

        [Tooltip("Angular turning speed in deg/s (GDD #12 nominal: 120 deg/s).")]
        [SerializeField] private float _angularSpeed = 120.0f;

        [Tooltip("Arrival distance threshold on XZ plane in meters.")]
        [SerializeField] private float _arrivalTolerance = 0.30f;

        [Tooltip("Time spent observing at each waypoint before advancing in seconds.")]
        [SerializeField] private float _dwellDuration = 2.0f;

        [Header("Debug")]
        [SerializeField] private bool _drawRouteGizmos = true;
        [SerializeField] private Color _routeColor = Color.yellow;
        [SerializeField] private Color _activeLegColor = Color.red;

        private NavMeshAgent _agent;
        private int _currentWaypointIndex;
        private PatrolState _currentState;
        private float _dwellTimer;
        private bool _isInitialized;

        /// <summary>
        /// Gets the current patrol navigation state.
        /// </summary>
        public PatrolState CurrentState => _currentState;

        /// <summary>
        /// Gets the index of the active destination waypoint in the route.
        /// </summary>
        public int CurrentWaypointIndex => _currentWaypointIndex;

        /// <summary>
        /// Gets the remaining dwell time at the current waypoint in seconds.
        /// </summary>
        public float DwellTimer => _dwellTimer;

        /// <summary>
        /// Gets or sets the patrol movement speed in m/s.
        /// </summary>
        public float PatrolSpeed
        {
            get => _patrolSpeed;
            set
            {
                _patrolSpeed = value;
                if (_agent != null) _agent.speed = _patrolSpeed;
            }
        }

        /// <summary>
        /// Gets the active destination transform.
        /// </summary>
        public Transform CurrentDestination =>
            (_waypoints != null && _waypoints.Count > 0 && _currentWaypointIndex < _waypoints.Count)
                ? _waypoints[_currentWaypointIndex]
                : null;

        private void Awake()
        {
            InitializeDriver();
        }

        /// <summary>
        /// Initializes the NavMeshAgent configuration and begins traversal toward the initial waypoint.
        /// </summary>
        /// <example>
        /// <code>
        /// driver.InitializeDriver();
        /// </code>
        /// </example>
        public void InitializeDriver()
        {
            if (_isInitialized) return;

            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_agent != null)
            {
                _agent.speed = _patrolSpeed;
                _agent.angularSpeed = _angularSpeed;
                _agent.stoppingDistance = _arrivalTolerance;
                _agent.autoBraking = true;
            }

            _currentWaypointIndex = 0;
            _currentState = PatrolState.MovingToWaypoint;
            _dwellTimer = 0f;

            if (CurrentDestination != null && _agent != null && _agent.isOnNavMesh)
            {
                _agent.SetDestination(CurrentDestination.position);
            }

            _isInitialized = true;
        }

        /// <summary>
        /// Explicitly configures driver dependencies and waypoints for programmatic setup and test injection.
        /// </summary>
        /// <param name="agent">NavMeshAgent component.</param>
        /// <param name="waypoints">List or array of waypoint transforms.</param>
        /// <param name="dwellDuration">Pause duration at each stop in seconds.</param>
        /// <param name="speed">Movement speed in m/s.</param>
        /// <example>
        /// <code>
        /// driver.Configure(agent, waypoints, 2.0f, 2.30f);
        /// </code>
        /// </example>
        public void Configure(NavMeshAgent agent, IList<Transform> waypoints, float dwellDuration = 2.0f, float speed = 2.30f)
        {
            _agent = agent;
            _waypoints = new List<Transform>(waypoints);
            _dwellDuration = dwellDuration;
            _patrolSpeed = speed;
            _isInitialized = false;
            InitializeDriver();
        }

        private void Update()
        {
            TickDriver(Time.deltaTime);
        }

        /// <summary>
        /// Evaluates arrival detection, dwell timer countdown, and waypoint sequence advancement.
        /// Zero managed allocations per frame (0 B GC).
        /// </summary>
        /// <param name="deltaTime">Elapsed frame time in seconds.</param>
        /// <example>
        /// <code>
        /// driver.TickDriver(Time.deltaTime);
        /// </code>
        /// </example>
        public void TickDriver(float deltaTime)
        {
            if (_waypoints == null || _waypoints.Count == 0) return;

            switch (_currentState)
            {
                case PatrolState.MovingToWaypoint:
                    if (CheckArrival())
                    {
                        _currentState = PatrolState.DwellingAtWaypoint;
                        _dwellTimer = _dwellDuration;
                    }
                    break;

                case PatrolState.DwellingAtWaypoint:
                    _dwellTimer -= deltaTime;
                    if (_dwellTimer <= 0f)
                    {
                        AdvanceToNextWaypoint();
                    }
                    break;
            }
        }

        /// <summary>
        /// Advances the active waypoint index in round-robin sequence and dispatches navigation.
        /// </summary>
        public void AdvanceToNextWaypoint()
        {
            if (_waypoints.Count == 0) return;

            _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Count;
            _currentState = PatrolState.MovingToWaypoint;

            if (_agent != null && _agent.isOnNavMesh && CurrentDestination != null)
            {
                _agent.SetDestination(CurrentDestination.position);
            }
        }

        private bool CheckArrival()
        {
            if (_agent == null) return false;

            if (!_agent.pathPending && _agent.remainingDistance <= _arrivalTolerance)
            {
                return true;
            }

            // Fallback manual XZ distance check if off mesh or in edit mode
            if (CurrentDestination != null)
            {
                Vector3 curPos = transform.position;
                Vector3 destPos = CurrentDestination.position;
                float dx = curPos.x - destPos.x;
                float dz = curPos.z - destPos.z;
                return (dx * dx + dz * dz) <= (_arrivalTolerance * _arrivalTolerance);
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            if (!_drawRouteGizmos || _waypoints == null || _waypoints.Count < 2) return;

            Gizmos.color = _routeColor;
            for (int i = 0; i < _waypoints.Count; i++)
            {
                Transform a = _waypoints[i];
                Transform b = _waypoints[(i + 1) % _waypoints.Count];
                if (a != null && b != null)
                {
                    Gizmos.DrawLine(a.position + Vector3.up * 0.2f, b.position + Vector3.up * 0.2f);
                    Gizmos.DrawWireSphere(a.position + Vector3.up * 0.2f, 0.20f);
                }
            }

            if (CurrentDestination != null)
            {
                Gizmos.color = _activeLegColor;
                Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, CurrentDestination.position + Vector3.up * 0.5f);
            }
        }
    }
}
