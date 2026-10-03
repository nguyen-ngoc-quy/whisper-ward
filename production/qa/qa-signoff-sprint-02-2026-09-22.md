# QA Sign-Off Report: Sprint 02 — 3D Level Playground & Playable Prototype

> **Date**: 2026-09-22  
> **Sprint**: Sprint 02 (`production/sprints/sprint-02.md`)  
> **Epic**: Level Playground (`production/epics/level-playground/EPIC.md`)  
> **Scene Target**: `Assets/Scenes/CorePlayground.unity`  
> **Engine**: Unity 6 LTS (6000.3.17f1) | URP | Cinemachine 3.x | Input System | AI Navigation  
> **Review Mode**: `lean`  
> **Author**: `qa-lead`  

---

## 1. Executive Summary

Sprint 02 successfully bridges pure C# mathematical logic into a tangible, high-fidelity, interactive 3D prototype inside Unity 6 LTS. All 6 planned stories (4 Must Have, 1 Should Have, 1 Nice to Have) have been fully authored, verified, and integrated into `Assets/Scenes/CorePlayground.unity`.

The scene incorporates full arena geometry ($20\text{ m} \times 20\text{ m}$ floor, $3.0\text{ m}$ perimeter walls, $1.50\text{ m}$ and $1.20\text{ m}$ standardized corridors, low-headroom alcove with $1.40\text{ m}$ ceiling), automated prefab generation (`Player_Capsule.prefab` and `Guard_PatrolNPC.prefab`), third-person Cinemachine orbit follow with real-time spherecast deocclusion, NavMesh endless patrol loop, an on-screen locomotion telemetry HUD (`DebugLocomotionHUD`), and URP moonlit stealth post-processing with high-contrast color materials.

A total of **26 automated NUnit Integration tests** were authored across 5 test suites in `tests/integration/scene/` (bringing the project total to **115 automated tests**), achieving 100% pass rate. An interactive 5-minute playtest session confirmed **0 console exceptions** and rock-solid **60 FPS** performance.

---

## 2. Test Coverage & Story Verification Summary

| Story ID | Story Title | Type | Automated Test Suite / QA Evidence | Test Count | Result |
|---|---|---|---|:---:|:---:|
| `SCENE-01` | 3D Arena Geometry Blockout & Moonlit Lighting Setup | Integration | `tests/integration/scene/core_playground_geometry_test.cs` | 6 | **PASS** ✅ |
| `SCENE-02` | Player Capsule Prefab & Runtime Input Driver Integration | Integration | `tests/integration/scene/player_runtime_driver_integration_test.cs` | 5 | **PASS** ✅ |
| `SCENE-03` | Cinemachine Camera Rig & Orbit Follow Integration | Integration | `tests/integration/scene/camera_orbit_driver_integration_test.cs` | 6 | **PASS** ✅ |
| `SCENE-04` | NavMesh Surface Baking & Simple Patrol Guard NPC | Integration | `tests/integration/scene/simple_patrol_driver_integration_test.cs` | 4 | **PASS** ✅ |
| `SCENE-05` | On-Screen Locomotion Debug HUD & Playtest Verification Suite | UI / Playtest | `tests/integration/scene/debug_locomotion_hud_integration_test.cs` + `core-playground-playtest-evidence.md` | 5 | **PASS** ✅ |
| `SCENE-06` | URP Moonlit Stealth Post-Processing & Distinct Materials | Visual/Feel | `production/qa/evidence/scene-06-materials-post-processing.md` | Evidence | **PASS** ✅ |
| **Total** | **6 Stories Completed** | | **5 Integration Suites + 6 Evidence Documents** | **26 (115 total)** | **100% PASS** |

---

## 3. Architecture & Performance Invariants Verified

1. **Kinematic CharacterController & Stance Scaling (ADR-0006, AC-SCENE-05..08)**:
   - Player capsule height continuously interpolates between $1.80\text{ m}$ (Stand) and $1.20\text{ m}$ (Crouch).
   - CharacterController `center.y = height * 0.5f` guarantees feet remain strictly anchored at ground level ($Y = 0\text{ m}$).
   - Low-ceiling alcove upward capsule probe rejects stance expansion when headroom is below $1.80\text{ m}$ (ceiling at $Y = 1.40\text{ m}$), preventing clipping.
2. **Camera Orbit & Real-Time Spherecast Deocclusion (ADR-0008, AC-SCENE-09..12)**:
   - Right-shoulder framing ($D_{\text{nom}} = 2.80\text{ m}, Y_{\text{target}} = 1.35\text{ m}, X_{\text{offset}} = +0.35\text{ m}$).
   - Strict pitch clamping to $[-35.0^\circ, +65.0^\circ]$ with angular look speed capped at $720.0^\circ/\text{s}$.
   - Instantaneous in-snap ($0\text{ ms}$) on wall obstruction ($D_{\text{min}} = 0.40\text{ m}$), followed by smooth exponential pull-out ($\tau = 0.25\text{ s}$).
   - Smooth chase FOV widening ($60^\circ \to 68^\circ$) and HideSpot yaw cone clamping ($\pm 30^\circ$).
3. **NavMesh Surface & Guard Patrol Loop (ADR-0007, AC-SCENE-13..15)**:
   - Static environment baked with Humanoid agent parameters ($R = 0.40\text{ m}, H = 1.80\text{ m}$).
   - Guard NPC patrols continuously at $2.30\text{ m/s}$ with $2.0\text{ s}$ dwell pause at each destination across standardized $1.50\text{ m}$ corridors.
4. **Memory Allocation & Frame Budget (Zero GC Hot-Path)**:
   - Zero managed heap allocations per frame across `PlayerRuntimeDriver`, `CameraOrbitDriver`, `SimplePatrolDriver`, and `DebugLocomotionHUD`.
   - Frame rate maintains rock-solid 60 FPS ($16.6\text{ ms}$ budget).
5. **URP Atmospheric Post-Processing & Visual Separation (AC-SCENE-18..19)**:
   - High-contrast color assignments: Floor (`#1C222B`), Walls (`#38404B`), Player (`#2AC3A2` Teal), and Guard (`#D9383A` Red).
   - Global Volume with ACES Tonemapping and subtle Vignette ($0.25$) establishing moonlit stealth atmosphere.

---

## 4. Bug Tracking & Defect Summary

| Bug ID | Title | Severity | Status | Resolution |
|:---:|---|:---:|:---:|:---:|
| — | Không phát hiện lỗi nghiêm trọng (Zero S1/S2/S3/S4 bugs open) | — | Closed | 100% tiêu chí nghiệm thu đạt chuẩn, 0 console exceptions |

---

## 5. QA Verdict

### **VERDICT: APPROVED** ✅

- Tất cả 6/6 Stories đều đã hoàn thành và đạt chuẩn nghiệm thu.
- 26 bài test tích hợp NUnit bảo đảm mọi cơ chế hoạt động chính xác trong không gian 3D.
- 0 lỗi ngoại lệ (0 Unhandled Exceptions) trong phiên chơi thử kéo dài 5 phút liên tục.
- Scene `Assets/Scenes/CorePlayground.unity` sẵn sàng để bàn giao cho giai đoạn tích hợp AI chuyên sâu (Sprint 3: Guard AI FSM & Perception).

---

## 6. Next Steps

1. Tiến hành chạy `/retrospective` cho Sprint 2 để đánh giá năng suất và đúc kết kinh nghiệm.
2. Khởi tạo kế hoạch cho **Sprint 3 (Guard AI Perception & Behavior Tree)**:
   - Trí tuệ nhân tạo thị giác lính gác (Perception Cone: $90^\circ$ góc nhìn, $12\text{ m}$ tầm nhìn).
   - Máy trạng thái hành vi lính gác (Patrol -> Suspicious -> Alert -> Search -> Chase).
   - Hệ thống phát tán tiếng động bước chân của người chơi (Player Noise Propagation).
