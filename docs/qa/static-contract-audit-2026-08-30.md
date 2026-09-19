# Static Contract Audit

**Date**: 2026-08-30  
**Scope**: read-only scan of canonical `design/gdd/*.md`, `design/registry/entities.yaml`, and `src/**/*.cs` for stale terms, duplicate YAML keys, ownership/schema consistency, phase/order semantics, S_DIFF, and 30x8 semantics.

## Check Results

- Stale-term scan: no canonical hits for `earliest_strictly_positive`, `earliest strictly positive`, or `earliest-positive`.
- `s_diff` appears as a starter/test-only literal in the source test harness and in a few canonical starter examples.
- `UNCAPTURED` and `PENDING_OQ6` are present as explicit placeholder/status vocabulary in the planning and QA surfaces, not as stray stale terms.
- Duplicate YAML keys: `duplicate-key: none`.
- Ownership/schema matrix: no cross-owner identity allocation or mutation was found in the canonical registry text.
- Phase/order semantics: re-check found the reviewer-corrected order contract surface in `design/fixtures/noise-fixture-spec.md:107`; SCA-002 is stale rather than open.
- 30x8 semantics: `stress_30_guard_8_fact` remains diagnostic-only; nothing in the canonical source claims it certifies the supported MVP budget.

## Findings

| finding_id | file | line | term/contract | expected | actual | owner | safe_fix_scope | severity | status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SCA-001 | `src/AI/Testing/FSMVerificationSuite.cs` | 49-59 | `S_DIFF` coverage | Exercise the configurable `s_diff` path, or explicitly mark the test as starter-only coverage. | Conflated ratio test removed in the 2026-08-31 baseline rewrite; `FSM_ReanchorBudget_Uses_Registered_Difficulty_Formula` now exercises `s_diff` 1.0 and 1.6 through the registry-current additive re-anchor budget on the real FSM path; the struck `reanchor_speed_ratio` form is intentionally not re-implemented (plan-vs-registry discrepancy recorded in the task-5 report). | FSM test harness / AI verification | yes | low | resolved |
| SCA-002 | `design/fixtures/noise-fixture-spec.md` | 107 | hearing queue admission order / trace tuple | Preserve the reviewer-corrected order contract surface and do not treat the tuple as absent in the current report. | `hearing_queue_admission_order` is explicitly documented in the fixture spec, and the earlier registry-span claim was stale. | Fixture/spec authoring | yes | info | resolved/stale |

## Notes

- No duplicate YAML keys were found.
- No owner/schema cross-allocation issue was found in the canonical registry text.
- A static audit alone does not provide OQ6 evidence or measured performance proof.

## Fix Round 1

- Changed artifacts:
  - `docs/qa/static-contract-audit-2026-08-30.md`
  - `.superpowers/sdd/2026-08-30-player-noise-contract-review-safe-plan/task-2-report.md`
- Command/evidence:
  - `rg -n "hearing_queue_admission_order" .` returned `design/fixtures/noise-fixture-spec.md:107`
  - Re-read `design/registry/entities.yaml:670-703` to confirm the trace schema block still carries `fact_id`, `entry_id`, `consumption`, `timestamp`, and related fields
  - Duplicate-key parser output: `duplicate-key: none`
- Output:
  - SCA-002 changed from open to resolved/stale
  - SCA-001 retained as an open harness-path note

## Fix Round 2 (consolidation alignment, 2026-09-01)

- Changed artifacts:
  - `src/AI/Testing/FSMVerificationSuite.cs` (synced from the main-checkout baseline 2026-08-31 09:44 rewrite)
  - `docs/qa/static-contract-audit-2026-08-30.md` (this SCA-001 update)
- Evidence:
  - The line 109-118 ratio test no longer exists in the synced suite; no test named for the `S_DIFF` ratio remains in `src/AI/Testing/FSMVerificationSuite.cs` or `FsmVerificationHarness.cs`.
  - `FSM_ReanchorBudget_Uses_Registered_Difficulty_Formula` asserts starter 6.8 (`s_diff` 1.0) and harder 9.68 (`s_diff` 1.6) through `ComputeCanonicalReanchorBudget` = `t_giveup_base × s_diff × (1 + k_thorough × (R/R_max)) + extension`, matching `entities.yaml` `reanchor_giveup_timeout` rev 2026-08-31.
- Output:
  - SCA-001: open → resolved (configurable `s_diff` path exercised by the FSM verification suite — an integration-style suite driving the real FSM through bus/clock/coordinator, not a helper-only unit test; the struck ratio form is not re-implemented, and the consolidation-plan-vs-registry wording discrepancy is recorded in the task-5 report).
