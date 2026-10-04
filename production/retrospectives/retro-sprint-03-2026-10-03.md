# Retrospective: Sprint 03 — Stealth Gameplay Loop & Perception Engine

> **Period**: 2026-10-03 to 2026-10-03  
> **Sprint**: Sprint 03 (`production/sprints/sprint-03.md`)  
> **Generated**: 2026-10-03  
> **Facilitator**: `producer` / `ai-programmer`  
> **Layer Focus**: Vision Cone Sensor, Suspicion Accumulator, Cone Visualizer, Guard FSM, Search Dwell Give-up, Footstep Noise Propagation  

---

## 1. Metrics & Completion Analysis

| Metric | Planned | Actual | Delta |
|---|---|---|---|
| **Stories / Tasks** | 6 stories (4 Must, 2 Should) | 6 stories | 0 (100% hoàn thành) |
| **Completion Rate** | 100% | 100% | 0% |
| **Estimated Effort** | 2.8 ngày (~22.0h) | ~1.5 ngày (~12.0h) | -1.3 ngày (vượt tiến độ) |
| **NUnit Test Cases mới** | ~30 tests | **51 tests** (46 Unit + 5 Integration) | +21 tests |
| **Tổng số Tests tích lũy** | 115 tests | **166 tests** | +51 tests |
| **Playtest Exceptions** | 0 | 0 | 0 (Clean Audit) |
| **Bugs Found / Open** | 0 | 0 | 0 |
| **Unplanned Tasks Added** | 0 | 0 | 0 |

---

## 2. Velocity & Timeline Trend

| Sprint | Kế hoạch | Hoàn thành | Tỷ lệ | Nội dung trọng tâm |
|---|---|---|---|---|
| **Sprint 01** | 12 stories | 12 stories | 100% | Core Foundation, Locomotion C#, Camera Orbit, NavMesh Query |
| **Sprint 02** | 6 stories | 6 stories | 100% | 3D Level Playground, Prefabs, Telemetry HUD, Playable Prototype |
| **Sprint 03** | 6 stories | 6 stories | 100% | Stealth Gameplay Loop (Perception, Suspicion, FSM, Noise) |

**Đánh giá xu hướng**: Ổn định ở mức tối đa (100% qua 3 sprint liên tiếp). Việc kiểm thử trước bằng Unit/Integration Test giúp các tính năng AI lén lút phức tạp được ghép nối mượt mà vào Scene `CorePlayground.unity` mà không phát sinh xung đột logic hay regression.

---

## 3. What Went Well (Điểm sáng nổi bật)

1. **Khử phụ thuộc hoàn toàn với `IPhysicsQueryService` (Headless Testability):**
   - Cả `VisionConeSensor`, `VisionConeVisualizer`, và `GuardHearingSensor` đều được tiêm phụ thuộc dịch vụ vật lý, cho phép 51 bài test NUnit chạy ngầm (headless) kiểm tra chính xác từng tình huống cản tầm nhìn/cản âm thanh qua tường Layer 20 `World` mà không cần nạp cảnh Unity thực tế.
2. **Tuân thủ tuyệt đối chuẩn Hot-path Zero Allocation (Zero GC):**
   - Vòng lặp quét thị giác 5 Hz ($200\text{ ms}$) và bộ phát tán nhịp bước chân (`PlayerFootstepNoiseEmitter`) không hề cấp phát bộ nhớ rác (0 B managed heap allocations), bảo vệ ngân sách 60 FPS trên PC và WebGL.
3. **Đầy đủ 100% các công thức GDD Toán học AI:**
   - Tích luỹ nghi ngờ nghịch đảo khoảng cách $dA/dt = \min(1.5/d, 0.60)$, cảnh giác dư thừa phân rã hàm mũ $e^{-\Delta t / 16.0}$, hạ ngưỡng điều tra $T_{entry}(R) = \max(0.30 - 0.24R, 0.06)$, cửa sổ xác nhận $1.2\text{ s}$, và cơ chế ép điều tra khi lén ngó $\ge 3$ lần.
4. **Vòng lặp tương tác khép kín hoàn chỉnh:**
   - Khi bước chân tạo tiếng ồn $\to$ Lính gác chuyển sang Điều tra $\to$ Đến nơi tìm kiếm $\pm 45^\circ$ trong $4\text{ s} \to$ Bỏ cuộc quay về tuần tra với cảnh giác tăng thêm $+0.15$. Nếu phát hiện người chơi $\to$ Tích luỹ đầy nghi ngờ $\to$ Chuyển sang Đuổi bắt $\to$ Bắt giữ sau $1\text{ s}$ áp sát.

---

## 4. What Went Poorly & Key Insights (Bài học kinh nghiệm)

1. **Quản lý phân tách Assembly Definition (`.asmdef`):**
   - Hệ thống AI cần tham chiếu tới Player Emitter trong Core Assembly (`WhisperWard.Core`). Cần duy trì quy tắc: Core phát tín hiệu / Interface, AI lắng nghe để tránh phụ thuộc vòng (circular dependency).
2. **Tránh trùng lặp thuộc tính C# khi refactor FSM:**
   - Khi mở rộng `GuardFSMRuntimeController` từ các Story trước, cần rà soát các property đã khai báo để tránh lỗi trùng lặp thuộc tính (duplicate property definitions) giữa các bản vá.

---

## 5. Technical Debt Status

- **Số lượng TODO / FIXME / HACK**: **0** (quét toàn bộ `src/`, `Assets/`, `tests/`).
- **Nợ kiến trúc**: Không có. Mọi hằng số gameplay đều được cấu hình hóa qua trường dữ liệu.
- **Xu hướng**: Hoàn toàn sạch sẽ.

---

## 6. Previous Action Items Follow-Up (Sprint 02)

| Mục tiêu từ Sprint 02 | Trạng thái | Đánh giá |
|---|:---:|---|
| **ACT-01: Triển khai Guard AI Perception & FSM** | **DONE** | Hoàn thành toàn bộ `VisionConeSensor`, `SuspicionAccumulator`, `GuardFSMRuntimeController`. |
| **ACT-02: Triển khai Hệ thống Tiếng ồn Bước chân** | **DONE** | Hoàn thành `PlayerFootstepNoiseEmitter` và `GuardHearingSensor`. |
| **ACT-03: Đóng Sprint 02 và cập nhật tài liệu** | **DONE** | Đã lưu tài liệu và nghiệm thu. |

---

## 7. Action Items for Next Iteration (Sprint 04)

| # | Hành động cụ thể | Người phụ trách | Ưu tiên | Hạn chót |
|:---:|---|:---:|:---:|:---:|
| **ACT-01** | **Khởi tạo Sprint 04 — Coordinated Alert & Investigation Network**: Triển khai cơ chế lính gác hú còi/báo động bằng bộ đàm (Radio Bark), lan truyền trạng thái cảnh giác cho các lính gác lân cận trong khu vực. | `ai-programmer` | **P0 (High)** | Sprint 04 |
| **ACT-02** | **Cơ chế Vật thể Gây xao nhãng (Distraction Prop / Sound Distraction)**: Cho phép người chơi ném chai/lon tạo điểm âm thanh thu hút lính gác rời vị trí canh gác. | `gameplay-programmer` | **P0 (High)** | Sprint 04 |
| **ACT-03** | **Cấu trúc Cây hành vi (Behavior Tree Framework)**: Chuyển đổi và tích hợp FSM hiện tại vào hệ thống Node Selector/Sequence/Decorator để hỗ trợ hành vi tìm kiếm tổ đội phức tạp. | `ai-programmer` / `technical-director` | **P1 (Normal)** | Sprint 04 |

---

## 8. Summary

Sprint 03 đã hiện thực hóa thành công trái tim của Whisper Ward: hệ thống Stealth Loop hoàn chỉnh với đầy đủ giác quan (Thị giác 100° FOV, Thính giác bán kính bước chân, Đo đạc nghi ngờ, và Máy trạng thái tuần tra/truy đuổi/bắt giữ). Với 51 bài test mới được bổ sung và 0 lỗi nợ kỹ thuật, dự án đã sẵn sàng bước sang Sprint 04 để nâng tầm AI thành mạng lưới phối hợp nhóm (Coordinated Alert).
