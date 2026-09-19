# Noise Fixture Specification

> **Status**: **Authoritative harness spec (2026-08-30; rev 2026-08-31 — AC19
> pass/fail mapping, gate-trigger binding, level-manifest self-consistency
> records, AC21 whole-frame gate)** — referenced by every
> noise-domain fixture in `player-noise.md` acceptance criteria. Defines the
> master conventions for `NoiseEmitterFixture`, `EventBusFixture`,
> `PerceptionHearingFixture`, `BurstLifecycleFixture`, `ConfigValidatorFixture`,
> `AudioFeedbackFixture`, `LevelFixture`, `HideSpotFixture` (Target-tier),
> `PerformanceFixture`, and cross-system observability fixtures (`FsmFixture`,
> `PerceptionFixture`).
>
> **Schema**: `WW-NOISE-FIXTURE-SPEC-1.1`
> **Validator**: `NoiseFixtureSpecValidator.v1`
> **Profile**: MVP
> **Owner**: Player Noise GDD (qa-lead co-sign)
> **Consumed by**: every Player Noise acceptance criterion, every cross-system
> noise/perception/FSM integration criterion
>
> **Cross-doc authority note (lifecycle / attempt_epoch).** The canonical
> lifecycle transition table for `attempt_epoch` increments — covering death,
> capture, respawn, segment reset, full-room restart, scene reload, new
> playable attempt, pause/resume, and pool/unpool — is authoritative in
> `design/registry/entities.yaml` and reproduced verbatim in
> `player-noise.md §Ownership`. This fixture spec consumes the same table; no
> fixture owns a distinct version.

---

## Overview

The Player Noise GDD is a contract with **observable, deterministic, virtual-clock-bound** behavior. The acceptance criteria are spread across 11 named fixtures, and each must agree on a small set of harness conventions: schema versions, virtual-clock injection, comparison tolerance, and trace field contracts. **`NoiseFixtureSpec` is the master specification** that pins those conventions so cross-fixture parity is testable and a failing fixture is always localizable.

The spec also enumerates the **mandatory fixture set** for the MVP noise-domain test battery. Every acceptance criterion in `player-noise.md` and the cross-system criteria in `perception.md` / `guard-ai-fsm.md` that depends on a noise event must name the owning fixture. Cross-system criteria do not simulate cross-system behavior inline; they name the required fixture explicitly.

## Player Fantasy (Harness)

A QA tester or CI runner should be able to run the noise-domain battery with one command, see all 11 fixtures execute in a defined order, and inspect a serialized record that is identical across runtime, preview, fixture, and replay. Failing assertions are localizable to one fixture, one criterion, and one expected/actual pair. There is no silent drop, no hidden runtime tolerance, no listener/thread arrival order dependence.

## Detailed Rules

### Mandatory Fixture Set (11 fixtures)

| # | Fixture | Owns | Cross-system? | Required ACs |
|---|---------|------|---------------|--------------|
| 1 | `NoiseEmitterFixture` | Valid `step_event` injection, Burst input, raw `busSpy` capture | No | AC1, AC1b, AC2, AC2b, AC3, AC4 (source-side leg), AC5 (publish leg), AC6, AC6a, AC6b, AC7, AC8, AC9, AC9b |
| 2 | `EventBusFixture` | Active-listener registration, publisher order, retry, dedup | Yes (Bus ↔ Perception) | AC4 (bus-side leg), AC5 (handoff leg), AC5 queue overflow supplements |
| 3 | `PerceptionHearingFixture` | Virtual clock, scheduler phase, due-boundary predicate, `relaySpy` capture | Yes (Perception ↔ FSM) | AC5, AC12-P, AC13, AC15-MVP, AC15-Target, AC15b-MVP saturation supplements |
| 4 | `BurstLifecycleFixture` | Pickup ids, carried slot, checkpoint snapshot, segment reset, death, collision/timeout stimuli | No (Player ↔ Burst) | AC6, AC6a, AC6b, AC7, AC8, AC9, AC9b, AC15-MVP (death-during-flight leg) |
| 5 | `ConfigValidatorFixture` | Config injection, load-report writer, stable failure codes | No | AC10, AC11, AC11b |
| 6 | `AudioFeedbackFixture` | Gameplay commit time, DSP sample onset, cue identity, accessibility modes, internal proxy dedup | Yes (Audio ↔ FSM) | AC16, AC17, AC19 |
| 7 | `LevelFixture` | Canonical MVP room manifest, route/receiver/occlusion/throw/failed-spend evidence | No (Level) | AC20 |
| 8 | `FsmFixture` (Approved FSM rev 4.1) | `exposeFsmStateSpy {guard_eid, state, entry_id, source}` | Yes (FSM) | AC12, AC12-P, AC15b-MVP, AC15-Target, AC-FSM-EX (LivenessFact) |
| 9 | `PerceptionFixture` (cross-system) | `exposeResidualSpy {guard_eid, R, dA_dt, residual_add, source}` | Yes (Perception F6) | AC18 (CR7 R_noise_share) |
| 10 | `HideSpotFixture` (Target-tier only) | Authored `Occupied` spot, hunch/no-break probe | No | AC14-Target |
| 11 | `PerformanceFixture` | Supported-MVP and diagnostic stress timing envelope | Yes (runtime slice) | AC21 |

`NoiseFixtureSpec` itself is the **master specification** that pins conventions used by all 11 fixtures. The MVP run includes fixtures 1–9 and 11; `HideSpotFixture` is Target-tier only and is excluded from MVP runs while its Target-only ACs are recorded as out of scope. It is the source of truth for `trace_contract`, comparison tolerances, and virtual-clock injection; fixtures consult it, they do not redefine it.

### Harness Conventions

#### Virtual-clock injection (mandatory)

All gameplay-time fixtures use **one injected monotonic virtual clock** with paused elapsed time excluded. Pooling/`OnEnable`/`OnDisable` does not shift the phase. The AC21 performance harness is the **sole** exception: it measures real wall-clock frame work against the whole-frame `webgl_whole_frame_budget_p95_ms` acceptance gate and replays the 33 ms WebGL hitch; all trajectory/hearing determinism stays virtual-time.

#### Trace field contracts (mandatory)

Every fixture records the fields required by the record type below. Fields shared by
all records are `session_id`, `attempt_epoch`, `source_timestamp` when applicable,
`terminal_publication_time` when applicable, `t_publish` when the record participates
in hearing eligibility, `publisher`, `evaluated_at` when evaluated, and
`epoch_transition_id` when a lifecycle barrier is involved. Omitted fields are not
synthesized with sentinel values. Every record keeps its normative `expected` values
separate from `actual` evidence; `actual` is only `UNCAPTURED` or a captured,
typed record-specific object.

**Identity types (mandatory):**

| Field | Type | Owner and rule |
|-------|------|----------------|
| `session_id` | `SessionId` | Lifecycle/session owner; constant per session |
| `attempt_epoch` | `AttemptEpoch` | Lifecycle/session owner; canonical transition table |
| `fact_id` | `ulong` | NoiseEmitter transport/dedup token, allocated at accepted raw-fact publication |
| `entry_id` | opaque `string` | Perception episode id, allocated at first eligible hearing admission |
| `step_id` | `ulong` | Controller movement source identity |
| `flight_handle_id` | `ulong` | Burst accepted-throw source identity, allocated at the accepted Throw edge |
| `guard_eid` | `string` | Stable guard identity |
| `epoch_transition_id` | `ulong` | Lifecycle/session owner; increments on epoch change |

**Source identity and timing mapping (mandatory):**

| Source kind | Source key | `source_timestamp` | `terminal_publication_time` | `t_publish` |
|---|---|---|---|---|
| Movement | `step:<step_id>` | Controller commit/input-edge ordering time | Not applicable | `source_timestamp` |
| Burst | `flight:<flight_handle_id>` | Accepted Throw input-edge ordering time only | Landing/contact or timeout publication time | `terminal_publication_time` |

The `step:` and `flight:` namespaces are disjoint and monotonic within each
`(session_id, attempt_epoch, namespace)`. `source_timestamp` is never replaced by
relay-arrival time. For Burst, `terminal_publication_time` is the hearing-deadline
anchor; the input timestamp is never substituted for it.

**`NoisePublished` record:** `kind`, `source`, `movement_mode`,
`resolved_nominal_radius`, `position`, `radius`, `source_timestamp`,
`terminal_publication_time`, `t_publish`, `source_event_class_rank`
(`movement=0`, `Burst=1`), `source_event_id` (`step_id` or `flight_handle_id`), `fact_id`, `timestamp`,
`publisher`, and `fact_publication_state ∈ {published, rejected, invalidated}`.
`movement_mode` and `resolved_nominal_radius` are required for movement and not
applicable to Burst. A published record requires nonzero `fact_id`; zero is never a
pending sentinel.

**`NoiseHeardRelay` record:** the immutable raw noise fields plus `session_id`,
`attempt_epoch`, `fact_id`, opaque string `entry_id`, `source_timestamp`,
`terminal_publication_time`, `t_publish`, `source_event_class_rank`,
`source_event_id`, `evaluated_at`, `residual_at_hearing`, `guard_eid`,
`consumption=eligible`, `timestamp`, and `publisher`. `movement_mode` and
`resolved_nominal_radius` remain present for movement and not applicable to Burst.
The admission relay is immutable; final `consumed`, `ignored`, or `stale` status is
a separate FSM-owned keyed outcome record and is never written back onto the relay.
For both schemas, `timestamp` is required transport/trace metadata only; it does not
replace `source_timestamp`, `terminal_publication_time`, or `t_publish`, and it has no
gameplay eligibility or ordering authority.

**Burst terminal record:** `session_id`, `attempt_epoch`, `flight_handle_id`,
`source_timestamp`, `terminal_publication_time`, `t_publish`, `terminal_event ∈
{Landed, TimedOut, DeathCancelled}`, `flight_elapsed_s`, `contact_fraction`,
`contact_event_time`, `timeout_boundary_s`, `comparison_result`,
`terminal_position`, `fact_publication_state ∈ {not_applicable, pending, published,
rejected, invalidated}`, nullable `fact_id`, `timestamp`, and `publisher`.
Contact fields are null for timeout/death cancellation as specified by the registry.
Pending or non-published terminals use `fact_id = null`, never numeric zero; a
published terminal requires a nonzero fact id and `t_publish =
terminal_publication_time`.

**Queue rejection record:** `session_id`, `attempt_epoch`, `queue_item_kind`,
`guard_eid` (required for a guard/fact pair and nullable otherwise), `source_kind`,
`source_timestamp`, `terminal_publication_time`, `t_publish`, `deadline`,
`source_event_id`, `fact_id`, `queue_name`, `queue_state`, `queue_depth`,
`queue_capacity`, `retry_count`, `rejection_code`, `timestamp`, and `publisher`.
For hearing work, `deadline = t_publish + T_hearing`. Retry/backpressure occurs
before rejection; the deterministic policy is retry, deduplicate, admit, defer, then
reject-newest after retry exhaustion. Older queued work is preserved, and a rejected
item creates no relay, decision, or passing evidence.

**`LivenessFact` record:** `session_id`, `attempt_epoch`, `guard_eid`, opaque string
`entry_id`, `tier ∈ {Investigate, Chase}`, `op ∈ {open, close}`, `cause`,
`position`, `source_timestamp`, `timestamp`, `evaluated_at`, and
`publisher=GuardAISystem`. Each open has exactly one matching terminal or stale
close.

**Pickup/Gate record:** `pickup_id`, `flight_handle_id` when applicable,
`throw_snapshot_id`, `landing_surface_id`, `gate_id`, transition result/code,
`tolerance_class` when a position is compared, and `actual` evidence state. Every
**gate trigger** record is self-identifying about its trigger stream: it carries
`trigger_noise_kind ∈ {Movement, Burst}` plus the responsible source id
(`landing_fact_id` for Burst, `step_id` for Movement). A gate whose certification
requires a Burst landing fails closed if its trigger record names no fact, a
null `landing_fact_id`, or a Movement `step_id` (rev 2026-08-31).

These per-record schemas are the authoritative trace contract; a fixture may add
non-authoritative diagnostics but may not change identity types or ownership.

#### Comparison tolerance (mandatory)

| Comparison class | Tolerance | Source |
|------------------|-----------|--------|
| `gameplay_contract` (`Vector3` position, XZ distance, throw contact, re-anchor origin, route waypoints) | `CompareTolerance ≤ 5×10⁻³ m (5 mm)` | `entities.yaml` `fixture_tolerance_classes` |
| `oracle_quantizer` (expected-vs-computed static-oracle position/range records) | `≤ 1×10⁻³ m` absolute (1 mm) | `entities.yaml` `fixture_tolerance_classes`; NOT a gameplay tolerance |
| `pure_math_relative` (F4/oracle formula, discriminant, root, and pure-math range checks) | `≤ 1×10⁻⁶` relative | `entities.yaml` `fixture_tolerance_classes`; never a gameplay tolerance |
| Trace field identity | exact | Deterministic tuple |
| Audio onset (AC19) | `audio_cue_onset_tolerance_ms = 20 ms` default (registered range `[10, 40]` ms) | Verified against injected virtual clock |
| AC21 acceptance gate (whole frame, rev 2026-08-31) | `webgl_whole_frame_budget_p95_ms = 33.0 ms` p95 at the WebGL 30 fps floor — whole-frame wall-clock | Asserted, not yet measured (OQ6); `PENDING_OQ6` is non-passing |
| Hearing/Burst/Perception slice (AC21 decomposition record) | `≤ 2.0 ms` p95/p99 for `supported_mvp` (1 guard / 1 fact / 1 flight); `stress_30_guard_8_fact` is diagnostic only | Asserted, not yet measured (OQ6); not the whole-frame gate |
| Aggregate WebGL subsystem slice (AC21 decomposition record) | `≤ 12.0 ms` p95/p99 — diagnostic decomposition, **NOT** the acceptance gate | Asserted, not yet measured (OQ6) |
| Stall magnitude | `stall_magnitude_budget_ms = 33` ms | Per-frame ceiling |
| Stall rate | `stall_rate_cap_per_s` sliding 1-second window | Separate from steady-state budget |

The 5 mm gameplay tolerance is the **gate**: anything looser is a known limitation surfaced in the load report; anything tighter is a fixture-oracle precision target, not a gameplay claim. The 1 mm oracle tolerance is the simulator-oracle precision target — it proves the simulator matches the formula at oracle precision, but no gameplay path is allowed to assume sub-millimetre physical precision.

#### Queue admission order (mandatory)

The Perception hearing queue admits in this exact order, shared by all fixtures:

1. Selected guard set is the **`stable_guard_snapshot`** taken at the boundary's perception-tick start (NOT the active subscription), ordered by `stable_guard_snapshot_position` with `guard_eid` as the stable tie-break.
2. Facts admitted to that snapshot are processed in `(source_timestamp, source_event_class_rank, source_event_id, fact_id)` order.
3. Guard/fact pairs are processed in `(source_timestamp, source_event_class_rank, source_event_id, fact_id, guard_eid)` order.

Listener/thread arrival order, subscription churn mid-boundary, queue insertion order, and per-listener registry mutation are **NOT** identity sources. The order is `hearing_queue_admission_order` in `entities.yaml` and is read-only to fixtures.

The three component keys are asserted independently: guard selection
`(stable_guard_snapshot_position, guard_eid)`, fact admission
`(source_timestamp, source_event_class_rank, source_event_id, fact_id)`, and pair
dispatch `(source_timestamp, source_event_class_rank, source_event_id, fact_id,
guard_eid)`. The source-allocation key `(source_timestamp,
source_event_class_rank, source_event_id)` is separate and is used only before
`fact_id` exists. The same deterministic retry, deduplication, admission, deferral,
and reject-newest policy applies to every bounded queue; listener/thread arrival
never changes identity or order.

#### Fixture applicability and validator ownership

| Fixture/profile | Applicability | Out-of-scope handling |
|-----------------|---------------|----------------------|
| MVP fixtures 1–9 and 11 | Required for MVP battery | Missing or failed evidence is a fixture failure |
| `HideSpotFixture` | Target-tier only | Target-only ACs are recorded `out_of_scope`, never silently passed |
| `ConfigValidatorFixture` | Runtime config and safe-range contracts | Emits only `CFG_*` codes |
| `LevelFixture` | Authored route/content and manifest contracts | Emits only Level/route result codes; it cannot validate runtime config |

A runtime-config failure is never substituted by a level validation result, and a
content failure is never substituted by a config result. Both validators fail
closed on unknown fields and preserve `UNCAPTURED` evidence.

#### AC19 onset report semantics (mandatory)

`AudioFeedbackFixture` records exactly one onset state per cue:
`confirmed` (the audio integration reports the actual DSP sample start within the
registered virtual-clock tolerance), `estimated` (the integration reports an
estimate derived from its own markers or duration), `pending` (the onset report
has not arrived), or `unsupported` (no audio integration; records
`DSP_onset_unsupported`). The requested virtual gameplay timestamp, reported DSP
sample start or estimate, integration source, and `audio_cue_onset_tolerance_ms`
are separate fields in the onset record.

**Fixed pass/fail mapping (rev 2026-09-01):** `confirmed` is the **only** passing
state. `estimated`, `pending`, and `unsupported` are non-passing evidence. An
estimate is a diagnostic and must not use an unregistered hardcoded pipeline
latency. A late confirmed arrival updates trace history **only** and cannot
retroactively convert an earlier non-passing evaluation into a pass; no state other
than `confirmed` is serialized as passing. A later confirmed onset updates trace
history only and cannot alter the earlier outcome; only `confirmed` is passing, while
`estimated`, `pending`, and `unsupported` are non-passing. If the virtual
deadline passes while paused, the cue is cancelled and is never buffered across
resume.

#### Identity, retry, and rejection contract (mandatory)

- `flight_handle_id` is allocated once at an accepted Throw edge; rejected Throws have no flight identity. It remains stable through landing, timeout, or death cancellation.
- `fact_id` is `ulong`, allocated once at accepted raw-fact publication after source deduplication, session-scoped, and resets only at a session boundary (per the lifecycle table).
- `entry_id` is an opaque Perception-owned `string`, allocated at first-eligible-relay **only when the latest event-sourced FSM liveness fact says no episode is live**; otherwise the live episode's `entry_id` is carried.
- Event Bus deduplicates accepted envelopes by `(session_id, attempt_epoch, fact_id)`.
- Source dedup is namespace-aware: `(session_id, attempt_epoch, step:<step_id>)` for movement and `(session_id, attempt_epoch, flight:<flight_handle_id>)` for Burst. The namespaces are disjoint and monotonic independently.
- FSM dedup is `(session_id, attempt_epoch, guard_eid, fact_id)`.
- Bounded queues retry, deduplicate, admit, and defer deterministically before rejecting; rejection is recorded with a stable code and serialized pair/item identity — never silent drop.
- Stale-closure via `attempt_epoch` is the **only** `op=close` outside a normal terminal resolution.

#### Level manifest record additions (rev 2026-08-31, mandatory)

`LevelFixture` (AC20) asserts these manifest contracts in addition to the
per-record schemas above; the canonical manifest is
`design/levels/mvp-burst-route-fixture.md` (`WW-MVP-BURST-ROUTE-1.2`):

- **Gate trigger binding:** every gate-open trigger record binds the raw fact's
  `kind` — the certified route gate requires `kind = Burst` with its
  `landing_fact_id`; a movement pip inside the gate volume must leave the gate
  `blocked` (the manifest's `gate_footstep_negative_01`).
- **Room self-consistency:** `room_aabb_max.y == room_ceiling_y` exactly, and all
  synthetic/derived endpoints (arc apexes, ghost contacts, occlusion endpoints)
  lie inside the declared AABB.
- **Pickup siting clearance:** every pickup anchor sits clear of every
  patrol-segment corridor by at least `pickup_reach_radius + agent_radius`;
  mechanical siting enforcement — a siting violation is a manifest rejection
  with a stable validator code, not a tuning note.
- **`receiver_phase_window[]`:** every gate-open receiver record carries the
  recorded patrol-phase window(s) in which the receiver state was captured; the
  gate chain must be satisfiable inside a declared window (no unstated
  patrol-phase dependence).
- **`teaching_sequence[]` semantics:** ordered; places Walk/Run plus Crouch
  control before the first Burst spend. Its `noise_heard` observable is the
  **raw `NoisePublished` emission** (teaching beats demonstrate pip cadence at
  spawn, not relay arrival), and the guard-state precondition before the
  gate-spend beat is `Patrol` — a re-anchor/give-up in flight at the spend beat
  voids the gate chain, so the sequence starts from a settled patrol phase
  inside a recorded `receiver_phase_window`.

**AC20 route-causality record (mandatory):** The LevelFixture serializes one
`route_causality` record with exactly the identity chain fields
`causality_id`, `pickup_marker_id`, `pickup_reach_route_id`,
`pickup_interaction_id`, `pickup_state_transition_id`,
`accepted_throw_source_event_id`, `throw_snapshot_id`, `flight_handle_id`,
`landing_contact_probe_id`, `landing_surface_id`, `landing_fact_id`,
`receiver_probe_id`, `hearing_relay_fact_id`, `entry_id`,
`investigate_commit_id`, `gate_transition_id`, `gate_entry_marker_id`,
`expected_gate_entry_transition`, `gate_exit_marker_id`,
`gate_exit_reach_route_id`, `expected_gate_exit_reachable`, `route_evidence_id`,
`expected_order`, and `actual`, matching the registry field contract. The
Burst source identity is the disjoint `flight:<flight_handle_id>` namespace and
is never reconstructed from a movement `step_id`; every downstream identity is
propagated from the captured upstream record.

Every gate-open trigger is separately self-identifying with
`trigger_noise_kind ∈ {Movement, Burst}` and its responsible source id. The MVP gate
requires `trigger_noise_kind=Burst` and a non-null
`landing_fact_id` resolving to the same captured landing fact in the causality
chain. A Movement `step_id`, missing landing fact, invalid/fallback landing, or
synthetic source cannot open the gate. `expected_order` and all expected gate
states remain distinct from `actual`; an uncaptured or contradictory actual
never satisfies the expected chain.

**AC21 `PerformanceFixture` record (mandatory):** Each profile record carries
`profile_id`, `purpose`, `active_guards`, `due_facts`,
`evaluated_guard_fact_pairs`, `total_candidate_pairs`, `expected_linecast_count`,
`expected_deferred_pairs`, `burst_flights`, `due_hearing_boundaries`,
`required_budget_ms`, `cap_controls`, and `actual`, matching the registry
`workload_profiles` contract. The complete performance sample record additionally
carries target/build/engine/scene/fixture/schema/config identity, warm-up count,
repetitions, sample count, excluded samples with reasons, frame and boundary
p50/p95/p99/max, allocations/bytes, guard/fact/pair/query/synchronization/
publication counts, queue depth/capacity, deferred/retry/rejection counts and
stable codes, Burst tick/remainder/backlog/clamp diagnostics, stall magnitude and
sliding one-second hitch rate, and the first differing replay field. The
`supported_mvp` record uses the whole-frame `webgl_whole_frame_budget_p95_ms =
33.0 ms` p95 gate; hearing/Burst/Perception `≤ 2.0 ms` and aggregate `≤ 12.0 ms`
are decomposition diagnostics. `stress_30_guard_8_fact` is diagnostic-only and
never gates. All AC21 budget values remain `PENDING_OQ6` until target WebGL
measurement; `PENDING_OQ6` is non-passing and must not be replaced by a
synthetic or diagnostic result.

### Schema Versions

| Schema | Version | Used by |
|--------|---------|---------|
| `NoisePublished` envelope | `WW-NOISE-1.1` (source_event_class_rank added 2026-08-30; fact_publication_state added 2026-09-01) | `NoiseEmitterFixture`, `EventBusFixture` |
| `NoiseHeardRelay` envelope | `WW-NOISE-RELAY-1.0` (guard_eid canonical identity confirmed 2026-09-01) | `PerceptionHearingFixture` |
| `ThrowSnapshot` | `WW-THROW-1.0` | `BurstLifecycleFixture` |
| `LevelFixture` manifest | `WW-MVP-BURST-ROUTE-1.2` | `LevelFixture` |
| `LivenessFact` | `WW-LIVENESS-1.0` (added 2026-08-30; source_timestamp/evaluated_at registered 2026-09-01) | `FsmFixture` |
| `trace_event_schema` | `WW-TRACE-1.0` | All fixtures |

Every fixture records its schema version in the serialized record. Mismatched versions fail closed with a stable validator code.

### Cross-fixture Parity

Runtime, preview, fixture, and replay must agree on the **same** simulator, the **same** collision query, the **same** root selection, the **same** ordering, and the **same** queue admission. Parity violations are fixture-replay divergences, not gameplay bugs; the first differing field is recorded and the run fails closed.

## Formulas

- **Per-record identity projection:** Let `I = (session_id, attempt_epoch, source_timestamp, source_event_class_rank, source_event_id, fact_id, entry_id, guard_eid, consumption, publisher, evaluated_at)` be the canonical identity fields. Each record serializes only `Π_record(I)`, the subset declared and present in that record's schema; omitted fields are not synthesized or backfilled from another record. Raw records must not synthesize `entry_id`, `guard_eid`, or `consumption`.
- **Tolerance-class comparison:** `gameplay_contract` uses `|a - b| ≤ CompareTolerance = 5×10⁻³ m` for `Vector3` position and XZ-distance; `oracle_quantizer` uses `≤ 1×10⁻³ m` absolute for expected-vs-computed static-oracle records; `pure_math_relative` uses `≤ 1×10⁻⁶` relative for formula, discriminant, root, and pure-math range checks. Pure-math checks never use or describe a `1×10⁻³ m` tolerance.
- **Audio onset window:** `|virtual_dsp_onset - virtual_cue_request| ≤ audio_cue_onset_tolerance_ms × T_to_s`. Verified against the injected virtual clock; wall-clock drift is irrelevant.
- **AC21 budget:** the acceptance gate is the **whole frame** — `webgl_whole_frame_budget_p95_ms = 33.0 ms` p95 at the WebGL 30 fps floor; the hearing/Burst/Perception slice (`hearing_burst_perception_budget_ms ≤ 2.0` p95/p99) and the subsystem aggregate (`aggregate_webgl_frame_budget_ms ≤ 12.0` p95/p99) are decomposition records inside that gate, not standalone acceptance. All `PENDING_OQ6` until measured on target hardware.

## Edge Cases

| ID | Case | Required fixture behavior |
|----|------|---------------------------|
| E-FX-1 | Bounded queue retry exhausted | Event Bus and Perception reject the **newest** item, preserve older items, serialize identity, and record a stable code. No silent drop. |
| E-FX-2 | `attempt_epoch` transition mid-test | Lifecycle/session owner increments epoch, all prior-epoch queued items are discarded, current-epoch processing uses fresh phase. Stale-closure recorded for any live episode. |
| E-FX-3 | Listener subscribed/unsubscribed mid-boundary | Snapshot is stable for the boundary in progress; new listeners join only future boundaries. |
| E-FX-4 | Two events with equal `source_timestamp` | `fact_id` allocated in `(source_timestamp, source_event_class_rank, source_event_id)` order. Listener/thread arrival order is NOT an identity source. |
| E-FX-5 | Pause/resume mid-flight | Virtual clock freezes; Burst sim time does not advance; audio cues are NOT buffered; on resume, normal capped catch-up applies. |
| E-FX-6 | `LivenessFact` published but relay never consumed | `op=open` is durable for the session; Perception reads it at the next boundary. Stale-closure via `attempt_epoch` is the only `op=close` outside a normal terminal. |
| E-FX-7 | Pure-math oracle mismatch | Failure is recorded with the difference and quantizer; this is a fixture-oracle bug, not a gameplay bug. |
| E-FX-8 | Render stall during test | `Physics.autoSyncTransforms` cost and Event Bus/audio main-thread cost are inside the measurement boundary. Stall magnitude/rate are reported separately; non-hitch frames alone contribute to the p95/p99 steady-state gate. |
| E-FX-9 | Multiple Burst `flight_handle_id`s in one test | Each flight is independent; `flight_handle_id` is the per-flight source identity; `fact_id` is allocated per landing. |
| E-FX-10 | Burst contact at-or-before vs after 3.0 s timeout | `contact_event_time ≤ 3.0 s` ⇒ contact wins (incl. equality); later contact ⇒ timeout wins; no late `NoisePublished`. |
| E-FX-11 | Burst terminal publication is deferred or fails | Contact/timeout terminal is `pending` with `fact_id = null` before emitter flush, then `published` with nonzero `fact_id`, or explicitly `rejected`/`invalidated`; cancellation is `not_applicable`; no state uses numeric zero as a fact identity. |

## Dependencies

| Dependency | Direction | Notes |
|------------|-----------|-------|
| `design/gdd/player-noise.md` | upstream | The GDD this spec implements; spec is normative for fixtures |
| `design/gdd/perception.md` | bidirectional | Hearing/relay contract; LivenessFact read-side |
| `design/gdd/guard-ai-fsm.md` | bidirectional | LivenessFact write-side; FSM dedup tuple; AC-FSM-EX |
| `design/registry/entities.yaml` | upstream | All registry-bound values, queue caps, trace fields, tolerances |
| `docs/architecture/adr-0001-event-messaging-bus.md` | upstream | Bus contract; envelope; ingress dedup |
| `docs/architecture/adr-0002-physics-collision-contract.md` | upstream | E20 mask, `ProjectileRadius`, `epsilon_contact` |
| `design/levels/mvp-burst-route-fixture.md` | upstream | Canonical MVP room manifest for `LevelFixture` |
| `src/AI/Testing/FSMVerificationSuite.cs` | cross-ref | Approved FSM rev 4.1 test suite used by `FsmFixture` |

## Tuning Knobs

| Knob | Range | Owner | Notes |
|------|-------|-------|-------|
| `compare_tolerance_m` | `(1×10⁻³, 5×10⁻³]` m (default 5×10⁻³) | Player Noise GDD | Gameplay contract gate; pure-math quantizer is separate |
| `oracle_quantizer_absolute_m` | `1×10⁻³` absolute | Player Noise GDD | Formula-oracle precision target; not a gameplay tolerance |
| `audio_cue_onset_tolerance_ms` | `[10, 40]` ms (default 20) | Player Noise GDD | Virtual-clock bound; wall-clock irrelevant |
| `hearing_burst_perception_budget_ms` | `[1.5, 3.0]` ms (default 2.0) | Player Noise GDD | Asserted, not yet measured (OQ6) |
| `webgl_whole_frame_budget_p95_ms` | locked `33.0` ms (WebGL 30 fps floor, 1000/30) | Player Noise GDD | AC21 **acceptance gate** — whole-frame wall-clock p95; asserted, not yet measured (OQ6) |
| `aggregate_webgl_frame_budget_ms` | locked `12.0` ms | Player Noise GDD | Subsystem-aggregate **decomposition record**, NOT an acceptance gate; asserted, not yet measured (OQ6) |
| `stall_magnitude_budget_ms` | `[25, 50]` ms (default 33) | Player Noise GDD | Per-frame ceiling |
| `stall_rate_cap_per_s` | `[0.5, 2.0]` hitches/s (default 1.0) | Player Noise GDD | Sliding 1-second window |

## Acceptance Criteria

| ID | Criterion |
|----|-----------|
| AC-FX-1 | All 11 fixtures (NoiseEmitter, EventBus, PerceptionHearing, BurstLifecycle, ConfigValidator, AudioFeedback, Level, Fsm, Perception cross-system, HideSpot Target-tier, PerformanceFixture) are registered, schema-versioned, and loadable from one harness entry point. |
| AC-FX-2 | Every Player Noise GDD acceptance criterion names its owning fixture; cross-system criteria name the required fixture explicitly. |
| AC-FX-3 | Every fixture records each emitted, enqueued, or consumed record's declared trace fields with expected values separate from actual evidence. Identity is a per-record partial projection of the canonical fields present in that schema; omitted fields are not synthesized or backfilled. Raw records, including `NoisePublished`, must not synthesize `entry_id`, `guard_eid`, or `consumption`; those fields appear only when owned and declared by downstream relay, pair, or outcome records. |
| AC-FX-4 | Virtual-clock injection is used by every gameplay fixture; no fixture reads wall-clock time except `AudioFeedbackFixture` for the AC19 audio-onset measurement and the AC21 performance harness for the steady-state gate. |
| AC-FX-5 | CompareTolerance is `5×10⁻³ m` for gameplay contracts; oracle checks quantize to `1×10⁻³ m` absolute; the quantizer is NOT a gameplay tolerance. |
| AC-FX-6 | Queue ordering is asserted as three independent keys: guard selection `(stable_guard_snapshot_position, guard_eid)`, fact admission `(source_timestamp, source_event_class_rank, source_event_id, fact_id)`, and pair dispatch `(source_timestamp, source_event_class_rank, source_event_id, fact_id, guard_eid)`; the source-allocation key `(source_timestamp, source_event_class_rank, source_event_id)` is separate. Listener/thread arrival order is rejected as an identity source. |
| AC-FX-7 | Liveness closure matrix passes: every `op=open` has exactly one matching `op=close` keyed by `(session_id, attempt_epoch, guard_eid, entry_id)`; duplicate closes, mismatched keys, closes for rejected/disabled emitters, and closes across the wrong epoch fail; terminal and stale closes are both accepted only with their matching cause. The FSM is the sole writer and Perception reads the latest published fact at the previous boundary. |
| AC-FX-8 | The AC21 budget is `PENDING_OQ6` until measured; `PENDING_OQ6` is not a passing evidence value. |
| AC-FX-9 | Cross-fixture parity: runtime, preview, fixture, and replay agree on simulator, collision query, root selection, ordering, and queue admission; first differing field is recorded and the run fails closed. |
| AC-FX-10 | Mismatched schema versions fail closed with a stable validator code; `UNCAPTURED` and unverified `actual` fields are not passing evidence. |

## Open Questions

- **OQ-FX-1**: should `compare_tolerance_m` be split into a position gate and a distance gate (currently a single value)? Authoring thresholds (route waypoints, throw contact) are functionally distinct; a single value simplifies the harness but may mask per-class drift. Owner: qa-lead. Target: pre-fixture-implementation.
- **OQ-FX-2**: should `LivenessFact` be its own Event Bus topic, or part of the existing `trace_event` topic? Cross-doc ownership currently says "FSM is the sole writer"; a dedicated topic would make stale-closure observable without polling the trace stream. Owner: technical-director + lead-programmer. Target: pre-fixture-implementation.

No other harness questions are carried — schema versions, queue caps, identity tuples, virtual-clock injection, and tolerances are all locked here.

## Related Decisions

- `design/gdd/player-noise.md` — Player Noise (`NoiseEmitter`) contract (consumes)
- `design/gdd/perception.md` — Perception hearing/relay contract (bidirectional)
- `design/gdd/guard-ai-fsm.md` — Guard AI FSM LivenessFact write-side (bidirectional)
- `design/registry/entities.yaml` — All registry-bound values, caps, trace fields (upstream)
- `docs/architecture/adr-0001-event-messaging-bus.md` — Bus envelope/dedup (upstream)
- `docs/architecture/adr-0002-physics-collision-contract.md` — E20/Pins (upstream)
- `design/levels/mvp-burst-route-fixture.md` — MVP room manifest (upstream)
- `src/AI/Testing/FSMVerificationSuite.cs` — FSM rev 4.1 suite (cross-ref)
