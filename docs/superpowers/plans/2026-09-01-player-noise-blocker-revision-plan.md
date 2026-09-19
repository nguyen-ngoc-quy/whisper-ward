# Player Noise Blocker Revision Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Synchronize every blocking Player Noise timing, identity, ordering, formula, tolerance, fixture, and evidence contract without modifying runtime code or fabricating evidence.

**Architecture:** `design/gdd/player-noise.md` defines behavior; `design/registry/entities.yaml` defines registered identifiers, ranges, and trace fields; fixture specifications define replayable records and tolerance classes; the level fixture defines authored geometry and route causality. Changes flow from the GDD contract into the registry, fixture schemas, level records, and directly contradictory dependency references, followed by static and numeric validation.

**Tech Stack:** Markdown, YAML, PowerShell or Bash static checks, inline deterministic arithmetic, Git diff tooling. Unity 6 LTS `6000.3.17f1` and C# runtime code are out of scope for this documentation pass.

## Global Constraints

- Revise the blocker-only scope approved on 2026-09-01; defer non-blocking player-fantasy and polish advisories.
- Use `source_timestamp` for source/input-edge ordering only.
- Use `terminal_publication_time` for Burst terminal publication and `burst_hearing_deadline = terminal_publication_time + T_hearing`.
- Use `t_publish = source_timestamp` for movement and `t_publish = terminal_publication_time` for Burst hearing.
- Allocate `flight_handle_id` at accepted throw and `fact_id` at accepted raw-fact publication.
- Keep `step:<step_id>` and `flight:<flight_handle_id>` source namespaces disjoint and monotonic per namespace.
- Order guard selection by `(stable_guard_snapshot_position, guard_eid)`.
- Include `guard_eid` in every queue record representing a guard/fact pair.
- Carry explicit `movement_mode` (`Walk` or `Run`) and resolved nominal movement radius.
- Define `cadence_ratio = cadence_walk / cadence_run`, approximately `0.79` for starter values.
- Define F4 root product as `t_minus * t_plus = -2 * DeltaY / g`.
- Define starter re-anchor range as `[6.0, 8.0] s` and full configured range as `[4.8, 11.6] s`.
- Distinguish nominal residual share from applied clamped residual delta.
- Label numeric checks `gameplay_contract`, `oracle_quantizer`, or `pure_math_relative`.
- Preserve `UNCAPTURED`, `PENDING_OQ6`, `pending`, `estimated`, and `unsupported` as non-passing states.
- Do not modify C# runtime files, add Unity metadata, change approval status, or claim unavailable runtime evidence.
- Do not commit or push unless the user separately authorizes it.

---

## File map

| File | Responsibility in this plan |
|---|---|
| `design/gdd/player-noise.md` | Canonical behavior, formulas, acceptance criteria, and timing/identity rules |
| `design/registry/entities.yaml` | Registered ranges, result codes, trace schemas, and formula metadata |
| `design/fixtures/noise-fixture-spec.md` | Replayable fixture record schemas, identity rules, tolerance classes, AC19/AC21 semantics |
| `design/levels/mvp-burst-route-fixture.md` | Authored Burst volumes, landing coordinates, route causality, receiver and trace records |
| `design/gdd/perception.md` | Directly contradictory hearing terminology and movement-radius references |
| `design/gdd/guard-ai-fsm.md` | Directly contradictory re-anchor, relay, and lifecycle references |
| `design/gdd/sound_performance_audit.md` | Directly contradictory audio/performance evidence and gate language |
| `docs/architecture/adr-0001-event-messaging-bus.md` | Event Bus ordering/dedup cross-reference, without changing proposal status |
| `docs/architecture/adr-0002-physics-collision-contract.md` | Physics/query cross-reference, without changing proposal status |
| `docs/qa/player-noise-blocker-revision-validation.md` | Static and numeric validation results for this revision |

---

### Task 1: Update the canonical Player Noise contract

**Files:**
- Modify: `design/gdd/player-noise.md` at the F2/F3/F4/F6/F12 formula sections, timing and identity rules, queue rules, AC19/AC21, and related amendments

**Interfaces:**
- Consumes: Approved canonical contract in `docs/superpowers/specs/2026-09-01-player-noise-blocker-revision-design.md`.
- Produces: The behavior wording and formula names that Tasks 2–5 must copy exactly.

- [ ] **Step 1: Locate all stale contract wording**

Run:

```powershell
Select-String -Path design/gdd/player-noise.md -Pattern 't_commit|6\.45|12\.0|run/walk|source_timestamp|terminal_publication_time|entry_id|F2b|F3|F4|F6|F12|AC19|AC21'
```

Expected: every affected occurrence is reviewed; no occurrence is changed by blind global replacement.

- [ ] **Step 2: Add the source-kind timing matrix**

Add a compact table adjacent to the F2 hearing rule with exactly these mappings:

| Source kind | Source identity | `source_timestamp` meaning | `terminal_publication_time` | `t_publish` for hearing |
|---|---|---|---|---|
| Movement | `step:<step_id>` | movement commit/input-edge ordering | not applicable | `source_timestamp` |
| Burst | `flight:<flight_handle_id>` | accepted throw/input-edge ordering only | landing/timeout publication time | `terminal_publication_time` |

State explicitly that Burst hearing uses `burst_hearing_deadline = terminal_publication_time + T_hearing`, never the throw timestamp.

- [ ] **Step 3: Replace the identity and ordering paragraphs**

State that `flight_handle_id` is allocated at accepted throw, `fact_id` is allocated at accepted raw-fact publication, and `entry_id` is opaque and Perception-owned. State that monotonicity is per namespace within `(session_id, attempt_epoch)`. Replace guard selection wording with `(stable_guard_snapshot_position, guard_eid)` and retain separate fact and pair ordering keys.

- [ ] **Step 4: Correct formulas and boundary semantics**

Make the following exact definitions authoritative:

```text
cadence_ratio = cadence_walk / cadence_run ≈ 0.79

t_minus × t_plus = -2 × ΔY / g

t_reanchor = t_giveup_base × s_diff ×
              (1 + k_thorough × (R_reanchor / R_max)) +
              t_noise_reanchor_extend
```

Record re-anchor ranges `[6.0, 8.0] s` for starter `s_diff = 1.0` and `[4.8, 11.6] s` for `s_diff ∈ [0.7, 1.6]`. Define F6 `applied_delta = R_after - R_before` separately from nominal eligible share `q`. Define F4 validator precedence as configuration/domain, angle, discriminant/positive root, readable range, authored geometry.

- [ ] **Step 5: Make movement radius and queue identity explicit**

Require `movement_mode` (`Walk` or `Run`) and resolved nominal radius in movement payloads. State that Perception applies vertical falloff to that payload radius. Require `guard_eid` and `queue_item_kind` in pair-level queue rejection records, alongside fact/source identity, epoch, deadline, retry, queue state, and rejection code.

- [ ] **Step 6: Correct acceptance and evidence language**

Remove any wording that treats 12 ms as the AC21 gate; identify `33.0 ms p95` whole-frame WebGL performance as the gate and 2 ms/12 ms as decomposition diagnostics. Reconcile AC19 with confirmed-only passing onset and trace-only later confirmation. Preserve all unavailable evidence states as non-passing.

- [ ] **Step 7: Run a focused document check**

Run:

```powershell
Select-String -Path design/gdd/player-noise.md -Pattern '6\.45|12\.0 ms budget|run/walk|t_commit.*Burst|source_timestamp.*deadline'
```

Expected: no stale authoritative wording remains; any intentional historical/tombstone mention is clearly labeled non-authoritative.

---

### Task 2: Synchronize the entity registry

**Files:**
- Modify: `design/registry/entities.yaml` entries for re-anchor, F3/F4, timing, source identities, queue records, movement radius, tolerance classes, AC19, and AC21

**Interfaces:**
- Consumes: Canonical wording from Task 1.
- Produces: Registered names, ranges, result codes, and schemas consumed by Tasks 3–5 and validation.

- [ ] **Step 1: Locate registry entries by identifier**

Run:

```powershell
Select-String -Path design/registry/entities.yaml -Pattern 'reanchor_giveup_timeout|cadence_ratio|throw_release_height|terminal_publication_time|NoisePublished|Burst terminal|Queue rejection|movement_mode|AC19|AC21|tolerance|webgl_whole_frame_budget_p95_ms'
```

Expected: each listed registry entry is identified before editing.

- [ ] **Step 2: Correct numeric ranges and formula metadata**

Set the re-anchor starter range to `[6.0, 8.0] s` and safe-domain range to `[4.8, 11.6] s`. Set `cadence_ratio` to the explicitly named walk/run direction and approximately `0.79`. Replace the root-product expression with `-2 * DeltaY / g`. Correct the release-height rationale to state that increased release height increases range for fixed landing height when the selected root remains valid.

- [ ] **Step 3: Add the complete timing and queue fields**

Add `terminal_publication_time` wherever a Burst terminal/publication/deadline record requires it. Add `t_publish` semantics to the relevant trace descriptions. Add `guard_eid` and `queue_item_kind` to pair-level queue rejection schemas. Preserve session and epoch fields.

- [ ] **Step 4: Register identity, movement, and tolerance semantics**

Register `step:` and `flight:` namespace rules, accepted Burst `flight_handle_id` allocation, publication-time `fact_id` allocation, explicit `movement_mode`, resolved nominal radius, and the three named tolerance classes. Keep proposed ADR status and unresolved OQ3/OQ6 state unchanged.

- [ ] **Step 5: Validate YAML structure without assuming a parser**

Run an available duplicate-key-aware YAML validator. If no such parser is installed, record that limitation in the validation report. At minimum run:

```powershell
git diff --check -- design/registry/entities.yaml
```

Expected: no whitespace errors; duplicate-key status is reported accurately rather than inferred.

---

### Task 3: Synchronize the noise fixture specification

**Files:**
- Modify: `design/fixtures/noise-fixture-spec.md` raw, terminal, relay, queue, causality, AC19, AC20, and AC21 schema sections

**Interfaces:**
- Consumes: Registry fields from Task 2 and behavior from Task 1.
- Produces: Replayable records that can distinguish input ordering from publication/deadline timing and uniquely identify guard/fact work.

- [ ] **Step 1: Locate affected schemas and assertions**

Run:

```powershell
Select-String -Path design/fixtures/noise-fixture-spec.md -Pattern 'NoisePublished|Burst terminal|source_timestamp|terminal_publication_time|Queue rejection|guard_eid|AC19|AC21|estimated|confirmed|tolerance|step:|flight:'
```

Expected: all affected record definitions are reviewed before editing.

- [ ] **Step 2: Add source-kind timing and identity fields**

For movement and Burst records, distinguish `source_timestamp`, `terminal_publication_time` where applicable, and derived `t_publish`. Require `step:<step_id>` or `flight:<flight_handle_id>` source keys. Require publication-time `fact_id` for published facts and accepted-throw `flight_handle_id` for Burst lifecycle records.

- [ ] **Step 3: Make pair-level queue records unique**

Require `queue_item_kind`, `guard_eid` for guard/fact pairs, `fact_id`, source identity, `session_id`, `attempt_epoch`, deadline, retry count, queue depth/capacity, and rejection code. State that retry, deduplication, admission, deferral, and reject-newest ordering is deterministic.

- [ ] **Step 4: Reconcile AC19 and tolerance classes**

Define only `confirmed` as passing onset evidence. Define `estimated`, `pending`, and `unsupported` as non-passing. A later confirmed record updates trace history only and cannot retroactively convert an earlier result. Label every numeric comparison as `gameplay_contract`, `oracle_quantizer`, or `pure_math_relative`; do not describe pure-math checks with a generic 1 mm tolerance.

- [ ] **Step 5: Correct AC21 semantics**

State that `33.0 ms p95` whole-frame WebGL is the acceptance gate, while 2 ms and 12 ms are decomposition diagnostics. Keep diagnostic stress records free of gate-like pass/fail claims and preserve `PENDING_OQ6` when the required measurement is unavailable.

- [ ] **Step 6: Check fixture record parity**

Compare each affected fixture field against the registry by name. Expected: terminal publication, namespace, guard-pair, movement-mode, tolerance, AC19, and AC21 definitions match exactly.

---

### Task 4: Correct and align the MVP Burst route fixture

**Files:**
- Modify: `design/levels/mvp-burst-route-fixture.md` unequal-height positive record, receiver/route records, failed-spend records, causality fields, and trace schema references

**Interfaces:**
- Consumes: F4 and schema contracts from Tasks 1–3.
- Produces: Authored level records consistent with the corrected trajectory and replay schemas; evidence remains uncaptured.

- [ ] **Step 1: Locate the unequal-height record and dependent references**

Run:

```powershell
Select-String -Path design/levels/mvp-burst-route-fixture.md -Pattern '9\.65757|14\.15757|11\.292259321388022|unequal|launch_source_marker_id|source_pickup_id|terminal_publication_time|UNCAPTURED'
```

Expected: the stale coordinate and all dependent route/causality records are identified.

- [ ] **Step 2: Replace the stale unequal-height landing**

For launch position `(1.5, 1.5, 6.0)`, diagonal XZ direction `(0.70710678, 0.70710678)`, and range `11.292259321388022 m`, set the expected landing to approximately `(9.48483, 0.25, 13.98483)`. Retain the appropriate gameplay/oracle tolerance label.

- [ ] **Step 3: Align source and pickup identity names**

Use the registry-required `source_pickup_id` where the record identifies the pickup that caused the Burst. Preserve any separate launch marker only when it has a distinct documented purpose; do not use two names for the same identity.

- [ ] **Step 4: Align route and failed-spend trace fields**

Add terminal publication timing, receiver phase-window records, guard-pair identity where applicable, and the revised tolerance class. Ensure each failed-spend case has its own search-zone and route record rather than reusing incompatible geometry.

- [ ] **Step 5: Preserve evidence state**

Keep expected values separate from actual values. Do not change `UNCAPTURED`, `ACTUAL_CAPTURE_UNAVAILABLE`, or any other non-passing evidence state.

- [ ] **Step 6: Recompute the level fixture values**

Verify the following calculations independently:

```text
R_flat = 11.536878011794986 m
R_unequal = 11.292259321388022 m
x = 1.5 + R_unequal * 0.70710678 ≈ 9.48483
z = 6.0 + R_unequal * 0.70710678 ≈ 13.98483
```

Expected: the corrected expected coordinate agrees with the F4 oracle within its named tolerance; no actual runtime pass is claimed.

---

### Task 5: Synchronize direct dependency references

**Files:**
- Modify: `design/gdd/perception.md` only at contradictory movement-radius/timing references
- Modify: `design/gdd/guard-ai-fsm.md` only at contradictory re-anchor/relay/lifecycle references
- Modify: `design/gdd/sound_performance_audit.md` only at contradictory AC19/AC21/evidence language
- Modify: `docs/architecture/adr-0001-event-messaging-bus.md` only at Event Bus ordering/dedup cross-reference
- Modify: `docs/architecture/adr-0002-physics-collision-contract.md` only at Player Noise query cross-reference

**Interfaces:**
- Consumes: Final canonical wording from Tasks 1–4.
- Produces: No direct dependency document contradicts timing, ownership, evidence, or ordering semantics; ADR proposal status remains unchanged.

- [ ] **Step 1: Search for contradictory references**

Run:

```powershell
Select-String -Path design/gdd/perception.md,design/gdd/guard-ai-fsm.md,design/gdd/sound_performance_audit.md,docs/architecture/adr-0001-event-messaging-bus.md,docs/architecture/adr-0002-physics-collision-contract.md -Pattern 'source_timestamp|terminal_publication_time|t_commit|run/walk|12\.0|33\.0|estimated|confirmed|entry_id|guard_eid|re-anchor|reanchor'
```

Expected: only directly contradictory passages are changed.

- [ ] **Step 2: Align Perception and FSM terminology**

Use `t_publish` for hearing eligibility, feet-anchored effective radius with explicit movement mode/radius, opaque Perception-owned `entry_id`, and the canonical re-anchor timing. Do not introduce new FSM or NavMesh behavior beyond the blocker corrections.

- [ ] **Step 3: Align audio and performance audit language**

State that confirmed onset is the only passing audio state, middleware authority remains unresolved through OQ3, the 33 ms whole-frame WebGL value is the gate, and `PENDING_OQ6` is non-passing. Do not add a DSP-to-virtual-clock mapping that has not been approved.

- [ ] **Step 4: Align ADR cross-references without accepting them**

Make Event Bus references use the canonical identity/dedup/order vocabulary and Physics references use the canonical query/timing vocabulary. Leave both ADRs marked Proposed and retain their implementation-readiness caveat.

- [ ] **Step 5: Run a contradiction scan**

Expected: direct dependencies contain no stale authoritative interpretation of Burst hearing time, ratio direction, performance gate, audio passing state, or `entry_id` ownership.

---

### Task 6: Produce and review the validation report

**Files:**
- Create: `docs/qa/player-noise-blocker-revision-validation.md`

**Interfaces:**
- Consumes: Edited documents from Tasks 1–5 and the approved design specification.
- Produces: Auditable static/numeric results and an explicit list of unavailable evidence.

- [ ] **Step 1: Run repository-wide stale-term scans**

Run:

```powershell
Select-String -Path design/gdd/player-noise.md,design/registry/entities.yaml,design/fixtures/noise-fixture-spec.md,design/levels/mvp-burst-route-fixture.md,design/gdd/perception.md,design/gdd/guard-ai-fsm.md,design/gdd/sound_performance_audit.md -Pattern '6\.45|9\.65757|14\.15757|12\.0 ms budget|run/walk|t_commit.*Burst|source_timestamp.*deadline|−2.*DeltaY.*v0|estimated.*passing' -AllMatches
```

Expected: zero stale authoritative matches. Any retained historical statement must be explicitly marked non-authoritative.

- [ ] **Step 2: Run deterministic numeric checks**

Record these expected values in the report and compare them to the edited canonical records:

```text
cadence_walk = 3.6 / 1.9 = 1.894736842105263 events/s
cadence_run = 6.25 / 2.6 = 2.403846153846154 events/s
cadence_walk / cadence_run = 0.788210526315789

re-anchor starter: 4 × 1 × (1 + 0.5 × 0) + 2 = 6.0 s
re-anchor starter upper: 4 × 1 × (1 + 0.5 × 1) + 2 = 8.0 s
re-anchor full minimum: 4 × 0.7 × 1 + 2 = 4.8 s
re-anchor full maximum: 4 × 1.6 × 1.5 + 2 = 11.6 s

root product for DeltaY = 1.25, g = 9.81: -2 × 1.25 / 9.81 = -0.25484199796 s²
```

- [ ] **Step 3: Check evidence-state preservation**

Search all edited files for `actual:` and `PENDING_OQ6`. Expected: no unavailable record is converted into `confirmed`, `passed`, or another passing state.

- [ ] **Step 4: Run formatting and diff checks**

Run:

```powershell
git diff --check
```

Expected: exit code 0 and no whitespace errors.

- [ ] **Step 5: Write the validation report**

The report must include:

- Revision date and source specification path.
- Files checked.
- Static scan results.
- Numeric expected values and comparison tolerance class.
- YAML parser availability and duplicate-key validation result.
- Explicit non-passing runtime/evidence list: Unity, NUnit, NavMesh, level capture, audio onset, WebGL profiling, restart atomicity, and Signal-A visibility.
- Statement that approval records were not changed.

- [ ] **Step 6: Perform final scope review**

Verify that only the approved blocker files and validation report changed, no C# files changed, no approval/status records changed, and no commits or pushes were performed without authorization.

---

## Completion criteria

The plan is complete when:

1. GDD, registry, fixture spec, level fixture, and direct dependencies agree on all approved timing, identity, ordering, formula, tolerance, and evidence rules.
2. The incorrect re-anchor range, F4 landing coordinate, F4 root proof, F3 ratio direction, and release-height rationale are corrected.
3. Pair-level queue rejection and Burst deadline records are replayable from declared fields.
4. AC19 and AC21 remain fail-closed and consistent across all documents.
5. Static and numeric validation results are recorded in `docs/qa/player-noise-blocker-revision-validation.md`.
6. `git diff --check` passes.
7. No runtime evidence is fabricated and Player Noise remains `In Review` pending a clean fresh full design review.
