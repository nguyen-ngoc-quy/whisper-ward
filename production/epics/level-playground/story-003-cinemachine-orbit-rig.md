# Story 003: Cinemachine Camera Rig & Orbit Follow Integration

> **Epic**: Level Playground (`production/epics/level-playground/EPIC.md`)  
> **Story ID**: `SCENE-03`  
> **Status**: Complete  
> **Layer**: Integration (Camera System)  
> **Type**: Integration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-09-22  
> **Last Updated**: 2026-09-22  
> **Owner**: `unity-specialist`  

## Context

**GDD**: `design/gdd/camera-cinemachine.md`  
**Governing ADRs**: `ADR-0008`  
**Dependencies**: `SCENE-02`  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

---

## Acceptance Criteria

- [x] **AC-SCENE-09 — Package & Rig Architecture**: Package `com.unity.cinemachine` declared in `Packages/manifest.json`. Scene contains Main Camera with `CinemachineBrain` executing strictly in `LateUpdate`.
- [x] **AC-SCENE-10 — Runtime Orbit Driver (`CameraOrbitDriver`)**: MonoBehaviour component `CameraOrbitDriver.cs` wraps `CameraRigService.cs`, consuming mouse delta and orienting the camera follow target.
- [x] **AC-SCENE-11 — Real-Time Spherecast Deocclusion**: Camera dynamically pushes in upon nearing arena walls/pillars ($R_{\text{cast}} = 0.20\text{ m}, D_{\text{min}} = 0.40\text{ m}$) against Layer 20, and pulls out with exponential recovery ($\tau = 0.25\text{ s}$).
- [x] **AC-SCENE-12 — Dynamic FOV & HideSpot Triggering**: FOV expands smoothly from $60^\circ$ to $68^\circ$ when sprinting. Trigger volume at HideSpot alcove calls `SwitchToHideSpotView` and clamps look cone to $\pm 30^\circ$.

---

## Implementation Files
- `src/Core/Camera/CameraOrbitDriver.cs`
- `src/Core/Camera/HideSpotTriggerZone.cs`
- `Assets/WhisperWardCore/Runtime/Camera/CameraOrbitDriver.cs`
- `Assets/WhisperWardCore/Runtime/Camera/HideSpotTriggerZone.cs`
- `Assets/Editor/CorePlaygroundBuilder.cs`
- `Packages/manifest.json`

## Test Evidence
- Integration Test Suite: `tests/integration/scene/camera_orbit_driver_integration_test.cs` (6 tests passing)
- QA Sign-Off Evidence: `production/qa/evidence/scene-03-camera-rig.md`

---

## Completion Notes
**Completed**: 2026-09-22  
**Criteria**: 4/4 passing  
**Deviations**: None  
**Test Evidence**: `tests/integration/scene/camera_orbit_driver_integration_test.cs`  
**Code Review**: Complete  
