# Player Noise Blocker Revision — Validation Report

**Revision date**: 2026-08-31 → 2026-09-01
**Source specification**: `docs/superpowers/specs/2026-09-01-player-noise-blocker-revision-design.md`
**Implementation plan**: `docs/superpowers/plans/2026-09-01-player-noise-blocker-revision-plan.md`
**Validation date**: 2026-09-01
**Validation type**: static and numeric documentation validation only — **no runtime evidence exists or is claimed**

This report records what was mechanically verified about the blocker revision. It is
not runtime evidence and does not change any approval status. Player Noise remains
**In Review** pending a clean fresh full re-review.

## 1. Files checked

| File | Disposition |
|---|---|
| `design/gdd/player-noise.md` | Edited by revision (Tasks 1–2). Full numeric + static battery. |
| `design/registry/entities.yaml` | Edited by revision (Tasks 2–4 + post-review closures). Full numeric + static battery + `t_commit` enumeration. |
| `design/fixtures/noise-fixture-spec.md` | Edited by revision (Task 2). Full numeric + static battery. |
| `design/levels/mvp-burst-route-fixture.md` | Edited by revision (Task 2). Full numeric battery (both throw records recomputed from authored inputs). |
| `design/gdd/perception.md` | Edited by revision (Task 5). Static battery (ordering keys, hearing anchors, evidence vocabulary). |
| `design/gdd/sound_performance_audit.md` | Edited by revision (Task 5). Static battery. |
| `design/gdd/guard-ai-fsm.md` | Verified already canonical (Task 5 scan; no edits). |
| `docs/architecture/adr-0001-event-messaging-bus.md` | Verified already canonical (Task 5 scan; no edits; Proposed status untouched). |
| `docs/architecture/adr-0002-physics-collision-contract.md` | Verified already canonical (Task 5 scan; no edits; Proposed status untouched). |
| `design/gdd/reviews/player-noise-review-log.md` | **Not edited** by this revision (pre-existing working-tree changes preserved). |

No C# runtime file was modified by this revision. Pre-existing working-tree changes
elsewhere (C# sources, review log) are unrelated to this revision and were preserved.

## 2. Static scan results

**Stale-term pattern** `6.45 | 9.65757 | 14.15757 | 12.0 ms budget | run/walk | t_commit…Burst | source_timestamp…deadline | estimated…passing`
run across the seven edited/verified files. Every hit adjudicated; **zero true stale
terms remain**. Hit classes found:

- **Canonical negations** (correct as written): "Burst never uses it as its hearing deadline" (`player-noise.md:354`), "the throw timestamp does not start the hearing deadline" (`:362`), "never the deadline anchor for a Burst" (`:633`), "Burst hearing never substitutes source_timestamp" (`entities.yaml:797`), "NEVER the F2b deadline anchor" (`entities.yaml:950`).
- **Non-passing evidence vocabulary** (correct as written): `estimated`/`pending`/`unsupported` states documented as non-passing (`player-noise.md:3,880`; `entities.yaml:1028`; `noise-fixture-spec.md:224,230`).
- **Tombstones** (struck prior values, correct as written): "the earlier `(9.65757, 0.25, 14.15757)` was the flat-ground result projected diagonally and is struck" (`mvp-burst-route-fixture.md:1292`).
- **Canonical field enumerations / keys** (correct as written): full ordering keys at `player-noise.md:862`, `perception.md:607,612`, `entities.yaml:1294,1398`; flat-case landing coordinates at `mvp-burst-route-fixture.md:1289,1291`; registry `T_hearing`/`t_publish` notes at `entities.yaml:797,950`.

**Abbreviated ordering keys** `(source_timestamp, fact_id…)`: zero remaining across
`perception.md` and `sound_performance_audit.md`; all sites now carry the full
registered keys `(source_timestamp, source_event_class_rank, source_event_id, fact_id)`
and the pair form with trailing `guard_eid` (registry `hearing_order_fact_admission_key`
/ `hearing_order_pair_dispatch_key`, `entities.yaml:844–881`).

**Registry `t_commit` enumeration**: exactly **one** occurrence remains in
`entities.yaml` — line 1408, the substring `residual_at_commit` inside the registered
investigate-commit payload field list. This is the registered residual-at-commit field
name, not a hearing-deadline anchor; it is correct as written.

**Evidence-state vocabulary**: `UNCAPTURED` (level fixture actuals),
`PENDING_OQ6` (audit gates), `pending`/`estimated`/`unsupported` (onset evidence
states), `REQUIRED_RUNTIME_THROW_SNAPSHOT_ID` placeholders (level fixture) all
present and documented as non-passing. No `actual:` field anywhere in the checked
files records a passing state (`confirmed|passed|supported|captured` scan: zero hits).

## 3. Numeric validation battery — 33/33 PASS

All values recomputed from authored inputs with Python `math` (IEEE 754 double).

**Expected values and tolerance classes**

| Check | Expected | Computed | Tolerance class | Result |
|---|---|---|---|---|
| `cadence_walk = 3.6/1.9` | `1.894736842105263` | display-precision match (rel < 10⁻¹⁵) | pure_math_relative ≤ 1×10⁻⁶ | PASS |
| `cadence_run = 6.25/2.6` | `2.403846153846154` | display-precision match (rel < 10⁻¹⁵) | pure_math_relative ≤ 1×10⁻⁶ | PASS |
| `cadence_ratio = cadence_walk/cadence_run` | `0.788210526315789` | `0.7882105263157896` | pure_math_relative (agreement to ~1e-15) | PASS |
| Re-anchor starter, `s_diff=1.0`, extend 0 | `6.0` | exact | exact (1e-12) | PASS |
| Re-anchor starter, `s_diff=1.5`, extend 0 | `8.0` | exact | exact (1e-12) | PASS |
| Re-anchor full min (`s_diff=0.7`, factor 1.0) | `4.8` | exact | exact (1e-12) | PASS |
| Re-anchor full max (`s_diff=1.6`, factor 1.5, extend 2.0) | `11.6` | exact | exact (1e-12) | PASS |
| F4 root product `t_minus × t_plus = −2ΔY/g`, ΔY=1.25, g=9.81 | `−0.25484199796` s² | exact | exact (1e-11 display) | PASS |

**Burst throw-record recomputation** (authored inputs: launch `[1.5, 1.5, 6.0]`,
direction xz `[0.70710678, 0.70710678]`, `v0_mps 10.0`, `theta_deg 38.0`;
discriminant `vy² + 2gΔY`, `t_plus = (vy + √disc)/g`, `R = v_xz·t_plus`):

| Record | Quantity | Authored | Recomputed | Tolerance | Result |
|---|---|---|---|---|---|
| flat (`h_landing_m 0.0`) | disc, t_minus, t_plus, R | `67.3339…`, `−0.20888…`, `1.46405…`, `11.53688…` | agree to < 1e-6 relative | pure_math_relative ≤ 1×10⁻⁶ | PASS |
| flat | landing `(x, y, z)` | `(9.65757, 0.0, 14.15757)` | dist ≈ 3.3×10⁻⁴ m Euclidean (per-axis ≈ 0.23 mm) | gameplay_contract ≤ 5×10⁻³ m | PASS |
| unequal (`h_landing_m 0.25`) | disc, t_minus, t_plus, R | `62.4289…`, `−0.17784…`, `1.43301…`, `11.29226…` | agree to < 1e-6 relative | pure_math_relative ≤ 1×10⁻⁶ | PASS |
| unequal | landing `(x, y, z)` | `(9.48483, 0.25, 13.98483)` | dist < 1e-3 m | gameplay_contract ≤ 5×10⁻³ m | PASS |
| both | root-product identity `t_minus × t_plus = −2ΔY/g` | per-record | exact (1e-12) | pure_math_relative | PASS |

Note: the flat landing's ≈0.23 mm-per-axis authored-vs-exact deviation (≈0.33 mm
Euclidean; ledger Task 4 Minor 2) is inside the 5 mm `gameplay_contract` placement
tolerance; confirmed by this battery.

**Authored-literal presence** (docs author display roundings / recompute targets, not
full-precision strings — all present): F3 `1.8947`, `2.4038`, `1.8947 / 2.4038`;
re-anchor literals `4.8`, `11.6`, `6.0`, `8.0`; fixture unequal landing `9.48483`.

**Battery result: 33 checks, 33 PASS, 0 FAIL.** Itemization: 3 cadence + 4 re-anchor
+ 1 F4 root product + 13 throw-record checks (6 flat + 6 unequal + 1 root-product
cross-check against the plan's ΔY=1.25 value) + 7 authored-literal presence checks
+ 5 evidence-state checks = 33. (A first battery run reported 15
spurious FAILs; every one was traced to a script defect — ASCII hyphen vs U+2212,
string-equality instead of tolerance comparison, invented inputs, wrong marker-file
mapping — not to a document error. The corrected battery above recomputes from the
docs' own authored inputs.)

## 4. Whitespace

```
git diff --check   # tree-wide, working tree
```
Result: **PASS** (exit 0, no whitespace errors).

## 5. YAML parser availability and duplicate-key validation

- PyYAML is **not installed** in this checkout; no duplicate-key-aware YAML parser is available.
- **Duplicate-key validation: NOT PERFORMED** (the formerly duplicated `consumed_by` key cannot be re-verified mechanically here). This is recorded as a non-passing static gap, consistent with the Task 2 ledger entry. A duplicate-key-aware check (e.g. `PyYAML` with a duplicate-rejecting mapping constructor, or `ruamel.yaml`) should be run when tooling is available.

## 6. Explicit non-passing runtime/evidence list

The following remain **unavailable and are not claimed** anywhere in the revision:

- Unity compilation, editor play-mode, and Unity Test Framework / NUnit execution
- Burst trajectory fixture runtime execution and NavMesh runtime capture
- Level fixture runtime capture (`REQUIRED_RUNTIME_THROW_SNAPSHOT_ID` and all `actual: UNCAPTURED` fields remain placeholders)
- Wwise/DSP audio onset measurement (`PENDING_OQ6`; `estimated`/`pending`/`unsupported` remain non-passing)
- WebGL/target-hardware profiling (33.0 ms whole-frame p95 acceptance gate; 2.0 ms hearing/Burst/Perception slice and 12.0 ms aggregate are decomposition diagnostics — all `PENDING_OQ6`/uncaptured)
- Render-stall replay and deterministic replay identity
- Restart/reload atomicity runtime test
- Signal-A runtime visibility
- PerformanceFixture capture of any kind

Static and numeric validation (this report) does not substitute for any of the above.

## 7. Approval records

No approval record, review log, or approval status was changed by this revision or
this validation. Player Noise remains **In Review**; the required next step is a
clean fresh full `/design-review design/gdd/player-noise.md`, and only a genuine
`APPROVED` verdict may change tracking records. Nothing in this repository has been
committed or pushed as part of this revision.

## Traceability

- Task reports: `.superpowers/sdd/task-1-report.md` … `task-5-report.md`; progress ledger `.superpowers/sdd/progress.md` (Minor-findings ledger for final review triage)
- Canonical timing: `player-noise.md` F2/F2b; registry `noise_timing_contract` (`entities.yaml:794–800`)
- Ordering keys: registry `hearing_order_fact_admission_key` / `hearing_order_pair_dispatch_key` (`entities.yaml:844–881`)
- Tolerance classes: registry `gameplay_contract` ≤ 5×10⁻³ m, `oracle_quantizer` ≤ 1×10⁻³ m, `pure_math_relative` ≤ 1×10⁻⁶ relative
