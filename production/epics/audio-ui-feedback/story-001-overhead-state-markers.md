# Story 001: Guard Overhead State Markers & Telegraphs

> **Epic**: Audio & Visual Feedback (`production/epics/audio-ui-feedback/EPIC.md`)  
> **Story ID**: `FEEDBACK-01`  
> **Status**: Complete  
> **Layer**: UI & Presentation / Gameplay Feedback  
> **Type**: UI / Visual  
> **Estimate**: 3h (0.3d)  
> **Manifest Version**: 2026-10-04  
> **Owner**: `technical-artist`  

## Context

**GDD**: `design/gdd/guard-ai-fsm.md` (Visual/Audio Requirements lines 584..598)  
**Governing ADRs**: `ADR-0001` (Event Bus), `ADR-0004` (Suspicion Meter & Grade Operator)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Contracts**:
- **State Representation**:
  - `Patrol`: `MarkerType.None` (completely invisible).
  - `Investigate`: `MarkerType.Question` (Warning Amber `#FFB800`).
  - `Chase`: `MarkerType.Exclamation` (Alert Red `#FF3333`).
  - `Captured`: `MarkerType.Captured` (Skull / End Indicator).
- **Camera Billboard Alignment**: Markers billboard toward the active camera position so they remain clear and readable from all third-person angles without pitch-inversion clipping.
- **Telegraph Dispatch**: Fires `OnTelegraphTriggered(MarkerType, GuardState)` on state change to drive audio/particle stings without tight coupling.
- **Zero Allocations**: Event-driven state updates listening to `GuardFSMRuntimeController.OnStateChanged`.

---

## Acceptance Criteria

- [x] **AC-FEED-01 — State-to-Marker Mapping**: Guard state changes dynamically switch overhead visual state (Patrol -> None, Investigate -> Question `?`, Chase -> Exclamation `!`).
- [x] **AC-FEED-02 — Camera Billboard & Telegraph Notification**: Marker faces camera and telegraph events fire synchronously on escalation transitions.

---

## QA Test Cases

- `test_overhead_marker_hidden_on_patrol_state`
- `test_overhead_marker_shows_question_mark_on_investigate_state`
- `test_overhead_marker_shows_exclamation_mark_on_chase_state`
- `test_overhead_marker_billboards_toward_camera`
- `test_overhead_marker_dispatches_telegraph_event_on_chase_transition`
- `test_overhead_marker_clears_when_guard_returns_to_patrol`

---

## Test Evidence
- Unit Tests: `tests/unit/ui/guard_overhead_marker_test.cs`
