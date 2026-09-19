# MVP Burst Route Fixture

> **Status**: Canonical design fixture manifest — schema 1.3, authored 2026-08-29, revised 2026-09-15 (route geometry bindings, Signal-A pocket predicates, gate-crossing boundary semantics, fixture-role split)
> **Fixture ID**: `WW-MVP-BURST-ROUTE-01`
> **Profile**: `MVP` — one room, one guard, two Burst pickups
> **Coordination service**: `absent`
> **Owner**: Level/Content (Noise co-sign)
> **Consumed by**: `LevelFixture`, Player Noise AC20, registry `level_burst_route`
> **Validator**: `LevelFixtureValidator.v1`
> **Evidence status**: `UNCAPTURED` — `actual` fields are required validator output; this manifest does not fabricate Unity, NavMesh, Wwise, WebGL, or performance evidence.

This manifest is the deterministic MVP certification fixture for the Player Noise
Burst route. It is not a production Unity scene or a substitute for the future Level
GDD. Coordinates, geometry variants, IDs, expected values, and tolerances are stable
fixture data. The validator fails closed for a missing, unknown, malformed,
contradictory, ambiguous, or nominal-radius-only record. The registry's conditional
field rules are part of this schema: every record carries the complete key set, with
`[]`, `none`, `not_applicable`, `NOT_EVALUATED`, or an explicitly permitted `null`
used only where the record outcome makes that value inapplicable.

## Manifest Schema and Provenance

```yaml
manifest:
  schema_version: WW-MVP-BURST-ROUTE-1.3
  fixture_id: WW-MVP-BURST-ROUTE-01
  profile: MVP
  coordination_service: absent
  validator_id: LevelFixtureValidator.v1
  validator_contract: LevelFixtureValidator.v1
  canonical_manifest: design/levels/mvp-burst-route-fixture.md
  provenance:
    authored_by: [Level/Content, Player Noise]
    source_documents:
      - design/gdd/player-noise.md
      - design/gdd/perception.md
      - docs/architecture/adr-0002-physics-collision-contract.md
      - design/registry/entities.yaml
    source_revision: 2026-09-09  # canonical cross-file contract snapshot; route-local authored date remains 2026-08-29
    runtime_capture: not_available
  certification_status: UNCAPTURED
  validation_result: ACTUAL_CAPTURE_UNAVAILABLE
  actual: UNCAPTURED
```

The required-section set is defined by the registry `level_burst_route.required_sections`
(rev 2026-08-31), not by a manifest key: `manifest`, `provenance` (serialized as
`manifest.provenance` per the registry field contract), `evidence_contract`,
`coordinate_contract`, `marker_lifecycle`, `pickup_interactions`,
`pickup_state_transitions`, `geometry_variants`, `solids`, `triggers`,
`route_evidence`, `route_safety_predicates`, `route_causality`, `gate_transitions`,
`synthetic_receiver_endpoints`, `receiver_probes`, `occlusion_probes`,
`valid_throw_volumes`, `negative_validation_cases`, `failed_gate_spends`,
`gate_trigger_negative_cases`, `patrol_phase_windows`, `teaching_sequence`,
`signal_a_observation_pockets`, `signal_a_visibility`, `restart_transaction`,
`guard_extended_search_patrol`, `navmesh_layer_separation_check`, `workload_profiles`,
and `trace_contract`. The
validator fails closed on a manifest key outside the registry field contract, so
section names are carried by the registry, and every section above exists in this
document exactly once under its registry name.

`manifest.certification_status=UNCAPTURED` is intentional until a deterministic runtime or
fixture runner serializes `actual` values. `PASS` may be emitted only when every
positive record has complete expected and actual fields and every negative record has
its expected rejection code and observed rejection code. Evidence status and validator
result are separate: `UNCAPTURED` describes missing runtime evidence, while
`ACTUAL_CAPTURE_UNAVAILABLE` is the current fail-closed validator result; neither is a
pass result.

```yaml
evidence_contract:
  evidence_status: [UNCAPTURED, CAPTURED]
  actual:
    union: [UNCAPTURED, CAPTURED_RECORD]
    captured_record: {status: CAPTURED, observed: record_specific_typed_object}
  expected_actual_pair:
    expected: record_specific_typed_object
    actual: evidence_contract.actual
  comparison: expected values are compared to observed fields using the record's named tolerance
  missing_fields: fail_closed
```

`actual: UNCAPTURED` is therefore a valid pre-capture evidence-state value, not an
observed measurement and never a validator pass. After capture, every record replaces
it with `{status: CAPTURED, observed: {...}}`, where the `observed` object uses the
record-specific fields named by the registry contract. The coordinate/runtime contract
is serialized as part of the manifest, not inferred from prose:

```yaml
coordinate_contract:
  world_frame: Y-up
  units: metres
  hearing_plane: XZ
  room_aabb_min: [0.0, 0.0, 0.0]
  room_aabb_max: [16.0, 4.5, 16.0]
  room_ceiling_y: 4.5
  e20_world_layer: World
  forbidden_layers: [Player, Guard, trigger]
  query_trigger_policy: QueryTriggerInteraction.Ignore
  physics_scene: REQUIRED_RUNTIME_PHYSICS_SCENE_ID
  physics_scene_injection: required_explicit_authoritative_scene
  auto_sync_transforms: false
  sync_transforms_per_burst_batch: 1
  actual: UNCAPTURED
```

## Registered Workload Profiles

The fixture separates the supported MVP envelope from deliberate stress coverage. The
stress profile exercises the registered 30-guard and 8-fact caps; it is not an
unlimited support promise and does not imply 240 Linecasts in one boundary because
`max_hearing_pairs_per_boundary=30` is the authoritative query cap.

```yaml
workload_profiles:
  - profile_id: supported_mvp
    purpose: supported_mvp_envelope
    active_guards: 1
    due_facts: 1
    evaluated_guard_fact_pairs: 1
    total_candidate_pairs: 1
    expected_linecast_count: 1
    burst_flights: 1
    due_hearing_boundaries: 1
    expected_deferred_pairs: 0
    required_budget_ms: 33.0
    budget_evidence: {status: PENDING_OQ6, value_ms: null}
    budget_role: acceptance_gate
    platform_scope: [WebGL]
    decomposition_budgets_ms: {hearing_burst_perception: 2.0, noise_path_aggregate: 12.0}
    cap_controls: [event_bus_capacity, event_bus_retry_limit, hearing_queue_capacity, deferred_boundary_capacity, max_hearing_boundaries_per_frame, max_hearing_guards_per_boundary, max_hearing_facts_per_boundary, max_hearing_pairs_per_boundary, frame_delta_cap, burst_backlog_cap, stall_magnitude_cap, stall_rate_cap]
    component_timer_contract: {frame_time_source: REQUIRED_RUNTIME_FRAME_TIME_SOURCE, hitch_window_clock_source: REQUIRED_RUNTIME_WALL_CLOCK_SOURCE, timer_resolution_s: REQUIRED_RUNTIME_TIMER_RESOLUTION, render_frame_index_field: required, reconciliation_policy: disjoint_child_subtraction}
    actual: UNCAPTURED
  - profile_id: stress_30_guard_8_fact
    purpose: stress_coverage_not_support_promise
    active_guards: 30
    due_facts: 8
    evaluated_guard_fact_pairs: 30
    expected_linecast_count: 30
    total_candidate_pairs: 240
    expected_deferred_pairs: 210
    burst_flights: 1
    due_hearing_boundaries: 1
    required_budget_ms: null
    budget_evidence: {status: PENDING_OQ6, value_ms: null}
    budget_role: diagnostic_only
    platform_scope: [PC, WebGL]
    cap_controls: [event_bus_capacity, event_bus_retry_limit, hearing_queue_capacity, deferred_boundary_capacity, max_hearing_guards_per_boundary, max_hearing_facts_per_boundary, max_hearing_pairs_per_boundary, frame_delta_cap, burst_backlog_cap, stall_magnitude_cap, stall_rate_cap]
    component_timer_contract: {frame_time_source: REQUIRED_RUNTIME_FRAME_TIME_SOURCE, hitch_window_clock_source: REQUIRED_RUNTIME_WALL_CLOCK_SOURCE, timer_resolution_s: REQUIRED_RUNTIME_TIMER_RESOLUTION, render_frame_index_field: required, reconciliation_policy: disjoint_child_subtraction}
    actual: UNCAPTURED
```

`actual` for both profiles requires measured duration, query counters, queue/deferred
counters, and replay identity fields. Missing capture remains `UNCAPTURED` and cannot
satisfy the performance gate.

## Coordinate, Layer, and Runtime Contract

- World frame is Y-up, metres, with X/Z as the hearing plane.
- Room bounds are the complete AABB snapshot `min=(0.0,0.0,0.0)`,
  `max=(16.0,4.5,16.0)`. The room envelope is self-consistent by construction:
  `room_aabb_max.y == room_ceiling_y == 4.5 m`, so no authored solid, launch
  position, or published contact may exceed `y=4.5 m`. The harness-synthetic
  `geometry_throw_void_open_01` variant is the sole explicit exception: its
  diagnostic fallback endpoint is outside the playable AABB, is tagged
  `aabb_policy=diagnostic_fallback_exempt`, and is never treated as a playable
  landing or route point.
- All solid geometry has `layer=World`, the exact configured E20 layer name.
  `Player`, `Guard`, and `trigger` bits are excluded.
- All shared queries use `QueryTriggerInteraction.Ignore`.
- The injected gameplay `PhysicsScene` resolved from the active contentful scene is the query scene. One global
  `Physics.SyncTransforms()` occurs before each Burst simulation batch; E20 applies
  to the subsequent queries, not to synchronization.
- `Physics.autoSyncTransforms=false` is the custom Burst service policy. The service
  never toggles it per query and records the prior setting on disposal.
- `pickup_reach_origin_offset=1.2 m` (chest height, rev 2026-09-19; clears 0.8 m table lips),
  `f12_noise_origin_offset=0.25 m`, `guard_eye_height=1.6 m`,
  `hearing_y_hard_cutoff=4.0 m`, and the Burst starter `R_burst=10.5 m` are loaded
  from the registry. The legal loaded `R_burst` range is `9.0–14.0 m`, subject to
  `R_burst ≤ R_vis`; this fixture uses the starter value.
- `root_policy=dual_regime_root_oracle` (rev 2026-09-19); selecting descending crossing `t_plus`
  for downward impacts (including $\Delta y \ge 0$ and elevated top surfaces) and ascending crossing `t_minus`
  for underside/ceiling impacts, with sub-tick rejection $t > \epsilon_t = \Delta t_{tick} = 1/120\text{ s}$,
  while Level-owned grazing geometry is rejected separately with `AUTHORED_GRAZING_GEOMETRY_REJECTED` (discriminants $D \le 0.01$).
- `diagnostic_simulation_envelope_m` is not a gameplay comparison tolerance; gameplay
  position/contact/route assertions use the registry `CompareTolerance` (≤ 5 mm).
- The fixture does not certify the 2.0 ms or 12.0 ms performance claims. Those are
  the pending OQ6 target-hardware gate.

## Stable Markers and Lifecycle Records

Every marker record has a stable ID, type, world-space position or AABB, layer,
source, expected lifecycle, and an `actual` capture slot. A validator must reject a
missing marker, duplicate ID, unexpected layer, or unknown lifecycle state.
Radius ownership is explicit: `player_agent_radius_m=0.30` is the player NavMesh
route-query radius, `patrol_agent_radius_m=0.40` is the authored guard patrol-body
clearance radius, `pickup_reach_radius_m=1.60` is the interaction sphere radius,
and `projectile_radius_m=0.05` is the Burst collision sphere radius; none may be
used as a substitute for another.

| Record | Stable ID | Expected fixture value / invariant |
|---|---|---|
| Checkpoint | `checkpoint_mvp_01` | point `(1.0,0.0,1.0)`; full-restart origin and reserve-route origin; expected lifecycle `active` |
| Gate pickup | `burst_gate_pickup_01` | point `(1.0,0.0,5.0)`; initial lifecycle `Placed`; only pickup whose failed spend is exercised; sited ≥ `pickup_reach_radius` (1.6 m) + agent radius (0.4 m) = 2.0 m from every patrol corridor segment (nearest segment (3,8)→(3,12) at ≈ 3.61 m) |
| Reserve pickup | `burst_reserve_pickup_01` | point `(1.0,0.0,14.5)`; initial lifecycle `Placed`; must remain reachable after failed gate cases; sited ≥ 2.0 m from every patrol corridor segment (nearest segment (3,8)→(3,12) at ≈ 3.20 m) |
| Gate entry | `burst_gate_entry_01` | AABB centre `(5.5,0.0,8.0)`, dimensions `(3.0,2.0,3.0) m`; receiver `guard_mvp_01`; `room_ceiling_y=4.5 m` |
| Gate exit | `burst_gate_exit_01` | AABB centre `(12.0,0.0,8.0)`, dimensions `(2.0,2.0,3.0) m`; reachable only after the certified Burst interaction |
| Guard patrol | `patrol_mvp_01` | closed route `(3,0,8) → (9,0,8) → (9,0,12) → (3,0,12)`; all nodes expected `PathComplete` |
| Guard | `guard_mvp_01` | eye anchor `(8.0,1.6,10.5)` in the clear receiver records; expected profile `MVP` |
| Receiver | `receiver_mvp_01` | guard `guard_mvp_01`; expected response `Investigate`; never `peer-recruit` or `locked-zone` |

```yaml
marker_lifecycle:
  - stable_id: checkpoint_mvp_01
    type: checkpoint
    world_position_or_aabb: {position: [1.0, 0.0, 1.0]}
    layer: World
    expected_initial_state: active
    expected_transition_states: {full_restart: active}
    actual: UNCAPTURED
  - stable_id: burst_gate_pickup_01
    type: pickup
    world_position_or_aabb: {position: [1.0, 0.0, 5.0]}
    layer: World
    expected_initial_state: Placed
    expected_transition_states: {failed_spend: Consumed, initial_overlap_rejection: Carried, full_restart: Placed}
    actual: UNCAPTURED
  - stable_id: burst_reserve_pickup_01
    type: pickup
    world_position_or_aabb: {position: [1.0, 0.0, 14.5]}
    layer: World
    expected_initial_state: Placed
    expected_transition_states: {after_failed_gate_cases: Placed, full_restart: Placed}
    actual: UNCAPTURED
  - stable_id: burst_gate_entry_01
    type: gate_entry
    world_position_or_aabb: {aabb_center: [5.5, 0.0, 8.0], dimensions: [3.0, 2.0, 3.0]}
    layer: World
    expected_initial_state: blocked
    expected_transition_states: {after_valid_gate_interaction: open}
    actual: UNCAPTURED
  - stable_id: burst_gate_exit_01
    type: gate_exit
    world_position_or_aabb: {aabb_center: [12.0, 0.0, 8.0], dimensions: [2.0, 2.0, 3.0]}
    layer: World
    expected_initial_state: unreachable
    expected_transition_states: {after_valid_gate_interaction: reachable}
    actual: UNCAPTURED
  - stable_id: patrol_mvp_01
    type: patrol
    world_position_or_aabb: {route_nodes: [[3.0, 0.0, 8.0], [9.0, 0.0, 8.0], [9.0, 0.0, 12.0], [3.0, 0.0, 12.0]]}
    layer: World
    expected_initial_state: active
    expected_transition_states: {all_nodes: PathComplete}
    actual: UNCAPTURED
  - stable_id: guard_mvp_01
    type: guard
    world_position_or_aabb: {eye_anchor: [8.0, 1.6, 10.5]}
    layer: Guard
    expected_initial_state: active
    expected_transition_states: {profile: MVP}
    actual: UNCAPTURED
  - stable_id: receiver_mvp_01
    type: receiver
    world_position_or_aabb: {guard_id: guard_mvp_01, eye_anchor: [8.0, 1.6, 10.5]}
    layer: Guard
    expected_initial_state: active
    expected_transition_states: {response: Investigate, target_only_responses: rejected}
    actual: UNCAPTURED
```

`pickup-reached` is telemetry only: it records entry into the shared pickup reach
query and never changes pickup state, spends a Burst, or publishes `NoisePublished`.
The explicit interaction and state-transition records below are the authoritative
proof of `Interact` acceptance and `Placed → Carried` ownership.

The reach probe is one inclusive 3D sphere predicate followed by one E20 semantic
ray probe: `distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2`,
then an injected-`PhysicsScene` ray query from the player-feet pickup reach origin
(`player_feet + up * pickup_reach_origin_offset`, 1.2 m chest height, rev 2026-09-19; clearing 0.8 m table lips) to the
pickup anchor with `QueryTriggerInteraction.Ignore`. The sphere result, Linecast result, and
`pickup-reached` telemetry are recorded separately from the explicit `Interact`
transition. `pickup_reach_radius=1.6 m` and the patrol-corridor siting check are
independent invariants: the latter remains an authoring clearance of at least
`pickup_reach_radius + patrol_agent_radius_m = 1.6 + 0.4 = 2.0 m` from every patrol corridor segment and
is not a reach or interaction result. Every uncaptured probe remains fail-closed.

```yaml
pickup_interactions:
  - interaction_id: pickup_interact_gate_01
    pickup_id: burst_gate_pickup_01
    reach_route_id: gate_route_open_01
    trigger: Interact
    expected_precondition:
      pickup_state: Placed
      reachable: true
      player_feet_ws: REQUIRED_RUNTIME_PLAYER_FEET
      pickup_anchor_ws: [1.0, 0.0, 5.0]
      pickup_reach_radius_m: 1.6
      distance_squared_m2: REQUIRED_RUNTIME_DISTANCE_SQUARED
      distance_predicate: distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2
      distance_within_reach: true
      e20_linecast: {origin_ws: REQUIRED_RUNTIME_PLAYER_FEET_PLUS_PICKUP_REACH_OFFSET, endpoint_ws: [1.0, 0.0, 5.0], clear: true, layer_mask: World, query_trigger_policy: QueryTriggerInteraction.Ignore}
      patrol_corridor_clearance: {invariant: authoring_only, pickup_reach_radius_m: 1.6, patrol_agent_radius_m: 0.4, minimum_m: 2.0, result: REQUIRED_AUTHORED_CLEARANCE}
      clear_linecast: true
    expected_result: accepted
    telemetry_event: pickup-reached
    telemetry_only: true
    emits_noise_published: false
    actual: UNCAPTURED
  - interaction_id: pickup_interact_reserve_01
    pickup_id: burst_reserve_pickup_01
    reach_route_id: reserve_route_open_01
    trigger: Interact
    expected_precondition:
      pickup_state: Placed
      reachable: true
      player_feet_ws: REQUIRED_RUNTIME_PLAYER_FEET
      pickup_anchor_ws: [1.0, 0.0, 14.5]
      pickup_reach_radius_m: 1.6
      distance_squared_m2: REQUIRED_RUNTIME_DISTANCE_SQUARED
      distance_predicate: distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2
      distance_within_reach: true
      e20_linecast: {origin_ws: REQUIRED_RUNTIME_PLAYER_FEET_PLUS_PICKUP_REACH_OFFSET, endpoint_ws: [1.0, 0.0, 14.5], clear: true, layer_mask: World, query_trigger_policy: QueryTriggerInteraction.Ignore}
      patrol_corridor_clearance: {invariant: authoring_only, pickup_reach_radius_m: 1.6, patrol_agent_radius_m: 0.4, minimum_m: 2.0, result: REQUIRED_AUTHORED_CLEARANCE}
      clear_linecast: true
    expected_result: accepted
    telemetry_event: pickup-reached
    telemetry_only: true
    emits_noise_published: false
    actual: UNCAPTURED

pickup_state_transitions:
  - transition_id: pickup_state_gate_placed_to_carried_01
    interaction_id: pickup_interact_gate_01
    pickup_id: burst_gate_pickup_01
    from_state: Placed
    to_state: Carried
    expected_spend_committed: false
    expected_fact_id: null
    expected_noise_published: false
    actual: UNCAPTURED
  - transition_id: pickup_state_reserve_placed_to_carried_01
    interaction_id: pickup_interact_reserve_01
    pickup_id: burst_reserve_pickup_01
    from_state: Placed
    to_state: Carried
    expected_spend_committed: false
    expected_fact_id: null
    expected_noise_published: false
    actual: UNCAPTURED
```

A full-room restart is the only operation that restores authored `Placed` pickups.
Death and segment reset do not restore spent pickups. A carried, unthrown Burst
survives death; an in-flight Burst killed by death is `Consumed` with no noise.

## Geometry Variants and E20 Solids

Geometry variants make identical endpoints independently reproducible. A prose claim
that a wall exists, without a variant ID and explicit bounds, is invalid evidence.
Each positive, wall, ceiling, void, initial-overlap, and ambiguous-collision case owns
an isolated variant and stable solid IDs; no case may borrow a solid whose contact
semantics belong to another variant. In particular, `geometry_initial_overlap_01`
uses `world_initial_overlap_01` for the exact pre-spend overlap query, while
`geometry_gate_wall_01` is reserved for the consumed in-flight wall collision.

| Variant ID | Fixture geometry | Layer / trigger policy | Expected purpose |
|---|---|---|---|
| `geometry_receiver_clear_01` | authored room floor, ceiling, perimeter solids, and a physical Signal-A observation pocket; none intersects the declared clear receiver segment; no trigger intersects it | solids `World`; triggers ignored | clear hearing, clear Linecast, enclosed player route, and safe-vantage authoring |
| `geometry_receiver_wall_01` | solid `world_wall_receiver_01`, AABB min `(7.95,0.0,9.2)`, max `(8.05,3.0,9.3)` | `World`; `QueryTriggerInteraction.Ignore` | binary blocked hearing |
| `geometry_receiver_trigger_01` | trigger `receiver_trigger_01`, AABB min `(7.95,0.0,9.2)`, max `(8.05,3.0,9.3)` | `trigger`; ignored | prove trigger does not block |
| `geometry_gate_ceiling_01` | certified ceiling contact overhang `world_gate_ceiling_01` at `y∈[3.0,3.05]`, explicit room floor `world_gate_ceiling_floor_01`, grounded launch corridor beneath it | `World`; queryable | negative/failed-spend ceiling case |
| `geometry_gate_wall_01` | solid `world_gate_wall_01`, explicit room floor `world_gate_wall_floor_01`, AABB min `(4.0,0.0,5.95)`, max `(4.1,3.0,6.05)` | `World`; queryable | negative/failed-spend wall case |
| `geometry_initial_overlap_01` | separate solid `world_initial_overlap_01`, AABB min `(4.0,0.0,5.95)`, max `(4.1,3.0,6.05)` | `World`; queryable | pre-spend exact-overlap rejection; not a wall-flight case |
| `geometry_throw_flat_open_01` | certified launch corridor with the authored flat landing slab `world_throw_flat_landing_01`; no solid intersects before the landing contact envelope | `World`; triggers ignored | positive flat throw volume |
| `geometry_throw_lower_open_01` | certified launch corridor with the authored lower landing slab `world_throw_lower_landing_01`; no solid intersects before the landing contact envelope | `World`; triggers ignored | positive unequal-height throw volume |
| `geometry_throw_void_open_01` | no solids at all — an open volume whose flight has no authored floor, slab, wall, or ceiling anywhere along its path | `World`; triggers ignored | negative/failed-spend timeout-void case (harness-synthetic) |

```yaml
geometry_variants:
  - variant_id: geometry_receiver_clear_01
    source_records: [world_room_floor_receiver_01, world_room_ceiling_receiver_01, world_room_wall_w_receiver_01, world_room_wall_e_receiver_01, world_room_wall_s_receiver_01, world_room_wall_n_receiver_01, world_signal_a_cover_01, world_signal_a_exit_cover_01]
    expected_purpose: clear hearing and clear Linecast
    aabb_policy: playable_aabb
  - variant_id: geometry_receiver_wall_01
    source_records: [world_wall_receiver_01]
    expected_purpose: binary blocked hearing
    aabb_policy: playable_aabb
  - variant_id: geometry_receiver_trigger_01
    source_records: [receiver_trigger_01]
    expected_purpose: trigger ignored by Linecast
    aabb_policy: playable_aabb
  - variant_id: geometry_receiver_riser_01
    source_records: [world_riser_01]
    expected_purpose: low riser cleared by the raised movement-noise origin
    aabb_policy: playable_aabb
  - variant_id: geometry_gate_ceiling_01
    source_records: [world_gate_ceiling_01, world_gate_ceiling_floor_01]
    expected_purpose: ceiling failed-spend case
    aabb_policy: playable_aabb
  - variant_id: geometry_gate_wall_01
    source_records: [world_gate_wall_01, world_gate_wall_floor_01]
    expected_purpose: wall failed-spend case
    aabb_policy: playable_aabb
  - variant_id: geometry_initial_overlap_01
    source_records: [world_initial_overlap_01]
    expected_purpose: exact launch-overlap rejection before spend; independent from wall-flight geometry
    aabb_policy: playable_aabb
  - variant_id: geometry_throw_flat_open_01
    source_records: [world_throw_flat_landing_01]
    expected_purpose: positive flat throw volume with physical landing surface
    aabb_policy: playable_aabb
  - variant_id: geometry_throw_lower_open_01
    source_records: [world_throw_lower_landing_01]
    expected_purpose: positive unequal-height throw volume with physical landing surface
    aabb_policy: playable_aabb
  - variant_id: geometry_throw_void_open_01
    source_records: []
    expected_purpose: timeout-void failed-spend case with no authored collision geometry
    aabb_policy: diagnostic_fallback_exempt
  - variant_id: geometry_grazing_edge_01
    source_records: [world_grazing_edge_01]
    expected_purpose: Level-owned tangent/edge contact rejection; not an F4 root rejection
    aabb_policy: playable_aabb
  - variant_id: geometry_gate_closed_01
    source_records: [world_gate_closed_01]
    expected_purpose: pre-open physical gate obstruction
    aabb_policy: playable_aabb
  - variant_id: geometry_ambiguous_collision_01
    source_records: [world_ambiguous_a_01, world_ambiguous_b_01]
    expected_purpose: equal-distance closest-hit rejection
    aabb_policy: playable_aabb
solids:
  - id: world_room_floor_receiver_01
    variant_id: geometry_receiver_clear_01
    layer: World
    is_trigger: false
    surface_role: landing_surface
    surface_top_y_m: 0.0
    bounds_min: [0.0, -0.10, 0.0]
    bounds_max: [16.0, 0.0, 16.0]
  - id: world_room_ceiling_receiver_01
    variant_id: geometry_receiver_clear_01
    layer: World
    is_trigger: false
    surface_role: ceiling
    surface_top_y_m: 4.5
    bounds_min: [0.0, 4.45, 0.0]
    bounds_max: [16.0, 4.5, 16.0]
  - id: world_room_wall_w_receiver_01
    variant_id: geometry_receiver_clear_01
    layer: World
    is_trigger: false
    surface_role: occluder
    surface_top_y_m: null
    bounds_min: [-0.10, 0.0, 0.0]
    bounds_max: [0.0, 4.5, 16.0]
  - id: world_room_wall_e_receiver_01
    variant_id: geometry_receiver_clear_01
    layer: World
    is_trigger: false
    surface_role: occluder
    surface_top_y_m: null
    bounds_min: [16.0, 0.0, 0.0]
    bounds_max: [16.10, 4.5, 16.0]
  - id: world_room_wall_s_receiver_01
    variant_id: geometry_receiver_clear_01
    layer: World
    is_trigger: false
    surface_role: occluder
    surface_top_y_m: null
    bounds_min: [0.0, 0.0, -0.10]
    bounds_max: [16.0, 4.5, 0.0]
  - id: world_room_wall_n_receiver_01
    variant_id: geometry_receiver_clear_01
    layer: World
    is_trigger: false
    surface_role: occluder
    surface_top_y_m: null
    bounds_min: [0.0, 0.0, 16.0]
    bounds_max: [16.0, 4.5, 16.10]
  - id: world_signal_a_cover_01
    variant_id: geometry_receiver_clear_01
    layer: World
    is_trigger: false
    surface_role: occluder
    surface_top_y_m: null
    bounds_min: [3.60, 0.0, 7.30]
    bounds_max: [4.40, 1.60, 7.70]
  - id: world_signal_a_exit_cover_01
    variant_id: geometry_receiver_clear_01
    layer: World
    is_trigger: false
    surface_role: occluder
    surface_top_y_m: null
    bounds_min: [9.20, 0.0, 11.60]
    bounds_max: [9.60, 1.60, 12.40]
  - id: world_gate_wall_floor_01
    variant_id: geometry_gate_wall_01
    layer: World
    is_trigger: false
    surface_role: landing_surface
    surface_top_y_m: 0.0
    bounds_min: [0.0, -0.10, 0.0]
    bounds_max: [16.0, 0.0, 16.0]
  - id: world_gate_ceiling_floor_01
    variant_id: geometry_gate_ceiling_01
    layer: World
    is_trigger: false
    surface_role: landing_surface
    surface_top_y_m: 0.0
    bounds_min: [0.0, -0.10, 0.0]
    bounds_max: [16.0, 0.0, 16.0]
  - id: world_riser_01
    variant_id: geometry_receiver_riser_01
    layer: World
    is_trigger: false
    surface_role: occluder
    surface_top_y_m: 0.20
    bounds_min: [7.95, 0.0, 9.0]
    bounds_max: [8.05, 0.20, 9.20]
  - id: world_wall_receiver_01
    variant_id: geometry_receiver_wall_01
    layer: World
    is_trigger: false
    surface_role: occluder
    surface_top_y_m: null
    bounds_min: [7.95, 0.0, 9.2]
    bounds_max: [8.05, 3.0, 9.3]
  - id: world_gate_wall_01
    variant_id: geometry_gate_wall_01
    layer: World
    is_trigger: false
    surface_role: occluder
    surface_top_y_m: null
    bounds_min: [4.0, 0.0, 5.95]
    bounds_max: [4.1, 3.0, 6.05]
  - id: world_initial_overlap_01
    variant_id: geometry_initial_overlap_01
    layer: World
    is_trigger: false
    surface_role: occluder
    fixture_role: launch_overlap_blocker
    surface_top_y_m: null
    bounds_min: [4.0, 0.0, 5.95]
    bounds_max: [4.1, 3.0, 6.05]
  - id: world_gate_ceiling_01
    variant_id: geometry_gate_ceiling_01
    layer: World
    is_trigger: false
    surface_role: ceiling
    surface_top_y_m: 3.05
    bounds_min: [3.8, 3.0, 5.5]
    bounds_max: [10.0, 3.05, 10.0]
  - id: world_throw_flat_landing_01
    variant_id: geometry_throw_flat_open_01
    layer: World
    is_trigger: false
    surface_role: landing_surface
    surface_top_y_m: 0.0
    bounds_min: [1.5, -0.10, 7.0]
    bounds_max: [14.5, 0.0, 16.0]
  - id: world_throw_lower_landing_01
    variant_id: geometry_throw_lower_open_01
    layer: World
    is_trigger: false
    surface_role: landing_surface
    surface_top_y_m: 0.25
    bounds_min: [1.5, 0.15, 7.0]
    bounds_max: [14.5, 0.25, 16.0]
  - id: world_grazing_edge_01
    variant_id: geometry_grazing_edge_01
    layer: World
    is_trigger: false
    surface_role: occluder
    surface_top_y_m: 3.40
    bounds_min: [7.98, 3.30, 1.95]
    bounds_max: [8.02, 3.40, 2.05]
  - id: world_gate_closed_01
    variant_id: geometry_gate_closed_01
    layer: World
    is_trigger: false
    surface_role: occluder
    fixture_role: gate_closed_obstruction
    surface_top_y_m: 2.0
    bounds_min: [6.8, 0.0, 7.0]
    bounds_max: [7.0, 2.0, 9.0]
  - id: world_ambiguous_a_01
    variant_id: geometry_ambiguous_collision_01
    layer: World
    is_trigger: false
    surface_role: occluder
    fixture_role: collision_probe
    surface_top_y_m: null
    bounds_min: [5.0, 0.0, 7.95]
    bounds_max: [5.1, 2.0, 8.05]
  - id: world_ambiguous_b_01
    variant_id: geometry_ambiguous_collision_01
    layer: World
    is_trigger: false
    surface_role: occluder
    fixture_role: collision_probe
    surface_top_y_m: null
    bounds_min: [5.0, 0.0, 7.95]
    bounds_max: [5.1, 2.0, 8.05]
triggers:
  - id: receiver_trigger_01
    variant_id: geometry_receiver_trigger_01
    layer: trigger
    is_trigger: true
    bounds_min: [7.95, 0.0, 9.2]
    bounds_max: [8.05, 3.0, 9.3]
```

The two positive throw variants each contain a physical landing slab on the `World`
layer. The flat slab's top is `y=0.0 m` and the unequal-height slab's top is
`y=0.25 m`; their stable IDs are referenced by `landing_surface_id` in the valid
throw records. The resolver must observe the first closest-hit SphereCast contact
with that slab (or fail certification if another solid wins or the contact is
ambiguous), then publish the pushed-out contact center. An open volume without its
referenced physical landing solid is invalid positive evidence. The flat slab's
`bounds_min.y=-0.10` is an intentional ten-centimetre collider-thickness exception
below the playable room AABB floor at `y=0.0`; the room envelope remains
`room_aabb_min.y=0.0`, the authored surface remains `surface_top_y_m=0.0`, and
this below-floor thickness must not be treated as a second playable floor or as
permission to infer a negative player/guard datum.

The authored route-closure baseline is `geometry_receiver_clear_01`: its World floor,
ceiling, four perimeter solids, and `world_signal_a_cover_01` observation-pocket
solid are part of the fixture data, not prose-only assertions. Player traversal records
must use this enclosed baseline (or a variant with an equivalent explicit closure set).
The wall and ceiling failed-spend variants now carry explicit grounded floor solids;
`geometry_throw_void_open_01` remains the intentional floorless/open-volume diagnostic
and is never a playable route. The Signal-A vantages must be reachable from the closed
route and must retain a physical observation pocket while preserving the declared clear
linecast to the guard. These are authored prerequisites; their runtime/NavMesh/Physics
actuals remain `UNCAPTURED` until capture.

## Teaching Sequence

The route teaches the movement signal before asking the player to spend a limited
Burst. Records use the registry `teaching_sequence` field contract (rev 2026-08-31);
`actual: UNCAPTURED` is retained until runtime capture.

Two patrol phase windows govern the sequence (defined here, referenced by id):

- `patrol_settled_after_spawn_01` — `loop_index ≥ 1`, guard state `Patrol`, and the
  first patrol leg after spawn churn is complete. Qualifies the movement-teaching
  beats; no spend is permitted inside it. Its `min_validity_s` is `1.5 s` (starter
  rationale: `T_hearing 0.2 s` + two maximum `T_sample 0.5 s` FSM intervals +
  `0.3 s` scheduling margin).
- `patrol_pre_spend_01` — `loop_index ≥ 1`, guard state `Patrol`, no re-anchor or
  give-up timer in flight, and the guard on a leg away from the gate pickup
  corridor. Qualifies the certified spend beat: a re-anchor or give-up in flight at
  the spend moment voids the gate causal chain, so the spend must originate inside
  this window. Its `min_validity_s` is `2.24 s`: the phase starts at the accepted
  Throw edge and covers the certified lower-route flight upper bound
  `selected_t_land_s=1.4330082768164518 s`, `T_hearing=0.2 s`, the certified
  `T_fsm=0.3 s` response bound, and `0.3 s` scheduling margin through the gate
  decision. The exact authored lower bound is `1.4330082768164518 + 0.2 + 0.3 +
  0.3 = 2.2330082768164518 s`; `2.24 s` therefore retains `0.0069917231835482 s`
  of scheduling slack. These are expected timing operands, not captured runtime
  evidence.

```yaml
navmesh_layer_separation_check:
  - check_id: navmesh_e20_separation_01
    scene_id: REQUIRED_RUNTIME_SCENE_ID
    e20_world_layer_mask: REQUIRED_RUNTIME_E20_MASK
    navmesh_layer_ids: [REQUIRED_RUNTIME_NAVMESH_LAYER_IDS]
    intersecting_collider_ids: [REQUIRED_RUNTIME_COLLIDER_IDS]
    expected_separated: true
    actual: UNCAPTURED

patrol_phase_windows:
  - window_id: patrol_settled_after_spawn_01
    guard_eid: guard_mvp_01
    guard_state: Patrol
    conditions: [loop_index_gte_1, first_patrol_leg_complete]
    purpose: movement_teaching_beats
    min_validity_s: 1.5
    actual: UNCAPTURED
  - window_id: patrol_pre_spend_01
    guard_eid: guard_mvp_01
    guard_state: Patrol
    conditions: [loop_index_gte_1, no_reanchor_or_giveup_in_flight, leg_away_from_gate_pickup_corridor]
    purpose: certified_burst_spend_and_receiver_phase
    min_validity_s: 2.24
    actual: UNCAPTURED
```

The `patrol_phase_windows` collection is the resolver for
`patrol_phase_window` and `receiver_phase_window_id`; it is a declared content
contract, not a claim that either interval has been captured at runtime.

```yaml
teaching_sequence:
  - step_id: teach_spawn_01
    order: 1
    beat: spawn_checkpoint
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [1.0, 0.0, 1.0]
    player_route_samples: []
    player_observation_position_ws: [1.0, 0.0, 1.0]
    expected_stride_state: none
    expected_step_event_ids: []
    expected_rustle_fires: 0
    expected_guard_state: Patrol
    patrol_phase_window: patrol_settled_after_spawn_01
    expected_guard_observable_response: none_no_response
    expected_crouch_control_no_fact: null
    expected_teaching_outcome: checkpoint_spawned_player_placed
    pre_burst_requirement: null
    throw_stance: null
    actual: UNCAPTURED
  - step_id: teach_crouch_control_01
    order: 2
    beat: crouch_control
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [1.0, 0.0, 1.0]
    player_route_samples: [[1.0, 0.0, 1.0], [1.5, 0.0, 2.0]]
    player_observation_position_ws: [1.5, 0.0, 2.0]
    expected_stride_state: Crouch
    expected_step_event_ids: []
    expected_rustle_fires: 0
    expected_guard_state: Patrol
    patrol_phase_window: patrol_settled_after_spawn_01
    expected_guard_observable_response: none_no_response
    expected_crouch_control_no_fact: true
    expected_teaching_outcome: crouch_movement_emits_no_step_events_and_no_noise
    pre_burst_requirement: null
    throw_stance: null
    actual: UNCAPTURED
  - step_id: teach_walk_signal_01
    order: 3
    beat: walk_signal
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [1.0, 0.0, 1.0]
    player_route_samples: [[1.0, 0.0, 1.0], [1.5, 0.0, 2.0], [1.5, 0.0, 3.0]]
    player_observation_position_ws: [1.5, 0.0, 3.0]
    expected_stride_state: Walk
    expected_step_event_ids: [REQUIRED_RUNTIME_STEP_EVENT_IDS]
    expected_rustle_fires: 0
    expected_guard_state: Patrol
    patrol_phase_window: patrol_settled_after_spawn_01
    expected_guard_observable_response: none_no_response_raw_noise_published_only
    expected_crouch_control_no_fact: null
    expected_teaching_outcome: walking_emits_step_pips_published_as_raw_noise_outside_r_eff_no_fsm_response
    pre_burst_requirement: null
    throw_stance: null
    actual: UNCAPTURED
  - step_id: teach_run_signal_01
    order: 4
    beat: run_signal
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [1.0, 0.0, 1.0]
    player_route_samples: [[1.0, 0.0, 1.0], [1.5, 0.0, 2.0], [1.5, 0.0, 3.5]]
    player_observation_position_ws: [1.5, 0.0, 3.5]
    expected_stride_state: Run
    expected_step_event_ids: [REQUIRED_RUNTIME_STEP_EVENT_IDS]
    expected_rustle_fires: 0
    expected_guard_state: Patrol
    patrol_phase_window: patrol_settled_after_spawn_01
    expected_guard_observable_response: none_no_response_raw_noise_published_only
    expected_crouch_control_no_fact: null
    expected_teaching_outcome: running_emits_faster_louder_pips_published_as_raw_noise_outside_r_eff_no_fsm_response
    pre_burst_requirement: null
    throw_stance: null
    actual: UNCAPTURED
  - step_id: teach_gate_pickup_01
    order: 5
    beat: pickup_burst
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [1.0, 0.0, 1.0]
    player_route_samples: [[1.0, 0.0, 1.0], [1.0, 0.0, 3.0], [1.0, 0.0, 5.0]]
    player_observation_position_ws: [1.0, 0.0, 5.0]
    expected_stride_state: Walk
    expected_step_event_ids: [REQUIRED_RUNTIME_STEP_EVENT_IDS]
    expected_rustle_fires: 0
    expected_guard_state: Patrol
    patrol_phase_window: patrol_settled_after_spawn_01
    expected_guard_observable_response: none_no_response_pickup_emits_no_noise_published
    expected_crouch_control_no_fact: null
    expected_teaching_outcome: burst_carried_via_reachable_interact_with_no_noise_published
    pre_burst_requirement: {walk_signal_observed: teach_walk_signal_01, run_signal_observed: teach_run_signal_01, crouch_control_observed: teach_crouch_control_01}
    actual: UNCAPTURED
  - step_id: teach_certified_throw_01
    order: 6
    beat: burst_spend_certified
    geometry_variant_id: geometry_throw_lower_open_01
    landing_surface_id: world_throw_lower_landing_01
    player_start_position_ws: [1.0, 0.0, 5.0]
    player_route_samples: [[1.0, 0.0, 5.0], [2.0, 0.0, 6.0], [3.0, 0.0, 7.0], [4.0, 0.0, 7.5]]
    player_observation_position_ws: [4.0, 0.0, 7.5]
    expected_stride_state: none
    expected_step_event_ids: []
    expected_rustle_fires: 0
    expected_guard_state: Patrol
    patrol_phase_window: patrol_pre_spend_01
    expected_guard_observable_response: orient_tell_toward_captured_landing_then_investigate_commit
    expected_crouch_control_no_fact: null
    expected_teaching_outcome: burst_lands_noise_heard_guard_orients_observable_from_vantage
    pre_burst_requirement: {carried_burst: true, guard_state: Patrol, no_reanchor_or_giveup_in_flight: true, phase_window_id: patrol_pre_spend_01}
    throw_stance: stationary_idle_or_successful_crouch_stand
    actual: UNCAPTURED
  - step_id: teach_gate_open_01
    order: 7
    beat: gate_open_verification
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [4.0, 0.0, 7.5]
    player_route_samples: [[4.0, 0.0, 7.5], [5.0, 0.0, 7.5], [5.5, 0.0, 8.0]]
    player_observation_position_ws: [5.5, 0.0, 8.0]
    expected_stride_state: none
    expected_step_event_ids: []
    expected_rustle_fires: 0
    expected_guard_state: Investigate
    patrol_phase_window: null
    expected_guard_observable_response: gate_state_blocked_to_open_observed_after_investigate_commit
    expected_crouch_control_no_fact: null
    expected_teaching_outcome: gate_causal_chain_completed_from_the_pre_spend_spend_beat
    pre_burst_requirement: null
    throw_stance: null
    actual: UNCAPTURED
  - step_id: teach_exit_01
    order: 8
    beat: gate_exit_reach
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [5.5, 0.0, 8.0]
    player_route_samples: [[5.5, 0.0, 8.0], [8.0, 0.0, 8.0], [12.0, 0.0, 8.0]]
    player_observation_position_ws: [12.0, 0.0, 8.0]
    expected_stride_state: Walk
    expected_step_event_ids: [REQUIRED_RUNTIME_STEP_EVENT_IDS]
    expected_rustle_fires: 0
    expected_guard_state: Investigate
    patrol_phase_window: null
    expected_guard_observable_response: none_no_response_route_stays_outside_r_eff_of_the_investigation_zone
    expected_crouch_control_no_fact: null
    expected_teaching_outcome: gate_exit_route_walked_to_reachable_exit
    pre_burst_requirement: null
    throw_stance: null
    actual: UNCAPTURED
```

`noise_heard` in the emission-only teaching beats means the raw `NoisePublished`
emission event, not a guard relay. The Crouch beat proves that no step event or
noise fact is emitted; the Walk and Run beats demonstrate raw pip cadence outside
the guard's effective hearing radius, so they intentionally require no relay or
FSM response. A visible guard response is required only for a hearing/response
beat, and is demonstrated by the certified Burst-spend beat inside
`patrol_pre_spend_01`. A `Burst` may not be spent before the Walk and Run
demonstrations have been observed (orders 3 and 4 precede the spend at order 6).
Out-of-order or missing steps fail with `TEACHING_SEQUENCE_OUT_OF_ORDER`; this
ordering is a content requirement, not a claim that runtime evidence exists.

## Signal-A Visibility

Signal-A is the player-facing post-throw vantage: the guard's orientation response
must be observable from a clear line-of-sight vantage without a persistent HUD. Each
vantage resolves an authored `signal_a_observation_pockets` record with its own cover
solid, geometry variant, bounds, entry route, exit route, and player clearance/
containment predicates; a point coordinate plus a declared clear Linecast is not
sufficient authoring. The player route must enter the bound pocket from the certified
throw approach and leave it through the exit route without re-entering the gate
corridor or any bound extended-search volume. The guard response window is bounded by
the registered FSM tick and remains an uncaptured runtime assertion.

```yaml
signal_a_observation_pockets:
  - pocket_id: signal_a_pocket_gate_01
    cover_solid_id: world_signal_a_cover_01
    geometry_variant_id: geometry_receiver_clear_01
    pocket_bounds_min: [3.60, 0.0, 6.40]
    pocket_bounds_max: [4.40, 1.80, 7.20]
    entry_route_id: gate_route_open_01
    exit_route_id: gate_route_open_01
    player_clearance_predicate: "swept_player_disc(radius=0.30m, route_samples) remains inside pocket_bounds and does not intersect cover_solid_id"
    containment_predicate: "every captured observation sample is inside pocket_bounds in XZ with player_agent_radius_m clearance from the XZ boundary; floor_contact_valid(sample) is asserted separately"
    floor_contact_predicate: "floor_contact_valid(sample): supporting World surface is present within the registered floor-contact tolerance; Y is not used as an AABB containment margin"
    actual: UNCAPTURED
  - pocket_id: signal_a_pocket_exit_01
    cover_solid_id: world_signal_a_exit_cover_01
    geometry_variant_id: geometry_receiver_clear_01
    pocket_bounds_min: [9.60, 0.0, 10.80]
    pocket_bounds_max: [10.40, 1.80, 11.60]
    entry_route_id: gate_exit_reachable_after_burst_01
    exit_route_id: gate_exit_reachable_after_burst_01
    player_clearance_predicate: "swept_player_disc(radius=0.30m, route_samples) remains inside pocket_bounds and does not intersect cover_solid_id"
    containment_predicate: "every captured observation sample is inside pocket_bounds in XZ with player_agent_radius_m clearance from the XZ boundary; floor_contact_valid(sample) is asserted separately"
    floor_contact_predicate: "floor_contact_valid(sample): supporting World surface is present within the registered floor-contact tolerance; Y is not used as an AABB containment margin"
    actual: UNCAPTURED

signal_a_visibility:
  - record_id: signal_a_vantage_gate_01
    causality_id: gate_burst_to_exit_01
    observation_pocket_id: signal_a_pocket_gate_01
    vantage_position_ws: [4.0, 0.0, 6.8]
    vantage_to_guard_linecast_clear: true
    expected_guard_initial_state: Patrol
    patrol_phase: patrol_pre_spend_01
    source_timestamp: REQUIRED_RUNTIME_SOURCE_TIMESTAMP
    expected_burst_landing_position_ws: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    expected_noise_published_fact_id: REQUIRED_RUNTIME_FACT_ID
    expected_noise_heard_relay_id: REQUIRED_RUNTIME_FACT_ID
    expected_investigate_commit_id: REQUIRED_RUNTIME_DECISION_ID
    expected_orient_tell_window_s: {lower_bound_s: 0.0, upper_bound_s: REQUIRED_RUNTIME_FSM_TICK_S}
    expected_fsm_tick_bound_s: REQUIRED_RUNTIME_FSM_TICK_S
    visibility_exclusions: [HideSpotFront, Chase, cooldown, stale_epoch]
    expected_visible_causal_chain: [burst_landed, NoisePublished, NoiseHeardRelay, investigate_commit, orient_tell, investigate_movement]
    actual: UNCAPTURED
  - record_id: signal_a_vantage_exit_01
    causality_id: gate_burst_to_exit_01
    observation_pocket_id: signal_a_pocket_exit_01
    vantage_position_ws: [10.0, 0.0, 11.4]
    vantage_to_guard_linecast_clear: true
    expected_guard_initial_state: Patrol
    patrol_phase: patrol_pre_spend_01
    source_timestamp: REQUIRED_RUNTIME_SOURCE_TIMESTAMP
    expected_burst_landing_position_ws: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    expected_noise_published_fact_id: REQUIRED_RUNTIME_FACT_ID
    expected_noise_heard_relay_id: REQUIRED_RUNTIME_FACT_ID
    expected_investigate_commit_id: REQUIRED_RUNTIME_DECISION_ID
    expected_orient_tell_window_s: {lower_bound_s: 0.0, upper_bound_s: REQUIRED_RUNTIME_FSM_TICK_S}
    expected_fsm_tick_bound_s: REQUIRED_RUNTIME_FSM_TICK_S
    visibility_exclusions: [HideSpotFront, Chase, cooldown, stale_epoch]
    expected_visible_causal_chain: [burst_landed, NoisePublished, NoiseHeardRelay, investigate_commit, orient_tell, investigate_movement]
    actual: UNCAPTURED
```

A missing clear vantage-to-guard LOS, an unbounded orient tell, or a missing
noise-caused `investigate-commit` fails with `SIGNAL_A_VISIBILITY_UNPROVEN`. The
`expected_burst_landing_position_ws` of every record is the referenced chain's
captured published landing — never an independently authored or synthetic source —
and the visible chain is asserted end-to-end: `burst_landed` → `NoisePublished` →
`NoiseHeardRelay` → `investigate-commit` → `orient_tell` → visible guard orientation
followed by Investigate movement from the episode-open commit position. `NoisePublished`
is the raw-fact identity boundary; the relay is the per-guard delivery boundary.

## Reserve Safety and Extended Search

The reserve pickup is the recovery route after failed gate spends. It must remain
outside the guard's extended search area while still reachable from the checkpoint.

```yaml
guard_extended_search_patrol:
  - zone_id: reanchor_search_zone_failed_wall_01
    causality_case_ids: [failed_wall_01]
    noise_origin_ws: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    investigation_zone_bounds: REQUIRED_RUNTIME_INVESTIGATION_ZONE_BOUNDS
    patrol_bounds: {route_nodes: [[4.0, 0.0, 8.0], [8.0, 0.0, 8.0], [8.0, 0.0, 12.0], [4.0, 0.0, 12.0]]}
    search_volume_geometry_id: REQUIRED_RUNTIME_SEARCH_VOLUME_GEOMETRY_ID
    guard_agent_radius_m: 0.40
    reserve_route_id: reserve_route_after_failed_spends_01
    clearance_predicate: "continuous swept_player_disc(radius=0.30m) over reserve_route_id has no intersection with search_volume_geometry_id expanded by guard_agent_radius_m"
    time_sampling_rule: fixed_virtual_tick_boundaries_from_reanchor_start_through_expected_reanchor_extend_s
    expected_reanchor_extend_s: REQUIRED_RUNTIME_REANCHOR_PATROL_EXTEND_S
    s_diff_resolved: 1.0
    visibility_window_s: REQUIRED_RUNTIME_RESERVE_VISIBILITY_WINDOW
    reserve_route_clear_during_search: true
    actual: UNCAPTURED
  - zone_id: reanchor_search_zone_failed_ceiling_01
    causality_case_ids: [failed_ceiling_01]
    noise_origin_ws: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    investigation_zone_bounds: REQUIRED_RUNTIME_INVESTIGATION_ZONE_BOUNDS
    patrol_bounds: {route_nodes: [[4.0, 0.0, 8.0], [8.0, 0.0, 8.0], [8.0, 0.0, 12.0], [4.0, 0.0, 12.0]]}
    search_volume_geometry_id: REQUIRED_RUNTIME_SEARCH_VOLUME_GEOMETRY_ID
    guard_agent_radius_m: 0.40
    reserve_route_id: reserve_route_after_failed_spends_01
    clearance_predicate: "continuous swept_player_disc(radius=0.30m) over reserve_route_id has no intersection with search_volume_geometry_id expanded by guard_agent_radius_m"
    time_sampling_rule: fixed_virtual_tick_boundaries_from_reanchor_start_through_expected_reanchor_extend_s
    expected_reanchor_extend_s: REQUIRED_RUNTIME_REANCHOR_PATROL_EXTEND_S
    s_diff_resolved: 1.0
    visibility_window_s: REQUIRED_RUNTIME_RESERVE_VISIBILITY_WINDOW
    reserve_route_clear_during_search: true
    actual: UNCAPTURED
  - zone_id: reanchor_search_zone_failed_void_01
    causality_case_ids: [failed_void_01]
    noise_origin_ws: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    investigation_zone_bounds: REQUIRED_RUNTIME_INVESTIGATION_ZONE_BOUNDS
    patrol_bounds: {route_nodes: [[4.0, 0.0, 8.0], [8.0, 0.0, 8.0], [8.0, 0.0, 12.0], [4.0, 0.0, 12.0]]}
    search_volume_geometry_id: REQUIRED_RUNTIME_SEARCH_VOLUME_GEOMETRY_ID
    guard_agent_radius_m: 0.40
    reserve_route_id: reserve_route_after_failed_spends_01
    clearance_predicate: "continuous swept_player_disc(radius=0.30m) over reserve_route_id has no intersection with search_volume_geometry_id expanded by guard_agent_radius_m"
    time_sampling_rule: fixed_virtual_tick_boundaries_from_reanchor_start_through_expected_reanchor_extend_s
    expected_reanchor_extend_s: REQUIRED_RUNTIME_REANCHOR_PATROL_EXTEND_S
    s_diff_resolved: 1.0
    visibility_window_s: REQUIRED_RUNTIME_RESERVE_VISIBILITY_WINDOW
    reserve_route_clear_during_search: true
    actual: UNCAPTURED
  - zone_id: reanchor_search_zone_failed_death_01
    causality_case_ids: [failed_death_01]
    noise_origin_ws: null
    investigation_zone_bounds: null
    patrol_bounds: null
    search_volume_geometry_id: null
    guard_agent_radius_m: 0.40
    reserve_route_id: reserve_route_after_failed_spends_01
    clearance_predicate: "not_applicable_when_death_cancels_before_investigation"
    time_sampling_rule: no_search_window
    expected_reanchor_extend_s: 0.0
    s_diff_resolved: null
    visibility_window_s: 0.0
    reserve_route_clear_during_search: true
    actual: UNCAPTURED
```

Each failed-spend case carries its own search-zone record (rev 2026-09-01): the zone
bounds derive from that case's own published position at capture — the wall and
ceiling cases publish at their collision contact, the void case at its
`last_simulated_sphere_center` fallback, and the death case publishes nothing, so its
record resolves with `reanchor_patrol_extend_s=0`, `reserve_visibility_window=0`, and no
investigation window; its required boolean `reanchor_patrol_clear=true` is vacuous and
never asserts a search zone — never from another case's geometry.
The certified diagonal landings — `(≈ 9.66, 0.0, ≈ 14.16)` flat and
`(≈ 9.48, 0.25, ≈ 13.98)` unequal-height (rev 2026-09-01) — remain the
certified-route envelope reference. The reserve route (`x = 2.0`, `z ∈ [10.5, 14.0]`)
stays at least `≈ 3.5 m` outside the certified-route envelope's minimum `x` bound, so
`reserve_route_clear_during_search` is satisfiable by construction.
`expected_reanchor_extend_s` is computed from the canonical `reanchor_giveup_timeout`
formula with the level's resolved difficulty scalar `s_diff_resolved = 1.0` (the
S_DIFF speed-ratio form is struck rev 2026-08-31); `visibility_window_s` must cover
the full `expected_reanchor_extend_s` window. A reserve route intersecting the
guard's extended search during that window is a certification failure, not a player
routing failure.

## Restart Transaction

After both Bursts are spent, the discoverable pause-menu `Restart Room` action (and
its contextual post-double-spend prompt) invokes one atomic transaction. No partial
pickup or gate reset is valid evidence.

```yaml
restart_transaction:
  transaction_id: full_room_restart_transaction_01
  trigger: [pause_menu_restart_room, post_double_spend_prompt]
  prior_state: REQUIRED_RUNTIME_PRIOR_STATE
  ordered_steps:
    - clear_carried_slot
    - restore_all_authored_pickups_to_Placed
    - reset_gate_and_exit_markers
    - discard_prior_epoch_work
    - clear_residual_state
    - start_fresh_session_envelope
  all_or_nothing: true
  canceled_flight_ids: [REQUIRED_RUNTIME_FLIGHT_HANDLE_ID]
  canceled_fact_ids: [REQUIRED_RUNTIME_FACT_ID]
  gate_state_after: blocked
  exit_state_after: unreachable
  pickup_states_after: {burst_gate_pickup_01: Placed, burst_reserve_pickup_01: Placed}
  carried_slot_after: empty
  session_envelope_after: {session_id: REQUIRED_RUNTIME_SESSION_ID, fresh_envelope: true}
  attempt_epoch_after: 1
  residual_state_after: cleared
  expected_recovery_outcome: {both_pickups_Placed: true, carried_slot_cleared: true, attempt_epoch: 1, gate_and_exit_at_authored_initial_states: true, residual_state_cleared: true}
  lifecycle_disambiguation:
    segment_reset: {epoch_transition: exactly_one_increment, pickup_restore: false, carried_slot_preserved: true, source_event_id: REQUIRED_RUNTIME_EPOCH_TRANSITION_ID, fact_id: not_applicable, relay_fact_id: not_applicable, decision_id: not_applicable}
    death_cancellation: {terminal_cause: DeathCancelled, epoch_transition: exactly_one_increment, terminal_publication: none, source_event_id: "flight:REQUIRED_RUNTIME_FLIGHT_HANDLE_ID", fact_id: null, relay_fact_id: none, decision_id: none}
    terminal_publication: {collision_or_timeout_only: true, publication_time: REQUIRED_RUNTIME_TERMINAL_PUBLICATION_TIME, source_event_id: "flight:REQUIRED_RUNTIME_FLIGHT_HANDLE_ID", fact_id: REQUIRED_RUNTIME_FACT_ID, relay_fact_id: REQUIRED_RUNTIME_RELAY_FACT_ID, decision_id: REQUIRED_RUNTIME_DECISION_ID}
    full_room_restart: {new_session_id: REQUIRED_RUNTIME_SESSION_ID, fresh_attempt_epoch: 1, pickup_restore: true, carried_slot_cleared: true, prior_epoch_work: discarded}
  actual: UNCAPTURED
```

Malformed or partially applied recovery fails with `RESTART_TRANSACTION_MALFORMED`
or `RESTART_NOT_ATOMIC`; a failed transaction never opens the gate or reports a
successful recovery. The lifecycle records keep segment reset, death cancellation,
terminal publication, and full-room restart distinct: a segment reset increments the
epoch but does not restore a spent pickup, death cancellation consumes an in-flight
Burst without publishing a fact, terminal publication is reserved for Collision or
Timeout, and full-room restart creates a fresh session and restores authored pickups.
When a captured episode continues from Investigate into Chase, its liveness record is
`op: promote` and retains the existing `entry_id` (Perception episode identity); it carries the
source, fact, relay, and decision identities and never models the transition as a
close/open pair. The MVP gate chain below ends at Investigate, so it does not invent a
Chase promotion or runtime identity; its promotion contract remains uncaptured.

## Reserve Route Evidence

The expected route is:

```text
checkpoint_mvp_01
  → reserve_approach_01 (1.0, 0.0, 10.5)
  → reserve_approach_02 (1.0, 0.0, 13.0)
  → burst_reserve_pickup_01 (1.0, 0.0, 14.5)
```

The route record must serialize the **player NavMesh agent radius** used by the
route query, the named NavMesh area set and its bitmask, path status, sampled
corners, stable path hash, pickup reach result, and the E20 semantic line probe.
`player_agent_radius_m=0.30` belongs only to player route traversal; registry
`r_player=0.35` remains the separate player collision-radius operand of the F10
catch invariant. The pickup corridor clearance uses the separately authored
`patrol_agent_radius_m=0.40` (the guard-body clearance value) and must not be
substituted with either player value. These radii must not be substituted for one
another. The
expected fields below are not a captured pass; `actual` remains `UNCAPTURED`.
The closed-room traversal contract is directional: the player approaches the gate
pickup and Signal-A pocket through the authored route, performs the stationary Throw,
then exits through `burst_gate_exit_01` on the open route. The exit route may cross the
closed-gate corridor exactly once during the captured post-Burst gate transition, then
must remain reachable without re-entering that corridor or the guard's extended-search
envelope; this is a content invariant to be proven by the route and search records, not
an inferred consequence of the AABB. Each route-safety record must also serialize the
active geometry overlay state, `tolerance_class`, distinct physical player-disc and
NavMesh agent radii, the authorized subpath and segment indices (when crossing is
allowed), the transition tick/time interval, entry and exit bounds, and the captured
crossing result. These actual crossing fields remain `UNCAPTURED` until runtime route
capture.

```yaml
route_safety_predicates:
  - predicate_id: route_safety_gate_closed_01
    route_id: gate_route_closed_01
    geometry_binding_id: geometry_receiver_clear_01
    geometry_overlay_ids: [geometry_gate_closed_01]
    forbidden_gate_corridor: {source_solid_id: world_gate_closed_01, expansion_radius_m: 0.30, state: blocked}
    extended_search_zone_ids: []
    physical_player_disc_radius_m: 0.30
    navmesh_agent_radius_m: 0.40
    swept_player_disc_radius_m: 0.30
    active_geometry_overlay_state: REQUIRED_RUNTIME_ACTIVE_GEOMETRY_OVERLAY
    tolerance_class: gameplay_contract
    authorized_subpath: null
    authorized_segment_indices: []
    transition_tick_interval: null
    entry_bounds: null
    exit_bounds: null
    crossing_result: REQUIRED_RUNTIME_CROSSING_RESULT
    boundary_semantics: exclusive
    boundary_tolerance_m: 0.001
    sampling_rule: continuous_polyline_sweep_between_path_samples
    intersection_rule: no_intersection_with_forbidden_gate_corridor
    expected_no_reentry: true
    actual: UNCAPTURED
  - predicate_id: route_safety_gate_approach_01
    route_id: gate_route_open_01
    geometry_binding_id: geometry_receiver_clear_01
    geometry_overlay_ids: [geometry_gate_closed_01]
    forbidden_gate_corridor: {source_solid_id: world_gate_closed_01, expansion_radius_m: 0.30, state: blocked}
    extended_search_zone_ids: []
    physical_player_disc_radius_m: 0.30
    navmesh_agent_radius_m: 0.40
    swept_player_disc_radius_m: 0.30
    active_geometry_overlay_state: REQUIRED_RUNTIME_ACTIVE_GEOMETRY_OVERLAY
    tolerance_class: gameplay_contract
    authorized_subpath: null
    authorized_segment_indices: []
    transition_tick_interval: null
    entry_bounds: null
    exit_bounds: null
    crossing_result: REQUIRED_RUNTIME_CROSSING_RESULT
    boundary_semantics: exclusive
    boundary_tolerance_m: 0.001
    sampling_rule: continuous_polyline_sweep_between_path_samples
    intersection_rule: no_intersection_with_forbidden_gate_corridor
    expected_no_reentry: true
    actual: UNCAPTURED
  - predicate_id: route_safety_exit_non_reentry_01
    route_id: gate_exit_reachable_after_burst_01
    geometry_binding_id: geometry_receiver_clear_01
    geometry_overlay_ids: [geometry_gate_closed_01]
    forbidden_gate_corridor: {source_solid_id: world_gate_closed_01, expansion_radius_m: 0.30, state: blocked, authorized_crossing: {transition_id: gate_state_blocked_to_open_01, direction: burst_gate_entry_to_burst_gate_exit, max_crossings: 1, crossing_phase: post_transition_only}}
    extended_search_zone_ids: [reanchor_search_zone_failed_wall_01, reanchor_search_zone_failed_ceiling_01, reanchor_search_zone_failed_void_01]
    physical_player_disc_radius_m: 0.30
    navmesh_agent_radius_m: 0.40
    swept_player_disc_radius_m: 0.30
    active_geometry_overlay_state: REQUIRED_RUNTIME_ACTIVE_GEOMETRY_OVERLAY
    tolerance_class: gameplay_contract
    authorized_subpath: REQUIRED_RUNTIME_AUTHORIZED_GATE_CROSSING_SUBPATH
    authorized_segment_indices: REQUIRED_RUNTIME_AUTHORIZED_SEGMENT_INDICES
    transition_tick_interval: REQUIRED_RUNTIME_GATE_TRANSITION_TICK_INTERVAL
    entry_bounds: REQUIRED_RUNTIME_CROSSING_ENTRY_BOUNDS
    exit_bounds: REQUIRED_RUNTIME_CROSSING_EXIT_BOUNDS
    crossing_result: REQUIRED_RUNTIME_CROSSING_RESULT
    boundary_semantics: exclusive
    boundary_tolerance_m: 0.001
    sampling_rule: continuous_polyline_sweep_between_path_samples_at_fixed_virtual_tick_boundaries
    intersection_rule: exactly_one_authorized_gate_crossing_then_no_reentry_into_forbidden_gate_corridor_or_any_bound_search_volume
    expected_no_reentry: true
    actual: UNCAPTURED
  - predicate_id: route_safety_reserve_01
    route_id: reserve_route_open_01
    geometry_binding_id: geometry_receiver_clear_01
    geometry_overlay_ids: []
    forbidden_gate_corridor: {source_solid_id: world_gate_closed_01, expansion_radius_m: 0.30, state: blocked}
    extended_search_zone_ids: []
    physical_player_disc_radius_m: 0.30
    navmesh_agent_radius_m: 0.40
    swept_player_disc_radius_m: 0.30
    active_geometry_overlay_state: REQUIRED_RUNTIME_ACTIVE_GEOMETRY_OVERLAY
    tolerance_class: gameplay_contract
    authorized_subpath: null
    authorized_segment_indices: []
    transition_tick_interval: null
    entry_bounds: null
    exit_bounds: null
    crossing_result: REQUIRED_RUNTIME_CROSSING_RESULT
    boundary_semantics: exclusive
    boundary_tolerance_m: 0.001
    sampling_rule: continuous_polyline_sweep_between_path_samples
    intersection_rule: no_intersection_with_forbidden_gate_corridor
    expected_no_reentry: true
    actual: UNCAPTURED
  - predicate_id: route_safety_reserve_after_failed_spends_01
    route_id: reserve_route_after_failed_spends_01
    geometry_binding_id: geometry_receiver_clear_01
    geometry_overlay_ids: [geometry_gate_closed_01]
    forbidden_gate_corridor: {source_solid_id: world_gate_closed_01, expansion_radius_m: 0.30, state: blocked}
    extended_search_zone_ids: [reanchor_search_zone_failed_wall_01, reanchor_search_zone_failed_ceiling_01, reanchor_search_zone_failed_void_01]
    physical_player_disc_radius_m: 0.30
    navmesh_agent_radius_m: 0.40
    swept_player_disc_radius_m: 0.30
    active_geometry_overlay_state: REQUIRED_RUNTIME_ACTIVE_GEOMETRY_OVERLAY
    tolerance_class: gameplay_contract
    authorized_subpath: null
    authorized_segment_indices: []
    transition_tick_interval: null
    entry_bounds: null
    exit_bounds: null
    crossing_result: REQUIRED_RUNTIME_CROSSING_RESULT
    boundary_semantics: exclusive
    boundary_tolerance_m: 0.001
    sampling_rule: continuous_polyline_sweep_between_path_samples_at_fixed_virtual_tick_boundaries
    intersection_rule: no_intersection_with_forbidden_gate_corridor_or_any_bound_search_volume
    expected_no_reentry: true
    actual: UNCAPTURED

route_evidence:
  - route_id: gate_route_closed_01
    geometry_binding_id: geometry_receiver_clear_01
    geometry_overlay_ids: [geometry_gate_closed_01]
    route_safety_predicate_id: route_safety_gate_closed_01
    from_marker: burst_gate_entry_01
    to_marker: burst_gate_exit_01
    obstruction_ids: [world_gate_closed_01]
    player_agent_radius_m: 0.30
    navmesh_area_mask: 1
    navmesh_area_names: [Walkable]
    navmesh_area_mapping_id: navmesh_area_mapping_mvp
    expected_path_status: PathPartial
    path_complete: false
    path_samples: [[5.5,0.0,8.0], [6.1,0.0,8.0]]
    stable_path_hash: REQUIRED_RUNTIME_HASH
    tolerance_class: gameplay_contract
    pickup_reach:
      reachable: true
      telemetry_event: pickup-reached
      telemetry_only: true
      player_feet_ws: REQUIRED_RUNTIME_PLAYER_FEET
      pickup_anchor_ws: [1.0, 0.0, 5.0]
      pickup_reach_radius_m: 1.6
      distance_squared_m2: REQUIRED_RUNTIME_DISTANCE_SQUARED
      distance_predicate: distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2
      distance_within_reach: true
      e20_linecast: {origin_ws: REQUIRED_RUNTIME_PLAYER_FEET_PLUS_PICKUP_REACH_OFFSET, endpoint_ws: [1.0, 0.0, 5.0], clear: true, layer_mask: World, query_trigger_policy: QueryTriggerInteraction.Ignore}
    e20_linecast: {clear: false, trigger_policy: QueryTriggerInteraction.Ignore}
    gate_state: blocked
    failed_gate_spend: false
    required_failed_cases: []
    actual: UNCAPTURED
  - route_id: gate_route_open_01
    geometry_binding_id: geometry_receiver_clear_01
    geometry_overlay_ids: [geometry_gate_closed_01]
    route_safety_predicate_id: route_safety_gate_approach_01
    from_marker: checkpoint_mvp_01
    to_marker: burst_gate_pickup_01
    obstruction_ids: []
    player_agent_radius_m: 0.30
    navmesh_area_mask: 1
    navmesh_area_names: [Walkable]
    navmesh_area_mapping_id: navmesh_area_mapping_mvp
    expected_path_status: PathComplete
    path_complete: true
    path_samples: [[1.0,0.0,1.0], [1.0,0.0,3.0], [1.0,0.0,5.0], [4.0,0.0,7.5], [1.0,0.0,5.0]]
    stable_path_hash: REQUIRED_RUNTIME_HASH
    tolerance_class: gameplay_contract
    pickup_reach:
      reachable: true
      telemetry_event: pickup-reached
      telemetry_only: true
      player_feet_ws: REQUIRED_RUNTIME_PLAYER_FEET
      pickup_anchor_ws: [1.0, 0.0, 5.0]
      pickup_reach_radius_m: 1.6
      distance_squared_m2: REQUIRED_RUNTIME_DISTANCE_SQUARED
      distance_predicate: distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2
      distance_within_reach: true
      e20_linecast: {origin_ws: REQUIRED_RUNTIME_PLAYER_FEET_PLUS_PICKUP_REACH_OFFSET, endpoint_ws: [1.0, 0.0, 5.0], clear: true, layer_mask: World, query_trigger_policy: QueryTriggerInteraction.Ignore}
    e20_linecast: {clear: true, trigger_policy: QueryTriggerInteraction.Ignore}
    gate_state: closed
    failed_gate_spend: false
    required_failed_cases: []
    actual: UNCAPTURED
  - route_id: gate_exit_reachable_after_burst_01
    geometry_binding_id: geometry_receiver_clear_01
    geometry_overlay_ids: [geometry_gate_closed_01]
    route_safety_predicate_id: route_safety_exit_non_reentry_01
    from_marker: burst_gate_entry_01
    to_marker: burst_gate_exit_01
    obstruction_ids: []
    player_agent_radius_m: 0.30
    navmesh_area_mask: 1
    navmesh_area_names: [Walkable]
    navmesh_area_mapping_id: navmesh_area_mapping_mvp
    expected_path_status: PathComplete
    path_complete: true
    path_samples: [[5.5,0.0,8.0], [8.0,0.0,8.0], [10.0,0.0,11.4], [12.0,0.0,8.0]]
    stable_path_hash: REQUIRED_RUNTIME_HASH
    tolerance_class: gameplay_contract
    pickup_reach:
      reachable: true
      telemetry_event: pickup-reached
      telemetry_only: true
      player_feet_ws: REQUIRED_RUNTIME_PLAYER_FEET
      pickup_anchor_ws: [1.0, 0.0, 5.0]
      pickup_reach_radius_m: 1.6
      distance_squared_m2: REQUIRED_RUNTIME_DISTANCE_SQUARED
      distance_predicate: distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2
      distance_within_reach: true
      e20_linecast: {origin_ws: REQUIRED_RUNTIME_PLAYER_FEET_PLUS_PICKUP_REACH_OFFSET, endpoint_ws: [1.0, 0.0, 5.0], clear: true, layer_mask: World, query_trigger_policy: QueryTriggerInteraction.Ignore}
    e20_linecast: {clear: true, trigger_policy: QueryTriggerInteraction.Ignore}
    gate_state: open
    failed_gate_spend: false
    required_failed_cases: []
    actual: UNCAPTURED
  - route_id: reserve_route_open_01
    geometry_binding_id: geometry_receiver_clear_01
    geometry_overlay_ids: []
    route_safety_predicate_id: route_safety_reserve_01
    from_marker: checkpoint_mvp_01
    to_marker: burst_reserve_pickup_01
    obstruction_ids: []
    player_agent_radius_m: 0.30
    navmesh_area_mask: 1
    navmesh_area_names: [Walkable]
    navmesh_area_mapping_id: navmesh_area_mapping_mvp
    expected_path_status: PathComplete
    path_complete: true
    path_samples: [[1.0,0.0,1.0], [1.0,0.0,10.5], [1.0,0.0,13.0], [1.0,0.0,14.5]]
    stable_path_hash: REQUIRED_RUNTIME_HASH
    tolerance_class: gameplay_contract
    pickup_reach:
      reachable: true
      telemetry_event: pickup-reached
      telemetry_only: true
      player_feet_ws: REQUIRED_RUNTIME_PLAYER_FEET
      pickup_anchor_ws: [1.0, 0.0, 14.5]
      pickup_reach_radius_m: 1.6
      distance_squared_m2: REQUIRED_RUNTIME_DISTANCE_SQUARED
      distance_predicate: distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2
      distance_within_reach: true
      e20_linecast: {origin_ws: REQUIRED_RUNTIME_PLAYER_FEET_PLUS_PICKUP_REACH_OFFSET, endpoint_ws: [1.0, 0.0, 14.5], clear: true, layer_mask: World, query_trigger_policy: QueryTriggerInteraction.Ignore}
    e20_linecast: {clear: true, trigger_policy: QueryTriggerInteraction.Ignore}
    gate_state: closed
    failed_gate_spend: false
    required_failed_cases: []
    actual: UNCAPTURED
  - route_id: reserve_route_after_failed_spends_01
    geometry_binding_id: geometry_receiver_clear_01
    geometry_overlay_ids: [geometry_gate_closed_01]
    route_safety_predicate_id: route_safety_reserve_after_failed_spends_01
    from_marker: checkpoint_mvp_01
    to_marker: burst_reserve_pickup_01
    obstruction_ids: []
    player_agent_radius_m: 0.30
    navmesh_area_mask: 1
    navmesh_area_names: [Walkable]
    navmesh_area_mapping_id: navmesh_area_mapping_mvp
    expected_path_status: PathComplete
    path_complete: true
    path_samples: [[1.0,0.0,1.0], [1.0,0.0,10.5], [1.0,0.0,13.0], [1.0,0.0,14.5]]
    stable_path_hash: REQUIRED_RUNTIME_HASH
    tolerance_class: gameplay_contract
    pickup_reach:
      reachable: true
      telemetry_event: pickup-reached
      telemetry_only: true
      player_feet_ws: REQUIRED_RUNTIME_PLAYER_FEET
      pickup_anchor_ws: [1.0, 0.0, 14.5]
      pickup_reach_radius_m: 1.6
      distance_squared_m2: REQUIRED_RUNTIME_DISTANCE_SQUARED
      distance_predicate: distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2
      distance_within_reach: true
      e20_linecast: {origin_ws: REQUIRED_RUNTIME_PLAYER_FEET_PLUS_PICKUP_REACH_OFFSET, endpoint_ws: [1.0, 0.0, 14.5], clear: true, layer_mask: World, query_trigger_policy: QueryTriggerInteraction.Ignore}
    e20_linecast: {clear: true, trigger_policy: QueryTriggerInteraction.Ignore}
    gate_state: closed
    failed_gate_spend: true
    required_failed_cases: [failed_wall_01, failed_ceiling_01, failed_void_01, failed_death_01]
    actual: UNCAPTURED
```

```yaml
route_causality:
  - causality_id: gate_burst_to_exit_01
    pickup_marker_id: burst_gate_pickup_01
    pickup_reach_route_id: gate_route_open_01
    pickup_interaction_id: pickup_interact_gate_01
    pickup_state_transition_id: pickup_state_gate_placed_to_carried_01
    causal_volume_id: throw_lower_landing_01
    causal_geometry_variant_id: geometry_throw_lower_open_01
    causal_landing_surface_id: world_throw_lower_landing_01
    accepted_throw_source_event_id: "flight:REQUIRED_RUNTIME_FLIGHT_HANDLE_ID"
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    source_timestamp: REQUIRED_RUNTIME_THROW_SOURCE_TIMESTAMP
    terminal_publication_time: REQUIRED_RUNTIME_TERMINAL_PUBLICATION_TIME
    t_publish: REQUIRED_RUNTIME_T_PUBLISH
    terminal_cause: REQUIRED_RUNTIME_TERMINAL_CAUSE
    fact_publication_state: published
    throw_snapshot_id: REQUIRED_RUNTIME_THROW_SNAPSHOT_ID
    throw_snapshot_parity: field_for_field_with_controller_capture
    resolved_velocity: REQUIRED_RUNTIME_RESOLVED_VELOCITY
    landing_surface_id: REQUIRED_RUNTIME_LANDING_SURFACE_ID
    landing_contact_probe_id: REQUIRED_RUNTIME_LANDING_CONTACT_PROBE_ID
    landing_fact_id: REQUIRED_RUNTIME_FACT_ID
    noise_published_fact_id: REQUIRED_RUNTIME_FACT_ID
    receiver_probe_id: receiver_from_captured_landing_01
    hearing_relay_fact_id: REQUIRED_RUNTIME_FACT_ID
    entry_id: REQUIRED_RUNTIME_ENTRY_ID
    investigate_commit_id: REQUIRED_RUNTIME_DECISION_ID
    fsm_relay_outcome: consumed
    gate_transition_id: gate_state_blocked_to_open_01
    gate_entry_marker_id: burst_gate_entry_01
    expected_gate_entry_transition: blocked_to_open
    gate_exit_marker_id: burst_gate_exit_01
    gate_exit_reach_route_id: gate_exit_reachable_after_burst_01
    expected_gate_exit_reachable: true
    route_evidence_id: gate_exit_reachable_after_burst_01
    expected_order: [pickup_reached, pickup_interact, pickup_state_transition, throw_accepted, throw_snapshot_captured, landing_surface_contact, burst_landed, noise_published, relay_heard, investigate_commit, orient_tell, investigate_movement, gate_state_transition, gate_exit_reachable]
    liveness_continuity:
      applicability: not_applicable_for_investigate_only_route
      op: not_applicable
      entry_id: REQUIRED_RUNTIME_ENTRY_ID
      retains_entry_id: true
      source_event_id: "flight:REQUIRED_RUNTIME_FLIGHT_HANDLE_ID"
      fact_id: REQUIRED_RUNTIME_FACT_ID
      relay_fact_id: REQUIRED_RUNTIME_RELAY_FACT_ID
      decision_id: REQUIRED_RUNTIME_DECISION_ID
      close_open_pair: forbidden
      actual: UNCAPTURED
    actual: UNCAPTURED
```

```yaml
gate_transitions:
  - transition_id: gate_state_blocked_to_open_01
    gate_marker_id: burst_gate_entry_01
    owner: Level
    from_state: blocked
    to_state: open
    trigger_event: investigate_commit
    trigger_noise_kind: burst
    trigger_decision_id: REQUIRED_RUNTIME_DECISION_ID
    causal_session_id: REQUIRED_RUNTIME_SESSION_ID
    causal_attempt_epoch: REQUIRED_RUNTIME_EPOCH
    causal_source_event_id: "flight:REQUIRED_RUNTIME_FLIGHT_HANDLE_ID"
    causal_fact_id: REQUIRED_RUNTIME_FACT_ID
    landing_fact_id: REQUIRED_RUNTIME_FACT_ID
    step_id: null
    causal_entry_id: REQUIRED_RUNTIME_ENTRY_ID
    source_timestamp: REQUIRED_RUNTIME_THROW_SOURCE_TIMESTAMP
    terminal_publication_time: REQUIRED_RUNTIME_TERMINAL_PUBLICATION_TIME
    t_publish: REQUIRED_RUNTIME_T_PUBLISH
    terminal_cause: REQUIRED_RUNTIME_TERMINAL_CAUSE
    fact_publication_state: published
    fsm_relay_outcome: consumed
    expected_cause: noise
    expected_order_after: investigate_commit
    expected_order_before: gate_exit_reachable
    receiver_phase_window_id: patrol_pre_spend_01
    receiver_phase_window: [REQUIRED_RUNTIME_PHASE_INTERVAL]
    receiver_binding:
      receiver_phase_window_id: patrol_pre_spend_01
      patrol_state: Patrol
      patrol_state_conditions: [loop_index_gte_1, no_reanchor_or_giveup_in_flight, leg_away_from_gate_pickup_corridor]
      minimum_validity_s: 2.24
      source_kind_publication_time: REQUIRED_RUNTIME_TERMINAL_PUBLICATION_TIME
      intended_fsm_response: Investigate
    gate_state_after_failed_spend: blocked
    gate_exit_reachable_after_failed_spend: false
    actual: UNCAPTURED
```

`gate_transitions[].owner=Level` is authoritative for the physical gate state;
NoiseEmitter, Perception, and the FSM publish or consume causal identities but do
not mutate the gate. The transition is valid only when its before/after states,
`investigate_commit` decision, `fact_id`, `entry_id`, session, epoch, and
`causal_source_event_id` match the same captured route chain. For the certified
route, the equality chain is mandatory: `trigger_noise_kind=burst`,
`causal_source_event_id=flight:<flight_handle_id>`, `landing_fact_id` equals the
route's captured Burst landing `fact_id`, `causal_fact_id` equals that same
`landing_fact_id`, `causal_entry_id == relay.entry_id == investigate_commit.entry_id`,
`causal_session_id == relay.session_id == decision.session_id`,
`causal_attempt_epoch == relay.attempt_epoch == decision.attempt_epoch`,
`t_publish == relay.t_publish`, `fact_publication_state=published`, and
`fsm_relay_outcome=consumed`; the FSM decision consumes the relay carrying that
fact.
Any mismatch, including a Burst fact from a different flight, fails closed as
`INVALID_GATE_TRANSITION`. `trigger_noise_kind` binds the gate-open record to the
noise kind that caused it (`Burst` for this fixture — a movement pip can never open
the gate), and `landing_fact_id` binds it to the captured published landing fact of
that `Burst`; a gate-open record without both bindings, with a Movement `step_id`
where a Burst landing is required, or with a bound landing that is not the same
chain's landing contact, fails with `MISSING_REQUIRED_FIELD` or
`INVALID_GATE_TRANSITION`. A landing with `invalid`, `fallback`, or
`death_cancelled` terminal status is forbidden from appearing in any
`gate_transitions[]` causal chain; the validator returns `GATE_OPENED_BY_INVALID_LANDING`
if it does. Because `trigger_noise_kind=burst` and `landing_fact_id` are mandatory,
a movement pip cannot appear as a gate-opening cause: `route_causality` defines no
movement-pip chain into `gate_transitions`, and a patrol-state guard hearing a
movement pip produces at most an `Investigate`, never a gate-opening transition.

## Revision Closure — Route and Producer Record Supplements

The introductory required-section list is normative and includes
`gate_trigger_negative_cases` and `patrol_phase_windows`. `patrol_phase_windows[]`
serializes the authored manifest fields
`{window_id, guard_eid, guard_state, conditions, purpose, min_validity_s, actual}`;
a gate receiver references exactly one resolving window. Window validity is an
asserted minimum duration, not a synthetic start/end or loop-index schema. The
certified MVP spend window is `patrol_pre_spend_01` with `min_validity_s=2.24` s;
it must cover accepted Throw through landing, hearing, and FSM response. No
runtime capture is implied by the authored value. `leg_away_from_gate_pickup_corridor`
is resolved as the patrol centerline segment whose minimum XZ distance to the
gate-pickup anchor is at least `pickup_reach_radius_m + patrol_agent_radius_m`;
unknown or borderline geometry fails closed as
`PICKUP_CORRIDOR_RESOLUTION_UNAVAILABLE`.

`pickup-reached` remains telemetry and never mutates lifecycle. Its record is
`{telemetry_id, pickup_id, publisher, session_id, attempt_epoch, virtual_time,
player_feet_ws, pickup_anchor_ws, reach_sphere_result, e20_linecast_result,
pickup_state_before, actual}`. `pickup_interact` and state-transition records carry
the same session/epoch and publisher fields. A telemetry record from another epoch,
a trigger hit, or a reach sphere without explicit Interact cannot produce `Carried`,
spend Burst, or open the gate.

`gate_trigger_negative_cases[]` includes a movement fact inside the gate volume, a
missing landing fact, a landing with an invalid/fallback/death-cancelled terminal,
and a mismatched session/epoch. Each records `expected_rejection_code`,
`trigger_noise_kind`, responsible source id, and `actual`. The route remains
`UNCAPTURED` / `ACTUAL_CAPTURE_UNAVAILABLE` until a real Unity fixture captures
patrol timing, NavMesh/E20 separation, and the complete causal chain.

`receiver_phase_window_id` (rev 2026-09-09, B9) binds this gate-open record to the
**declared patrol window in which the accepted Throw, certified landing, hearing,
and FSM response remain valid and the landing is hearable at the recorded `R_eff`** — the `receiver_phase_window[]` evidence the GDD's AC20 requires. This
record binds `patrol_pre_spend_01`: the patrol-phase interval where the guard
(`loop_index ≥ 1`, state `Patrol`, no re-anchor or give-up timer in flight) is on a
leg away from the gate pickup corridor, so the landing at the captured contact
origin is within `R_burst_eff` of the guard's patrol position and the Linecast is
clear. The id must resolve to a `patrol_phase_window` declared by this manifest —
the two declared windows are defined in the teaching-sequence section
(`patrol_settled_after_spawn_01`, `patrol_pre_spend_01`); a null, unknown, or
non-resolving id fails `INVALID_GATE_TRANSITION`. The gate chain is satisfiable
inside this window by construction: the certified spend beat
(`teach_certified_throw_01`) carries the same `phase_window_id: patrol_pre_spend_01`
precondition, so the throw, the hearing, and the gate-open all certify inside one
declared window.

`REQUIRED_RUNTIME_HASH` is a required capture placeholder, not a valid pass value.
The validator returns `MISSING_REQUIRED_FIELD` until a real hash and path sample
capture are supplied.

## Receiver and Effective-Radius Records

`R_eff` is computed from the source-kind hearing origin and the registered guard
feet datum (rev 2026-08-31): `dy_m = |hearing_origin_ws.y − guard_feet_ws.y|` and
`R_eff = nominal_radius × (1 − dy_m / dy_max_m)`, exclusive at `dy_m ≥ dy_max_m`.
The guard eye endpoint is the occlusion-ray endpoint only — it no longer anchors
the falloff. For movement, `hearing_origin_ws = source_position_ws + [0.0,
f12_noise_origin_offset, 0.0]`; for Burst, `hearing_origin_ws = source_position_ws`
because the source position is already the pushed-out landing contact. On this
room's flat ground every authored guard feet datum is `y = 0.0`, so movement pips
(hearing origin `y = 0.25`) measure `dy_m = 0.25` → factor `0.9375`
(`R_walk_eff ≈ 3.75 m`, `R_run_eff ≈ 5.63 m`) and the authored flat-ground
Burst landing contact is approximately `0.05 m` above the guard-feet datum
(`dy_m ≈ 0.05`) → `R_burst_eff ≈ 10.37 m`. Every probe carries the explicit
`guard_feet_ws` field the datum is measured against. The playable route's Burst
receiver position is not independently authored: `receiver_from_captured_landing_01`
must derive both `source_position_ws` and `hearing_origin_ws` from the captured
`landing_contact_probe_id` and its `landing_surface_id`. A receiver record with an
authored substitute position cannot certify the route. `d_noise` is planar X/Z
distance. Each record stores `endpoint_authoring_mode` and `endpoint_source_id`:
`real_guard_endpoint` must resolve to the authored guard marker and its registered
eye height; `synthetic_endpoint` is permitted only for an explicitly authored
receiver test marker and never certifies the playable MVP route. The vertical
probes raise their synthetic Burst sources above the floor (`y = 2.4` for the soft
falloff, `y = 4.0` at the `dy_max` cutoff) while the synthetic guard stays grounded
at `y = 0.0` — these floating sources are harness-synthetic test states; the
`geometry_receiver_clear_01` variant contains no solids, so no Linecast geometry is
implied by a raised source. Each record stores the exact endpoints, geometry
variant, expected hearing result, intended FSM response, and meters tolerance.
`actual` must include the observed effective radius, distance, Linecast result,
endpoint mode/source, and relay/decision result.

```yaml
synthetic_receiver_endpoints:
  - endpoint_id: receiver_boundary_endpoint_01
    endpoint_authoring_mode: synthetic_endpoint
    world_position: [14.96058, 1.6, 14.96058]
    geometry_variant_id: geometry_receiver_clear_01
    purpose: inclusive_radius_boundary_diagonal_placement_inside_room
    actual: UNCAPTURED
  - endpoint_id: receiver_vertical_endpoint_01
    endpoint_authoring_mode: synthetic_endpoint
    world_position: [8.0, 1.6, 8.0]
    geometry_variant_id: geometry_receiver_clear_01
    purpose: grounded_guard_eye_for_raised_source_soft_falloff_probe
    actual: UNCAPTURED
  - endpoint_id: receiver_vertical_zero_endpoint_01
    endpoint_authoring_mode: synthetic_endpoint
    world_position: [8.0, 1.6, 8.0]
    geometry_variant_id: geometry_receiver_clear_01
    purpose: grounded_guard_eye_for_dy_max_exclusive_cutoff_probe
    actual: UNCAPTURED

receiver_probes:
  - probe_id: receiver_clear_01
    geometry_variant_id: geometry_receiver_clear_01
    landing_surface_id: null
    noise_kind: burst
    nominal_radius_m: 10.5
    endpoint_authoring_mode: real_guard_endpoint
    endpoint_source_id: guard_mvp_01
    source_position_id: null
    source_position_ws: [8.0, 0.25, 8.0]
    hearing_origin_ws: [8.0, 0.25, 8.0]
    guard_feet_ws: [8.0, 0.0, 10.5]
    guard_eye_ws: [8.0, 1.6, 10.5]
    dy_m: 0.25
    dy_max_m: 4.0
    d_noise_xz_m: 2.5
    expected_r_eff_m: 9.84375
    expected_linecast_clear: true
    expected_hearing: true
    intended_fsm_response: Investigate
    expected_blocker_id: none
    boundary_semantics: not_applicable
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
  - probe_id: receiver_from_captured_landing_01
    geometry_variant_id: geometry_throw_lower_open_01
    landing_surface_id: world_throw_lower_landing_01
    noise_kind: burst
    nominal_radius_m: 10.5
    endpoint_authoring_mode: real_guard_endpoint
    endpoint_source_id: guard_mvp_01
    source_position_id: REQUIRED_RUNTIME_LANDING_CONTACT_PROBE_ID
    source_position_ws: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    hearing_origin_ws: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    guard_feet_ws: [8.0, 0.0, 10.5]
    guard_eye_ws: [8.0, 1.6, 10.5]
    dy_m: REQUIRED_RUNTIME_LANDING_TO_GUARD_DY
    dy_max_m: 4.0
    d_noise_xz_m: REQUIRED_RUNTIME_LANDING_TO_GUARD_D_NOISE
    expected_r_eff_m: REQUIRED_RUNTIME_LANDING_R_EFF
    expected_linecast_clear: true
    expected_hearing: true
    intended_fsm_response: Investigate
    expected_blocker_id: none
    boundary_semantics: not_applicable
    tol_m: 0.005
    tolerance_class: gameplay_contract
    actual: UNCAPTURED
  - probe_id: receiver_movement_raised_origin_01
    geometry_variant_id: geometry_receiver_clear_01
    landing_surface_id: null
    noise_kind: movement
    nominal_radius_m: 4.0
    endpoint_authoring_mode: real_guard_endpoint
    endpoint_source_id: guard_mvp_01
    source_position_id: null
    source_position_ws: [8.0, 0.0, 8.0]
    hearing_origin_ws: [8.0, 0.25, 8.0]
    guard_feet_ws: [8.0, 0.0, 10.5]
    guard_eye_ws: [8.0, 1.6, 10.5]
    dy_m: 0.25
    dy_max_m: 4.0
    d_noise_xz_m: 2.5
    expected_r_eff_m: 3.75
    expected_linecast_clear: true
    expected_hearing: true
    intended_fsm_response: Investigate
    expected_blocker_id: none
    boundary_semantics: not_applicable
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
  - probe_id: riser_clear_probe_01
    geometry_variant_id: geometry_receiver_riser_01
    landing_surface_id: null
    noise_kind: movement
    nominal_radius_m: 4.0
    endpoint_authoring_mode: real_guard_endpoint
    endpoint_source_id: guard_mvp_01
    source_position_id: null
    source_position_ws: [8.0, 0.0, 8.0]
    hearing_origin_ws: [8.0, 0.25, 8.0]
    guard_feet_ws: [8.0, 0.0, 10.5]
    guard_eye_ws: [8.0, 1.6, 10.5]
    dy_m: 0.25
    dy_max_m: 4.0
    d_noise_xz_m: 2.5
    expected_r_eff_m: 3.75
    expected_linecast_clear: true
    expected_hearing: true
    intended_fsm_response: Investigate
    expected_blocker_id: none
    boundary_semantics: not_applicable
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
  - probe_id: receiver_blocked_01
    geometry_variant_id: geometry_receiver_wall_01
    landing_surface_id: null
    noise_kind: burst
    nominal_radius_m: 10.5
    endpoint_authoring_mode: real_guard_endpoint
    endpoint_source_id: guard_mvp_01
    source_position_id: null
    source_position_ws: [8.0, 0.25, 8.0]
    hearing_origin_ws: [8.0, 0.25, 8.0]
    guard_feet_ws: [8.0, 0.0, 10.5]
    guard_eye_ws: [8.0, 1.6, 10.5]
    dy_m: 0.25
    dy_max_m: 4.0
    d_noise_xz_m: 2.5
    expected_r_eff_m: 9.84375
    expected_linecast_clear: false
    expected_hearing: false
    intended_fsm_response: none
    expected_blocker_id: world_wall_receiver_01
    boundary_semantics: not_applicable
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
  - probe_id: receiver_boundary_01
    geometry_variant_id: geometry_receiver_clear_01
    landing_surface_id: null
    noise_kind: burst
    nominal_radius_m: 10.5
    endpoint_authoring_mode: synthetic_endpoint
    endpoint_source_id: receiver_boundary_endpoint_01
    source_position_id: null
    source_position_ws: [8.0, 0.25, 8.0]
    hearing_origin_ws: [8.0, 0.25, 8.0]
    guard_feet_ws: [14.96058, 0.0, 14.96058]
    guard_eye_ws: [14.96058, 1.6, 14.96058]
    dy_m: 0.25
    dy_max_m: 4.0
    d_noise_xz_m: 9.84375
    expected_r_eff_m: 9.84375
    expected_linecast_clear: true
    expected_hearing: true
    intended_fsm_response: Investigate
    expected_blocker_id: none
    boundary_semantics: inclusive
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
  - probe_id: receiver_vertical_cutoff_01
    geometry_variant_id: geometry_receiver_clear_01
    landing_surface_id: null
    noise_kind: burst
    nominal_radius_m: 10.5
    endpoint_authoring_mode: synthetic_endpoint
    endpoint_source_id: receiver_vertical_endpoint_01
    source_position_id: null
    source_position_ws: [8.0, 2.4, 8.0]
    hearing_origin_ws: [8.0, 2.4, 8.0]
    guard_feet_ws: [8.0, 0.0, 8.0]
    guard_eye_ws: [8.0, 1.6, 8.0]
    dy_m: 2.4
    dy_max_m: 4.0
    d_noise_xz_m: 0.0
    expected_r_eff_m: 4.2
    expected_linecast_clear: true
    expected_hearing: true
    intended_fsm_response: Investigate
    expected_blocker_id: none
    boundary_semantics: not_applicable
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
  - probe_id: receiver_vertical_zero_01
    geometry_variant_id: geometry_receiver_clear_01
    landing_surface_id: null
    noise_kind: burst
    nominal_radius_m: 10.5
    endpoint_authoring_mode: synthetic_endpoint
    endpoint_source_id: receiver_vertical_zero_endpoint_01
    source_position_id: null
    source_position_ws: [8.0, 4.0, 8.0]
    hearing_origin_ws: [8.0, 4.0, 8.0]
    guard_feet_ws: [8.0, 0.0, 8.0]
    guard_eye_ws: [8.0, 1.6, 8.0]
    dy_m: 4.0
    dy_max_m: 4.0
    d_noise_xz_m: 0.0
    expected_r_eff_m: 0.0
    expected_linecast_clear: true
    expected_hearing: false
    intended_fsm_response: none
    expected_blocker_id: none
    boundary_semantics: exclusive_cutoff
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
```

## Occlusion Probe Records

```yaml
occlusion_probes:
  - probe_id: occlusion_clear_01
    segment_id: receiver_segment_clear_01
    geometry_variant_id: geometry_receiver_clear_01
    origin_ws: [8.0, 0.25, 8.0]
    guard_endpoint_ws: [8.0, 1.6, 10.5]
    expected_linecast_clear: true
    expected_blocker_id: none
    query_trigger_policy: QueryTriggerInteraction.Ignore
    trigger_ignored: true
    observed_is_trigger: false
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
  - probe_id: occlusion_wall_01
    segment_id: receiver_segment_wall_01
    geometry_variant_id: geometry_receiver_wall_01
    origin_ws: [8.0, 0.25, 8.0]
    guard_endpoint_ws: [8.0, 1.6, 10.5]
    expected_linecast_clear: false
    expected_blocker_id: world_wall_receiver_01
    query_trigger_policy: QueryTriggerInteraction.Ignore
    trigger_ignored: true
    observed_is_trigger: false
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
  - probe_id: occlusion_trigger_ignored_01
    segment_id: receiver_segment_trigger_01
    geometry_variant_id: geometry_receiver_trigger_01
    origin_ws: [8.0, 0.25, 8.0]
    guard_endpoint_ws: [8.0, 1.6, 10.5]
    expected_linecast_clear: true
    expected_blocker_id: none
    query_trigger_policy: QueryTriggerInteraction.Ignore
    trigger_ignored: true
    observed_is_trigger: true
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
```

## Valid Throw-Volume Records

Only `valid_throw_volumes` participate in positive production certification. Each
record includes the launch inputs, the per-volume `band_reachable_theta_domain_deg`
(union of closed θ intervals within the global `0° < θ ≤ 45°` cap for which the
same F4 oracle returns `R_actual ∈ [8, 12]`), both quadratic roots, selected root,
range gate, ceiling/lateral margins, collision/landing envelope, expected values,
actual values, and tolerance. The authored `theta_deg` must belong to the recorded
band domain; otherwise content validation returns
`AUTHORED_THETA_OUTSIDE_BAND_DOMAIN` before launch/spend. The runtime, preview, and validator use the same F4 and collision
oracle. In this fixture, `target_marker_id` names the logical gate-entry objective
that the Burst noise is intended to open; it is not a claim that the projectile center
must land inside that marker's AABB. Physical landing is certified by the launch
snapshot, the referenced `landing_surface_id`, `open_volume_bounds`, F4 range, and
`landing_envelope`. The landing surface is a real `World` solid in the geometry
variant, not an open-volume annotation or a nominal-radius assumption. A future
fixture that requires a physical target must add a separate landing-target marker
field rather than overloading `target_marker_id`.

```yaml
# throw_flat_starter_01 is supplemental F4 formula/placement coverage only;
# gate_burst_to_exit_01 is causally bound to throw_lower_landing_01 below.
valid_throw_volumes:
  - volume_id: throw_flat_starter_01
    route_role: supplemental_formula_coverage
    geometry_variant_id: geometry_throw_flat_open_01
    band_reachable_theta_domain_deg: [[12.6648, 45.0]]
    launch_position_ws: [1.5, 1.5, 6.0]
    launch_direction_xz: [0.70710678, 0.70710678]
    source_pickup_id: burst_gate_pickup_01
    open_volume_bounds: {min: [1.4, 0.0, 5.9], max: [11.4, 3.95, 15.8]}
    landing_surface_id: world_throw_flat_landing_01
    landing_surface_y_m: 0.0
    landing_contact_mode: surface_top_world_solid
    target_marker_id: burst_gate_entry_01
    route_id: formula_coverage_flat_01
    throw_snapshot_id: REQUIRED_RUNTIME_THROW_SNAPSHOT_ID
    throw_snapshot_parity: required_field_for_field_with_controller_capture
    v0_mps: 10.0
    theta_deg: 30.0
    band_domain_result: PASS
    h_release_m: 1.5
    h_landing_m: 0.0
    delta_y_m: 1.5
    projectile_radius_m: 0.05
    epsilon_contact_m: 0.001
    contact_ambiguity_tolerance_m: 0.001
    angle_domain_result: PASS
    discriminant_m2ps2: 54.43
    t_minus_s: -0.24237196825373027
    t_plus_s: 1.2617399600987863
    selected_t_land_s: 1.2617399600987863
    root_policy: dual_regime_root_oracle
    r_actual_m: 10.927008740588667
    range_gate: PASS
    ceiling_margin_m: 1.72579001019368
    lateral_margin_m: 0.4
    collision: landing_surface_contact
    landing_envelope: [10.92, 10.94]
    expected:
      discriminant_m2ps2: 54.43
      t_land_s: 1.2617399600987863
      r_actual_m: 10.927008740588667
      theta_rad: 0.5235987755982988
      theta_rad_conversion: {source: theta_deg, formula: theta_deg * pi / 180, expected: 0.5235987755982988}
      angle_input: {theta_deg: 30.0, theta_rad: 0.5235987755982988, domain: 0 < theta_rad <= pi/4, result: PASS}
      authored_landing_surface_height_m: 0.0
      projectile_center_result: {surface_height_m: 0.0, center_height_m: REQUIRED_RUNTIME_PROJECTILE_CENTER_Y, contact_mode: surface_top_world_solid, push_out: REQUIRED_RUNTIME_CONTACT_PUSH_OUT}
      selected_positive_root: {root_id: t_plus, t_s: 1.2617399600987863, policy: dual_regime_root_oracle}
      clearance: {ceiling_margin_m: 1.72579001019368, lateral_margin_m: 0.4}
      geometry_ids: {variant_id: geometry_throw_flat_open_01, landing_surface_id: world_throw_flat_landing_01}
    formula_tolerance_class: pure_math_relative
    formula_relative_tol: 0.000001
    placement_tolerance_class: gameplay_contract
    placement_tol_m: 0.005
    diagnostic_simulation_envelope_m: 0.0666675628
    actual: UNCAPTURED
  - volume_id: throw_lower_landing_01
    geometry_variant_id: geometry_throw_lower_open_01
    band_reachable_theta_domain_deg: [[14.7500, 45.0]]
    launch_position_ws: [1.5, 1.5, 6.0]
    launch_direction_xz: [0.70710678, 0.70710678]
    source_pickup_id: burst_gate_pickup_01
    open_volume_bounds: {min: [1.4, 0.0, 5.9], max: [11.4, 3.95, 15.8]}
    landing_surface_id: world_throw_lower_landing_01
    landing_surface_y_m: 0.25
    landing_contact_mode: surface_top_world_solid
    target_marker_id: burst_gate_entry_01
    route_id: gate_route_open_01
    route_role: canonical_mvp_causal_volume
    causal_route_binding: {causality_id: gate_burst_to_exit_01, volume_id: throw_lower_landing_01, geometry_variant_id: geometry_throw_lower_open_01, landing_surface_id: world_throw_lower_landing_01}
    throw_snapshot_id: REQUIRED_RUNTIME_THROW_SNAPSHOT_ID
    throw_snapshot_parity: required_field_for_field_with_controller_capture
    v0_mps: 10.0
    theta_deg: 30.0
    band_domain_result: PASS
    h_release_m: 1.5
    h_landing_m: 0.25
    delta_y_m: 1.25
    projectile_radius_m: 0.05
    epsilon_contact_m: 0.001
    contact_ambiguity_tolerance_m: 0.001
    angle_domain_result: PASS
    discriminant_m2ps2: 49.525
    t_minus_s: -0.20768604363895336
    t_plus_s: 1.2270540354840094
    selected_t_land_s: 1.2270540354840094
    root_policy: dual_regime_root_oracle
    r_actual_m: 10.626600021611093
    range_gate: PASS
    ceiling_margin_m: 1.72579001019368
    lateral_margin_m: 0.4
    collision: landing_surface_contact
    landing_envelope: [10.62, 10.64]
    expected:
      discriminant_m2ps2: 49.525
      t_land_s: 1.2270540354840094
      r_actual_m: 10.626600021611093
      theta_rad: 0.5235987755982988
      theta_rad_conversion: {source: theta_deg, formula: theta_deg * pi / 180, expected: 0.5235987755982988}
      angle_input: {theta_deg: 30.0, theta_rad: 0.5235987755982988, domain: 0 < theta_rad <= pi/4, result: PASS}
      authored_landing_surface_height_m: 0.25
      projectile_center_result: {surface_height_m: 0.25, center_height_m: REQUIRED_RUNTIME_PROJECTILE_CENTER_Y, contact_mode: surface_top_world_solid, push_out: REQUIRED_RUNTIME_CONTACT_PUSH_OUT}
      selected_positive_root: {root_id: t_plus, t_s: 1.2270540354840094, policy: dual_regime_root_oracle}
      clearance: {ceiling_margin_m: 1.72579001019368, lateral_margin_m: 0.4}
      geometry_ids: {variant_id: geometry_throw_lower_open_01, landing_surface_id: world_throw_lower_landing_01}
    formula_tolerance_class: pure_math_relative
    formula_relative_tol: 0.000001
    placement_tolerance_class: gameplay_contract
    placement_tol_m: 0.005
    diagnostic_simulation_envelope_m: 0.0666675628
    actual: UNCAPTURED
```

`REQUIRED_RUNTIME_VALUE` is not a pass value. The authored expected ceiling and
lateral margins above are design inputs; their observed runtime counterparts remain
inside `actual: UNCAPTURED` until capture, and a missing observed margin returns
`MISSING_REQUIRED_FIELD`. Roots with `t ≤ ϵ_t` (where `ϵ_t = Δt_tick = 1/120 s ≈ 0.00833 s`, rev 2026-09-19) are discarded; the minimum strictly positive root exceeding `ϵ_t` is selected, eliminating float-rounding zero-meter detonations on flat ground; a positive repeated
root where `D = 0` is accepted as the single selected root, while a later descending
root is not selected when an earlier strictly positive crossing exists. The
`diagnostic_simulation_envelope_m` is a fixed-step diagnostic bound only; it is not a
replacement for the gameplay `CompareTolerance` gate.

The rev 2026-08-31 pickup re-siting moves both certified launches from
`(3.0, 1.5, 7.5)` to `(1.5, 1.5, 6.0)` (the pickup point plus `h_release_m`). The F4
oracle is launch-position invariant: discriminant, roots, `r_actual`, the range gate,
and `landing_envelope` depend only on `v0`, `theta`, `h_release`, and `h_landing`, so
those authored values are unchanged. The horizontal landing points move to
`(9.65757, 0.0, 14.15757)` (flat) and `(9.48483, 0.25, 13.98483)` (unequal height —
rev 2026-09-01: the unequal-height range `11.292259321388022 m` projects to
`1.5 + R × 0.70710678` and `6.0 + R × 0.70710678`; the earlier `(9.65757, 0.25,
14.15757)` was the flat-ground result projected diagonally and is struck),
both still inside their referenced slabs with the authored margins intact.

The swept collision contract is exact: each fixed substep casts the projectile
sphere along `p_current → p_next` with `maxDistance=|p_next−p_current|` and
`radius=projectile_radius`. `epsilon_contact` is used only in the post-hit push-out
`hit.point + hit.normal × (projectile_radius + epsilon_contact)`. A zero-length
center segment skips the sweep after the initial-overlap check. A single closest-hit
SphereCast is authoritative; full-buffer and nearest-returned-hit fallbacks are
invalid.

F4 consumes the authored landing surface height (`h_landing_m` /
`landing_surface_y_m`) and never substitutes the pushed-out projectile center height.
The records retain both the degree-to-radian conversion and the angle-domain result,
the candidate/selected positive root, contact mode, clearance values, and stable
variant/solid IDs. `projectile_center_result` is a separate runtime-result slot;
its `REQUIRED_RUNTIME_*` values and the record's `actual: UNCAPTURED` are not
measurements. Missing, malformed, contradictory, nominal-only, or uncaptured
center/contact evidence fails closed.

## Expected-Negative Validation Records

Negative records are a separate collection. They are expected rejections and are not
counted as positive throw-volume certification. A negative case is valid only when
its complete expected rejection code and actual validator result match.

The Level validator owns a separate authored-geometry grazing predicate. It rejects a
throw volume when the authored landing/contact geometry has tangent or edge contact,
zero required clearance, or an otherwise ambiguous physical winner, and records the
stable code `AUTHORED_GRAZING_GEOMETRY_REJECTED`. This predicate runs after the F4
math oracle and does not reject a finite, positive repeated root solely because its
mathematical discriminant is `D = 0`; a positive repeated root remains valid math
when the authored physical geometry is not grazing. The grazing result records the
referenced solid, contact mode, measured clearance, and expected/actual validator
status. It is never inferred from an uncaptured runtime hit.

```yaml
negative_validation_cases:
  - case_id: throw_invalid_angle_01
    category: invalid_throw_configuration
    geometry_variant_id: geometry_throw_flat_open_01
    geometry_validation_result: NOT_EVALUATED
    band_reachable_theta_domain_deg: NOT_EVALUATED
    band_domain_result: NOT_EVALUATED
    grazing_surface_id: null
    grazing_clearance_m: null
    contact_ambiguity_tolerance_m: null
    candidate_solids: []
    hit_distance_delta_m: null
    complete_inputs: {v0_mps: 10.0, theta_deg: 50.0, h_release_m: 1.5, h_landing_m: 0.0, delta_y_m: 1.5, launch_position_ws: [2.0, 1.5, 2.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, pickup_id: burst_gate_pickup_01}
    configuration_result: NOT_EVALUATED
    expected_rejection_code: INVALID_ANGLE
    angle_domain_result: REJECT
    discriminant_m2ps2: 88.11240888334652
    root_result: PASS
    r_actual_m: 11.17000103140645
    range_gate: PASS
    initial_overlap: false
    no_runtime_launch: true
    no_spend: true
    flight_handle_id: null
    fact_id: null
    source_event_id: null
    burst_state: Carried
    actual: UNCAPTURED
  - case_id: authored_theta_outside_band_domain_01
    category: invalid_f4_content_volume
    geometry_variant_id: geometry_throw_flat_open_01
    geometry_validation_result: NOT_EVALUATED
    grazing_surface_id: null
    grazing_clearance_m: null
    contact_ambiguity_tolerance_m: null
    candidate_solids: []
    hit_distance_delta_m: null
    complete_inputs: {v0_mps: 10.0, theta_deg: 5.0, h_release_m: 1.5, h_landing_m: 0.0, delta_y_m: 1.5}
    configuration_result: NOT_EVALUATED
    expected_rejection_code: AUTHORED_THETA_OUTSIDE_BAND_DOMAIN
    angle_domain_result: PASS
    band_reachable_theta_domain_deg: [[12.6648, 45.0]]
    band_domain_result: REJECT
    discriminant_m2ps2: 67.33390522001662
    root_result: PASS
    r_actual_m: 6.4647
    range_gate: REJECT
    initial_overlap: false
    no_runtime_launch: true
    no_spend: true
    flight_handle_id: null
    fact_id: null
    source_event_id: null
    burst_state: Carried
    actual: UNCAPTURED
  - case_id: authored_grazing_01
    category: authored_geometry_grazing
    geometry_variant_id: geometry_grazing_edge_01
    geometry_validation_result: REJECT
    band_reachable_theta_domain_deg: [[12.6648, 45.0]]
    band_domain_result: PASS
    grazing_surface_id: world_grazing_edge_01
    grazing_clearance_m: 0.0
    contact_ambiguity_tolerance_m: null
    candidate_solids: []
    hit_distance_delta_m: null
    complete_inputs: {v0_mps: 10.0, theta_deg: 30.0, h_release_m: 1.5, h_landing_m: 0.0, delta_y_m: 1.5, launch_position_ws: [2.0, 1.5, 2.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, pickup_id: burst_gate_pickup_01}
    configuration_result: NOT_EVALUATED
    expected_rejection_code: AUTHORED_GRAZING_GEOMETRY_REJECTED
    angle_domain_result: PASS
    discriminant_m2ps2: 54.43
    root_result: PASS
    r_actual_m: 10.927008740588667
    range_gate: PASS
    initial_overlap: false
    no_runtime_launch: true
    no_spend: true
    flight_handle_id: null
    fact_id: null
    source_event_id: null
    burst_state: Carried
    actual: UNCAPTURED
  - case_id: ambiguous_collision_01
    category: ambiguous_swept_contact
    geometry_variant_id: geometry_ambiguous_collision_01
    geometry_validation_result: REJECT
    band_reachable_theta_domain_deg: [[12.6648, 45.0]]
    band_domain_result: PASS
    grazing_surface_id: null
    grazing_clearance_m: null
    complete_inputs: {v0_mps: 10.0, theta_deg: 30.0, h_release_m: 1.5, h_landing_m: 0.0, delta_y_m: 1.5, launch_position_ws: [2.0, 1.5, 8.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, projectile_radius_m: 0.05, pickup_id: burst_gate_pickup_01}
    configuration_result: NOT_EVALUATED
    expected_rejection_code: AMBIGUOUS_COLLISION
    contact_ambiguity_tolerance_m: 0.001
    candidate_solids: [world_ambiguous_a_01, world_ambiguous_b_01]
    hit_distance_delta_m: 0.0
    no_runtime_launch: true
    no_spend: true
    flight_handle_id: null
    fact_id: null
    source_event_id: null
    burst_state: Carried
    actual: UNCAPTURED
  - case_id: initial_overlap_01
    category: launch_initial_overlap
    geometry_variant_id: geometry_initial_overlap_01
    geometry_validation_result: NOT_EVALUATED
    band_reachable_theta_domain_deg: [[12.6648, 45.0]]
    band_domain_result: PASS
    grazing_surface_id: null
    grazing_clearance_m: null
    contact_ambiguity_tolerance_m: null
    candidate_solids: []
    hit_distance_delta_m: null
    complete_inputs: {v0_mps: 10.0, theta_deg: 30.0, h_release_m: 1.5, h_landing_m: 0.0, delta_y_m: 1.5, launch_sphere_radius_m: 0.05, launch_position_ws: [4.05, 1.0, 6.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, geometry_variant_id: geometry_initial_overlap_01, pickup_id: burst_gate_pickup_01}
    configuration_result: NOT_EVALUATED
    expected_rejection_code: INITIAL_OVERLAP_REJECTED
    angle_domain_result: PASS
    discriminant_m2ps2: 54.43
    root_result: PASS
    r_actual_m: 10.927008740588667
    range_gate: PASS
    initial_overlap: true
    no_runtime_launch: true
    no_spend: true
    flight_handle_id: null
    fact_id: null
    source_event_id: null
    burst_state: Carried
    actual: UNCAPTURED
  - case_id: unequal_height_discriminant_01
    category: invalid_f4_content_volume
    geometry_variant_id: geometry_throw_lower_open_01
    geometry_validation_result: NOT_EVALUATED
    band_reachable_theta_domain_deg: [[12.6648, 45.0]]
    band_domain_result: PASS
    grazing_surface_id: null
    grazing_clearance_m: null
    contact_ambiguity_tolerance_m: null
    candidate_solids: []
    hit_distance_delta_m: null
    complete_inputs: {v0_mps: 10.0, theta_deg: 38.0, h_release_m: 1.5, h_landing_m: 4.0, delta_y_m: -2.5}
    configuration_result: NOT_EVALUATED
    expected_rejection_code: INVALID_ROOT
    angle_domain_result: PASS
    discriminant_m2ps2: -11.14609477998338
    root_result: REJECT_DISCRIMINANT
    r_actual_m: null
    range_gate: NOT_EVALUATED
    initial_overlap: false
    no_runtime_launch: true
    no_spend: true
    flight_handle_id: null
    fact_id: null
    source_event_id: null
    burst_state: Carried
    actual: UNCAPTURED
  - case_id: no_positive_root_01
    category: invalid_throw_configuration
    geometry_variant_id: geometry_throw_flat_open_01
    geometry_validation_result: NOT_EVALUATED
    band_reachable_theta_domain_deg: NOT_EVALUATED
    band_domain_result: NOT_EVALUATED
    grazing_surface_id: null
    grazing_clearance_m: null
    contact_ambiguity_tolerance_m: null
    candidate_solids: []
    hit_distance_delta_m: null
    complete_inputs: {v0_mps: 0.0, theta_deg: 38.0, h_release_m: 1.5, h_landing_m: 1.5, delta_y_m: 0.0}
    configuration_result: REJECT
    expected_rejection_code: INVALID_SPEED
    angle_domain_result: NOT_EVALUATED
    discriminant_m2ps2: NOT_EVALUATED
    root_result: NOT_EVALUATED
    r_actual_m: null
    range_gate: NOT_EVALUATED
    initial_overlap: false
    no_runtime_launch: true
    no_spend: true
    flight_handle_id: null
    fact_id: null
    source_event_id: null
    burst_state: Carried
    actual: UNCAPTURED
  - case_id: out_of_band_range_01
    category: invalid_f4_content_volume
    geometry_variant_id: geometry_throw_flat_open_01
    geometry_validation_result: NOT_EVALUATED
    band_reachable_theta_domain_deg: [[12.6648, 45.0]]
    band_domain_result: PASS
    grazing_surface_id: null
    grazing_clearance_m: null
    contact_ambiguity_tolerance_m: null
    candidate_solids: []
    hit_distance_delta_m: null
    complete_inputs: {v0_mps: 7.0, theta_deg: 38.0, h_release_m: 1.5, h_landing_m: 0.0, delta_y_m: 1.5}
    configuration_result: NOT_EVALUATED
    expected_rejection_code: INVALID_F4_RANGE
    angle_domain_result: PASS
    discriminant_m2ps2: 48.00291355780814
    root_result: PASS
    r_actual_m: 6.319051546408076
    range_gate: REJECT
    initial_overlap: false
    no_runtime_launch: true
    no_spend: true
    flight_handle_id: null
    fact_id: null
    source_event_id: null
    burst_state: Carried
    actual: UNCAPTURED
```

The invalid-angle row is never treated as a valid production throw merely because
its nominal range lies inside the range band. Initial overlap is also not a failed
spend: it leaves the Burst `Carried` and emits only the private
`burst-launch-blocked` feedback.

## Gate Trigger Negative Record

A movement pip may be heard near the gate but cannot satisfy the Burst-only gate
causal contract. This record is content validation, not runtime evidence; the
movement source identity and resulting blocked gate state remain
`REQUIRED_RUNTIME_*`/`UNCAPTURED` until capture.

```yaml
gate_trigger_negative_cases:
  - case_id: gate_footstep_negative_01
    category: gate_trigger_wrong_noise_kind
    geometry_variant_id: geometry_receiver_clear_01
    trigger_noise_kind: movement
    source_step_id: REQUIRED_RUNTIME_STEP_ID
    gate_marker_id: burst_gate_entry_01
    expected_gate_state: blocked
    expected_gate_exit_reachable: false
    configuration_result: NOT_EVALUATED
    expected_rejection_code: MOVEMENT_GATE_TRIGGER_REJECTED
    actual: UNCAPTURED
```

## Failed Gate-Pickup Spend Records

These are consumed-spend cases and are distinct from `initial_overlap_01`.

Staging (rev 2026-08-31): all four cases launch from the re-sited gate pickup
`(1.5, 1.5, 6.0)`. The wall case flies straight `+X` at `z=6.0` into
`world_gate_wall_01` (moved to `z∈[5.95,6.05]` to stay on that axis), so the wall is
hit head-on well before the 3.0 s timeout boundary. The ceiling case is re-staged as
a grounded launch beneath a lowered overhang: `world_gate_ceiling_01` now spans
`y∈[3.0,3.05]`, `x∈[3.8,10.0]`, `z∈[5.5,10.0]`. The ascending arc from
`(1.5, 1.5, 6.0)` crosses the overhang underside `y=3.0` at
`4.905t² − 6.15662t + 1.5 = 0` → `t = 0.330873 s`, `x ≈ 4.10732`, `z = 6.0` — strictly
inside the overhang footprint (≥ 0.25 m from the near `x` edge after projectile
radius, ≥ 0.45 m inside the `z` span), and the projectile's top (`y + 0.05`) is still
below `3.0` at the overhang's near face (`x=3.8`, `y_top ≈ 2.929`), so the first
contact is the ceiling underside, not a side face. The prior staging (raised feet at
`y=2.1` against a ceiling at `y=4.0`) never contacted the ceiling solid at all — its
arc overflew it — and is struck. The void case now runs in the dedicated
`geometry_throw_void_open_01` variant, which contains no solids by construction: the
timeout fallback (`last_simulated_sphere_center` at `t=3.0`, `x ≈ 25.14`) lies far
outside the room AABB by design and is covered by the variant's explicit
`aabb_policy=diagnostic_fallback_exempt` rather than being treated as a playable
endpoint. The previous staging reused `geometry_throw_flat_open_01`,
whose authored landing slab would have won a contact at `x ≈ 10.4` before the timeout
— a contradiction with the terminal contract
(`terminal_cause=Timeout`, `burst_state_after=Consumed`,
`terminal_publication_time=3.0 s`). The void variant is a harness-synthetic staging state: the fixture's geometry variants
define exactly the solids listed, and no room floor, slab, wall, or ceiling is
implicitly present in a variant's flight path. The authored `3.0 s` timeout boundary
in the void record is an expected fixed-tick contract value, not a measured runtime
publication or elapsed-time actual; the record remains `actual: UNCAPTURED` until a
runtime capture supplies provenance. Collision, NavMesh, DSP, Unity, and WebGL actuals
are likewise never inferred from these authored expectations. For `failed_death_01`,
`terminal_publication_time` is the death-cancellation time — a lifecycle/trace
ordering value only, per the registry's terminal contract — never a fact publication
time: the case still requires `fact_id: null`, `noise_count: 0`, no `t_publish`, no
relay, and no hearing deadline, and its `expected_landing.publication: none` refers to
the raw `NoisePublished` fact, not to this field.

```yaml
failed_gate_spends:
  - case_id: failed_wall_01
    stimulus: {pickup_id: burst_gate_pickup_01, launch_position_ws: [1.0, 1.5, 5.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, geometry_variant_id: geometry_gate_wall_01, config_profile: starter, death_at_s: null, reset_at_s: null}
    terminal_cause: Collision
    burst_state_after: Consumed
    terminal_publication_time: REQUIRED_RUNTIME_TERMINAL_PUBLICATION_TIME
    terminal_order: collision_resolved_before_timeout_then_publication
    terminal_timing: {winning_tick_index: REQUIRED_RUNTIME_WINNING_TICK_INDEX, flight_elapsed_s: REQUIRED_RUNTIME_FLIGHT_ELAPSED, contact_fraction: REQUIRED_RUNTIME_CONTACT_FRACTION, contact_event_time: REQUIRED_RUNTIME_CONTACT_EVENT_TIME, timeout_boundary_s: 3.0, comparison_result: contact_before_or_at_timeout}
    pickup_state: Consumed
    spend_committed: true
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    source_event_id: "flight:REQUIRED_RUNTIME_FLIGHT_HANDLE_ID"
    fact_id: REQUIRED_RUNTIME_FACT_ID
    noise_count: 1
    refund: false
    reserve_state: Placed
    reserve_route_id: reserve_route_after_failed_spends_01
    reanchor_patrol_clear: true
    reanchor_patrol_zone_id: reanchor_search_zone_failed_wall_01
    reanchor_patrol_extend_s: REQUIRED_RUNTIME_REANCHOR_PATROL_EXTEND_S
    reserve_visibility_window: REQUIRED_RUNTIME_RESERVE_VISIBILITY_WINDOW
    reserve_reachable_after_extend: true
    gate_state_after_failed_spend: blocked
    gate_exit_reachable_after_failed_spend: false
    rejection_code: none
    epoch_assertion: {transition: none, attempt_epoch_before: REQUIRED_RUNTIME_EPOCH, attempt_epoch_after: REQUIRED_RUNTIME_EPOCH, increment: 0, stale_prior_epoch_work: not_applicable}
    landing_position: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    landing_navmesh_status: off_navmesh
    landing_guard_reachable: false
    fallback_type: none
    gate_transition_eligibility: ineligible
    expected_landing: {geometry_variant_id: geometry_gate_wall_01, collision: true, contact: REQUIRED_RUNTIME_CONTACT}
    actual: UNCAPTURED
  - case_id: failed_ceiling_01
    stimulus: {pickup_id: burst_gate_pickup_01, launch_feet_position_ws: [1.0, 0.0, 5.0], launch_position_ws: [1.0, 1.5, 5.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, geometry_variant_id: geometry_gate_ceiling_01, config_profile: starter, death_at_s: null, reset_at_s: null}
    terminal_cause: Collision
    burst_state_after: Consumed
    terminal_publication_time: REQUIRED_RUNTIME_TERMINAL_PUBLICATION_TIME
    terminal_order: collision_resolved_before_timeout_then_publication
    terminal_timing: {winning_tick_index: REQUIRED_RUNTIME_WINNING_TICK_INDEX, flight_elapsed_s: REQUIRED_RUNTIME_FLIGHT_ELAPSED, contact_fraction: REQUIRED_RUNTIME_CONTACT_FRACTION, contact_event_time: REQUIRED_RUNTIME_CONTACT_EVENT_TIME, timeout_boundary_s: 3.0, comparison_result: contact_before_or_at_timeout}
    pickup_state: Consumed
    spend_committed: true
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    source_event_id: "flight:REQUIRED_RUNTIME_FLIGHT_HANDLE_ID"
    fact_id: REQUIRED_RUNTIME_FACT_ID
    noise_count: 1
    refund: false
    reserve_state: Placed
    reserve_route_id: reserve_route_after_failed_spends_01
    reanchor_patrol_clear: true
    reanchor_patrol_zone_id: reanchor_search_zone_failed_ceiling_01
    reanchor_patrol_extend_s: REQUIRED_RUNTIME_REANCHOR_PATROL_EXTEND_S
    reserve_visibility_window: REQUIRED_RUNTIME_RESERVE_VISIBILITY_WINDOW
    reserve_reachable_after_extend: true
    gate_state_after_failed_spend: blocked
    gate_exit_reachable_after_failed_spend: false
    rejection_code: none
    epoch_assertion: {transition: none, attempt_epoch_before: REQUIRED_RUNTIME_EPOCH, attempt_epoch_after: REQUIRED_RUNTIME_EPOCH, increment: 0, stale_prior_epoch_work: not_applicable}
    landing_position: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    landing_navmesh_status: off_navmesh
    landing_guard_reachable: false
    fallback_type: none
    gate_transition_eligibility: ineligible
    expected_landing: {geometry_variant_id: geometry_gate_ceiling_01, collision: true, contact: REQUIRED_RUNTIME_CONTACT}
    actual: UNCAPTURED
  - case_id: failed_void_01
    stimulus: {pickup_id: burst_gate_pickup_01, launch_position_ws: [1.0, 1.5, 5.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, geometry_variant_id: geometry_throw_void_open_01, config_profile: starter, death_at_s: null, reset_at_s: null}
    terminal_cause: Timeout
    burst_state_after: Consumed
    terminal_publication_time: REQUIRED_RUNTIME_TERMINAL_PUBLICATION_TIME
    terminal_order: timeout_at_3.0_then_fallback_publication
    terminal_timing: {winning_tick_index: REQUIRED_RUNTIME_WINNING_TICK_INDEX, flight_elapsed_s: 3.0, contact_fraction: null, contact_event_time: null, timeout_boundary_s: 3.0, comparison_result: timeout_wins}
    expected_timeout_elapsed_s: 3.0
    pickup_state: Consumed
    spend_committed: true
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    source_event_id: "flight:REQUIRED_RUNTIME_FLIGHT_HANDLE_ID"
    fact_id: REQUIRED_RUNTIME_FACT_ID
    noise_count: 1
    refund: false
    reserve_state: Placed
    reserve_route_id: reserve_route_after_failed_spends_01
    reanchor_patrol_clear: true
    reanchor_patrol_zone_id: reanchor_search_zone_failed_void_01
    reanchor_patrol_extend_s: REQUIRED_RUNTIME_REANCHOR_PATROL_EXTEND_S
    reserve_visibility_window: REQUIRED_RUNTIME_RESERVE_VISIBILITY_WINDOW
    reserve_reachable_after_extend: true
    gate_state_after_failed_spend: blocked
    gate_exit_reachable_after_failed_spend: false
    rejection_code: none
    epoch_assertion: {transition: none, attempt_epoch_before: REQUIRED_RUNTIME_EPOCH, attempt_epoch_after: REQUIRED_RUNTIME_EPOCH, increment: 0, stale_prior_epoch_work: not_applicable}
    landing_position: REQUIRED_RUNTIME_LAST_SIMULATED_SPHERE_CENTER
    landing_navmesh_status: void
    landing_guard_reachable: false
    fallback_type: void_last_simulated
    gate_transition_eligibility: ineligible
    expected_landing: {collision: false, timeout_s: 3.0, fallback: last_simulated_sphere_center}
    actual: UNCAPTURED
  - case_id: failed_death_01
    stimulus: {pickup_id: burst_gate_pickup_01, launch_position_ws: [1.0, 1.5, 5.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, geometry_variant_id: geometry_throw_flat_open_01, config_profile: starter, death_at_s: 0.5, reset_at_s: null}
    terminal_cause: DeathCancelled
    burst_state_after: Consumed
    terminal_publication_time: REQUIRED_RUNTIME_TERMINAL_PUBLICATION_TIME
    terminal_order: death_latched_before_collision_or_timeout
    terminal_timing: {winning_tick_index: null, flight_elapsed_s: REQUIRED_RUNTIME_FLIGHT_ELAPSED_AT_DEATH, contact_fraction: null, contact_event_time: null, timeout_boundary_s: 3.0, comparison_result: death_wins}
    pickup_state: Consumed
    spend_committed: true
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    source_event_id: "flight:REQUIRED_RUNTIME_FLIGHT_HANDLE_ID"
    fact_id: null
    noise_count: 0
    refund: false
    reserve_state: Placed
    reserve_route_id: reserve_route_after_failed_spends_01
    reanchor_patrol_clear: true
    reanchor_patrol_zone_id: null
    reanchor_patrol_extend_s: 0.0
    reserve_visibility_window: 0.0
    reserve_reachable_after_extend: true
    gate_state_after_failed_spend: blocked
    gate_exit_reachable_after_failed_spend: false
    rejection_code: none
    epoch_assertion: {transition: incremented_once, attempt_epoch_before: REQUIRED_RUNTIME_EPOCH, attempt_epoch_after: REQUIRED_RUNTIME_EPOCH, increment: 1, stale_prior_epoch_work: rejected_stale_epoch}
    stale_work_assertion: {flight_callback: ignored, late_collision: none, late_timeout: none, fact_id: null, relay: none, decision: none, audio: cancelled_before_presentation}
    landing_position: null
    landing_navmesh_status: not_applicable
    landing_guard_reachable: false
    fallback_type: not_applicable
    gate_transition_eligibility: ineligible
    expected_landing: {collision: false, timeout: not_reached, publication: none}
    actual: UNCAPTURED
```

## Trace and Sample Identity Contract

Every captured record must carry stable trace metadata. A validator rejects a record
without these fields or with an identity reused across distinct source events.

```yaml
trace_contract:
  session_id: REQUIRED_RUNTIME_SESSION_ID
  attempt_epoch: REQUIRED_RUNTIME_EPOCH
  sample_id: REQUIRED_RUNTIME_SAMPLE_ID
  source_timestamp_s: REQUIRED_RUNTIME_TIMESTAMP
  source_event_class_rank: REQUIRED_RUNTIME_SOURCE_CLASS_RANK
  source_event_id: "REQUIRED_RUNTIME_NAMESPACED_SOURCE_EVENT_ID"
  fact_id: REQUIRED_RUNTIME_FACT_ID
  flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
  entry_id: REQUIRED_RUNTIME_ENTRY_ID_OR_NONE
  epoch_transition_id: REQUIRED_RUNTIME_EPOCH_TRANSITION_ID_OR_NONE
  stale_work_assertion: required_for_death_cancelled_flight
  expected_actual_pair: required
  identity_reuse_within_session_epoch: forbidden
```

The placeholders above are capture requirements, not fabricated IDs. Serialized
Movement source records use `step:<step_id>` and serialized Burst source records use
`flight:<flight_handle_id>`; bare `step_id` and `flight_handle_id` remain internal
producer/allocation fields. Transport dedup uses `(session_id, attempt_epoch, fact_id)`;
Perception episode identity uses `entry_id`.

## Stable Fail-Closed Result Codes

The validator may return only a registered code from this set:

- `PASS`
- `MISSING_REQUIRED_FIELD`
- `MISSING_MARKER`
- `INVALID_CARDINALITY`
- `INVALID_TOLERANCE`
- `INVALID_F4_RANGE`
- `INVALID_ANGLE`
- `AUTHORED_THETA_OUTSIDE_BAND_DOMAIN`
- `INVALID_ROOT`
- `INVALID_ROUTE`
- `AMBIGUOUS_COLLISION`
- `AUTHORED_GRAZING_GEOMETRY_REJECTED`
- `INVALID_SPEED`
- `MOVEMENT_GATE_TRIGGER_REJECTED`
- `INITIAL_OVERLAP_QUERY_INCOMPLETE`
- `INITIAL_OVERLAP_REJECTED`
- `THROW_WHILE_MOVING_REJECTED`
- `EXPECTED_NEGATIVE_CASE`
- `E20_LAYER_MISMATCH`
- `TRIGGER_POLICY_MISMATCH`
- `ACTUAL_CAPTURE_UNAVAILABLE`
- `CONTRADICTORY_RECORD`
- `INVALID_PICKUP_TRANSITION`
- `INVALID_GATE_TRANSITION`
- `ENDPOINT_MODE_MISMATCH`
- `MISSING_LANDING_SURFACE`
- `EPOCH_BARRIER_MISMATCH`
- `STALE_WORK_NOT_CANCELLED`
- `GATE_OPENED_BY_INVALID_LANDING`
- `RESTART_TRANSACTION_MALFORMED`
- `RESTART_NOT_ATOMIC`
- `TEACHING_SEQUENCE_OUT_OF_ORDER`
- `SIGNAL_A_VISIBILITY_UNPROVEN`

Unknown, unexpected null, malformed, contradictory, or ambiguous values fail closed.
Null is permitted only where the record's outcome contract explicitly marks a field as
not applicable: no-launch identity fields on rejected cases, `fact_id` and publication
identity on the death-cancelled case, and `landing_position` when no landing occurs.
In particular, `REQUIRED_RUNTIME_VALUE`, `REQUIRED_RUNTIME_HASH`, and `UNCAPTURED`
cannot be accepted as `PASS`.

## AC20 Certification Checklist

`LevelFixture` reports `UNCAPTURED` or a stable failure code until each item is
verified; it never silently skips a malformed record.

- [ ] Manifest identity, schema version, validator identity, profile, provenance, and exact room AABB are present.
- [ ] Exactly one guard, one checkpoint, two pickup instances, one gate-entry AABB, and one gate-exit AABB are present with unique stable IDs.
- [ ] Every solid has exact `World` layer membership; every shared query records `QueryTriggerInteraction.Ignore`.
- [ ] Both pickups are sited at least `pickup_reach_radius` + agent radius (2.0 m) from every patrol corridor segment; the room envelope is self-consistent (`room_aabb_max.y == room_ceiling_y`).
- [ ] Marker and pickup lifecycle records prove full-restart restoration and death/segment non-restoration of spent pickups.
- [ ] Pickup interaction records prove an explicit reachable `Interact` edge, while pickup state-transition records prove `Placed → Carried`; `pickup-reached` remains telemetry-only and no pickup emits `NoisePublished`.
- [ ] Reserve route records include agent radius, area mask, PathComplete status, path samples, stable path hash, pickup reach, and E20 Linecast results before and after failed spends.
- [ ] Receiver records include noise kind/nominal radius, the source-specific hearing origin (raised feet origin for movement or published landing contact for Burst), endpoint authoring mode/source, the explicit `guard_feet_ws` falloff datum, eye endpoint (occlusion ray only), `dy_m`, `dy_max_m`, expected `R_eff`, hearing result, intended FSM response, geometry variant, and tolerance; synthetic endpoints never certify the playable MVP route.
- [ ] Occlusion records include exact endpoints, geometry variant, expected blocker, binary Linecast result, and tolerance.
- [ ] Positive throw records independently pass angle and range gates and include `D`, both roots, selected root, `R_actual`, explicit `landing_surface_id`/surface height, physical World-solid contact mode, ceiling/lateral margins, collision/landing envelope, expected values, actual values, and tolerance.
- [ ] Expected-negative angle and initial-overlap records are separate from positive certification and match stable rejection codes.
- [ ] Initial overlap rejects before spend; consumed failed-spend cases remain separate and preserve the reachable reserve route.
- [ ] The Level-owned gate transition records typed `from_state`/`to_state` values, binds `trigger_noise_kind=burst` and the chain's `landing_fact_id`, matches the captured investigate decision's session, epoch, `fact_id`, and `entry_id`, and carries `receiver_phase_window_id` resolving to a declared patrol window plus a non-empty `receiver_phase_window[]` interval capture (`patrol_pre_spend_01` — the patrol-phase interval in which the certified landing is hearable at the recorded `R_eff`; a null/unknown id or missing interval fails `INVALID_GATE_TRANSITION`).
- [ ] Death during flight increments `attempt_epoch` exactly once, cancels prior-epoch flight callbacks and queued work before presentation, and records no late collision, timeout, fact, relay, or decision; stale work cannot pass certification.
- [ ] Runtime, preview, and fixture use exact center-segment SphereCast distance, one closest hit, contact push-out, zero-length rule, timeout precedence, and no hit-buffer fallback.
- [ ] Runtime records one pre-batch global `SyncTransforms` and does not toggle `autoSyncTransforms` per query.
- [ ] MVP response is single-guard `Investigate`; Target-only peer/zone responses are rejected in this fixture.
- [ ] Every actual field and trace/sample identity is captured; nominal-radius-only or prose-only evidence fails closed.
- [ ] `teaching_sequence` is ordered and proves Walk/Run signal demonstrations before the certified Burst spend; the spend beat originates inside `patrol_pre_spend_01` (settled `Patrol`, no re-anchor or give-up in flight).
- [ ] `signal_a_observation_pockets` binds every `signal_a_visibility` record to a cover solid, geometry variant, pocket bounds, entry/exit routes, and player clearance/containment predicates; a clear vantage-to-guard LOS and bounded orient-tell window with a captured noise-caused `investigate-commit` are then required.
- [ ] Reserve safety records bind the reserve route to an exact search-volume geometry, guard clearance radius, and fixed virtual-tick continuous-sweep predicate; the reserve remains reachable and outside the guard's extended search after every failed spend. Each wall/ceiling case has a grounded floor, and the void case is explicitly open-volume diagnostic only.
- [ ] Route records bind `geometry_binding_id` and `route_safety_predicate_id`, proving physical room containment, a pickup-to-vantage approach, a stationary Throw boundary, a reachable exit after the causal gate transition, and an exit/non-reentry path that does not reuse the closed gate corridor or bound search volume.
- [ ] `restart_transaction` proves discoverable full-room restart is atomic: both pickups return to `Placed`, carried slot clears, gate markers reset, and prior-epoch work is discarded.
- [ ] No invalid, fallback, or death-cancelled landing appears in a gate causal chain; otherwise `GATE_OPENED_BY_INVALID_LANDING` is returned.

## Remaining Authoring Notes (non-blocking)

The selected route-closure revision resolves the former physical-containment gaps:
`geometry_receiver_clear_01` now owns explicit World floor, ceiling, perimeter-wall,
and Signal-A observation-pocket solids; grounded wall and ceiling failed-spend variants
own explicit floors; the certified teaching throw has a pickup-to-vantage route; and
teaching, reserve/search, exit-safety, and non-reentry requirements are explicit. These
are authored prerequisites, not runtime proof: all NavMesh, PhysicsScene, LOS, guard
search, audio, and target-platform actuals remain `UNCAPTURED` until captured.

- `target_marker_id: burst_gate_entry_01` is logical-objective semantics per the
  registry; no physical landing-target marker exists, and none is required for MVP.
- `geometry_throw_void_open_01` remains intentionally floorless/open-volume diagnostic
  content and cannot certify a playable landing, gate transition, or route.
- Geometry variant records carry purpose strings instead of an owning-column
  provenance field; the registry field list is authoritative and deliberately does
  not include an owner key.
