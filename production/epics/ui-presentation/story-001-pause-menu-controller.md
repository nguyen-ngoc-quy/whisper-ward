# Story 001: Pause & In-Game Menu Controller

> **Epic**: UI Presentation & Visual Polish (`production/epics/ui-presentation/EPIC.md`)  
> **Story ID**: `UI-PAUSE-01`  
> **Status**: Complete  
> **Layer**: UI / Game Flow  
> **Type**: UI / Logic  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-27  
> **Owner**: `ui-programmer`  

---

## Context

**GDD**: `PLAN.md` (Tuần 7 — UI/UX Pause Menu & Flow), `design/gdd/suspicion-meter-grade.md`  
**Governing ADRs**: `ADR-0001` (Event Bus), Unity 6 LTS Input System  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Pause Menu Contracts**:
1. **Input Interception**:
   - Polling Escape key via Unity 6 `Keyboard.current.escapeKey.wasPressedThisFrame` (preprocessor guarded for multi-platform compatibility).
2. **TimeScale Coordination**:
   - Pausing freezes simulation (`Time.timeScale = 0.0f`).
   - Resuming unfreezes simulation (`Time.timeScale = 1.0f`).
3. **Menu Flow & Actions**:
   - `Resume()`: Hides pause panel and restores time.
   - `Restart()`: Resumes time and dispatches restart / scene reload event.
   - `Quit()`: Quits application or fires exit callback.

---

## Acceptance Criteria

- [x] **AC-PAUSE-01 — Escape Key Toggle**: Pressing Escape toggles the pause panel visibility and halts/resumes game simulation time.
- [x] **AC-PAUSE-02 — Resume Action Restoration**: Calling Resume explicitly restores `Time.timeScale = 1.0f` and hides pause UI.
- [x] **AC-PAUSE-03 — Restart Notification**: Calling Restart triggers the restart lifecycle event cleanly.

---

## QA Test Cases

- `test_pause_menu_toggles_pause_state_and_timescale`
- `test_pause_menu_resume_restores_normal_timescale`
- `test_pause_menu_restart_dispatches_restart_event`
- `test_pause_menu_inactive_when_game_over`

---

## Test Evidence
- Unit Tests: `tests/unit/ui/pause_menu_controller_test.cs`
