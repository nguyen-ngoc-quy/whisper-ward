# Epic: Player Noise & Sound Propagation

> **Layer**: Gameplay (Core Perception Interop)  
> **GDD Reference**: `design/gdd/player-noise.md` (#3 Player Noise), `design/gdd/perception.md` (R5 Hearing)  
> **Governing ADRs**: `ADR-0001` (Event Bus), `ADR-0002` (Physics Query Service)  
> **Status**: In Progress  
> **Stories**: 1 story in Sprint 03 (`NOISE-01`)  

## Overview

The Player Noise epic governs sound emission, spatial propagation, and guard hearing detection. Movement states generate distinct acoustic footprints:
- **Crouch**: $0\text{ m}$ (Committed stealth state — completely silent).
- **Walk**: $4.0\text{ m}$ planar hearing radius.
- **Run**: $6.0\text{ m}$ planar hearing radius.

Hearing evaluation is evaluated at 5 Hz with E20 Linecast wall occlusion and soft vertical attenuation ($R_{eff} = R \times \max(0, 1 - |\Delta Y|/4.0)$).

## Stories

| # | Story | Type | Status | Owner |
|---|-------|------|--------|-------|
| `NOISE-01` | Player Footstep Noise Emitter & Guard Hearing Evaluation | Logic/Integration | Ready | gameplay-programmer |

## Definition of Done

This epic is complete when:
- Player locomotion publishes footstep noise events at walk and run speeds.
- Guard hearing detects noise within planar radius when unobstructed by solid walls.
- Hearing triggers guard investigation toward the noise origin point.
- Crouching emits 0 noise.
- Automated tests verify hearing radii, occlusion blocking, and vertical attenuation.
