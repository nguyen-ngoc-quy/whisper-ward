# Sprint 04 — 2026-10-15 to 2026-10-26

## Sprint Goal
Nâng cấp trải nghiệm lén lút chiến thuật với Cơ chế Ẩn nấp (Hide Spots), Đồ vật Đánh lạc hướng (Burst Distraction), Mạng lưới Báo động Phối hợp Lính gác (Coordinated Alert & Radio Bark), và Giao diện Cảnh báo Nghi ngờ (Suspicion Meter HUD).

## Capacity
- Total days: 10 days (80h)
- Buffer (20%): 2 days (16h reserved for unplanned work / bugfixes)
- Available: 8 days (64h)
- Planned Load: 21h (~2.6 days)

## Tasks

### Must Have (Critical Path)
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `HIDE-01` | Hide Spot & Sanctuary Interaction Volume | `gameplay-programmer` | 0.5d (4h) | None | AC-HIDE-01..04 (Tương tác vào/ra hốc nấp, che khuất tầm nhìn nếu trốn khi chưa bị phát hiện, bẫy tử thần nếu chui vào khi đang bị đuổi theo) | Complete |
| `NOISE-02` | Burst Noise-Maker Distraction Tool | `gameplay-programmer` | 0.5d (4h) | `HIDE-01` | AC-NOISE-05..08 (Ném đồ vật tạo xung âm thanh 10m tại điểm va chạm, thu hút lính gác tới điều tra, cộng dồn cảnh giác dư thừa nếu không thấy ai) | Complete |
| `ALERT-01` | Guard Radio Bark & Coordinated Alert | `ai-programmer` | 0.5d (4h) | None | AC-ALERT-01..04 (Lính gác phát tín hiệu bộ đàm khi đuổi bắt, điều hướng lính gác tuần tra lân cận tới khu vực nghi vấn với sai số 1.5m, lính đang Chase miễn nhiễm alert) | Complete |

### Should Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `UI-SUSP-01` | Suspicion Meter & Threat Chevron HUD | `ui-programmer` | 0.4d (3h) | `ALERT-01` | AC-UI-01..03 (Hiển thị thanh nghi ngờ của lính gác nguy hiểm nhất, vạch ngưỡng hạ dần theo cảnh giác, chevron 360 độ chỉ hướng lính gác) | Complete |
| `ALERT-02` | Multi-Guard Search Standoff Coordination | `ai-programmer` | 0.4d (3h) | `ALERT-01` | AC-ALERT-05..06 (Nhiều lính gác tới điều tra cùng một điểm LKP sẽ tản ra các góc tìm kiếm lệch nhau, không đứng đè chồng lên nhau) | Complete |

### Nice to Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `FEEDBACK-01` | Guard Overhead Markers & Telegraphs | `technical-artist` | 0.3d (3h) | `ALERT-01` | AC-FEED-01..02 (Biểu tượng ? trên đầu khi vào trạng thái Investigate, biểu tượng ! khi vào trạng thái Chase kèm âm thanh báo hiệu) | Complete |

## Carryover from Previous Sprint
None (Sprint 03 hoàn thành 100% 6/6 stories).

## Risks
| Risk | Probability | Impact | Mitigation | Owner |
|---|---|---|---|---|
| Xung đột điều hướng giữa nhiều lính gác khi cùng tìm kiếm 1 điểm LKP | Medium | High | Áp dụng bán kính sai số `r_investigate_error = 1.5m` và thuật toán phân tán góc nhìn khi đến đích | `ai-programmer` |
| Lỗi trốn vào hốc nấp khi camera đang đuổi theo | Low | Medium | Sử dụng camera transition blend từ CameraRigService đã được chứng nhận ở Sprint 01 | `gameplay-programmer` |
| Quá tải số lượng sự kiện âm thanh nếu ném liên tục | Low | Medium | Ràng buộc số lượng Burst mang theo tối đa (1 lần ném/lượt chơi) theo GDD Player Noise | `gameplay-programmer` |

## Dependencies on External Factors
- Scene 3D `CorePlayground.unity` và Prefabs `Player_Capsule`, `Guard_PatrolNPC`.
- Unity 6 LTS (6000.3.17f1).

## Definition of Done for this Sprint
- [ ] Toàn bộ 3 tasks Must Have hoàn thành (`HIDE-01`, `NOISE-02`, `ALERT-01`)
- [ ] Tất cả các bài test tự động logic NUnit mới vượt qua 100% (Zero GC hot-path)
- [ ] Chơi thử tương tác trong `CorePlayground.unity`:
  - Trốn vào hốc nấp khi lính gác đi ngang qua -> lính không thấy
  - Ném đồ vật đánh lạc hướng -> lính gác chạy lại vị trí va chạm
  - Lính gác phát hiện người chơi -> hú còi bộ đàm -> lính khác gần đó cùng chạy lại hỗ trợ
- [ ] Không có ngoại lệ (0 console errors / NullReferenceException)
- [ ] QA Plan và QA Sign-Off Sprint 04 được phê duyệt
- [ ] Retrospective Sprint 04 hoàn tất và code được commit/push lên GitHub main
