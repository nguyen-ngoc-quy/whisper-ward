# Epic: Guard AI & Perception System

> **Layer**: AI & Perception (Core Gameplay)  
> **GDD Reference**: `design/gdd/perception.md` (#2 Perception), `design/gdd/guard-ai-fsm.md` (#1 Guard AI FSM)  
> **Governing ADRs**: `ADR-0002` (Physics Query & Collision), `ADR-0007` (NavMesh Pathfinding)  
> **Status**: In Progress  
> **Stories**: 5 stories (`GUARD-01` through `GUARD-05`)  

## Overview

The Guard AI & Perception epic establishes the thinking enemy intelligence for Whisper Ward. It incorporates:
1. **Raycast Vision Cone ($100^\circ, 12\text{ m}$)** with 5 Hz scanning cadence, E20 sensing mask, and crouch/stand height adaptation.
2. **Two-Component Suspicion Meter**: Distance-inverse Accumulator ($dA/dt = \min(1.5/d, 0.60)$) and residual wariness ($R$) that sinks the investigate threshold.
3. **Guard FSM Controller (Patrol $\to$ Investigate $\to$ Chase)** with strict 3-state cap, speed scaling ($2.3 \to 5.0 \to 7.5\text{ m/s}$), and a $5.5\text{ m}$ in $1.0\text{ s}$ catch contract.
4. **Visual Telemetry**: Real-time 3D vision cone visualization (Color transitioning Moonlit/Green $\to$ Yellow $\to$ Red).
5. **Search Dwell & Give-up**: $4.0\text{ s}$ search dwell look-around at Last Known Position (LKP) before returning to patrol.

## Stories

| # | Story | Type | Status | Owner |
|---|-------|------|--------|-------|
| `GUARD-01` | Vision Cone Sensor & Line-of-Sight Sensing Engine | Logic/Integration | Ready | ai-programmer |
| `GUARD-02` | Suspicion Accumulator & Residual Wariness Engine | Logic | Ready | systems-designer |
| `GUARD-03` | Vision Cone 3D Mesh Visualizer & Suspicion Indicator | Visual/Feel | Ready | technical-artist |
| `GUARD-04` | Guard FSM Runtime Controller (Patrol -> Investigate -> Chase) | Integration | Ready | ai-programmer |
| `GUARD-05` | Search Dwell & Give-up Return to Patrol | Logic | Ready | ai-programmer |

## Definition of Done

This epic is complete when:
- Guard NPC accurately senses the player in a $100^\circ, 12\text{ m}$ cone at 5 Hz.
- Suspicion charges when player is seen, triggers Investigate, and escalates to Chase when full.
- Guard chases player at $7.5\text{ m/s}$ and executes Catch when within $5.5\text{ m}$ for $1.0\text{ s}$.
- If player breaks LOS and hides, guard searches LKP for $4.0\text{ s}$ then returns to patrol route.
- Vision cone is rendered in 3D with dynamic alert coloring.
- 100% automated test coverage for perception and FSM transitions with zero GC allocations on tick hot-paths.
