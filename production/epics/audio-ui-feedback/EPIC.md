# Epic: Audio & Visual Feedback

> **Layer**: UI & Presentation / Gameplay Feedback  
> **GDD Reference**: `design/gdd/guard-ai-fsm.md` (Visual/Audio Requirements lines 584..598)  
> **Governing ADRs**: `ADR-0001` (Event Bus), `ADR-0004` (Suspicion Meter & Grade Operator)  
> **Status**: Complete  
> **Stories**: 5 stories (`FEEDBACK-01` [Complete], `AUDIO-01` [Complete], `AUDIO-02` [Complete], `AUDIO-03` [Complete], `AUDIO-04` [Complete])  

## Overview

The Audio & Visual Feedback epic provides instant in-world legibility for AI behavioral states and sensory feedback for gameplay actions. As guards transition between internal FSM states (Patrol -> Investigate -> Chase -> Captured), world-space overhead markers and audio cues communicate their current awareness to the player. In Sprint 06 Polish, the epic expands to cover the complete 4-bus audio infrastructure, footstep audio, guard awareness SFX, and ambient environment sound.

## Stories

| # | Story | Type | Status | Owner |
|---|-------|------|--------|-------|
| `FEEDBACK-01` | Guard Overhead State Markers & Telegraphs | UI / Visual | Complete | technical-artist |
| `AUDIO-01` | Core Audio Infrastructure & Sound Manager | Logic / Audio | Complete | engine-programmer |
| `AUDIO-02` | Player Dual-Voice Footstep Audio System | Gameplay / Audio | Complete | gameplay-programmer |
| `AUDIO-03` | Guard Awareness SFX & Alert Stingers | Logic / Audio | Complete | gameplay-programmer |
| `AUDIO-04` | World Interaction & Ambient Audio | Level / Audio | Complete | level-designer |

## Definition of Done

This epic is complete when:
- Overhead marker accurately reflects guard FSM state without polling.
- Marker billboards smoothly toward main camera.
- Patrol state completely suppresses overhead visual clutter.
- Telegraph events fire synchronously on state transitions.
- Automated tests verify state-to-marker mapping, billboard rotation, and telegraph callbacks.
