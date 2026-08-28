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
        private NavMeshAgent _agent;

        // Pinned speeds from design/registry/entities.yaml
        private const float V_PATROL = 2.3f;
        private const float V_INVESTIGATE = 5.0f;
        private const float V_CHASE = 7.50f;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.stoppingDistance = 0.1f; // Based on stopping_distance = 0 in registry
        }

        public void SetSpeed(float speed)
        {
            _agent.speed = speed;
        }

        public void SetPatrolSpeed() => SetSpeed(V_PATROL);
        public void SetInvestigateSpeed() => SetSpeed(V_INVESTIGATE);
        public void SetChaseSpeed() => SetSpeed(V_CHASE);

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
            return path.GetLength();
        }

        public Vector3 GetLastKnownPosition() => _agent.destination;
    }
}
