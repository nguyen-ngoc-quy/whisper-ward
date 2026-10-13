# Story 003: End-of-Mission Scorecard & Grade Card HUD

> **Epic**: Suspicion Meter HUD & Grade Operator (`production/epics/suspicion-meter-grade/EPIC.md`)  
> **Story ID**: `UI-RESULT-01`  
> **Status**: Ready  
> **Layer**: UI / HUD  
> **Type**: UI Implementation  
> **Estimate**: 4h (0.5d)  
> **Owner**: `ui-programmer`  

## Context

**GDD**: `design/gdd/suspicion-meter-grade.md` (UI Requirements: End-of-Segment Grade Card), `design/gdd/hud-ui.md`  
**Governing ADRs**: `ADR-0004` (Suspicion Meter & Grade Operator)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Contracts**:
- Thẻ điểm tổng kết màn chơi (Modal dialog HUD):
  - Ẩn hoàn toàn trong suốt quá trình chơi lén lút (`ActiveInHierarchy = false`).
  - Lắng nghe sự kiện `OnGradeEvaluated` từ `StealthGradeOperator` hoặc `MissionLifecycleController`.
  - Khi được kích hoạt:
    - Hiển thị huy hiệu xếp hạng lớn: **S**, **A**, **B**, **C**, hoặc **F (FAIL)** với màu sắc nhận diện đặc trưng (S: Vàng Kim `#FFD700`, A: Xanh lục `#4CAF50`, B: Lam `#2196F3`, C: Cam `#FF9800`, F: Đỏ sẫm `#B71C1C`).
    - Hiển thị điểm số chất lượng cuối cùng (`FinalScore: 0.0 - 100.0 pts`).
    - Hiển thị chi tiết bảng phân tích khấu trừ (Incident Penalty: Chase count * 25.0, Residual Penalty, Clean Distraction count).
    - Cung cấp nút Restart / Next Mission callback.
  - Zero allocation trong quá trình cập nhật hiển thị.

---

## Acceptance Criteria

- [ ] **AC-UI-05 — Scorecard Presentation**: Tự động hiển thị khi nhận kết quả `StealthGradeResult`.
- [ ] **AC-UI-06 — Grade Badge & Breakdown Formatting**: Định dạng đúng chữ cái xếp hạng, màu sắc, điểm số và các mục khấu trừ chi tiết.
- [ ] **AC-UI-07 — Modal Control & Dismissal**: Cung cấp cơ chế mở/đóng và tương tác nút bấm.

---

## QA Test Cases

- `test_scorecard_hidden_by_default`
- `test_scorecard_presents_rank_s_badge_and_score`
- `test_scorecard_presents_rank_f_for_failed_capture_mission`
- `test_scorecard_formats_breakdown_metrics_accurately`
- `test_scorecard_dismiss_or_restart_event_fired`

---

## Test Evidence
- Unit Tests: `tests/unit/ui/end_of_mission_scorecard_test.cs`
