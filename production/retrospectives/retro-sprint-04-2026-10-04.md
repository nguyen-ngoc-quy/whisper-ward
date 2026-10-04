# Retrospective: Sprint 04 — Tactical Stealth & Coordinated Alert

> **Period**: 2026-10-04 to 2026-10-04  
> **Sprint**: Sprint 04 (`production/sprints/sprint-04.md`)  
> **Generated**: 2026-10-04  
> **Facilitator**: `producer` / `ai-programmer`  
> **Layer Focus**: Hide Spots, Burst Distraction, Coordinated Alert & Radio Bark, Suspicion Meter HUD, Search Standoff, Overhead Markers  

---

## 1. Metrics & Completion Analysis

| Metric | Planned | Actual | Delta |
|---|---|---|---|
| **Stories / Tasks** | 6 stories (3 Must, 2 Should, 1 Nice) | 6 stories | 0 (100% hoàn thành) |
| **Completion Rate** | 100% | 100% | 0% |
| **Estimated Effort** | 2.6 ngày (~21.0h) | ~1.5 ngày (~12.0h) | -1.1 ngày (vượt tiến độ) |
| **NUnit Test Cases mới** | ~30 tests | **58 tests** (47 Unit + 11 Integration) | +28 tests |
| **Tổng số Tests tích lũy** | 166 tests | **224 tests** | +58 tests |
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
| **Sprint 04** | 6 stories | 6 stories | 100% | Tactical Stealth & Coordinated Alert (Hide, Burst, Bark, HUD, Standoff, Markers) |

**Đánh giá xu hướng**: Duy trì tỷ lệ hoàn thành 100% qua 4 sprint liên tiếp. Kiến trúc hướng sự kiện (Event-driven) và kiểm thử Unit/Integration không phụ thuộc vào cảnh thực tế (Mockable Services) cho phép xây dựng các hệ thống AI lén lút nhiều lớp với độ tin cậy tuyệt đối.

---

## 3. What Went Well (Điểm sáng nổi bật)

1. **Khử hoàn toàn điểm kỳ dị lật góc của Camera khi vẽ Chevron 360° (`UI-SUSP-01`):**
   - Thay vì dùng `Camera.WorldToScreenPoint` dễ bị lật tọa độ khi kẻ địch ở sau lưng camera ($z_{\text{cam}} < 0$), hệ thống chiếu tọa độ camera-local và dùng $\text{atan2}(x_{\text{cam}}, z_{\text{cam}})$ để ánh xạ trơn tru lên hình elip màn hình ($R_x = 240\text{ px}, R_y = 160\text{ px}$), đảm bảo trải nghiệm trực quan hoàn hảo cho người chơi.
2. **Loại bỏ hiện tượng kẹt cụm lính gác khi tìm kiếm chung (`ALERT-02`):**
   - Thuật toán `CalculateStandoffPosition` tự động phân tách các lính gác tham gia tìm kiếm một điểm LKP thành các vị trí dừng chân cách nhau $\ge 2.0\text{ m}$ và phân bổ góc quét phân kỳ ($180^\circ$ đối diện nhau), vừa loại bỏ lỗi kẹt NavMesh vừa bao quát toàn diện khu vực.
3. **Mô phỏng quỹ đạo ném kết hợp lời giải giải tích chính xác (`NOISE-02`):**
   - Bộ giải giải tích bậc hai (`SolveAnalyticalLanding`) tính trước điểm tiếp đất chính xác trong $0\text{ ms}$, đồng thời bộ mô phỏng từng bước (`TickFlight`) thực hiện va chạm vật lý mượt mà và kiểm tra điều kiện đứng yên khi ném.
4. **Vòng đời hốc nấp công bằng và chân thực (`HIDE-01`):**
   - Ẩn nấp trước khi bị phát hiện đem lại sự an toàn tuyệt đối (miễn nhiễm nón quét thị giác); lặn vào hốc nấp khi đang bị đuổi theo trở thành bẫy tử thần khi lính gác chạy thẳng tới cửa tủ, đứng kiểm tra $1.5\text{ s}$ và bắt giữ.
5. **Chuẩn hiệu năng không cấp phát rác trên Hot-Path (Zero GC):**
   - Mọi cấu trúc dữ liệu truyền nhận sự kiện (`GuardAlertEvent`, `GuardThreatCandidate`, `SuspicionHUDState`, `SearchAssignment`) đều sử dụng `struct` giá trị và mảng cố định định sẵn, không phát sinh bất kỳ byte GC rác nào trong vòng lặp game.

---

## 4. Key Insights & Lessons Learned (Bài học kinh nghiệm)

1. **Hiển thị thông tin trực quan nhưng bảo vệ cảm giác lén lút (Quiet Region):**
   - Khi kẻ địch chưa nghi ngờ ($A = 0.0$), việc ẩn toàn bộ HUD và mũi tên chevron là tối quan trọng để tránh biến giao diện thành "radar nhìn xuyên tường" làm hỏng tính thử thách lén lút.
2. **Khai báo phân lớp rõ ràng giữa Core và Presentation:**
   - FSM và AI chỉ phát các sự kiện quyết định trạng thái (`OnStateChanged`, `OnTelegraphTriggered`), tầng giao diện và hiệu ứng (`GuardOverheadMarkerController`, `SuspicionMeterHUDController`) độc lập nhận diện và thể hiện ra thế giới mà không can thiệp ngược lại logic AI.

---

## 5. Technical Debt Status

- **Số lượng TODO / FIXME / HACK**: **0** (quét sạch trên toàn bộ `src/`, `Assets/`, `tests/`).
- **Nợ kiến trúc**: Không có.
- **Xu hướng**: Chất lượng mã nguồn đạt chuẩn cao nhất của studio.

---

## 6. Action Items for Next Sprint (Sprint 05)

1. **Sprint 05 Focus**: Tích hợp màn chơi hoàn chỉnh, chuỗi nhiệm vụ màn chơi theo kịch bản (Objective Flow & Keycard Door Gates), và hệ thống chấm điểm đánh giá lén lút (Stealth Grade Operator System).
2. **Chuẩn bị tích hợp âm thanh SFX**: Gắn các tệp âm thanh thực tế vào các sự kiện telegraph (Radio bark alert sting, Footstep audio, Burst impact clang).
