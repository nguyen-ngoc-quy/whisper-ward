# Sprint 05 — 2026-10-27 to 2026-11-07

## Sprint Goal
Xây dựng vòng lặp nhiệm vụ chơi được hoàn chỉnh (Playable Objective Loop): Lấy vật phẩm mục tiêu (Keycard/Data Drive), mở khóa cổng bảo an (Locked Security Gate), rút lui an toàn qua ranh giới thoát hiểm (Extraction Zone), và Đánh giá Xếp hạng Lén lút (Stealth Grade Operator System) với bảng tổng kết điểm số Rank S/A/B/C/F.

## Capacity
- Total days: 10 days (80h)
- Buffer (20%): 2 days (16h reserved for unplanned work / bugfixes)
- Available: 8 days (64h)
- Planned Load: 19h (~2.4 days)

## Tasks

### Must Have (Critical Path)
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `GRADE-01` | Stealth Grade Operator System | `systems-designer` / `gameplay-programmer` | 0.5d (4h) | None | AC-GRADE-01..05 (Chấm điểm chuẩn GDD: S_base = 100, phạt Chase -25đ/lần gộp toàn phòng, miễn phạt Burst distraction, phạt cảnh giác dư thừa <= 10đ, khóa Rank S nếu N_chase >= 1, Rank F nếu bị bắt) | Complete |
| `OBJ-01` | Objective Keycard Pickup & Inventory State | `gameplay-programmer` | 0.5d (4h) | None | AC-OBJ-01..03 (Vật thể nhiệm vụ Keycard nhặt được trong tầm tương tác, phát sự kiện OnObjectiveAcquired, cập nhật trạng thái người chơi) | Complete |
| `OBJ-02` | Locked Security Gate & Extraction Zone | `level-designer` / `gameplay-programmer` | 0.5d (4h) | `OBJ-01` | AC-OBJ-04..06 (Cửa an ninh yêu cầu Keycard để mở, Extraction Boundary hoàn thành màn chơi khi người chơi đã có Keycard, chuyển trạng thái cho Grade Operator) | Complete |

### Should Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `FLOW-01` | Mission Game Loop & Lifecycle Controller | `gameplay-programmer` | 0.4d (3h) | `GRADE-01`, `OBJ-02` | AC-FLOW-01..03 (Điều phối vòng đời nhiệm vụ: Infiltrate -> ObjectiveAcquired -> Extracting -> Completed / Captured, kết nối FSM lính gác và Grade Operator) | Complete |
| `UI-RESULT-01` | End-of-Mission Scorecard & Grade Card HUD | `ui-programmer` | 0.5d (4h) | `GRADE-01`, `FLOW-01` | AC-UI-05..07 (Hiển thị bảng tổng kết huân chương S/A/B/C/F, điểm số, chi tiết số lần bị phát hiện và thời gian hoàn thành) | Complete |

## Carryover from Previous Sprint
None (Sprint 04 hoàn thành 100% 6/6 stories, 224 tests passing).

## Risks
| Risk | Probability | Impact | Mitigation | Owner |
|---|---|---|---|---|
| Phụ thuộc vòng giữa FSM lính gác bắt giữ và Mission Controller | Low | High | Sử dụng kiến trúc hướng sự kiện lỏng lẻo (`OnPlayerCaptured`, `OnMissionCompleted`), không gọi hàm trực tiếp | `gameplay-programmer` |
| Người chơi chạm cổng thoát hiểm trước khi lấy Keycard | Low | Medium | Khóa cửa bằng collider vật lý và chỉ kích hoạt ranh giới hoàn thành khi cờ `hasKeycard == true` | `level-designer` |
| Tính toán điểm sai lệch do nhiều lính gác cùng phát hiện cùng lúc | Low | High | Bộ chấm điểm `StealthGradeOperator` lọc trùng sự kiện (deduplication) và gộp phạt Chase toàn phòng | `systems-designer` |

## Definition of Done for this Sprint
- [x] Hoàn thành 100% 5 stories (`GRADE-01`, `OBJ-01`, `OBJ-02`, `FLOW-01`, `UI-RESULT-01`)
- [x] Tất cả các bài kiểm thử NUnit Unit & Integration mới vượt qua 100%
- [x] Tích hợp vào Scene 3D `CorePlayground.unity`:
  - Người chơi xuất phát tại Spawn Point
  - Lẻn qua 2 lính gác, nhặt Keycard trên bàn
  - Dùng Keycard mở cửa thoát hiểm
  - Chạy qua ranh giới thoát hiểm -> hiển thị Scorecard đạt Rank (S/A/B/C)
  - Nếu bị lính bắt -> hiển thị Scorecard Rank F
- [x] Không có lỗi console hoặc rò rỉ bộ nhớ (0 GC allocations per frame trên hot-path)
- [x] Báo cáo QA Sign-Off và Retrospective Sprint 05 được phê duyệt
