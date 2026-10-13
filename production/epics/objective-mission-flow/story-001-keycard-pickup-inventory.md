# Story 001: Objective Keycard Pickup & Inventory State

> **Epic**: Objective Mission Flow & Stealth Mastery Loop (`production/epics/objective-mission-flow/EPIC.md`)  
> **Story ID**: `OBJ-01`  
> **Status**: Complete  
> **Layer**: Gameplay / Logic  
> **Type**: Gameplay Mechanic  
> **Estimate**: 4h (0.5d)  
> **Owner**: `gameplay-programmer`  

## Context

**GDD**: `design/gdd/game-concept.md`, `design/gdd/systems-index.md` (System #7 Objective Interaction)  
**Governing ADRs**: `ADR-0001` (Event Bus)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Contracts**:
- Keycard đặt trong không gian màn chơi 3D (ví dụ trên bàn điều khiển).
- Khi người chơi bước vào bán kính tương tác ($r \le 2.0\text{ m}$) và kích hoạt tương tác (hoặc tự động nhặt khi chạm trigger):
  1. Trạng thái người chơi được cập nhật `HasKeycard = true`.
  2. Phát sự kiện `OnObjectiveAcquired` (hoặc delegate) cho các hệ thống liên quan.
  3. Vật thể Keycard trong thế giới được ẩn hiển thị (disable visual mesh / collider) mà không phá hủy GameObject để tránh rác GC (`zero alloc / object reuse`).
  4. Hỗ trợ reset trạng thái khi bắt đầu lượt chơi mới (`ResetPickup`).

---

## Acceptance Criteria

- [x] **AC-OBJ-01 — Proximity Interaction & Acquisition**: Người chơi tiếp cận trong bán kính tương tác có thể nhặt Keycard thành công, chuyển `HasKeycard` thành `true`.
- [x] **AC-OBJ-02 — Event Notification**: Phát sự kiện `OnObjectiveAcquired` chính xác 1 lần tại thời điểm nhặt.
- [x] **AC-OBJ-03 — World Object Deactivation & Reusability**: Vật thể Keycard trong thế giới ẩn mesh và tắt collider, ngăn nhặt lặp lại; hỗ trợ khôi phục khi reset run.

---

## QA Test Cases

- `test_keycard_pickup_initial_state_not_acquired`
- `test_keycard_pickup_interaction_sets_has_keycard_true`
- `test_keycard_pickup_dispatches_objective_acquired_event`
- `test_keycard_pickup_deactivates_visuals_and_prevents_duplicate_pickup`
- `test_keycard_pickup_reset_restores_pickup_state`

---

## Test Evidence
- Unit Tests: `tests/unit/gameplay/objective_keycard_pickup_test.cs`
