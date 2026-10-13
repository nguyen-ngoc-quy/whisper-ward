# Story 002: Stealth Grade Operator System

> **Epic**: Suspicion Meter HUD & Grade Operator (`production/epics/suspicion-meter-grade/EPIC.md`)  
> **Story ID**: `GRADE-01`  
> **Status**: Complete  
> **Layer**: Gameplay / Logic  
> **Type**: Logic / Core Math  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-04  
> **Owner**: `gameplay-programmer`  

## Context

**GDD**: `design/gdd/suspicion-meter-grade.md` (System #6 Suspicion Meter / Grade Operator, Rules 4..5, Formulas D4..D8)  
**Governing ADRs**: `ADR-0004` (Suspicion Meter & Grade Operator), `ADR-0001` (Event Bus)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Contracts**:
- **Base Score**: $S_{\text{base}} = 100.0\text{ pts}$.
- **Global Room Incident Penalty Pooling (D4)**:
  $$P_{\text{incident, room}} = \min(90.0,\ \sum N_{\text{chase}, i} \times 25.0)$$
  Chase escalations are pooled across all guards in the room to prevent penalty dilution.
- **Clean Distraction Exemption (D4 invariant)**: Fruitless sound investigations from Burst noise-maker deployments incur $0.0\text{ pts}$ direct scorecard penalty ($W_{\text{fruitless}} = 0.0$).
- **Residual Wariness Penalty (D5)**:
  $$P_{\text{residual, room}} = 10.0 \times \text{clamp}(R_{\text{avg}} / R_{\max},\ 0.0,\ 1.0)$$
- **Total Penalty Cap & Completion Floor (D6)**:
  $$P_{\text{total}} = \min(90.0,\ P_{\text{incident}} + P_{\text{residual}})$$
  $$S_{\text{room}} = \max(10.0,\ S_{\text{base}} - P_{\text{total}}) \quad \text{for completed runs}$$
- **Terminal Captured Outcome (D6)**:
  If player is captured by a guard, Final Score $= 0.0$, Status $= \text{Failed}$, Rank $= \mathbf{F}$.
- **Grade Letter Band Mapping & Rank S Zero-Chase Gate (D8)**:
  - **Rank S**: $S_{\text{room}} \ge 90.0 \land \sum N_{\text{chase}} == 0$. (Triggering even 1 Chase strictly disqualifies from Rank S).
  - **Rank A**: $S_{\text{room}} \ge 75.0$.
  - **Rank B**: $S_{\text{room}} \ge 60.0$.
  - **Rank C (Needs Improvement)**: $S_{\text{room}} < 60.0$.
  - **Rank F**: Failed / Captured.

---

## Acceptance Criteria

- [x] **AC-GRADE-01 — Base Score & Incident Pooling**: Base score starts at 100.0; each Chase incident deducts 25.0 pts, pooled globally with a 90.0 pt penalty cap.
- [x] **AC-GRADE-02 — Clean Distraction Exemption**: Fruitless noise investigations from Burst noise-maker deployments result in zero direct score deductions.
- [x] **AC-GRADE-03 — Rank S Zero-Chase Disqualification**: Achieving $\ge 90.0$ points awards Rank S ONLY if zero Chase escalations occurred; any Chase forces rank to A or lower.
- [x] **AC-GRADE-04 — Completion Floor & Capture Failure**: Successful extractions receive at least 10.0 points (completion floor); guard capture results in 0.0 points and Rank F.
- [x] **AC-GRADE-05 — Residual Wariness Penalty**: Exit residual wariness contributes up to 10.0 penalty points proportionally.

---

## QA Test Cases

- `test_grade_operator_clean_run_awards_rank_s`
- `test_grade_operator_single_chase_deducts_25_pts_and_disqualifies_rank_s`
- `test_grade_operator_clean_burst_distraction_incurs_zero_score_deduction`
- `test_grade_operator_global_penalty_cap_enforces_10pt_completion_floor`
- `test_grade_operator_capture_terminates_with_score_zero_and_rank_f`
- `test_grade_operator_residual_wariness_penalty_applied_correctly`

---

## Test Evidence
- Unit Tests: `tests/unit/gameplay/stealth_grade_operator_test.cs`
