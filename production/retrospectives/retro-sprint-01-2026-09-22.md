# Retrospective: Sprint 01 — Core Layer Execution

> **Period**: 2026-09-21 to 2026-09-22  
> **Sprint**: Sprint 01 (`production/sprints/sprint-01.md`)  
> **Generated**: 2026-09-22  
> **Facilitator**: `producer`  
> **Layer Focus**: Core Layer (Player Controller, NavMesh Pathfinding, Camera Cinemachine)  

---

## 1. Metrics & Completion Analysis

| Metric | Planned | Actual | Delta |
|---|---|---|---|
| **Stories / Tasks** | 9 stories | 9 stories | 0 (100% completed) |
| **Completion Rate** | 100% | 100% | 0% |
| **Estimated Effort** | 4.3 days (~34.4h) | ~2.0 days (~16.0h) | -2.3 days (vượt tiến độ) |
| **NUnit Test Cases** | ~40 tests | 89 tests | +49 tests |
| **Bugs Found / Open** | 0 | 0 | 0 |
| **Unplanned Tasks Added**| 0 | 0 | 0 |
| **Core Commits** | -- | 6 commits | 6 commits clean |

---

## 2. Velocity & Timeline Trend

Sprint 01 là sprint kỹ thuật đầu tiên trong giai đoạn **Pre-Production** của dự án *Whisper Ward*.
- Toàn bộ 7 stories **Must Have** và 2 stories **Should Have** đều được đóng sạch sẽ với đầy đủ NUnit test.
- Tốc độ hoàn thành vượt kế hoạch nhờ việc áp dụng mô hình phân tách interface contract (`src/Core/Contracts/`) và kiểm thử cô lập bằng delegates.

---

## 3. What Went Well (Điểm sáng)

1. **Mô hình lập trình hướng kiểm thử (TDD) chặt chẽ:**
   - Mỗi Story đều được viết kèm 1 test suite NUnit hoàn chỉnh (89 tests toàn sprint).
   - Kiểm thử được toàn bộ các ca góc (edge cases): góc quay $180^\circ$, suy biến pitch camera $\pm 89.9^\circ$, choke point hành lang $< 1.20\text{ m}$, và va chạm trần khi đứng lên.
2. **Kỹ thuật Headless Test Delegates hữu hiệu:**
   - Sử dụng các delegates (`SphereCastNonAllocDelegate`, `RaycastDistanceDelegate`, `TimeScaleProvider`) cho phép kiểm thử toàn bộ toán học 3D phức tạp mà không cần nạp asset cảnh nặng nề của Unity.
3. **Kỷ luật bộ nhớ 0 B GC trên Hot-Path:**
   - 100% các vòng lặp cập nhật vận tốc nhân vật, tính cự ly NavMesh và deocclusion camera đều pre-allocate mảng cố định, đạt chuẩn hiệu năng cao cho WebGL và PC 60 fps.
4. **Trunk-Based Git Workflow gọn gàng:**
   - 6 commits sạch sẽ tuân thủ tuyệt đối quy ước Conventional Commits, dễ dàng truy vết và quản lý lịch sử.

---

## 4. What Went Poorly & Key Insights (Bài học kinh nghiệm)

1. **Khoảng cách giữa "Logic C# thuần" và "Trải nghiệm game thực tế":**
   - Mặc dù code C# và test logic đã chạy tốt 100%, dự án vẫn **chưa có Scene 3D thực tế trong Unity** (chưa có sàn nhà, chưa có Prefab nhân vật Capsule, chưa có Cinemachine Brain thực tế). Người phát triển chưa thể trực tiếp vào game để điều khiển và cảm nhận bằng tay.
2. **Quy trình Smoke-Check bị máy móc:**
   - Công cụ kiểm tra khói tiêu chuẩn đưa ra các câu hỏi về crash màn hình menu, FPS tụt trong khi dự án ở giai đoạn Core Layer chưa xây dựng scene UI/Gameplay. Quy trình kiểm thử cần phản ánh sát thực trạng từng sprint.

---

## 5. Technical Debt Status

- **TODO / FIXME / HACK Count**: **0** (quét toàn bộ `src/` và `tests/`).
- **Nợ kỹ thuật ghi nhận**: Không có nợ kỹ thuật tồn đọng.
- **Xu hướng nợ kỹ thuật**: Ổn định và sạch sẽ.

---

## 6. Action Items for Next Iteration (Kế hoạch hành động)

| # | Hành động cụ thể | Người phụ trách | Ưu tiên | Hạn chót |
|:---:|---|:---:|:---:|:---:|
| **ACT-01** | **Dựng Scene Playground 3D trong Unity (`Assets/Scenes/CorePlayground.unity`)**: Tạo sàn phẳng, tường hành lang $1.2\text{ m}$, Prefab Player Capsule gắn Controller, và Cinemachine Brain để bấm Play test thực tế bằng bàn phím/chuột. | `unity-specialist` / `level-designer` | **P0 (High)** | Đầu Sprint 2 |
| **ACT-02** | **Lập kế hoạch Sprint 2 (`/sprint-plan new`)**: Trọng tâm triển khai hệ thống Trí tuệ nhân tạo (AI Behavior Tree, Guard Perception Cone, Alert Broadcast) - linh hồn của Whisper Ward. | `producer` / `ai-programmer` | **P0 (High)** | Bắt đầu Sprint 2 |
| **ACT-03** | **Cập nhật trạng thái Sprint 1 sang Closed** trên toàn bộ hệ thống tài liệu. | `producer` | **P1 (Normal)** | Ngay lập tức |
