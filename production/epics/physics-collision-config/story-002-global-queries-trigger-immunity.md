# Story 002: Global Physics Queries & Trigger Immunity Contract

> **Epic**: Physics & Collision Configuration  
> **Status**: Ready  
> **Layer**: Foundation  
> **Type**: Logic  
> **Estimate**: 3 hours  
> **Manifest Version**: 2026-09-21  
> **Last Updated**: 2026-09-21  

## Context

**GDD**: `design/gdd/physics-collision-config.md`  
**Requirement**: `TR-FOUND-006`  
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: `ADR-0002: Shared Physics and Collision Contract`  
**ADR Decision Summary**: Global assertions for `autoSyncTransforms = false` and `queriesHitBackfaces = false`, universal `QueryTriggerInteraction.Ignore` for perception/sensing queries, and demand-driven transform sync batching.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  
**Engine Notes**: Enforces PhysX global settings at runtime initialization.

**Control Manifest Rules (this layer)**:
- Forbidden: **Never Allow queriesHitTriggers = true for Perception** — Never enable trigger collision (`queriesHitTriggers = true` or `QueryTriggerInteraction.Collide`) for hearing, vision, pickup reach, or Burst collision queries. Use `QueryTriggerInteraction.Ignore` universally across sensing raycasts. — source: `ADR-0002`
- Forbidden: **Never Allow queriesHitBackfaces = true** — Backface hits must remain disabled (`Physics.queriesHitBackfaces = false`) for the entire gameplay session. — source: `ADR-0002`
- Required: **Solid Transform Synchronization** — Explicitly call `Physics.SyncTransforms()` once prior to executing Burst trajectory simulation batches or capsule query resize operations. — source: `ADR-0002`, `ADR-0006`

---

## Acceptance Criteria

*From GDD `design/gdd/physics-collision-config.md`, scoped to this story:*

- [ ] **AC3 (Global Physics Invariant Assertion)**: On startup, assert that `Physics.autoSyncTransforms == false` and `Physics.queriesHitBackfaces == false`. If not set, configure them explicitly and verify.
- [ ] **AC4 (Sensing Query Trigger Immunity)**: All spatial queries dispatched via `IPhysicsQueryService` (hearing linecasts, vision raycasts, pickup queries) strictly use `QueryTriggerInteraction.Ignore`. Perception rays cast through trigger volumes (HideSpots, Pickups) pass through without collision.
- [ ] **AC6 (Demand-Driven Transform Sync Batching)**: Transform synchronization (`Physics.SyncTransforms()`) is rate-limited to $\le 5.0\text{ Hz}$ or batched at most once per sensing cycle / Burst trajectory batch, avoiding per-ray sync calls.

---

## Implementation Notes

*Derived from ADR-0002 Implementation Guidelines:*

1. Expose `IPhysicsQueryService` with methods:
   - `bool CheckOcclusionLine(Vector3 start, Vector3 end, int layerMask, out RaycastHit hit)`
   - `int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDistance, int layerMask)`
2. Enforce `QueryTriggerInteraction.Ignore` internally inside query implementations so callers cannot accidentally collide with triggers.
3. Provide `SyncTransformsBatch()` method with a cooldown timer enforcing `sensing_sync_max_frequency = 5.0f`.

---

## Out of Scope

- [Story 001]: Layer mask initialization and Rigidbody kinematic validation.
- [Story 003]: Wall thickness geometric audit and origin-embedded detection.

---

## QA Test Cases

*Defined by QA Lead for automated testing:*

- **AC-3 (Global Settings Assertion)**:
  - Given: `Physics.autoSyncTransforms` or `Physics.queriesHitBackfaces` is true.
  - When: Service startup executes.
  - Then: Assert settings are locked to `false`.

- **AC-4 (Trigger Immunity Proof)**:
  - Given: A Raycast originates at Point A and targets Point B behind a World wall, with a Trigger volume placed between them.
  - When: `CheckOcclusionLine` is executed.
  - Then: Assert the hit result is the World wall, completely ignoring the trigger volume.

- **AC-6 (Transform Sync Batching)**:
  - Given: 50 raycasts requested in rapid succession.
  - When: Executed within the same frame.
  - Then: Assert `Physics.SyncTransforms()` is invoked at most once.

---

## Test Evidence

**Story Type**: Logic  
**Required evidence**: `tests/EditMode/Foundation/PhysicsQueryTriggerImmunityTests.cs` — must exist and pass  
**Status**: [ ] Not yet created  

---

## Dependencies

- Depends on: Story 001 (Requires resolved E20 masks)
- Unlocks: Story 003 (Validator builds upon query contracts)
