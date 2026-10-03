# Story 006: URP Moonlit Stealth Post-Processing & Distinct Materials

> **Epic**: Level Playground (`production/epics/level-playground/EPIC.md`)  
> **Story ID**: `SCENE-06`  
> **Status**: Complete  
> **Layer**: Visual / Atmosphere  
> **Type**: Visual/Feel  
> **Estimate**: 2h (0.3d)  
> **Manifest Version**: 2026-09-22  
> **Last Updated**: 2026-09-22  
> **Owner**: `technical-artist`  

## Context

**GDD**: `design/gdd/game-concept.md`  
**Dependencies**: `SCENE-01`  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

---

## Acceptance Criteria

- [x] **AC-SCENE-18 — High Contrast Materials**: Distinct URP Lit materials for Floor (Slate dark `#1C222B`), Obstacle Walls (Concrete grey `#38404B`), Player (Teal `#2AC3A2`), and Guard (Hostile Red/Amber `#D9383A`).
- [x] **AC-SCENE-19 — Moonlit Post-Processing Volume**: Volume profile with subtle Vignette ($0.25$), ACES Tonemapping, and Moonlit color grading establishing the stealth mood.

---

## Implementation Files
- `Assets/Editor/CorePlaygroundBuilder.cs`
- `Assets/Materials/Mat_Floor_Slate.mat`
- `Assets/Materials/Mat_Wall_Concrete.mat`
- `Assets/Materials/Mat_Player_Teal.mat`
- `Assets/Materials/Mat_Guard_HostileRed.mat`
- `Assets/Settings/PlaygroundVolumeProfile.asset`
- `production/qa/evidence/scene-06-materials-post-processing.md`

---

## Completion Notes
**Completed**: 2026-09-22  
**Criteria**: 2/2 passing (AC-SCENE-18, AC-SCENE-19)  
**Deviations**: None  
**Test Evidence**: QA Evidence file at `production/qa/evidence/scene-06-materials-post-processing.md`  
**Code Review**: Complete  
