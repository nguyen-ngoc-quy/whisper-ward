# Story 003: Geometric Wall Thickness Validator & NonAlloc Spatial Queries

> **Epic**: Physics & Collision Configuration  
> **Status**: Ready  
> **Layer**: Foundation  
> **Type**: Logic  
> **Estimate**: 4 hours  
> **Manifest Version**: 2026-09-21  
> **Last Updated**: 2026-09-21  

## Context

**GDD**: `design/gdd/physics-collision-config.md`  
**Requirement**: `TR-FOUND-007`  
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: `ADR-0002: Shared Physics and Collision Contract`  
**ADR Decision Summary**: Invariant enforcement of minimum geometric wall thickness $\ge 0.10\text{ m}$, origin-embedded geometry detection via $0.05\text{ m}$ CheckSphere, and 0-allocation NonAlloc query arrays.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  
**Engine Notes**: Operates with pre-allocated buffer arrays (`initial_overlap_result_capacity = 64`).

**Control Manifest Rules (this layer)**:
- Required: **Geometric Wall Thickness Invariant** — All solid obstacle, wall, and occluder geometry must adhere to a minimum thickness invariant of $\ge 0.10\text{ m}$ to guarantee zero tunneling. — source: `ADR-0002`
- Forbidden: **Never Query NavMesh or Physics with Allocating Methods** — Never use allocating calls (e.g. `Physics.RaycastAll`); all queries must use pre-allocated non-allocating buffers. — source: `ADR-0002`
- Guardrail: **Physics Query Frame Budget** — Maximum 1.20 ms per frame total for all spatial physics queries. — source: `ADR-0002`

---

## Acceptance Criteria

*From GDD `design/gdd/physics-collision-config.md`, scoped to this story:*

- [ ] **AC (Minimum Wall Thickness Invariant)**: The `WallThicknessValidator` inspects colliders on the `E20Mask`. If any box, capsule, or mesh collider boundary has a thickness dimension $< 0.10\text{ m}$, it flags an invariant violation with collider identification.
- [ ] **AC (Embedded Origin Safety Fallback - E10)**: When a raycast or linecast origin is embedded inside a solid collider, `CheckSphere(origin, 0.05f, E20Mask)` detects the embedding and immediately returns `HIT_OCCLUDED` (fail-closed) rather than letting the ray exit without contact.
- [ ] **AC (Zero Allocation NonAlloc Queries)**: All spatial query APIs (`SphereCastNonAlloc`, `OverlapSphereNonAlloc`) use internal pre-allocated buffers ($C_{\text{buf}} = 64$). Buffer saturation fails closed and logs a diagnostic warning.

---

## Implementation Notes

*Derived from ADR-0002 Implementation Guidelines:*

1. Implement `WallThicknessValidator` utility with method `bool ValidateColliderThickness(Collider col, float minThickness = 0.10f)`.
2. For `BoxCollider`, check `size.x`, `size.y`, `size.z` multiplied by `lossyScale`.
3. For `SphereCollider` and `CapsuleCollider`, check diameter $2 \times R \ge 0.10\text{ m}$.
4. In `PhysicsQueryService`, prepend raycasts with `Physics.CheckSphere(origin, 0.05f, E20Mask)` to handle E10 embedded origins.
5. Allocate a static `Collider[64]` and `RaycastHit[64]` buffer for zero-GC operations.

---

## Out of Scope

- [Story 001]: Layer matrix resolution and Rigidbody scan.
- [Story 002]: Global settings assertion and trigger immunity contracts.

---

## QA Test Cases

*Defined by QA Lead for automated testing:*

- **AC-Thickness (Wall Thickness Assertion)**:
  - Given: A BoxCollider with thickness $0.05\text{ m}$ ($< 0.10\text{ m}$) on layer `World`.
  - When: `WallThicknessValidator.Validate(collider)` is called.
  - Then: Assert validation returns `false` and identifies the violating dimension.

- **AC-Embedded (Embedded Origin Detection)**:
  - Given: A raycast origin placed inside a solid World BoxCollider.
  - When: `CheckOcclusionLine` is executed.
  - Then: Assert result is `true` (occluded) due to embedded origin check.

- **AC-NonAlloc (Zero GC on Queries)**:
  - Given: 500 spherecast queries executed in a loop.
  - When: Memory allocations are tracked.
  - Then: Assert GC allocation is 0 bytes.

---

## Test Evidence

**Story Type**: Logic  
**Required evidence**: `tests/EditMode/Foundation/PhysicsWallThicknessValidatorTests.cs` — must exist and pass  
**Status**: [ ] Not yet created  

---

## Dependencies

- Depends on: Story 001, Story 002
- Unlocks: Physics Configuration Epic completion (`/story-done`)
