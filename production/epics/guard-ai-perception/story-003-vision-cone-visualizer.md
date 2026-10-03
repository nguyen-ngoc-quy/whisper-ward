# Story 003: Vision Cone 3D Mesh Visualizer & Suspicion Indicator

> **Epic**: Guard AI & Perception (`production/epics/guard-ai-perception/EPIC.md`)  
> **Story ID**: `GUARD-03`  
> **Status**: Complete  
> **Layer**: Visual / UI  
> **Type**: Visual / Feel  
> **Estimate**: 3h (0.4d)  
> **Manifest Version**: 2026-10-03  
> **Owner**: `technical-artist`  

## Context

**GDD**: `design/gdd/perception.md` (R2 Vision, Rendered-cone bias $\Delta\text{FOV} = 5^\circ$, Tuning Knobs)  
**Engine**: Unity 6 LTS (6000.3.17f1) | URP  

**Visual Requirements**:
- 3D Mesh generated dynamically on the XZ ground plane projecting forward from guard eye.
- Segmented arc mesh (30..40 vertices) projected downward to ground, raycasted against Layer 20 `World` so the cone does not project through walls.
- Color grading according to suspicion state:
  - Idle / Patrol ($A = 0$): Soft Moonlit Cyan / Teal (`#38A0B8`, Alpha 0.25)
  - Charging / Confirming ($0 < A < 1.0$): Warm Suspicious Amber / Yellow (`#F0B820`, Alpha 0.35)
  - Chase / Alert ($A \ge 1.0$): Hostile Alert Red (`#E03030`, Alpha 0.45)

---

## Acceptance Criteria

- [x] **AC-GUARD-13 — Dynamic Ground-Projected Vision Mesh**: Generates a smooth horizontal circular sector mesh with radius $12.0\text{ m}$ and arc $100^\circ$. Raycasts against Layer 20 `World` walls clamp edge vertices to surface hits.
- [x] **AC-GUARD-14 — State-Driven Visual Styling**: Cone material color smoothly transitions from Calm Cyan to Warning Amber to Alert Red as accumulator $A$ charges.
