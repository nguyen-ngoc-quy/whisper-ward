# Story 002: Locked Security Gate & Extraction Zone

> **Epic**: Objective Mission Flow & Stealth Mastery Loop (`production/epics/objective-mission-flow/EPIC.md`)  
> **Story ID**: `OBJ-02`  
> **Status**: Complete  
> **Layer**: Gameplay / Level  
> **Type**: Gameplay / Environment  
> **Estimate**: 4h (0.5d)  
> **Owner**: `gameplay-programmer` / `level-designer`  

## Context

**GDD**: `design/gdd/game-concept.md`, `design/gdd/systems-index.md` (System #8 Extraction Boundary)  
**Governing ADRs**: `ADR-0001` (Event Bus)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Contracts**:
- **Locked Security Gate**:
  - Cổng chắn đường có collider vật lý ngăn người chơi vượt qua.
  - Khi người chơi tương tác với cổng:
    - Nếu `HasKeycard == false`: Từ chối mở, phát sự kiện `OnUnlockFailed` / `GateLockedFeedback`.
    - Nếu `HasKeycard == true`: Chuyển trạng thái `IsOpen = true`, mở khóa, vô hiệu hóa collider cản đường (hoặc mở cửa), phát sự kiện `OnGateUnlocked`.
- **Extraction Zone**:
  - Khu vực trigger ranh giới thoát hiểm (`BoxCollider` với `isTrigger = true`).
  - Khi người chơi bước vào Extraction Zone:
    - Nếu đã mở cổng và hoàn thành mục tiêu: Phát sự kiện `OnExtractionTriggered` / `OnMissionCompleted`, kích hoạt kết thúc màn chơi cho `MissionLifecycleController`.
    - Hỗ trợ reset trạng thái khi bắt đầu lượt chơi mới.

---

## Acceptance Criteria

- [x] **AC-OBJ-04 — Gate Rejection without Keycard**: Khi chưa có Keycard, cổng từ chối mở và giữ nguyên collider cản trở.
- [x] **AC-OBJ-05 — Gate Unlock with Keycard**: Khi có Keycard, cổng mở khóa thành công, cập nhật trạng thái `IsOpen = true` và phát sự kiện `OnGateUnlocked`.
- [x] **AC-OBJ-06 — Extraction Zone Completion**: Người chơi bước vào vùng thoát hiểm sau khi cổng mở sẽ kích hoạt sự kiện rút lui thành công (`OnExtractionTriggered`).

---

## QA Test Cases

- `test_locked_gate_rejects_interaction_without_keycard`
- `test_locked_gate_unlocks_and_opens_with_keycard`
- `test_locked_gate_idempotent_when_already_open`
- `test_extraction_zone_triggers_completion_event_when_player_enters`
- `test_extraction_zone_ignores_non_player_colliders`

---

## Test Evidence
- Unit Tests: `tests/unit/gameplay/locked_gate_extraction_zone_test.cs`
