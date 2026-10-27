# Sprint 06 — 2026-10-27 to 2026-11-10

## Sprint Goal
Cân bằng độ nhạy và nhịp thở của AI lính gác (AI Balance & Tuning) mang lại cảm giác công bằng nhưng kịch tính; tích hợp toàn diện hạ tầng âm thanh (Audio Manager & SFX Presentation) gồm tiếng bước chân phân tầng theo dáng đi, âm thanh cảnh báo lính gác / stinger phát hiện, âm thanh tương tác cơ khí và nhạc nền ambient lén lút.

## Capacity
- Total days: 10 days (80h)
- Buffer (20%): 2 days (16h reserved for unplanned tuning / sound clip mixing)
- Available: 8 days (64h)
- Planned Load: 19h (~2.4 days)

## Tasks

### Must Have (Critical Path)
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `AUDIO-01` | Core Audio Infrastructure & Sound Manager | `engine-programmer` | 0.5d (4h) | None | AC-AUD-01..03 (Cài đặt AudioManager quản lý 4-bus hierarchy theo ADR-0003, tái sử dụng voice pool không rác GC, triệt tiêu âm thanh khi reset màn) | Complete |
| `AUDIO-02` | Player Dual-Voice Footstep Audio System | `gameplay-programmer` | 0.5d (4h) | `AUDIO-01` | AC-AUD-04..06 (Gắn âm thanh bước chân thực tế theo step_event của PlayerFootstepNoiseEmitter, điều chỉnh âm lượng & nhịp độ theo Crouch/Walk/Run) | Complete |
| `AUDIO-03` | Guard Awareness SFX & Alert Stingers | `gameplay-programmer` | 0.5d (4h) | `AUDIO-01` | AC-AUD-07..09 (Âm thanh lính ngờ vực '?', tiếng hô báo động bộ đàm radio bark, còi stinger dồn dập khi bị Chase, âm báo bị bắt kết thúc màn chơi) | Complete |
| `AI-TUNE-01` | AI Perception & Give-Up Pacing Tuning | `ai-programmer` | 0.5d (4h) | None | AC-TUNE-01..03 (Cân bằng góc quét FOV 100°->90°, tốc độ tích tụ nghi ngờ, khoảng thời gian tìm kiếm 4.0s trước khi give-up để tạo khe hở né tránh) | Complete |

### Should Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `AUDIO-04` | World Interaction & Ambient Audio | `level-designer` / `sound-designer` | 0.4d (3h) | `AUDIO-01` | AC-AUD-10..12 (Âm thanh nhặt Keycard, tiếng cơ khí cổng an ninh mở, tiếng ném & va chạm đạn mồi nhử Burst, nhạc nền ambient rù rì không khí rình rập) | Complete |

## Carryover from Previous Sprint
None (Sprint 05 hoàn thành 100% 5/5 stories, 258+ tests passing).

## Risks
| Risk | Probability | Impact | Mitigation | Owner |
|---|---|---|---|---|
| Thiếu tệp âm thanh thực tế (.wav/.ogg) trong project | Medium | Medium | Sử dụng synthesized audio procedural tone (AudioClip.Create) cho unit test và fallback, tích hợp file audio thực tế khi có asset | `engine-programmer` |
| Quá tải số lượng voice phát cùng lúc (Voice saturation) | Low | High | Cài đặt Voice Pooling với giới hạn số kênh tối đa (Max voices = 16) và cơ chế ưu tiên (Priority Voice Stealing) | `engine-programmer` |
| Âm thanh bước chân lặp lại đơn điệu | Low | Medium | Áp dụng pitch randomizer ngẫu nhiên ($\pm 5\%$) và micro-volume variation cho mỗi bước chân | `gameplay-programmer` |

## Definition of Done for this Sprint
- [x] Hoàn thành 100% 5 stories (`AUDIO-01`, `AUDIO-02`, `AUDIO-03`, `AUDIO-04`, `AI-TUNE-01`)
- [x] Toàn bộ các bài kiểm thử NUnit Unit & Integration mới vượt qua 100% (23 test mới: 7 AudioManager + 5 PlayerFootstep + 4 GuardAwareness + 4 WorldInteraction + 3 AIPerceptionTuning)
- [x] Tích hợp vào Scene 3D `CorePlayground.unity`:
  - Người chơi bước đi phát ra tiếng bước chân theo nhịp và dáng đi
  - Lính gác chuyển trạng thái phát ra âm thanh cảnh báo tương ứng
  - Nhặt Keycard, mở cổng, ném mồi nhử và hoàn thành/thất bại có âm thanh phản hồi sắc nét
  - Nhạc nền ambient chạy liên tục, tự động ducking khi vào trạng thái Chase
  - Nhịp điệu và độ khó AI cân bằng hợp lý, có thể vượt qua màn chơi bằng kỹ năng lén lút
- [x] Không có lỗi console hoặc rò rỉ bộ nhớ (Zero GC allocation trên audio hot-path)
- [x] Báo cáo QA Sign-Off và Retrospective Sprint 06 được tạo lập và phê duyệt
