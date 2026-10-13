# QA Test Plan: Sprint 05 — Objective Mission Flow & Stealth Mastery Loop

> **Date**: 2026-10-04  
> **Sprint**: Sprint 05 (`production/sprints/sprint-05.md`)  
> **Epics**: Suspicion Meter & Grade Operator, Objective Mission Flow  
> **Engine**: Unity 6 LTS (6000.3.17f1) | URP | Cinemachine 3.x | Input System | AI Navigation  
> **Author**: `qa-lead`  
> **Review Mode**: `lean`  

---

## 1. Mục tiêu Kiểm thử Sprint 05
Sprint 05 hoàn thiện vòng lặp chơi hoàn chỉnh từ đầu đến cuối (End-to-End Mission Loop) của **Whisper Ward**:
1. Thu thập vật phẩm nhiệm vụ (`Keycard`) trong khu vực lính gác tuần tra.
2. Mở khóa cánh cổng an ninh (`Locked Security Gate`) và tiếp cận ranh giới rút lui (`Extraction Boundary`).
3. Đánh giá xếp hạng trình độ lén lút của người chơi thông qua hệ thống **Stealth Grade Operator**:
   - Tính toán khấu trừ điểm số chính xác dựa trên số lần bị Chase ($N_{\text{chase}} \times 25.0\text{ đ}$) và chỉ số cảnh giác tồn dư ($P_{\text{residual}} \le 10.0\text{ đ}$).
   - Đảm bảo miễn trừ phạt hoàn toàn cho việc ném Burst đánh lạc hướng.
   - Kiểm chứng điều kiện nghiêm ngặt của **Rank S**: Chỉ trao Rank S khi điểm $\ge 90.0$ VÀ **$N_{\text{chase}} == 0$**.
   - Ghi nhận Rank F (Fail, Score = 0) ngay lập tức khi người chơi bị bắt.
4. Điều phối trạng thái vòng đời nhiệm vụ (`MissionLifecycleController`) không sinh rác GC và cập nhật HUD tổng kết màn chơi (`End-of-Mission Scorecard`).

---

## 2. Kế hoạch Kiểm thử Tự động (Automated Test Suites)

### 2.1 `GRADE-01`: Stealth Grade Operator System (Logic / Math)
- **Vị trí file test**: `tests/unit/gameplay/stealth_grade_operator_test.cs`
- **Mục tiêu kiểm thử (GDD `suspicion-meter-grade.md` D4..D8)**:
  1. `test_grade_operator_clean_run_awards_rank_s`: Không bị Chase, không có cảnh giác tồn dư -> 100 điểm, Rank S.
  2. `test_grade_operator_single_chase_deducts_25_pts_and_disqualifies_rank_s`: Bị Chase 1 lần -> trừ 25đ, điểm 75 -> Rank A, cấm Rank S dù điểm $\ge 90$.
  3. `test_grade_operator_clean_burst_distraction_incurs_zero_score_deduction`: Ném Burst đánh lạc hướng nhiều lần không bị trừ điểm trực tiếp trên thẻ điểm.
  4. `test_grade_operator_global_penalty_cap_enforces_10pt_completion_floor`: Bị phát hiện 4-5 lần nhưng thoát được -> điểm tối thiểu là 10.0đ (sàn hoàn thành), Rank C.
  5. `test_grade_operator_capture_terminates_with_score_zero_and_rank_f`: Bị lính gác bắt giữ -> Score 0, Rank F.
  6. `test_grade_operator_residual_wariness_penalty_applied_correctly`: Phạt tối đa 10.0đ theo mức độ cảnh giác của các lính gác tại thời điểm rút lui.

### 2.2 `OBJ-01`: Objective Keycard Pickup & Inventory State (Gameplay / Logic)
- **Vị trí file test**: `tests/unit/gameplay/objective_keycard_pickup_test.cs`
- **Mục tiêu kiểm thử**:
  1. `test_keycard_pickup_adds_item_to_player_inventory`: Tiếp cận và tương tác chuyển `HasKeycard` thành true.
  2. `test_keycard_pickup_dispatches_objective_acquired_event`: Phát sự kiện `OnObjectiveAcquired` thành công.
  3. `test_keycard_pickup_deactivates_world_object_without_destroy_leak`: Ẩn mesh và vô hiệu collider nhặt để tránh nhặt lặp lại.

### 2.3 `OBJ-02`: Locked Security Gate & Extraction Zone (Gameplay / Level)
- **Vị trí file test**: `tests/unit/gameplay/locked_gate_extraction_zone_test.cs`
- **Mục tiêu kiểm thử**:
  1. `test_locked_gate_rejects_interaction_without_keycard`: Không có Keycard cửa giữ nguyên trạng thái khóa.
  2. `test_locked_gate_unlocks_and_opens_with_keycard`: Có Keycard tương tác mở cửa thành công.
  3. `test_extraction_zone_triggers_mission_complete_when_player_enters`: Bước vào ranh giới thoát hiểm khi đã mở cửa kích hoạt `OnExtractionCompleted`.

### 2.4 `FLOW-01`: Mission Game Loop & Lifecycle Controller (Meta / Integration)
- **Vị trí file test**: `tests/integration/gameplay/mission_lifecycle_integration_test.cs`
- **Mục tiêu kiểm thử**:
  1. `test_mission_lifecycle_full_flow_infiltrate_to_extract_clean`: Luồng thành công Infiltrate -> Keycard -> Gate -> Extract -> Rank S.
  2. `test_mission_lifecycle_player_captured_triggers_fail_scorecard`: Luồng thất bại do lính bắt -> Rank F.

### 2.5 `UI-RESULT-01`: End-of-Mission Scorecard & Grade Card HUD (UI / Logic)
- **Vị trí file test**: `tests/unit/ui/end_of_mission_scorecard_test.cs`
- **Mục tiêu kiểm thử**:
  1. `test_scorecard_presents_rank_badge_and_score`: Hiển thị đúng ký hiệu Rank (S/A/B/C/F) và điểm số.
  2. `test_scorecard_displays_incident_breakdown`: Hiển thị số lần Chase và phạt cảnh giác.

---

## 3. Tiêu chuẩn Nghiệm thu Sprint 05 (Definition of Done)
- [ ] 5/5 Stories hoàn thành 100% tiêu chí nghiệm thu.
- [ ] Toàn bộ các test suite mới đạt 100% Pass.
- [ ] Tổng số tests tự động toàn dự án vượt mốc 260 tests.
- [ ] Không có lỗi runtime / NullReferenceException trong scene `CorePlayground.unity`.
- [ ] Zero managed heap allocation trên hot-path.
