# Epic: Player Movement & Hide

> **Layer**: Gameplay (Core Player & Environment Interaction)  
> **GDD Reference**: `design/gdd/player-movement-hide.md` (#5 Player Movement & Hide)  
> **Governing ADRs**: `ADR-0002` (Physics Collision), `ADR-0006` (Player Locomotion FSM), `ADR-0008` (Camera Orbit Rig)  
> **Status**: In Progress  
> **Stories**: 1 story in Sprint 04 (`HIDE-01`)  

## Overview

The Player Movement & Hide epic governs interactive environmental sanctuary objects (lockers, cupboards, vents, and under-desk crawlspaces) and their interaction with the player capsule and guard perception.

Core tenets:
1. **Sanctuary Invariant**: A player hidden inside an unobserved hide spot is 100% immune to guard perception and cannot be captured.
2. **Witnessed Trap Invariant**: Entering a hide spot while being actively chased and seen by a guard converts the spot into a lethal trap. The guard approaches `guard_hold` ($d \le 5.10\text{ m}$), initiates a $1.5\text{ s}$ dwell verify (`t_spotfront_verify`), and executes Capture if the player remains inside.
3. **Pure Pivot Rotation**: Movement inside a hide spot locks translation ($\Delta \vec{p} == 0$), but allows rotational looking without generating footstep noise ($0\text{ dB}$).
4. **Boundary Hysteresis**: Prevents event flapping across trigger boundaries within $0.06\text{ s}$.

## Stories

| # | Story | Type | Status | Owner |
|---|-------|------|--------|-------|
| `HIDE-01` | Hide Spot & Sanctuary Interaction Volume | Logic / Integration | In Progress | gameplay-programmer |

## Definition of Done

This epic is complete when:
- Player can enter and exit hide spots with pure-pivot translation lock.
- Unwitnessed entry provides complete sanctuary from guard detection.
- Witnessed chase entry attracts the pursuing guard to verify and capture the player after $1.5\text{ s}$ dwell.
- 100% automated test coverage with zero GC allocations on hot paths.
