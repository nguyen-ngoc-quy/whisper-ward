# Sprint 01 — 2026-09-21 to 2026-10-02

## Sprint Goal
Triển khai hoàn chỉnh tầng Core Layer (Player Kinematic Controller, NavMesh Pathfinding Query Service, và Cinemachine Camera Rig) với 100% NUnit EditMode/Integration tests đạt chuẩn, thiết lập nền tảng di chuyển, định vị không gian và điều hướng cho AI Guard.

## Capacity
- Total days: 10 days (80h)
- Buffer (20%): 2 days (16h reserved for unplanned work / bugfixes)
- Available: 8 days (64h)
- Planned Load: 35h (~4.5 days)

## Tasks

### Must Have (Critical Path)
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|---|---|---|---|---|---|
| `CORE-P01` | Kinematic Locomotion, Diagonal Normalization & Linear Slew FSM | `unity-specialist` | 0.5d (4h) | None | AC-P1, AC-P2, AC-P5..P8, AC-P17, AC-P18, AC-P20, AC-P23 (`ADR-0006`) |
| `CORE-P02` | Camera-Relative Basis, Proportional Yaw Slew & Idle Facing Decoupling | `unity-specialist` | 0.5d (4h) | `CORE-P01` | AC-P3, AC-P4, AC-P19, AC-P21, AC-P22, AC-P24, `AC-P25` (`ADR-0006`) |
| `CORE-P03` | Feet-Anchored Capsule Scaling, Stand Headroom & HideSpot Exit | `unity-specialist` | 0.5d (4h) | `CORE-P02` | AC-P12, AC-P13, AC-P15, AC-P16 (`ADR-0006`) |
| `CORE-NAV01` | NonAlloc Path Query Service & Polyline Distance Metric | `ai-programmer` | 0.5d (4h) | None | AC-NAV-01..06, AC-NAV-09, AC-NAV-10, AC-NAV-15, AC-NAV-16 (`ADR-0007`) |
| `CORE-NAV02` | Reciprocal Velocity Avoidance & Spatial Navigation Interop | `ai-programmer` | 0.5d (4h) | `CORE-NAV01` | AC-NAV-11..14, AC-NAV-17 (`ADR-0007`) |
| `CORE-CAM01` | Third-Person Follow Rig & Planar Basis Service | `unity-specialist` | 0.5d (4h) | None | AC-CAM-01..05, AC1, AC2, AC5, AC10, AC11 (`ADR-0008`) |
| `CORE-CAM02` | Spherecast Deocclusion & Exponential Recovery Damping | `unity-specialist` | 0.5d (4h) | `CORE-CAM01` | AC-CAM-06..09, AC3, AC4, AC8, AC9 (`ADR-0008`) |

### Should Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|---|---|---|---|---|---|
| `CORE-NAV03` | Spatial Corridor Clearance & NavMesh Certification Validator | `ai-programmer` | 0.4d (3h) | `CORE-NAV02` | AC-NAV-07, AC-NAV-08, AC-NAV-18..21 (`ADR-0007`) |
| `CORE-CAM03` | Dynamic Chase FOV & HideSpot Viewport Blend | `unity-specialist` | 0.5d (4h) | `CORE-CAM02` | AC-CAM-10..13, AC6, AC7, AC12 (`ADR-0008`) |

### Nice to Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|---|---|---|---|---|---|
| `NICE-01` | Scene Mockup Integration: Playground Arena for Core Systems | `level-designer` | 1.0d (8h) | `CORE-P03`, `CORE-NAV02`, `CORE-CAM02` | Manual playable verification of Player, Guard NavMesh, and Camera |

## Carryover from Previous Sprint
| Task | Reason | New Estimate |
|---|---|---|
| None | First formal sprint of Pre-Production phase | N/A |

## Risks
| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| GC allocation spike during hot-path character/camera updates | Low | High | Enforce pre-allocated non-alloc buffers and NUnit 0 B GC assertions |
| Thin-wall spherecast clipping when camera orbits against corners | Medium | Medium | Spherecast radius $0.20\text{ m}$ against E20 Solid with instant distance collapse ($D_{\text{min}} = 0.40\text{ m}$) |
| Multi-guard simultaneous repathing causing CPU spikes on WebGL | Medium | High | Staggered query scheduler round-robin (max 1 query/frame tick) |

## Dependencies on External Factors
- Unity 6 LTS (6000.3.17f1) editor installation for PlayMode test verification.
- Packages: Universal Render Pipeline (URP), Cinemachine 3.x, AI Navigation, New Input System.

## Definition of Done for this Sprint
- [ ] All Must Have tasks completed
- [ ] All tasks pass acceptance criteria
- [x] QA plan exists (`production/qa/qa-plan-sprint-01-2026-09-21.md`)
- [ ] All Logic/Integration stories have passing unit/integration tests in `tests/`
- [ ] Smoke check passed (`/smoke-check sprint`)
- [ ] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS (`/team-qa sprint`)
- [ ] No S1 or S2 bugs in delivered features
- [ ] Design documents updated for any deviations
- [ ] Code reviewed and merged into main
