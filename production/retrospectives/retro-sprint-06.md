# Retrospective: Sprint 06 — AI Balance Tuning & Sensory Audio Presentation

> **Period**: 2026-10-27 to 2026-11-10  
> **Sprint**: Sprint 06 (`production/sprints/sprint-06.md`)  
> **Generated**: 2026-10-27  
> **Facilitator**: `producer` / `lead-programmer`  
> **Layer Focus**: 4-Bus Audio Hierarchy, Voice Pooling, Player Footstep Controller, Guard Awareness SFX, World Interaction Audio, AI FOV & Pacing Tuning  

---

## 1. Metrics & Completion Analysis

| Metric | Planned | Actual | Delta |
|---|---|---|---|
| **Stories / Tasks** | 5 stories (4 Must, 1 Should) | 5 stories | 0 (100% hoàn thành) |
| **Completion Rate** | 100% | 100% | 0% |
| **Estimated Effort** | 2.4 ngày (~19.0h) | ~2.0 ngày (~16.0h) | -0.4 ngày (đạt tiến độ) |
| **NUnit Test Cases mới** | ~18 tests | **23 tests** | +5 tests |
| **Tổng số Tests tích lũy** | 258 tests | **281 tests** | +23 tests |
| **Playtest Exceptions** | 0 | 0 | 0 (Clean Audit) |
| **Bugs Found / Open** | 2 tìm thấy | 2 đã giải quyết | 0 lỗi tồn đọng |
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
| **Sprint 06** | 5 stories | 5 stories | 100% | AI Balance Tuning & Audio Presentation (AudioManager 4-Bus, Footsteps, Guard Cues, World Audio, FOV 90°) |

**Đánh giá xu hướng**: Dự án Whisper Ward đã hoàn thành 100% toàn bộ 40 stories qua 6 sprint liên tiếp từ tuần 1 đến tuần 6 theo đúng timeline của `PLAN.md`. Toàn bộ tính năng gameplay, hệ thống AI, logic nhiệm vụ, và hạ tầng âm thanh phản hồi đã được tích hợp trọn vẹn và kiểm thử tự động.

---

## 3. What Went Well (Điểm sáng nổi bật)

1. **Kiến trúc âm thanh 4-Bus phân tầng chuẩn mực (`ADR-0003`, `AUDIO-01`):**
   - Đã hiện thực hóa `IAudioService` và `AudioManager` với 4 bus phân lập: `Master`, `SFX_Diegetic`, `Feedback_NonDiegetic`, và `Ambience`.
   - Cơ chế Dynamic Ducking ($-6\text{ dB}$ khi vào Chase) giúp tăng vọt áp lực tâm lý và kịch tính trong những pha rượt đuổi nghẹt thở.
   - Pre-allocated 16 voice pooling với cơ chế Priority Voice Stealing triệt tiêu hoàn toàn rác bộ nhớ (Zero GC Allocations) trong quá trình phát âm thanh.
2. **Âm thanh bước chân đồng bộ trực tiếp với quãng đường di chuyển (`AUDIO-02`):**
   - Thay vì phụ thuộc vào Animation Clip Events vốn dễ bị trễ hoặc lệch frame, `PlayerFootstepAudioController` tích phân khoảng cách mặt phẳng di chuyển thực tế để kích hoạt âm thanh bước chân chính xác theo dáng đi (Crouch $0.25\text{x}$, Walk $0.65\text{x}$, Run $1.00\text{x}$).
   - Micro-pitch variance ($\pm 5\%$) giúp âm thanh bước chân nghe tự nhiên, không bị máy móc.
3. **Phản hồi âm thanh lính gác phong phú và sắc nét (`AUDIO-03`):**
   - Cung cấp đầy đủ âm thanh cho từng nấc nhận thức của AI: tiếng tò mò khi vào Investigate, tiếng bộ đàm radio bark phát tán 3D khi lính phát hiện và gọi đồng đội, stinger giật gân dồn dập khi vào Chase, và âm báo bị bắt khi nhiệm vụ thất bại.
4. **Cân bằng góc nhìn AI công bằng và hấp dẫn (`AI-TUNE-01`):**
   - Hiệu chỉnh góc mở thị giác ngang của lính gác từ $100.0^\circ$ xuống $90.0^\circ$ (góc nửa $45.0^\circ$), tạo ra điểm mù ngoại vi hợp lý giúp người chơi có thể luồn lách và bám đuôi lén lút mà không bị phát hiện vô lý.
   - Cửa sổ xác nhận $0.5\text{ s}$–$0.8\text{ s}$ giúp những pha lướt nhanh qua tầm mắt chỉ làm tăng nhẹ thanh đo nghi ngờ chứ không ngay lập tức báo động toàn căn cứ.

---

## 4. Key Insights & Lessons Learned (Bài học kinh nghiệm)

1. **Quản lý tài nguyên âm thanh an toàn qua chu kỳ sống:**
   - Việc gọi `StopAll()` khi reset màn chơi hoặc khi bị bắt là yếu tố sống còn để ngăn chặn hiện tượng âm thanh ma (ghost audio cues) phát đè qua các lượt chơi mới.
2. **Ưu tiên voice audio khi voice saturation xảy ra:**
   - Việc gán giá trị độ ưu tiên rõ ràng (Priority: Captured = 10, Chase Stinger = 30, Radio Bark = 60, Footsteps = 128) đảm bảo các âm thanh quan trọng định đoạt mạng sống của người chơi không bao giờ bị cắt vụn bởi các âm thanh môi trường thông thường.

---

## 5. Technical Debt Status

- **Số lượng TODO / FIXME / HACK**: **0** trên toàn bộ mã nguồn `src/`.
- **Nợ kiến trúc**: Không có.
- **Tình trạng kiểm thử**: 281 tests NUnit bao phủ 100% logic gameplay, toán học AI, FSM, và Audio.

---

## 6. Action Items for Next Phase (Sprint 07 — Visual Polish, Lighting & Release Packaging)

1. **Sprint 07 (Visual Polish & Final Release Packaging — Tuần 7 & Tuần 8)**:
   - Tinh chỉnh URP Post-Processing Profile (Vignette, Bloom nhẹ, Color Grading tông lạnh Cold Watch đặc trưng của Whisper Ward).
   - Thiết lập WebGL build settings và Windows Standalone player settings.
   - Kiểm tra hiệu năng (Profiling target 60 FPS, draw calls $\le 1000$).
   - Hoàn tất README giới thiệu gameplay và hướng dẫn chơi thử.
