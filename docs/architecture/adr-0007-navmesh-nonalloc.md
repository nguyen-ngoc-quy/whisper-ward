# ADR-0007: NavMesh NonAlloc Query Service and Path Polyline Metric

## Status
Accepted

## Date
2026-09-21

## Engine Compatibility

| Field | Value |
|---|---|
| **Engine** | Unity 6 LTS (6000.3.17f1) |
| **Package** | AI Navigation (`com.unity.ai.navigation`), PhysX |
| **Domain** | Navigation / AI / Core |
| **Knowledge Risk** | LOW |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `.claude/docs/technical-preferences.md`, `design/gdd/navmesh-pathfinding.md`, `design/gdd/guard-ai-fsm.md`, `design/gdd/perception.md`, `docs/architecture/adr-0002-physics-collision-contract.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | NUnit tests for zero-allocation corner extraction (`GetCornersNonAlloc`), cumulative distance formula accuracy, $0.40\text{ m}$ sample position threshold rejection, and arrival vs path-end mutual exclusivity |

## ADR Dependencies

| Field | Value |
|---|---|
| **Depends On** | `ADR-0001: Deterministic Event/Messaging Bus`, `ADR-0002: Shared Physics and Collision Contract` |
| **Enables** | `ADR-0010: Guard AI 3-State FSM`, `ADR-0011: Player Noise Stride Ledger` |
| **Blocks** | Production implementation of `NavMeshQueryService.cs` in Sprint 1 |
| **Ordering Note** | This ADR defines the core navigation service contract consumed by Guard AI and Level Certification. |

---

## Context

### Problem Statement
In *Whisper Ward*, stealth tension relies on the physical plausibility and geometric fairness of enemy pathing (Pillar 1: Thinking Enemies, Pillar 2: Fair Mind-Challenge). Guard movement across Patrol, Investigate, and Chase states is governed by path queries on a baked 2.5D walkable manifold. 

Previous prototype implementations suffered from:
1. **Garbage Collection Spikes on Hot Pathing**: Accessing `NavMeshPath.corners` allocates a new `Vector3[]` array on every query, triggering frequent GC frame drops incompatible with WebGL (30 fps) and PC (60 fps) performance targets.
2. **Naive Proximity False Catches**: Evaluating catch gates using straight-line Euclidean distance triggered catches through solid walls, glass partitions, and across floor voids where no physical walking path existed.
3. **Static Query Radius Inconsistencies**: Calling static `NavMesh.CalculatePath` ignored the agent's specific physical radius ($0.40\text{ m}$) and step height ($0.30\text{ m}$), leading to route calculations that clipped doorframes or snagged corners.
4. **Surface Snapping Drift**: Snapping noise sources or target positions across excessive distances caused guards to investigate across structural barriers or snap onto unreachable upper ledges.

### Constraints
- **Unity 6 LTS**, C# 9+, targeting PC Windows (60 fps) and WebGL (30 fps).
- **Scope Discipline (Anti-Pillar Enforcement)**: Ground-plane 2.5D planar navigation only. Off-mesh links strictly disabled (`generateLinks = false`). No jumping, vaulting, climbing, or ladder traversal.
- **Zero-GC on Hot Paths**: 0 managed heap allocations during repeated path calculations, corner extraction, and polyline distance measurements.
- **Agent Physical Footprint**: Radius $r_{\text{guard}} = 0.40\text{ m}$, height $h_{\text{guard}} = 2.00\text{ m}$, max slope $45^\circ$, step height $0.30\text{ m}$.
- **Tight Surface Sampling**: `NavMesh.SamplePosition` search radius capped at $\text{navmesh\_sample\_maxdistance} = 0.40\text{ m}$ (exceeds maximum step height $0.30\text{ m}$ but strictly less than minimum wall thickness $0.50\text{ m}$).
- **Corridor Clearance Invariant**: Minimum traversable corridor width $\ge 1.20\text{ m}$ ($\ge 1.50\text{ m}$ for primary two-guard patrol corridors).

### Requirements
- **TR-CORE-005**: Agent-parameterized `NavMeshAgent.CalculatePath` invocation using pre-allocated `NavMeshPath` buffers (0 GC).
- **TR-CORE-006**: Spatial corridor clearance enforcement: minimum navigable width $\ge 1.20\text{ m}$ ($\ge 1.50\text{ m}$ main patrol routes).
- **TR-CORE-007**: Off-mesh links strictly disabled (planar 2.5D surface navigation only, `generateLinks = false`).

---

## Decision

Implement a **Non-Allocating NavMesh Query Service** (`NavMeshQueryService`) backed by Unity's AI Navigation package, exposing the `INavMeshQueryService` contract.

```text
               ┌───────────────────────────────┐
               │         Guard AI FSM          │
               │   (Patrol / Investigate /     │
               │            Chase)             │
               └───────────────┬───────────────┘
                               │ Request Path
                               ▼
               ┌───────────────────────────────┐
               │    INavMeshQueryService       │
               │  - TrySamplePosition(0.40m)   │
               │  - CalculatePathNonAlloc      │
               │  - Cumulative Distance Metric │
               └───────────────┬───────────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
        ▼                      ▼                      ▼
┌────────────────┐   ┌───────────────────┐   ┌─────────────────┐
│ SamplePosition │   │ NavMeshAgent      │   │ Pre-allocated   │
│ MaxDist <=0.40m│   │ CalculatePath     │   │ Corner Buffer   │
│ Snap to Mesh   │   │ Parameterized     │   │ Vector3[64]     │
└───────┬────────┘   └─────────┬─────────┘   └────────┬────────┘
        │                      │                      │
        └──────────────────────┼──────────────────────┘
                               │
                               ▼
               ┌───────────────────────────────┐
               │      PathQueryResult          │
               │  - Status (Complete/Partial)  │
               │  - d_path (3D Polyline Sum)   │
               │  - Terminal Corner            │
               │  - IsReachable (bool)         │
               └───────────────┬───────────────┘
                               │
                 ┌─────────────┴─────────────┐
                 ▼                           ▼
         Guard Locomotion             Catch Gate Metric
       (RVO 10/30/50 Steer)         (d_path <= 5.50m & PathComplete)
```

### 1. Zero-Allocation Corner Extraction & Query Pooling
- Direct access to `navMeshPath.corners` is strictly prohibited.
- `NavMeshQueryService` maintains an internal reusable `NavMeshPath` instance per worker thread / main thread and pre-allocates an internal corner buffer:
  ```csharp
  private readonly Vector3[] _cornerBuffer = new Vector3[64];
  ```
- Corner extraction uses Unity's non-allocating API:
  ```csharp
  int count = path.GetCornersNonAlloc(_cornerBuffer);
  ```
- This guarantees 0 bytes of managed heap allocations per query.

### 2. Agent-Parameterized Path Authority
- All path calculations must be parameterized against the specific `NavMeshAgent` instance via:
  ```csharp
  agent.CalculatePath(targetPosition, _sharedPath);
  ```
- Static `NavMesh.CalculatePath` is banned because it omits the agent's physical radius ($0.40\text{ m}$), step height ($0.30\text{ m}$), and agent-type clearances.

### 3. Cumulative 3D Polyline Distance Metric ($d_{\text{path}}$)
The distance metric along the calculated path is computed as the piecewise sum of Euclidean straight-line segments:

$$d_{\text{path}} = \begin{cases} \sum_{k=0}^{N-2} \|\vec{p}_{k+1} - \vec{p}_k\|_2 & \text{if } \text{status} \neq \text{PathInvalid} \land N \ge 2 \\ +\infty & \text{otherwise} \end{cases}$$

Where:
- $\vec{p}_k$ are the points extracted via `GetCornersNonAlloc`.
- If the path is `PathInvalid` or $N < 2$, $d_{\text{path}} = +\infty$.
- The Catch Gate requires:
  $$\text{status} == \text{PathComplete} \land d_{\text{path}} \le \text{catch\_range} \quad (5.50\text{ m})$$

### 4. Surface Snapping Policy & Threshold
Before pathing toward arbitrary world coordinates (noise events, player position, or hide spots), the position is snapped to the NavMesh:
```csharp
bool valid = NavMesh.SamplePosition(sourcePosition, out NavMeshHit hit, 0.40f, agent.areaMask);
```
- Snapping radius is hard-locked to $0.40\text{ m}$.
- If `SamplePosition` fails, the target is declared unreachable (`reachable = false`), and the query returns `PathQueryResultStatus.Invalid`.

### 5. Arrival Predicate vs. Path-End Precedence
- **Arrival Condition**:
  $$\text{EuclideanXZ}(\vec{p}_{\text{guard}}, \vec{p}_{\text{target}}) \le \text{eps\_arrive} \quad (0.30\text{ m})$$
- **Mutual Exclusivity**:
  - If within $0.30\text{ m}$ of sampled target $\implies$ signal `arrived`.
  - If at terminal corner of a `PathPartial` that is $> 0.30\text{ m}$ from sampled target $\implies$ signal `path-end`.

### 6. RVO Avoidance and Priority Hierarchy
Guards utilize Unity's `NavMeshAgent` Reciprocal Velocity Avoidance with `ObstacleAvoidanceType.HighQualityObstacleAvoidance`:
- **Chase State**: `avoidancePriority = 10` (highest; other guards yield).
- **Investigate State**: `avoidancePriority = 30`.
- **Patrol State**: `avoidancePriority = 50` (yields to alerted guards).
- Avoidance radius: $r_{\text{avoid}} = 0.45\text{ m}$ ($r_{\text{guard}} + 0.05\text{ m}$ cushion).
- The player character is **not** a `NavMeshObstacle` (carving dynamic player obstacles induces expensive runtime re-bakes and breaks determinism). Physical capsule-capsule collisions are resolved via PhysX.

---

## Key Interfaces

```csharp
namespace WhisperWard.Core.Contracts
{
    public enum PathQueryResultStatus
    {
        Invalid = 0,
        Complete = 1,
        Partial = 2
    }

    public readonly struct PathQueryResult
    {
        public readonly PathQueryResultStatus Status;
        public readonly float CumulativeDistance;
        public readonly int CornerCount;
        public readonly Vector3 TerminalCorner;
        public readonly bool IsReachable;

        public PathQueryResult(
            PathQueryResultStatus status,
            float cumulativeDistance,
            int cornerCount,
            Vector3 terminalCorner,
            bool isReachable)
        {
            Status = status;
            CumulativeDistance = cumulativeDistance;
            CornerCount = cornerCount;
            TerminalCorner = terminalCorner;
            IsReachable = isReachable;
        }
    }

    public interface INavMeshQueryService
    {
        bool TrySamplePosition(Vector3 sourcePosition, out Vector3 snappedPosition, float maxDistance = 0.40f);
        PathQueryResult CalculatePathNonAlloc(NavMeshAgent agent, Vector3 targetPosition);
        float CalculateCumulativeDistanceNonAlloc(NavMeshPath path);
        bool IsArrived(Vector3 currentPosition, Vector3 targetPosition, float arriveThreshold = 0.30f);
    }
}
```

---

## Alternatives Considered

### Alternative 1: Static `NavMesh.CalculatePath` Query Utility
- **Description**: Use a static helper calling `NavMesh.CalculatePath(start, end, filter, path)`.
- **Pros**: Does not require an active `NavMeshAgent` reference.
- **Cons**: Strips out agent radius and step height parameters; queries clip walls and calculate illegal routes through narrow gaps that guards cannot traverse.
- **Rejection Reason**: Violates Pillar 1 physical plausibility and causes guard pathing snags.

### Alternative 2: Custom A* Graph Over Grid Nodes
- **Description**: Implement a proprietary A* pathfinder on a discretized 2D/3D grid.
- **Pros**: Complete internal control over memory allocation and serialization.
- **Cons**: Excessive development cost (3–4 weeks to handle 2.5D slopes, ramps, obstacle avoidance, and baking tools); unacceptable schedule risk for an 8-week solo timeline.
- **Rejection Reason**: Violates the 8-week production schedule when Unity's built-in AI Navigation package is battle-tested.

---

## Consequences

### Positive
- **Zero-GC Allocation on Hot Paths**: Using pre-allocated buffers and `GetCornersNonAlloc` prevents garbage collection hitches during high-frequency pursuit queries.
- **Absolute Catch Fairness**: Catch evaluation strictly enforces `PathComplete` and $d_{\text{path}} \le 5.50\text{ m}$, eliminating unfair catches through walls or across floor voids.
- **Deterministic Avoidance**: Priority-based RVO ensures pursuing guards never deadlock or get obstructed by patrolling guards.

### Negative
- **Corner Buffer Limit**: Static buffer capacity of 64 corners bounds path complexity. Paths exceeding 64 corners clamp at corner 64. (Mitigated: 64 corners exceeds any authored room in Whisper Ward's compact facility levels).

### Risks & Mitigations
- **Target Off-Mesh Invalidation**: Player standing near edge could result in `SamplePosition` failure. Mitigated by setting sample threshold to $0.40\text{ m}$ (larger than step height $0.30\text{ m}$).
- **Re-pathing Thrashing**: Rapid path recalculation in Chase could consume CPU budget. Mitigated by enforcing dual-trigger cadence (minimum $0.10\text{ s}$ cooldown; $0.50\text{ m}$ player displacement trigger).

---

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|---|---|---|
| `navmesh-pathfinding.md` | TR-CORE-005: Agent-parameterized `CalculatePath` with 0 GC | Enforces `agent.CalculatePath` and `GetCornersNonAlloc` with pre-allocated buffer in `INavMeshQueryService`. |
| `navmesh-pathfinding.md` | TR-CORE-006: Corridor width $\ge 1.20\text{ m}$ ($\ge 1.50\text{ m}$ patrol) | Formalized as an architectural level-baking invariant matching guard radius $0.40\text{ m}$ and avoidance cushion $0.45\text{ m}$. |
| `navmesh-pathfinding.md` | TR-CORE-007: Off-mesh links strictly disabled | Bakes `NavMeshSurface` with `generateLinks = false` for pure 2.5D planar navigation. |

---

## Performance Implications
- **CPU**: Sub-millisecond path calculation ($< 0.1\text{ ms}$ per query on modern desktop CPU; $< 0.3\text{ ms}$ on WebGL). Re-pathing throttled to $10\text{ Hz}$ max.
- **Memory**: Zero managed heap allocations during queries. Static buffer overhead is $< 1\text{ KB}$ per service instance.
- **Load Time**: Static pre-baked NavMesh assets loaded asynchronously with scene data.

---

## Validation Criteria
- NUnit EditMode tests confirm:
  1. `CalculateCumulativeDistanceNonAlloc` computes exact polyline lengths for $N$-corner paths.
  2. Passing invalid or 1-corner paths yields float.PositiveInfinity.
  3. `TrySamplePosition` returns false for coordinates $> 0.40\text{ m}$ from geometry.
  4. Memory profiler validates 0 bytes allocated during continuous 1000-iteration query loops.
