# Story 001: Player Footstep Noise Emitter & Guard Hearing Evaluation

> **Epic**: Player Noise & Sound Propagation (`production/epics/player-noise/EPIC.md`)  
> **Story ID**: `NOISE-01`  
> **Status**: Complete  
> **Layer**: Gameplay / AI  
> **Type**: Logic / Integration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-03  
> **Owner**: `gameplay-programmer`  

## Context

**GDD**: `design/gdd/player-noise.md` (R1..R6 Footstep noise), `design/gdd/perception.md` (R5 Hearing, F12 Formula)  
**Governing ADRs**: `ADR-0001` (Event Bus), `ADR-0002` (Physics Query Service)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Footstep Noise Radii & Rules**:
- Crouch ($V \le 1.8\text{ m/s}$): $R_{crouch} = 0.0\text{ m}$ (Completely silent).
- Walk ($V \in (1.8, 3.5]\text{ m/s}$): $R_{walk} = 4.0\text{ m}$.
- Run ($V > 3.5\text{ m/s}$): $R_{run} = 6.0\text{ m}$.
- Step cadence: Emitted on movement stride rhythm ($\approx 0.45\text{ s}$ walk, $\approx 0.30\text{ s}$ run) when moving.
- Hearing check: Planar horizontal distance $d_{noise} \le R_{eff}$ and clear Linecast between noise origin ($Y = \text{feet} + 0.25\text{ m}$) and Guard eye ($Y = \text{feet} + 1.60\text{ m}$) against Layer 20 `World`.
- Guard reaction: Unalerted patrolling guard hearing noise transitions to `Investigate`, turning and moving to the noise origin.

---

## Acceptance Criteria

- [x] **AC-NOISE-01 — Stance-Aware Footstep Radii**: Player emits noise events matching velocity and stance: Crouch = $0\text{ m}$, Walk = $4.0\text{ m}$, Run = $6.0\text{ m}$. Stationary player emits $0\text{ m}$.
- [x] **AC-NOISE-02 — Planar Hearing Distance Evaluation**: Guard within planar radius ($d_{XZ} \le R$) detects sound if Linecast is unoccluded. Guard outside radius does not hear.
- [x] **AC-NOISE-03 — Solid Wall Sound Occlusion**: Solid wall (Layer 20 `World`) between player and guard blocks sound completely (`hasLinecastClear == false`).
- [x] **AC-NOISE-04 — Guard Hearing Investigation Trigger**: When a patrolling guard hears a footstep noise event, FSM triggers transition to `Investigate` with target set to the noise origin position.
