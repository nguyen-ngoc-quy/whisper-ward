using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.Core;

namespace WhisperWard.AI.Navigation
{
    /// <summary>
    /// Wrapper for Unity's NavMeshAgent to enforce design-specified speeds
    /// and provide a clean interface for the Guard AI FSM.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuardNavigator : MonoBehaviour
    {
        [Header("Navigation Configuration")]
        [SerializeField] private float patrolSpeed = 2.3f;
        [SerializeField] private float investigateSpeed = 5.0f;
        [SerializeField] private float chaseSpeed = 7.50f;
        [SerializeField] private float stoppingDistance;

        private NavMeshAgent _agent;
        private NoiseRuntimeConfiguration _runtimeConfiguration;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.stoppingDistance = NoiseRuntimeConfiguration
                .RegisteredStoppingDistanceMeters;
        }

        /// <summary>
        /// Injects the immutable registry-backed navigation tuning. Serialized
        /// fields remain authoring-only compatibility values and are not runtime
        /// authority.
        /// </summary>
        public void ConfigureRuntime(NoiseRuntimeConfiguration configuration)
        {
            _runtimeConfiguration = configuration
                ?? throw new System.ArgumentNullException(nameof(configuration));
            if (_agent != null)
                _agent.stoppingDistance = _runtimeConfiguration.StoppingDistanceMeters;
        }

        private float PatrolSpeed
        {
            get { return _runtimeConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredPatrolSpeedMetersPerSecond
                : _runtimeConfiguration.PatrolSpeedMetersPerSecond; }
        }

        private float InvestigateSpeed
        {
            get { return _runtimeConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredInvestigateSpeedMetersPerSecond
                : _runtimeConfiguration.InvestigateSpeedMetersPerSecond; }
        }

        private float ChaseSpeed
        {
            get { return _runtimeConfiguration == null
                ? NoiseRuntimeConfiguration.RegisteredChaseSpeedMetersPerSecond
                : _runtimeConfiguration.ChaseSpeedMetersPerSecond; }
        }

        public void SetSpeed(float speed)
        {
            if (speed < 0f || float.IsNaN(speed) || float.IsInfinity(speed))
                return;
            _agent.speed = speed;
        }

        public void SetPatrolSpeed() { SetSpeed(PatrolSpeed); }
        public void SetInvestigateSpeed() { SetSpeed(InvestigateSpeed); }
        public void SetChaseSpeed() { SetSpeed(ChaseSpeed); }

        public void MoveTo(Vector3 destination)
        {
            _agent.SetDestination(destination);
        }

        public bool HasReachedDestination()
        {
            return !_agent.pathPending
                && _agent.remainingDistance <= _agent.stoppingDistance;
        }

        /// <summary>
        /// Observes the Investigate arrival predicate. Arrival is based on the
        /// sampled target's XZ distance and is evaluated before path-end so a tie
        /// can never be classified as path-end.
        /// </summary>
        public bool HasReachedArrival(Vector3 target, float toleranceMeters)
        {
            if (_agent == null || _agent.pathPending
                || !IsFinite(target) || !IsFinite(toleranceMeters)
                || toleranceMeters < 0f)
                return false;
            Vector3 delta = target - transform.position;
            float xzDistance = new Vector2(delta.x, delta.z).magnitude;
            return IsFinite(xzDistance) && xzDistance <= toleranceMeters;
        }

        /// <summary>
        /// Observes one path-end candidate. The caller supplies the registered
        /// tolerances and consecutive-tick policy; this adapter only reports the
        /// current agent observation and never starts an FSM timer.
        /// </summary>
        public bool HasPathEndObservation(float remainingToleranceMeters,
            float speedToleranceMetersPerSecond)
        {
            if (_agent == null || _agent.pathPending
                || !IsFinite(remainingToleranceMeters)
                || !IsFinite(speedToleranceMetersPerSecond)
                || remainingToleranceMeters < 0f
                || speedToleranceMetersPerSecond < 0f)
                return false;
            return _agent.remainingDistance <= _agent.stoppingDistance
                + remainingToleranceMeters
                && _agent.velocity.magnitude <= speedToleranceMetersPerSecond;
        }

        public Vector3 GetCurrentPosition() => transform.position;

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public NavMeshPath CalculatePathTo(Vector3 destination)
        {
            NavMeshPath path = new NavMeshPath();
            _agent.CalculatePath(destination, path);
            return path;
        }

        public float GetPathDistance(NavMeshPath path)
        {
            if (path == null || path.corners == null) return 0f;
            float length = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++)
                length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            return length;
        }

        public Vector3 GetLastKnownPosition() => _agent.destination;
    }
}
