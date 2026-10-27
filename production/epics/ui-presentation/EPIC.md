# Epic: UI Presentation & Visual Polish

> **Layer**: UI / Rendering / Post-Processing  
> **GDD Reference**: `PLAN.md` (Tuần 7 — Visual Polish & UX)  
> **Governing ADRs**: `ADR-0002` (Materials & Post-Processing), `ADR-0004` (Suspicion HUD)  
> **Status**: Complete  
> **Stories**: 2 stories (`UI-PAUSE-01` [Complete], `VFX-POST-01` [Complete])  

## Overview

The UI Presentation & Visual Polish epic delivers the visual aesthetic and menu interaction flows for Whisper Ward. It encapsulates the Cold Watch moonlit post-processing volume (Vignette, cool tone grading, subtle Bloom) and the Pause/Menu interaction flow supporting both Unity 6 Input System and legacy fallbacks with zero GC allocations.

## Stories

| # | Story | Type | Status | Owner |
|---|-------|------|--------|-------|
| `UI-PAUSE-01` | Pause & In-Game Menu Controller | UI / Logic | Complete | ui-programmer |
| `VFX-POST-01` | Cold Watch URP Post-Processing & Lighting Profile | Visual / Rendering | Complete | technical-artist |

## Definition of Done

This epic is complete when:
- Pause menu intercepts Escape key and toggles `Time.timeScale` (0.0 <-> 1.0) without input leakage.
- Restart and resume actions restore game state cleanly.
- URP Volume profile applies Cold Watch visual aesthetic: Vignette intensity, Cold Moonlit color tint (`#A0C4E2`), subtle bloom threshold.
- Automated tests verify state transitions, volume configuration, and zero GC allocation on hot paths.
