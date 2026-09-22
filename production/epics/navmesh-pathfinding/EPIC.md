# Epic: NavMesh / Pathfinding

> **Layer**: Core  
> **GDD**: `design/gdd/navmesh-pathfinding.md`  
> **Architecture Module**: `NavMeshQueryService` (`WhisperWard.Core.Navigation`)  
> **Status**: Ready  
> **Stories**: 3 stories (`story-001`, `story-002`, `story-003`) — Ready  

## Overview

The NavMesh / Pathfinding system (System #12) establishes Whisper Ward's authoritative spatial navigation and reachability infrastructure. Built on Unity's AI Navigation package (`NavMeshSurface`, `NavMeshAgent`, `NavMeshPath`), it provides the continuous 2.5D walkable manifold and geometric path metrics consumed by Guard AI (`#1`), Perception (`#2`), and Level Certification (`#8`). The system manages agent-parameterized path queries (`NavMeshAgent.CalculatePath`) using pre-allocated corner buffers (guaranteeing 0 B managed GC), computes exact cumulative piecewise-linear 3D polyline distances ($d_{\text{path}}$), evaluates surface snapping within a tight $0.40\text{ m}$ threshold (`NavMesh.SamplePosition`), governs reciprocal velocity avoidance (RVO) priorities, and strictly distinguishes arrival ($\le 0.30\text{ m}$) from partial path terminal blockage (`path-end`).

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|---|---|---|
| `ADR-0007: NavMesh NonAlloc Query Service and Path Polyline Metric` | Agent-parameterized path queries via `INavMeshQueryService`, static `NavMeshPath` corner pooling (0 GC), piecewise polyline distance calculation ($d_{\text{path}}$), surface-sample tolerance ($0.40\text{ m}$), RVO avoidance priorities (10/30/50), and zero off-mesh links. | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|---|---|---|
| `TR-CORE-005` | Agent-parameterized `NavMesh.CalculatePath` invocation using pre-allocated `NavMeshPath` buffers (0 GC). | `ADR-0007` ✅ |
| `TR-CORE-006` | Spatial corridor clearance enforcement: minimum navigable width $\ge 1.20\text{ m}$ ($\ge 1.50\text{ m}$ main patrol corridors). | `ADR-0007` ✅ |
| `TR-CORE-007` | Off-mesh links strictly disabled (planar 2.5D surface navigation only, `generateLinks = false`). | `ADR-0007` ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | NonAlloc Path Query Service & Polyline Distance Metric | Logic | Complete | ADR-0007 |
| 002 | Reciprocal Velocity Avoidance & Spatial Navigation Interop | Integration | Complete | ADR-0007 |
| 003 | Spatial Corridor Clearance & NavMesh Certification Validator | Logic | Complete | ADR-0007 |

## Definition of Done

This epic is complete when:
- All stories under this epic are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/navmesh-pathfinding.md` (AC1 through AC21) are verified
- `NavMeshQueryService.cs` implements `INavMeshQueryService` with zero heap allocations on repeated path calculations
- `CalculateCumulativePathDistance` accurately returns piecewise Euclidean distances and yields $+\infty$ for invalid/empty paths
- Surface projection rejects target points farther than $0.40\text{ m}$ from the baked mesh
- Arrival ($d \le 0.30\text{ m}$) and partial path termination (`path-end`) are mutually exclusive and deterministically reported to Guard AI FSM
- Unit tests confirm path metric mathematical invariants and zero GC on hot query paths

## Next Step

Run `/story-readiness production/epics/navmesh-pathfinding/story-001-nonalloc-query-polyline-metric.md` then `/dev-story`.
