# Sprint 03 — 2026-10-03 to 2026-10-14

## Sprint Goal
Tích hợp hệ thống Thị giác (Vision Cone 100°, 12m), Máy đo Nghi ngờ (Suspicion Accumulator & Residual), Máy trạng thái FSM (Patrol -> Investigate -> Chase), và Cơ chế Phát tán Tiếng ồn bước chân vào Scene 3D CorePlayground.unity, tạo nên vòng lặp gameplay hành động lén lút (Stealth Loop) hoàn chỉnh.

## Capacity
- Total days: 10 days (80h)
- Buffer (20%): 2 days (16h reserved for unplanned work / bugfixes)
- Available: 8 days (64h)
- Planned Load: 22h (~2.8 days)

## Tasks

### Must Have (Critical Path)
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `GUARD-01` | Vision Cone Sensor & Line-of-Sight Sensing Engine | `ai-programmer` | 0.5d (4h) | None | AC-GUARD-01..04 (FOV 100°, Range 12m, Linecast E20 mask, Stand/Crouch stance height offsets, 5Hz tick) | Not Started |
| `GUARD-02` | Suspicion Accumulator & Residual Wariness Engine | `systems-designer` | 0.5d (4h) | `GUARD-01` | AC-GUARD-05..08 (Tích lũy dA/dt = min(1.5/d, 0.6), cửa sổ xác nhận 1.2s, chỉ số R giảm ngưỡng, n_cancel >= 3 kích hoạt điều tra) | Not Started |
| `GUARD-04` | Guard FSM Runtime Controller (Patrol -> Investigate -> Chase) | `ai-programmer` | 0.5d (4h) | `GUARD-01`, `GUARD-02` | AC-GUARD-09..12 (Tích hợp FSM lên Guard_PatrolNPC, chuyển tốc độ 2.3 -> 5.0 -> 7.5 m/s, Catch range 5.5m trong 1.0s) | Not Started |
| `NOISE-01` | Player Footstep Noise Emitter & Guard Hearing Evaluation | `gameplay-programmer` | 0.5d (4h) | `GUARD-04` | AC-NOISE-01..04 (Phát tán tiếng bước chân Cúi 0m / Đi 4m / Chạy 6m, lính gác nghe và chuyển hướng điều tra) | Not Started |

### Should Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `GUARD-03` | Vision Cone 3D Mesh Visualizer & Suspicion Indicator | `technical-artist` | 0.4d (3h) | `GUARD-01`, `GUARD-02` | AC-GUARD-13..14 (Chiếu nón ánh sáng 3D trên mặt đất, đổi màu Xanh/Vàng/Đỏ theo độ nghi ngờ) | Not Started |
| `GUARD-05` | Search Dwell & Give-up Return to Patrol | `ai-programmer` | 0.4d (3h) | `GUARD-04` | AC-GUARD-15..16 (Dừng lại quan sát 4.0s khi mất dấu LKP, bỏ cuộc quay về tuần tra nếu không tìm thấy) | Not Started |

## Carryover from Previous Sprint
None (Sprint 02 hoàn thành 100% 6/6 stories).

## Risks
| Risk | Probability | Impact | Mitigation | Owner |
|---|---|---|---|---|
| Hiệu năng Linecast tầm nhìn khi quét liên tục | Medium | High | Giới hạn tần số quét 5 Hz (200ms/lần) theo GDD R1, không chạy trong Update() mỗi frame | `ai-programmer` |
| Tranh chấp trạng thái giữa Nghe tiếng động và Nhìn thấy người chơi | Low | Medium | Áp dụng nguyên tắc ưu tiên Chase-wins và Thị giác > Thính giác theo GDD Perception R12/R13 | `ai-programmer` |
| Nón thị giác chiếu xuyên tường | Medium | Medium | Sử dụng thuật toán cắt mesh theo raycast để nón mesh bị chặn bởi tường World | `technical-artist` |

## Dependencies on External Factors
- Unity 6 LTS (6000.3.17f1) mở project để compile asset và PlayMode testing.
- Packages: Universal Render Pipeline (URP), Cinemachine 3.x, AI Navigation, New Input System.

## Definition of Done for this Sprint
- [ ] Toàn bộ 4 tasks Must Have hoàn thành (`GUARD-01`, `GUARD-02`, `GUARD-04`, `NOISE-01`)
- [ ] Tất cả các bài test tự động logic NUnit mới vượt qua 100% (Zero GC hot-path)
- [ ] Mở Unity bấm Play: Lính gác tuần tra -> thấy người chơi thì tăng thanh nghi ngờ -> đuổi theo khi nghi ngờ đầy -> bắt giữ người chơi khi tiếp cận
- [ ] Nghe tiếng bước chân chạy gần tường -> lính gác quay lại điều tra điểm phát ra âm thanh
- [ ] Không có ngoại lệ (0 console errors / NullReferenceException)
- [ ] QA Plan và QA Sign-Off Sprint 03 được phê duyệt
- [ ] Retrospective Sprint 03 hoàn tất và code được commit/push lên GitHub main
