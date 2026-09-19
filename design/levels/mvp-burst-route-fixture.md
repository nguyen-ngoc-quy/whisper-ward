# MVP Burst Route Fixture

> **Status**: Canonical design fixture manifest — schema 1.2, authored 2026-08-29, revised 2026-08-31 (feet-datum falloff probes, gate `kind`/`landing_fact_id` binding, registry-schema record conformance, pickup siting clearance, ceiling overhang re-stage, void-variant separation)  
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
  schema_version: WW-MVP-BURST-ROUTE-1.2
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
    source_revision: 2026-08-31
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
`route_evidence`, `route_causality`, `gate_transitions`,
`synthetic_receiver_endpoints`, `receiver_probes`, `occlusion_probes`,
`valid_throw_volumes`, `negative_validation_cases`, `failed_gate_spends`,
`teaching_sequence`, `signal_a_visibility`, `restart_transaction`,
`guard_extended_search_patrol`, `workload_profiles`, and `trace_contract`. The
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
  room_aabb_max: [16.0, 4.0, 16.0]
  room_ceiling_y: 4.0
  e20_world_layer: World
  forbidden_layers: [Player, Guard, trigger]
  query_trigger_policy: QueryTriggerInteraction.Ignore
  physics_scene: default_gameplay
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
    required_budget_ms: 2.0
    cap_controls: [max_hearing_boundaries_per_frame, max_hearing_guards_per_boundary, max_hearing_facts_per_boundary, max_hearing_pairs_per_boundary]
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
    required_budget_ms: 2.0
    cap_controls: [max_hearing_guards_per_boundary, max_hearing_facts_per_boundary, max_hearing_pairs_per_boundary]
    actual: UNCAPTURED
```

`actual` for both profiles requires measured duration, query counters, queue/deferred
counters, and replay identity fields. Missing capture remains `UNCAPTURED` and cannot
satisfy the performance gate.

## Coordinate, Layer, and Runtime Contract

- World frame is Y-up, metres, with X/Z as the hearing plane.
- Room bounds are the complete AABB snapshot `min=(0.0,0.0,0.0)`,
  `max=(16.0,4.0,16.0)`. The room envelope is self-consistent by construction:
  `room_aabb_max.y == room_ceiling_y == 4.0 m`, so no authored solid, launch
  position, published contact, or synthetic probe may exceed `y=4.0 m`.
- All solid geometry has `layer=World`, the exact configured E20 layer name.
  `Player`, `Guard`, and `trigger` bits are excluded.
- All shared queries use `QueryTriggerInteraction.Ignore`.
- The default gameplay `PhysicsScene` is the query scene. One global
  `Physics.SyncTransforms()` occurs before each Burst simulation batch; E20 applies
  to the subsequent queries, not to synchronization.
- `Physics.autoSyncTransforms=false` is the custom Burst service policy. The service
  never toggles it per query and records the prior setting on disposal.
- `f12_noise_origin_offset=0.25 m`, `guard_eye_height=1.6 m`,
  `hearing_y_hard_cutoff=4.0 m`, and the Burst starter `R_burst=10.5 m` are loaded
  from the registry. The legal loaded `R_burst` range is `9.0–14.0 m`, subject to
  `R_burst ≤ R_vis`; this fixture uses the starter value.
- `root_policy=minimum_strictly_positive`; positive repeated roots at `D = 0` are
  accepted when the repeated root is strictly positive, while Level-owned grazing
  geometry is rejected separately with `AUTHORED_GRAZING_GEOMETRY_REJECTED`.
- `diagnostic_simulation_envelope_m` is not a gameplay comparison tolerance; gameplay
  position/contact/route assertions use the registry `CompareTolerance` (≤ 5 mm).
- The fixture does not certify the 2.0 ms or 12.0 ms performance claims. Those are
  the pending OQ6 target-hardware gate.

## Stable Markers and Lifecycle Records

Every marker record has a stable ID, type, world-space position or AABB, layer,
source, expected lifecycle, and an `actual` capture slot. A validator must reject a
missing marker, duplicate ID, unexpected layer, or unknown lifecycle state.

| Record | Stable ID | Expected fixture value / invariant |
|---|---|---|
| Checkpoint | `checkpoint_mvp_01` | point `(1.0,0.0,1.0)`; full-restart origin and reserve-route origin; expected lifecycle `active` |
| Gate pickup | `burst_gate_pickup_01` | point `(1.5,0.0,6.0)`; initial lifecycle `Placed`; only pickup whose failed spend is exercised; sited ≥ `pickup_reach_radius` (1.6 m) + agent radius (0.4 m) = 2.0 m from every patrol corridor segment (nearest segment (4,8)→(4,12) at ≈ 3.20 m) |
| Reserve pickup | `burst_reserve_pickup_01` | point `(2.0,0.0,14.0)`; initial lifecycle `Placed`; must remain reachable after failed gate cases; sited ≥ 2.0 m from every patrol corridor segment (nearest segment (4,8)→(4,12) at ≈ 2.83 m) |
| Gate entry | `burst_gate_entry_01` | AABB centre `(5.5,0.0,8.0)`, dimensions `(3.0,2.0,3.0) m`; receiver `guard_mvp_01`; `room_ceiling_y=4.0 m` |
| Gate exit | `burst_gate_exit_01` | AABB centre `(12.0,0.0,8.0)`, dimensions `(2.0,2.0,3.0) m`; reachable only after the certified Burst interaction |
| Guard patrol | `patrol_mvp_01` | closed route `(4,0,8) → (8,0,8) → (8,0,12) → (4,0,12)`; all nodes expected `PathComplete` |
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
    world_position_or_aabb: {position: [1.5, 0.0, 6.0]}
    layer: World
    expected_initial_state: Placed
    expected_transition_states: {failed_spend: Consumed, initial_overlap_rejection: Carried, full_restart: Placed}
    actual: UNCAPTURED
  - stable_id: burst_reserve_pickup_01
    type: pickup
    world_position_or_aabb: {position: [2.0, 0.0, 14.0]}
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
    world_position_or_aabb: {route_nodes: [[4.0, 0.0, 8.0], [8.0, 0.0, 8.0], [8.0, 0.0, 12.0], [4.0, 0.0, 12.0]]}
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

```yaml
pickup_interactions:
  - interaction_id: pickup_interact_gate_01
    pickup_id: burst_gate_pickup_01
    reach_route_id: gate_route_open_01
    trigger: Interact
    expected_precondition: {pickup_state: Placed, reachable: true, distance_within_reach: true, clear_linecast: true}
    expected_result: accepted
    telemetry_event: pickup-reached
    telemetry_only: true
    emits_noise_published: false
    actual: UNCAPTURED
  - interaction_id: pickup_interact_reserve_01
    pickup_id: burst_reserve_pickup_01
    reach_route_id: reserve_route_open_01
    trigger: Interact
    expected_precondition: {pickup_state: Placed, reachable: true, distance_within_reach: true, clear_linecast: true}
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

| Variant ID | Fixture geometry | Layer / trigger policy | Expected purpose |
|---|---|---|---|
| `geometry_receiver_clear_01` | no solid intersects the receiver segment; no trigger intersects it | solids `World`; triggers ignored | clear hearing and clear Linecast |
| `geometry_receiver_wall_01` | solid `world_wall_receiver_01`, AABB min `(7.95,0.0,9.2)`, max `(8.05,3.0,9.3)` | `World`; `QueryTriggerInteraction.Ignore` | binary blocked hearing |
| `geometry_receiver_trigger_01` | trigger `receiver_trigger_01`, AABB min `(7.95,0.0,9.2)`, max `(8.05,3.0,9.3)` | `trigger`; ignored | prove trigger does not block |
| `geometry_gate_ceiling_01` | certified ceiling contact overhang `world_gate_ceiling_01` at `y∈[3.0,3.05]`, grounded launch corridor beneath it | `World`; queryable | negative/failed-spend ceiling case |
| `geometry_gate_wall_01` | solid `world_gate_wall_01`, AABB min `(4.0,0.0,5.95)`, max `(4.1,3.0,6.05)` | `World`; queryable | negative/failed-spend wall case |
| `geometry_throw_flat_open_01` | certified launch corridor with the authored flat landing slab `world_throw_flat_landing_01`; no solid intersects before the landing contact envelope | `World`; triggers ignored | positive flat throw volume |
| `geometry_throw_lower_open_01` | certified launch corridor with the authored lower landing slab `world_throw_lower_landing_01`; no solid intersects before the landing contact envelope | `World`; triggers ignored | positive unequal-height throw volume |
| `geometry_throw_void_open_01` | no solids at all — an open volume whose flight has no authored floor, slab, wall, or ceiling anywhere along its path | `World`; triggers ignored | negative/failed-spend timeout-void case (harness-synthetic) |

```yaml
geometry_variants:
  - variant_id: geometry_receiver_clear_01
    source_records: []
    expected_purpose: clear hearing and clear Linecast
  - variant_id: geometry_receiver_wall_01
    source_records: [world_wall_receiver_01]
    expected_purpose: binary blocked hearing
  - variant_id: geometry_receiver_trigger_01
    source_records: [receiver_trigger_01]
    expected_purpose: trigger ignored by Linecast
  - variant_id: geometry_gate_ceiling_01
    source_records: [world_gate_ceiling_01]
    expected_purpose: ceiling failed-spend case
  - variant_id: geometry_gate_wall_01
    source_records: [world_gate_wall_01]
    expected_purpose: wall failed-spend case
  - variant_id: geometry_throw_flat_open_01
    source_records: [world_throw_flat_landing_01]
    expected_purpose: positive flat throw volume with physical landing surface
  - variant_id: geometry_throw_lower_open_01
    source_records: [world_throw_lower_landing_01]
    expected_purpose: positive unequal-height throw volume with physical landing surface
  - variant_id: geometry_throw_void_open_01
    source_records: []
    expected_purpose: timeout-void failed-spend case with no authored collision geometry
  - variant_id: geometry_grazing_edge_01
    source_records: [world_grazing_edge_01]
    expected_purpose: Level-owned tangent/edge contact rejection; not an F4 root rejection
solids:
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
    surface_top_y_m: null
    bounds_min: [8.0, 0.0, 1.95]
    bounds_max: [8.02, 1.5, 2.05]
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
referenced physical landing solid is invalid positive evidence.

## Teaching Sequence

The route teaches the movement signal before asking the player to spend a limited
Burst. Records use the registry `teaching_sequence` field contract (rev 2026-08-31);
`actual: UNCAPTURED` is retained until runtime capture.

Two patrol phase windows govern the sequence (defined here, referenced by id):

- `patrol_settled_after_spawn_01` — `loop_index ≥ 1`, guard state `Patrol`, and the
  first patrol leg after spawn churn is complete. Qualifies the movement-teaching
  beats; no spend is permitted inside it.
- `patrol_pre_spend_01` — `loop_index ≥ 1`, guard state `Patrol`, no re-anchor or
  give-up timer in flight, and the guard on a leg away from the gate pickup
  corridor. Qualifies the certified spend beat: a re-anchor or give-up in flight at
  the spend moment voids the gate causal chain, so the spend must originate inside
  this window.

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
    actual: UNCAPTURED
  - step_id: teach_walk_signal_01
    order: 3
    beat: walk_signal
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [1.0, 0.0, 1.0]
    player_route_samples: [[1.0, 0.0, 1.0], [1.5, 0.0, 3.5], [1.5, 0.0, 5.0]]
    player_observation_position_ws: [1.5, 0.0, 5.0]
    expected_stride_state: Walk
    expected_step_event_ids: [REQUIRED_RUNTIME_STEP_EVENT_IDS]
    expected_rustle_fires: 0
    expected_guard_state: Patrol
    patrol_phase_window: patrol_settled_after_spawn_01
    expected_guard_observable_response: none_no_response_raw_noise_published_only
    expected_crouch_control_no_fact: null
    expected_teaching_outcome: walking_emits_step_pips_published_as_raw_noise_outside_r_eff_no_fsm_response
    pre_burst_requirement: null
    actual: UNCAPTURED
  - step_id: teach_run_signal_01
    order: 4
    beat: run_signal
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [1.0, 0.0, 1.0]
    player_route_samples: [[1.0, 0.0, 1.0], [1.5, 0.0, 3.5], [1.5, 0.0, 5.0]]
    player_observation_position_ws: [1.5, 0.0, 5.0]
    expected_stride_state: Run
    expected_step_event_ids: [REQUIRED_RUNTIME_STEP_EVENT_IDS]
    expected_rustle_fires: 0
    expected_guard_state: Patrol
    patrol_phase_window: patrol_settled_after_spawn_01
    expected_guard_observable_response: none_no_response_raw_noise_published_only
    expected_crouch_control_no_fact: null
    expected_teaching_outcome: running_emits_faster_louder_pips_published_as_raw_noise_outside_r_eff_no_fsm_response
    pre_burst_requirement: null
    actual: UNCAPTURED
  - step_id: teach_gate_pickup_01
    order: 5
    beat: pickup_burst
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [1.0, 0.0, 1.0]
    player_route_samples: [[1.0, 0.0, 1.0], [1.5, 0.0, 3.5], [1.5, 0.0, 6.0]]
    player_observation_position_ws: [1.5, 0.0, 6.0]
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
    geometry_variant_id: geometry_throw_flat_open_01
    player_start_position_ws: [1.5, 0.0, 6.0]
    player_route_samples: []
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
    actual: UNCAPTURED
  - step_id: teach_gate_open_01
    order: 7
    beat: gate_open_verification
    geometry_variant_id: geometry_receiver_clear_01
    player_start_position_ws: [4.0, 0.0, 7.5]
    player_route_samples: []
    player_observation_position_ws: [4.0, 0.0, 7.5]
    expected_stride_state: none
    expected_step_event_ids: []
    expected_rustle_fires: 0
    expected_guard_state: Investigate
    patrol_phase_window: null
    expected_guard_observable_response: gate_state_blocked_to_open_observed_after_investigate_commit
    expected_crouch_control_no_fact: null
    expected_teaching_outcome: gate_causal_chain_completed_from_the_pre_spend_spend_beat
    pre_burst_requirement: null
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
    actual: UNCAPTURED
```

`noise_heard` in the teaching beats means the raw `NoisePublished` emission event
(the player-visible emission feedback), not a guard relay: the Walk and Run
demonstrations run ~8.5 m from the guard, outside `R_walk_eff ≈ 3.75 m`, so no
relay or FSM response is expected — the guard-observable response is demonstrated
by the certified spend beat inside `patrol_pre_spend_01`. A `Burst` may not be
spent before the Walk and Run demonstrations have been observed (orders 3 and 4
precede the spend at order 6). Out-of-order or missing steps fail with
`TEACHING_SEQUENCE_OUT_OF_ORDER`; this ordering is a content requirement, not a
claim that runtime evidence exists.

## Signal-A Visibility

Signal-A is the player-facing post-throw vantage: the guard's orientation response
must be observable from a clear line-of-sight vantage without a persistent HUD.
The guard response window is bounded by the registered FSM tick and remains an
uncaptured runtime assertion.

```yaml
signal_a_visibility:
  - record_id: signal_a_vantage_gate_01
    causality_id: gate_burst_to_exit_01
    vantage_position_ws: [4.0, 0.0, 7.5]
    vantage_to_guard_linecast_clear: true
    expected_guard_initial_state: Patrol
    patrol_phase: patrol_pre_spend_01
    source_timestamp: REQUIRED_RUNTIME_SOURCE_TIMESTAMP
    expected_burst_landing_position_ws: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    expected_noise_heard_relay_id: REQUIRED_RUNTIME_FACT_ID
    expected_investigate_commit_id: REQUIRED_RUNTIME_DECISION_ID
    expected_orient_tell_window_s: {lower_bound_s: 0.0, upper_bound_s: REQUIRED_RUNTIME_FSM_TICK_S}
    expected_fsm_tick_bound_s: REQUIRED_RUNTIME_FSM_TICK_S
    visibility_exclusions: [HideSpotFront, Chase, cooldown, stale_epoch]
    expected_visible_causal_chain: [burst_landed, noise_heard_relay, investigate_commit, orient_tell]
    actual: UNCAPTURED
  - record_id: signal_a_vantage_exit_01
    causality_id: gate_burst_to_exit_01
    vantage_position_ws: [10.0, 0.0, 12.0]
    vantage_to_guard_linecast_clear: true
    expected_guard_initial_state: Patrol
    patrol_phase: patrol_pre_spend_01
    source_timestamp: REQUIRED_RUNTIME_SOURCE_TIMESTAMP
    expected_burst_landing_position_ws: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    expected_noise_heard_relay_id: REQUIRED_RUNTIME_FACT_ID
    expected_investigate_commit_id: REQUIRED_RUNTIME_DECISION_ID
    expected_orient_tell_window_s: {lower_bound_s: 0.0, upper_bound_s: REQUIRED_RUNTIME_FSM_TICK_S}
    expected_fsm_tick_bound_s: REQUIRED_RUNTIME_FSM_TICK_S
    visibility_exclusions: [HideSpotFront, Chase, cooldown, stale_epoch]
    expected_visible_causal_chain: [burst_landed, noise_heard_relay, investigate_commit, orient_tell]
    actual: UNCAPTURED
```

A missing clear vantage-to-guard LOS, an unbounded orient tell, or a missing
noise-caused `investigate-commit` fails with `SIGNAL_A_VISIBILITY_UNPROVEN`. The
`expected_burst_landing_position_ws` of every record is the referenced chain's
captured published landing — never an independently authored or synthetic source —
and the visible chain is asserted end-to-end: landing → `NoiseHeardRelay` →
`investigate-commit` → visible guard orientation from the vantage.

## Reserve Safety and Extended Search

The reserve pickup is the recovery route after failed gate spends. It must remain
outside the guard's extended search area while still reachable from the checkpoint.

```yaml
guard_extended_search_patrol:
  - zone_id: reanchor_search_zone_gate_01
    causality_case_ids: [failed_wall_01, failed_ceiling_01, failed_void_01, failed_death_01]
    noise_origin_ws: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    investigation_zone_bounds: {min: [5.5, 0.0, 10.0], max: [13.5, 0.0, 16.0]}
    patrol_bounds: {route_nodes: [[4.0, 0.0, 8.0], [8.0, 0.0, 8.0], [8.0, 0.0, 12.0], [4.0, 0.0, 12.0]]}
    expected_reanchor_extend_s: REQUIRED_RUNTIME_REANCHOR_PATROL_EXTEND_S
    s_diff_resolved: 1.0
    visibility_window_s: REQUIRED_RUNTIME_RESERVE_VISIBILITY_WINDOW
    reserve_route_clear_during_search: true
    actual: UNCAPTURED
```

The zone bounds envelope the certified landing area `(≈ 9.66, 0.0, ≈ 14.16)` with a
search margin; the reserve route (`x = 2.0`, `z ∈ [10.5, 14.0]`) stays at least
`≈ 3.5 m` outside the zone's minimum `x` bound, so `reserve_route_clear_during_search`
is satisfiable by construction. `expected_reanchor_extend_s` is computed from the
canonical `reanchor_giveup_timeout` formula with the level's resolved difficulty
scalar `s_diff_resolved = 1.0` (the S_DIFF speed-ratio form is struck rev 2026-08-31);
`visibility_window_s` must cover the full `expected_reanchor_extend_s` window. A
reserve route intersecting the guard's extended search during that window is a
certification failure, not a player routing failure.

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
  actual: UNCAPTURED
```

Malformed or partially applied recovery fails with `RESTART_TRANSACTION_MALFORMED`
or `RESTART_NOT_ATOMIC`; a failed transaction never opens the gate or reports a
successful recovery.

## Reserve Route Evidence

The expected route is:

```text
checkpoint_mvp_01
  → reserve_approach_01 (2.0, 0.0, 10.5)
  → reserve_approach_02 (2.0, 0.0, 13.0)
  → burst_reserve_pickup_01 (2.0, 0.0, 14.0)
```

The route record must serialize the agent radius, area mask, path status, sampled
corners, stable path hash, pickup reach result, and the E20 Linecast result. The
expected fields below are not a captured pass; `actual` remains `UNCAPTURED`.

```yaml
route_evidence:
  - route_id: gate_route_open_01
    from_marker: checkpoint_mvp_01
    to_marker: burst_gate_pickup_01
    obstruction_ids: []
    player_agent_radius_m: 0.30
    navmesh_area_mask: 1
    expected_path_status: PathComplete
    path_complete: true
    path_samples: [[1.0,0.0,1.0], [1.5,0.0,3.5], [1.5,0.0,6.0]]
    stable_path_hash: REQUIRED_RUNTIME_HASH
    tolerance_class: gameplay_contract
    pickup_reach: {reachable: true, distance_within_reach: true, clear_linecast: true, query_trigger_policy: QueryTriggerInteraction.Ignore}
    e20_linecast: {clear: true, trigger_policy: QueryTriggerInteraction.Ignore}
    gate_state: closed
    failed_gate_spend: false
    required_failed_cases: []
    actual: UNCAPTURED
  - route_id: gate_exit_reachable_after_burst_01
    from_marker: burst_gate_entry_01
    to_marker: burst_gate_exit_01
    obstruction_ids: []
    player_agent_radius_m: 0.30
    navmesh_area_mask: 1
    expected_path_status: PathComplete
    path_complete: true
    path_samples: [[5.5,0.0,8.0], [8.0,0.0,8.0], [12.0,0.0,8.0]]
    stable_path_hash: REQUIRED_RUNTIME_HASH
    tolerance_class: gameplay_contract
    pickup_reach: {reachable: true, distance_within_reach: true, clear_linecast: true}
    e20_linecast: {clear: true, trigger_policy: QueryTriggerInteraction.Ignore}
    gate_state: open
    failed_gate_spend: false
    required_failed_cases: []
    actual: UNCAPTURED
  - route_id: reserve_route_open_01
    from_marker: checkpoint_mvp_01
    to_marker: burst_reserve_pickup_01
    obstruction_ids: []
    player_agent_radius_m: 0.30
    navmesh_area_mask: 1
    expected_path_status: PathComplete
    path_complete: true
    path_samples: [[1.0,0.0,1.0], [2.0,0.0,10.5], [2.0,0.0,13.0], [2.0,0.0,14.0]]
    stable_path_hash: REQUIRED_RUNTIME_HASH
    tolerance_class: gameplay_contract
    pickup_reach: {reachable: true, distance_within_reach: true, clear_linecast: true}
    e20_linecast: {clear: true, trigger_policy: QueryTriggerInteraction.Ignore}
    gate_state: closed
    failed_gate_spend: false
    required_failed_cases: []
    actual: UNCAPTURED
  - route_id: reserve_route_after_failed_spends_01
    from_marker: checkpoint_mvp_01
    to_marker: burst_reserve_pickup_01
    obstruction_ids: []
    player_agent_radius_m: 0.30
    navmesh_area_mask: 1
    expected_path_status: PathComplete
    path_complete: true
    path_samples: [[1.0,0.0,1.0], [2.0,0.0,10.5], [2.0,0.0,13.0], [2.0,0.0,14.0]]
    stable_path_hash: REQUIRED_RUNTIME_HASH
    tolerance_class: gameplay_contract
    pickup_reach: {reachable: true, distance_within_reach: true, clear_linecast: true}
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
    accepted_throw_source_event_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    throw_snapshot_id: REQUIRED_RUNTIME_THROW_SNAPSHOT_ID
    throw_snapshot_parity: field_for_field_with_controller_capture
    landing_surface_id: REQUIRED_RUNTIME_LANDING_SURFACE_ID
    landing_contact_probe_id: REQUIRED_RUNTIME_LANDING_CONTACT_PROBE_ID
    landing_fact_id: REQUIRED_RUNTIME_FACT_ID
    receiver_probe_id: receiver_from_captured_landing_01
    hearing_relay_fact_id: REQUIRED_RUNTIME_FACT_ID
    entry_id: REQUIRED_RUNTIME_ENTRY_ID
    investigate_commit_id: REQUIRED_RUNTIME_DECISION_ID
    gate_transition_id: gate_state_blocked_to_open_01
    gate_entry_marker_id: burst_gate_entry_01
    expected_gate_entry_transition: blocked_to_open
    gate_exit_marker_id: burst_gate_exit_01
    gate_exit_reach_route_id: gate_exit_reachable_after_burst_01
    expected_gate_exit_reachable: true
    route_evidence_id: gate_exit_reachable_after_burst_01
    expected_order: [pickup_reached, pickup_interact, pickup_state_transition, throw_accepted, throw_snapshot_captured, landing_surface_contact, burst_landed, noise_published, relay_heard, investigate_commit, gate_state_transition, gate_exit_reachable]
    actual: UNCAPTURED
  - causality_id: gate_exit_reachable_after_burst_01
    pickup_marker_id: burst_gate_pickup_01
    pickup_reach_route_id: gate_route_open_01
    pickup_interaction_id: pickup_interact_gate_01
    pickup_state_transition_id: pickup_state_gate_placed_to_carried_01
    accepted_throw_source_event_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    throw_snapshot_id: REQUIRED_RUNTIME_THROW_SNAPSHOT_ID
    throw_snapshot_parity: field_for_field_with_controller_capture
    landing_surface_id: REQUIRED_RUNTIME_LANDING_SURFACE_ID
    landing_contact_probe_id: REQUIRED_RUNTIME_LANDING_CONTACT_PROBE_ID
    landing_fact_id: REQUIRED_RUNTIME_FACT_ID
    receiver_probe_id: receiver_from_captured_landing_01
    hearing_relay_fact_id: REQUIRED_RUNTIME_FACT_ID
    entry_id: REQUIRED_RUNTIME_ENTRY_ID
    investigate_commit_id: REQUIRED_RUNTIME_DECISION_ID
    gate_transition_id: gate_state_blocked_to_open_01
    gate_entry_marker_id: burst_gate_entry_01
    expected_gate_entry_transition: open
    gate_exit_marker_id: burst_gate_exit_01
    gate_exit_reach_route_id: gate_exit_reachable_after_burst_01
    expected_gate_exit_reachable: true
    route_evidence_id: gate_exit_reachable_after_burst_01
    expected_order: [pickup_reached, pickup_interact, pickup_state_transition, throw_accepted, throw_snapshot_captured, landing_surface_contact, burst_landed, noise_published, relay_heard, investigate_commit, gate_state_transition, gate_exit_reachable]
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
    trigger_noise_kind: Burst
    trigger_decision_id: REQUIRED_RUNTIME_DECISION_ID
    causal_session_id: REQUIRED_RUNTIME_SESSION_ID
    causal_attempt_epoch: REQUIRED_RUNTIME_EPOCH
    causal_fact_id: REQUIRED_RUNTIME_FACT_ID
    landing_fact_id: REQUIRED_RUNTIME_FACT_ID
    causal_entry_id: REQUIRED_RUNTIME_ENTRY_ID
    expected_cause: noise
    expected_order_after: investigate_commit
    expected_order_before: gate_exit_reachable
    actual: UNCAPTURED
```

`gate_transitions[].owner=Level` is authoritative for the physical gate state;
NoiseEmitter, Perception, and the FSM publish or consume causal identities but do
not mutate the gate. The transition is valid only when its before/after states,
`investigate_commit` decision, `fact_id`, `entry_id`, session, and epoch match the
same captured route chain. `trigger_noise_kind` binds the gate-open record to the
noise kind that caused it (`Burst` for this fixture — a movement pip can never open
the gate), and `landing_fact_id` binds it to the captured published landing fact of
that `Burst`; a gate-open record without both bindings, or with a bound landing that
is not the same chain's landing contact, fails with `MISSING_REQUIRED_FIELD` or
`INVALID_GATE_TRANSITION`. A landing with `invalid`, `fallback`, or
`death_cancelled` terminal status is forbidden from appearing in any
`gate_transitions[]` causal chain; the validator returns `GATE_OPENED_BY_INVALID_LANDING`
if it does. Because `trigger_noise_kind=Burst` and `landing_fact_id` are mandatory,
a movement pip cannot appear as a gate-opening cause: `route_causality` defines no
movement-pip chain into `gate_transitions`, and a patrol-state guard hearing a
movement pip produces at most an `Investigate`, never a gate-opening transition.

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
(`R_walk_eff ≈ 3.75 m`, `R_run_eff ≈ 5.63 m`) and flat Burst landings measure
`dy_m ≈ 0` → `R_burst_eff ≈ 10.37 m`. Every probe carries the explicit
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
    noise_kind: Burst
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
    geometry_variant_id: geometry_throw_flat_open_01
    noise_kind: Burst
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
    boundary_semantics: derived_from_captured_landing
    tol_m: 0.005
    tolerance_class: gameplay_contract
    actual: UNCAPTURED
  - probe_id: receiver_movement_raised_origin_01
    geometry_variant_id: geometry_receiver_clear_01
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
    noise_kind: Burst
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
    noise_kind: Burst
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
    noise_kind: Burst
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
    noise_kind: Burst
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
    trigger_ignored: false
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
    trigger_ignored: false
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
    trigger_ignored: true
    tol_m: 0.001
    tolerance_class: oracle_quantizer
    actual: UNCAPTURED
```

## Valid Throw-Volume Records

Only `valid_throw_volumes` participate in positive production certification. Each
record includes the launch inputs, both quadratic roots, selected root, range gate,
ceiling/lateral margins, collision/landing envelope, expected values, actual values,
and tolerance. The runtime, preview, and validator use the same F4 and collision
oracle. In this fixture, `target_marker_id` names the logical gate-entry objective
that the Burst noise is intended to open; it is not a claim that the projectile center
must land inside that marker's AABB. Physical landing is certified by the launch
snapshot, the referenced `landing_surface_id`, `open_volume_bounds`, F4 range, and
`landing_envelope`. The landing surface is a real `World` solid in the geometry
variant, not an open-volume annotation or a nominal-radius assumption. A future
fixture that requires a physical target must add a separate landing-target marker
field rather than overloading `target_marker_id`.

```yaml
valid_throw_volumes:
  - volume_id: throw_flat_starter_01
    geometry_variant_id: geometry_throw_flat_open_01
    launch_position_ws: [1.5, 1.5, 6.0]
    launch_direction_xz: [0.70710678, 0.70710678]
    launch_source_marker_id: burst_gate_pickup_01
    open_volume_bounds: {min: [1.4, 0.0, 5.9], max: [11.4, 3.95, 15.8]}
    landing_surface_id: world_throw_flat_landing_01
    landing_surface_y_m: 0.0
    landing_contact_mode: surface_top_world_solid
    target_marker_id: burst_gate_entry_01
    route_id: gate_route_open_01
    throw_snapshot_id: REQUIRED_RUNTIME_THROW_SNAPSHOT_ID
    throw_snapshot_parity: required_field_for_field_with_controller_capture
    v0_mps: 10.0
    theta_deg: 38.0
    h_release_m: 1.5
    h_landing_m: 0.0
    delta_y_m: 1.5
    projectile_radius_m: 0.05
    epsilon_contact_m: 0.001
    contact_ambiguity_tolerance_m: 0.001
    angle_domain_result: PASS
    discriminant_m2ps2: 67.33390522001662
    t_minus_s: -0.20887963068565404
    t_plus_s: 1.4640508342038154
    selected_t_land_s: 1.4640508342038154
    root_policy: minimum_strictly_positive
    r_actual_m: 11.536878011794986
    range_gate: PASS
    ceiling_margin_m: 0.518098612638639
    lateral_margin_m: 0.4
    collision: landing_surface_contact
    landing_envelope: [11.53, 11.55]
    expected:
      discriminant_m2ps2: 67.33390522001662
      t_land_s: 1.4640508342038154
      r_actual_m: 11.536878011794986
    formula_relative_tol: 0.001
    placement_tol_m: 0.005
    tolerance_class: gameplay_contract
    diagnostic_simulation_envelope_m: 0.0666675628
    actual: UNCAPTURED
  - volume_id: throw_lower_landing_01
    geometry_variant_id: geometry_throw_lower_open_01
    launch_position_ws: [1.5, 1.5, 6.0]
    launch_direction_xz: [0.70710678, 0.70710678]
    launch_source_marker_id: burst_gate_pickup_01
    open_volume_bounds: {min: [1.4, 0.0, 5.9], max: [11.4, 3.95, 15.8]}
    landing_surface_id: world_throw_lower_landing_01
    landing_surface_y_m: 0.25
    landing_contact_mode: surface_top_world_solid
    target_marker_id: burst_gate_entry_01
    route_id: gate_route_open_01
    throw_snapshot_id: REQUIRED_RUNTIME_THROW_SNAPSHOT_ID
    throw_snapshot_parity: required_field_for_field_with_controller_capture
    v0_mps: 10.0
    theta_deg: 38.0
    h_release_m: 1.5
    h_landing_m: 0.25
    delta_y_m: 1.25
    projectile_radius_m: 0.05
    epsilon_contact_m: 0.001
    contact_ambiguity_tolerance_m: 0.001
    angle_domain_result: PASS
    discriminant_m2ps2: 62.42890522001662
    t_minus_s: -0.17783707329829032
    t_plus_s: 1.4330082768164518
    selected_t_land_s: 1.4330082768164518
    root_policy: minimum_strictly_positive
    r_actual_m: 11.292259321388022
    range_gate: PASS
    ceiling_margin_m: 0.518098612638639
    lateral_margin_m: 0.4
    collision: landing_surface_contact
    landing_envelope: [11.28, 11.32]
    expected:
      discriminant_m2ps2: 62.42890522001662
      t_land_s: 1.4330082768164518
      r_actual_m: 11.292259321388022
    formula_relative_tol: 0.001
    placement_tol_m: 0.005
    tolerance_class: gameplay_contract
    diagnostic_simulation_envelope_m: 0.0666675628
    actual: UNCAPTURED
```

`REQUIRED_RUNTIME_VALUE` is not a pass value. The authored expected ceiling and
lateral margins above are design inputs; their observed runtime counterparts remain
inside `actual: UNCAPTURED` until capture, and a missing observed margin returns
`MISSING_REQUIRED_FIELD`. Roots with `t ≤ 0` are discarded; a positive repeated
root where `D = 0` is accepted as the single selected root, while a later descending
root is not selected when an earlier strictly positive crossing exists. The
`diagnostic_simulation_envelope_m` is a fixed-step diagnostic bound only; it is not a
replacement for the gameplay `CompareTolerance` gate.

The rev 2026-08-31 pickup re-siting moves both certified launches from
`(3.0, 1.5, 7.5)` to `(1.5, 1.5, 6.0)` (the pickup point plus `h_release_m`). The F4
oracle is launch-position invariant: discriminant, roots, `r_actual`, the range gate,
and `landing_envelope` depend only on `v0`, `theta`, `h_release`, and `h_landing`, so
those authored values are unchanged. The horizontal landing points move to
`(9.65757, 0.0, 14.15757)` (flat) and `(9.65757, 0.25, 14.15757)` (unequal height),
both still inside their referenced slabs with the authored margins intact.

The swept collision contract is exact: each fixed substep casts the projectile
sphere along `p_current → p_next` with `maxDistance=|p_next−p_current|` and
`radius=projectile_radius`. `epsilon_contact` is used only in the post-hit push-out
`hit.point + hit.normal × (projectile_radius + epsilon_contact)`. A zero-length
center segment skips the sweep after the initial-overlap check. A single closest-hit
SphereCast is authoritative; full-buffer and nearest-returned-hit fallbacks are
invalid.

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
    grazing_surface_id: null
    grazing_clearance_m: null
    complete_inputs: {v0_mps: 10.0, theta_deg: 50.0, h_release_m: 1.5, h_landing_m: 0.0, delta_y_m: 1.5, launch_position_ws: [2.0, 1.5, 2.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, pickup_id: burst_gate_pickup_01}
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
  - case_id: authored_grazing_01
    category: authored_geometry_grazing
    geometry_variant_id: geometry_grazing_edge_01
    geometry_validation_result: REJECT
    grazing_surface_id: world_grazing_edge_01
    grazing_clearance_m: 0.0
    complete_inputs: {v0_mps: 10.0, theta_deg: 38.0, h_release_m: 1.5, h_landing_m: 0.0, delta_y_m: 1.5, launch_position_ws: [2.0, 1.5, 2.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, pickup_id: burst_gate_pickup_01}
    expected_rejection_code: AUTHORED_GRAZING_GEOMETRY_REJECTED
    angle_domain_result: PASS
    discriminant_m2ps2: 67.33390522001662
    root_result: PASS
    r_actual_m: 11.536878011794986
    range_gate: PASS
    initial_overlap: false
    no_runtime_launch: true
    no_spend: true
    flight_handle_id: null
    fact_id: null
    source_event_id: null
    burst_state: Carried
    actual: UNCAPTURED
  - case_id: initial_overlap_01
    category: launch_initial_overlap
    geometry_variant_id: geometry_gate_wall_01
    geometry_validation_result: NOT_EVALUATED
    grazing_surface_id: null
    grazing_clearance_m: null
    complete_inputs: {v0_mps: 10.0, theta_deg: 38.0, h_release_m: 1.5, h_landing_m: 0.0, delta_y_m: 1.5, launch_sphere_radius_m: 0.05, launch_position_ws: [4.05, 1.0, 6.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, geometry_variant_id: geometry_gate_wall_01, pickup_id: burst_gate_pickup_01}
    expected_rejection_code: INITIAL_OVERLAP_REJECTED
    angle_domain_result: PASS
    discriminant_m2ps2: 67.33390522001662
    root_result: PASS
    r_actual_m: 11.536878011794986
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
    grazing_surface_id: null
    grazing_clearance_m: null
    complete_inputs: {v0_mps: 10.0, theta_deg: 38.0, h_release_m: 1.5, h_landing_m: 4.0, delta_y_m: -2.5}
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
    category: invalid_f4_content_volume
    geometry_variant_id: geometry_throw_flat_open_01
    geometry_validation_result: NOT_EVALUATED
    grazing_surface_id: null
    grazing_clearance_m: null
    complete_inputs: {v0_mps: 0.0, theta_deg: 38.0, h_release_m: 1.5, h_landing_m: 1.5, delta_y_m: 0.0}
    expected_rejection_code: INVALID_ROOT
    angle_domain_result: PASS
    discriminant_m2ps2: 0.0
    root_result: NO_STRICTLY_POSITIVE_ROOT
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
    grazing_surface_id: null
    grazing_clearance_m: null
    complete_inputs: {v0_mps: 7.0, theta_deg: 38.0, h_release_m: 1.5, h_landing_m: 0.0, delta_y_m: 1.5}
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
outside the room AABB. The previous staging reused `geometry_throw_flat_open_01`,
whose authored landing slab would have won a contact at `x ≈ 10.4` before the timeout
— a contradiction with the `timeout_fallback_then_consumed` terminal contract. The
void variant is a harness-synthetic staging state: the fixture's geometry variants
define exactly the solids listed, and no room floor, slab, wall, or ceiling is
implicitly present in a variant's flight path.

```yaml
failed_gate_spends:
  - case_id: failed_wall_01
    stimulus: {pickup_id: burst_gate_pickup_01, launch_position_ws: [1.5, 1.5, 6.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, geometry_variant_id: geometry_gate_wall_01, config_profile: starter, death_at_s: null, reset_at_s: null}
    terminal_event: collision_landed_then_consumed
    terminal_order: collision_resolved_before_timeout_then_publication
    terminal_timing: {flight_elapsed_s: REQUIRED_RUNTIME_FLIGHT_ELAPSED, contact_fraction: REQUIRED_RUNTIME_CONTACT_FRACTION, contact_event_time: REQUIRED_RUNTIME_CONTACT_EVENT_TIME, timeout_boundary_s: 3.0, comparison_result: contact_before_or_at_timeout}
    pickup_state: Consumed
    spend_committed: true
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    source_event_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    fact_id: REQUIRED_RUNTIME_FACT_ID
    noise_count: 1
    refund: false
    reserve_state: Placed
    reserve_route_id: reserve_route_after_failed_spends_01
    gate_state_after_failed_spend: blocked
    gate_exit_reachable_after_failed_spend: false
    rejection_code: none
    epoch_assertion: {transition: none, attempt_epoch_before: REQUIRED_RUNTIME_EPOCH, attempt_epoch_after: REQUIRED_RUNTIME_EPOCH, increment: 0, stale_prior_epoch_work: not_applicable}
    landing_position: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    expected_landing: {geometry_variant_id: geometry_gate_wall_01, collision: true, contact: REQUIRED_RUNTIME_CONTACT}
    actual: UNCAPTURED
  - case_id: failed_ceiling_01
    stimulus: {pickup_id: burst_gate_pickup_01, launch_feet_position_ws: [1.5, 0.0, 6.0], launch_position_ws: [1.5, 1.5, 6.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, geometry_variant_id: geometry_gate_ceiling_01, config_profile: starter, death_at_s: null, reset_at_s: null}
    terminal_event: collision_landed_then_consumed
    terminal_order: collision_resolved_before_timeout_then_publication
    terminal_timing: {flight_elapsed_s: REQUIRED_RUNTIME_FLIGHT_ELAPSED, contact_fraction: REQUIRED_RUNTIME_CONTACT_FRACTION, contact_event_time: REQUIRED_RUNTIME_CONTACT_EVENT_TIME, timeout_boundary_s: 3.0, comparison_result: contact_before_or_at_timeout}
    pickup_state: Consumed
    spend_committed: true
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    source_event_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    fact_id: REQUIRED_RUNTIME_FACT_ID
    noise_count: 1
    refund: false
    reserve_state: Placed
    reserve_route_id: reserve_route_after_failed_spends_01
    gate_state_after_failed_spend: blocked
    gate_exit_reachable_after_failed_spend: false
    rejection_code: none
    epoch_assertion: {transition: none, attempt_epoch_before: REQUIRED_RUNTIME_EPOCH, attempt_epoch_after: REQUIRED_RUNTIME_EPOCH, increment: 0, stale_prior_epoch_work: not_applicable}
    landing_position: REQUIRED_RUNTIME_PUBLISHED_CONTACT_CENTER
    expected_landing: {geometry_variant_id: geometry_gate_ceiling_01, collision: true, contact: REQUIRED_RUNTIME_CONTACT}
    actual: UNCAPTURED
  - case_id: failed_void_01
    stimulus: {pickup_id: burst_gate_pickup_01, launch_position_ws: [1.5, 1.5, 6.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, geometry_variant_id: geometry_throw_void_open_01, config_profile: starter, death_at_s: null, reset_at_s: null}
    terminal_event: timeout_fallback_then_consumed
    terminal_order: timeout_at_3.0_then_fallback_publication
    terminal_timing: {flight_elapsed_s: 3.0, contact_fraction: null, contact_event_time: null, timeout_boundary_s: 3.0, comparison_result: timeout_wins}
    pickup_state: Consumed
    spend_committed: true
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    source_event_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    fact_id: REQUIRED_RUNTIME_FACT_ID
    noise_count: 1
    refund: false
    landing_position: REQUIRED_RUNTIME_LAST_SIMULATED_SPHERE_CENTER
    reserve_state: Placed
    reserve_route_id: reserve_route_after_failed_spends_01
    gate_state_after_failed_spend: blocked
    gate_exit_reachable_after_failed_spend: false
    rejection_code: none
    epoch_assertion: {transition: none, attempt_epoch_before: REQUIRED_RUNTIME_EPOCH, attempt_epoch_after: REQUIRED_RUNTIME_EPOCH, increment: 0, stale_prior_epoch_work: not_applicable}
    expected_landing: {collision: false, timeout_s: 3.0, fallback: last_simulated_sphere_center}
    actual: UNCAPTURED
  - case_id: failed_death_01
    stimulus: {pickup_id: burst_gate_pickup_01, launch_position_ws: [1.5, 1.5, 6.0], launch_direction_xz: [1.0, 0.0], camera_azimuth_deg: 90.0, geometry_variant_id: geometry_throw_flat_open_01, config_profile: starter, death_at_s: 0.5, reset_at_s: null}
    terminal_event: death_cancelled_inflight
    terminal_order: death_latched_before_collision_or_timeout
    terminal_timing: {flight_elapsed_s: REQUIRED_RUNTIME_FLIGHT_ELAPSED_AT_DEATH, contact_fraction: null, contact_event_time: null, timeout_boundary_s: 3.0, comparison_result: death_wins}
    pickup_state: Consumed
    spend_committed: true
    flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    source_event_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
    fact_id: null
    noise_count: 0
    refund: false
    landing_position: null
    reserve_state: Placed
    reserve_route_id: reserve_route_after_failed_spends_01
    gate_state_after_failed_spend: blocked
    gate_exit_reachable_after_failed_spend: false
    rejection_code: none
    epoch_assertion: {transition: incremented_once, attempt_epoch_before: REQUIRED_RUNTIME_EPOCH, attempt_epoch_after: REQUIRED_RUNTIME_EPOCH, increment: 1, stale_prior_epoch_work: rejected_stale_epoch}
    stale_work_assertion: {flight_callback: ignored, late_collision: none, late_timeout: none, fact_id: null, relay: none, decision: none, audio: cancelled_before_presentation}
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
  source_event_id: REQUIRED_RUNTIME_SOURCE_EVENT_ID
  fact_id: REQUIRED_RUNTIME_FACT_ID
  flight_handle_id: REQUIRED_RUNTIME_FLIGHT_HANDLE_ID
  entry_id: REQUIRED_RUNTIME_ENTRY_ID_OR_NONE
  epoch_transition_id: REQUIRED_RUNTIME_EPOCH_TRANSITION_ID_OR_NONE
  stale_work_assertion: required_for_death_cancelled_flight
  expected_actual_pair: required
  identity_reuse_within_session_epoch: forbidden
```

The placeholders above are capture requirements, not fabricated IDs. Movement source
records use `step_id`; Burst source records use `flight_handle_id`; transport dedup
uses `(session_id, attempt_epoch, fact_id)`; Perception episode identity uses
`entry_id`.

## Stable Fail-Closed Result Codes

The validator may return only a registered code from this set:

- `PASS`
- `MISSING_REQUIRED_FIELD`
- `MISSING_MARKER`
- `INVALID_CARDINALITY`
- `INVALID_TOLERANCE`
- `INVALID_F4_RANGE`
- `INVALID_ANGLE`
- `INVALID_ROOT`
- `INVALID_ROUTE`
- `AMBIGUOUS_COLLISION`
- `INITIAL_OVERLAP_REJECTED`
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
- [ ] The Level-owned gate transition records typed `from_state`/`to_state` values, binds `trigger_noise_kind=Burst` and the chain's `landing_fact_id`, and matches the captured investigate decision's session, epoch, `fact_id`, and `entry_id`.
- [ ] Death during flight increments `attempt_epoch` exactly once, cancels prior-epoch flight callbacks and queued work before presentation, and records no late collision, timeout, fact, relay, or decision; stale work cannot pass certification.
- [ ] Runtime, preview, and fixture use exact center-segment SphereCast distance, one closest hit, contact push-out, zero-length rule, timeout precedence, and no hit-buffer fallback.
- [ ] Runtime records one pre-batch global `SyncTransforms` and does not toggle `autoSyncTransforms` per query.
- [ ] MVP response is single-guard `Investigate`; Target-only peer/zone responses are rejected in this fixture.
- [ ] Every actual field and trace/sample identity is captured; nominal-radius-only or prose-only evidence fails closed.
- [ ] `teaching_sequence` is ordered and proves Walk/Run signal demonstrations before the certified Burst spend; the spend beat originates inside `patrol_pre_spend_01` (settled `Patrol`, no re-anchor or give-up in flight).
- [ ] `signal_a_visibility` records clear vantage-to-guard LOS and a bounded orient-tell window with a captured noise-caused `investigate-commit`.
- [ ] Reserve safety records prove the reserve route remains reachable and outside the guard's extended search after every failed spend.
- [ ] `restart_transaction` proves discoverable full-room restart is atomic: both pickups return to `Placed`, carried slot clears, gate markers reset, and prior-epoch work is discarded.
- [ ] No invalid, fallback, or death-cancelled landing appears in a gate causal chain; otherwise `GATE_OPENED_BY_INVALID_LANDING` is returned.

## Known Open Authoring Items (non-blocking)

These are acknowledged level-authoring gaps for the future Level GDD; none of them
blocks the fixture's schema conformance or the rev 2026-08-31 revision contracts:

- The room envelope is asserted as an AABB contract, but no authored perimeter-wall
  solids exist in any geometry variant; a future Level GDD must author them as
  `World` occluders without moving any fixture endpoint.
- `target_marker_id: burst_gate_entry_01` is logical-objective semantics per the
  registry; no physical landing-target marker exists, and none is required for MVP.
- The two Signal-A vantages are authored assertions with declared clear Linecasts;
  no dedicated safe-vantage geometry (cover, height) is authored for them yet.
- Geometry variant records carry purpose strings instead of an owning-column
  provenance field; the registry field list is authoritative and deliberately does
  not include an owner key.
