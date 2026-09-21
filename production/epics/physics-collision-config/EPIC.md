# Epic: Physics & Collision Configuration

> **Layer**: Foundation  
> **GDD**: `design/gdd/physics-collision-config.md`  
> **Architecture Module**: `PhysicsCollisionConfig` (`WhisperWard.Foundation.Physics`)  
> **Status**: Ready  
> **Stories**: 3 stories created — see table below  

## Stories

| # | Story | Type | Status | ADR | File |
|---|-------|------|--------|-----|------|
| 001 | 7-Layer Physics Matrix & Fail-Closed E20 Mask Resolution | Logic | Ready | ADR-0002 | `story-001-physics-matrix-e20-resolution.md` |
| 002 | Global Physics Queries & Trigger Immunity Contract | Logic | Ready | ADR-0002 | `story-002-global-queries-trigger-immunity.md` |
| 003 | Geometric Wall Thickness Validator & NonAlloc Spatial Queries | Logic | Ready | ADR-0002 | `story-003-wall-thickness-validator-nonalloc.md` |  

## Overview

The Physics & Collision Configuration system (System #18) defines and enforces all spatial query contracts, layer separation matrices, and kinematic ballistics trajectory rules for Whisper Ward. It acts as the single source of authority for absolute spatial fairness, eliminating stealth-genre failure modes such as guards seeing/hearing through walls, trigger volumes occluding perception raycasts, or projectiles tunneling through geometry. It initializes and validates the E20 LayerMask at startup, enforces a minimum geometric wall thickness invariant ($\ge 0.10\text{ m}$), guarantees `queriesHitTriggers = false` across all sensing raycasts, provides an isolated `HideSpotContainmentProfile` for trigger-based hiding checks, and sets `Physics.queriesHitBackfaces = false`.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|---|---|---|
| `ADR-0002: Shared Physics and Collision Contract` | 7-layer physics matrix, E20 startup resolution, fail-closed scene validation, queriesHitTriggers = false, queriesHitBackfaces = false, and minimum 0.10m wall thickness invariant. | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|---|---|---|
| `TR-FOUND-005` | Strict 7-layer physics matrix: Player, Guard, World, VisionOccluder, SoundOccluder, HideSpotTrigger, PickupTrigger. | `ADR-0002` ✅ |
| `TR-FOUND-006` | Global `queriesHitTriggers = false` for perception raycasts to prevent trigger volume occlusion anomalies. | `ADR-0002` ✅ |
| `TR-FOUND-007` | Geometric minimum wall thickness invariant of $0.10\text{ m}$ across all collision geometry. | `ADR-0002` ✅ |

## Definition of Done

This epic is complete when:
- All stories under this epic are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/physics-collision-config.md` (AC1 through AC16) are verified
- The 7-layer physics matrix and E20 LayerMask are registered and asserted at startup with fail-closed diagnostics
- `queriesHitTriggers = false` and `queriesHitBackfaces = false` are asserted at engine initialization
- Geometric wall thickness validator detects and flags any collision geometry $< 0.10\text{ m}$ in level scenes

## Next Step

Run `/create-stories physics-collision-config` to break this epic into implementable stories.
