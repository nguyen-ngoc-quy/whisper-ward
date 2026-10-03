# Story 002: Player Capsule Prefab & Runtime Input Driver Integration

> **Epic**: Level Playground (`production/epics/level-playground/EPIC.md`)  
> **Story ID**: `SCENE-02`  
> **Status**: Complete  
> **Layer**: Integration (Gameplay Setup)  
> **Type**: Integration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-09-22  
> **Last Updated**: 2026-09-22  
> **Owner**: `unity-specialist`  

## Context

**GDD**: `design/gdd/player-third-person-controller.md`  
**Governing ADRs**: `ADR-0005`, `ADR-0006`, `ADR-0008`  
**Dependencies**: `SCENE-01`  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

---

## Acceptance Criteria

- [x] **AC-SCENE-05 — Player Capsule Prefab Setup**: Prefab `Player_Capsule.prefab` configured with `CharacterController` (Radius 0.30m, Stand Height 1.80m, SkinWidth 0.03m), `PlayerThirdPersonController`, and a visible visual capsule mesh.
- [x] **AC-SCENE-06 — Runtime Input Hookup (`PlayerRuntimeDriver`)**: Component `PlayerRuntimeDriver.cs` reads `Move`, `Sprint`, and `Crouch` actions from Unity Input System (or `InputSystemService`) and passes `PlayerInputPacket` to `PlayerThirdPersonController.Tick(input, Time.deltaTime)`.
- [x] **AC-SCENE-07 — Interactive Stance Toggling**: Pressing Crouch key (`C` / `Ctrl`) toggles between Stand ($1.80\text{ m}$) and Crouch ($1.20\text{ m}$). Entering the alcove and attempting to uncrouch is blocked by `QueryStandClearance`.
- [x] **AC-SCENE-08 — Directional Gizmo & Facing Feedback**: Editor Gizmos draw current planar velocity vector and character forward facing vector in the Scene view for visual debugging.

---

## Implementation Files
- `src/Core/Player/PlayerRuntimeDriver.cs`
- `Assets/WhisperWardCore/Runtime/Player/PlayerRuntimeDriver.cs`
- `Assets/Editor/CorePlaygroundBuilder.cs` (`BuildPlayerCapsule`)
- `Assets/Prefabs/Player_Capsule.prefab`

## Test Evidence
- Integration Test Suite: `tests/integration/scene/player_runtime_driver_integration_test.cs` (5 tests passing)
- QA Sign-Off Evidence: `production/qa/evidence/scene-02-player-driver.md`

---

## Completion Notes
**Completed**: 2026-09-22  
**Criteria**: 4/4 passing  
**Deviations**: None  
**Test Evidence**: `tests/integration/scene/player_runtime_driver_integration_test.cs`  
**Code Review**: Complete  
