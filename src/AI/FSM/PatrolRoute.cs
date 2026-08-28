using System.Collections.Generic;
using UnityEngine;

namespace WhisperWard.AI.FSM
{
    /// <summary>
    /// Represents an authored patrol route for a guard.
    /// </summary>
    [System.Serializable]
    public class PatrolRoute
    {
        public List<Waypoint> waypoints = new List<Waypoint>();

        public Waypoint GetNextWaypoint(int currentIndex)
        {
            if (waypoints.Count == 0) return null;
            return waypoints[(currentIndex + 1) % waypoints.Count];
        }
    }

    [System.Serializable]
    public class Waypoint
    {
        public Transform targetPosition;
        [Range(0, 5f)] public float dwellTime = 1.0f;
        [Range(0, 180f)] public float scanArcSpan = 0f;
        public int scanCount = 0;
    }
}
