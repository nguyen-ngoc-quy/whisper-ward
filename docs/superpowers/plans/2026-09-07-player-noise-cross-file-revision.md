# Player Noise Cross-File Revision Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the 49 documentation blockers from the 2026-09-07 Player Noise review through one authority-driven sweep across eight canonical files without changing runtime code or fabricating evidence.

**Architecture:** `design/gdd/player-noise.md` is the behavioral authority, `design/registry/entities.yaml` is the value/schema/rejection authority, Perception and Guard AI FSM own their respective relay and liveness semantics, the fixture specification owns replay-record shape, the route fixture owns authored geometry, and the audit/Physics ADR own evidence and query-contract pins. The implementation flows in that order and ends with cross-file validation plus a fresh full design review.

**Tech Stack:** Markdown, YAML, PowerShell/Bash static checks, deterministic arithmetic checks, Git diff tooling, Unity 6 LTS `6000.3.17f1` documentation references. Runtime C# implementation, Unity execution, middleware capture, WebGL profiling, and target-hardware capture are out of scope.

## Global Constraints

- Keep Player Noise **In Review** until a fresh full `/design-review design/gdd/player-noise.md` returns **APPROVED**.
- Change documentation only; do not modify `src/` or add Unity metadata.
- Use the approved 3D pickup sphere: `distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2`, then E20 Linecast.
- Use authored landing **surface height** as F4 `h_landing`; record projectile-center height as a separate collision result.
- Require instrumented DSP onset evidence with `voice_instance_id` and `dsp_start_sample`; `AudioSource.timeSamples` is clip-relative and cannot be absolute DSP evidence.
- Treat MVP E20 geometry as static; Burst performs one explicit `Physics.SyncTransforms()` before each simulation batch after authored solids are finalized.
- Let FSM own the semantic suppressed-receipt event; presentation/audio only renders the micro-tell.
- Represent Investigate-to-Chase with `LivenessFact op=promote`, retaining the episode and `entry_id`.
- Keep MVP single-guard corroboration/re-anchor; keep peer-recruit and locked-zone Target-only.
- Sort guard selection by ascending `guard_eid` only; `stable_guard_snapshot_position` is recorded data, never a sort key.
- Preserve `UNCAPTURED`, `PENDING_OQ6`, `pending`, `estimated`, `unsupported`, and `limiter_unsupported` as non-passing states.
- Do not claim Unity, NUnit, WebGL, NavMesh, DSP, middleware, or target-hardware evidence unless executed in this checkout.
- Do not update systems-index/review-log approval status, commit, or push without explicit user instruction and a genuine APPROVED review.

---

## File map

| File | Responsibility in this plan |
|---|---|
| `design/gdd/player-noise.md` | Canonical source behavior, Burst admission, formulas, lifecycle, acceptance criteria |
| `design/registry/entities.yaml` | Registered values, schemas, identity/order tuples, validators, rejection codes |
| `design/gdd/perception.md` | Hearing geometry, deadline, relay, entry reservation, queue semantics |
| `design/gdd/guard-ai-fsm.md` | Corroboration/re-anchor, liveness, `promote`, semantic micro-tell ownership |
| `design/fixtures/noise-fixture-spec.md` | Replay schemas, parity fields, timing/identity, DSP and evidence states |
| `design/levels/mvp-burst-route-fixture.md` | Concrete pickup/route/geometry/patrol/negative records |
| `design/gdd/sound_performance_audit.md` | DSP provenance, limiter evidence, performance decomposition and gates |
| `docs/architecture/adr-0002-physics-collision-contract.md` | E20, PhysicsScene, initial overlap, SphereCast, sync, backface/winding authority |
| `docs/qa/player-noise-cross-file-revision-validation.md` | New validation record and blocker-to-gate matrix; no runtime pass is implied |
| `production/session-state/active.md` | Recovery checkpoint after each implementation milestone |

The review log and systems index are read-only during this plan. They change only after a later APPROVED gate.

---

### Task 1: Establish the blocker matrix and validation record

**Files:**
- Create: `docs/qa/player-noise-cross-file-revision-validation.md`
- Modify: `production/session-state/active.md` recovery checkpoint

**Interfaces:**
- Consumes: `docs/superpowers/specs/2026-09-07-player-noise-cross-file-revision-design.md`, the 2026-09-07 review memory, and the eight canonical files.
- Produces: A blocker matrix used by Tasks 2–7 and a validation report that records checks without converting uncaptured evidence into passes.

- [ ] **Step 1: Create the validation report skeleton**

Create the report with these exact sections:

```markdown
# Player Noise Cross-File Revision Validation

- Review source: 2026-09-07 fresh full design review
- Scope: documentation-only; Player Noise remains In Review
- Runtime/platform evidence: not captured unless explicitly listed below

## Blocker Matrix
## Locked Decisions
## Static Checks
## Numeric/Schema Checks
## Evidence Limitations
## Final Gate
```

Add a row for each review cluster A–G with columns `cluster`, `canonical authority`, `affected files`, `check`, `status`, and `notes`. Do not claim PASS before the corresponding task is verified.

- [ ] **Step 2: Record the locked decisions**

Record the six user decisions and the previously locked episode-open anchor and `guard_eid` ordering decisions. Include the exact pickup expression `distance_squared <= pickup_reach_radius^2`; do not reproduce the typographical `distance³` form.

- [ ] **Step 3: Update the recovery checkpoint**

Update `production/session-state/active.md` with the current task, the approved revision spec/plan paths, the eight-file scope, the six decisions, and the rule that status remains In Review. Do not overwrite the historical review record.

- [ ] **Step 4: Verify the baseline before edits**

Run:

```powershell
Select-String -Path design/gdd/player-noise.md,design/gdd/perception.md,design/gdd/guard-ai-fsm.md,design/registry/entities.yaml,design/fixtures/noise-fixture-spec.md,design/levels/mvp-burst-route-fixture.md,design/gdd/sound_performance_audit.md,docs/architecture/adr-0002-physics-collision-contract.md -Pattern 'THROW_WHILE_MOVING_REJECTED|INITIAL_OVERLAP_REJECTED|promote|AudioSource.timeSamples|stable_guard_snapshot_position|World layer|terminal_publication_time|PENDING_OQ6'
```

Expected: baseline hits are captured in the report as `before` references; no global replacement is performed from this command.

---

### Task 2: Normalize the canonical Player Noise contract

**Files:**
- Modify: `design/gdd/player-noise.md` Core Rules, Throw admission table, lifecycle/phase rules, F2/F4 sections, visual/audio ownership, AC6/AC10/AC12/AC14/AC15/AC18/AC19/AC20, and related amendments

**Interfaces:**
- Consumes: Task 1 matrix and the approved revision spec.
- Produces: The exact behavioral/formula wording that Tasks 3–7 propagate; no dependent file may contradict it.

- [ ] **Step 1: Locate every affected authority section**

Run:

```powershell
Select-String -Path design/gdd/player-noise.md -Pattern 'Canonical Throw admission|initial-overlap|pickup_reach|F4|F2|terminal_publication_time|corroboration|re-anchor|t_noise_recommit_cooldown|promote|micro-tell|AudioSource.timeSamples|AC6|AC10|AC12|AC14|AC15|AC18|AC19|AC20'
```

Expected: all matches are reviewed against the matrix before editing; historical/tombstone text is left labeled as non-authoritative.

- [ ] **Step 2: Replace the Throw admission contract**

Make the canonical table state:

```text
Idle/standing + planar speed = 0 -> eligible for launch-clear validation
Crouch-Idle -> same-tick stand resolution; blocked stand rejects before allocation/spend
Walk or Run label at any resolved speed, including zero -> reject before allocation/spend
Any resolved planar speed > 0 -> reject before allocation/spend
Initial overlap -> reject before spend; no flight, terminal, audio, or NoisePublished
```

Use stable codes `THROW_WHILE_MOVING_REJECTED` and `INITIAL_OVERLAP_REJECTED`. State that the initial query is an exact overlap operation in the injected PhysicsScene, with E20 and `QueryTriggerInteraction.Ignore`, not a swept SphereCast terminal.

- [ ] **Step 3: Normalize pickup reach and F4 datum wording**

State that prompt and `Interact` use the same 3D sphere predicate from player feet to pickup anchor plus E20 Linecast. State that F4 `h_landing` is authored surface height and that the pushed-out projectile center is a separate collision result. Do not use center height as the F4 oracle operand.

- [ ] **Step 4: Normalize timing, fixed ticks, and identity**

State that Burst `t_publish` and hearing deadline use `terminal_publication_time`; `source_timestamp` remains accepted-edge allocation/order metadata. State that timeout authority is integer fixed ticks, with float elapsed values diagnostic only. Require immutable `ThrowSnapshot` parity, explicit `stride_id` producer, and disjoint `step:`/`flight:` namespaces.

- [ ] **Step 5: Normalize corroboration and liveness**

Keep MVP re-anchor in scope; mark peer-recruit and locked-zone Target-only. Use the episode-open anchor and source-kind `t_publish` rules. State that cooldown applies only after resolution. State that Investigate→Chase emits `LivenessFact op=promote` retaining the current `entry_id`; do not emit a close/open pair for the same episode.

- [ ] **Step 6: Normalize micro-tell and audio evidence**

Make FSM the owner of the semantic suppressed-receipt event. Presentation/audio renders only the tell. Define AC19 evidence as `voice_instance_id` plus instrumented `dsp_start_sample`; explicitly reject `AudioSource.timeSamples` as absolute DSP proof. Keep confirmed-only as passing.

- [ ] **Step 7: Normalize acceptance criteria and evidence states**

Give AC6, AC10, AC12, AC14, AC15, AC18, AC19, and AC20 complete Given/When/Then legs with identity and actual/evidence fields. Preserve all unavailable/non-passing states and keep `PENDING_OQ6` non-passing. State the WebGL whole-frame gate separately from subsystem decomposition values.

- [ ] **Step 8: Run the canonical-document check**

Run:

```powershell
git diff --check -- design/gdd/player-noise.md
Select-String -Path design/gdd/player-noise.md -Pattern 'distance³|Walk/Run.*zero.*accept|initial overlap.*terminal|Target-only.*re-anchor|AudioSource.timeSamples.*absolute|stable_guard_snapshot_position.*sort'
```

Expected: no active contradictory wording; any intentional historical wording is explicitly struck or non-authoritative. Record the result in the validation report.

---

### Task 3: Synchronize the registry authority

**Files:**
- Modify: `design/registry/entities.yaml` pickup, Burst, timing, identity/order, hearing, liveness, audio, evidence, rejection-code, and fixture-schema entries

**Interfaces:**
- Consumes: Task 2 canonical contract.
- Produces: Registered values, fields, safe ranges, validator precedence, stable codes, and trace schemas consumed by Tasks 4–7.

- [ ] **Step 1: Locate the registry entries by exact identifier**

Run:

```powershell
Select-String -Path design/registry/entities.yaml -Pattern 'pickup_reach_radius|burst_throw|terminal_publication_time|ThrowSnapshot|initial-overlap|THROW_WHILE_MOVING_REJECTED|INITIAL_OVERLAP_REJECTED|guard_selection_order|stable_guard_snapshot_position|promote|voice_instance_id|dsp_start_sample|PENDING_OQ6|22 ms|E20'
```

Expected: every affected entry is identified before editing.

- [ ] **Step 2: Register the pickup and F4 contracts**

Add/align the pickup reach geometry as a 3D sphere with the E20 Linecast requirement. Mark F4 `h_landing` as surface height and register the projectile-center push-out as a separate result. Keep `projectile_radius`, `epsilon_contact`, and `contact_ambiguity_tolerance` independent.

- [ ] **Step 3: Register Throw admission and initial-overlap codes**

Add the exact Walk/Run-label rejection rule and the two stable codes. Record initial overlap as reject-before-spend with no flight terminal. Register the exact overlap operation, PhysicsScene binding, E20 mask, and trigger-ignore policy.

- [ ] **Step 4: Register identity, ordering, and liveness**

Align `step:`/`flight:` namespaces, `flight_handle_id` allocation, publication-time `fact_id`, `guard_eid`-only guard selection, fact/pair order tuples, and `promote` retention of `entry_id`. Remove any registry note that makes snapshot position an ordering key.

- [ ] **Step 5: Register timing, DSP, and evidence states**

Register `terminal_publication_time` as Burst hearing authority, integer fixed-tick timeout authority, instrumented DSP fields, and the exact passing/non-passing evidence enums. Clarify whether 22 ms is a default/floor or comparison threshold and prevent it from being used as an implicit universal gate.

- [ ] **Step 6: Check duplicate keys and YAML structure**

Run:

```powershell
git diff --check -- design/registry/entities.yaml
```

Then invoke any duplicate-key-aware YAML parser available in the environment. If none is available, record `NOT PERFORMED — parser unavailable`; do not infer duplicate-key safety from a normal YAML load. Record both results.

---

### Task 4: Align Perception and Guard AI FSM contracts

**Files:**
- Modify: `design/gdd/perception.md` hearing geometry, deadline, queue, relay, and liveness snapshot sections
- Modify: `design/gdd/guard-ai-fsm.md` corroboration, re-anchor, liveness, promotion, suppression, and response ownership sections

**Interfaces:**
- Consumes: Tasks 2–3 behavioral and registry contracts.
- Produces: Identical cross-system interpretations of each raw fact, relay, consumption outcome, and liveness transition.

- [ ] **Step 1: Locate direct contradictions**

Run:

```powershell
Select-String -Path design/gdd/perception.md,design/gdd/guard-ai-fsm.md -Pattern 'stable_guard_snapshot_position|guard_eid|feet|eye|terminal_publication_time|source_timestamp|corroboration|re-anchor|recommit|peer-recruit|locked-zone|promote|investigate-resolution|micro-tell|suppressed'
```

Expected: all contradictory matches are mapped to the Task 1 matrix.

- [ ] **Step 2: Align Perception ownership**

Make Perception own hearing geometry, source-kind deadline, ordered work, relay creation, and entry reservation. Use feet for falloff and eye only for Linecast endpoint. Use the same pickup sphere/E20 policy where pickup reach is described. Preserve immutable relay semantics and non-passing deadline-miss behavior.

- [ ] **Step 3: Align FSM ownership**

Make FSM own response precedence, consumption outcomes, corroboration/re-anchor behavior, liveness facts, `promote`, and semantic suppressed-receipt event. Keep MVP re-anchor and Target-only peer/locked-zone behavior. State that cooldown does not suppress live corroboration.

- [ ] **Step 4: Align ordering and identity prose**

Use `guard_eid` ascending only for guard selection and retain the independent fact and pair tuples. Require guard/fact identity on queue records. Preserve epoch barriers and avoid synchronous Perception→FSM queries.

- [ ] **Step 5: Run dependency checks**

Run:

```powershell
git diff --check -- design/gdd/perception.md design/gdd/guard-ai-fsm.md
Select-String -Path design/gdd/perception.md,design/gdd/guard-ai-fsm.md -Pattern 'position.*sort|sort.*position|Target.*re-anchor|MVP.*peer-recruit|AudioSource.timeSamples|close.*promote.*entry_id'
```

Expected: no active contradiction remains; record the check and any intentional historical tombstone.

---

### Task 5: Update the fixture specification

**Files:**
- Modify: `design/fixtures/noise-fixture-spec.md` raw/terminal/relay/queue/causality/route/AC19/AC20/AC21 schemas

**Interfaces:**
- Consumes: Tasks 2–4 exact fields and validator/rejection semantics.
- Produces: Replayable fixture records whose field names and evidence semantics match the registry and route fixture.

- [ ] **Step 1: Locate all affected schema records**

Run:

```powershell
Select-String -Path design/fixtures/noise-fixture-spec.md -Pattern 'NoisePublished|ThrowSnapshot|terminal_publication_time|initial overlap|pickup|guard_eid|stable_guard_snapshot_position|promote|voice_instance_id|dsp_start_sample|AC19|AC20|AC21|estimated|confirmed|unsupported|22 ms'
```

Expected: each affected schema is reviewed before modification.

- [ ] **Step 2: Add complete source and terminal identity fields**

Require source-kind timing, `step:<id>`/`flight:<id>` identity, accepted-throw `flight_handle_id`, publication-time `fact_id`, immutable ThrowSnapshot fields, and explicit terminal state. A death-cancelled Burst has cancellation timing but no NoisePublished/hearing deadline.

- [ ] **Step 3: Add pickup and initial-overlap records**

Require the 3D sphere predicate, E20 Linecast result, query policy, initial-overlap operation, stable rejection code, and no-spend/no-launch result. Keep initial overlap separate from wall/ceiling/void/death consumed-spend records.

- [ ] **Step 4: Align queue, ordering, and liveness schemas**

Require `queue_item_kind`, `guard_eid` for guard/fact pairs, full fact/source identity, deadline/retry/capacity state, deterministic order, and liveness `promote` retention of `entry_id`. Record micro-tell semantic event ownership as FSM.

- [ ] **Step 5: Align F4, DSP, tolerance, and evidence schemas**

Require surface height as F4 datum and separate projectile-center result. Require `voice_instance_id` and `dsp_start_sample` for confirmed onset. Keep `estimated`, `pending`, `unsupported`, `PENDING_OQ6`, and `UNCAPTURED` non-passing. Classify numeric tolerances as `gameplay_contract`, `oracle_quantizer`, or `pure_math_relative`.

- [ ] **Step 6: Align AC21 performance records**

Record the 33 ms p95 whole-frame WebGL gate separately from 2 ms/12 ms decomposition diagnostics. Mark the 30-guard stress profile diagnostic-only and retain unavailable evidence as non-passing.

- [ ] **Step 7: Check schema parity**

Compare every changed field against `entities.yaml` by exact name and update the validation report with missing, extra, or mismatched fields. Do not resolve a mismatch by silently aliasing two names.

---

### Task 6: Correct the concrete MVP Burst route fixture

**Files:**
- Modify: `design/levels/mvp-burst-route-fixture.md` pickup records, geometry variants, phase windows, receiver/landing probes, negative cases, gate causality, restart/segment lifecycle, and trace records

**Interfaces:**
- Consumes: Tasks 2–5 behavior, registry, and fixture schemas.
- Produces: A route fixture that is structurally consistent and keeps actual runtime evidence uncaptured unless genuinely captured.

- [ ] **Step 1: Locate all route records touched by the clusters**

Run:

```powershell
Select-String -Path design/levels/mvp-burst-route-fixture.md -Pattern 'pickup|reach|initial.overlap|valid_throw|negative_validation|receiver_probes|landing|phase_window|patrol|segment.reset|promote|investigate-resolution|terminal_publication_time|UNCAPTURED|actual'
```

Expected: the route records are mapped to the fixture schema before editing.

- [ ] **Step 2: Apply the 3D pickup sphere contract**

For each pickup interaction and route evidence record, require the player-feet-to-anchor squared-distance result and E20 Linecast result. Keep reach-entry telemetry separate from the explicit `Interact` transition. Preserve patrol-corridor clearance as a separate authoring invariant.

- [ ] **Step 3: Isolate geometry variants and F4 fields**

Ensure positive, wall, ceiling, void, initial-overlap, and ambiguous-collision variants cannot reuse incompatible solids. Record F4 surface height, center-result fields, `theta_rad` conversion/angle, selected positive root, contact mode, clearance, and stable geometry IDs.

- [ ] **Step 4: Correct patrol phase and receiver records**

Every gate receiver record must bind a declared `receiver_phase_window_id`, patrol state conditions, minimum validity, source-kind publication time, and intended MVP Investigate response. Do not shorten the window below its declared minimum to make a route appear valid.

- [ ] **Step 5: Correct lifecycle and causality records**

Use `promote` for Investigate→Chase continuity where applicable, retain `entry_id`, include source/fact/relay/decision identities, and distinguish segment reset, death cancellation, terminal publication, and full-room restart. Gate-open records bind `kind=burst` and the landing fact identity.

- [ ] **Step 6: Preserve fail-closed actual/evidence fields**

Keep expected values, actual values, provenance, and evidence status separate. Missing, malformed, contradictory, nominal-only, or uncaptured actuals fail closed. Do not insert measured numbers for Unity, NavMesh, DSP, or WebGL behavior.

- [ ] **Step 7: Run route structural checks**

Run:

```powershell
git diff --check -- design/levels/mvp-burst-route-fixture.md
Select-String -Path design/levels/mvp-burst-route-fixture.md -Pattern 'distance³|reach sphere.*cylinder|initial overlap.*Landed|kind: noise.*gate-open|actual: [0-9]|promote.*new entry_id'
```

Expected: no stale active route interpretation remains; any numeric actual is backed by existing provenance or remains explicitly unavailable.

---

### Task 7: Align performance audit and Physics ADR

**Files:**
- Modify: `design/gdd/sound_performance_audit.md` DSP, limiter, performance, pause/hitch, and evidence sections
- Modify: `docs/architecture/adr-0002-physics-collision-contract.md` E20, PhysicsScene, initial-overlap, SphereCast, sync, backface/winding, and NavMesh-layer sections

**Interfaces:**
- Consumes: Tasks 2–6 canonical behavior and schemas.
- Produces: Supporting documents that pin implementation/evidence constraints without becoming a second gameplay authority.

- [ ] **Step 1: Locate contradictory audit/ADR wording**

Run:

```powershell
Select-String -Path design/gdd/sound_performance_audit.md,docs/architecture/adr-0002-physics-collision-contract.md -Pattern 'AudioSource.timeSamples|DSP|voice_instance_id|dsp_start_sample|22 ms|33 ms|2\.0 ms|12\.0 ms|autoSyncTransforms|initial overlap|SphereCast|Linecast|World layer|backface|winding|NavMesh'
```

Expected: all matches are assigned to Cluster E or F before edits.

- [ ] **Step 2: Align audio evidence and performance gates**

Make the audit require instrumented DSP fields for confirmed onset, retain stock Unity onset as unsupported, distinguish whole-frame WebGL p95 from subsystem decomposition, and preserve all non-passing evidence states. Clarify pause deadline handling without inventing wall-clock gameplay progress.

- [ ] **Step 3: Align Physics authority**

Pin the exact initial-overlap operation, injected PhysicsScene, E20 mask and trigger policy, static MVP geometry, one Burst sync per batch, closest-hit swept SphereCast, backface/winding policy, contentful-scene validation, and NavMesh-layer separation. Keep pickup sphere/Linecast semantics linked to the Player Noise/registry authority.

- [ ] **Step 4: Run supporting-document checks**

Run:

```powershell
git diff --check -- design/gdd/sound_performance_audit.md docs/architecture/adr-0002-physics-collision-contract.md
Select-String -Path design/gdd/sound_performance_audit.md,docs/architecture/adr-0002-physics-collision-contract.md -Pattern 'AudioSource.timeSamples.*confirmed|33 ms.*subsystem gate|autoSyncTransforms.*per query|initial overlap.*terminal|every World layer'
```

Expected: no active contradictory authority remains. Record any unsupported runtime evidence explicitly.

---

### Task 8: Run the full cross-file validation gate

**Files:**
- Modify: `docs/qa/player-noise-cross-file-revision-validation.md` with results only
- Modify: `production/session-state/active.md` with the completed revision checkpoint

**Interfaces:**
- Consumes: all changed canonical files from Tasks 2–7.
- Produces: A truthful static validation record and a handoff ready for fresh design review.

- [ ] **Step 1: Check required sections and Markdown fences**

Run a heading scan for the eight required GDD sections in `player-noise.md` and fence-balance checks across all changed Markdown files. Expected: required sections remain present and fence counts are balanced.

- [ ] **Step 2: Run stale-term and contradiction scans**

Search all eight canonical files for the review contradiction families:

```powershell
Select-String -Path design/gdd/player-noise.md,design/gdd/perception.md,design/gdd/guard-ai-fsm.md,design/registry/entities.yaml,design/fixtures/noise-fixture-spec.md,design/levels/mvp-burst-route-fixture.md,design/gdd/sound_performance_audit.md,docs/architecture/adr-0002-physics-collision-contract.md -Pattern 'distance³|World layer family|stable_guard_snapshot_position.*sort|Walk.*zero.*accept|initial overlap.*terminal|Target.*re-anchor|AudioSource.timeSamples.*DSP|source_timestamp.*Burst deadline|promote.*new entry_id|22 ms.*universal'
```

Expected: no active contradictory wording; intentional historical text is labeled non-authoritative.

- [ ] **Step 3: Run schema and identity parity checks**

Compare registry fields to fixture-spec fields and route fields. Verify `step:`/`flight:` namespaces, `fact_id`/`entry_id` ownership, `guard_eid` pair identity, `promote` continuity, and stable rejection codes. Record mismatches as failures, not warnings.

- [ ] **Step 4: Run formula and geometry checks**

Recompute only deterministic expected values already present in canonical docs: F4 discriminant/root selection, `theta_rad` conversion, surface-vs-center relationship, pickup sphere boundary, and route phase-window arithmetic. Label checks by tolerance class and do not convert expected-only values into actual capture.

- [ ] **Step 5: Run YAML and whitespace checks**

Run:

```powershell
git diff --check
git status --short
```

Run duplicate-key-aware YAML validation if available. Record parser unavailability exactly if blocked.

- [ ] **Step 6: Update final validation status**

Mark a check `PASS` only when it ran and passed. Mark unavailable runtime/platform checks `UNCAPTURED`/`PENDING_OQ6`/`unsupported` as applicable. The report must state that this is a documentation-slice validation, not full MVP/release readiness.

- [ ] **Step 7: Update the recovery checkpoint**

Record changed files, validation results, remaining evidence limitations, and the next action: fresh full design review. Keep Player Noise and its tracking records In Review.

---

### Task 9: Fresh full design review handoff

**Files:**
- Read: `design/gdd/player-noise.md` and the current validation report
- Do not modify: systems index/review log unless the review genuinely returns APPROVED and the user authorizes that tracking update

**Interfaces:**
- Consumes: Task 8 validation report and all eight canonical files.
- Produces: A fresh full review verdict; only APPROVED can unlock approval tracking.

- [ ] **Step 1: Start a clean review context**

Run the full design-review workflow against `design/gdd/player-noise.md`, including all required specialists. Do not rely on the prior 2026-09-07 synthesis as a substitute for a fresh verdict.

- [ ] **Step 2: Preserve fail-closed review behavior**

If the verdict is NEEDS REVISION/MAJOR REVISION/PARTIAL, record the blockers and keep approval status unchanged. If a specialist is unavailable, report the panel as partial and do not claim a full approval.

- [ ] **Step 3: Apply the approval boundary**

Only after a genuine APPROVED verdict, and only with explicit user authorization, update systems-index/review-log approval records and follow the separate commit/push workflow. Otherwise stop with the revision handoff and list the remaining blockers.

---

## Plan self-review

- **Spec coverage:** The plan covers all locked decisions, all seven review clusters, all eight canonical files, the validation artifact, evidence limitations, and the fresh-review boundary.
- **Placeholder scan:** No `TBD`, `TODO`, “implement later”, or unassigned validation step is used. Parser unavailability is an explicit recorded outcome, not a placeholder.
- **Type/field consistency:** `distance_squared`, surface-height `h_landing`, `terminal_publication_time`, `voice_instance_id`, `dsp_start_sample`, `promote`, `entry_id`, `fact_id`, `guard_eid`, and the rejection codes are used consistently across tasks.
- **Scope check:** This remains one plan because every task is a documentation contract propagation step for the same Player Noise feature; no runtime subsystem is introduced.
- **Commit policy:** The plan deliberately omits commit steps because project instructions prohibit commits without explicit user authorization.
