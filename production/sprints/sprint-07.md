# Sprint 07 — 2026-11-10 to 2026-11-24 (Weeks 7 & 8)

## Sprint Goal
Hoàn thiện mỹ thuật và ánh sáng Cold Watch (Visual Polish & URP Post-Processing), điều phối giao diện tạm dừng/menu (Pause Menu Controller), kiểm chuẩn ngân sách hiệu năng (Performance Budget 60 FPS / Draw Calls ceiling), tự động hóa đóng gói bản build WebGL/Windows Standalone, và hoàn tất tài liệu kỹ thuật Portfolio Showcase khẳng định năng lực tự viết AI và hệ thống game 3D.

## Capacity
- Total days: 10 days (80h)
- Buffer (20%): 2 days (16h reserved for build platform edge cases / asset compression)
- Available: 8 days (64h)
- Planned Load: 18h (~2.3 days)

## Tasks

### Must Have (Critical Path)
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `UI-PAUSE-01` | Pause & In-Game Menu Controller | `ui-programmer` | 0.5d (4h) | None | AC-PAUSE-01..03 (Bật/tắt Pause với phím Escape, quản lý Time.timeScale, Restart màn chơi và Resume mượt mà không rò rỉ input) | Complete |
| `VFX-POST-01` | Cold Watch URP Post-Processing & Lighting Profile | `technical-artist` | 0.5d (4h) | None | AC-POST-01..03 (Volume Profile thiết lập Vignette viền tối, Color Grading tông lạnh nguy hiểm #A0C4E2, Bloom nhẹ cho đèn báo động) | Complete |
| `PERF-01` | Performance Budget & Frame Time Validator | `performance-analyst` | 0.5d (4h) | None | AC-PERF-01..03 (Kiểm tra ngân sách 60 FPS / 16.6ms, giới hạn Draw Calls <= 1000 PC / <= 300 WebGL, Zero GC trên hot-path) | Complete |
| `BUILD-01` | Release Build Automation & Platform Packaging | `devops-engineer` | 0.4d (3h) | None | AC-BUILD-01..03 (Script build tự động cho Windows x64 và WebGL, thiết lập nén asset và cấu hình player settings) | Complete |

### Should Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria | Status |
|---|---|---|---|---|---|---|
| `DOCS-PORTFOLIO-01` | Technical Architecture Showcase & Release Portfolio Guide | `technical-director` | 0.4d (3h) | All | AC-PORT-01..03 (Tài liệu kiến trúc tổng hợp AI, Audio, Math, sơ đồ hệ thống, hướng dẫn chơi và điểm sáng portfolio) | Complete |

## Carryover from Previous Sprint
None (Sprint 06 hoàn thành 100% 5/5 stories, 281 tests passing).

## Definition of Done for this Sprint
- [x] Hoàn thành 100% 5 stories (`UI-PAUSE-01`, `VFX-POST-01`, `PERF-01`, `BUILD-01`, `DOCS-PORTFOLIO-01`)
- [x] Toàn bộ các bài kiểm thử NUnit Unit & Verification mới vượt qua 100% (15 tests mới: 4 PauseMenu + 3 PostProcess + 5 PerformanceBudget + 3 ReleaseBuild; tổng suite toàn dự án đạt 296 tests!)
- [x] Tích hợp vào Scene 3D `CorePlayground.unity`:
  - Nhấn Escape hiển thị menu Pause, tạm dừng thời gian thế giới, cho phép Tiếp tục hoặc Chơi lại
  - Cảnh chơi hiển thị chuẩn tông màu lạnh bí ẩn đặc trưng của Whisper Ward với hiệu ứng viền tối Vignette
  - Bộ kiểm tra hiệu năng chứng minh game đạt chuẩn 60 FPS, draw calls tối ưu
- [x] Pipeline đóng gói Release Build sẵn sàng cho Windows Standalone & WebGL
- [x] Báo cáo QA Sign-Off và Retrospective Sprint 07 được tạo lập và phê duyệt
