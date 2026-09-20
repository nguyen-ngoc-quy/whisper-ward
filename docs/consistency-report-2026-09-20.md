# Consistency Check Report

Date: 2026-09-20

## Scope

Registry entries checked: 13 active formula definitions, 81 scalar/config constants, plus registered noise, ballistics, physics, HideSpot, suspicion/grade, lifecycle, event, and fixture contracts.

GDDs scanned:
- `design/gdd/game-concept.md`
- `design/gdd/perception.md`
- `design/gdd/guard-ai-fsm.md`
- `design/gdd/player-noise.md`
- `design/gdd/player-movement-hide.md`
- `design/gdd/player-third-person-controller.md`
- `design/gdd/sound_performance_audit.md`
- `design/gdd/suspicion-meter-grade.md`
- `design/gdd/navmesh-pathfinding.md`
- `design/gdd/physics-collision-config.md`
- `design/fixtures/noise-fixture-spec.md`
- `design/levels/mvp-burst-route-fixture.md`

## Audit Focus Areas

1. **Re-Anchor Budget Formula (`reanchor_giveup_timeout`)**:
   - `entities.yaml`, `player-noise.md` (§F2), and `guard-ai-fsm.md` (§D1) are unified on the monotonic formulation:
     $$t_{reanchor\_floor} = t_{giveup\_base} \times s_{diff} \times \left(1 + k_{thorough} \times \frac{R_{reanchor}}{R_{max}}\right) + t_{noise\_reanchor\_extend}$$
     $$t_{reanchor} = \min(t_{investigate\_max}, \max(t_{remaining}, t_{reanchor\_floor}))$$
   - `s_diff` is consistently consumed as the registered difficulty scalar ($1.0\text{ [0.7, 1.6]}$).
   - `S_DIFF` speed-ratio is confirmed STRUCK with tombstone documentation across all active files.

2. **Burst Ballistics & Release Geometry**:
   - `throw_release_height`: $1.5\text{ m [1.2, 1.8] m}$ locked standing upright release across registry, GDD, and Level fixture.
   - Crouch Skip is confirmed pruned from all canonical specifications.
   - Overhead clearance check: $1.8\text{ m}$ standing height clearance required; failure rejects before spend as `BURST_STAND_CLEARANCE_BLOCKED` (`burst-stand-clearance-blocked`).
   - Input buffer and velocity snap: `input_buffer_window_s` ($0.15\text{ s}$) and `throw_velocity_snap_threshold` ($1.0\text{ m/s}$) are registered and aligned across `entities.yaml` and `player-noise.md`.

3. **Re-Anchor Acoustic Centroid Scatter**:
   - `r_corroborate_scatter_movement` ($0.0\text{ m}$) and `r_corroborate_scatter_burst` ($0.0\text{ m}$) are locked to exact acoustic centroid in `entities.yaml`, `player-noise.md`, and `guard-ai-fsm.md`.

4. **Ceiling Limit & Max Investigate Timeout**:
   - `t_investigate_max`: $10.0\text{ s [8.0, 15.0] s}$ ceiling registered in `entities.yaml` and consumed in `player-noise.md` and `guard-ai-fsm.md`.

5. **Diagnostic Mappings & Rejection Codes**:
   - Rejection codes `BURST_STAND_CLEARANCE_BLOCKED`, `THROW_WHILE_MOVING_REJECTED`, `INITIAL_OVERLAP_REJECTED`, and `INITIAL_OVERLAP_QUERY_INCOMPLETE` in `src/AI/Core/NoiseRejectionCodes.cs` are completely mapped in `design/registry/entities.yaml` `player_noise_diagnostics`.

6. **Player Movement & Hide Post-Approval Synchronization (2026-09-20)**:
   - Synchronized `guard-ai-fsm.md §C1.4` Backstop Linecast endpoint to `Physics.Linecast(guard_eye, aperture_portal_target)` per canonical `player-movement-hide.md §D1`.
   - Added formulas D1 (`hidespot_catch_reach_resolvable`), D2 (`hidespot_vertical_offset_authoring_margin`), and D4 (`through_spot_escape_time_invariant`) to `design/registry/entities.yaml`.
   - Registered tuning constants `standoff_margin` ($0.10\text{ m}$), `t_margin_react` ($0.3\text{ s}$), `t_spotfront_verify_through` ($2.5\text{ s}$) and vector `aperture_portal_target` to `design/registry/entities.yaml`.
   - Updated `auth_margin` note with load-time joint constraint: $\text{delta\_y\_tolerance} - \text{auth\_margin} \ge 0.3\text{ m}$.

7. **Suspicion Meter / Grade Operator Synchronization (2026-09-20)**:
   - Verified that all scoring constants (`grade_weight_chase: 25`, `grade_weight_fruitless: 0` per CD clean-distraction ruling, `grade_weight_capture: 50`, `grade_weight_residual: 10`, `grade_penalty_cap: 90`, `grade_threshold_s: 90`, `grade_threshold_a: 75`, `grade_threshold_b: 60`, `meter_display_scale: 100`) align exactly across `entities.yaml`, ADR-0004, and `design/gdd/suspicion-meter-grade.md`.
   - Added formula definitions D1 (`dominant_threat_selection_ratio`) and D6/D7 (`room_grade_quality_score`) to `design/registry/entities.yaml`.
   - Updated bidirectional `consumed_by` references for `T_chase` and `k_res` to include `Suspicion/Grade GDD`.
   - Verified F15 forgiveness floor chain ($0.0 \le \text{forgiveness\_floor} < T_{\text{floor}}$) holds with positive margin ($0.030 \le 0.045$) across Perception GDD, registry, and Suspicion/Grade GDD.

8. **NavMesh / Pathfinding Synchronization (2026-09-20)**:
   - Verified that all NavMesh / Pathfinding formulas (D1 `cumulative_path_length`, D2 `catch_gate_state`, D3 `arrival_precedence`, D4 `chase_repath_trigger`, D5 `navmesh_bake_voxel_invariant`, D6 `corridor_width_invariant`) are registered in `design/registry/entities.yaml`.
   - Registered tuning constants (`t_repath_min = 0.10 s`, `T_repath = 0.25 s`, `Delta_p_trigger = 0.50 m`, `omega_turn = 480.0 deg/s`, `a_max = 16.0 m/s^2`, `v_size = 0.10 m`, `margin_corridor = 0.40 m`, `r_avoid = 0.45 m`) in `design/registry/entities.yaml`.
   - Verified cross-system locks: `r_guard = 0.40 m`, `stopping_distance = 0.00 m`, `navmesh_sample_maxdistance = 0.40 m`, `eps_arrive = 0.30 m`, `catch_range = 5.50 m` (floor $5.20\text{ m}$), `hyst = 0.50 m`, `V_patrol = 2.30 m/s`, `V_investigate = 5.00 m/s`, `V_chase = 7.50 m/s`.
   - Verified bidirectional contracts: Guard AI FSM `#1`, Perception `#2`, Player Movement & Hide `#5`, Suspicion Meter & Grade `#7`, Physics `#18` (ADR-0002).
   - Confirmed thin-wall linecast verification on `SamplePosition` eliminates wall-teleport snapping; confirmed `Arrived` strictly takes precedence over `PathEnd` in D3.

9. **Physics & Collision Config Synchronization (2026-09-20)**:
   - Verified that all Physics formulas (D1 `contact_surface_pushout`, D2 `kinematic_step_trajectory`, D3 `subtick_root_selection`, D4 `launch_initial_overlap_check`, D5 `collision_ambiguity_invariant`, D6 `min_wall_thickness_invariant`) are registered in `design/registry/entities.yaml`.
   - Registered tuning constants (`min_wall_thickness = 0.10 m`, `sensing_sync_max_frequency = 5.0 Hz`) in `design/registry/entities.yaml`.
   - Verified cross-system locks: `E20_layer_manifest` (`World` required; `Player`, `Guard`, `trigger` strictly forbidden), `HideSpotContainmentProfile` (`QueryTriggerInteraction.Collide` dedicated mask), `QueryTriggerInteraction.Ignore` universal for sensing/occlusion, `queriesHitBackfaces = false`, `autoSyncTransforms = false` global lifetime authority, `projectile_radius = 0.05 m`, `epsilon_contact = 0.001 m`, `contact_ambiguity_tolerance = 0.001 m`, `initial_overlap_result_capacity = 64`, `dt_max = 0.0083333333 s` ($120\text{ Hz}$), `epsilon_t = 0.0083333333 s`.
   - Confirmed demand-driven batch transform sync contract (1 call per Burst flight batch, 1 call per AI sensing tick) resolves historical unity-specialist F2 advisory without global setting toggle churn.
   - Zero conflicts detected across all 9 canonical GDDs and ADR-0002.

## Conflicts Found

- **Resolved**: `guard-ai-fsm.md §C1.4` line 161 Backstop Linecast endpoint updated from `proxy(interior_position)` to `aperture_portal_target`. Logged in `docs/consistency-failures.md`.
- Zero conflicts found in NavMesh / Pathfinding audit.
- Zero conflicts found in Physics & Collision Config audit.

## Stale Registry Entries

- **Resolved**: Added missing HideSpot constants (`standoff_margin`, `t_margin_react`, `t_spotfront_verify_through`), authored vector `aperture_portal_target`, and formulas D1/D2/D4 to `design/registry/entities.yaml`.
- **Resolved**: Added NavMesh formulas D1–D6 and constants `t_repath_min`, `T_repath`, `Delta_p_trigger`, `omega_turn`, `a_max`, `v_size`, `margin_corridor`, `r_avoid` to `design/registry/entities.yaml`.
- **Resolved**: Added Physics formulas D1–D6 and constants `min_wall_thickness`, `sensing_sync_max_frequency` to `design/registry/entities.yaml`.

## Verdict

**PASS** — All canonical GDDs, fixture specifications, and registry entries are mutually consistent with zero remaining conflicts. All detected sync items resolved.
