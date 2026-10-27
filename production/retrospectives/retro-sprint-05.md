# Retrospective: Sprint 05 — Objective Mission Flow & Stealth Mastery Loop

> **Period**: 2026-10-04 to 2026-10-27  
> **Sprint**: Sprint 05 (`production/sprints/sprint-05.md`)  
> **Generated**: 2026-10-27  
> **Facilitator**: `producer` / `lead-programmer`  
> **Layer Focus**: Objective Keycard, Locked Security Gate, Extraction Zone, Mission Lifecycle Controller, Stealth Grade Operator, End-of-Mission Scorecard HUD  

---

## 1. Metrics & Completion Analysis

| Metric | Planned | Actual | Delta |
|---|---|---|---|
| **Stories / Tasks** | 5 stories (3 Must, 2 Should) | 5 stories | 0 (100% hoàn thành) |
| **Completion Rate** | 100% | 100% | 0% |
| **Estimated Effort** | 2.4 ngày (~19.0h) | ~2.0 ngày (~16.0h) | -0.4 ngày (đạt tiến độ) |
| **NUnit Test Cases mới** | ~20 tests | **34 tests** | +14 tests |
| **Tổng số Tests tích lũy** | 224 tests | **258+ tests** (358 test methods) | +34 tests |
| **Playtest Exceptions** | 0 | 0 | 0 (Clean Audit) |
| **Bugs Found / Open** | 3 tìm thấy | 3 đã giải quyết | 0 lỗi tồn đọng |
| **Unplanned Tasks Added** | 0 | 0 | 0 |

---

## 2. Velocity & Timeline Trend

| Sprint | Kế hoạch | Hoàn thành | Tỷ lệ | Nội dung trọng tâm |
|---|---|---|---|---|
| **Sprint 01** | 12 stories | 12 stories | 100% | Core Foundation, Locomotion C#, Camera Orbit, NavMesh Query |
| **Sprint 02** | 6 stories | 6 stories | 100% | 3D Level Playground, Prefabs, Telemetry HUD, Playable Prototype |
| **Sprint 03** | 6 stories | 6 stories | 100% | Stealth Gameplay Loop (Perception, Suspicion, FSM, Noise) |
| **Sprint 04** | 6 stories | 6 stories | 100% | Tactical Stealth & Coordinated Alert (Hide, Burst, Bark, HUD, Standoff, Markers) |
| **Sprint 05** | 5 stories | 5 stories | 100% | Playable Objective Loop & Stealth Grade System (Keycard, Gate, Extraction, Lifecycle, Scorecard) |

**Đánh giá xu hướng**: Dự án Whisper Ward đã hoàn thành 100% toàn bộ 35 stories xuyên suốt 5 sprint liên tiếp từ tuần 1 đến tuần 5 theo đúng timeline của `PLAN.md`. Toàn bộ tính năng cốt lõi (Core Mechanics) và vòng lặp gameplay hoàn chỉnh (Infiltrate -> Objective -> Escape -> Grade) đã sẵn sàng.

---

## 3. What Went Well (Điểm sáng nổi bật)

1. **Bộ chấm điểm Stealth Grade Operator chặt chẽ và nhất quán (`GRADE-01`):**
   - Đã hiện thực hóa trọn vẹn hợp đồng toán học D4..D8 trong GDD `suspicion-meter-grade.md`: phân tách rõ ràng giữa việc bị lính đuổi (Chase penalty -25đ/lần) và việc ném mồi nhử tạo tiếng ồn (Burst distraction) được miễn phạt hoàn toàn.
   - Điều kiện bắt buộc để đạt Rank S ($N_{\text{chase}} == 0$) tạo ra giá trị chơi lại (replayability) cực cao cho người chơi yêu thích phong cách lén lút tuyệt đối (Ghost Playstyle).
2. **Vòng đời nhiệm vụ mạch lạc, phi tập trung (`FLOW-01`):**
   - `MissionLifecycleController` phối hợp giữa trạng thái nhặt chìa khóa, mở cổng và vùng thoát hiểm qua Event Bus, triệt tiêu sự phụ thuộc vòng giữa các hệ thống lính gác AI và điều phối màn chơi.
3. **Thẻ điểm tổng kết End-of-Mission giàu thông tin và thẩm mỹ (`UI-RESULT-01`):**
   - Thiết kế UI phân cấp trực quan với huy hiệu Rank màu sắc chuẩn quy chuẩn thẩm mỹ, hiển thị chi tiết số lần bị phát hiện, điểm số tổng, thời gian hoàn thành nhiệm vụ và hướng dẫn bấm phím để chơi lại.
4. **Scene chơi thử 3D tích hợp hoàn chỉnh (`CorePlayground.unity`):**
   - Dựng sẵn màn chơi hoàn chỉnh bằng `CorePlaygroundBuilder.cs` với Spawn Point, 2 lính gác tuần tra có tầm nhìn/thính giác và báo động bầy đàn, bàn chứa Keycard, cửa an ninh chặn đường và ranh giới rút lui.

---

## 4. Key Insights & Lessons Learned (Bài học kinh nghiệm)

1. **Khả năng tương thích giữa các gói Package mới của Unity 6:**
   - Khi chuyển hoàn toàn sang New Input System, các đoạn mã debug cũ gọi `Input.GetKeyDown` sẽ quăng Exception ở runtime. Cần bọc tiền xử lý `#if ENABLE_INPUT_SYSTEM` hoặc dùng `Keyboard.current` để đảm bảo game chạy mượt mà trên mọi môi trường build.
2. **Cập nhật liên tục giữa cảm biến và máy đo nghi ngờ:**
   - Bộ tích phân nghi ngờ (`SuspicionAccumulator`) cần được bơm nhịp `Tick(deltaTime)` đều đặn trong FSM của lính gác dựa trên trạng thái LOS của cảm biến, đảm bảo thanh đo nghi ngờ phản ứng tức thì khi người chơi bước vào vùng sáng.

---

## 5. Technical Debt Status

- **Số lượng TODO / FIXME / HACK**: **0** trên toàn bộ mã nguồn `src/`.
- **Nợ kiến trúc**: Không có.
- **Tình trạng kiểm thử**: 358 test methods bao phủ toàn bộ logic, công thức và tích hợp.

---

## 6. Action Items for Next Phase (Polish Phase — Sprints 06 & 07)

1. **Chuyển giao giai đoạn phát triển**: Chính thức cập nhật trạng thái dự án sang **Polish** (Tuần 6 & Tuần 7 theo `PLAN.md`).
2. **Sprint 06 (Polish AI & Audio Design)**:
   - Tinh chỉnh cân bằng độ nhạy của AI (góc nhìn, tốc độ tích tụ nghi ngờ, thời gian tìm kiếm trước khi bỏ cuộc).
   - Thiết kế và gắn hệ thống âm thanh (Audio/SFX): tiếng bước chân (đi/chạy/rón rén), tiếng hô báo động radio của lính, tiếng còi stinger khi bị phát hiện, tiếng mở cổng cơ khí, âm thanh nhặt thẻ và nhạc nền ambient lén lút.
3. **Sprint 07 (Visual Polish & UX)**:
   - Thiết lập Lighting & URP Post-processing Volume, hoàn thiện Animation blend tree, và menu chuyển cảnh mượt mà.
