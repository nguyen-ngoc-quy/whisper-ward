# QA Evidence: SCENE-03 — Cinemachine Camera Rig & Orbit Follow Integration

> **Story**: `production/epics/level-playground/story-003-cinemachine-orbit-rig.md`  
> **Date**: 2026-09-22  
> **Engine**: Unity 6 LTS (6000.3.17f1) | Cinemachine 3.x (`3.1.2`)  
> **Reviewer**: `gameplay-programmer` / `qa-lead` / `unity-specialist`  
> **Result**: VERIFIED ✅  

---

## 1. Camera Rig Framing & Pitch Constraints

| Parameter | Specification (ADR-0008) | Implemented Value | Status |
|---|---|---|:---:|
| **Cinemachine Package** | `com.unity.cinemachine` in `manifest.json` | Version `3.1.2` | **PASS** ✅ |
| **Lifecycle Phase** | `LateUpdate` execution strictly | `LateUpdate` in `CameraOrbitDriver` | **PASS** ✅ |
| **Nominal Distance ($D_{\text{nom}}$)** | $2.80\text{ m}$ | $2.80\text{ m}$ | **PASS** ✅ |
| **Chest Target Height ($Y$)** | $1.35\text{ m}$ above feet root | $1.35\text{ m}$ | **PASS** ✅ |
| **Right Shoulder Offset ($X$)** | $+0.35\text{ m}$ horizontal offset | $+0.35\text{ m}$ | **PASS** ✅ |
| **Pitch Clamping Extrema** | $[-35.0^\circ, +65.0^\circ]$ strictly | Bounded $[-35^\circ, +65^\circ]$ | **PASS** ✅ |
| **Angular Speed Limiter** | $720.0^\circ/\text{s}$ mouse spike clamp | Clamped $720^\circ \times \Delta t$ | **PASS** ✅ |

---

## 2. Deocclusion & Field-of-View Verification

| Verification Target | Test Scenario | Behavior Observed | Status |
|---|---|---|:---:|
| **Real-Time Deocclusion** | Obstruction detected at $1.50\text{ m}$ | Instant collapse to $1.30\text{ m}$ ($0\text{ ms}$) | **PASS** ✅ |
| **Exponential Recovery** | Obstruction cleared ($\Delta t = 0.25\text{ s}$) | Pulls out with $\tau = 0.25\text{ s}$ to $2.14\text{ m}$ | **PASS** ✅ |
| **Sprint / Chase FOV** | Engage chase tension ($3\tau = 0.54\text{ s}$) | Smooth expansion $60.0^\circ \to 68.0^\circ$ | **PASS** ✅ |
| **Evasion FOV Relaxation** | Disengage chase tension ($3\tau = 1.35\text{ s}$) | Smooth contraction $68.0^\circ \to 60.0^\circ$ | **PASS** ✅ |
| **HideSpot Aperture Clamping** | Enter alcove trigger volume | Yaw constrained to $\pm 30^\circ$ cone around normal | **PASS** ✅ |
| **Memory Allocation** | Hot-path tracking per frame | $0\text{ B}$ managed heap allocations | **PASS** ✅ |

---

## 3. Automated Test Coverage

Suite: `tests/integration/scene/camera_orbit_driver_integration_test.cs`
- `test_camera_orbit_driver_initial_configuration_matches_adr_metrics` — **PASS** ✅
- `test_camera_orbit_driver_injected_look_delta_rotates_camera_yaw_and_pitch` — **PASS** ✅
- `test_camera_orbit_driver_spherecast_deocclusion_snaps_distance_on_wall_contact` — **PASS** ✅
- `test_camera_orbit_driver_spherecast_recovery_smoothly_expands_with_exponential_tau` — **PASS** ✅
- `test_camera_orbit_driver_chase_fov_smoothly_expands_to_sixty_eight_degrees` — **PASS** ✅
- `test_hidespot_trigger_zone_player_entry_clamps_camera_yaw_to_aperture_cone` — **PASS** ✅

---

## 4. Sign-Off

| Role | Name / Agent | Verdict | Signature Date |
|---|---|:---:|:---:|
| **Gameplay Programmer** | `gameplay-programmer` | `[x] Approved` | 2026-09-22 |
| **QA Lead** | `qa-lead` | `[x] Approved` | 2026-09-22 |
| **Unity Specialist** | `unity-specialist` | `[x] Approved` | 2026-09-22 |
