# NavMesh / Pathfinding

> **Status**: In Design
> **Author**: Claude Code Game Studios
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Thinking Enemies (Pillar 1), Fair Mind-Challenge (Pillar 2)

## Overview

The NavMesh / Pathfinding system is Whisper Ward's core spatial navigation and reachability infrastructure. Built on Unity's AI Navigation package (`NavMeshSurface`, `NavMeshAgent`, `NavMeshPath`), it provides the authoritative continuous 2.5D walkable manifold and geometric path metrics consumed by Guard AI (`#1`), Perception (`#2`), and Level Certification (`#8`). The system is entirely automatic and invisible to the player, serving as the physical backbone for guard locomotion across all three FSM states: deterministic transit between patrol waypoints, investigative routing to noise and visual distraction anchors, and live pursuit during active chase. 

Crucially, the NavMesh system defines and bounds the game's **catch contract**: catch-gate engagement, arrival predicates, and give-up evaluation are strictly governed by agent-parameterized path queries (`NavMeshAgent.CalculatePath`) rather than naive Euclidean proximity. By establishing authoritative surface-snapping (`NavMesh.SamplePosition`), deterministic waypoint traversal, explicit partial-path termination, and strict physics layer separation, the system prevents guards from clipping geometry, taking illegal shortcuts through walls, or triggering unfair captures through solid cover, directly upholding Pillar 1 (*Thinking Enemies*) and Pillar 2 (*Fair Mind-Challenge*).

## Player Fantasy

As an underlying infrastructure system, the player never sees or directly manipulates the Navigation Mesh; instead, they experience its presence through the **physical believability, spatial legibility, and absolute fairness** of enemy movement.

The primary emotional targets served are **tactical anticipation, acute stealth tension, and intellectual mastery**:

1. **The Physicality of the Pursuer (Pillar 1 — Thinking Enemies)**:
   The player never feels opposed by "game-logic ghosts" on invisible rails or cheaters that phase through level geometry. Guards navigate doorways, round corridor corners, and path around desks and industrial machinery exactly as a physical human guard would. When a guard enters Chase, their pursuit vector is physically grounded—the player hears boots pounding along the floor contour, allowing them to read the pursuer’s route and predict choke points.

2. **Absolute Spatial Fairness (Pillar 2 — Fair Mind-Challenge)**:
   The player feels secure in architectural cover. If a player ducks behind a solid half-wall or into an alcove, they know with 100% confidence that a guard cannot execute a catch unless an unblocked, walkable path physically connects them within `catch_range`. The catch contract is an ironclad physical bond: no catches through glass, no catches through solid metal partitions, and no phantom captures across navmesh-isolated chasms.

3. **Agency Through Spatial Manipulation (Pillar 3 — You Create the Situation)**:
   Because guard pathfinding is deterministic and adheres strictly to the walkable manifold, the player can use tools with geometric confidence. When throwing a Burst noisemaker behind an obstacle, the player anticipates the exact flanking path the guard will navigate to investigate the disturbance, creating the precise window needed to slip past unobserved.

## Detailed Design

### Core Rules

#### C1.1 Walkable Manifold & Baking Configuration
1. **Continuous 2.5D Surface**: The facility traversal space is defined by a pre-baked Unity `NavMeshSurface` component covering static floor geometry.
2. **Zero Off-Mesh Links (MVP)**: In MVP, guards do not jump, vault, climb, or drop across elevation breaks. All off-mesh link generation is strictly disabled (`generateLinks = false`). All traversable geometry must be continuously connected via ramps or level floors with slope $\le 45^\circ$ and step height $\le 0.30\text{ m}$.
3. **Voxel Resolution**: To ensure doorframes, narrow hallways, and modular corners mesh accurately without choking, the NavMesh bake voxel size is locked to $v_{\text{size}} \le 0.133\text{ m}$ ($r_{\text{guard}} / 3$).
4. **Physics Layer Exclusion (ADR-0002)**: The NavMesh bake process collects geometry exclusively from static structural layers (`World`, `Default`). Layers containing dynamic actors or trigger volumes (`Player`, `Guard`, `HideSpot`, `Ignore Raycast`, `Trigger`) are strictly excluded from the bake mask.
5. **Area Definition**: Navigation operates on a single default area: `Walkable` (Area index `0`, Area mask `1`, identifier `navmesh_area_mapping_mvp`). Cost multiplier is locked to `1.0`.

#### C1.2 Agent Physical Footprint & Locomotion Parameters
Every guard GameObject hosts an active `NavMeshAgent` configured to match the registered physical footprint:
- **Radius ($r_{\text{guard}}$)**: $0.40\text{ m}$ (locked, `entities.yaml` entry `r_guard`).
- **Height ($h_{\text{guard}}$)**: $2.00\text{ m}$ (guard capsule height).
- **Stopping Distance (`stopping_distance`)**: $0.00\text{ m}$ (locked, `entities.yaml` entry `stopping_distance`). Guards do not brake early; they traverse until reaching the exact arrival threshold.
- **Max Acceleration ($a_{\text{max}}$)**: $16.0\text{ m/s}^2$ (sharp, responsive pursuit start without sluggish ramp-up).
- **Angular Speed ($\omega_{\text{turn}}$)**: $480^\circ/\text{s}$ (direct, crisp turning along path bends; eliminates slow arcing turns).
- **Steering Control**: `NavMeshAgent` directly updates both world position and yaw rotation (`updatePosition = true`, `updateRotation = true`).
- **Base Speeds**: Synchronized with Guard AI FSM:
  - Patrol: $V_{\text{patrol}} = 2.30\text{ m/s}$ ($[2.0, 2.8]\text{ m/s}$)
  - Investigate: $V_{\text{investigate}} = 5.00\text{ m/s}$ ($[4.5, 5.5]\text{ m/s}$)
  - Chase: $V_{\text{chase}} = 7.50\text{ m/s}$ (starter pinned, $\ge 1.10 \times V_{\text{run}}$)

#### C1.3 Authoritative Path Seam & Distance Metric
1. **Agent-Parameterized Path Authority**: All pathing queries, reachability checks, and catch-gate metrics must invoke the agent instance API:
   ```csharp
   bool success = agent.CalculatePath(targetPosition, navMeshPath);
   ```
   Static `NavMesh.CalculatePath` is strictly forbidden because it omits the agent's radius ($0.40\text{ m}$), step height, and agent-type clearances.
2. **Cumulative Path Length Calculation**: The metric path distance between guard and target is the piecewise Euclidean sum of all straight-line segments along the calculated path polygon:
   $$d_{\text{path}} = \sum_{k=0}^{N-2} \|\vec{p}_{k+1} - \vec{p}_k\|$$
   where $\vec{p}_0, \dots, \vec{p}_{N-1}$ are the ordered vectors in `navMeshPath.corners`. If $N < 2$, $d_{\text{path}} = \infty$.

#### C1.4 Surface Snapping & Target Sampling
1. **Target Projection (`SamplePosition`)**: Before computing a path toward an arbitrary world coordinate (such as a noise event contact, player placement, or hide-spot anchor), the coordinate must be projected onto the NavMesh:
   ```csharp
   bool valid = NavMesh.SamplePosition(sourcePosition, out NavMeshHit hit, navmesh_sample_maxdistance, agent.areaMask);
   ```
2. **Max Distance Bound**: `navmesh_sample_maxdistance` is locked to $0.40\text{ m}$ (`entities.yaml` entry `navmesh_sample_maxdistance`). This exceeds the maximum authorable lip ($0.30\text{ m}$) while remaining strictly less than the minimum non-traversable architectural gap ($0.50\text{ m}$), preventing coordinates from snapping across partitions or chasms.
3. **Off-Mesh Unreachable Policy**: If `SamplePosition` returns `false` (no walkable surface within $0.40\text{ m}$), the target is classified as unreachable. The system publishes `reachable = false` and returns `PathInvalid`.

#### C1.5 State-Dependent Path Execution & Re-Pathing Cadence
1. **Patrol State**:
   - Path is calculated once upon reaching each patrol node toward the next cyclic node index.
   - Agent sets destination: `agent.SetPath(navMeshPath)`.
2. **Investigate State**:
   - Path is calculated once upon state entry toward the sampled noise/visual disturbance anchor.
   - If a subsequent noise event of equal or higher priority arrives during transit, the path is recalculated immediately to the new anchor (per Player Noise re-anchor rules).
3. **Chase State (Live Pursuit)**:
   - To protect the 60 fps (16.6 ms) frame budget and prevent pathfinding thrash, Chase re-pathing is governed by a **dual-trigger cadence**:
     1. **Sensing Tick Alignment**: Re-pathing occurs at the shared Perception/FSM sensing rate ($2\text{--}5\text{ Hz}$, period $T_{\text{repath}} = 0.2\text{--}0.5\text{ s}$).
     2. **Spatial Displacement Trigger**: Immediate re-pathing is forced if the player's Euclidean planar displacement since the last calculated path exceeds $\Delta p_{\text{trigger}} = 0.50\text{ m}$.
     3. Minimum re-pathing interval is clamped to $0.10\text{ s}$ ($10\text{ Hz}$) to prevent framerate hitching during rapid player zig-zagging.

#### C1.6 Path Status Semantics & Give-Up Integration
Every path calculation yields a `NavMeshPathStatus`:
1. **`PathComplete`**:
   - A complete, unobstructed path exists to the destination.
   - Agent moves to destination. Guard AI publishes `reachable = true`.
2. **`PathPartial`**:
   - Path terminates prematurely at an intermediate corner (`corners[last]`) due to a closed door, impassable obstacle, or disconnected boundary.
   - **Investigation Behavior**: Guard travels to `corners[last]`. Upon arriving within `eps_arrive` of `corners[last]`, the FSM start-arm state transitions to `path-end` (Guard AI FSM C1.1), beginning the investigation search timer $t_{\text{search}}$ from the obstruction.
   - **Chase Behavior**: Guard pursues to `corners[last]`. If line-of-sight to the player is broken, the Chase give-up timer ($t_{\text{giveup\_chase}}$) begins counting down while the guard dwells at `corners[last]`.
   - **Catch-Gate Metric**: A partial path cannot satisfy the primary path catch condition ($d_{\text{path}} \le \text{catch\_range}$) because the path does not reach the player. The guard must rely on the F10 backstop (direct LOS + Euclidean cylinder) to execute a catch across a partial path.
3. **`PathInvalid`**:
   - Zero path segments could be generated (e.g. guard or target fully outside the NavMesh).
   - Guard stops, logs diagnostic `PATH_QUERY_FAILED`, and Guard AI FSM initiates an immediate state abort/give-up.

#### C1.7 Arrival Predicates & Catch-Gate Evaluation
1. **Arrival Predicate**:
   Guard arrival at a designated waypoint, noise anchor, or hide spot front is satisfied if and only if:
   $$\text{EuclidXZ}(\vec{p}_{\text{guard}}, \vec{p}_{\text{target}}) \le \text{eps\_arrive} \quad (0.30\text{ m})$$
   evaluated at tick boundaries.
2. **Arrival vs. Path-End Precedence (FSM rev 3.8 lock)**:
   Arrival and path-end are mutually exclusive:
   - If guard is within `eps_arrive` of the sampled target $\implies$ state becomes `arrived`.
   - If guard reaches the end of a `PathPartial` that is $> \text{eps\_arrive}$ from target $\implies$ state becomes `path-end`.
3. **Catch-Gate Path Metric**:
   During active Chase, the catch contract checks:
   $$\text{navMeshPath.status} == \text{PathComplete} \land d_{\text{path}} \le \text{catch\_range} \quad (5.50\text{ m starter})$$
   Accrual pauses if $d_{\text{path}} > \text{catch\_range} + \text{hyst}$ ($6.00\text{ m}$).

#### C1.8 Guard-to-Guard Avoidance & Dynamic Obstacles
1. **Avoidance Mechanism**: Guards use Unity's `NavMeshAgent` reciprocal velocity avoidance (RVO) with `ObstacleAvoidanceType.HighQualityObstacleAvoidance`.
2. **Avoidance Priority**:
   - Chasing guards: Priority `10` (highest; other guards yield).
   - Investigating guards: Priority `30`.
   - Patrolling guards: Priority `50` (yields to alerted guards).
3. **Avoidance Radius**: Avoidance radius is locked to $r_{\text{avoid}} = 0.45\text{ m}$ ($r_{\text{guard}} + 0.05\text{ m}$ cushion), preventing inter-penetration during corridor crossing.
4. **Player Collision**: The player is NOT a `NavMeshObstacle` (carving obstacles on player movement induces costly runtime NavMesh re-bakes and breaks determinism). Player-guard collision is handled entirely via standard PhysX capsule-capsule resolution.

---

### States and Transitions

The pathfinding agent operates through an internal state machine coordinating queries with locomotion:

```text
       [ Idle / Unassigned ]
                 │
                 │ RequestPath(target)
                 ▼
        [ Sampling Target ] ─── SamplePosition fails ───► [ Target Unreachable ]
                 │                                                │
                 │ SamplePosition succeeds                        │ Publish reachable=false
                 ▼                                                ▼
         [ Querying Path ] ─── PathInvalid ─────────────► [ Query Failed ]
                 │
                 ├── PathComplete ──────────────────────┐
                 │                                      │
                 ▼ PathPartial                          ▼
      [ Traversing Partial ]                  [ Traversing Complete ]
                 │                                      │
                 │ DistanceToCorner[last] <= eps_arrive │ DistanceToTarget <= eps_arrive
                 ▼                                      ▼
        [ At Partial End ]                       [ Target Arrived ]
        (Arm 'path-end')                         (Arm 'arrived')
```

- **`Idle`**: Agent has no destination or has completed prior transit. Velocity $= 0$.
- **`SamplingTarget`**: Validating target projection within $0.40\text{ m}$.
- **`QueryingPath`**: Invoking `NavMeshAgent.CalculatePath`.
- **`TraversingComplete`**: Agent actively moving along a verified, unobstructed path toward target.
- **`TraversingPartial`**: Agent moving toward `corners[last]` of an obstructed path.
- **`TargetArrived`**: Guard is within $0.30\text{ m}$ XZ of target. Publishes arrival to FSM.
- **`AtPartialEnd`**: Guard reached the terminal node of a blocked route. Publishes `path-end` to FSM.
- **`TargetUnreachable` / `QueryFailed`**: Query rejected; publishes `reachable = false`.

---

### Interactions with Other Systems

| System | Direction | Interface & Data Contract |
|---|---|---|
| **Guard AI FSM (`#1`)** | Downstream | Consumes `CalculatePath`, path status (`PathComplete`, `PathPartial`, `PathInvalid`), cumulative $d_{\text{path}}$, arrival boolean (`eps_arrive`), and reachability flag. Provides desired speed ($V_{\text{patrol}}$, $V_{\text{investigate}}$, $V_{\text{chase}}$) and target positions. |
| **Perception (`#2`)** | Downstream | Consumes `NavMesh.SamplePosition` with $0.40\text{ m}$ radius for the R3 same-surface snap rule and foot placement projection. Consumes $d_{\text{path}}$ for the catch-gate evaluation. |
| **Player Movement & Hide (`#5`)** | Upstream | Consumes HideSpot anchors (`spot_front_anchor` + guard radius standoff `hold_vector`) and verified interior reachability (`proxy(interior_position)`). |
| **Level / Content (`#8`)** | Upstream | Bakes static geometry into `NavMeshSurface`. Enforces corridor clearance: minimum corridor width $\ge 1.20\text{ m}$ ($\ge 2 \times r_{\text{guard}} + 0.40\text{ m}$ buffer) to permit two guards to pass without deadlock. |
| **Physics & Collision (`#18`)** | Upstream | Adheres to ADR-0002: bake geometry excludes `Player`, `Guard`, `HideSpot`, and trigger layers. Physical capsule-capsule collisions prevent tunneling independently of NavMesh queries. |

## Formulas

### D1. Cumulative Path Length

The `cumulative_path_length` formula computes the continuous piecewise-linear 3D Euclidean distance along the polygonal polyline generated by `NavMeshAgent.CalculatePath`.

The cumulative path length formula is defined as:
$$d_{\text{path}} = \begin{cases} \sum_{k=0}^{N-2} \|\vec{p}_{k+1} - \vec{p}_k\|_2 & \text{if } \text{status} \neq \text{PathInvalid} \land N \ge 2 \\ +\infty & \text{otherwise} \end{cases}$$

where:
$$\|\vec{p}_{k+1} - \vec{p}_k\|_2 = \sqrt{(x_{k+1} - x_k)^2 + (y_{k+1} - y_k)^2 + (z_{k+1} - z_k)^2}$$

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Path Status | `status` | Enum | `{PathComplete, PathPartial, PathInvalid}` | Status of calculated path from `NavMeshPath.status` |
| Corner Count | `N` | Integer | $[0, 64]$ | Length of `NavMeshPath.corners` array |
| Corner Vector | $\vec{p}_k$ | Vector3 | $\mathbb{R}^3$ (m) | 3D coordinate of the $k$-th corner point ($k \in [0, N-1]$) |
| Cumulative Distance | $d_{\text{path}}$ | Float | $[0.00, +\infty)$ (m) | Cumulative path length along all polyline segments |

**Output Range:**
`[0.00, +infinity)` m. Evaluates to $+\infty$ if `status == PathInvalid` or $N < 2$. For valid paths, bounded below by Euclidean straight-line distance: $d_{\text{path}} \ge \|\vec{p}_{N-1} - \vec{p}_0\|_2 \ge 0.00\text{ m}$.

**Worked Example:**
A guard paths around an L-junction pillar to reach the player. The calculated `NavMeshPath` yields $N = 3$ corners:
- $\vec{p}_0 = (0.00, 0.00, 0.00)\text{ m}$ (guard feet)
- $\vec{p}_1 = (3.00, 0.00, 0.00)\text{ m}$ (pillar corner)
- $\vec{p}_2 = (3.00, 0.00, 4.00)\text{ m}$ (player target)
- Segment 1: $\|\vec{p}_1 - \vec{p}_0\| = \sqrt{(3 - 0)^2 + (0 - 0)^2 + (0 - 0)^2} = 3.00\text{ m}$
- Segment 2: $\|\vec{p}_2 - \vec{p}_1\| = \sqrt{(3 - 3)^2 + (0 - 0)^2 + (4 - 0)^2} = 4.00\text{ m}$
- $d_{\text{path}} = 3.00 + 4.00 = 7.00\text{ m}$. (Euclidean straight-line distance is $\sqrt{3^2 + 4^2} = 5.00\text{ m}$; the path metric correctly measures the true $7.00\text{ m}$ navigation detour).

**Degenerate / Boundary Cases:**
- **$N = 0$ or $N = 1$**: Path calculation failed or agent is already co-located at target position without valid polyline segments. Returns $d_{\text{path}} = +\infty$ (unless $N = 1$ and $\|\vec{p}_{\text{guard}} - \vec{p}_{\text{target}}\|_{XZ} \le \text{eps\_arrive}$, where arrival predicate takes precedence).
- **`status == PathInvalid`**: Query failed to find a walkable path. Evaluates strictly to $d_{\text{path}} = +\infty$.
- **`status == PathPartial`**: Path terminates at an intermediate blockage (`corners[N-1]`). The segment sum is finite to `corners[N-1]`, but because the path does not terminate at target, primary catch cannot be satisfied (see D2).

---

### D2. Chase Catch-Gate Path Distance & Hysteresis State

The `catch_gate_state` formula governs the dual-boundary Schmidt trigger evaluating whether a chasing guard satisfies the primary path proximity condition required to execute or maintain catch-meter accrual.

The catch-gate state formula is defined as:
$$S_{\text{catch}}(t) = \begin{cases} \text{ACTIVE} & \text{if } \text{status} == \text{PathComplete} \land d_{\text{path}} \le \text{catch\_range} \\ \text{INACTIVE} & \text{if } \text{status} \neq \text{PathComplete} \lor d_{\text{path}} > \text{catch\_range} + \text{hyst} \\ S_{\text{catch}}(t - \Delta t) & \text{if } \text{status} == \text{PathComplete} \land \text{catch\_range} < d_{\text{path}} \le \text{catch\_range} + \text{hyst} \end{cases}$$

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Path Status | `status` | Enum | `{PathComplete, PathPartial, PathInvalid}` | Query status from `CalculatePath` |
| Cumulative Path Length | $d_{\text{path}}$ | Float | $[0.00, +\infty)$ (m) | Output of formula D1 |
| Catch Range Baseline | `catch_range` | Float | $[5.20, 6.00]$ (m) | Pinned starter $5.50\text{ m}$ (`entities.yaml` entry `catch_range`, floor $5.20\text{ m}$) |
| Hysteresis Buffer | `hyst` | Float | $[0.30, 0.70]$ (m) | Pinned starter $0.50\text{ m}$ (`entities.yaml` entry `hyst`) |
| Prior Catch State | $S_{\text{catch}}(t - \Delta t)$ | Enum | `{ACTIVE, INACTIVE}` | State from previous sensing tick |
| Catch Gate State | $S_{\text{catch}}(t)$ | Enum | `{ACTIVE, INACTIVE}` | Resulting state: `ACTIVE` permits catch meter charging |

**Output Range:**
Discrete state: `{ACTIVE, INACTIVE}`.

**Worked Example:**
With starter parameters $\text{catch\_range} = 5.50\text{ m}$ and $\text{hyst} = 0.50\text{ m}$:
- Engagement Threshold: $d_{\text{engage}} = \text{catch\_range} = 5.50\text{ m}$.
- Disengagement Threshold: $d_{\text{disengage}} = \text{catch\_range} + \text{hyst} = 6.00\text{ m}$.
1. Guard begins at $d_{\text{path}} = 5.80\text{ m}$ with $S_{\text{catch}} = \text{INACTIVE}$. Since $5.80 > 5.50$, state remains $\text{INACTIVE}$.
2. Guard closes to $d_{\text{path}} = 5.40\text{ m} \le 5.50\text{ m}$. Gate transitions to $\text{ACTIVE}$ (meter begins charging).
3. Player sprints, opening gap to $d_{\text{path}} = 5.75\text{ m}$. Because $5.50 < 5.75 \le 6.00$, gate remains $\text{ACTIVE}$ (hysteresis memory prevents thrashing).
4. Player rounds corner, opening gap to $d_{\text{path}} = 6.10\text{ m} > 6.00\text{ m}$. Gate drops to $\text{INACTIVE}$ (meter accrual pauses).

**Degenerate / Boundary Cases:**
- **`status == PathPartial`**: Path does not reach player. $S_{\text{catch}}$ evaluates to `INACTIVE`. (Primary catch-gate fails; catch can only occur via Perception F10 backstop cylinder check if linecast is clear).
- **$d_{\text{path}}$ at exact boundary $5.50\text{ m}$**: Condition $d_{\text{path}} \le \text{catch\_range}$ is met $\implies \text{ACTIVE}$.
- **$d_{\text{path}}$ at exact boundary $6.00\text{ m}$**: Condition $d_{\text{path}} > \text{catch\_range} + \text{hyst}$ is not met $\implies$ retains previous state.

---

### D3. Arrival Predicate & Precedence Function

The `arrival_precedence` formula classifies agent arrival at a destination into mutually exclusive terminal states (`Arrived`, `PathEnd`, or `Traversing`).

The arrival precedence formula is defined as:
$$\text{State}_{\text{arrive}} = \begin{cases} \text{Arrived} & \text{if } \text{EuclidXZ}(\vec{p}_{\text{guard}}, \vec{p}_{\text{target}}) \le \text{eps\_arrive} \\ \text{PathEnd} & \text{if } \text{status} == \text{PathPartial} \land \text{EuclidXZ}(\vec{p}_{\text{guard}}, \vec{p}_{\text{corner\_last}}) \le \text{eps\_arrive} \\ \text{Traversing} & \text{otherwise} \end{cases}$$

where:
$$\text{EuclidXZ}(\vec{a}, \vec{b}) = \sqrt{(a_x - b_x)^2 + (a_z - b_z)^2}$$

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Guard Position | $\vec{p}_{\text{guard}}$ | Vector3 | $\mathbb{R}^3$ (m) | Current world position of guard feet |
| Target Position | $\vec{p}_{\text{target}}$ | Vector3 | $\mathbb{R}^3$ (m) | Projected destination coordinate |
| Last Path Corner | $\vec{p}_{\text{corner\_last}}$ | Vector3 | $\mathbb{R}^3$ (m) | Terminal coordinate of partial path (`corners[N-1]`) |
| Arrival Tolerance | `eps_arrive` | Float | $[0.20, 0.50]$ (m) | Pinned starter $0.30\text{ m}$ (`entities.yaml` entry `eps_arrive`) |
| Path Status | `status` | Enum | `{PathComplete, PathPartial, PathInvalid}` | Status of active path |
| Arrival State | $\text{State}_{\text{arrive}}$ | Enum | `{Arrived, PathEnd, Traversing}` | Mutually exclusive arrival classification |

**Output Range:**
Discrete state: `{Arrived, PathEnd, Traversing}`.

**Worked Example:**
A guard investigates a noise anchor at $\vec{p}_{\text{target}} = (10.00, 0.00, 5.00)\text{ m}$.
- **Scenario A (Full Arrival)**: Guard reaches $\vec{p}_{\text{guard}} = (10.15, 0.00, 5.20)\text{ m}$.
  $\text{EuclidXZ} = \sqrt{(10.15 - 10.00)^2 + (5.20 - 5.00)^2} = \sqrt{0.0225 + 0.0400} = \sqrt{0.0625} = 0.25\text{ m}$.
  Since $0.25\text{ m} \le 0.30\text{ m}$, state is `Arrived`.
- **Scenario B (Obstruction / Path-End)**: Door is locked. Path status is `PathPartial` terminating at $\vec{p}_{\text{corner\_last}} = (8.00, 0.00, 5.00)\text{ m}$.
  Guard position is $(8.10, 0.00, 5.10)\text{ m}$.
  Distance to target: $\text{EuclidXZ}(\vec{p}_{\text{guard}}, \vec{p}_{\text{target}}) = 1.903\text{ m} > 0.30\text{ m}$.
  Distance to last corner: $\text{EuclidXZ}(\vec{p}_{\text{guard}}, \vec{p}_{\text{corner\_last}}) = 0.141\text{ m} \le 0.30\text{ m}$.
  Evaluates to `PathEnd` (triggers search timer from obstruction, per FSM C1.1).

**Degenerate / Boundary Cases:**
- **Precedence Lock**: If $\vec{p}_{\text{corner\_last}}$ happens to be within $\text{eps\_arrive}$ of $\vec{p}_{\text{target}}$, the evaluation order strictly resolves to `Arrived`, never `PathEnd` (Guard AI FSM rev 3.8 lock).
- **Vertical Displacement**: Elevation ($\Delta Y$) is excluded from $\text{EuclidXZ}$; vertical step compatibility is enforced by NavMesh slope $\le 45^\circ$ and step height $\le 0.30\text{ m}$.

---

### D4. Chase Re-Pathing Dual-Trigger Predicate

The `chase_repath_trigger` formula governs the conditional evaluation on each frame tick to determine whether an expensive NavMesh query must be issued during active pursuit.

The chase re-pathing trigger formula is defined as:
$$\text{Trigger}_{\text{repath}} = (t_{\text{since\_last\_path}} \ge t_{\text{repath\_min}}) \land \Big( (t_{\text{since\_last\_path}} \ge T_{\text{repath}}) \lor \big(\text{EuclidXZ}(\vec{p}_{\text{player}}, \vec{p}_{\text{player\_last\_path}}) \ge \Delta p_{\text{trigger}}\big) \Big)$$

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Elapsed Time | $t_{\text{since\_last\_path}}$ | Float | $[0.00, +\infty)$ (s) | Continuous timer since last query completion |
| Min Re-path Interval | $t_{\text{repath\_min}}$ | Float | $[0.05, 0.15]$ (s) | Clamped rate floor: pinned $0.10\text{ s}$ ($10\text{ Hz}$ cap) |
| Cadence Period | $T_{\text{repath}}$ | Float | $[0.20, 0.50]$ (s) | Perception/FSM sensing tick interval ($2\text{--}5\text{ Hz}$) |
| Player Current Pos | $\vec{p}_{\text{player}}$ | Vector3 | $\mathbb{R}^3$ (m) | Current world position of player feet |
| Player Sampled Pos | $\vec{p}_{\text{player\_last\_path}}$ | Vector3 | $\mathbb{R}^3$ (m) | Player position recorded when last path was calculated |
| Displacement Trigger | $\Delta p_{\text{trigger}}$ | Float | $[0.30, 0.80]$ (m) | Pinned starter $0.50\text{ m}$ |
| Re-path Flag | $\text{Trigger}_{\text{repath}}$ | Boolean | `{TRUE, FALSE}` | When `TRUE`, agent calls `CalculatePath` |

**Output Range:**
Boolean: `{TRUE, FALSE}`.

**Worked Example:**
Parameters: $t_{\text{repath\_min}} = 0.10\text{ s}$, $T_{\text{repath}} = 0.25\text{ s}$, $\Delta p_{\text{trigger}} = 0.50\text{ m}$.
- **Case 1 (Player Sprinting / Cutting)**: At $t = 0.12\text{ s}$, player cuts laterally, moving $0.55\text{ m}$ from last recorded path point.
  $t_{\text{since\_last\_path}} = 0.12 \ge 0.10\text{ s}$ (passes floor).
  $\text{EuclidXZ} = 0.55 \ge 0.50\text{ m}$ (passes displacement trigger).
  Result: `TRUE` $\implies$ Query executed immediately.
- **Case 2 (Clamp Defense)**: At $t = 0.05\text{ s}$, player moves $0.60\text{ m}$.
  $t_{\text{since\_last\_path}} = 0.05 < 0.10\text{ s}$.
  Result: `FALSE` $\implies$ Query deferred until minimum interval passes, protecting 16.6 ms frame budget.
- **Case 3 (Idle / Slow Target)**: Player creeps slowly ($\Delta p = 0.10\text{ m}$). At $t = 0.25\text{ s} = T_{\text{repath}}$, cadence period elapses.
  Result: `TRUE` $\implies$ Path updates periodically to track drift.

**Degenerate / Boundary Cases:**
- **Zero Movement**: Player stands completely still. Re-pathing occurs exactly every $T_{\text{repath}}$ ($0.25\text{ s}$), ensuring the guard adjusts to any dynamic local obstacle changes.
- **Framerate Spikes**: If framerate drops to 15 fps ($\Delta t = 0.066\text{ s}$), the timer accumulates correctly across frames without skipped queries.

---

### D5. NavMesh Bake Resolution Invariant

The `navmesh_bake_voxel_invariant` formula defines the mandatory compilation ceiling for NavMesh voxel resolution, guaranteeing that agent capsule geometry clears all authored passageways without topological disconnections.

The NavMesh bake voxel invariant formula is defined as:
$$v_{\text{size}} \le \frac{r_{\text{guard}}}{3}$$

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Bake Voxel Size | $v_{\text{size}}$ | Float | $(0.00, 0.20]$ (m) | Size of rasterized voxel cell in `NavMeshSurface` bake settings |
| Guard Radius | $r_{\text{guard}}$ | Float | Locked $0.40$ (m) | Physical radius of guard (`entities.yaml` entry `r_guard`) |
| Validation Output | `navmesh_bake_voxel_invariant` | Boolean | `{PASS, FAIL}` | Build verification gate |

**Output Range:**
Boolean in `{PASS, FAIL}`. At starter values: $v_{\text{size}} \le 0.1333\text{ m}$.

**Worked Example:**
- Guard radius $r_{\text{guard}} = 0.40\text{ m}$.
- Required ceiling: $v_{\text{size\_max}} = 0.40 / 3 = 0.1333\text{ m}$.
- Level Designer authors $v_{\text{size}} = 0.100\text{ m}$:
  $0.100 \le 0.1333 \implies \mathbf{PASS}$.
- Level Designer attempts coarse setting $v_{\text{size}} = 0.200\text{ m}$:
  $0.200 > 0.1333 \implies \mathbf{FAIL}$ (triggers build-time bake assertion `ERR_BAKE_VOXEL_TOO_COARSE`).

**Degenerate / Boundary Cases:**
- $v_{\text{size}} \le 0$: Physically invalid. Guarded by build script validation $v_{\text{size}} \ge 0.02\text{ m}$ to prevent memory exhaustion during voxel rasterization.

---

### D6. Corridor Width Clearance Invariant

The `corridor_width_invariant` formula defines the minimum architectural corridor width permitted in Level Certification (`#8`) to guarantee unjammed two-way passing and clean RVO obstacle avoidance.

The corridor width invariant formula is defined as:
$$W_{\text{corridor}} \ge 2 \times r_{\text{guard}} + \text{margin}_{\text{corridor}}$$

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Corridor Width | $W_{\text{corridor}}$ | Float | $[1.20, +\infty)$ (m) | Clear horizontal width between static wall colliders |
| Guard Radius | $r_{\text{guard}}$ | Float | Locked $0.40$ (m) | Guard physical radius (`entities.yaml` entry `r_guard`) |
| Clearance Margin | $\text{margin}_{\text{corridor}}$ | Float | $[0.20, 0.60]$ (m) | Safety buffer for RVO steering cushion; starter $0.40\text{ m}$ |
| Validation Output | `corridor_width_invariant` | Boolean | `{PASS, FAIL}` | Level geometry certification gate |

**Output Range:**
Boolean in `{PASS, FAIL}`. Absolute structural floor is $W_{\text{corridor}} \ge 1.20\text{ m}$.

**Worked Example:**
- $r_{\text{guard}} = 0.40\text{ m}$, $\text{margin}_{\text{corridor}} = 0.40\text{ m}$.
- $W_{\text{corridor\_min}} = 2 \times 0.40 + 0.40 = 1.20\text{ m}$.
- A standard facility hallway is modeled at $W_{\text{corridor}} = 1.50\text{ m}$:
  $1.50 \ge 1.20 \implies \mathbf{PASS}$.
- A maintenance duct is modeled at $W_{\text{corridor}} = 1.00\text{ m}$:
  $1.00 < 1.20 \implies \mathbf{FAIL}$ (triggers Level Certification error `ERR_CORRIDOR_CHOKE_POINT`).

**Degenerate / Boundary Cases:**
- **Single-direction patrol**: Even in single-guard patrol routes, $W_{\text{corridor}} \ge 1.20\text{ m}$ is strictly enforced so that a chasing guard can bypass a fleeing player without PhysX capsule snagging.

## Edge Cases

- **If a target coordinate lies near a thin partition ($\le 0.40\text{ m}$) where `SamplePosition` could snap to the opposite side**: verify the projection with `Physics.Linecast(sourcePosition, hit.position, WorldLayerMask, QueryTriggerInteraction.Ignore)`. If the linecast hits an intervening static collider, the projection is rejected as invalid (`reachable = false`), preventing guards from pathing to the wrong side of walls.
- **If a noise event or target is elevated on non-walkable geometry (crates, furniture, or ducts > 0.40 m above floor)**: `NavMesh.SamplePosition` returns `false` due to the strict $0.40\text{ m}$ search radius constraint. The target is declared unreachable (`reachable = false`), and the guard does not path toward the ceiling or float.
- **If a target is placed in a completely disconnected or sealed room**: `NavMeshAgent.CalculatePath` returns `PathInvalid` (or `PathPartial` terminating at the boundary wall). The system returns $d_{\text{path}} = \infty$, the guard cannot satisfy catch-gate conditions, and Guard AI FSM triggers give-up/abort without hanging.
- **If a path calculation yields `PathPartial` during active Chase**: the guard pursues along the partial path to `corners[last]` and halts. If direct LOS to the player is clear and the player is within the 2.5D cylinder ($\text{EuclidXZ} \le \text{catch\_range} \land |\Delta Y| \le 1.0\text{ m}$), the catch gate engages via the F10 backstop. If LOS is broken, the Chase give-up timer ($t_{\text{giveup\_chase}}$) begins counting down from `corners[last]`.
- **If a path calculation yields `PathPartial` during Investigation**: the guard navigates to `corners[last]`. Upon reaching within $\text{eps\_arrive} = 0.30\text{ m}$ of `corners[last]`, the FSM start-arm state transitions to `path-end` (Guard AI FSM C1.1), beginning the look-around sweep and search timer from the obstruction.
- **If a dynamic physics collision or kinematic push forces a guard off the NavMesh**: `agent.isOnNavMesh` becomes `false`. The system queries `NavMesh.SamplePosition(guard.position, out hit, 1.0f, agent.areaMask)`. If a valid surface is found, it calls `agent.Warp(hit.position)` to snap the guard back cleanly. If no surface exists within $1.0\text{ m}$, the system logs fatal error `ERR_GUARD_FALLEN_OFF_NAVMESH` and disables the agent.
- **If a guard is spawned off the NavMesh at scene initialization**: during the start-up validation pass, `NavMeshAgent.Warp` is invoked to snap the guard to the nearest NavMesh point within $1.0\text{ m}$. If the distance exceeds $1.0\text{ m}$, level load fails validation with `ERR_SPAWN_OFF_NAVMESH`.
- **If two guards encounter each other head-on in a minimum-clearance corridor ($1.20\text{ m}$)**: reciprocal velocity avoidance (RVO) evaluates avoidance priority: Chasing (`10`) > Investigating (`30`) > Patrolling (`50`). The lower-priority guard yields and steers to the corridor wall, while the higher-priority guard proceeds along the centerline. If both share equal priority, RVO angular perturbation breaks symmetry.
- **If the target coordinate is co-located with the guard's current position ($\text{EuclidXZ} \le \text{eps\_arrive}$)**: `CalculatePath` yields 1 corner. The arrival predicate evaluates to `Arrived` immediately on the same tick without issuing locomotion commands or path segment summation.
- **If the player enters a HideSpot during an active Chase**: Guard AI switches `GuardGoalMode` to `HideSpotApproach` and paths to `guard_hold` ($\text{spot\_front\_anchor} + r_{\text{guard}} \times \text{hold\_vector}$). Reachability into the spot is evaluated against `proxy(interior_position)` per Player Movement & Hide GDD #5 §D1, not raw prop centroid.
- **If a doorway or corridor is dynamically sealed while an agent is mid-transit**: the next scheduled re-path query detects the blockage, transitioning the active path from `PathComplete` to `PathPartial`. The agent decelerates cleanly to the new blockage point rather than attempting to penetrate the closed collider.
- **If target or guard is located on an inclined ramp (slope $\le 45^\circ$)**: $\text{EuclidXZ}$ planar projection handles arrival tolerance while the `NavMeshAgent` automatically adjusts vertical height and orientation along the ramp surface without vertical jitter.

## Dependencies

### Upstream Dependencies (What NavMesh Consumes)

| System | Type | What NavMesh Consumes | Status | Contract & Boundary Notes |
|---|---|---|---|---|
| **Physics & Collision Config (`#18`)** | Hard | Static collider geometry on `World` layer, layer masks, and Linecast query API. | **Approved** (`docs/architecture/adr-0002-physics-collision-contract.md`) | NavMesh bake geometry collects only static structural layers (`World`). Dynamic actors (`Player`, `Guard`, `HideSpot`, `Trigger`) are strictly excluded. Linecast verification for thin-wall projection uses `QueryTriggerInteraction.Ignore`. |
| **Level / Content (`#8`)** | Hard | Pre-baked `NavMeshSurface` data, static room architecture, doorways, ramps, and corridor clearance. | Undesigned (Pre-MVP) | Level geometry must satisfy $W_{\text{corridor}} \ge 1.20\text{ m}$, max slope $\le 45^\circ$, max step $\le 0.30\text{ m}$, and zero off-mesh links. |
| **Player Movement & Hide (`#5`)** | Hard | HideSpot spatial anchors (`spot_front_anchor`, `hold_vector`, `proxy(interior_position)`). | **Approved** (`design/gdd/player-movement-hide.md`, 2026-09-20) | Guards navigate to `guard_hold` ($\text{spot\_front\_anchor} + r_{\text{guard}} \times \text{hold\_vector}$) and verify reachability against `proxy(interior_position)`, never the raw prop centroid. |
| **Scene / Asset Management (`#17`)** | Soft | Additive scene loading and pre-baked `NavMeshData` instance registration. | Undesigned | Each facility segment/room registers its pre-baked NavMesh surface upon additive scene load. |

---

### Downstream Dependents (What Consumes NavMesh)

| System | Type | What It Consumes From NavMesh | Status | Contract & Boundary Notes |
|---|---|---|---|---|
| **Guard AI FSM (`#1`)** | Hard | `NavMeshAgent.CalculatePath`, cumulative $d_{\text{path}}$, path status (`PathComplete`, `PathPartial`, `PathInvalid`), arrival predicate (`eps_arrive = 0.30 m`), reachability flag, and avoidance priority. | **Approved** (`design/gdd/guard-ai-fsm.md`, rev 4.1) | FSM controls locomotion by setting agent velocity, destination, and stopping. Give-up clocks evaluate path-end vs arrival per rev 3.8 precedence. |
| **Perception (`#2`)** | Hard | `NavMesh.SamplePosition` ($0.40\text{ m}$ radius) for R3 same-surface foot snap; $d_{\text{path}}$ for F10 catch-gate path condition. | **Approved** (`design/gdd/perception.md`, rev 3.4) | Perception uses NavMesh to ground player and guard feet, ensuring LOS checks and distance falloff operate on physically valid surfaces. |
| **Player Third-Person Controller (`#11`)** | Soft | Player movement speed bounds guard chase velocity: $V_{\text{chase}} \ge 1.10 \times V_{\text{run}}$. | **Approved-terminal** (`design/gdd/player-third-person-controller.md`, 2026-08-26) | Player run speed ($V_{\text{run}} = 6.25\text{ m/s}$) sets the kinematic baseline that NavMeshAgent chase speed ($7.50\text{ m/s}$) must exceed to prevent perpetual kiting. |
| **Suspicion Meter / Grade Operator (`#7`)** | Indirect | Incident records and chase escalations resulting from path-arrival. | **Approved** (`design/gdd/suspicion-meter-grade.md`, 2026-09-20) | Catch-gate closure over NavMesh triggers the Terminal Capture event, concluding grading with $S_{\text{room}} = 0.0\text{ pts}$. |

---

### Bidirectional Contract Audit
- **Guard AI FSM `#1` §Dependencies**: Explicitly lists NavMesh (order 7) for `NavMesh.SamplePosition`, `NavMeshAgent.CalculatePath`, arrival `eps_arrive = 0.3 m`, and partial-path stand-off. *(Satisfied & Bidirectional)*
- **Perception `#2` §Dependencies**: Explicitly lists NavMesh for R3 same-surface snap ($0.40\text{ m}$) and catch-gate path distance $d$. *(Satisfied & Bidirectional)*
- **Player Movement & Hide `#5` §Dependencies**: Explicitly lists NavMesh for guard reachability to `proxy(interior_position)`. *(Satisfied & Bidirectional)*

## Tuning Knobs

### System-Owned Tunable Knobs

| Knob | Starter Value | Safe Range | Unit | Source / Formula | Gameplay Impact |
|---|---|---|---|---|---|
| `eps_arrive` | `0.30` | `[0.20, 0.50]` | m | `entities.yaml` (`eps_arrive`), D3 | Arrival threshold for waypoints, noise anchors, and spot fronts. Smaller values increase positional precision but risk stuttering if agent deceleration overshoots; larger values trigger arrival prematurely. |
| `t_repath_min` | `0.10` | `[0.05, 0.15]` | s | D4 | Hard rate-limiting clamp on Chase re-pathing. Protects the 60 fps (16.6 ms) frame budget against high-frequency player movement. |
| `T_repath` | `0.25` | `[0.20, 0.50]` | s | D4 | Periodic Chase re-pathing cadence. Synchronized with the Perception sensing tick (2–5 Hz). Lower values make pursuit tracking tighter; higher values save CPU time. |
| `Delta p_trigger` | `0.50` | `[0.30, 0.80]` | m | D4 | Player Euclidean displacement threshold triggering an immediate Chase re-path. Balances pursuit responsiveness against query frequency when player sprints or jukes. |
| `omega_turn` | `480.0` | `[360.0, 720.0]` | deg/s | C1.2 | NavMeshAgent angular turning speed. Governs how crisply guards pivot around corners. Lower values produce wide sweeping turns; higher values produce robotic snap-turns. |
| `a_max` | `16.0` | `[12.0, 24.0]` | m/s² | C1.2 | NavMeshAgent linear acceleration. Higher values give immediate speed responsiveness upon entering Chase; lower values create a noticeable acceleration lag. |
| `v_size` | `0.10` | `[0.05, 0.133]` | m | D5 (`navmesh_bake_voxel_invariant`) | Voxel size for `NavMeshSurface` bake. Smaller values improve mesh fidelity around doorframes but increase bake time and memory; must remain $\le 0.133\text{ m}$ ($r_{\text{guard}} / 3$). |
| `margin_corridor` | `0.40` | `[0.20, 0.60]` | m | D6 (`corridor_width_invariant`) | Extra horizontal corridor clearance added to $2 \times r_{\text{guard}}$. Sized to guarantee collision-free two-guard RVO passing. |
| `r_avoid` | `0.45` | `[0.42, 0.55]` | m | C1.8 | High-quality RVO obstacle avoidance radius. Set slightly above physical radius ($0.40\text{ m}$) to prevent physical capsule snagging between converging guards. |

---

### Invariant & Architecture-Locked Values (Read-Only)

These parameters are architecturally pinned across the project. Modifying them violates verified cross-system contracts:

| Parameter | Pinned Value | Unit | Authority | Rationale for Lock |
|---|---|---|---|---|
| `r_guard` | `0.40` | m | `entities.yaml` (`r_guard`), ADR-0002 | Guard physical capsule radius. Hardcoded into bake resolution ($v_{\text{size}}$) and corridor clearance ($W_{\text{corridor}}$). |
| `stopping_distance` | `0.00` | m | `entities.yaml` (`stopping_distance`), F10 | `NavMeshAgent` stopping distance. Pinned to zero so the agent navigates directly to the surface boundary without artificial offsets. |
| `navmesh_sample_maxdistance` | `0.40` | m | `entities.yaml` (`navmesh_sample_maxdistance`), Perception R3 | `NavMesh.SamplePosition` search radius. Strictly pinned to $[0.30\text{ m}, 0.50\text{ m}]$: exceeds max authorable lip ($0.30\text{ m}$) while less than non-walkable gap ($0.50\text{ m}$). |
| `navmesh_area_mask` | `Walkable (1)` | bitmask | `navmesh_area_mapping_mvp` | Single-area 2.5D manifold for MVP. Cost is locked to `1.0`. |
| `off_mesh_links` | `Disabled (0)` | boolean | C1.1, MVP Scope | Zero off-mesh links in MVP. Guards never jump, climb, or drop across disjoint meshes. |

---

### Consumed External Constants

| Parameter | Value | System Owner | Usage in NavMesh |
|---|---|---|---|
| `V_patrol` | `2.30 m/s` ($[2.0, 2.8]$) | Guard AI FSM (`#1`) | Sets `agent.speed` during Patrol state. |
| `V_investigate` | `5.00 m/s` ($[4.5, 5.5]$) | Guard AI FSM (`#1`) | Sets `agent.speed` during Investigate state. |
| `V_chase` | `7.50 m/s` (starter, $\ge 1.10 \times V_{\text{run}}$) | Guard AI FSM (`#1`) / Player Controller (`#11`) | Sets `agent.speed` during Chase state. |
| `catch_range` | `5.50 m` (starter, floor $5.20\text{ m}$) | Perception (`#2`) / Guard AI FSM (`#1`) | Distance threshold for catch-gate path condition (D2). |
| `hyst` | `0.50 m` ($[0.30, 0.70]$) | Guard AI FSM (`#1`) | Catch-gate hysteresis pause threshold (D2). |

## Visual/Audio Requirements

### Visual Requirements

1. **Player-Facing Runtime Invisibility**:
   - The Navigation Mesh, agent paths, target waypoints, and corners are 100% invisible during normal gameplay. There are no player-visible projected navigation paths, floor decals, or breadcrumbs.
2. **Editor & Development Debug Visualizer (`NavMeshDebugGizmo`)**:
   - In Editor mode and development builds (toggled via debug hotkey `F3`), an authoritative debug overlay renders:
     - **Active Path Polyline**: Rendered in yellow (`Patrol`), amber (`Investigate`), or red (`Chase`) via `Gizmos.DrawLine` connecting `navMeshPath.corners`.
     - **Path Corners**: Small solid spheres ($r = 0.08\text{ m}$) at each intermediate node $\vec{p}_k$.
     - **Sampled Destination Anchor**: Cyan wireframe disc ($r = 0.40\text{ m}$) indicating the `SamplePosition` target point.
     - **Arrival Boundary**: Green wireframe cylinder of radius $\text{eps\_arrive} = 0.30\text{ m}$ centered at the active destination.
     - **Catch-Range Boundary (Chase Only)**: Red wireframe cylinder of radius $\text{catch\_range} = 5.50\text{ m}$ and height $1.0\text{ m}$ around the chasing guard, with an outer dotted cylinder at $\text{catch\_range} + \text{hyst} = 6.00\text{ m}$.
     - **Avoidance Radius**: Grey wireframe circle of radius $r_{\text{avoid}} = 0.45\text{ m}$ at guard feet.

### Audio Requirements

1. **Path-Synchronized Footstep Cadence**:
   - Guard locomotion audio events (`guard_step`) are driven directly by the `NavMeshAgent`'s physical linear speed ($V_{\text{patrol}}$, $V_{\text{investigate}}$, $V_{\text{chase}}$).
   - Footstep trigger intervals scale inversely with speed:
     $$\Delta t_{\text{step}} = \frac{L_{\text{stride}}}{v_{\text{current}}}$$
     where nominal stride length $L_{\text{stride}} = 0.90\text{ m}$. At $V_{\text{patrol}} = 2.30\text{ m/s} \implies \Delta t \approx 0.39\text{ s}$; at $V_{\text{chase}} = 7.50\text{ m/s} \implies \Delta t \approx 0.12\text{ s}$.
2. **Acoustic Cornering & Occlusion Fidelity**:
   - Guard footstep sound emitters are located at the guard's world position (`guard_feet`). Because the guard strictly traces the NavMesh path around corners, footstep sounds naturally propagate from around corners rather than beaming straight through solid walls.

## UI Requirements

### Player-Facing HUD
1. **Zero Player-Facing UI**:
   - The NavMesh / Pathfinding system renders **no direct HUD widgets, minimaps, or UI overlays** on the player screen.
   - All player-facing awareness of guard pursuit is communicated indirectly through diegetic world audio (footsteps) and the existing Suspicion Meter / Directional Threat Chevron (GDD #7), which points toward the dominant guard's world position.

### Developer & Diagnostics Overlay (`NavMeshDebugOverlay`)
In development and test builds, an in-game telemetry panel (toggleable via `F3` or developer console) exposes real-time navigation metrics for the selected or nearest guard:

| Diagnostic Field | Format | Description |
|---|---|---|
| `guard_eid` | `string` | Unique entity identifier of the inspected guard. |
| `path_status` | `enum` | `{PathComplete, PathPartial, PathInvalid}`. Highlighted red if `PathInvalid`, amber if `PathPartial`. |
| `path_length` | `float (0.00 m)` | Current cumulative distance $d_{\text{path}}$ to active target. |
| `corner_count` | `int` | Current number of polyline corners ($N$). |
| `repath_timer` | `float (0.00 s)` | Time elapsed since last path calculation ($t_{\text{since\_last\_path}}$). |
| `target_displacement` | `float (0.00 m)` | Euclidean distance player has moved since last path calculation. |
| `catch_gate_state` | `enum` | `{ACTIVE, INACTIVE}` showing D2 hysteresis state. |
| `arrival_state` | `enum` | `{Arrived, PathEnd, Traversing}` per D3 precedence. |
| `is_on_navmesh` | `bool` | True if agent is currently grounded on the NavMesh surface; flashes red if false. |
| `diagnostic_code` | `string` | Diagnostic status: `OK`, `ERR_TARGET_UNREACHABLE`, `ERR_PATH_QUERY_FAILED`, `ERR_GUARD_FALLEN_OFF_NAVMESH`. |

## Acceptance Criteria

### Scope 1: Unit Tests (Logic & Mathematical Formulas)

#### AC-NAV-01: Cumulative Path Length Calculation (Formula D1)
- **Given**: A test polyline fixture representing an L-shaped path with $N = 3$ corners:
  $\vec{p}_0 = (0.00, 0.00, 0.00)\text{ m}$, $\vec{p}_1 = (3.00, 0.00, 0.00)\text{ m}$, and $\vec{p}_2 = (3.00, 0.00, 4.00)\text{ m}$, with status `PathComplete`.
- **When**: `NavMeshPathMetric.CalculateCumulativeLength(navMeshPath)` is executed.
- **Then**: Returned cumulative distance $d_{\text{path}}$ equals exactly $7.000\text{ m} \pm 0.001\text{ m}$.
- **Failure diagnostic**: `"FAIL [AC-NAV-01]: Cumulative path length expected 7.000m, but evaluated to " + d_path + "m."`

#### AC-NAV-02: Path Length Degenerate / Invalid Handling (Formula D1 Degenerates)
- **Given**: Two separate degenerate path fixtures:
  1. Fixture A: `status == PathInvalid`.
  2. Fixture B: `status == PathComplete` with $N = 0$ corners.
- **When**: `CalculateCumulativeLength` is invoked for both fixtures.
- **Then**: Both invocations return `float.PositiveInfinity`.
- **Failure diagnostic**: `"FAIL [AC-NAV-02]: Degenerate path did not return PositiveInfinity (Fixture A: " + lenA + ", Fixture B: " + lenB + ")."`

#### AC-NAV-03: Catch-Gate Engagement Threshold & Hysteresis State (Formula D2)
- **Given**: A chasing guard with baseline `catch_range = 5.50 m`, `hyst = 0.50 m`, and initial gate state $S_{\text{catch}}(t - \Delta t) = \text{INACTIVE}$.
- **When**: Path distance $d_{\text{path}}$ transitions through the following consecutive sensing ticks with `PathComplete`:
  - Tick 1: $d_{\text{path}} = 5.80\text{ m}$
  - Tick 2: $d_{\text{path}} = 5.40\text{ m}$
  - Tick 3: $d_{\text{path}} = 5.75\text{ m}$
  - Tick 4: $d_{\text{path}} = 6.01\text{ m}$
- **Then**: Evaluated $S_{\text{catch}}$ sequence is strictly:
  - Tick 1: `INACTIVE`
  - Tick 2: `ACTIVE`
  - Tick 3: `ACTIVE` (held by hysteresis memory since $5.75 \le 6.00$)
  - Tick 4: `INACTIVE` (dropped because $6.01 > 6.00$)
- **Failure diagnostic**: `"FAIL [AC-NAV-03]: Catch-gate hysteresis mismatch at Tick " + tick + ". Expected " + expectedState + ", got " + actualState + " with d_path=" + d_path + "m."`

#### AC-NAV-04: Catch-Gate PathPartial Immediate Invalidation (Formula D2 Boundary)
- **Given**: A chasing guard with current state $S_{\text{catch}} = \text{ACTIVE}$ and $d_{\text{path}} = 3.00\text{ m}$.
- **When**: A doorway closes or path becomes blocked, causing the next query to return `status == PathPartial` with remaining distance to last corner $d = 2.00\text{ m}$.
- **Then**: $S_{\text{catch}}$ transitions immediately to `INACTIVE` on the exact tick the path status changed, regardless of Euclidean distance.
- **Failure diagnostic**: `"FAIL [AC-NAV-04]: Catch-gate remained ACTIVE during PathPartial status."`

#### AC-NAV-05: Arrival vs. Path-End Mutually Exclusive Precedence (Formula D3)
- **Given**: An active agent with `eps_arrive = 0.30 m` testing destination $\vec{p}_{\text{target}} = (10.00, 0.00, 0.00)\text{ m}$.
- **When**: Three discrete spatial configurations are evaluated:
  - Case A (True Arrival): Agent at $(9.80, 0.00, 0.00)\text{ m}$ ($\text{EuclidXZ} = 0.20\text{ m} \le 0.30\text{ m}$), path status `PathComplete`.
  - Case B (Obstruction Path-End): Agent at $(5.10, 0.00, 0.00)\text{ m}$, path status `PathPartial` terminating at $\vec{p}_{\text{corner\_last}} = (5.00, 0.00, 0.00)\text{ m}$ ($\text{EuclidXZ}(\text{guard}, \text{corner\_last}) = 0.10\text{ m} \le 0.30\text{ m}$, $\text{EuclidXZ}(\text{guard}, \text{target}) = 4.90\text{ m} > 0.30\text{ m}$).
  - Case C (Precedence Overlap Lock): Agent at $(9.85, 0.00, 0.00)\text{ m}$, path status `PathPartial` terminating at $\vec{p}_{\text{corner\_last}} = (9.90, 0.00, 0.00)\text{ m}$ (both target and corner within $0.30\text{ m}$).
- **Then**: Evaluated `State_arrive` resolves to:
  - Case A: `Arrived`
  - Case B: `PathEnd`
  - Case C: `Arrived` (Precedence lock: `Arrived` strictly overrides `PathEnd`).
- **Failure diagnostic**: `"FAIL [AC-NAV-05]: Arrival precedence violated in Case " + caseId + ". Expected " + expectedState + ", but got " + actualState + "."`

#### AC-NAV-06: Chase Re-Pathing Dual-Trigger Predicate (Formula D4)
- **Given**: Pursuit configuration $t_{\text{repath\_min}} = 0.10\text{ s}$, $T_{\text{repath}} = 0.25\text{ s}$, and $\Delta p_{\text{trigger}} = 0.50\text{ m}$.
- **When**: Evaluated across four test stimuli:
  - Subcase 1: $t_{\text{since}} = 0.05\text{ s}$, $\Delta p = 0.80\text{ m}$.
  - Subcase 2: $t_{\text{since}} = 0.12\text{ s}$, $\Delta p = 0.55\text{ m}$.
  - Subcase 3: $t_{\text{since}} = 0.15\text{ s}$, $\Delta p = 0.20\text{ m}$.
  - Subcase 4: $t_{\text{since}} = 0.26\text{ s}$, $\Delta p = 0.05\text{ m}$.
- **Then**: `Trigger_repath` evaluates to:
  - Subcase 1: `FALSE` (rate floor clamp blocked re-path).
  - Subcase 2: `TRUE` (displacement trigger fired after rate floor).
  - Subcase 3: `FALSE` (neither displacement nor cadence period reached).
  - Subcase 4: `TRUE` (cadence period reached).
- **Failure diagnostic**: `"FAIL [AC-NAV-06]: Re-path trigger predicate failure in Subcase " + subcaseId + ". Expected " + expectedBool + ", got " + actualBool + "."`

#### AC-NAV-07: NavMesh Voxel Resolution Invariant (Formula D5)
- **Given**: Guard radius $r_{\text{guard}} = 0.40\text{ m}$.
- **When**: A `NavMeshSurface` bake configuration is validated against `v_size_max = r_guard / 3 = 0.1333 m`.
- **Then**:
  - A configuration with $v_{\text{size}} = 0.100\text{ m}$ passes validation (`PASS`).
  - A configuration with $v_{\text{size}} = 0.150\text{ m}$ fails validation (`FAIL`), returning error code `ERR_BAKE_VOXEL_TOO_COARSE`.
- **Failure diagnostic**: `"FAIL [AC-NAV-07]: Voxel invariant validation failed. Resolution " + v_size + "m must not exceed 0.1333m."`

#### AC-NAV-08: Corridor Width Invariant Gate (Formula D6)
- **Given**: Standard guard radius $r_{\text{guard}} = 0.40\text{ m}$ and safety margin $\text{margin}_{\text{corridor}} = 0.40\text{ m}$ ($W_{\text{corridor\_min}} = 1.20\text{ m}$).
- **When**: Automated level geometry scanner checks two modeled test corridors:
  - Corridor A: Clear width between static colliders $W = 1.25\text{ m}$.
  - Corridor B: Clear width between static colliders $W = 1.10\text{ m}$.
- **Then**:
  - Corridor A returns `PASS`.
  - Corridor B returns `FAIL` with diagnostic code `ERR_CORRIDOR_CHOKE_POINT`.
- **Failure diagnostic**: `"FAIL [AC-NAV-08]: Corridor width " + width + "m violated minimum clearance floor of 1.20m."`

---

### Scope 2: Integration & Locomotion Tests

#### AC-NAV-09: Thin-Wall Projection Linecast Rejection
- **Given**: A static thin wall partition ($0.15\text{ m}$ thickness) on the `World` layer separating Room 1 and Room 2. A target noise coordinate is placed in Room 1 at $(0.00, 0.00, 0.00)\text{ m}$.
- **When**: `NavMesh.SamplePosition` with `navmesh_sample_maxdistance = 0.40 m` finds a surface projection point $(0.00, 0.00, 0.20)\text{ m}$ on the Room 2 side of the wall.
- **Then**: The system executes `Physics.Linecast(sourcePosition, hit.position, WorldLayerMask, QueryTriggerInteraction.Ignore)`, detects the intervening wall collider, rejects the sample, and returns `valid = false` with `reachable = false`.
- **Failure diagnostic**: `"FAIL [AC-NAV-09]: SamplePosition snapped through a thin wall without linecast occlusion rejection."`

#### AC-NAV-10: Target Elevated Off-Mesh Rejection
- **Given**: A non-walkable crate prop of height $0.60\text{ m}$ ($> 0.40\text{ m}$) placed on a level floor. A noise event anchor is placed at the crate's top center $(2.00, 0.60, 2.00)\text{ m}$.
- **When**: NavMesh target validation is executed with maxDistance $0.40\text{ m}$.
- **Then**: `SamplePosition` returns `false`, target is marked `reachable = false`, and the agent does not set destination or generate a path.
- **Failure diagnostic**: `"FAIL [AC-NAV-10]: Off-mesh elevated target was incorrectly accepted as reachable."`

#### AC-NAV-11: Agent-Parameterized Path Clearance vs. Static Query
- **Given**: A doorway with clear width $0.60\text{ m}$ (navigable for a point or small agent $r \le 0.30\text{ m}$, but impassable for guard $r_{\text{guard}} = 0.40\text{ m}$).
- **When**: `agent.CalculatePath(destination, path)` is invoked for the guard instance.
- **Then**: `agent.CalculatePath` returns `false` or status `PathPartial` / `PathInvalid`, asserting that static queries are not used and the agent's $0.40\text{ m}$ radius is strictly enforced during path generation.
- **Failure diagnostic**: `"FAIL [AC-NAV-11]: Agent-parameterized path query routed guard through a 0.60m constriction (r_guard=0.40m requires >= 0.80m passage)."`

#### AC-NAV-12: RVO Head-On Avoidance Priority Resolution
- **Given**: Two guards in a minimum-width corridor ($1.20\text{ m}$) on collision trajectories: Guard 1 in Chase state (`avoidancePriority = 10`), Guard 2 in Patrol state (`avoidancePriority = 50`).
- **When**: The simulation advances over 30 ticks ($1.5\text{ s}$).
- **Then**:
  - Guard 1 maintains forward velocity along the corridor axis with lateral deviation $\le 0.15\text{ m}$.
  - Guard 2 decelerates or steers laterally toward the corridor boundary, permitting Guard 1 to pass without capsule inter-penetration ($\|\vec{p}_{\text{guard1}} - \vec{p}_{\text{guard2}}\|_{XZ} \ge r_{\text{guard1}} + r_{\text{guard2}} = 0.80\text{ m}$ at all times).
- **Failure diagnostic**: `"FAIL [AC-NAV-12]: RVO avoidance failed; guard capsules penetrated (min distance: " + minDistance + "m < 0.80m)."`

#### AC-NAV-13: Off-Mesh Warp Recovery Gate
- **Given**: A guard agent whose world position is forcibly perturbed off the NavMesh manifold by $0.50\text{ m}$ due to physics collision, causing `agent.isOnNavMesh == false`.
- **When**: The NavMesh recovery routine executes on the subsequent update tick.
- **Then**: The system samples the nearest NavMesh point within $1.0\text{ m}$, calls `agent.Warp(hit.position)`, and restores `agent.isOnNavMesh == true` within 1 tick. If perturbed $> 1.0\text{ m}$ with no surface, `ERR_GUARD_FALLEN_OFF_NAVMESH` is logged.
- **Failure diagnostic**: `"FAIL [AC-NAV-13]: Off-mesh agent failed to warp recover to valid surface within 1 tick."`

#### AC-NAV-14: HideSpot Approach Standoff Navigation
- **Given**: A HideSpot located at centroid $(10.00, 0.00, 0.00)\text{ m}$ with `spot_front_anchor = (10.00, 0.00, 1.00) m`, `hold_vector = (0, 0, 1)`, and interior position $(10.00, 0.00, -0.20)\text{ m}$.
- **When**: Guard AI initiates a `HideSpotApproach` navigation request.
- **Then**:
  - The destination passed to `agent.CalculatePath` equals exactly `guard_hold` = $(10.00, 0.00, 1.40)\text{ m}$ ($\text{spot\_front\_anchor} + r_{\text{guard}} \times \text{hold\_vector}$).
  - Reachability check validates `proxy(interior_position)` per GDD #5, never the raw prop centroid.
- **Failure diagnostic**: `"FAIL [AC-NAV-14]: HideSpot approach destination targeted raw centroid or incorrect standoff offset."`

---

### Scope 3: Performance & Budget Tests

#### AC-NAV-15: Main-Thread CalculatePath Duration Budget
- **Given**: A facility NavMesh scene containing 15 rooms, 25 doorways, and complex furniture obstacles.
- **When**: 100 random longest-path queries ($d_{\text{path}} \ge 30.0\text{ m}$) are executed on the main thread via `NavMeshAgent.CalculatePath`.
- **Then**: 99% of queries complete in $< 0.50\text{ ms}$ each, and max single-query execution duration does not exceed $1.00\text{ ms}$ (well within the 16.6 ms frame budget).
- **Failure diagnostic**: `"FAIL [AC-NAV-15]: CalculatePath exceeded performance ceiling. Max duration: " + maxMs + "ms (budget 1.00ms)."`

#### AC-NAV-16: Chase Re-Pathing Frequency Clamp Enforcement
- **Given**: A test scene where the player moves in high-frequency sinusoidal oscillations (direction reversed every frame at 60 fps).
- **When**: Simulation runs for 120 frames ($2.0\text{ s}$).
- **Then**: The total number of `CalculatePath` invocations issued by the chasing guard agent does not exceed $20$ queries ($\le 10\text{ Hz}$, strictly enforcing $t_{\text{repath\_min}} = 0.10\text{ s}$).
- **Failure diagnostic**: `"FAIL [AC-NAV-16]: Chase re-pathing violated frequency ceiling. Queries issued: " + queryCount + " in 2.0s (max 20)."`

#### AC-NAV-17: Multi-Guard Chase Staggered Query Distribution
- **Given**: Three active guards chasing the player simultaneously.
- **When**: Simulation runs for 60 consecutive frames.
- **Then**: The path calculation queries are round-robin staggered such that no more than 1 guard issues a full `CalculatePath` call on any single engine frame tick.
- **Failure diagnostic**: `"FAIL [AC-NAV-17]: Query batching burst detected. Multiple CalculatePath calls on frame tick " + frameNumber + "."`

---

### Scope 4: Level Geometry Certification Rules (Editor/Build-Time Assertions)

#### AC-NAV-18: Automated Minimum Corridor Clearance Certification
- **Given**: A level blockout or production scene ready for build certification.
- **When**: The headless level certification script sweeps all baked NavMesh polygons and performs raycasts perpendicular to corridor wall colliders on the `World` layer.
- **Then**: Zero points on traversable NavMesh corridors have a static clearance width $< 1.20\text{ m}$. Any narrower section fails the build with `ERR_CORRIDOR_CHOKE_POINT` and logs the exact world coordinates.
- **Failure diagnostic**: `"FAIL [AC-NAV-18]: Level certification detected corridor choke point of " + width + "m at coordinate " + pos + "."`

#### AC-NAV-19: Zero Off-Mesh Links Verification Gate
- **Given**: Pre-baked `NavMeshSurface` asset for any level scene.
- **When**: Build pipeline inspects the baked `NavMeshData`.
- **Then**: `navMeshData.GetOffMeshLinks()` count is strictly $0$, and `generateLinks` is `false`.
- **Failure diagnostic**: `"FAIL [AC-NAV-19]: NavMesh contains " + linkCount + " off-mesh links. Off-mesh links are forbidden in MVP scope."`

#### AC-NAV-20: Physics Bake Layer Mask Exclusion Invariant
- **Given**: The `NavMeshSurface` component attached to facility root geometry.
- **When**: The layer mask property `layerMask` is inspected during build validation.
- **Then**:
  - The mask includes static structural layers (`World`, `Default`).
  - The mask strictly excludes dynamic layers: `Player` (Layer 6), `Guard` (Layer 7), `HideSpot` (Layer 8), `Ignore Raycast` (Layer 2), and `Trigger` (Layer 9), conforming to ADR-0002.
- **Failure diagnostic**: `"FAIL [AC-NAV-20]: NavMeshSurface layerMask includes dynamic actor or trigger layers: " + maskNames + "."`

#### AC-NAV-21: Maximum Traversable Slope and Step Height Ceiling
- **Given**: The bake settings of `NavMeshSurface`.
- **When**: Parameter validation runs during editor asset post-processing.
- **Then**:
  - `agentMaxSlope` is $\le 45.0^\circ$.
  - `agentClimb` (step height) is $\le 0.30\text{ m}$.
- **Failure diagnostic**: `"FAIL [AC-NAV-21]: NavMesh bake settings violate vertical bounds: slope=" + slope + " (max 45), climb=" + climb + "m (max 0.30m)."`

## Open Questions

1. **Dynamic Doorway Obstacles (Target Tier Roadmap)**:
   - *Question*: In MVP, all doors and corridors are static passages. For the Target tier (multi-room facility with keycard doors and security shutters), should moving doors utilize `NavMeshObstacle` with runtime carving (`carve = true`), or should doors manipulate area masks (e.g. toggling door volume cost between `1.0` and `Infinity`)?
   - *Status*: **Deferred to Target Scope**. MVP maintains strictly static baked geometry. If dynamic doors are introduced in Target, area mask cost toggling will be tested first to avoid runtime NavMesh tile re-baking spikes.

2. **Additive Scene Boundary Stitching (GDD #17 Coupling)**:
   - *Question*: When segments are loaded additively, how will NavMesh surfaces between adjacent rooms connect across segment transition thresholds?
   - *Status*: **Owned by Scene / Asset Management (`#17`)**. Unity's `NavMeshDataInstance` allows multiple separately baked surfaces to connect seamlessly if their border vertices align within $0.05\text{ m}$. Level Certification rule AC-NAV-18 will be extended in GDD #17 to validate seam alignment.

3. **WebGL Memory & Voxel Optimization**:
   - *Question*: Does the high-resolution voxel bake ($v_{\text{size}} = 0.10\text{ m}$) increase `NavMeshData` binary memory size significantly for the WebGL portfolio demo build?
   - *Status*: **Low Risk / Validated at Milestone 0**. Single-room and vertical slice test levels with $v_{\text{size}} = 0.10\text{ m}$ produce $< 2\text{ MB}$ of NavMeshData, well within the browser WebAssembly heap ceiling.
