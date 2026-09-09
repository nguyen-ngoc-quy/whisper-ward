using System;
using UnityEngine;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Immutable, session-cached E20 physics query profile. Every caller uses the
    /// same mask and ignores trigger colliders; layer names are never resolved in a
    /// per-frame query.
    /// </summary>
    public sealed class PhysicsQueryProfile
    {
        private PhysicsQueryProfile(int e20Mask)
        {
            E20Mask = e20Mask;
        }

        public int E20Mask { get; private set; }
        public QueryTriggerInteraction TriggerInteraction
        {
            get { return QueryTriggerInteraction.Ignore; }
        }

        /// <summary>
        /// Resolves exact configured layer names and fails closed on missing,
        /// forbidden, or empty masks.
        /// </summary>
        public static bool TryCreate(string[] requiredWorldLayers,
            string[] forbiddenLayers, out PhysicsQueryProfile profile, out string errorCode)
        {
            profile = null;
            errorCode = string.Empty;
            if (requiredWorldLayers == null || requiredWorldLayers.Length == 0)
            {
                errorCode = "E20_REQUIRED_LAYERS_EMPTY";
                return false;
            }

            int mask = 0;
            for (int i = 0; i < requiredWorldLayers.Length; i++)
            {
                string layerName = requiredWorldLayers[i];
                if (string.IsNullOrWhiteSpace(layerName))
                {
                    errorCode = "E20_REQUIRED_LAYER_INVALID";
                    return false;
                }
                int layer = LayerMask.NameToLayer(layerName);
                if (layer < 0)
                {
                    errorCode = "E20_REQUIRED_LAYER_MISSING:" + layerName;
                    return false;
                }
                mask |= 1 << layer;
            }

            if (mask == 0)
            {
                errorCode = "E20_MASK_ZERO";
                return false;
            }

            if (forbiddenLayers != null)
            {
                for (int i = 0; i < forbiddenLayers.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(forbiddenLayers[i]))
                        continue;
                    int layer = LayerMask.NameToLayer(forbiddenLayers[i]);
                    if (layer >= 0 && (mask & (1 << layer)) != 0)
                    {
                        errorCode = "E20_FORBIDDEN_LAYER:" + forbiddenLayers[i];
                        return false;
                    }
                }
            }

            profile = new PhysicsQueryProfile(mask);
            return true;
        }

        /// <summary>
        /// Runs a binary occlusion query using the cached E20 mask.
        /// </summary>
        public PhysicsProbeResult Linecast(Vector3 origin, Vector3 endpoint)
        {
            RaycastHit hit;
            bool blocked = Physics.Linecast(origin, endpoint, out hit, E20Mask, TriggerInteraction);
            return new PhysicsProbeResult(!blocked, blocked, blocked ? hit.collider : null);
        }

        /// <summary>
        /// Checks a launch sphere before any resource spend or flight identity is
        /// allocated.
        /// </summary>
        public bool HasInitialOverlap(Vector3 center, float radius)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
                return false;
            return Physics.CheckSphere(center, radius, E20Mask, TriggerInteraction);
        }

        /// <summary>
        /// Performs exactly one closest-hit SphereCast over the supplied center
        /// segment. A zero-length segment deliberately does not issue a cast.
        /// </summary>
        public bool TrySphereCast(Vector3 currentCenter, Vector3 nextCenter,
            float radius, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
                return false;
            Vector3 segment = nextCenter - currentCenter;
            float distance = segment.magnitude;
            if (distance <= 0f) return false;

            return Physics.SphereCast(currentCenter, radius, segment / distance,
                out hit, distance, E20Mask, TriggerInteraction);
        }
    }

    /// <summary>
    /// Observable result of a shared line query.
    /// </summary>
    public struct PhysicsProbeResult
    {
        public PhysicsProbeResult(bool clear, bool blocked, Collider collider)
        {
            Clear = clear;
            Blocked = blocked;
            Collider = collider;
        }

        public bool Clear { get; private set; }
        public bool Blocked { get; private set; }
        public Collider Collider { get; private set; }
    }

    /// <summary>
    /// Canonical surface-contact result for the Burst resolver.
    /// </summary>
    public struct ClosestContactResult
    {
        public bool HasContact { get; private set; }
        public Vector3 ColliderSurfaceContact { get; private set; }
        public Vector3 Normal { get; private set; }
        public float DistanceAlongSegment { get; private set; }

        public static ClosestContactResult FromHit(RaycastHit hit, float distance)
        {
            return new ClosestContactResult
            {
                HasContact = true,
                ColliderSurfaceContact = hit.point,
                Normal = hit.normal,
                DistanceAlongSegment = distance
            };
        }

        /// <summary>
        /// Creates a deterministic contact result for injected fixture queries.
        /// The surface point remains distinct from the published projectile center.
        /// </summary>
        public static ClosestContactResult FromSurfaceContact(Vector3 surfaceContact,
            Vector3 normal, float distanceAlongSegment)
        {
            return new ClosestContactResult
            {
                HasContact = true,
                ColliderSurfaceContact = surfaceContact,
                Normal = normal,
                DistanceAlongSegment = distanceAlongSegment
            };
        }
    }

    /// <summary>
    /// Injectable Burst physics seam. Fixture adapters can provide deterministic
    /// overlap and closest-hit results without invoking Unity Physics.
    /// </summary>
    public interface IBurstPhysicsQuery
    {
        /// <summary>Synchronizes authored transforms once before a step batch.</summary>
        void PrepareBatch();

        /// <summary>Checks initial overlap before a throw spend is committed.</summary>
        bool HasInitialOverlap(Vector3 center, float radius);

        /// <summary>Runs one closest-hit center-segment sweep.</summary>
        ClosestContactResult Sweep(Vector3 currentCenter, Vector3 nextCenter,
            float radius);
    }

    /// <summary>
    /// Shared pure boundary around the Unity query profile. Player Noise owns
    /// lifecycle/publication; this resolver owns neither inventory nor event output.
    /// </summary>
    public sealed class BurstCollisionResolver : IBurstPhysicsQuery, IDisposable
    {
        private static int _autoSyncPinCount;
        private static bool _autoSyncValueBeforePin;

        private readonly PhysicsQueryProfile _profile;
        private bool _disposed;

        /// <summary>
        /// Synchronizes physics transforms once before a simulation batch. The
        /// service lifetime owns the global auto-sync pin; this method never
        /// toggles it per query and never synchronizes per sweep.
        /// </summary>
        public void PrepareBatch()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BurstCollisionResolver));
            Physics.SyncTransforms();
        }

        public BurstCollisionResolver(PhysicsQueryProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (_autoSyncPinCount == 0)
                _autoSyncValueBeforePin = Physics.autoSyncTransforms;
            _autoSyncPinCount++;
            Physics.autoSyncTransforms = false;
        }

        public bool HasInitialOverlap(Vector3 center, float radius)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BurstCollisionResolver));
            return _profile.HasInitialOverlap(center, radius);
        }

        public ClosestContactResult Sweep(Vector3 currentCenter, Vector3 nextCenter,
            float radius)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BurstCollisionResolver));
            RaycastHit hit;
            if (!_profile.TrySphereCast(currentCenter, nextCenter, radius, out hit))
                return new ClosestContactResult();

            // RaycastHit.distance is the closest hit distance along the exact
            // center-segment ray; the collider surface point is not a center path
            // distance and must not be used for timeout/contact-fraction math.
            return ClosestContactResult.FromHit(hit, hit.distance);
        }

        /// <summary>
        /// Releases this resolver's lifetime auto-sync pin and restores the
        /// project setting captured before the first active Burst resolver.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _autoSyncPinCount = Mathf.Max(0, _autoSyncPinCount - 1);
            if (_autoSyncPinCount == 0)
                Physics.autoSyncTransforms = _autoSyncValueBeforePin;
        }

        public static Vector3 GetPublishedContact(ClosestContactResult contact,
            float projectileRadius, float epsilonContact)
        {
            if (!contact.HasContact) throw new InvalidOperationException("No contact");
            return contact.ColliderSurfaceContact
                + contact.Normal * (projectileRadius + epsilonContact);
        }
    }
}
