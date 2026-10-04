# Story 002: Burst Noise-Maker Distraction Tool

> **Epic**: Player Noise & Sound Propagation (`production/epics/player-noise/EPIC.md`)  
> **Story ID**: `NOISE-02`  
> **Status**: Complete  
> **Layer**: Gameplay / AI Perception Interop  
> **Type**: Logic / Integration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-04  
> **Owner**: `gameplay-programmer`  

## Context

**GDD**: `design/gdd/player-noise.md` (System #3 Player Noise, AC6..AC15b)  
**Governing ADRs**: `ADR-0001` (Event Bus), `ADR-0002` (Physics Query Service)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Constraints**:
- **Lifecycle**: `Placed` -> `Carried` -> `InFlight` -> `Landed` -> `Consumed`.
- **Ballistic Parameters**: Launch angle $\theta = 30^\circ$, release height $h_{\text{release}} = 1.50\text{ m}$, initial launch speed $V_0 = 12.0\text{ m/s}$, gravity $g = 9.81\text{ m/s}^2$.
- **Stationary Launch Constraint**: Can only be thrown while stationary ($V_{\text{planar}} < 1.0\text{ m/s}$). Throws while moving at Walk/Run speed are rejected (`THROW_WHILE_MOVING_REJECTED`) without consuming inventory.
- **Acoustic Impact Pulse**: On contact with ground or geometry, emits a discrete acoustic event with radius $R_{\text{burst}} = 10.0\text{ m}$.
- **Guard Lure**: Unalerted/Patrolling guards within $10.0\text{ m}$ (subject to vertical falloff and Layer 20 World occlusion) transition to `Investigate` targeting the impact point.
- **Single-Carry Inventory**: The player carries at most 1 burst item per run. After throw, state becomes `Consumed`.

---

## Acceptance Criteria

- [x] **AC-NOISE-05 — Ballistic Trajectory & Landing**: The projectile follows a ballistic arc at $30^\circ$ elevation and $12.0\text{ m/s}$ initial speed, correctly detecting collision with geometry or floor.
- [x] **AC-NOISE-06 — Movement Rejection**: Attempting to throw while moving with planar speed $\ge 1.0\text{ m/s}$ is rejected, preserving the carried item.
- [x] **AC-NOISE-07 — Impact Acoustic Pulse ($10.0\text{ m}$)**: On impact, publishes a discrete noise event with $R_{\text{burst}} = 10.0\text{ m}$ at the contact point and transitions item to `Consumed`.
- [x] **AC-NOISE-08 — Guard Lure & Investigation**: Nearby patrolling guards hearing the impact redirect their path to investigate the landing position. Fruitless investigation adds residual wariness ($+0.15$ to $R$).

---

## QA Test Cases

- `test_burst_ballistic_flight_and_landing_position`
- `test_burst_movement_rejection_preserves_carried_state`
- `test_burst_impact_emits_10m_acoustic_radius`
- `test_burst_single_carry_inventory_depletion`
- `test_burst_hearing_redirects_patrol_guard_to_impact`
- `test_burst_fruitless_adds_residual_wariness`

---

## Test Evidence
- Unit Tests: `tests/unit/noise/burst_noise_maker_test.cs`
- Integration Tests: `tests/integration/noise/burst_distraction_lure_test.cs`
