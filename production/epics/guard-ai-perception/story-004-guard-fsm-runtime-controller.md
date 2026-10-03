# Story 004: Guard FSM Runtime Controller (Patrol -> Investigate -> Chase)

> **Epic**: Guard AI & Perception (`production/epics/guard-ai-perception/EPIC.md`)  
> **Story ID**: `GUARD-04`  
> **Status**: Complete  
> **Layer**: AI & Navigation  
> **Type**: Integration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-03  
> **Owner**: `ai-programmer`  

## Context

**GDD**: `design/gdd/guard-ai-fsm.md` (3-State FSM, Speed coupling, Catch contract), `design/gdd/perception.md`  
**Governing ADRs**: `ADR-0007` (NavMesh Pathfinding)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Movement Speeds & Contracts**:
- Patrol speed: $V_{patrol} = 2.30\text{ m/s}$ (NavMeshAgent speed).
- Investigate speed: $V_{investigate} = 5.00\text{ m/s}$.
- Chase speed: $V_{chase} = 7.50\text{ m/s}$ ($1.20 \times V_{run} = 1.20 \times 6.25\text{ m/s}$).
- Catch contract: Guard catches player when distance $d \le 5.50\text{ m}$ maintained continuously for $t_{catch} = 1.0\text{ s}$ during Chase.

---

## Acceptance Criteria

- [x] **AC-GUARD-09 — 3-State FSM Transition Chain**: Guard transitions cleanly:
  - `Patrol` (default, follows waypoints at $2.3\text{ m/s}$).
  - `Investigate` (triggered by confirm window commit, noise, or cap-forced, moves to investigation point at $5.0\text{ m/s}$).
  - `Chase` (triggered by $A \ge 1.0$, pursues player continuously at $7.5\text{ m/s}$).
- [x] **AC-GUARD-10 — NavMesh Speed Switching**: `NavMeshAgent.speed` switches synchronously with state transitions ($2.3 \to 5.0 \to 7.5\text{ m/s}$).
- [x] **AC-GUARD-11 — Catch Condition Resolution**: During `Chase`, when $d \le 5.50\text{ m}$ for $1.0\text{ s}$, triggers `OnPlayerCaptured` event, halts movement, and records capture.
- [x] **AC-GUARD-12 — Zero Heap Allocation on State Evaluation**: FSM state checks execute with 0 B managed GC allocation per tick.
