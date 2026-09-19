using System;
using UnityEngine;

namespace WhisperWard.AI.Navigation
{
    /// <summary>
    /// Compatibility shell for older scene references. Catch authority belongs to
    /// the authoritative FSM state and its injected PhysicsQueryProfile; this
    /// component deliberately does not maintain a second geometry configuration.
    /// </summary>
    [Obsolete("Catch evaluation is owned by ChaseState/InvestigateState")]
    public class CatchEvaluator : MonoBehaviour
    {
        /// <summary>
        /// Rejects calls from legacy composition until the caller is migrated to the
        /// state-owned catch contract. It never performs an independent physics query.
        /// </summary>
        public bool EvaluateCatch(Vector3 playerPosition)
        {
            return false;
        }
    }
}
