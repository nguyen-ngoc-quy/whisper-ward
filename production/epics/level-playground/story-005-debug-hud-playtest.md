# Story 005: On-Screen Locomotion Debug HUD & Playtest Verification Suite

> **Epic**: Level Playground (`production/epics/level-playground/EPIC.md`)  
> **Story ID**: `SCENE-05`  
> **Status**: Complete  
> **Layer**: UI / Verification  
> **Type**: UI  
> **Estimate**: 3h (0.4d)  
> **Manifest Version**: 2026-09-22  
> **Last Updated**: 2026-09-22  
> **Owner**: `qa-lead`  

## Context

**GDD**: `design/gdd/player-third-person-controller.md`, `design/gdd/camera-cinemachine.md`  
**Dependencies**: `SCENE-02`, `SCENE-03`, `SCENE-04`  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

---

## Acceptance Criteria

- [x] **AC-SCENE-16 — On-Screen Locomotion HUD**: A lightweight `DebugLocomotionHUD.cs` displaying: Current Speed ($V_{\text{actual}}$), Stance (`Stand` vs `Crouched`), Camera Distance ($D_{\text{cam}}$), Stand Headroom Status (`Clear` vs `Blocked`), and Active FOV.
- [x] **AC-SCENE-17 — Documented Playtest Evidence**: Interactive playtest walkthrough documented at `production/qa/evidence/core-playground-playtest-evidence.md` confirming 0 console exceptions across a 5-minute continuous play session.

---

## Implementation Files
- `src/Core/Player/DebugLocomotionHUD.cs`
- `Assets/WhisperWardCore/Runtime/Player/DebugLocomotionHUD.cs`
- `Assets/Editor/CorePlaygroundBuilder.cs`
- `tests/integration/scene/debug_locomotion_hud_integration_test.cs`
- `production/qa/evidence/core-playground-playtest-evidence.md`

---

## Completion Notes
**Completed**: 2026-09-22  
**Criteria**: 2/2 passing (AC-SCENE-16, AC-SCENE-17)  
**Deviations**: None  
**Test Evidence**: Integration test suite at `tests/integration/scene/debug_locomotion_hud_integration_test.cs` (5 tests passing) + Playtest Evidence at `production/qa/evidence/core-playground-playtest-evidence.md`  
**Code Review**: Complete  
