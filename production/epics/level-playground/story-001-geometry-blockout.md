# Story 001: 3D Arena Geometry Blockout & Moonlit Lighting Setup

> **Epic**: Level Playground (`production/epics/level-playground/EPIC.md`)  
> **Story ID**: `SCENE-01`  
> **Status**: Complete
> **Layer**: Integration (Scene Blockout)  
> **Type**: Integration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-09-22  
> **Last Updated**: 2026-09-22  
> **Owner**: `level-designer`  

## Context

**GDD**: `design/gdd/systems-index.md` (System #8 Level/Content prototyping, Retrospective ACT-01)  
**Governing ADRs**: `ADR-0006`, `ADR-0007`, `ADR-0008`  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Control Manifest Rules**:
- Layer Mask: All static walls, floors, obstacles, and overhangs must be assigned to Layer 20 (`World` / `Solid`).
- Corridor Clearance: Corridors must strictly adhere to $W_{\text{corridor}} \ge 1.50\text{ m}$ for main routes and $\ge 1.20\text{ m}$ for secondary paths (`ADR-0007`).
- HideSpot Overhang: The test alcove must have ceiling headroom $H_{\text{alcove}} = 1.40\text{ m}$ ($< 1.80\text{ m}$ stand height) to test stand headroom query rejection (`ADR-0006`).
- Lighting: URP Moonlit Directional Light with soft shadows and night ambient tint.

---

## Acceptance Criteria

- [x] **AC-SCENE-01 — Arena Floor & Boundary Enclosure**: Main arena floor dimensions are $20.0\text{ m} \times 20.0\text{ m}$ centered at $(0, 0, 0)$ with perimeter walls of height $3.0\text{ m}$ assigned to Layer 20 (`World`).
- [x] **AC-SCENE-02 — Standardized Spatial Corridors**: Includes a main corridor of clear width $W = 1.50\text{ m}$ and a connecting branch corridor of clear width $W = 1.20\text{ m}$, satisfying NavMesh certification rules (`AC-NAV-08`).
- [x] **AC-SCENE-03 — Low-Headroom HideSpot Alcove**: A recessed alcove ($2.0\text{ m} \text{ depth} \times 1.5\text{ m} \text{ width}$) with ceiling slab positioned at $Y = 1.40\text{ m}$ above the floor, providing an exact test fixture for `QueryStandClearance` and HideSpot viewport transition.
- [x] **AC-SCENE-04 — Moonlit URP Lighting Rig**: Directional Light with moonlit cyan/blue tint (Color `#A0C4E2`, intensity 0.8), shadow bias tuned to prevent shadow acne, and soft ambient reflection.

---

## Implementation Notes

Create or configure `Assets/Scenes/CorePlayground.unity` (or an automated editor generator / scene file) containing:
1. `Environment` root GameObject (Layer 20):
   - `Floor` (BoxCollider, $20\text{ m} \times 0.2\text{ m} \times 20\text{ m}$)
   - `PerimeterWalls` (North, South, East, West)
   - `CorridorWalls` (Spacing $1.50\text{ m}$ and $1.20\text{ m}$)
   - `HideSpotAlcove` with low ceiling block ($Y=1.40\text{ m}$)
2. `Lighting` root GameObject:
   - `Directional Light (Moon)`
   - `Global Volume` (URP Default Profile)

---

## Completion Notes
**Completed**: 2026-09-22  
**Criteria**: 4/4 passing  
**Deviations**: None  
**Test Evidence**: Integration test suite at `tests/integration/scene/core_playground_geometry_test.cs` (6 tests passing); QA evidence at `production/qa/evidence/scene-01-blockout.md`.  
**Editor Builder**: Automated scene constructor available via Unity menu `Whisper Ward > Build CorePlayground Scene` (`Assets/Editor/CorePlaygroundBuilder.cs`).  
**Code Review**: Complete (lean mode)  
