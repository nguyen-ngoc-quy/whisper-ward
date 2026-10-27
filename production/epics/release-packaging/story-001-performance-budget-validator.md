# Story 001: Performance Budget & Frame Time Validator

> **Epic**: Release Packaging & Performance Certification (`production/epics/release-packaging/EPIC.md`)  
> **Story ID**: `PERF-01`  
> **Status**: Complete  
> **Layer**: Performance / Engine Quality  
> **Type**: Performance / Logic  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-27  
> **Owner**: `performance-analyst`  

---

## Context

**GDD**: `technical-preferences.md` (Performance Budgets lines 25..30), `PLAN.md` (Tuần 8 Performance Gate)  
**Governing ADRs**: `ADR-0002` (Physics Query & Collision), `ai-code.md` (2ms AI update budget)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Performance Budget Contracts**:
1. **Target Framerate & Frame Budget**:
   - Target Framerate: $60\text{ FPS}$ ($16.67\text{ ms}$ per frame).
   - AI Tick Budget: $\le 2.0\text{ ms}$ total across all active guard NPCs.
2. **Draw Calls & Batching**:
   - PC Windows Target: $\le 1000$ draw calls.
   - WebGL Target: $\le 300$ draw calls.
3. **Memory & Garbage Collection**:
   - Zero GC managed heap allocations on hot-path update loops (Locomotion, Perception, Suspicion, Audio, UI).

---

## Acceptance Criteria

- [x] **AC-PERF-01 — Frame Budget Metric Validation**: Framework models and validates frametime bounds against the $16.67\text{ ms}$ envelope.
- [x] **AC-PERF-02 — Platform Draw Call Ceilings**: Validates draw call ceiling limits for PC ($\le 1000$) and WebGL ($\le 300$).
- [x] **AC-PERF-03 — AI Execution Budget**: Verifies aggregate AI tick execution limits ($\le 2.0\text{ ms}$).

---

## QA Test Cases

- `test_performance_budget_validates_60fps_envelope`
- `test_draw_call_limits_enforced_by_platform_target`
- `test_ai_update_budget_enforces_2ms_threshold`
- `test_zero_gc_allocation_verified_on_simulation_tick`

---

## Test Evidence
- Unit Tests: `tests/unit/performance/performance_budget_validator_test.cs`
