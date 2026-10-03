# Sprint 02 — 2026-09-22 to 2026-10-03

## Sprint Goal
Xây dựng Scene 3D tương tác thực tế (`Assets/Scenes/CorePlayground.unity`) trong Unity 6 LTS, tích hợp hoàn chỉnh Player Capsule, Cinemachine Camera Rig và NavMesh Guard NPC, chuyển hóa 100% logic Core Layer thành trải nghiệm chơi thử bằng tay trực quan (Playable Prototype).

## Capacity
- Total days: 10 days (80h)
- Buffer (20%): 2 days (16h reserved for unplanned work / bugfixes)
- Available: 8 days (64h)
- Planned Load: 21h (~2.6 days)

## Tasks

### Must Have (Critical Path)
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|---|---|---|---|---|---|
| `SCENE-01` | 3D Arena Geometry Blockout & Moonlit Lighting Setup | `level-designer` | 0.5d (4h) | None | AC-SCENE-01..04 (Arena 20x20m, Hành lang 1.5m/1.2m, Hốc HideSpot trần 1.4m, URP Moonlit Light) |
| `SCENE-02` | Player Capsule Prefab & Runtime Input Driver Integration | `unity-specialist` | 0.5d (4h) | `SCENE-01` | AC-SCENE-05..08 (Prefab Player Capsule, PlayerRuntimeDriver, WASD+Mouse, Heading Gizmo) |
| `SCENE-03` | Cinemachine Camera Rig & Orbit Follow Integration | `unity-specialist` | 0.5d (4h) | `SCENE-02` | AC-SCENE-09..12 (Cinemachine package, Orbit Yaw/Pitch, E20 Spherecast deocclusion, HideSpot blend) |
| `SCENE-04` | NavMesh Surface Baking & Simple Patrol Guard NPC | `ai-programmer` | 0.5d (4h) | `SCENE-01` | AC-SCENE-13..15 (NavMeshSurface bake, Guard Capsule NPC tuần tra 2 waypoints, di chuyển mượt mà) |

### Should Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|---|---|---|---|---|---|
| `SCENE-05` | On-Screen Locomotion Debug HUD & Playtest Verification Suite | `qa-lead` | 0.4d (3h) | `SCENE-02`, `SCENE-03`, `SCENE-04` | AC-SCENE-16..17 (HUD hiển thị Speed/Stance/Distance/Headroom, Playtest evidence doc không lỗi console) |

### Nice to Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|---|---|---|---|---|---|
| `SCENE-06` | URP Moonlit Stealth Post-Processing & Distinct Materials | `technical-artist` | 0.3d (2h) | `SCENE-01` | AC-SCENE-18 (Volume profile đêm trăng, vật liệu phân màu tương phản Player/Guard/Vật cản) |

## Carryover from Previous Sprint
| Task | Reason | New Estimate |
|---|---|---|
| `NICE-01` (Scene Mockup Integration) | Được nâng cấp và chuyển hóa thành trọng tâm chính của Sprint 2 theo Retrospective ACT-01 | Phân bổ vào `SCENE-01`..`SCENE-05` |

## Risks
| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Thiếu package Cinemachine 3.x trong Unity 6 LTS | Medium | High | Thêm `"com.unity.cinemachine": "3.1.2"` vào `Packages/manifest.json` trước khi dựng Rig |
| Input Action binding xung đột hoặc thiếu mouse delta | Low | Medium | Sử dụng trực tiếp `Assets/InputSystem_Actions.inputactions` chuẩn của Unity 6 |
| NavMeshSurface cần component từ package `com.unity.ai.navigation` | Low | Low | Package `com.unity.ai.navigation` (2.0.12) đã có sẵn trong dự án |

## Dependencies on External Factors
- Unity 6 LTS (6000.3.17f1) mở project để compile asset và PlayMode testing.
- Packages: Universal Render Pipeline (URP), Cinemachine 3.x, AI Navigation, New Input System.

## Definition of Done for this Sprint
- [x] All Must Have tasks completed (`SCENE-01`..`SCENE-04`)
- [x] All tasks pass acceptance criteria
- [x] QA plan exists (`production/qa/qa-plan-sprint-02-2026-09-22.md`)
- [x] Playable manual verification record exists (`production/qa/evidence/core-playground-playtest-evidence.md`)
- [x] Bấm Play trong Unity: Player di chuyển mượt mà, camera orbit chuẩn xác, Guard tuần tra đúng lộ trình
- [x] Không có ngoại lệ (0 Unhandled Exceptions / NullReferenceException) trong Unity Console
- [x] Smoke check passed (`production/qa/evidence/core-playground-playtest-evidence.md`)
- [x] QA sign-off report: APPROVED (`production/qa/qa-signoff-sprint-02-2026-09-22.md`)
- [ ] Code reviewed và commit sạch lên GitHub main
