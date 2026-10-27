# Story 002: Cold Watch URP Post-Processing & Lighting Profile

> **Epic**: UI Presentation & Visual Polish (`production/epics/ui-presentation/EPIC.md`)  
> **Story ID**: `VFX-POST-01`  
> **Status**: Complete  
> **Layer**: Visual / Post-Processing  
> **Type**: Rendering / Configuration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-27  
> **Owner**: `technical-artist`  

---

## Context

**GDD**: `PLAN.md` (Tuần 7 — Lighting & Post-processing), `design/gdd/level-playground.md` (Cold Watch Moonlit Visual Style)  
**Governing ADRs**: `ADR-0002` (URP Volume Profile Setup)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Visual Architecture Contracts**:
1. **Cold Watch Palette**:
   - Moonlit directional light: `#A0C4E2` (Intensity $0.85$, Soft Shadows).
   - Ambient color grading tint: Cool cyan-blue (`#94B6C9`) to enhance the isolation and stealth atmosphere.
2. **Post-Processing Overrides**:
   - **Vignette**: Intensity $0.35$, Smoothness $0.50$ (darkens screen edges, focusing player attention on the stealth zone).
   - **Bloom**: Threshold $1.10$, Intensity $0.65$ (softly blooms alarm stingers, security lasers, and keycard indicators without washing out dark shadows).
   - **Tonemapping**: Neutral / ACES to preserve deep darks and prevent clipped speculars.

---

## Acceptance Criteria

- [x] **AC-POST-01 — Cold Watch Volume Overrides**: Programmatic configuration provides verified Vignette, Bloom, and Color Adjustment parameter sets.
- [x] **AC-POST-02 — Moonlit Directional Lighting Setup**: Directional moonlight delivers soft contrast without blinding shadows.
- [x] **AC-POST-03 — WebGL Shader Compatibility**: Post-processing parameters maintain 60 FPS performance on WebGL target without heavy compute shaders.

---

## QA Test Cases

- `test_cold_watch_profile_generates_valid_vignette_parameters`
- `test_cold_watch_bloom_threshold_preserves_shadow_depth`
- `test_cold_watch_color_tint_matches_moonlit_palette`

---

## Test Evidence
- Unit Tests: `tests/unit/rendering/cold_watch_post_process_test.cs`
