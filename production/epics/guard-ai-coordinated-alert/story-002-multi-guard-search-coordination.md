# Story 002: Multi-Guard Search Standoff Coordination

> **Epic**: Guard AI Coordinated Alert & Radio Bark Network (`production/epics/guard-ai-coordinated-alert/EPIC.md`)  
> **Story ID**: `ALERT-02`  
> **Status**: Complete  
> **Layer**: AI & Navigation  
> **Type**: Logic / Integration  
> **Estimate**: 3h (0.4d)  
> **Manifest Version**: 2026-10-04  
> **Owner**: `ai-programmer`  

## Context

**GDD**: `design/gdd/guard-ai-fsm.md` (Alert propagation C3, multi-guard search coordination lines 514..548)  
**Governing ADRs**: `ADR-0001` (Event Bus), `ADR-0007` (NavMesh Pathfinding)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Contracts**:
- **Standoff Separation**: When $\ge 2$ guards investigate the same Last Known Position (LKP), they must not occupy the same destination. Standoff positions are separated by $d_{\text{sep}} \ge 2.0\text{ m}$ to prevent NavMeshAgent crowding and collision.
- **Divergent Scan Heading**: During the 4.0s dwell search, multiple guards scan along divergent base angles:
  - For 2 guards: Guard 1 faces forward ($0^\circ$), Guard 2 faces backward ($180^\circ$).
  - For $N$ guards: Base headings diverge by $\Delta \psi = 360^\circ / N$, maximizing room coverage.
- **Dynamic Registration & Release**: Guards dynamically register with the search coordinator when committing to an investigation target and release their slot upon returning to Patrol or entering Chase.

---

## Acceptance Criteria

- [x] **AC-ALERT-05 — Standoff Offset Separation ($\ge 2.0\text{ m}$)**: Multiple guards investigating the same LKP are assigned standoff positions with pairwise distance $\ge 2.0\text{ m}$.
- [x] **AC-ALERT-06 — Divergent Dwell Scan Arcs**: Guards dwelling at the shared investigation site adopt divergent base yaw headings ($180^\circ$ for 2 guards), sweeping complementary search arcs.

---

## QA Test Cases

- `test_two_guards_investigating_same_target_apply_standoff_offsets`
- `test_standoff_separation_is_at_least_2m_between_guards`
- `test_multi_guard_dwell_lookaround_angles_are_divergent`
- `test_three_guards_standoff_and_divergent_headings`
- `test_coordinator_releases_guard_on_investigation_complete`

---

## Test Evidence
- Unit Tests: `tests/unit/ai/multi_guard_search_standoff_test.cs`
