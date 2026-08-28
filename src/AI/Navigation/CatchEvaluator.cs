using UnityEngine;
using UnityEngine.AI;
using WhisperWard.AI.FSM;

namespace WhisperWard.AI.Navigation
{
    /// <summary>
    /// Handles the complex "Catch Contract" logic for the Guard AI.
    /// Evaluates path-arrival metrics and physical backstops.
    /// </summary>
    public class CatchEvaluator : MonoBehaviour
    {
        [Header("Catch Configuration")]
        [SerializeField] private float catchRange = 5.5f;
        [SerializeField] private float deltaYTolerance = 1.0f;
        [SerializeField] private float hysteresis = 0.5f;
        [SerializeField] private LayerMask worldLayerMask;

        private NavMeshAgent _agent;
        private GuardFSM _fsm;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _fsm = GetComponent<GuardFSM>();
        }

        /// <summary>
        /// Executes the full Catch contract check.
        /// Returns true if the guard can capture the player now.
        /// </summary>
        public bool EvaluateCatch(Vector3 playerPosition)
        {
            // 1. Nav-goal gate
            if (_fsm.currentGoalMode != GuardGoalMode.LivePursuit &&
                _fsm.currentGoalMode != GuardGoalMode.HideSpotFront)
            {
                return false;
            }

            // 2. Path-arrival metric
            if (!EvaluatePathArrival(playerPosition))
            {
                return false;
            }

            // 3. Backstop (XZ distance and DeltaY)
            if (!EvaluateBackstop(playerPosition))
            {
                return false;
            }

            return true;
        }

        private bool EvaluatePathArrival(Vector3 playerPosition)
        {
            NavMeshPath path = new NavMeshPath();
            _agent.CalculatePath(playerPosition, path);

            if (path.status == NavMeshPathStatus.PathInvalid)
            {
                return false;
            }

            float pathLength = GetPathLength(path);

            // Check against catch_range + hysteresis
            if (pathLength > catchRange + hysteresis)
            {
                // For partial paths, check if the closest point is geometry-clear
                if (path.status == NavMeshPathStatus.PathPartial)
                {
                    return EvaluatePartialPathClearance(path, playerPosition);
                }
                return false;
            }

            return true;
        }

        private bool EvaluatePartialPathClearance(NavMeshPath path, Vector3 playerPosition)
        {
            // Get the last corner (closest reachable point)
            Vector3 lastCorner = path.corners[path.corners.Length - 1];

            // Physics.Linecast check (Capsule offsets simplified to transform height/2)
            float height = _agent.height;
            Vector3 origin = lastCorner + Vector3.up * (height / 2f);
            Vector3 target = playerPosition + Vector3.up * (height / 2f);

            // Check for obstructions
            if (Physics.Linecast(origin, target, worldLayerMask, QueryTriggerInteraction.Ignore))
            {
                return false; // Obstructed partial path never passes
            }

            return true;
        }

        private bool EvaluateBackstop(Vector3 playerPosition)
        {
            Vector3 guardPos = transform.position;
            Vector3 delta = playerPosition - guardPos;

            // XZ plane distance
            float xzDist = new Vector2(delta.x, delta.z).magnitude;
            if (xzDist > catchRange + hysteresis)
            {
                return false;
            }

            // DeltaY tolerance
            if (Mathf.Abs(delta.y) > deltaYTolerance)
            {
                return false;
            }

            return true;
        }

        private float GetPathLength(NavMeshPath path)
        {
            float length = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            }
            return length;
        }
    }
}
