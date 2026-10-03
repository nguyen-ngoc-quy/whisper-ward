# Story 004: NavMesh Surface Baking & Simple Patrol Guard NPC

> **Epic**: Level Playground (`production/epics/level-playground/EPIC.md`)  
> **Story ID**: `SCENE-04`  
> **Status**: Complete  
> **Layer**: Integration (AI & Navigation)  
> **Type**: Integration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-09-22  
> **Last Updated**: 2026-09-22  
> **Owner**: `ai-programmer`  

## Context

**GDD**: `design/gdd/navmesh-pathfinding.md`, `design/gdd/guard-ai-fsm.md`  
**Governing ADRs**: `ADR-0007`  
**Dependencies**: `SCENE-01`  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

---

## Acceptance Criteria

- [x] **AC-SCENE-13 — NavMesh Surface Baking**: `NavMeshSurface` configured on the environment floor with Agent Humanoid parameters (Radius $0.30\text{ m}$, Height $1.80\text{ m}$, Step Height $0.30\text{ m}$, Slope $45^\circ$).
- [x] **AC-SCENE-14 — Guard NPC Patrol Prefab**: Prefab `Guard_PatrolNPC.prefab` (distinct red visual capsule) equipped with `NavMeshAgent` and a lightweight patrol driver (`SimplePatrolDriver.cs`).
- [x] **AC-SCENE-15 — Endless Waypoint Patrol**: Guard patrols smoothly between 2 or more waypoints across the $1.50\text{ m}$ main corridor, turning with constant angular speed and pausing for $2.0\text{ s}$ at each destination.

---

## Implementation Files
- `src/AI/Navigation/SimplePatrolDriver.cs`
- `Assets/WhisperWardAI/Runtime/Navigation/SimplePatrolDriver.cs`
- `Assets/Editor/CorePlaygroundBuilder.cs`
- `tests/integration/scene/simple_patrol_driver_integration_test.cs`
- `production/qa/evidence/scene-04-patrol-guard.md`

---

## Completion Notes
**Completed**: 2026-09-22  
**Criteria**: 3/3 passing (AC-SCENE-13, AC-SCENE-14, AC-SCENE-15)  
**Deviations**: None  
**Test Evidence**: Integration test suite at `tests/integration/scene/simple_patrol_driver_integration_test.cs` (4 tests passing) + QA Evidence at `production/qa/evidence/scene-04-patrol-guard.md`  
**Code Review**: Complete  
