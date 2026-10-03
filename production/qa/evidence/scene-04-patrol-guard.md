# QA Evidence: SCENE-04 — NavMesh Surface Baking & Simple Patrol Guard NPC

> **Story**: `production/epics/level-playground/story-004-navmesh-patrol-guard.md`  
> **Date**: 2026-09-22  
> **Engine**: Unity 6 LTS (6000.3.17f1) | AI Navigation (`Unity.AI.Navigation`)  
> **Reviewer**: `ai-programmer` / `qa-lead` / `unity-specialist`  
> **Result**: VERIFIED ✅  

---

## 1. NavMesh Baking & Agent Configuration

| Parameter | Specification (ADR-0007 / GDD #12) | Implemented Value | Status |
|---|---|---|:---:|
| **NavMesh Surface Target** | Static arena geometry (Layer 20 `World`) | `NavMeshSurface` on `Arena_Floor_20x20` | **PASS** ✅ |
| **Agent Radius** | $0.40\text{ m}$ ($0.80\text{ m}$ diameter) | $0.40\text{ m}$ on `NavMeshAgent` | **PASS** ✅ |
| **Agent Height** | $1.80\text{ m}$ | $1.80\text{ m}$ on `NavMeshAgent` | **PASS** ✅ |
| **Patrol Speed** | $2.30\text{ m/s}$ nominal walking pace | $2.30\text{ m/s}$ | **PASS** ✅ |
| **Angular Speed** | $120.0^\circ/\text{s}$ turning rate | $120.0^\circ/\text{s}$ | **PASS** ✅ |
| **Arrival Tolerance** | $0.30\text{ m}$ stopping distance | $0.30\text{ m}$ | **PASS** ✅ |
| **Waypoint Dwell Duration** | $2.0\text{ s}$ pause at each destination | $2.0\text{ s}$ countdown timer | **PASS** ✅ |

---

## 2. Patrol Dynamics & Traversal Verification

| Verification Target | Test Scenario | Behavior Observed | Status |
|---|---|---|:---:|
| **Initial Parameter Match** | Driver spawn & configuration | State = `MovingToWaypoint`, Index = 0, Speed = $2.30\text{ m/s}$ | **PASS** ✅ |
| **Arrival Detection** | Guard enters $0.30\text{ m}$ waypoint sphere | Transitions to `DwellingAtWaypoint`, sets timer = $2.0\text{ s}$ | **PASS** ✅ |
| **Dwell Countdown** | Timer advances past $2.0\text{ s}$ | Advances index to 1, dispatches agent to next waypoint | **PASS** ✅ |
| **Endless Round-Robin Loop** | Guard arrives at final waypoint (WP_B) | Index wraps around to 0 (WP_A), continuous loop | **PASS** ✅ |
| **Corridor Clearance** | Main corridor width ($1.50\text{ m}$) vs Guard diameter ($0.80\text{ m}$) | Clear lateral margin $> 0.35\text{ m}$ on both sides | **PASS** ✅ |
| **Memory Allocation** | Continuous frame ticking (`TickDriver`) | $0\text{ B}$ managed heap allocations | **PASS** ✅ |

---

## 3. Automated Test Coverage

Suite: `tests/integration/scene/simple_patrol_driver_integration_test.cs`
- `test_simple_patrol_driver_initial_state_and_speed_match_ac_scene_14_and_15` — **PASS** ✅
- `test_simple_patrol_driver_arrival_transitions_to_dwelling_with_two_second_pause` — **PASS** ✅
- `test_simple_patrol_driver_dwelling_countdown_advances_to_next_waypoint` — **PASS** ✅
- `test_simple_patrol_driver_round_robin_loop_wraps_around_endlessly` — **PASS** ✅

---

## 4. Sign-Off

| Role | Name / Agent | Verdict | Signature Date |
|---|---|:---:|:---:|
| **AI Programmer** | `ai-programmer` | `[x] Approved` | 2026-09-22 |
| **QA Lead** | `qa-lead` | `[x] Approved` | 2026-09-22 |
| **Unity Specialist** | `unity-specialist` | `[x] Approved` | 2026-09-22 |
