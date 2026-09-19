# Player Noise Blocker Revision Design

**Date:** 2026-09-01  
**Scope:** Documentation and contract revision only  
**Review basis:** Fresh full review of `design/gdd/player-noise.md`  
**Approved scope:** All blocking findings; non-blocking advisories deferred

## 1. Goal

Synchronize the Player Noise behavioral contract across the canonical GDD, registry, fixture specification, level fixture, and directly contradictory dependency references. The revision must make timing, identity, ordering, formulas, tolerances, and evidence semantics deterministic without claiming unavailable runtime evidence.

## 2. Source-of-truth model

- `design/gdd/player-noise.md` is authoritative for behavior and player-facing rules.
- `design/registry/entities.yaml` is authoritative for registered identifiers, ranges, result codes, and trace-field contracts.
- `design/fixtures/noise-fixture-spec.md` is authoritative for executable fixture schemas, record identity, and tolerance labels.
- `design/levels/mvp-burst-route-fixture.md` is authoritative for authored geometry, route records, and level-owned causality.
- `design/gdd/perception.md`, `design/gdd/guard-ai-fsm.md`, `design/gdd/sound_performance_audit.md`, and the relevant ADRs are synchronized only where they contradict the canonical Player Noise contract.
- `design/gdd/systems-index.md` and `design/gdd/reviews/player-noise-review-log.md` are not changed by this revision. A later clean review owns status changes.

## 3. Canonical timing and identity contract

### 3.1 Timing

Every affected record carries the timing fields applicable to its source kind:

- `source_timestamp`: source input-edge timestamp used for source ordering only.
- `terminal_publication_time`: Burst landing/timeout publication timestamp; absent for movement facts.
- `t_publish`: derived publication time used by hearing:
  - Movement: `source_timestamp`.
  - Burst: `terminal_publication_time`.
- Burst deadline: `burst_hearing_deadline = terminal_publication_time + T_hearing`.

The Burst input timestamp is never substituted for the terminal publication timestamp when calculating hearing eligibility or corroboration. Corroboration uses the explicitly named gameplay/publication timeline field defined by the fixture schema.

### 3.2 Identity allocation

- `flight_handle_id` is allocated when an accepted Burst throw begins.
- `fact_id` is allocated when an accepted raw noise fact is published.
- Movement and Burst source namespaces are disjoint:
  - `step:<step_id>`
  - `flight:<flight_handle_id>`
- Monotonicity is scoped within each source namespace and `(session_id, attempt_epoch)`.
- `entry_id` remains opaque and Perception-owned; the FSM never allocates or reconstructs it.

### 3.3 Ordering and queue identity

Guard selection uses `(stable_guard_snapshot_position, guard_eid)`. Fact ordering uses source timestamp, source class rank, and source event ID. Pair dispatch uses an explicit deterministic pair key.

Queue rejection records include `queue_item_kind`, `guard_eid` when the item is a guard/fact pair, `fact_id`, source identity, `session_id`, `attempt_epoch`, deadline, retry count, queue depth/capacity, and stable rejection code. Retry, deduplication, admission, deferral, and rejection ordering is stated once and copied to the registry and fixture specification.

## 4. Formula and validation corrections

- F3 defines and logs `cadence_ratio = cadence_walk / cadence_run`, approximately `0.79` for starter values.
- F4 uses the stated raised-origin trajectory and the minimum strictly positive root for unequal-height landings.
- F4 root proof states `t_minus * t_plus = -2 * DeltaY / g`.
- F4 validator precedence is:
  1. finite/configuration-domain validation;
  2. angle validation;
  3. discriminant and strictly-positive-root validation;
  4. readable-range validation;
  5. authored-geometry validation.
  The first failing predicate supplies the stable rejection code.
- The registry re-anchor range is corrected to `[6.0, 8.0] s` for starter `s_diff = 1.0` and `[4.8, 11.6] s` over `s_diff in [0.7, 1.6]`.
- F6 distinguishes nominal eligible share `q` from applied delta `R_after - R_before`; the latter may be zero at the residual ceiling while the eligible share is recorded once.
- F12 movement payloads carry explicit `movement_mode` (`Walk` or `Run`) and resolved nominal radius. Perception applies vertical falloff to that payload value and never infers the mode from generic `kind=movement`.
- The registry release-height rationale states that raising release height increases fixed-landing-height range when the selected root remains valid.

## 5. Tolerance contract

Every numeric comparison is labeled with exactly one tolerance class:

- `gameplay_contract`: maximum `0.005 m`.
- `oracle_quantizer`: maximum `0.001 m`.
- `pure_math_relative`: registered relative precision `1e-6`.

The Burst simulation envelope tolerance remains separately named and is not silently treated as an oracle tolerance. Fixture records cannot use an unlabeled generic formula tolerance.

## 6. Document propagation

### 6.1 Player Noise GDD

Update F2, F3, F4, F6, F12, re-anchor bounds, source namespaces, identity allocation, guard ordering, queue records, corroboration timing, AC19, AC21, and affected acceptance criteria. Remove stale 12 ms gate wording while retaining 2 ms and 12 ms as decomposition diagnostics if still useful.

### 6.2 Entity registry

Synchronize identifiers, ranges, formulas, result-code precedence, timing fields, queue fields, movement mode, tolerance classes, release-height rationale, and fixture applicability. Keep Proposed ADRs and unresolved OQ3/OQ6 explicitly provisional.

### 6.3 Noise fixture specification

Add the terminal publication timestamp and source-kind timing mapping to raw, terminal, relay, queue, causality, and replay records. Add namespace-aware identity, movement mode, pair-level guard identity, tolerance labels, and confirmed-only AC19 semantics. Preserve all non-passing evidence states.

### 6.4 MVP Burst route fixture

Correct the unequal-height landing coordinate to approximately `(9.48483, 0.25, 13.98483)`. Align receiver, route, failed-spend, causality, and trace records with the revised schema. Keep the fixture `UNCAPTURED` until runtime capture exists.

### 6.5 Direct dependencies

Correct only contradictory timing, identity, formula, audio, performance, or ownership references in Perception, Guard FSM, audio/performance audit, and ADR cross-references. No unrelated mechanics are introduced.

## 7. Validation plan

1. Run static searches for stale timing terms, old re-anchor bounds, stale 12 ms gate language, the incorrect F4 denominator, reversed cadence ratio, and incorrect landing coordinates.
2. Verify schema field parity across GDD, registry, fixture, level, relay, queue, and trace records.
3. Recompute F3, F4, re-anchor, hearing-radius, and F6 boundary values.
4. Confirm eleven required fixture names remain present.
5. Confirm no unavailable evidence changes from `UNCAPTURED`, `PENDING_OQ6`, `pending`, `estimated`, or `unsupported` to a passing state.
6. Attempt duplicate-key-aware YAML validation if tooling is available; report the limitation if it is not.
7. Run `git diff --check`.

This validation establishes contract consistency only. It does not certify Unity compilation, NUnit, NavMesh behavior, Wwise/DSP onset, WebGL profiling, restart atomicity, Signal-A visibility, or captured level behavior.

## 8. Boundaries and follow-up

This revision does not modify C# runtime code, add Unity project metadata, resolve unresolved product questions outside the blocker scope, alter approval status, or commit/push automatically. After the edits and validation, a clean fresh `/design-review design/gdd/player-noise.md` is required. Only a genuine `APPROVED` result may change tracking records.

Deferred advisories include Burst economy pacing, movement teaching quality, Signal-A presentation, ghost occlusion, pickup siting, NavMesh edge behavior, audio attenuation, limiter scope, allocation budgets, and deeper Physics API abstraction.
