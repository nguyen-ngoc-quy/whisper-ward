# Story 002: Reciprocal Velocity Avoidance & Spatial Navigation Interop

> **Epic**: NavMesh / Pathfinding
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-22

## Context

**GDD**: `design/gdd/navmesh-pathfinding.md`, `design/gdd/player-movement-hide.md`
**Requirement**: `TR-CORE-005`, `TR-FEAT-015`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0007: NavMesh NonAlloc Query Service and Path Polyline Metric (Secondary: ADR-0002, ADR-0006)
**ADR Decision Summary**: Configure Unity `NavMeshAgent` Reciprocal Velocity Avoidance (RVO) with strict state-driven priority ordering (Chase: 10, Investigate: 30, Patrol: 50) and avoidance radius $r_{\text{avoid}} = 0.45\text{ m}$. Enforce multi-guard query staggering to eliminate frame spikes, provide automatic off-mesh recovery warping within $1.0\text{ m}$, enforce agent physical radius ($0.40\text{ m}$) through narrow apertures, and route `HideSpot` approaches to the standardized `guard_hold` anchor.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW
**Engine Notes**: `NavMeshAgent.avoidancePriority`, `NavMeshAgent.radius`, `NavMeshAgent.Warp()`. Player is strictly not a dynamic NavMeshObstacle.

**Control Manifest Rules (this layer)**:
- Required: Set `NavMeshAgent.avoidancePriority` dynamically by state: Chase (10), Investigate (30), Patrol (50).
- Required: Avoidance radius configured to $r_{\text{avoid}} = 0.45\text{ m}$ ($r_{\text{guard}} + 0.05\text{ m}$ cushion).
- Required: Multi-guard chase queries must be staggered across frames (max 1 full `CalculatePath` per frame tick).
- Required: During HideSpot approach, navigate to `guard_hold` ($\text{spot\_front\_anchor} + r_{\text{guard}} \times \text{hold\_vector}$) and verify reachability against `proxy(interior_position)`.
- Forbidden: Never attach dynamic carving `NavMeshObstacle` components to the player or moving guards (breaks determinism and induces runtime re-bakes).
- Guardrail: Staggered query scheduler limits NavMesh query burst cost to $< 1.0\text{ ms}$ per frame across all guards.

---

## Acceptance Criteria

*From GDD `design/gdd/navmesh-pathfinding.md` and `design/gdd/player-movement-hide.md`, scoped to this story:*

- [x] **AC-NAV-12 — RVO Head-On Avoidance Priority Resolution**: In a $1.20\text{ m}$ corridor, a Chasing guard (`priority = 10`) maintains forward velocity along the corridor axis ($\le 0.15\text{ m}$ deviation), while a Patrolling guard (`priority = 50`) yields to the wall, maintaining separation $\ge 0.80\text{ m}$ at all times without capsule interpenetration.
- [x] **AC-NAV-11 — Agent-Parameterized Aperture Clearance**: Navigating through a $0.60\text{ m}$ narrow gap fails for guard instances ($r_{\text{guard}} = 0.40\text{ m}$, required clear width $0.80\text{ m}$), confirming agent parameters are enforced over static queries.
- [x] **AC-NAV-13 — Off-Mesh Warp Recovery Gate**: If external physics forces cause `agent.isOnNavMesh == false`, the recovery routine samples the nearest surface within $1.0\text{ m}$ and calls `agent.Warp(hit.position)` to restore valid grounded navigation within 1 tick.
- [x] **AC-NAV-14 — HideSpot Approach Standoff Navigation**: Guard AI navigating toward an occupied HideSpot targets `guard_hold` ($\text{spot\_front\_anchor} + r_{\text{guard}} \times \text{hold\_vector}$) rather than the raw prop centroid, and reachability is tested against `proxy(interior_position)`.
- [x] **AC-NAV-17 — Multi-Guard Chase Staggered Query Distribution**: When multiple guards chase the player simultaneously, queries are round-robin interleaved so that at most 1 guard executes a full `CalculatePath` query on any single engine frame tick.

---

## Implementation Notes

*Derived from ADR-0007 Implementation Guidelines:*

1. **RVO Agent Setup**:
   ```csharp
   agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
   agent.radius = 0.40f;
   // Set priority according to state
   agent.avoidancePriority = state switch
   {
       GuardState.Chase => 10,
       GuardState.Investigate => 30,
       GuardState.Patrol => 50,
       _ => 50
   };
   ```
2. **Off-Mesh Warp Recovery**:
   ```csharp
   if (!agent.isOnNavMesh)
   {
       if (NavMesh.SamplePosition(agent.transform.position, out NavMeshHit hit, 1.0f, agent.areaMask))
       {
           agent.Warp(hit.position);
       }
       else
       {
           Debug.LogError($"ERR_GUARD_FALLEN_OFF_NAVMESH: Guard {agent.name} unrecoverable.");
       }
   }
   ```
3. **Query Scheduler / Staggerer**:
   - Maintain an internal round-robin queue `Queue<NavMeshAgent> _pendingQueryQueue`.
   - On each frame update, dequeue and execute at most one query from the queue, deferring subsequent requests to next ticks.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: NonAlloc corner buffers, polyline cumulative distance metric, and arrival/path-end predicates.
- [Story 003]: Automated corridor clearance scanner and level certification rules.

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**[For Logic / Integration stories — automated test specs]:**

- **AC-NAV-12**: RVO Head-On Priority Resolution
  - Given: Guard 1 (Chase, priority 10) and Guard 2 (Patrol, priority 50) positioned $10\text{ m}$ apart in a $1.20\text{ m}$ wide corridor.
  - When: Simulation runs for 30 ticks ($1.5\text{ s}$).
  - Then: Guard 1 maintains axial trajectory; Guard 2 yields; minimum distance between centers never drops below $0.80\text{ m}$.
- **AC-NAV-11**: Aperture Clearance Enforcement
  - Given: A static corridor constriction with width $0.60\text{ m}$.
  - When: Path calculation is executed for guard agent ($r_{\text{guard}} = 0.40\text{ m}$).
  - Then: Path returns `PathPartial` or `PathInvalid`, refusing passage through the narrow gap.
- **AC-NAV-13**: Off-Mesh Warp Recovery
  - Given: Guard agent displaced $0.50\text{ m}$ below NavMesh floor.
  - When: Recovery routine runs on subsequent tick.
  - Then: Agent position is warped to nearest surface point and `agent.isOnNavMesh` returns `true`.
- **AC-NAV-14**: HideSpot Approach Anchor
  - Given: HideSpot at centroid $(10, 0, 0)$, front anchor at $(10, 0, 1)$, hold vector $(0, 0, 1)$.
  - When: `HideSpotApproach` path is generated for guard.
  - Then: Target coordinate matches $(10, 0, 1) + 0.40 \times (0, 0, 1) = (10, 0, 1.40)\text{ m}$.
- **AC-NAV-17**: Multi-Guard Staggered Query Distribution
  - Given: 3 chasing guards requesting re-paths on the same frame tick.
  - When: Scheduler runs over 3 consecutive frames.
  - Then: Exactly 1 path query executes per frame, distributing the 3 requests cleanly across frames 0, 1, and 2.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/navigation/navmesh_avoidance_interop_test.cs` — must exist and pass

**Status**: [x] Verified
- Automated integration test suite: `tests/integration/navigation/navmesh_avoidance_interop_test.cs` (6 tests, 100% passing).
- Validates RVO priority hierarchy resolution, agent-parameterized aperture physical clearance, off-mesh recovery warping, HideSpot standoff anchor calculation, and multi-guard query round-robin time-slicing.

---

## Dependencies

- Depends on: Story 001 (`story-001-nonalloc-query-polyline-metric.md`)
- Unlocks: Story 003 (`story-003-corridor-clearance-certification.md`)

---

## Completion Notes
**Completed**: 2026-09-22
**Criteria**: 5/5 passing
**Deviations**: None
**Test Evidence**: Integration: test suite at `tests/integration/navigation/navmesh_avoidance_interop_test.cs` (6 tests)
**Code Review**: Complete (Approved)
