using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Immutable, session-cached E20 physics query profile. Every caller uses the
    /// same mask and ignores trigger colliders; layer names are never resolved in a
    /// per-frame query.
    /// </summary>
    public sealed class PhysicsQueryProfile
    {
        private readonly PhysicsScene _physicsScene;
        private readonly Collider[] _overlapBuffer;

        private PhysicsQueryProfile(int e20Mask, PhysicsScene physicsScene,
            int overlapBufferCapacity)
        {
            E20Mask = e20Mask;
            _physicsScene = physicsScene;
            OverlapBufferCapacity = overlapBufferCapacity;
            _overlapBuffer = new Collider[overlapBufferCapacity];
        }

        /// <summary>Physics scene owned by this immutable query profile.</summary>
        public PhysicsScene PhysicsScene { get { return _physicsScene; } }

        public int E20Mask { get; private set; }

        /// <summary>
        /// Registered bounded result capacity for the initial-overlap probe.
        /// Saturation (>= this count) fails closed as Incomplete.
        /// </summary>
        public int OverlapBufferCapacity { get; private set; }

        public QueryTriggerInteraction TriggerInteraction
        {
            get { return QueryTriggerInteraction.Ignore; }
        }

        /// <summary>
        /// Validates the captured scene-content proof against this immutable
        /// E20 profile before any authoritative query is enabled.
        /// </summary>
        public bool Matches(PhysicsSceneContentProof proof)
        {
            return proof.IsValid
                && proof.PhysicsScene.Equals(_physicsScene)
                && proof.E20Mask == E20Mask
                && proof.TriggerInteraction == TriggerInteraction;
        }

        /// <summary>
        /// Resolves exact configured layer names and fails closed on missing,
        /// forbidden, or empty masks.
        /// </summary>
        [Obsolete("An authoritative PhysicsScene must be injected.")]
        public static bool TryCreate(string[] requiredWorldLayers,
            string[] forbiddenLayers, out PhysicsQueryProfile profile, out string errorCode)
        {
            profile = null;
            errorCode = "E20_PHYSICS_SCENE_REQUIRED";
            return false;
        }

        /// <summary>
        /// Creates a profile bound to one explicit PhysicsScene and the
        /// registered initial-overlap result capacity.
        /// </summary>
        public static bool TryCreate(string[] requiredWorldLayers,
            string[] forbiddenLayers, PhysicsScene physicsScene,
            out PhysicsQueryProfile profile, out string errorCode)
        {
            return TryCreate(requiredWorldLayers, forbiddenLayers, physicsScene,
                NoiseRuntimeConfiguration.RegisteredInitialOverlapBufferCapacity,
                out profile, out errorCode);
        }

        /// <summary>
        /// Creates a profile with an explicit overlap result capacity. The
        /// capacity must lie within the registered safe range and is fixed
        /// for the profile lifetime; per-query or per-scene resizing is
        /// forbidden by the registry contract.
        /// </summary>
        public static bool TryCreate(string[] requiredWorldLayers,
            string[] forbiddenLayers, PhysicsScene physicsScene,
            int overlapBufferCapacity,
            out PhysicsQueryProfile profile, out string errorCode)
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
                    if (layer < 0)
                    {
                        errorCode = "E20_FORBIDDEN_LAYER_MISSING:"
                            + forbiddenLayers[i];
                        return false;
                    }
                    if ((mask & (1 << layer)) != 0)
                    {
                        errorCode = "E20_FORBIDDEN_LAYER:" + forbiddenLayers[i];
                        return false;
                    }
                }
            }

            if (!physicsScene.IsValid())
            {
                errorCode = "E20_PHYSICS_SCENE_INVALID";
                return false;
            }

            if (overlapBufferCapacity
                    < NoiseRuntimeConfiguration
                        .RegisteredMinInitialOverlapBufferCapacity
                || overlapBufferCapacity
                    > NoiseRuntimeConfiguration
                        .RegisteredMaxInitialOverlapBufferCapacity)
            {
                errorCode = "E20_OVERLAP_BUFFER_CAPACITY_INVALID";
                return false;
            }

            profile = new PhysicsQueryProfile(mask, physicsScene,
                overlapBufferCapacity);
            return true;
        }

        /// <summary>
        /// Runs a binary occlusion query using the cached E20 mask.
        /// </summary>
        public PhysicsProbeResult Linecast(Vector3 origin, Vector3 endpoint)
        {
            if (!IsFinite(origin) || !IsFinite(endpoint))
                return PhysicsProbeResult.IncompleteResult;
            RaycastHit hit = default(RaycastHit);
            Vector3 delta = endpoint - origin;
            float distance = delta.magnitude;
            if (!IsFinite(distance))
                return PhysicsProbeResult.IncompleteResult;
            bool blocked = distance > 0f && _physicsScene.Raycast(origin,
                delta / distance, out hit, distance, E20Mask, TriggerInteraction);
            return new PhysicsProbeResult(!blocked, blocked, blocked ? hit.collider : null);
        }

        /// <summary>Runs one bounded overlap and distinguishes saturation.</summary>
        public BurstOverlapResult GetInitialOverlap(Vector3 center, float radius)
        {
            if (!IsFinite(center) || !IsFinitePositive(radius))
                return BurstOverlapResult.Incomplete;
            int count = _physicsScene.OverlapSphere(center, radius,
                _overlapBuffer, E20Mask, TriggerInteraction);
            if (count < 0 || count >= _overlapBuffer.Length)
                return BurstOverlapResult.Incomplete;
            return count == 0 ? BurstOverlapResult.Clear : BurstOverlapResult.Blocked;
        }

        /// <summary>
        /// Evaluates the shared pickup predicate: inclusive three-dimensional
        /// reach followed by one clear E20 linecast to the pickup anchor.
        /// </summary>
        public PickupReachResult EvaluatePickupReach(Vector3 playerFeet,
            Vector3 pickupAnchor, float reachRadius)
        {
            return EvaluatePickupReach(playerFeet, pickupAnchor, reachRadius,
                NoiseRuntimeConfiguration.RegisteredF12NoiseOriginOffset);
        }

        /// <summary>
        /// Evaluates pickup reach using the same raised feet origin as hearing.
        /// The sphere remains feet-to-anchor, while only the E20 linecast uses the
        /// authored noise-origin offset.
        /// </summary>
        public PickupReachResult EvaluatePickupReach(Vector3 playerFeet,
            Vector3 pickupAnchor, float reachRadius, float originOffset)
        {
            if (!IsFinite(playerFeet) || !IsFinite(pickupAnchor)
                || !IsFinitePositive(reachRadius) || !IsFiniteNonNegative(originOffset))
                return PickupReachResult.Incomplete;
            float distance = (pickupAnchor - playerFeet).magnitude;
            if (!IsFinite(distance)) return PickupReachResult.Incomplete;
            if (distance > reachRadius) return PickupReachResult.OutOfReach;
            PhysicsProbeResult visibility = Linecast(
                playerFeet + Vector3.up * originOffset, pickupAnchor);
            if (visibility.Clear) return PickupReachResult.Reachable;
            if (visibility.Blocked) return PickupReachResult.Blocked;
            return PickupReachResult.Incomplete;
        }

        /// <summary>
        /// Performs exactly one closest-hit SphereCast over the supplied center
        /// segment. A zero-length segment deliberately does not issue a cast.
        /// </summary>
        public bool TrySphereCast(Vector3 currentCenter, Vector3 nextCenter,
            float radius, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            if (!IsFinite(currentCenter) || !IsFinite(nextCenter)
                || !IsFinitePositive(radius))
                return false;
            Vector3 segment = nextCenter - currentCenter;
            float distance = segment.magnitude;
            if (!IsFinite(distance) || distance <= 0f) return false;

            return _physicsScene.SphereCast(currentCenter, radius,
                segment / distance, out hit, distance, E20Mask, TriggerInteraction);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }
    }

    /// <summary>Provenance of a PhysicsScene content proof.</summary>
    public enum PhysicsProofEvidenceProvenance
    {
        Unavailable,
        Declared,
        SyntheticTest,
        CapturedRuntime
    }

    /// <summary>
    /// Immutable composition-root proof for one authoritative E20 PhysicsScene.
    /// The proof is captured by the scene owner, not inferred by a query caller.
    /// </summary>
    public struct PhysicsSceneContentProof
    {
        /// <summary>
        /// Compatibility constructor for declaration-only and fixture callers.
        /// It is deliberately not runtime-captured evidence.
        /// </summary>
        public PhysicsSceneContentProof(PhysicsScene physicsScene, int e20Mask,
            int e20WorldColliderCount, QueryTriggerInteraction triggerInteraction,
            string diagnosticCode)
            : this(physicsScene, e20Mask, e20WorldColliderCount,
                triggerInteraction, diagnosticCode,
                PhysicsProofEvidenceProvenance.Declared)
        {
        }

        /// <summary>Creates a proof with explicit evidence provenance.</summary>
        public PhysicsSceneContentProof(PhysicsScene physicsScene, int e20Mask,
            int e20WorldColliderCount, QueryTriggerInteraction triggerInteraction,
            string diagnosticCode, PhysicsProofEvidenceProvenance provenance)
        {
            PhysicsScene = physicsScene;
            E20Mask = e20Mask;
            E20WorldColliderCount = e20WorldColliderCount;
            TriggerInteraction = triggerInteraction;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            EvidenceProvenance = provenance;
        }

        /// <summary>Scene identity captured by the authoring/session owner.</summary>
        public PhysicsScene PhysicsScene { get; private set; }

        /// <summary>Resolved E20 world-only mask used by every query.</summary>
        public int E20Mask { get; private set; }

        /// <summary>Proved count of non-trigger E20 world colliders.</summary>
        public int E20WorldColliderCount { get; private set; }

        /// <summary>Trigger policy captured with the proof.</summary>
        public QueryTriggerInteraction TriggerInteraction { get; private set; }

        /// <summary>Stable capture diagnostic retained for replay/QA.</summary>
        public string DiagnosticCode { get; private set; }

        /// <summary>Source state of the scene-content evidence.</summary>
        public PhysicsProofEvidenceProvenance EvidenceProvenance { get; private set; }

        /// <summary>
        /// True only for structurally valid evidence captured by the runtime scene
        /// owner. Declaration-only and synthetic-test proofs remain non-runtime.
        /// </summary>
        public bool IsCapturedRuntime
        {
            get { return IsValid
                && EvidenceProvenance == PhysicsProofEvidenceProvenance.CapturedRuntime; }
        }

        /// <summary>
        /// True only when the proof contains a valid scene, nonzero E20 mask and
        /// world content, the canonical trigger policy, and a stable diagnostic.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return PhysicsScene.IsValid()
                    && E20Mask != 0
                    && E20WorldColliderCount > 0
                    && TriggerInteraction == QueryTriggerInteraction.Ignore
                    && !string.IsNullOrWhiteSpace(DiagnosticCode);
            }
        }

        public static PhysicsSceneContentProof Invalid
        {
            get
            {
                return new PhysicsSceneContentProof(default(PhysicsScene), 0, 0,
                    QueryTriggerInteraction.Ignore, "E20_PHYSICS_SCENE_PROOF_INVALID");
            }
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
            Incomplete = !clear && !blocked;
            Collider = collider;
        }

        public bool Clear { get; private set; }
        public bool Blocked { get; private set; }
        public bool Incomplete { get; private set; }
        public Collider Collider { get; private set; }

        public static PhysicsProbeResult IncompleteResult
        {
            get { return new PhysicsProbeResult(false, false, null); }
        }
    }

    /// <summary>Result of the shared pickup reach predicate.</summary>
    public enum PickupReachStatus
    {
        Reachable,
        OutOfReach,
        Blocked,
        Incomplete
    }

    /// <summary>Fail-closed pickup reach result.</summary>
    public struct PickupReachResult
    {
        public PickupReachResult(PickupReachStatus status)
        {
            Status = status;
        }

        public PickupReachStatus Status { get; }
        public static PickupReachResult Reachable
        {
            get { return new PickupReachResult(PickupReachStatus.Reachable); }
        }
        public static PickupReachResult OutOfReach
        {
            get { return new PickupReachResult(PickupReachStatus.OutOfReach); }
        }
        public static PickupReachResult Blocked
        {
            get { return new PickupReachResult(PickupReachStatus.Blocked); }
        }
        public static PickupReachResult Incomplete
        {
            get { return new PickupReachResult(PickupReachStatus.Incomplete); }
        }
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

        /// <summary>
        /// Returns false for malformed injected contact data. Contact normals and
        /// distances are authoritative solver inputs and must fail closed.
        /// </summary>
        public bool IsFiniteValid()
        {
            if (!HasContact)
                return true;

            if (!IsFinite(ColliderSurfaceContact)
                || !IsFinite(Normal)
                || !IsFinite(DistanceAlongSegment)
                || DistanceAlongSegment < 0f)
                return false;

            float normalSqrMagnitude = Normal.sqrMagnitude;
            return IsFinite(normalSqrMagnitude)
                && normalSqrMagnitude > 0f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y)
                && !float.IsNaN(value.z)
                && !float.IsInfinity(value.x) && !float.IsInfinity(value.y)
                && !float.IsInfinity(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

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

    /// <summary>Typed result of the bounded initial-overlap query.</summary>
    public enum BurstOverlapStatus
    {
        Clear,
        Blocked,
        Incomplete
    }

    /// <summary>Fail-closed initial-overlap result.</summary>
    public struct BurstOverlapResult
    {
        public BurstOverlapResult(BurstOverlapStatus status)
        {
            Status = status;
        }

        public BurstOverlapStatus Status { get; }
        public static BurstOverlapResult Clear
        {
            get { return new BurstOverlapResult(BurstOverlapStatus.Clear); }
        }
        public static BurstOverlapResult Blocked
        {
            get { return new BurstOverlapResult(BurstOverlapStatus.Blocked); }
        }
        public static BurstOverlapResult Incomplete
        {
            get { return new BurstOverlapResult(BurstOverlapStatus.Incomplete); }
        }
    }

    /// <summary>Optional pickup seam carrying the authored raised origin.</summary>
    public interface IBurstPickupQueryWithOrigin
    {
        PickupReachResult EvaluatePickupReach(Vector3 playerFeet,
            Vector3 pickupAnchor, float reachRadius, float originOffset);
    }

    /// <summary>Typed overlap capability required by authoritative resolvers.</summary>
    public interface IBurstPhysicsQueryWithOverlapStatus
    {
        BurstOverlapResult GetInitialOverlap(Vector3 center, float radius);
    }

    /// <summary>Optional seam for the shared pickup reach predicate.</summary>
    public interface IBurstPickupQuery
    {
        PickupReachResult EvaluatePickupReach(Vector3 playerFeet,
            Vector3 pickupAnchor, float reachRadius);
    }

    /// <summary>
    /// Injectable Burst physics seam. Fixture adapters can provide deterministic
    /// overlap and closest-hit results without invoking Unity Physics.
    /// </summary>
    public interface IBurstPhysicsQuery
    {
        /// <summary>Synchronizes authored transforms once before a step batch.</summary>
        void PrepareBatch();

        /// <summary>Runs one closest-hit center-segment sweep.</summary>
        ClosestContactResult Sweep(Vector3 currentCenter, Vector3 nextCenter,
            float radius);
    }

    /// <summary>
    /// Shared pure boundary around the Unity query profile. Player Noise owns
    /// lifecycle/publication; this resolver owns neither inventory nor event output.
    /// </summary>
    public sealed class BurstCollisionResolver : IBurstPhysicsQuery,
        IBurstPhysicsQueryWithOverlapStatus, IBurstPickupQuery,
        IBurstPickupQueryWithOrigin, IDisposable
    {
        private static int _autoSyncPinCount;
        private static bool _autoSyncValueBeforePin;
        private static int _backfacePinCount;
        private static bool _backfaceValueBeforePin;

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
            if (_backfacePinCount == 0)
                _backfaceValueBeforePin = Physics.queriesHitBackfaces;
            _backfacePinCount++;
            Physics.queriesHitBackfaces = false;
        }

        public BurstOverlapResult GetInitialOverlap(Vector3 center, float radius)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BurstCollisionResolver));
            return _profile.GetInitialOverlap(center, radius);
        }

        public PickupReachResult EvaluatePickupReach(Vector3 playerFeet,
            Vector3 pickupAnchor, float reachRadius)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BurstCollisionResolver));
            return _profile.EvaluatePickupReach(playerFeet, pickupAnchor, reachRadius);
        }

        public PickupReachResult EvaluatePickupReach(Vector3 playerFeet,
            Vector3 pickupAnchor, float reachRadius, float originOffset)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BurstCollisionResolver));
            return _profile.EvaluatePickupReach(playerFeet, pickupAnchor,
                reachRadius, originOffset);
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
            _backfacePinCount = Mathf.Max(0, _backfacePinCount - 1);
            if (_backfacePinCount == 0)
                Physics.queriesHitBackfaces = _backfaceValueBeforePin;
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
