# QA Evidence: SCENE-02 — Player Capsule Prefab & Runtime Input Driver Integration

> **Story**: `production/epics/level-playground/story-002-player-prefab-input-driver.md`  
> **Date**: 2026-09-22  
> **Engine**: Unity 6 LTS (6000.3.17f1)  
> **Reviewer**: `gameplay-programmer` / `qa-lead` / `unity-specialist`  
> **Result**: VERIFIED ✅  

---

## 1. Physical Capsule & Kinematic Controller Dimensions

| Parameter | Target Invariant (ADR-0006) | Implemented Value | Status |
|---|---|---|:---:|
| **Radius ($R$)** | $0.30\text{ m}$ | $0.30\text{ m}$ | **PASS** ✅ |
| **Standing Height ($H_{\text{stand}}$)** | $1.80\text{ m}$ | $1.80\text{ m}$ | **PASS** ✅ |
| **Standing Center ($Y$)** | $H \times 0.5 = 0.90\text{ m}$ | $(0, 0.90, 0)$ | **PASS** ✅ |
| **Crouched Height ($H_{\text{crouch}}$)** | $1.20\text{ m}$ | $1.20\text{ m}$ | **PASS** ✅ |
| **Crouched Center ($Y$)** | $H \times 0.5 = 0.60\text{ m}$ | $(0, 0.60, 0)$ | **PASS** ✅ |
| **Skin Width** | $0.03\text{ m}$ | $0.03\text{ m}$ | **PASS** ✅ |
| **Step Offset** | $0.30\text{ m}$ | $0.30\text{ m}$ | **PASS** ✅ |
| **Slope Limit** | $45.0^\circ$ | $45.0^\circ$ | **PASS** ✅ |
| **Physics Layer** | Layer 6 (`Player`) | Layer 6 | **PASS** ✅ |

---

## 2. Locomotion & Stance Gating Verification

| Verification Target | Test Scenario | Behavior Observed | Status |
|---|---|---|:---:|
| **Camera-Relative Locomotion** | Move input $(0, 1)$ with Yaw $90^\circ$ | Velocity shifts from $+Z$ to $+X$ basis | **PASS** ✅ |
| **Stance Transition Slewing** | Toggle Crouch in open space | Capsule height slews $1.80\text{ m} \to 1.20\text{ m}$ in $0.2\text{ s}$ | **PASS** ✅ |
| **Feet-Anchored Scaling** | Center co-moves with height | $Y_{\text{feet}} = \text{center}.y - H/2 = 0.0\text{ m}$ throughout | **PASS** ✅ |
| **Low-Headroom Obstruction** | Headroom probe blocked ($Y=1.40\text{ m} < 1.80\text{ m}$) | Stand request rejected; stance locked in Crouch | **PASS** ✅ |
| **Zero Memory Allocation** | Input packet polling & dispatch | $0\text{ B}$ managed heap allocations per frame | **PASS** ✅ |

---

## 3. Automated Test Coverage

Suite: `tests/integration/scene/player_runtime_driver_integration_test.cs`
- `test_player_capsule_initial_dimensions_conform_to_ac_scene_05` — **PASS** ✅
- `test_runtime_driver_forward_input_advances_locomotion_along_camera_facing` — **PASS** ✅
- `test_runtime_driver_camera_yaw_rotates_planar_displacement` — **PASS** ✅
- `test_player_locomotion_stance_toggle_shrinks_and_restores_capsule_dimensions` — **PASS** ✅
- `test_player_headroom_stand_clearance_query_rejects_uncrouch_under_low_headroom` — **PASS** ✅

---

## 4. Sign-Off

| Role | Name / Agent | Verdict | Signature Date |
|---|---|:---:|:---:|
| **Gameplay Programmer** | `gameplay-programmer` | `[x] Approved` | 2026-09-22 |
| **QA Lead** | `qa-lead` | `[x] Approved` | 2026-09-22 |
| **Unity Specialist** | `unity-specialist` | `[x] Approved` | 2026-09-22 |
