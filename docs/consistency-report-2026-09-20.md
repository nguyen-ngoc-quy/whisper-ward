# Consistency Check Report

Date: 2026-09-20

## Scope

Registry entries checked: 10 formula definitions, 81 scalar/config constants, plus registered noise, ballistics, physics, HideSpot, lifecycle, event, and fixture contracts.

GDDs scanned:
- `design/gdd/game-concept.md`
- `design/gdd/perception.md`
- `design/gdd/guard-ai-fsm.md`
- `design/gdd/player-noise.md`
- `design/gdd/player-movement-hide.md`
- `design/gdd/player-third-person-controller.md`
- `design/gdd/sound_performance_audit.md`
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

## Conflicts Found

None. All cross-file formulas, constants, safe ranges, and rejection diagnostics are in 100% lock-step parity.

## Stale Registry Entries

None confirmed.

## Verdict

**PASS** — All canonical GDDs, fixture specifications, and registry entries are mutually consistent with zero detected conflicts.
