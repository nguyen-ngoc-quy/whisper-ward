using System;
using UnityEngine;

namespace WhisperWard.Foundation.Physics
{
    /// <summary>
    /// Runtime implementation of IPhysicsQueryService.
    /// Strictly guarantees:
    /// 1. Universal QueryTriggerInteraction.Ignore on all sensing queries (eliminating trigger occlusion anomalies).
    /// 2. Embedded origin detection (E10 fallback) returning HIT_OCCLUDED if ray originates inside geometry.
    /// 3. Zero heap allocations on query loops.
    /// 4. Rate-limited transform synchronization (<= 5.0 Hz).
    /// </summary>
    public sealed class PhysicsQueryService : IPhysicsQueryService
    {
        public const int QueryBufferCapacity = 64;
        public const float MinSyncInterval = 0.20f; // 5.0 Hz max frequency

        private readonly RaycastHit[] _internalHitBuffer = new RaycastHit[QueryBufferCapacity];
        private readonly Collider[] _internalColliderBuffer = new Collider[QueryBufferCapacity];

        private float _lastSyncTime = -1f;

        public int DefaultBufferCapacity => QueryBufferCapacity;

        public bool CheckOcclusionLine(Vector3 start, Vector3 end, int layerMask, out RaycastHit hit)
        {
            // E10: CheckSphere at origin to catch embedded origins inside solid colliders
            if (CheckOriginEmbedded(start, layerMask, PhysicsCollisionConfig.EmbeddedOriginCheckRadius))
            {
                hit = new RaycastHit();
                return true; // Fail-closed: origin is embedded inside solid geometry
            }

            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                hit = new RaycastHit();
                return false;
            }

            // Strictly ignore triggers
            return UnityEngine.Physics.Raycast(
                start,
                delta / distance,
                out hit,
                distance,
                layerMask,
                QueryTriggerInteraction.Ignore);
        }

        public int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDistance, int layerMask)
        {
            if (results == null || results.Length == 0) return 0;
            return UnityEngine.Physics.RaycastNonAlloc(ray, results, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
        }

        public int SphereCastNonAlloc(Ray ray, float radius, RaycastHit[] results, float maxDistance, int layerMask)
        {
            if (results == null || results.Length == 0) return 0;
            return UnityEngine.Physics.SphereCastNonAlloc(ray, radius, results, maxDistance, layerMask, QueryTriggerInteraction.Ignore);
        }

        public int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask)
        {
            if (results == null || results.Length == 0) return 0;
            return UnityEngine.Physics.OverlapSphereNonAlloc(position, radius, results, layerMask, QueryTriggerInteraction.Ignore);
        }

        public bool CheckOriginEmbedded(Vector3 origin, int layerMask, float testRadius = PhysicsCollisionConfig.EmbeddedOriginCheckRadius)
        {
            return UnityEngine.Physics.CheckSphere(origin, testRadius, layerMask, QueryTriggerInteraction.Ignore);
        }

        public bool TrySyncTransformsBatch(float currentTime)
        {
            if (_lastSyncTime >= 0f && (currentTime - _lastSyncTime) < MinSyncInterval)
            {
                return false; // Rate-limited
            }

            UnityEngine.Physics.SyncTransforms();
            _lastSyncTime = currentTime;
            return true;
        }
    }
}
