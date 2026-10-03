# QA Sign-Off Report: Sprint 01 — Core Layer Execution

> **Date**: 2026-09-22  
> **Sprint**: Sprint 01 (`production/sprints/sprint-01.md`)  
> **Layer**: Core Layer (#11 Player Controller, #12 NavMesh Pathfinding, #20 Camera Rig)  
> **Engine**: Unity 6 LTS (6000.3.17f1)  
> **Review Mode**: `lean`  
> **Author**: `qa-lead`  

---

## 1. Executive Summary

Sprint 01 establishes the foundational spatial locomotion, pathfinding reachability, and third-person camera reference systems of *Whisper Ward*. All 9 planned stories (7 Must Have, 2 Should Have) have been fully implemented, code-reviewed, and verified against governing Architecture Decision Records (`ADR-0006`, `ADR-0007`, `ADR-0008`) and Game Design Documents (`design/gdd/player-movement-hide.md`, `design/gdd/navmesh-pathfinding.md`, `design/gdd/camera-cinemachine.md`).

A total of **89 automated NUnit EditMode & Integration tests** were authored and verified, achieving **100% test coverage** across all acceptance criteria with **0 B GC managed allocations** in hot loops.

---

## 2. Test Coverage & Story Verification Summary

| Story ID | Story Title | Type | Automated Test Suite | Test Count | Result |
|---|---|---|---|:---:|:---:|
| `CORE-P01` | Kinematic Locomotion, Diagonal Normalization & Linear Slew FSM | Logic | `tests/unit/playercontroller/playercontroller_movement_test.cs` | 11 | **PASS** ✅ |
| `CORE-P02` | Camera-Relative Basis, Proportional Yaw Slew & Idle Facing Decoupling | Logic | `tests/unit/playercontroller/playercontroller_orientation_test.cs` | 9 | **PASS** ✅ |
| `CORE-P03` | Feet-Anchored Capsule Scaling, Stand Headroom & HideSpot Exit | Logic | `tests/unit/playercontroller/playercontroller_capsule_test.cs` | 11 | **PASS** ✅ |
| `CORE-NAV01` | NonAlloc Path Query Service & Polyline Distance Metric | Logic | `tests/unit/navigation/navmesh_query_test.cs` | 12 | **PASS** ✅ |
| `CORE-NAV02` | Reciprocal Velocity Avoidance & Spatial Navigation Interop | Integration | `tests/integration/navigation/navmesh_avoidance_interop_test.cs` | 6 | **PASS** ✅ |
| `CORE-NAV03` | Spatial Corridor Clearance & NavMesh Certification Validator | Logic | `tests/unit/navigation/navmesh_certification_test.cs` | 16 | **PASS** ✅ |
| `CORE-CAM01` | Third-Person Follow Rig & Planar Basis Service | Logic | `tests/unit/camera/camera_follow_rig_test.cs` | 6 | **PASS** ✅ |
| `CORE-CAM02` | Spherecast Deocclusion & Exponential Recovery Damping | Logic | `tests/unit/camera/camera_deocclusion_test.cs` | 11 | **PASS** ✅ |
| `CORE-CAM03` | Dynamic Chase FOV & HideSpot Viewport Blend | Integration | `tests/integration/camera/camera_blend_integration_test.cs` | 7 | **PASS** ✅ |
| **Total** | **9 Stories Completed** | | **8 Suites** | **89** | **100% PASS** |

---

## 3. Architecture & Performance Invariants Verified

1. **Memory Ceiling & Hot Path Allocations (ADR-0001, ADR-0007, ADR-0008)**:
   - Verified 0 B heap allocation during character kinematic movement, camera evaluation, and NavMesh polyline calculations.
   - Pre-allocated corner buffers (`Vector3[128]`) and raycast hit buffers (`RaycastHit[2]`) prevent runtime garbage collection hitching.
2. **Speed Ratio & Locomotion Invariant (ADR-0006, GDD #03)**:
   - Verified $V_{\text{chase}} / V_{\text{run}} \ge 1.20$ ($7.50 / 6.25 = 1.20$).
   - Verified linear slew ramp ($a_{\text{max}} = 78.125\text{ m/s}^2$) reaching top speed in exactly $0.08\text{ s}$ without velocity jump.
   - Verified diagonal normalization eliminating the $\sqrt{2}$ speed exploit.
3. **Camera-Relative Basis & Invariant AC-P25 (ADR-0008)**:
   - Verified that zero input (idle) maintains character facing orientation regardless of 360-degree camera yaw rotation.
   - Pitch clamped strictly to $[-35^\circ, +65^\circ]$ with pitch degeneracy fallback handling vertical look singularity ($\pm 89.9^\circ$).
4. **Collision & Occlusion Safety (ADR-0002, ADR-0008)**:
   - Verified instantaneous spherecast collapse ($0\text{ ms}$) on wall obstruction ($D_{\text{min}} = 0.40\text{ m}$) preventing geometry clipping.
   - Verified exponential pull-out recovery ($\tau = 0.25\text{ s}$) reaching $\ge 95\%$ nominal distance within $0.75\text{ s}$.
   - Verified dither opacity range $[0.15, 1.0]$ when distance $< 0.60\text{ m}$.
5. **NavMesh & Spatial Certification (ADR-0007)**:
   - Verified choke point detection for corridor clearances $< 1.20\text{ m}$ (`ERR_CORRIDOR_CHOKE_POINT`).
   - Verified zero off-mesh link enforcement and bake layer mask exclusion of dynamic actor layers.

---

## 4. Bug Tracking & Defect Summary

| Bug ID | Title | Severity | Status | Resolution |
|:---:|---|:---:|:---:|:---:|
| — | Không phát hiện lỗi nghiêm trọng (Zero S1/S2/S3/S4 bugs open) | — | Closed | 100% tiêu chí nghiệm thu đạt chuẩn |

---

## 5. QA Verdict

### **VERDICT: APPROVED** ✅

- Tất cả 9/9 Stories đều đã hoàn thành và đạt chuẩn kiểm thử.
- 89 bài test NUnit bao phủ toàn diện các ca kiểm thử biên và bất biến kỹ thuật.
- Không tồn đọng bất kỳ blocker hoặc tech debt chưa xử lý.

---

## 6. Next Steps

1. Tiến hành chạy `/retrospective` để đúc rút bài học kinh nghiệm và đo lường velocity của Sprint 1.
2. Lập kế hoạch cho Sprint tiếp theo (`/sprint-plan new`), tập trung vào tầng trí tuệ nhân tạo lính gác (AI Behavior Tree, Perception Cone, Alert Propagation) hoặc tích hợp Scene kiểm thử 3D.
