# Story 001: NonAlloc Path Query Service & Polyline Distance Metric

> **Epic**: NavMesh / Pathfinding
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-22

## Context

**GDD**: `design/gdd/navmesh-pathfinding.md`
**Requirement**: `TR-CORE-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: NavMesh NonAlloc Query Service and Path Polyline Metric
**ADR Decision Summary**: Implement an agent-parameterized `INavMeshQueryService` using pre-allocated corner buffers (`GetCornersNonAlloc`) for zero GC allocations. Calculate exact piecewise 3D polyline distances ($d_{\text{path}}$), enforce strict surface snapping ($\le 0.40\text{ m}$) with thin-wall linecast verification, evaluate catch-gate conditions ($\text{PathComplete} \land d_{\text{path}} \le 5.50\text{ m}$) with hysteresis, and resolve arrival ($\le 0.30\text{ m}$) vs. path-end precedence.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW
**Engine Notes**: AI Navigation package (`com.unity.ai.navigation`). Accessing `path.corners` allocates; must use `path.GetCornersNonAlloc`.

**Control Manifest Rules (this layer)**:
- Required: All path calculations must use `agent.CalculatePath(targetPosition, _sharedPath)` parameterized to the specific agent's radius ($0.40\text{ m}$) and step height ($0.30\text{ m}$).
- Required: NavMesh path queries must never access `path.corners` directly; use pre-allocated buffers with `path.GetCornersNonAlloc(_cornerBuffer)`.
- Required: World target coordinates must snap to NavMesh using `NavMesh.SamplePosition` with a maximum search radius of $\le 0.40\text{ m}$ before path calculation.
- Required: Path distance $d_{\text{path}}$ is computed as the piecewise sum of Euclidean corner segments; catch gate requires $\text{status} == \text{PathComplete} \land d_{\text{path}} \le 5.50\text{ m}$.
- Forbidden: Never invoke static `NavMesh.CalculatePath` (bypasses agent physical clearances).
- Guardrail: Zero bytes (0 B GC) managed heap allocations on path queries and polyline calculations.

---

## Acceptance Criteria

*From GDD `design/gdd/navmesh-pathfinding.md`, scoped to this story:*

- [x] **AC-NAV-01 — Cumulative Path Length Calculation**: Piecewise Euclidean distance along path corners equals exact polyline sum: $d_{\text{path}} = \sum \|\vec{p}_{k+1} - \vec{p}_k\|_2$ (tested with L-shaped fixture $3\text{ m} + 4\text{ m} = 7.000\text{ m} \pm 0.001\text{ m}$).
- [x] **AC-NAV-02 — Path Length Degenerate / Invalid Handling**: If path status is `PathInvalid` or corner count $N < 2$, $d_{\text{path}}$ returns `float.PositiveInfinity`.
- [x] **AC-NAV-03 — Catch-Gate Threshold & Hysteresis**: Catch gate requires $\text{status} == \text{PathComplete} \land d_{\text{path}} \le 5.50\text{ m}$. Once active, holds engagement until $d_{\text{path}} > 6.00\text{ m}$ ($\text{catch\_range} + \text{hyst}$).
- [x] **AC-NAV-04 — Catch-Gate PathPartial Invalidation**: If active path transitions to `PathPartial` (doorway closed), catch gate disengages immediately regardless of Euclidean proximity.
- [x] **AC-NAV-05 — Arrival vs. Path-End Mutually Exclusive Precedence**:
  - Distance to target $\le \text{eps\_arrive} = 0.30\text{ m} \implies \text{Arrived}$.
  - At terminal corner of `PathPartial` and distance to target $> 0.30\text{ m} \implies \text{PathEnd}$.
  - Precedence lock: if both conditions evaluate true, `Arrived` strictly overrides `PathEnd`.
- [x] **AC-NAV-06 / AC-NAV-16 — Chase Re-Pathing Dual-Trigger Predicate**: Re-pathing triggers if $\text{elapsed} \ge T_{\text{repath}} = 0.25\text{ s}$ OR target displacement $\Delta p \ge \Delta p_{\text{trigger}} = 0.50\text{ m}$, subject to hard rate floor $t_{\text{repath\_min}} = 0.10\text{ s}$ (max $10\text{ Hz}$).
- [x] **AC-NAV-09 — Thin-Wall Projection Linecast Rejection**: If `NavMesh.SamplePosition` snaps to the opposite side of a thin wall ($0.15\text{ m}$ partition), `Physics.Linecast` against the `World` mask detects the obstacle and rejects the projection (`reachable = false`).
- [x] **AC-NAV-10 — Elevated Target Rejection**: Noise anchors or targets placed $> 0.40\text{ m}$ above the floor (e.g. elevated crates) fail `SamplePosition` and are marked unreachable.
- [x] **AC-NAV-15 — Query Duration Budget**: Main-thread `CalculatePath` completes in $< 0.50\text{ ms}$ for $99\%$ of queries and never exceeds $1.00\text{ ms}$.

---

## Implementation Notes

*Derived from ADR-0007 Implementation Guidelines:*

1. **Zero-Allocation Corner Buffer**:
   ```csharp
   private readonly NavMeshPath _sharedPath = new NavMeshPath();
   private readonly Vector3[] _cornerBuffer = new Vector3[64];
   ```
2. **Cumulative Polyline Distance Algorithm**:
   ```csharp
   public float CalculateCumulativeDistanceNonAlloc(NavMeshPath path)
   {
       if (path.status == NavMeshPathStatus.PathInvalid) return float.PositiveInfinity;
       int count = path.GetCornersNonAlloc(_cornerBuffer);
       if (count < 2) return float.PositiveInfinity;
       
       float sum = 0f;
       for (int i = 0; i < count - 1; i++)
       {
           sum += Vector3.Distance(_cornerBuffer[i], _cornerBuffer[i + 1]);
       }
       return sum;
   }
   ```
3. **Thin-Wall Protection**:
   ```csharp
   if (NavMesh.SamplePosition(sourcePosition, out NavMeshHit hit, 0.40f, agent.areaMask))
   {
       if (Physics.Linecast(sourcePosition, hit.position, _worldLayerMask, QueryTriggerInteraction.Ignore))
       {
           return false; // Intervening wall detected
       }
       snappedPosition = hit.position;
       return true;
   }
   ```
4. **Precedence Logic**:
   - `float distToTarget = Vector2.Distance(new Vector2(guardPos.x, guardPos.z), new Vector2(targetPos.x, targetPos.z));`
   - If `distToTarget <= 0.30f` $\implies$ return `ArrivalState.Arrived`.
   - If `path.status == NavMeshPathStatus.PathPartial` and `Vector2.Distance(guardXZ, lastCornerXZ) <= 0.30f` $\implies$ return `ArrivalState.PathEnd`.
   - Otherwise return `ArrivalState.Traversing`.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: RVO avoidance priorities, multi-guard query staggering, and HideSpot approach standoff routing.
- [Story 003]: Automated corridor clearance scanner and level certification rules.

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**[For Logic / Integration stories — automated test specs]:**

- **AC-NAV-01**: Cumulative Path Length Calculation
  - Given: A synthetic path fixture with corners $(0, 0, 0)$, $(3, 0, 0)$, $(3, 0, 4)$ and `PathComplete`.
  - When: `CalculateCumulativeDistanceNonAlloc` is executed.
  - Then: Returned length equals $3.0 + 4.0 = 7.000\text{ m} \pm 0.001\text{ m}$.
  - Edge cases: 3D vertical elevation changes on ramps.
- **AC-NAV-02**: Degenerate / Invalid Path Handling
  - Given: A path fixture with `PathInvalid` status or $N = 1$ corner.
  - When: `CalculateCumulativeDistanceNonAlloc` is executed.
  - Then: Return value equals `float.PositiveInfinity`.
- **AC-NAV-03**: Catch-Gate Threshold & Hysteresis
  - Given: Chasing guard with initial `isCatchEngaged = false`.
  - When: Consecutive ticks evaluate distances $5.80\text{ m} \to 5.40\text{ m} \to 5.75\text{ m} \to 6.01\text{ m}$.
  - Then: States strictly resolve to `false` $\to$ `true` $\to$ `true` $\to$ `false`.
- **AC-NAV-04**: PathPartial Gate Invalidation
  - Given: Active catch gate engaged at $d_{\text{path}} = 3.0\text{ m}$.
  - When: Path status changes to `PathPartial`.
  - Then: Catch gate immediately disengages on the same tick.
- **AC-NAV-05**: Arrival vs PathEnd Precedence
  - Given: Agent with $\text{eps\_arrive} = 0.30\text{ m}$.
  - When: Case A (target within $0.20\text{ m}$), Case B (target at $4.9\text{ m}$, last corner at $0.10\text{ m}$), Case C (target and corner within $0.20\text{ m}$).
  - Then: Resolves strictly to Case A: `Arrived`, Case B: `PathEnd`, Case C: `Arrived`.
- **AC-NAV-06 / 16**: Chase Re-Path Trigger Clamp
  - Given: Pursuit configuration $t_{\text{repath\_min}} = 0.10\text{ s}$, $T_{\text{repath}} = 0.25\text{ s}$, $\Delta p_{\text{trigger}} = 0.50\text{ m}$.
  - When: Target displaces $0.80\text{ m}$ after only $0.05\text{ s}$.
  - Then: Re-path predicate returns `false` due to rate floor clamp.
- **AC-NAV-09**: Thin-Wall Linecast Rejection
  - Given: A static $0.15\text{ m}$ wall collider between source coordinate and sampled point.
  - When: `TrySamplePosition` is executed.
  - Then: Method returns `false`, rejecting the sample point on the opposite side of the wall.
- **AC-NAV-10**: Elevated Target Rejection
  - Given: Crate obstacle with top surface at $Y = 0.60\text{ m}$.
  - When: `TrySamplePosition` is invoked with `maxDistance = 0.40m`.
  - Then: Returns `false` due to search radius exhaustion.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/navigation/navmesh_query_test.cs` — must exist and pass

**Status**: [x] Verified
- Automated unit test suite: `tests/unit/navigation/navmesh_query_test.cs` (10 tests, 100% passing).
- Validates cumulative 3D polyline distance, degenerate infinity handling, catch-gate threshold/hysteresis/invalidation, arrival vs path-end precedence locking, chase dual-trigger rate floor clamp, thin-wall linecast rejection, elevated coordinate rejection, and zero-allocation 64-corner buffer capacity.

---

## Dependencies

- Depends on: None (Foundational Core story)
- Unlocks: Story 002 (`story-002-rvo-avoidance-navigation-interop.md`)

---

## Completion Notes
**Completed**: 2026-09-22
**Criteria**: 9/9 passing
**Deviations**: None
**Test Evidence**: Logic: test suite at `tests/unit/navigation/navmesh_query_test.cs` (10 unit tests)
**Code Review**: Complete (Approved)
