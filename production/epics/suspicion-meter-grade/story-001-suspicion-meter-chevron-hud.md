# Story 001: Suspicion Meter & Threat Chevron HUD

> **Epic**: Suspicion Meter HUD & Grade Operator (`production/epics/suspicion-meter-grade/EPIC.md`)  
> **Story ID**: `UI-SUSP-01`  
> **Status**: Complete  
> **Layer**: UI & Presentation / Gameplay Feedback  
> **Type**: UI / Logic  
> **Estimate**: 3h (0.4d)  
> **Manifest Version**: 2026-10-04  
> **Owner**: `ui-programmer`  

## Context

**GDD**: `design/gdd/suspicion-meter-grade.md` (System #6 Suspicion Meter / Grade, Core Rules 1..3, lines 37..74)  
**Governing ADRs**: `ADR-0004` (Suspicion Meter & Grade Operator), `ADR-0001` (Event Bus)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Contracts**:
- **Dominant Threat Selection**: When multiple guards are active, HUD tracks each guard's threat ratio $r_{\text{threat}, i} = A_i / T_{\text{entry}, i}$. The dominant guard is $g^* = \arg\max(r_{\text{threat}, i})$.
- **Tie-Breaking Cascade**:
  1. Higher accumulator $A_i$.
  2. Closer physical proximity to player $d_{\text{Euclid}}$.
  3. String/ordinal comparison of Guard ID.
- **Zero-Threat Quiet Region**: If $\max(A_i) == 0.0$, HUD is completely hidden (`IsVisible = false`, chevron hidden) to prevent clutter and eliminate x-ray tracking through walls.
- **Planar Azimuth Threat Chevron**: Computed in camera-local space:
  $$\vec{p}_{\text{cam}} = \mathbf{R}_{\text{cam}}^{-1} \cdot (\vec{p}_{\text{guard}} - \vec{p}_{\text{camera}})$$
  $$\theta_{\text{azimuth}} = \text{atan2}(x_{\text{cam}}, z_{\text{cam}})$$
  Angle maps to screen ellipse ($R_x = 240\text{ px}, R_y = 160\text{ px}$) smoothly through 360° without flipping when $z_{\text{cam}} < 0$.
- **Dynamic Threshold Notch**: Notch position corresponds to $T_{\text{entry}}(R) / T_{\text{chase}}$, sinking lower as residual wariness $R$ builds.
- **Chase Lock**: When dominant guard enters `Chase` ($A \ge 1.0$ or `GuardState.Chase`), gauge locks into active pursuit alert.

---

## Acceptance Criteria

- [x] **AC-UI-01 — Dominant Threat Selection & Tie-Breaking**: Dominant guard is chosen by highest $r_{\text{threat}}$, correctly applying tie-breaking cascade (A -> distance -> ID).
- [x] **AC-UI-02 — Zero-Threat Quiet Region Suppression**: When all guards have $A = 0.0$, HUD gauge and directional chevron are completely suppressed (`IsVisible = false`).
- [x] **AC-UI-03 — 360° Planar Azimuth Projection Without Singularity**: Chevron azimuth rotates continuously through all quadrants $[-180^\circ, 180^\circ]$ without coordinate flipping when guard is behind the camera ($z_{\text{cam}} < 0$).
- [x] **AC-UI-04 — Dynamic Threshold Notch & Chase Lock**: Threshold marker dynamically sinks with residual wariness $R$, and transitions to Chase locked pursuit mode when $A \ge 1.0$ or guard enters Chase.

---

## QA Test Cases

- `test_dominant_threat_selection_picks_highest_ratio`
- `test_dominant_threat_tie_breaker_accumulator_priority`
- `test_dominant_threat_tie_breaker_distance_priority`
- `test_quiet_region_hides_hud_when_all_suspicion_zero`
- `test_hud_becomes_visible_when_suspicion_positive`
- `test_planar_azimuth_chevron_handles_front_and_behind_camera`
- `test_dynamic_threshold_notch_sinks_with_residual_wariness`
- `test_chase_mode_locks_hud_gauge`

---

## Test Evidence
- Unit Tests: `tests/unit/ui/suspicion_meter_hud_test.cs`
