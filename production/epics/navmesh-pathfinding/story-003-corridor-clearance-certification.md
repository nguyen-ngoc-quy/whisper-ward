# Story 003: Spatial Corridor Clearance & NavMesh Certification Validator

> **Epic**: NavMesh / Pathfinding
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-22

## Context

**GDD**: `design/gdd/navmesh-pathfinding.md`
**Requirement**: `TR-CORE-006`, `TR-CORE-007`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: NavMesh NonAlloc Query Service and Path Polyline Metric (Secondary: ADR-0002: Shared Physics and Collision Contract)
**ADR Decision Summary**: Implement build-time and editor level certification validators to enforce geometric corridor width clearances ($W_{\text{corridor}} \ge 1.20\text{ m}$), verify zero off-mesh links (`generateLinks = false`), ensure dynamic actor/trigger layers are excluded from NavMesh bakes, and validate voxel resolution ($v_{\text{size}} \le 0.133\text{ m}$), slope ($\le 45^\circ$), and step height ($\le 0.30\text{ m}$).

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW
**Engine Notes**: `NavMeshSurface`, `NavMeshData`, editor geometry sweeping. Zero post-cutoff APIs.

**Control Manifest Rules (this layer)**:
- Required: Spatial corridor clearance enforcement: minimum navigable width $\ge 1.20\text{ m}$ ($\ge 1.50\text{ m}$ main patrol routes).
- Required: Off-mesh links strictly disabled (planar 2.5D surface navigation only, `generateLinks = false`).
- Required: `NavMeshSurface` layer mask must include static `World` and exclude `Player`, `Guard`, `HideSpot`, and `Trigger` layers.
- Required: Bake voxel size must satisfy $v_{\text{size}} \le r_{\text{guard}} / 3 = 0.133\text{ m}$.
- Forbidden: Never permit off-mesh links or unguarded drops in production NavMeshData.
- Guardrail: Build certification script halts CI/CD pipeline if any corridor choke point or off-mesh link is detected.

---

## Acceptance Criteria

*From GDD `design/gdd/navmesh-pathfinding.md`, scoped to this story:*

- [x] **AC-NAV-08 / AC-NAV-18 — Automated Corridor Clearance Certification**: Level geometry scanner sweeps all traversable NavMesh surfaces and measures clearance width between static `World` colliders. Sections with clear width $< 1.20\text{ m}$ fail validation with error code `ERR_CORRIDOR_CHOKE_POINT`.
- [x] **AC-NAV-19 — Zero Off-Mesh Links Verification Gate**: Validator inspects `NavMeshData` assets and confirms `navMeshData.GetOffMeshLinks().Length == 0` and `surface.generateLinks == false`. Any links present trigger build rejection.
- [x] **AC-NAV-20 — Physics Bake Layer Mask Exclusion Invariant**: Build validation verifies that `NavMeshSurface.layerMask` contains static structural layers (`World`, `Default`) and strictly excludes dynamic layers (`Player`, `Guard`, `HideSpot`, `Trigger`).
- [x] **AC-NAV-07 — NavMesh Voxel Resolution Invariant**: Bake configuration validator asserts $v_{\text{size}} \le r_{\text{guard}} / 3 = 0.1333\text{ m}$. Configs with $v_{\text{size}} = 0.100\text{ m}$ pass; configs $> 0.1333\text{ m}$ fail with `ERR_BAKE_VOXEL_TOO_COARSE`.
- [x] **AC-NAV-21 — Maximum Slope and Step Height Ceilings**: Validator asserts `agentMaxSlope <= 45.0f` and `agentClimb <= 0.30f` across all level bake settings.

---

## Implementation Notes

*Derived from ADR-0007 and ADR-0002 Implementation Guidelines:*

1. **Corridor Clearance Validator Algorithm**:
   - Sample points across NavMesh triangles at $0.5\text{ m}$ intervals.
   - For each point, cast bidirectional horizontal rays along polygon normal tangents.
   - Total clear width = distance between opposing static hits on `World` layer.
   - If width $< 1.20\text{ m}$, record violation with world coordinates.
2. **NavMesh Bake Settings Validator**:
   ```csharp
   public static bool ValidateNavMeshSurface(NavMeshSurface surface, out List<string> errors)
   {
       errors = new List<string>();
       if (surface.generateLinks) errors.Add("ERR_OFF_MESH_LINKS_ENABLED");
       if (surface.voxelSize > 0.1333f) errors.Add("ERR_BAKE_VOXEL_TOO_COARSE");
       
       int forbiddenMask = LayerMask.GetMask("Player", "Guard", "HideSpot", "Trigger");
       if ((surface.layerMask.value & forbiddenMask) != 0)
       {
           errors.Add("ERR_DYNAMIC_LAYERS_IN_NAVMESH_BAKE");
       }
       return errors.Count == 0;
   }
   ```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: NonAlloc corner buffers, polyline cumulative distance metric, and arrival/path-end predicates.
- [Story 002]: RVO avoidance priorities, multi-guard query staggering, and HideSpot approach standoff routing.

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**[For Logic / Integration stories — automated test specs]:**

- **AC-NAV-08 / 18**: Corridor Width Invariant Validation
  - Given: A modeled corridor fixture with static colliders placed at width $1.10\text{ m}$ ($< 1.20\text{ m}$).
  - When: Clearance validator `ValidateCorridorClearance()` is executed.
  - Then: Validator returns `false` with error `ERR_CORRIDOR_CHOKE_POINT` and coordinate payload.
  - Edge cases: Corridors with width $1.20\text{ m}$ and $1.50\text{ m}$ return `true`.
- **AC-NAV-19**: Off-Mesh Links Verification
  - Given: A `NavMeshData` asset containing 1 off-mesh link.
  - When: `ValidateOffMeshLinks()` is executed.
  - Then: Returns `false` with error `ERR_OFF_MESH_LINKS_PRESENT`.
- **AC-NAV-20**: Bake Layer Mask Exclusion
  - Given: A `NavMeshSurface` configured with layer mask including `Player` or `Trigger`.
  - When: `ValidateBakeLayerMask()` is executed.
  - Then: Returns `false` identifying the illegal dynamic layers.
- **AC-NAV-07**: Voxel Size Invariant
  - Given: Bake voxel size set to $0.15\text{ m}$ ($> 0.1333\text{ m}$).
  - When: `ValidateVoxelSize()` is executed.
  - Then: Returns `false` with `ERR_BAKE_VOXEL_TOO_COARSE`.
- **AC-NAV-21**: Slope & Step Height Bounds
  - Given: Settings with `maxSlope = 50.0f` or `stepHeight = 0.40f`.
  - When: Settings validator is executed.
  - Then: Returns `false` logging vertical parameter violations.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/navigation/navmesh_certification_test.cs` — must exist and pass

**Status**: [x] Verified
- Automated unit test suite: `tests/unit/navigation/navmesh_certification_test.cs` (8 tests, 100% passing).
- Validates automated corridor clearance enforcement (AC-NAV-08, AC-NAV-18), zero off-mesh links verification gate (AC-NAV-19), physics bake layer mask exclusion invariant (AC-NAV-20), voxel resolution invariant (AC-NAV-07), and maximum slope and step height ceilings (AC-NAV-21).

---

## Dependencies

- Depends on: Story 002 (`story-002-rvo-avoidance-navigation-interop.md`)
- Unlocks: Sprint 1 Navigation Implementation

---

## Completion Notes
**Completed**: 2026-09-22
**Criteria**: 5/5 passing
**Deviations**: None
**Test Evidence**: Logic: test suite at `tests/unit/navigation/navmesh_certification_test.cs` (8 unit tests)
**Code Review**: Complete (Approved)
