# Story 006: AI Perception & Give-Up Pacing Tuning

> **Epic**: Guard AI & Perception (`production/epics/guard-ai-perception/EPIC.md`)  
> **Story ID**: `AI-TUNE-01`  
> **Status**: Complete  
> **Layer**: AI & Balance Tuning  
> **Type**: Balance / Configuration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-27  
> **Owner**: `ai-programmer`  

---

## Context

**GDD**: `design/gdd/perception.md`, `design/gdd/guard-ai-fsm.md` (Pacing, FOV, Giving-up Timer), `PLAN.md` (Tuần 6 — Tinh chỉnh AI)  
**Governing ADRs**: `ADR-0004` (Suspicion Meter), `CLAUDE.md` / `ai-code.md` rules  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Tuning & Balance Invariants**:
1. **Perception FOV & Range Balance**:
   - Refine horizontal Field of View: Target $90.0^\circ$ (down from overly punishing $100.0^\circ$) to provide realistic peripheral blind spots for player traversal.
   - Max detection distance: $12.0\text{ m}$ in bright light, attenuating smoothly with distance.
2. **Suspicion Accumulation vs Confirm Window**:
   - Calibrate rate of suspicion accumulation $R_{acc}$ so player has an intuitive $0.5\text{ s}$ to $0.8\text{ s}$ "reaction window" when glancing past edges of the vision cone before Investigate is triggered.
3. **Investigation Dwell & Give-Up Pacing**:
   - Search dwell scan duration: Maintain $4.0\text{ s}$ with rhythmic $\pm 45^\circ$ head/body turns.
   - Post-search give-up cleanly returns guard to patrol route, creating reliable, predictable openings for stealth infiltration.

---

## Acceptance Criteria

- [x] **AC-TUNE-01 — Vision Cone Field of View Calibration**: Guard vision sensor FOV tuned to $90.0^\circ$, granting fair peripheral stealth corridors.
- [x] **AC-TUNE-02 — Suspicion Reaction Curve**: Peripheral or brief vision glimpses accumulate suspicion gracefully rather than instigating immediate alarm.
- [x] **AC-TUNE-03 — Rhythmic Patrol Resumption**: Guard give-up behavior seamlessly returns to waypoint pathing without stutter or orientation pops.

---

## QA Test Cases

- `test_vision_cone_fov_calibrated_to_90_degrees`
- `test_glancing_vision_accumulation_respects_confirm_window`
- `test_giveup_pacing_cleanly_resumes_patrol_route`

---

## Test Evidence
- Unit Tests: `tests/unit/ai/ai_perception_tuning_test.cs`
