# Story 003: Mission Game Loop & Lifecycle Controller

> **Epic**: Objective Mission Flow & Stealth Mastery Loop (`production/epics/objective-mission-flow/EPIC.md`)  
> **Story ID**: `FLOW-01`  
> **Status**: Complete  
> **Layer**: Meta / Gameplay  
> **Type**: Architecture / Controller  
> **Estimate**: 3h (0.4d)  
> **Owner**: `lead-programmer` / `gameplay-programmer`  

## Context

**GDD**: `design/gdd/game-concept.md`, `design/gdd/systems-index.md` (System #9 Mission Lifecycle)  
**Governing ADRs**: `ADR-0001` (Event Bus), `ADR-0004` (Suspicion Meter & Grade Operator)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Contracts**:
- Quản lý FSM vòng đời nhiệm vụ:
  `Infiltration` -> `ObjectiveAcquired` -> `GateUnlocked` -> `MissionCompleted` (hoặc `PlayerCaptured` / `MissionFailed`).
- Kết nối các luồng sự kiện:
  1. Khi nhặt Keycard -> chuyển sang `ObjectiveAcquired`.
  2. Khi mở Cổng an ninh -> chuyển sang `GateUnlocked`.
  3. Khi bước vào Extraction Zone -> chuyển sang `MissionCompleted`, gọi `StealthGradeOperator.EvaluateFinalGrade(Completed)`.
  4. Khi lính gác bắt giữ người chơi (`OnPlayerCaptured`) -> chuyển sang `MissionFailed`, gọi `StealthGradeOperator.RecordPlayerCaptured()` và `EvaluateFinalGrade(Failed)`.
- Hỗ trợ hàm khởi động lại màn chơi (`RestartMission`).

---

## Acceptance Criteria

- [x] **AC-FLOW-01 — Lifecycle State Transitions**: Chuyển đổi trạng thái mượt mà và đúng thứ tự từ Infiltration -> ObjectiveAcquired -> GateUnlocked -> MissionCompleted.
- [x] **AC-FLOW-02 — Clean Grade Evaluation on Extraction**: Khi hoàn thành rút lui, tự động kích hoạt tính toán điểm cho StealthGradeOperator và phát sự kiện `OnMissionFinished`.
- [x] **AC-FLOW-03 — Immediate Failure on Capture**: Khi có sự kiện `PlayerCaptured`, chuyển trạng thái `MissionFailed` và thông báo cho hệ thống chấm điểm đánh dấu Rank F.

---

## QA Test Cases

- `test_mission_lifecycle_starts_in_infiltration_state`
- `test_mission_lifecycle_advances_to_objective_acquired`
- `test_mission_lifecycle_advances_to_gate_unlocked`
- `test_mission_lifecycle_extraction_completes_mission_and_evaluates_grade`
- `test_mission_lifecycle_player_capture_fails_mission_and_triggers_rank_f`

---

## Test Evidence
- Integration Tests: `tests/integration/gameplay/mission_lifecycle_integration_test.cs`
