# Epic: Audio & Visual Feedback

> **Layer**: UI & Presentation / Gameplay Feedback  
> **GDD Reference**: `design/gdd/guard-ai-fsm.md` (Visual/Audio Requirements lines 584..598)  
> **Governing ADRs**: `ADR-0001` (Event Bus), `ADR-0004` (Suspicion Meter & Grade Operator)  
> **Status**: Complete  
> **Stories**: 1 story (`FEEDBACK-01` in Sprint 04)  

## Overview

The Audio & Visual Feedback epic provides instant in-world legibility for AI behavioral states and gameplay intentions. As guards transition between internal FSM states (Patrol -> Investigate -> Chase -> Captured), world-space overhead markers communicate their current awareness to the player:
- **Patrol**: Hidden / suppressed, maintaining visual clarity.
- **Investigate**: Amber Warning `?` marker indicating curiosity / suspicion following sound or vision flicker.
- **Chase**: Red Alert `!` marker indicating confirmed visual detection and active pursuit, accompanied by alert telegraph cues.
- **Captured**: Decisive capture indicator terminating stealth gameplay.

## Stories

| # | Story | Type | Status | Owner |
|---|-------|------|--------|-------|
| `FEEDBACK-01` | Guard Overhead State Markers & Telegraphs | UI / Visual | Complete | technical-artist |

## Definition of Done

This epic is complete when:
- Overhead marker accurately reflects guard FSM state without polling.
- Marker billboards smoothly toward main camera.
- Patrol state completely suppresses overhead visual clutter.
- Telegraph events fire synchronously on state transitions.
- Automated tests verify state-to-marker mapping, billboard rotation, and telegraph callbacks.
