# ADR-0002: Shared Physics and Collision Contract

## Status
Proposed

## Date
2026-08-29

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6 LTS (6000.3.17f1) |
| **Domain** | Physics |
| **Knowledge Risk** | LOW |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `.claude/docs/technical-preferences.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | Unity Test Framework coverage for layer validation, query profiles, initial overlap, swept contact, transform synchronization, and fixed-step determinism |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | Player Noise, Perception, Player Movement & Hide, Level authoring and NavMesh certification |
| **Blocks** | Production implementation of hearing occlusion, Burst collision, pickup reach and HideSpot trigger contracts until the shared pins are accepted and tested |
| **Ordering Note** | This ADR defines shared physics/query authority. It does not alter the active Player Noise design review or its review records. |

## Context

### Problem Statement

Several Whisper Ward systems require the same physics assumptions: hearing
occlusion, vision queries, pickup reach, HideSpot triggers, and the Burst projectile.
The current design documents identify the required pins, but Physics system `#18`
has no standalone contract. Without one, systems can silently diverge on layer
resolution, trigger handling, origin points, collision authority, transform
synchronization, or simulation timing.

The most sensitive case is Burst. It must produce the same collision-resolved landing
point in the runtime simulator and the visual preview, independent of render-frame
partitioning. It must also remain deterministic on the WebGL target without relying
on Rigidbody timing or an unspecified physics simulation mode.

### Constraints

- Unity 6 LTS with PhysX, C#, PC and WebGL targets.
- The shared E20 mask must be valid before gameplay queries begin.
- Hearing and vision use binary occlusion; there is no partial portal attenuation in
  the physics query contract.
- Burst uses a kinematic custom simulation and does not require a Rigidbody.
- Physics queries run on Unity's main thread in the MVP.
- The injected virtual clock, not render-frame duration, is authoritative for Burst
  flight time and timeout behavior.
- Query behavior must be observable in deterministic fixtures and level certification.

### Requirements

- Resolve and cache the configured E20 layer names once at initialization.
- Fail load when a required layer is missing, the assembled mask is zero, or a
  forbidden `Player`, `Guard`, or `trigger` bit is present.
- Pass `QueryTriggerInteraction.Ignore` for hearing, vision, pickup reach and Burst
  collision queries.
- Use `player_feet + f12_noise_origin_offset` for movement hearing, the published
  post-collision landing contact for Burst hearing, and the registered guard-eye
  anchor as the endpoint for both.
- Use one authoritative closest-hit `SphereCast` for each Burst substep, plus one
  explicit initial-overlap check at launch.
- Resolve contact at the sphere surface and apply the registered push-out margin.
- Synchronize authored solid transforms once before each Burst simulation batch.
- Keep Burst's fixed substep, accumulator remainder and backlog policy deterministic.
- Make the same collision resolver available to the preview and runtime simulator.

## Decision

Define one shared Physics & Collision service with two responsibilities:

1. Initialize and expose the validated, cached E20 query mask and shared query
   profiles.
2. Provide deterministic, main-thread query and Burst simulation seams that all
   consumers use instead of rebuilding masks or choosing their own collision rules.

### Layer and query authority

At initialization, resolve every exact name in the configured E20 required set using
`LayerMask.NameToLayer(name)`. The current registry configuration requires `World`;
the implementation must support the project's exact configured names rather than
assuming a `World` family placeholder. If any required name resolves to `-1`,
initialization fails with a stable diagnostic.

After assembling the mask:

- reject a zero mask;
- reject any `Player`, `Guard`, or `trigger` bit;
- cache the final mask for the session;
- never rebuild or resolve the mask inside a per-frame or per-query path.

All shared gameplay queries pass `QueryTriggerInteraction.Ignore`. A query profile may
add a system-specific collider mask only by intersecting with the validated E20 mask;
it may not bypass the forbidden-layer checks.

### Hearing and vision query profile

Perception owns the hearing decision, but Physics owns the query setup. The hearing
occlusion probe is a binary `Physics.Linecast` from a source-kind-specific origin to
the registered guard-eye position:

```text
origin(movement) = player_feet + f12_noise_origin_offset
origin(Burst)    = published post-collision landing contact
endpoint        = registered guard-eye position
mask            = cached E20 mask
triggers        = QueryTriggerInteraction.Ignore
```

The Physics service returns the clear/blocked result and probe record. Perception
then applies its planar distance and soft vertical falloff rules. Physics does not
apply hearing radius, residual, suspicion, or FSM behavior. Burst never receives an
additional player-feet offset after its landing contact is published.

Vision and HideSpot-related line queries use the same validated mask and trigger
policy, with their owning system supplying the endpoints and interpreting the result.

### Burst collision and simulation authority

Burst is simulated by a main-thread custom fixed-step service using semi-implicit
Euler integration. Each flight owns an accumulator and elapsed time. For every
simulation batch:

```text
tick_count = floor(accumulator / dt_max)
run tick_count steps of exactly dt_max
retain the fractional remainder
```

The canonical `dt_max` is `1/120 s`. No remainder tick is simulated. The render
frame cannot change the trajectory. The service does not call `Physics.Simulate` or
advance the default `PhysicsScene` as part of Burst; `Physics.simulationMode` and
Rigidbody callbacks are not the authority for Burst time or landing resolution.
Authored static/kinematic E20 solids are queryable scene geometry; Burst itself has
no Rigidbody and does not depend on ordinary fixed-step callbacks.

Before each batch, the service finalizes authored solid transforms and performs one
explicit global `Physics.SyncTransforms()`. The cached E20 mask applies to the
subsequent queries, not to synchronization itself. The custom Burst service pins
`Physics.autoSyncTransforms = false` for the simulation lifetime; it restores the
prior project setting on disposal and never toggles the global setting per query.
The implementation and performance harness record the one-sync-per-batch count;
no hidden automatic sync may be relied upon for correctness.

For each fixed substep:

```text
v_next = v_current + gravity * dt_max
p_next = p_current + v_next * dt_max
cast segment = p_current -> p_next
```

The authoritative swept query runs in the default gameplay `PhysicsScene` after the
single pre-batch synchronization and is a single-hit `Physics.SphereCast` using:

```text
origin      = p_current
direction   = normalize(p_next - p_current)
maxDistance = length(p_next - p_current)
radius      = projectile_radius
mask        = cached E20 mask
triggers    = QueryTriggerInteraction.Ignore
```

`direction` is required only when `p_next - p_current` is non-zero. A zero-length
center segment skips the sweep for that substep after the initial-overlap check;
there is no zero-direction cast and no substitute distance. The initial-overlap
query runs once at flight start against the resolved launch sphere. An initial
overlap rejects the throw before spend. It is not repeated as a substitute for the
swept query.

When a swept collision succeeds, the Physics service returns the closest hit's
collider-surface point and outward normal. For this contract Unity's
`RaycastHit.point` is named `collider_surface_contact`: the world-space point on the
hit collider surface, not the projectile center. Player Noise owns the publication
decision and uses the canonical projectile-center contact position:

```text
published_position = collider_surface_contact
                  + hit.normal * (projectile_radius + epsilon_contact)
```

`epsilon_contact` is only the post-hit center push-out margin. The separate
registered `contact_ambiguity_tolerance` is only a Level/content-certification
threshold: if two authored solids resolve within that distance, certification fails
with `AMBIGUOUS_COLLISION`; runtime never uses it to alter the SphereCast result.
The projectile-center apex remains distinct from the sphere-top clearance datum. The
Physics service does not publish `NoisePublished`, consume Burst inventory, or decide
whether a failed launch is a player-facing rejection.

Unity closest-hit semantics are authoritative. A full hit-buffer nearest-hit fallback
is not permitted. Zero-length center segments skip the sweep after the initial-overlap
check; they do not issue a zero-direction cast.

### Backlog and timeout behavior

The Burst service retains fractional accumulator time and simulates integer fixed
ticks while the backlog is below the registered cap. If the cap is reached, only
simulation time beyond the cap is discarded and a `burst-backlog-clamped` diagnostic
is recorded. Published sensing facts are not deleted by backlog clamping.

Flight timeout resolves on the first fixed tick whose `flight_elapsed >= 3.0 s`.
Collision wins when its swept contact resolves on or before that timeout tick. The
preview uses the same fixed-step and collision resolver, including initial-overlap,
contact push-out and timeout rules.

## Architecture Diagram

```text
Project layer list + registry constants
                 │
                 ▼
      Physics initialization service
      - resolve exact layer names
      - validate E20 and forbidden bits
      - cache mask and query profiles
                 │
        ┌────────┴────────┐
        ▼                 ▼
  Hearing/Vision      Burst fixed-step service
  Linecast profiles   SyncTransforms once/batch
                      SphereCast per substep
                      initial-overlap at launch
        │                 │
        ▼                 ▼
   Perception/FSM     NoiseEmitter + preview
```

## Key Interfaces

The concrete class names may follow the project's composition conventions, but the
observable seams are:

```text
PhysicsQueryConfig.Initialize(projectLayerNames)
  -> ValidatedPhysicsConfig { E20Mask, query profiles }

PhysicsQueryService.Linecast(origin, endpoint, profile)
  -> PhysicsProbeResult { Clear, Hit, Collider, Diagnostics }

BurstSimulationService.StepBatch(flight, virtual_delta)
  -> BurstStepResult { TickCount, Contact, Timeout, BacklogClamped, Diagnostics }

BurstCollisionResolver.InitialOverlap(position, radius, profile)
  -> InitialOverlapResult

BurstCollisionResolver.Sweep(center, nextCenter, radius, profile)
  -> ClosestContactResult
```

`PhysicsQueryConfig` owns validation and caching. `PhysicsQueryService` owns query
setup and probe observability. `BurstSimulationService` owns fixed-tick advancement.
Player Noise owns Burst inventory and raw event publication; Perception owns hearing
eligibility and relay creation; Level owns authored certification.

## Alternatives Considered

### Alternative 1: Rigidbody-driven Burst

- **Description**: Spawn a Rigidbody projectile and use Unity physics callbacks for
  flight and landing.
- **Pros**: Familiar Unity workflow and automatic collision integration.
- **Cons**: Callback timing and render/physics partitioning complicate deterministic
  replay, preview parity, timeout ownership and WebGL hitch behavior.
- **Rejection Reason**: Does not satisfy the fixed virtual-time trajectory contract
  without adding a second authoritative simulation layer.

### Alternative 2: Per-system physics setup

- **Description**: Let Perception, Player Noise, HideSpot and Level each resolve
  their own masks and query options.
- **Pros**: Local implementation and fewer shared abstractions initially.
- **Cons**: Masks, trigger policies, endpoints and layer validation can drift;
  missing layers may fail differently across systems.
- **Rejection Reason**: Violates the shared E20 contract and makes cross-system
  certification unreliable.

### Alternative 3: Multi-hit buffer with caller-side nearest selection

- **Description**: Use a non-allocating hit buffer and select a nearest hit in caller
  code.
- **Pros**: Appears to provide explicit control over multiple contacts.
- **Cons**: Buffer capacity becomes a correctness dependency, tie behavior is easy to
  leave undefined, and callers can disagree on nearest-hit selection.
- **Rejection Reason**: The authoritative contract requires Unity closest-hit
  semantics and content rejection for ambiguous certified contacts.

### Alternative 4: Shared validated profiles plus custom fixed-step Burst service

- **Description**: Centralize E20/query validation and use a deterministic custom
  fixed-step resolver for Burst.
- **Pros**: Matches the GDD contracts, preserves preview/runtime parity, supports
  deterministic tests and keeps WebGL behavior explicit.
- **Cons**: Requires dedicated simulation and test seams instead of relying solely on
  built-in Rigidbody behavior.
- **Rejection Reason**: Not rejected; this is the selected approach.

## Consequences

### Positive

- All systems use one validated E20 mask and trigger policy.
- Burst runtime and preview share the same trajectory and collision authority.
- Initial overlap, closest contact, timeout and backlog behavior are testable without
  relying on render-frame timing.
- Missing or malformed project layers fail early instead of producing silent gameplay
  inconsistencies.
- WebGL performance and hitch behavior remain explicit measurement concerns.

### Negative

- A shared Physics service becomes a foundation dependency for several systems.
- The custom Burst simulator requires more implementation and test code than a
  Rigidbody prototype.
- Layer configuration changes require initialization validation and may fail content
  loading until corrected.
- The performance harness must measure synchronization and query costs separately.

### Risks

- **Layer drift**: validate the exact configured names and persist a load diagnostic.
- **Transform timing drift**: make the single pre-batch sync observable and test it.
- **Preview/runtime divergence**: route both through the same collision resolver.
- **Backlog spiral**: enforce the registered cap and record every clamp diagnostic.
- **Ambiguous contacts**: reject the authored level volume rather than choosing by
  incidental collider or buffer order.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| `player-noise.md` | Shared E20 mask, `NameToLayer` validation, trigger exclusion and cached resolution | Defines initialization validation and one session-cached mask |
| `player-noise.md` | Raised hearing origin, binary linecast, Burst single-hit sphere cast and initial-overlap rejection | Defines query profiles and authoritative collision paths |
| `player-noise.md` | Fixed `dt_max = 1/120 s`, retained accumulator remainder and deterministic timeout | Defines custom fixed-step simulation authority |
| `player-noise.md` | Contact position uses surface point plus normal push-out | Defines the canonical contact result and ownership boundary |
| `perception.md` | Hearing geometry uses the shared origin, eye endpoint, E20 mask and ignored triggers | Provides the physics-owned probe setup while leaving hearing semantics to Perception |
| `player-movement-hide.md` | HideSpot trigger and line-query setup uses the shared Physics contract | Provides the validated layer and trigger policy for HideSpot |
| `systems-index.md` | Physics pins are a pre-milestone-0 foundation obligation | Establishes system `#18` as the owner of shared physics authority |

## Performance Implications

- **CPU**: One cached-mask lookup per query, at most one hearing Linecast per
  evaluated guard/fact pair per hearing boundary under the registered pair cap, one
  explicit transform sync per Burst batch, and one single-hit SphereCast per fixed
  Burst substep.
- **Memory**: Cached layer/query configuration and bounded simulation state; no full
  hit-buffer fallback or unbounded contact history.
- **Load Time**: Exact layer validation occurs at initialization and fails early on
  invalid project configuration.
- **Network**: None; this is a local gameplay physics contract.
- **Measurement**: AC21/OQ6 remains the gate for the 2.0 ms case, aggregate subsystem
  budget and hitch replay. This ADR does not claim those measurements are complete.

## Migration Plan

1. Introduce the validated layer configuration and query-profile seam.
2. Route Perception hearing and vision probes through the shared setup.
3. Route HideSpot and pickup reach queries through the shared trigger policy.
4. Implement the Burst fixed-step service and shared collision resolver.
5. Replace any provisional per-system layer or cast setup with the shared service.
6. Add deterministic fixtures for layer failure, initial overlap, closest contact,
   timeout, backlog clamp and preview/runtime parity.

No migration step authorizes changes to the active Player Noise design document,
registry entries, level manifest or review records.

## Validation Criteria

- Missing required layer names fail initialization with a stable diagnostic.
- Zero E20 mask or forbidden `Player`, `Guard`, or `trigger` bits fail initialization.
- Every shared query records the validated E20 mask and
  `QueryTriggerInteraction.Ignore`.
- Hearing origin and guard-eye endpoint match the registered geometry pins.
- An initial Burst overlap rejects before spend and produces no contact publication.
- A single swept hit returns the closest contact and the canonical push-out point.
- Two ambiguous certified solids fail level validation rather than relying on buffer
  order.
- Runtime and preview produce identical fixed-tick trajectory/contact results.
- Render-frame partitioning does not alter the virtual trajectory while below the
  backlog cap.
- Backlog clamp discards only excess simulation time and records its diagnostic.
- Timeout and collision tie behavior match the fixed-tick rule.
- Transform synchronization occurs once per batch and is visible to the performance
  harness.

## Related Decisions

- [ADR-0001: Deterministic Event/Messaging Bus](adr-0001-event-messaging-bus.md)
- `design/gdd/player-noise.md` — Player Noise (`NoiseEmitter`) contract
- `design/gdd/perception.md` — Perception sensing and hearing geometry
- `design/gdd/player-movement-hide.md` — HideSpot trigger and authoring pins
- `design/registry/entities.yaml` — shared physics query and Burst constants
