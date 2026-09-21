# Consistency Check Report

Date: 2026-09-21

## Scope

Registry entries checked: 47 active formula definitions, 272 constants, contracts, and diagnostic mappings (319 total registered items) across 16 sections in `design/registry/entities.yaml`.

GDDs scanned:
1. `design/gdd/guard-ai-fsm.md` (System #1 — MVP)
2. `design/gdd/perception.md` (System #2 — MVP)
3. `design/gdd/player-noise.md` (System #3 — MVP)
4. `design/gdd/player-movement-hide.md` (System #5 — Target)
5. `design/gdd/suspicion-meter-grade.md` (System #7 — MVP)
6. `design/gdd/player-third-person-controller.md` (System #11 — MVP)
7. `design/gdd/navmesh-pathfinding.md` (System #12 — MVP)
8. `design/gdd/audio-ui-feedback.md` (System #13 — MVP)
9. `design/gdd/event-messaging-bus.md` (System #15 — MVP)
10. `design/gdd/physics-collision-config.md` (System #18 — MVP)
11. `design/gdd/input-system.md` (System #19 — MVP)
12. `design/gdd/camera-cinemachine.md` (System #20 — MVP)
13. `design/gdd/suspicion-attribution-telemetry.md` (System #10 — MVP)

Also verified against:
- `design/gdd/game-concept.md` (Meta / Claims 1–3)
- `design/levels/mvp-burst-route-fixture.md` (Route contract)
- `design/fixtures/noise-fixture-spec.md` (Fixture spec)

---

## Audit Focus Areas & Verified Invariants

1. **System #10 (Suspicion Attribution Telemetry) Alignment**:
   - Reconciled knob naming in `entities.yaml` to match canonical Section G in `suspicion-attribution-telemetry.md`:
     - `telemetry_ring_buffer_capacity`: $2048$ entries ($[1024, 8192]$)
     - `telemetry_record_byte_size_max`: $128$ bytes ($[64, 256]$)
     - `telemetry_burst_reach_distance`: $1.20\text{ m}$ ($[0.80, 2.00]\text{ m}$)
     - `telemetry_claim1_pass_threshold`: $0.80$ ($[0.70, 0.90]$)
     - `telemetry_claim2_pass_threshold`: $0.60$ ($[0.50, 0.80]$)
     - `telemetry_claim3_pass_threshold`: $0.60$ ($[0.50, 0.80]$)
     - `telemetry_claim2_residual_epsilon`: $0.05$ ($[0.01, 0.10]$)
     - `telemetry_min_tester_sample_size`: $5$ ($[5, 20]$)
     - `telemetry_flush_async_timeout_ms`: $500\text{ ms}$ ($[100, 2000]\text{ ms}$)
     - `telemetry_trivial_duration_threshold_s`: $1.00\text{ s}$ ($[0.50, 2.00]\text{ s}$)
     - `telemetry_webgl_storage_key`: `"ww_fsm_trace_latest"`
     - `telemetry_debug_overlay`: `false`
     - `telemetry_flush_hotkey`: `"F12"`
   - Verified that formulas D1 (`ring_buffer_valid_count`), D2 (`claim1_forensic_legibility_score`), D3 (`claim2_dual_sub_bar_comprehension_score`), and D4 (`claim3_unprompted_tool_adoption_score`) match the mathematical specifications in Section D.

2. **System #20 (Camera Cinemachine Rig) Alignment**:
   - Verified that all 17 configuration constants (`camera_nominal_distance`, `camera_min_distance`, `camera_shoulder_offset_x`, `camera_target_height_y`, `camera_spherecast_radius`, `camera_distance_recover_tau`, `camera_vertical_leash_threshold`, `camera_dither_fade_start`, `camera_base_fov`, `camera_chase_fov`, `camera_fov_expand_tau`, `camera_fov_contract_tau`, `camera_hidespot_blend_time`, `camera_pitch_min`, `camera_pitch_max`, `camera_max_angular_velocity`, `camera_mouse_sensitivity_default`) match `camera-cinemachine.md` Section G 100%.
   - Verified formulas D1 (`camera_relative_basis_transform`), D2 (`spherecast_deocclusion_distance`), D3 (`asymmetric_distance_damping`), and D4 (`dynamic_chase_fov_scaling`).

3. **System #13 (Audio & UI Feedback) Alignment**:
   - 5 tuning constants (`voice_pool_max_capacity: 24`, `rustle_stride_threshold: 0.70`, `hidespot_lpf_cutoff_hz: 800.0`, `occlusion_lpf_cutoff_hz: 1200.0`, `lpf_cutoff_smoothing_tau_s: 0.15`) verified 100% matched.
   - Formulas D1 (`dual_voice_footstep_gain`), D2 (`dsp_onset_verification_predicate`), D3 (`spatial_acoustics_occlusion_attenuation`), and D4 (`suspicion_drone_pitch_volume_modulation`) verified.

4. **System #15 (Event / Messaging Bus) Alignment**:
   - 4 tuning constants (`max_active_subscribers_per_event: 32`, `ingress_dedup_capacity: 4096`, `retroactive_timestamp_tolerance_s: 0.001`, `phase_drain_budget_warning_ms: 5.0`) verified 100% matched.
   - Formulas D1 (`ingress_admission_predicate`), D2 (`deterministic_ordering_tuple`), D3 (`perception_handoff_resolution`), and D4 (`epoch_barrier_invalidation_predicate`) verified.

5. **System #19 (Input System) Alignment**:
   - 10 tuning constants (`analog_deadzone_inner`, `analog_deadzone_outer`, `look_response_exponent`, `gamepad_look_speed_deg_per_s`, `mouse_sensitivity_multiplier`, `uncrouch_clearance_height`, `uncrouch_sphere_radius`, `hidespot_exit_threshold`, `hidespot_exit_sustain_time`, `throw_cooldown_s`) verified 100% matched.
   - Formulas D1–D4 verified.

6. **System #18 (Physics & Collision Config) Alignment**:
   - Invariants `min_wall_thickness: 0.10 m` and `sensing_sync_max_frequency: 5.0 Hz` verified.
   - Formulas D1–D6 verified.

7. **System #12 (NavMesh / Pathfinding) Alignment**:
   - Formulas D1–D6 verified with zero conflicts against `physics-collision-config.md` and `perception.md`.

8. **AI Core & Sensing Alignment (Systems #1, #2, #3, #5, #7, #11)**:
   - Monotonic re-anchor budget formula `t_reanchor = min(t_investigate_max, max(t_remaining, t_reanchor_floor))` verified identical across `entities.yaml`, `player-noise.md`, and `guard-ai-fsm.md`.
   - Radii ratios `R_walk = 4.0 m`, `R_run = 6.0 m`, `R_burst = 10.5 m` lock-step verified.
   - Speeds `V_crouch = 1.8 m/s`, `V_walk = 3.6 m/s`, `V_run = 6.25 m/s`, `V_patrol = 2.0 m/s`, `V_investigate = 4.5 m/s`, `V_chase = 7.5 m/s` verified.
   - Tombstone verification: `S_DIFF` / `reanchor_speed_ratio` remains completely struck with 0 active references.

---

### Conflicts Found (must resolve before architecture)

None.

---

### Stale Registry Entries (registry behind the GDD)

None. Stale entries in `suspicion_attribution_telemetry` were reconciled directly to match canonical GDD Section G.

---

### Unverifiable References (no conflict, informational)

None.

---

### Clean Entries (no issues found)

✅ 319 registry entries verified across all 13 GDDs with 0 conflicts.

---

Verdict: PASS
