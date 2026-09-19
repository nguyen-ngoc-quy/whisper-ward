using UnityEngine;
using UnityEngine.AI;

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

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.stoppingDistance = stoppingDistance;
        }

        public void SetSpeed(float speed)
        {
            if (speed < 0f || float.IsNaN(speed) || float.IsInfinity(speed))
                return;
            _agent.speed = speed;
        }

        public void SetPatrolSpeed() { SetSpeed(patrolSpeed); }
        public void SetInvestigateSpeed() { SetSpeed(investigateSpeed); }
        public void SetChaseSpeed() { SetSpeed(chaseSpeed); }

        public void MoveTo(Vector3 destination)
        {
            _agent.SetDestination(destination);
        }

        public bool HasReachedDestination()
        {
            // Check if we are close to the target and not currently calculating a path
            return !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance;
        }

        public Vector3 GetCurrentPosition() => transform.position;

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
