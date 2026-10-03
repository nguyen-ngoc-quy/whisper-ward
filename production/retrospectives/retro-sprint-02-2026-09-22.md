# Retrospective: Sprint 02 — 3D Level Playground & Playable Prototype

> **Period**: 2026-09-22 to 2026-09-22  
> **Sprint**: Sprint 02 (`production/sprints/sprint-02.md`)  
> **Generated**: 2026-09-22  
> **Facilitator**: `producer`  
> **Layer Focus**: Level Playground (`Assets/Scenes/CorePlayground.unity`), Input Driver, Cinemachine Orbit Rig, NavMesh NPC, Telemetry HUD  

---

## 1. Metrics & Completion Analysis

| Metric | Planned | Actual | Delta |
|---|---|---|---|
| **Stories / Tasks** | 6 stories | 6 stories | 0 (100% completed) |
| **Completion Rate** | 100% | 100% | 0% |
| **Estimated Effort** | 2.6 days (~21.0h) | ~1.5 days (~12.0h) | -1.1 days (vượt tiến độ) |
| **NUnit Test Cases** | ~20 tests | 26 tests (+115 total) | +6 tests |
| **Playtest Exceptions** | 0 | 0 | 0 (Clean Audit) |
| **Bugs Found / Open** | 0 | 0 | 0 |
| **Core Commits** | -- | 5 commits | Clean |

---

## 2. Velocity & Timeline Trend

Sprint 02 hoàn thành xuất sắc mục tiêu chuyển hóa 100% toán học C# logic từ Sprint 1 thành một **sản phẩm chơi thử 3D thực tế (Playable 3D Prototype)** hoàn chỉnh:
- Giải quyết dứt điểm bài học kinh nghiệm **ACT-01** từ Sprint 1: Tạo xong toàn bộ Scene 3D tương tác (`Assets/Scenes/CorePlayground.unity`), Prefab `Player_Capsule.prefab`, Prefab `Guard_PatrolNPC.prefab`, và Cinemachine Orbit Rig.
- Toàn bộ 4 stories **Must Have**, 1 story **Should Have**, và 1 story **Nice to Have** đều đạt 100% tiêu chí nghiệm thu.
- Tốc độ thực hiện nhanh nhờ công cụ biên tập tự động `CorePlaygroundBuilder.cs`, cho phép tái tạo hoặc hiệu chỉnh toàn bộ cảnh 3D chỉ với 1 cú click chuột trong Unity Editor (`Whisper Ward/Build CorePlayground Scene`).

---

## 3. What Went Well (Điểm sáng)

1. **Công cụ sinh tự động Cảnh & Prefab (Editor Builder Automation):**
   - Lập trình `CorePlaygroundBuilder.cs` tạo sẵn toàn bộ hình học E20, vật liệu tương phản, gán Tag/Layer, thiết lập Camera Orbit, và sinh Prefab chuẩn xác không qua thao tác kéo thả thủ công dễ sinh lỗi.
2. **Kiến trúc Driver hướng kiểm thử Headless (Zero Hardware Coupling):**
   - Các MonoBehaviours (`PlayerRuntimeDriver`, `CameraOrbitDriver`, `SimplePatrolDriver`, `DebugLocomotionHUD`) đều sở hữu phương thức `Configure()` và các hàm inject input (`InjectInput`, `InjectLookDelta`), cho phép NUnit kiểm thử 100% tính năng trong tích tắc mà không cần cắm phần cứng bàn phím/chuột.
3. **Hiển thị trực quan Telemetry On-Screen (Debug HUD):**
   - `DebugLocomotionHUD.cs` cung cấp bảng chẩn đoán tức thời tốc độ, chiều cao capsule, trạng thái cản trần hốc nấp (`BLOCKED` vs `CLEAR`), cự ly camera và FOV, tạo điều kiện thuận lợi cho việc playtest và căn chỉnh sau này.
4. **Văn hóa chất lượng không ngoại lệ (0 Exceptions Policy):**
   - Phiên chơi thử 5 phút liên tục đạt chuẩn Clean Audit: 0 lỗi console, 0 NullReferenceException, duy trì ổn định 60 FPS ($16.6\text{ ms}$).

---

## 4. What Went Poorly & Key Insights (Bài học kinh nghiệm)

1. **Cần đồng bộ mã nguồn giữa `src/` và `Assets/` để tránh lỗi biên dịch Unity:**
   - Trong quá trình phát triển, các file C# mới cần được đồng bộ song song vào thư mục `Assets/WhisperWard*` kèm assembly definitions có cờ `"autoReferenced": true` để Editor scripts và Scene scripts nhận diện tức thời.
2. **Cần quản lý thống nhất bảng Layer Tag trong `TagManager.asset`:**
   - Các Layer quan trọng như Layer 20 `World`, Layer 6 `Player`, Layer 7 `Guard`, Layer 10 `HideSpotTrigger` cần được đăng ký sẵn trong `ProjectSettings/TagManager.asset` để tránh phụ thuộc vào tên chuỗi động lúc khởi động.

---

## 5. Technical Debt Status

- **TODO / FIXME / HACK Count**: **0** (quét toàn bộ `src/`, `Assets/`, và `tests/`).
- **Nợ kỹ thuật ghi nhận**: Không có nợ kỹ thuật tồn đọng.
- **Xu hướng nợ kỹ thuật**: Ổn định và sạch sẽ.

---

## 6. Action Items for Next Iteration (Kế hoạch hành động)

| # | Hành động cụ thể | Người phụ trách | Ưu tiên | Hạn chót |
|:---:|---|:---:|:---:|:---:|
| **ACT-01** | **Khởi tạo Sprint 3: Guard AI Perception & Behavior Tree FSM**: Triển khai nón thị giác lính gác (Perception Cone: góc $90^\circ$, tầm xa $12\text{ m}$), nón cảnh giác ngoại vi, và máy trạng thái hành vi lính gác (Patrol $\to$ Suspicious $\to$ Alert $\to$ Search $\to$ Chase). | `ai-programmer` / `producer` | **P0 (High)** | Đầu Sprint 3 |
| **ACT-02** | **Triển khai Hệ thống Phát tán Tiếng động (Player Noise Propagation)**: Tích hợp sự kiện phát tán âm thanh bước chân (Đi bộ, Chạy nhanh, Cúi người) dựa trên GDD #02 đã được duyệt. | `gameplay-programmer` / `audio-director` | **P0 (High)** | Sprint 3 |
| **ACT-03** | **Cập nhật trạng thái Sprint 2 sang Closed** và chuẩn bị Phase Gate review. | `producer` | **P1 (Normal)** | Ngay lập tức |
