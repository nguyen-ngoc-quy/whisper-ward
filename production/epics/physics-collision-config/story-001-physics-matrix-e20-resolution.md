# Story 001: 7-Layer Physics Matrix & Fail-Closed E20 Mask Resolution

> **Epic**: Physics & Collision Configuration  
> **Status**: Ready  
> **Layer**: Foundation  
> **Type**: Logic  
> **Estimate**: 3 hours  
> **Manifest Version**: 2026-09-21  
> **Last Updated**: 2026-09-21  

## Context

**GDD**: `design/gdd/physics-collision-config.md`  
**Requirement**: `TR-FOUND-005`  
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: `ADR-0002: Shared Physics and Collision Contract`  
**ADR Decision Summary**: 7-layer physics matrix definition, fail-closed layer resolution at startup, contentful scene validation, zero dynamic Rigidbody invariant, and isolated `HideSpotContainmentProfile`.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  
**Engine Notes**: Layers resolved once at startup via `LayerMask.NameToLayer` with bitmask caching.

**Control Manifest Rules (this layer)**:
- Required: **E20 Layer Mask Resolution at Startup** — Exact layer names (`World`, etc.) must resolve and cache once at initialization via `LayerMask.NameToLayer`. Mask initialization must fail-closed if any layer name returns `-1`. — source: `ADR-0002`
- Required: **Contentful Scene Validation** — The Physics service must assert that the active scene contains at least one non-trigger collider on the assembled E20 mask upon initialization; an empty/contentless mask fails closed. — source: `ADR-0002`
- Forbidden: **Never Include Player/Guard/Trigger in E20 Mask** — Reject any mask containing bits for `Player`, `Guard`, or trigger volumes during E20 assembly. — source: `ADR-0002`

---

## Acceptance Criteria

*From GDD `design/gdd/physics-collision-config.md`, scoped to this story:*

- [ ] **AC1 (Layer Resolution & E20 Mask Assembly)**: On initialization, resolve the 7 required layers (`Player`, `Guard`, `World`, `VisionOccluder`, `SoundOccluder`, `HideSpotTrigger`, `PickupTrigger`). If any required layer is missing (`NameToLayer == -1`), fail closed and throw `PhysicsLayerMissingException`. Assemble `E20Mask` strictly from occluder and world layers, asserting no `Player`, `Guard`, or trigger bits are included.
- [ ] **AC2 (Zero Dynamic Rigidbody Invariant)**: When scanning the active scene or registering entities, assert that 100% of Rigidbodies have `isKinematic == true`. Any dynamic Rigidbody (`isKinematic == false`) throws a validation failure.
- [ ] **AC5 (Dedicated HideSpot Containment Profile)**: Provide `HideSpotContainmentProfile` as an isolated configuration filtering solely the `HideSpotTrigger` layer, completely isolated from solid geometry.

---

## Implementation Notes

*Derived from ADR-0002 Implementation Guidelines:*

1. Define layer constants and names in `WhisperWard.Foundation.Physics.Contracts`.
2. Implement `PhysicsCollisionConfigService` which caches layer IDs and bitmasks in readonly fields.
3. Validate that `E20Mask & (LayerMask.GetMask("Player", "Guard", "HideSpotTrigger", "PickupTrigger")) == 0`.
4. Provide scene validation method `ValidateScenePhysics()` to verify non-empty E20 collider presence and assert zero dynamic Rigidbodies.

---

## Out of Scope

- [Story 002]: Global physics setting assertions and sensing raycast trigger immunity.
- [Story 003]: Wall thickness validation geometry audit and spatial queries.

---

## QA Test Cases

*Defined by QA Lead for automated testing:*

- **AC-1 (Fail-Closed Layer Resolution)**:
  - Given: A mocked layer provider where layer "World" returns -1.
  - When: `PhysicsCollisionConfigService.Initialize()` is called.
  - Then: Assert `PhysicsLayerMissingException` is thrown.
  - Edge cases: When all 7 layers resolve, verify `E20Mask` contains only World, VisionOccluder, and SoundOccluder.

- **AC-2 (Zero Dynamic Rigidbody Assertion)**:
  - Given: A test GameObject with a Rigidbody where `isKinematic = false`.
  - When: `ValidateScenePhysics()` is called.
  - Then: Assert validation returns failure or throws invariant violation.

- **AC-5 (HideSpot Profile Isolation)**:
  - Given: `HideSpotContainmentProfile` is queried.
  - When: Comparing its mask against `E20Mask`.
  - Then: Assert `(HideSpotProfile.Mask & E20Mask) == 0`.

---

## Test Evidence

**Story Type**: Logic  
**Required evidence**: `tests/EditMode/Foundation/PhysicsMatrixResolutionTests.cs` — must exist and pass  
**Status**: [ ] Not yet created  

---

## Dependencies

- Depends on: None (Foundational physics contract)
- Unlocks: Story 002 (Queries depend on resolved masks)
