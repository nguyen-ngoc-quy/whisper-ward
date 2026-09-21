using UnityEngine;

namespace WhisperWard.Foundation.Physics
{
    /// <summary>
    /// Contract for non-allocating, trigger-immune spatial physics queries.
    /// Conforms to ADR-0002 and TR-FOUND-006 / TR-FOUND-007.
    /// </summary>
    public interface IPhysicsQueryService
    {
        int DefaultBufferCapacity { get; }

        bool CheckOcclusionLine(Vector3 start, Vector3 end, int layerMask, out RaycastHit hit);
        int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDistance, int layerMask);
        int SphereCastNonAlloc(Ray ray, float radius, RaycastHit[] results, float maxDistance, int layerMask);
        int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask);

        bool CheckOriginEmbedded(Vector3 origin, int layerMask, float testRadius = 0.05f);
        bool TrySyncTransformsBatch(float currentTime);
    }
}
