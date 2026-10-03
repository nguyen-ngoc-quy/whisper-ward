# Epic: Level Playground (Playable 3D Integration Arena)

> **Layer**: Feature / Integration (Scene Sandbox)  
> **GDD Reference**: `design/gdd/systems-index.md` (System #8 Level/Content prototyping, Retrospective ACT-01)  
> **Governing ADRs**: `ADR-0006`, `ADR-0007`, `ADR-0008`  
> **Status**: In Progress  
> **Stories**: 6 stories (`SCENE-01` through `SCENE-06`)  

## Overview

The Level Playground epic establishes Whisper Ward's first interactive, playable 3D sandbox environment (`Assets/Scenes/CorePlayground.unity`) in Unity 6 LTS. It unifies the pure C# headless systems developed in Foundation and Core layers (Kinematic Character Controller, NavMesh Pathfinding & Corridor Clearance, and Cinemachine Follow Rig) into an interactive real-time prototype.

The arena features a $20\text{ m} \times 20\text{ m}$ perimeter floor, standardized $1.50\text{ m}$ main and $1.20\text{ m}$ secondary corridors, a low-headroom HideSpot alcove ($H=1.40\text{ m} < 1.80\text{ m}$), a baked NavMesh surface, an animated patrolling Guard NPC, and a full third-person camera rig with right-shoulder framing, spherecast deocclusion, and dynamic chase FOV scaling.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|---|---|---|
| `ADR-0006: Kinematic Character Controller and Stance FSM` | Kinematic locomotion, diagonal normalization, feet-anchored capsule scaling, stand headroom probe against E20 Solid layer. | LOW |
| `ADR-0007: NavMesh NonAlloc Path Query and Corridor Clearance` | NonAlloc path query service, polyline distance metric, NavMesh surface certification, corridor clearance $\ge 1.20\text{ m}$. | LOW |
| `ADR-0008: Cinemachine 3rd-Person Follow Rig and Deocclusion Recovery` | Dual Virtual Camera architecture (`CM_FreeOrbit`, `CM_HideSpot`), right-shoulder framing, pitch clamp $[-35^\circ, +65^\circ]$, spherecast deocclusion against E20 with exponential recovery. | LOW |

## Stories

| # | Story | Type | Status | Owner |
|---|-------|------|--------|-------|
| `SCENE-01` | 3D Arena Geometry Blockout & Moonlit Lighting Setup | Integration | Ready | level-designer |
| `SCENE-02` | Player Capsule Prefab & Runtime Input Driver Integration | Integration | Ready | unity-specialist |
| `SCENE-03` | Cinemachine Camera Rig & Orbit Follow Integration | Integration | Ready | unity-specialist |
| `SCENE-04` | NavMesh Surface Baking & Simple Patrol Guard NPC | Integration | Ready | ai-programmer |
| `SCENE-05` | On-Screen Locomotion Debug HUD & Playtest Verification Suite | UI / Playtest | Ready | qa-lead |
| `SCENE-06` | URP Moonlit Stealth Post-Processing & Distinct Materials | Visual/Feel | Ready | technical-artist |

## Definition of Done

This epic is complete when:
- `Assets/Scenes/CorePlayground.unity` loads cleanly in Unity 6 LTS with 0 errors.
- Pressing Play in the Unity Editor allows fluid WASD navigation (walk, run, crouch).
- Mouse controls camera orbit smoothly without geometry clipping or gimbal lock.
- Walking into the HideSpot alcove clamps stand headroom and triggers the HideSpot camera blend.
- Guard NPC navigates the baked NavMesh surface in an endless patrol loop.
- The interactive playtest evidence report is signed off with 0 unhandled console exceptions.
