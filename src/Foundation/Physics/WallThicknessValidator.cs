using System;
using UnityEngine;

namespace WhisperWard.Foundation.Physics
{
    /// <summary>
    /// Audits solid occlusion geometry against the minimum wall thickness invariant (>= 0.10 m)
    /// to prevent tunneling and penetration anomalies. Conforms to ADR-0002 and TR-FOUND-007.
    /// </summary>
    public static class WallThicknessValidator
    {
        public const float MinimumThickness = 0.10f;

        /// <summary>
        /// Validates whether a collider adheres to the minimum wall thickness constraint across all dimensions.
        /// </summary>
        public static bool ValidateCollider(Collider collider, out string violationReason, float minThickness = MinimumThickness)
        {
            if (collider == null)
            {
                violationReason = "Collider is null";
                return false;
            }

            if (collider.isTrigger)
            {
                // Trigger volumes are exempt from solid wall thickness checks
                violationReason = string.Empty;
                return true;
            }

            Vector3 lossyScale = collider.transform.lossyScale;
            Vector3 absScale = new Vector3(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));

            if (collider is BoxCollider box)
            {
                Vector3 worldSize = Vector3.Scale(box.size, absScale);
                if (worldSize.x < minThickness)
                {
                    violationReason = $"BoxCollider X dimension ({worldSize.x:F3}m) is below minimum thickness ({minThickness:F3}m)";
                    return false;
                }
                if (worldSize.y < minThickness)
                {
                    violationReason = $"BoxCollider Y dimension ({worldSize.y:F3}m) is below minimum thickness ({minThickness:F3}m)";
                    return false;
                }
                if (worldSize.z < minThickness)
                {
                    violationReason = $"BoxCollider Z dimension ({worldSize.z:F3}m) is below minimum thickness ({minThickness:F3}m)";
                    return false;
                }
            }
            else if (collider is SphereCollider sphere)
            {
                float maxScale = Mathf.Max(absScale.x, Mathf.Max(absScale.y, absScale.z));
                float diameter = sphere.radius * 2f * maxScale;
                if (diameter < minThickness)
                {
                    violationReason = $"SphereCollider diameter ({diameter:F3}m) is below minimum thickness ({minThickness:F3}m)";
                    return false;
                }
            }
            else if (collider is CapsuleCollider capsule)
            {
                float maxRadialScale;
                float heightScale;

                switch (capsule.direction)
                {
                    case 0: // X-axis
                        maxRadialScale = Mathf.Max(absScale.y, absScale.z);
                        heightScale = absScale.x;
                        break;
                    case 1: // Y-axis
                        maxRadialScale = Mathf.Max(absScale.x, absScale.z);
                        heightScale = absScale.y;
                        break;
                    default: // Z-axis
                        maxRadialScale = Mathf.Max(absScale.x, absScale.y);
                        heightScale = absScale.z;
                        break;
                }

                float diameter = capsule.radius * 2f * maxRadialScale;
                float height = capsule.height * heightScale;

                if (diameter < minThickness)
                {
                    violationReason = $"CapsuleCollider diameter ({diameter:F3}m) is below minimum thickness ({minThickness:F3}m)";
                    return false;
                }
                if (height < minThickness)
                {
                    violationReason = $"CapsuleCollider height ({height:F3}m) is below minimum thickness ({minThickness:F3}m)";
                    return false;
                }
            }
            else
            {
                // Fallback to bounding box extents for MeshCollider / others
                Bounds bounds = collider.bounds;
                Vector3 size = bounds.size;
                if (size.x < minThickness || size.y < minThickness || size.z < minThickness)
                {
                    violationReason = $"Collider bounds size ({size.x:F3}m, {size.y:F3}m, {size.z:F3}m) has dimension below minimum thickness ({minThickness:F3}m)";
                    return false;
                }
            }

            violationReason = string.Empty;
            return true;
        }
    }
}
