# Epic: Camera (Cinemachine Rig)

> **Layer**: Core  
> **GDD**: `design/gdd/camera-cinemachine.md`  
> **Architecture Module**: `CameraRigService` (`WhisperWard.Core.Camera`)  
> **Status**: Complete  
> **Stories**: 3 stories (`story-001`, `story-002`, `story-003`) — Complete  

## Overview

The Camera system (System #20) establishes Whisper Ward's core spatial orientation, visual perspective, and reference coordinate rig. Built on Unity 6's Cinemachine 3.x package and integrated with the New Input System, it provides a tactical third-person over-the-shoulder orbit perspective (`CM_FreeOrbit`) and a constrained interior inspection rig (`CM_HideSpot`). The system acts as the sole source of truth for horizontal camera yaw (`camera_yaw`) consumed by the Player Controller, strictly enforces the `AC-P25` invariant (zero autonomous character turning when idle), executes real-time spherecast deocclusion against the E20 `World` layer ($R_{\text{cam\_col}} = 0.20\text{ m}$) with exponential distance recovery damping ($\tau_{\text{recover}} = 0.25\text{ s}$), and dynamically scales FOV ($60^\circ \to 68^\circ$) during active chase pursuit.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|---|---|---|
| `ADR-0008: Cinemachine 3rd-Person Follow Rig and Deocclusion Recovery` | Dual Virtual Camera architecture (`CM_FreeOrbit`, `CM_HideSpot`) managed in `LateUpdate`, pitch clamping $[-35^\circ, +65^\circ]$, right-shoulder framing offset ($X=+0.35\text{ m}, Y=1.35\text{ m}, Z=-2.80\text{ m}$), spherecast wall deocclusion with exponential recovery damping ($\tau_{\text{recover}} = 0.25\text{ s}$), strict `AC-P25` idle-facing decoupling, and dynamic chase FOV scaling. | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|---|---|---|
| `TR-CORE-008` | Cinemachine 3rd-person follow rig (right-shoulder offset $X=+0.35\text{ m}, Y=1.35\text{ m}, D_{\text{nom}}=2.80\text{ m}$, pitch clamped $[-35^\circ, +65^\circ]$). | `ADR-0008` ✅ |
| `TR-CORE-009` | Asymmetric occlusion recovery damping: fast push-in ($D_{\text{actual}} = \max(D_{\text{min}}, d_{\text{hit}} - R)$), smooth exponential pull-out ($\tau_{\text{recover}} = 0.25\text{ s}$). | `ADR-0008` ✅ |
| `TR-CORE-010` | Dynamic FOV expansion during active chase ($60.0^\circ \to 68.0^\circ$ over $0.6\text{ s}$; contraction over $1.5\text{ s}$). | `ADR-0008` ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | Third-Person Follow Rig & Planar Basis Service | Logic | Complete | ADR-0008 |
| 002 | Spherecast Deocclusion & Exponential Recovery Damping | Logic | Complete | ADR-0008 |
| 003 | Dynamic Chase FOV & HideSpot Viewport Blend | Integration | Complete | ADR-0008 |

## Definition of Done

This epic is complete when:
- All stories under this epic are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/camera-cinemachine.md` (AC1 through AC12) are verified
- `CameraRigService.cs` implements `ICameraRigService` and exposes authoritative planar forward and right basis vectors
- Orbit rotation clamped strictly to $[-35^\circ, +65^\circ]$ without gimbal lock or floor clipping
- Invariant `AC-P25` verified: rotating camera while WASD input is zero results in bit-identical character facing orientation
- Spherecast wall deocclusion prevents geometry clipping, and exponential damping ensures stutter-free pull-out
- `CM_HideSpot` transitions smoothly via `EaseInOut` blend ($0.35\text{ s}$) upon hide spot entry/exit
- Chase FOV scaling interpolates between $60^\circ$ and $68^\circ$ cleanly on chase start and stop events

## Next Step

Run `/story-readiness production/epics/camera-cinemachine/story-001-follow-rig-planar-basis.md` then `/dev-story`.
