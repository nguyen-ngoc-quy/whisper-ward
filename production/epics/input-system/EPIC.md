# Epic: Input System Service

> **Layer**: Foundation  
> **GDD**: `design/gdd/input-system.md`  
> **Architecture Module**: `InputSystemService` (`WhisperWard.Foundation.Input`)  
> **Status**: Ready  
> **Stories**: 2 stories created — see table below  

## Stories

| # | Story | Type | Status | ADR | File |
|---|-------|------|--------|-----|------|
| 001 | Unified C# Input Actions Wrapper & Action Map Switching | Logic | Ready | ADR-0005 | `story-001-input-actions-wrapper-map-switching.md` |
| 002 | Radial Deadzones & Stance/Throw Input Buffering Service | Logic | Ready | ADR-0005 | `story-002-radial-deadzones-buffering-service.md` |  

## Overview

The Input System Service (System #19) receives, normalizes, filters, and abstracts all hardware control signals (Keyboard, Mouse, Gamepad) into decoupled, context-aware Action Maps for Whisper Ward. Built on the Unity New Input System (`com.unity.inputsystem`), it isolates locomotion, camera, and UI from direct hardware quirks. It manages mutually exclusive switching between `Player` and `UI` action maps, provides a $150\text{ ms} - 200\text{ ms}$ input buffer window for stance and throw triggers, applies radial deadzones ($0.10$ inner, $0.95$ outer) to analog stick inputs, enforces Anti-Pillar scope discipline (strictly rejecting jump, climb, vault, or cover-magnet bindings), and guarantees zero heap allocations on continuous polling loops.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|---|---|---|
| `ADR-0005: Input Action Asset & Stance Buffering Contract` | Strongly typed C# class wrapper for `WhisperWardInputActions.inputactions`, exclusive action map switching with state flush, 150ms stance/throw buffering, dual radial deadzones, and 0 B GC polling. | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|---|---|---|
| `TR-FOUND-008` | Unified C# Action Asset: WASD locomotion, Crouch toggle/hold, Run hold, Burst throw charge/release. | `ADR-0005` ✅ |
| `TR-FOUND-009` | Analog stick response curve with inner deadzone $0.10$ and outer deadzone $0.95$. | `ADR-0005` ✅ |

## Definition of Done

This epic is complete when:
- All stories under this epic are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/input-system.md` (AC1 through AC14) are verified
- `WhisperWardInputActions.cs` typed C# wrapper is generated and registered
- Exclusive switching between `Player` and `UI` maps flushes analog motion and look vectors to `Vector2.zero`
- Automated EditMode tests confirm radial deadzone clamping, stance buffering window, and zero-allocation polling

## Next Step

Run `/create-stories input-system` to break this epic into implementable stories.
