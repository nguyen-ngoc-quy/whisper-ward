# Noise Fixture Specification

> **Status**: **Authoritative harness spec (2026-08-30; rev 2026-09-08 — Task 5
> canonical fixture handoff, source/terminal identity, pickup/overlap, queue,
> liveness, F4, AC19 evidence, and AC21 gate parity)** — referenced by every
> noise-domain fixture in `player-noise.md` acceptance criteria. Defines the
> master conventions for `NoiseEmitterFixture`, `EventBusFixture`,
> `PerceptionHearingFixture`, `BurstLifecycleFixture`, `ConfigValidatorFixture`,
> `AudioFeedbackFixture`, `LevelFixture`, `HideSpotFixture` (Target-tier),
> `PerformanceFixture`, and cross-system observability fixtures (`FsmFixture`,
> `PerceptionFixture`).
>
> **Schema**: `WW-NOISE-FIXTURE-SPEC-1.2`
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

The Player Noise GDD is a contract with **observable, deterministic, virtual-clock-bound** behavior. The acceptance criteria are spread across 11 executable fixtures governed by this one master specification, and each must agree on a small set of harness conventions: schema versions, virtual-clock injection, comparison tolerance, and trace field contracts. **`NoiseFixtureSpec` is the master specification** that pins those conventions so cross-fixture parity is testable and a failing fixture is always localizable.

The spec also enumerates the **mandatory fixture set** for the MVP noise-domain test battery. Every acceptance criterion in `player-noise.md` and the cross-system criteria in `perception.md` / `guard-ai-fsm.md` that depends on a noise event must name the owning fixture. Cross-system criteria do not simulate cross-system behavior inline; they name the required fixture explicitly.

## Player Fantasy (Harness)

A QA tester or CI runner should be able to run the noise-domain battery with one command, see all 11 fixtures execute in a defined order, and inspect a serialized record that is identical across runtime, preview, fixture, and replay. Failing assertions are localizable to one fixture, one criterion, and one expected/actual pair. There is no silent drop, no hidden runtime tolerance, no listener/thread arrival order dependence.

## Detailed Rules

### Mandatory Fixture Set (11 fixtures)

| # | Fixture | Owns | Cross-system? | Required ACs |
|---|---------|------|---------------|--------------|
| 1 | `NoiseEmitterFixture` | Valid `step_event` injection, Burst input, raw `busSpy` capture; `busSpy` fields include source-kind `source_timestamp`, `terminal_publication_time`, `t_publish`, `fact_publication_state`, `stride_target_state`, observational `state`, `movement_mode`, and `resolved_nominal_radius` | No | AC1, AC1b, AC2, AC2b, AC3, AC4 (source-side leg), AC5 (publish leg), AC6, AC6a, AC6b, AC7, AC8, AC9, AC9b |
| 2 | `EventBusFixture` | Active-listener registration, publisher order, retry, dedup | Yes (Bus ↔ Perception) | AC4 (bus-side leg), AC5 (handoff leg), AC5 queue overflow supplements |
| 3 | `PerceptionHearingFixture` | Virtual clock, scheduler phase, due-boundary predicate, `relaySpy` capture; `relaySpy` preserves raw movement/Burst payload fields, source ordering keys, `evaluated_at`, `residual_at_hearing`, `publisher`, and `t_publish` | Yes (Perception ↔ FSM) | AC5, AC12-P, AC13, AC15-MVP, AC15-Target, AC15b-MVP saturation supplements |
| 4 | `BurstLifecycleFixture` | Pickup ids, carried slot, checkpoint snapshot, segment reset, death, collision/timeout stimuli, persistent `ghostSpy`, and accepted-edge `cameraAzimuthSpy` | No (Player ↔ Burst) | AC6, AC6a, AC6b, AC7, AC8, AC9 (initial-overlap/contact legs), AC9b, AC15-MVP (death-during-flight leg) |
| 5 | `ConfigValidatorFixture` | Config injection, load-report writer, stable failure codes | No | AC10, AC11, AC11b |
| 6 | `AudioFeedbackFixture` | Gameplay commit time, DSP sample onset, cue identity, trigger/cancellation/presentation outcome, accessibility modes, visible micro-tell fields, internal proxy dedup | Yes (Audio ↔ FSM) | AC16, AC17, AC19 |
| 7 | `LevelFixture` | Canonical MVP room manifest, route/receiver/occlusion/throw/failed-spend evidence, authored ambiguity and NavMesh/E20 separation checks | No (Level) | AC9 (ambiguous-collision leg), AC20 |
| 8 | `FsmFixture` (Approved FSM rev 4.1) | `exposeFsmStateSpy {guard_eid, state, entry_id, source}` plus append-only `noiseOutcomeSpy {guard_eid, fact_id, entry_id, outcome, suppression_reason, episode_anchor_kind, episode_anchor_identity, episode_anchor_position_xz, episode_anchor_t_publish, start_arm, reanchor_budget_s, timer_started_at, target_path_end_reached_at}` | Yes (FSM) | AC12, AC12-P, AC15b-MVP, AC15-Target, AC-FSM-EX (LivenessFact) |
| 9 | `PerceptionFixture` (cross-system) | `exposeResidualSpy {guard_eid, R, dA_dt, residual_add, source}` | Yes (Perception F6) | AC18 (CR7 R_noise_share) |
| 10 | `HideSpotFixture` (Target-tier only) | Authored `Occupied` spot, hunch/no-break probe | No | AC14-Target |
| 11 | `PerformanceFixture` | Supported-MVP and diagnostic stress timing envelope | Yes (runtime slice) | AC21 |

`NoiseFixtureSpec` itself is the **sole schema authority and master specification** for all 11 executable fixtures. The MVP run includes fixtures 1–9 and 11; `HideSpotFixture` is Target-tier only and is excluded from MVP runs while its Target-only ACs are recorded as out of scope. It is the source of truth for fixture names/count, `trace_contract`, serialized field lists, comparison tolerances, and virtual-clock injection; the Player Noise GDD only summarizes fixture ownership and acceptance coverage, and fixtures consult this file rather than redefining its schema.

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
| `flight_handle_id` | `ulong` | Burst accepted-throw internal allocation identity, allocated at the accepted Throw edge; serialized source identity is `flight:<flight_handle_id>` |
| `guard_eid` | `string` | Stable guard identity |
| `epoch_transition_id` | `ulong` | Lifecycle/session owner; increments on epoch change |

**Source identity and timing mapping (mandatory):**

| Source kind | Source key | `source_timestamp` | `terminal_publication_time` | `t_publish` |
|---|---|---|---|---|
| Movement | `step:<step_id>` | Committed controller step time (`step_event.timestamp`) | Not applicable | `source_timestamp` |
| Burst | `flight:<flight_handle_id>` | Accepted Throw input-edge ordering time only | Terminal resolution time: landing/contact or timeout publication time, or death-cancellation time | `terminal_publication_time` for a published Collision/Timeout fact; not applicable for DeathCancelled |

A `DeathCancelled` terminal records its cancellation time for lifecycle/trace ordering but emits no `NoisePublished` fact, has no hearing `t_publish`, and creates no hearing deadline. The `step:` and `flight:` namespaces are disjoint and monotonic within each
`(session_id, attempt_epoch, namespace)`. `source_timestamp` is never replaced by
relay-arrival time. For a published Burst Collision/Timeout fact,
`terminal_publication_time` is the hearing-deadline anchor; for `DeathCancelled`
it is lifecycle/trace data only and the input timestamp is never substituted for
it.

**`NoisePublished` record:** `kind`, `source`, `flight_handle_id` when `kind=burst`, `movement_mode`,
`resolved_nominal_radius`, `position`, `radius`, `source_timestamp`,
`terminal_publication_time`, `t_publish`, `source_event_class_rank`
(`movement=0`, `burst=1`), namespaced `source_event_id`, `fact_id`, `timestamp`,
`publisher`, and `fact_publication_state ∈ {published, rejected, invalidated}`.
For Movement, `source_event_id` is `step:<step_id>`; for Burst, it is
`flight:<flight_handle_id>`. The Burst `flight_handle_id` is the accepted-Throw
identity and is never reconstructed from a movement `step_id`. `fact_id` is
allocated at accepted raw-fact publication time, not at Throw admission or terminal
allocation, and is shared by every guard relay for that fact. `movement_mode` and
`resolved_nominal_radius` are required for movement and not applicable to Burst. A
published record requires nonzero `fact_id`; zero is never a pending sentinel.

**`NoiseHeardRelay` record:** the immutable raw noise fields plus `session_id`,
`attempt_epoch`, `fact_id`, `flight_handle_id` when `kind=burst`, opaque string
`entry_id`, `raw_origin`, `source_timestamp`,
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
`source_timestamp`, `terminal_publication_time`, `t_publish`, `terminal_cause ∈
{Collision, Timeout, DeathCancelled}`, `burst_state_after ∈ {Landed, Consumed}`,
`flight_elapsed_s`, `contact_fraction`, `contact_event_time`, `timeout_boundary_s`,
`comparison_result`, `terminal_position`, `fact_publication_state ∈ {not_applicable, pending, published,
rejected, invalidated}`, nullable `fact_id`, `timestamp`, and `publisher`.
`terminal_cause` records why the flight ended, while `burst_state_after` is the
explicit resulting lifecycle state; neither is a substitute for
`terminal_publication_time`, the terminal resolution timestamp. For Collision and
Timeout, `terminal_publication_time` is also the authoritative publication/deadline
timestamp and a published terminal has `t_publish = terminal_publication_time`.
For `DeathCancelled`, `terminal_publication_time` records cancellation time,
`t_publish` is not applicable, and there is **no `NoisePublished` fact and no
hearing deadline**. Contact fields are null for timeout/death cancellation as
specified by the registry. Pending or non-published terminals use `fact_id = null`,
never numeric zero; a published terminal requires a nonzero fact id.

**Accepted `ThrowSnapshot` record (`WW-THROW-1.0`):** the envelope carries
`throw_snapshot_id` and the immutable payload is exactly
`{session_id, attempt_epoch, flight_handle_id, accepted_edge_timestamp,
launch_feet_position, launch_position, resolved_velocity, resolved_stance,
camera_azimuth_deg, resolved_facing_azimuth_deg, trajectory_v0_mps,
trajectory_theta_deg, throw_release_height_m, projectile_radius_m,
source_pickup_id, geometry_variant_id}`. Runtime, preview, and LevelFixture must
compare this field-for-field snapshot; no consumer re-samples camera, origin,
stance, or geometry. Rejected Throws have no `flight_handle_id`,
`throw_snapshot_id`, or snapshot payload.

**Pickup reach and interaction records:** prompt and `Interact` use one
`PickupReachRecord` with `pickup_id`, `pickup_reach_route_id`, `player_feet`,
`pickup_anchor`, `pickup_reach_radius`, `distance_squared`, and the inclusive
predicate `distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2`,
followed by an E20 `Linecast` from `player_feet + up * f12_noise_origin_offset`
to `pickup_anchor` in the injected `PhysicsScene` with the cached E20 World mask
and `QueryTriggerInteraction.Ignore`. The record retains `e20_linecast`,
`reach_predicate`, `reachable`, `tolerance_class` when a position is compared,
and `actual` separately. The LevelFixture `pickup_interactions` record uses the
exact registry fields `interaction_id`, `pickup_id`, `reach_route_id`, `trigger`,
`expected_precondition`, `expected_result`, `telemetry_event`, `telemetry_only`,
`emits_noise_published`, and `actual`. Its expected precondition carries the
reachable sphere and E20 results, its expected result is the typed interaction
outcome, `telemetry_event=pickup-reached`, `telemetry_only=true`, and
`emits_noise_published=false`. The LevelFixture record uses `interaction_id`,
not `pickup_interaction_id`; the latter name is reserved for the separate
`route_causality` identity-chain field. The trace-event `Pickup interaction`
record separately uses the exact registry fields `session_id`, `attempt_epoch`,
`interaction_id`, `pickup_id`, `trigger`, `reachable`, `distance_within_reach`,
`clear_linecast`, `telemetry_event`, `telemetry_only`, `timestamp`, and
`publisher`. The paired pickup state-transition record owns the explicit
`Placed -> Carried` state change and spend/noise result. `pickup-reached` cannot
mutate state, spend, or publish a fact.

**Initial-overlap rejection record:** the launch-clear probe is a distinct
`InitialOverlapRecord` with `initial_launch_overlap_probe =
PhysicsScene.OverlapSphere(launch_position, projectile_radius, E20_mask,
QueryTriggerInteraction.Ignore)` executed exactly once before spend or
allocation. It records `initial_overlap`, `geometry_validation_result`,
`rejection_code=INITIAL_OVERLAP_REJECTED`, `no_spend=true`,
`no_runtime_launch=true`, `burst_state=Carried`,
`flight_handle_id=null`, `throw_snapshot_id=null`, `fact_id=null`,
`source_event_id=null`, and `actual` separately. It emits no flight, terminal,
audio, or `NoisePublished`. This record is not a swept SphereCast terminal and is
kept separate from wall, ceiling, void, timeout, and death-cancelled consumed-spend
records.

**F4 landing resolution record:** every positive throw records the authored
`landing_surface_id` and `landing_surface_y_m`, with `h_landing_m` equal to that
authored surface height for the F4 oracle. The separate
`published_projectile_center` is the post-hit SphereCast result
`collider_surface_contact + hit_normal * (projectile_radius + epsilon_contact)`;
it is the landing/hearing position, not the F4 `h_landing` operand. Both results
carry their declared `tolerance_class` (`pure_math_relative` for F4 arithmetic,
`gameplay_contract` for gameplay placement, or `oracle_quantizer` for a static
oracle comparison) and separate expected/actual evidence.

**Queue rejection record:** the serialized record uses the exact registry fields
`session_id`, `attempt_epoch`, `queue_item_kind`, `guard_eid` (required for a
guard/fact pair and nullable otherwise), `source_kind`, `source_timestamp`,
`terminal_publication_time`, `t_publish`, `deadline`, `source_event_id`, `fact_id`,
`queue_name`, `queue_state`, `queue_depth`, `queue_capacity`, `retry_count`,
`rejection_code`, `timestamp`, and `publisher`. Deterministic ordering is
asserted against the registered order contracts rather than by adding
fixture-only fields: guard selection uses `[guard_eid]`, fact admission uses
`[source_timestamp, source_event_class_rank, source_event_id, fact_id]`, and
pair dispatch uses `[source_timestamp, source_event_class_rank, source_event_id,
fact_id, guard_eid]`. `source_event_class_rank` remains part of the upstream
source-order contract; `admission_order_key` is the validator's applicable tuple,
not a serialized Queue rejection field. `stable_guard_snapshot_position` is
recorded data and never a sort key. For hearing work, `deadline = t_publish +
T_hearing`; a DeathCancelled source has no hearing deadline. Existing identities
are deduplicated before retry/backpressure; only a new identity enters the
deterministic policy of retry, admit, defer, then reject-newest after retry
exhaustion. Older queued work is preserved, and a rejected item creates no relay,
decision, or passing evidence. A source-side rejection uses the typed record
`SourceRejection {session_id, attempt_epoch, source_event_id, source_kind,
invalid_field_or_validation_reason, rejection_code, publisher, input_identity,
expected, actual}`. `rejection_code` is one of `SOURCE_MALFORMED`,
`SOURCE_DUPLICATE`, `SOURCE_OUT_OF_ORDER`, `SOURCE_WRONG_EPOCH`,
`SOURCE_NONFINITE`, `SOURCE_ZERO_DISPLACEMENT`, or `SOURCE_NEGATIVE_DISPLACEMENT`;
missing fields, missing expected/actual parity, or an uncaptured `actual` are
non-passing.

**`LivenessFact` record:** `session_id`, `attempt_epoch`, `guard_eid`, opaque string
`entry_id`, `tier ∈ {Investigate, Chase}`, `op ∈ {open, promote, close}`, `cause`,
`position`, `position_source`, `source_timestamp`, `timestamp`, `evaluated_at`, and
`publisher=GuardAISystem`. `position_source` is `opener_decision` for `open`,
`episode_position` for `promote`, `terminal_decision` for a normal close, and
`last_authoritative_episode_position` for a stale close. Each open has exactly one
matching terminal or stale close. The lifecycle owner sends one `StaleLivenessClose`
request before prior-epoch invalidation; GuardAISystem publishes the one stale close,
with Event Bus key `(session_id, attempt_epoch, guard_eid, entry_id, op,
source_timestamp)` and order `(source_timestamp, guard_eid, entry_id, op)`. The
lifecycle owner never publishes a duplicate, and the barrier commits only after the
close is retained or a fail-closed barrier record exists. An Investigate→Chase
promotion records `op=promote` with the same `entry_id` and does not create a second
open or close.

**FSM semantic micro-tell record:** `suppressed-receipt` is serialized as
`{session_id, attempt_epoch, guard_eid, fact_id, entry_id, suppression_reason,
visible_to_player, micro_tell_emitted, timestamp, publisher=GuardAISystem}`.
The FSM owns this semantic event and its single-consumption identity; VFX/audio
only render the tell from the immutable receipt and never query behavior or infer a
gameplay outcome. A relay suppressed by state, visibility, cooldown, area, or
out-of-window rules remains a consumed FSM outcome, not an audio-only event.

**Pickup/Gate record:** `pickup_id`, `flight_handle_id` when applicable,
`throw_snapshot_id`, `landing_surface_id`, `gate_id`, transition result/code,
`tolerance_class` when a position is compared, and `actual` evidence state. Every
**gate trigger** record is self-identifying about its trigger stream: it carries
`trigger_noise_kind ∈ {movement, burst}` plus the responsible source id
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
| Audio onset (AC19) | `audio_cue_onset_tolerance_ms = 22 ms` default (registered range `[10, 40]` ms; rev 2026-09-01, B5 — 20 ms sat below a 1024-sample/48 kHz mix quantum ≈ 21.3 ms) | Verified against injected virtual clock |
| AC21 acceptance gate (whole frame, rev 2026-08-31) | `webgl_whole_frame_budget_p95_ms = 33.0 ms` p95 at the WebGL 30 fps floor — whole-frame wall-clock | Asserted, not yet measured (OQ6); `PENDING_OQ6` is non-passing |
| Hearing/Burst/Perception slice (AC21 decomposition record) | `≤ 2.0 ms` p95/p99 for `supported_mvp` (1 guard / 1 fact / 1 flight); `stress_30_guard_8_fact` is diagnostic only | Asserted, not yet measured (OQ6); not the whole-frame gate |
| Aggregate WebGL subsystem slice (AC21 decomposition record) | `≤ 12.0 ms` p95/p99 — diagnostic decomposition, **NOT** the acceptance gate | Asserted, not yet measured (OQ6) |
| Stall magnitude | `stall_magnitude_budget_ms = 33` ms | Per-frame ceiling |
| Stall rate | `stall_rate_cap_per_s` sliding 1-second window | Separate from steady-state budget |

The 5 mm gameplay tolerance is the **gate**: anything looser is a known limitation surfaced in the load report; anything tighter is a fixture-oracle precision target, not a gameplay claim. The 1 mm oracle tolerance is the simulator-oracle precision target — it proves the simulator matches the formula at oracle precision, but no gameplay path is allowed to assume sub-millimetre physical precision.

#### Queue admission order (mandatory)

The Perception hearing queue admits in this exact order, shared by all fixtures:

1. Selected guard set is the **`stable_guard_snapshot`** taken at the boundary's perception-tick start (NOT the active subscription), ordered by ascending `guard_eid` only; `stable_guard_snapshot_position` is recorded but never a sort key.
2. Facts admitted to that snapshot are processed in `(source_timestamp, source_event_class_rank, source_event_id, fact_id)` order.
3. Guard/fact pairs are processed in `(source_timestamp, source_event_class_rank, source_event_id, fact_id, guard_eid)` order.

Listener/thread arrival order, subscription churn mid-boundary, queue insertion order, and per-listener registry mutation are **NOT** identity sources. The order is `hearing_queue_admission_order` in `entities.yaml` and is read-only to fixtures.

The three component keys are asserted independently: guard selection
`guard_eid` ascending (with `stable_guard_snapshot_position` recorded but non-ordering), fact admission
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

`AudioFeedbackFixture` records `audio_cue_id` and `audio_applicability` for every cue as
`audible` or `visual_only`. `audio_cue_id` is allocated by Audio/UI at cue-request
creation from `(session_id, attempt_epoch, cue_kind, local_sequence)`, is monotonic
within that namespace, and is retained across retries, duplicate delivery, adapter
callbacks, cancellation, stale-epoch rejection, and unsupported integration. It is
presentation evidence only and never replaces `fact_id`, `source_event_id`, or
`entry_id`. Only an `audible` cue receives one immutable `onset_outcome` and append-only `onset_trace_history[]`; a `visual_only` cue
records `onset_evidence=not_applicable` and is excluded from the onset and limiter
pass gates. For an audible cue, `onset_outcome` is one of `confirmed` (the audio
integration reports the actual DSP sample start within the registered virtual-clock
tolerance), `estimated` (the integration reports an estimate derived from its own
markers or duration), `pending` (the onset report has not arrived), or `unsupported`
(no audio integration; records `DSP_onset_unsupported`). The record keeps
`virtual_cue_request`, `voice_instance_id`, `dsp_start_sample`, a dedicated
nullable `dsp_start_sample_estimate`, `sample_rate`, `audio_clock_source`,
`integration_source`, `epoch_offset`, and `audio_cue_onset_tolerance_ms` as
separate fields. An `estimated` onset serializes its estimate only in
`dsp_start_sample_estimate` (null otherwise); it never populates or mutates
`dsp_start_sample` and never converts the immutable `onset_outcome`. A confirmed
onset requires the integration-owned `voice_instance_id` and an absolute
`dsp_start_sample`; clip-relative `AudioSource.timeSamples` is not DSP evidence.
For the logical body+accent blend, child voice records are grouped under the
same `audio_cue_id` and must carry role-typed `body` and `accent` identities plus
their own absolute sample/provenance fields before the logical cue can be
confirmed.
DSP sample conversion is valid only when the registered `sample_rate`,
`audio_clock_source`, and `epoch_offset` are present; no wall-clock callback or
inferred zero is accepted as a sample start.

For an `audible` cue routed through an applicable measured audio bus, the
limiter/true-peak result is recorded as an explicit evidence state:
`limiter_reported` (the audio integration reports a true-peak limiter result
within the registered `≤ −1 dBTP` bound and supplies finite `true_peak_dbTP`,
nonempty `limiter_stage_id`, `bus_id`, and `measurement_source`),
`limiter_unsupported` (the integration ships no true-peak limiter — stock Unity
audio is the canonical case until the OQ3 middleware ADR lands; it carries a
reason and null `true_peak_dbTP`), or `not_applicable` (the cue is visual-only or
outside the registered limiter bus scope, carries reason `visual_only` or
`out_of_scope`, and null `true_peak_dbTP`). A `visual_only` cue records `limiter_evidence=not_applicable` and is not
evaluated by AC19. These are the only accepted values for their declared
applicability; any other value is a schema error. The limiter evidence state is bound to the same
`audio_cue_onset_tolerance_ms` record only when onset evidence is applicable.

**Fixed pass/fail mapping (rev 2026-09-01):** `confirmed` is the **only** passing
state. `estimated`, `pending`, and `unsupported` are non-passing evidence. An
estimate is a diagnostic and must not use an unregistered hardcoded pipeline
latency. A late confirmed arrival appends to `onset_trace_history[]` **only** and
cannot retroactively convert an earlier non-passing `onset_outcome` into a pass; no
state other than `confirmed` is serialized as passing. If the virtual
deadline passes while paused, the cue is cancelled and is never buffered across
resume.

The canonical onset record shape is:

```text
AudioOnsetRecord {
  session_id, attempt_epoch, audio_cue_id, cue_kind, audio_applicability,
  source_event_id or fact_id, virtual_cue_request, virtual_dsp_onset,
  voice_instance_id, dsp_start_sample, dsp_start_sample_estimate, sample_rate,
  audio_clock_source,
  epoch_offset, onset_outcome or onset_evidence, onset_trace_history[],
  integration_source, audio_cue_onset_tolerance_ms or not_applicable,
  limiter_evidence, presentation_outcome, visible_to_player,
  micro_tell_emitted, cancellation_reason, actual
}
```

`voice_instance_id` and `dsp_start_sample` are nullable until an integration report
exists; `onset_trace_history[]` is append-only; every audible cue, including Pickup
Confirmation, is subject to the same AC19 onset gate. An audible cue may use
`limiter_evidence=not_applicable` only when its record explicitly declares that it is
outside the registered limiter bus scope; `audio_applicability=audible` never permits
`onset_evidence=not_applicable`. An `audible` cue's
`limiter_evidence` is restricted to `limiter_reported`,
`limiter_unsupported`, or `not_applicable`; an out-of-scope or `visual_only`
cue uses `not_applicable`. The fixture must reject unknown outcome/limiter values and
must not infer a passing result from `actual=UNCAPTURED`.

#### Identity, retry, and rejection contract (mandatory)

- `Throw` admission is stance-authoritative: `Idle`/standing may accept only at resolved planar speed `= 0`; `Crouch-Idle` must pass same-tick stand resolution and then be stationary; every `Walk`/`Run`-labelled edge is rejected before spend and allocation, including a zero-speed edge. Rejected legs record `THROW_WHILE_MOVING_REJECTED` and no flight/audio/raw-fact identity.
- `flight_handle_id` is allocated once at an accepted Throw edge; rejected Throws have no flight identity. It remains stable through landing, timeout, or death cancellation.
- `fact_id` is `ulong`, allocated once at accepted raw-fact publication after source deduplication, session-scoped, and resets only at a session boundary (per the lifecycle table).
- `entry_id` is an opaque Perception-owned `string`; the first eligible relay reserves one only when the latest event-sourced liveness fact reports no live episode, and later relays carry the live episode's same `entry_id`.
- Event Bus deduplicates accepted envelopes by `(session_id, attempt_epoch, fact_id)`.
- Source dedup is namespace-aware: `(session_id, attempt_epoch, step:<step_id>)` for movement and `(session_id, attempt_epoch, flight:<flight_handle_id>)` for Burst. The namespaces are disjoint and monotonic independently.
- FSM dedup is `(session_id, attempt_epoch, guard_eid, fact_id)`.
- Bounded queues deduplicate existing identities before retry/backpressure; new identities retry, admit, and defer deterministically before rejection. Rejection is recorded with a stable code and serialized pair/item identity — never silent drop.
- Stale-closure via `attempt_epoch` is the **only** `op=close` outside a normal terminal resolution.

#### Level manifest record additions (rev 2026-08-31, mandatory)

`LevelFixture` (AC20) asserts these manifest contracts in addition to the
per-record schemas above; the canonical manifest is
`design/levels/mvp-burst-route-fixture.md` (`WW-MVP-BURST-ROUTE-1.2`):

- **Gate trigger binding:** every gate-open trigger record binds the raw fact's
  canonical lowercase `kind` — the certified route gate requires `kind = burst`
  with its `landing_fact_id`; a movement pip inside the gate volume must leave
  the gate `blocked` (the manifest's `gate_footstep_negative_01`).
- **Room self-consistency:** `room_aabb_max.y == room_ceiling_y` exactly, and all
  synthetic/derived endpoints (arc apexes, ghost contacts, occlusion endpoints)
  lie inside the declared AABB.
- **Pickup siting clearance:** every pickup anchor sits clear of every
  patrol-segment corridor by at least `pickup_reach_radius + agent_radius`;
  mechanical siting enforcement — a siting violation is a manifest rejection
  with a stable validator code, not a tuning note.
- **`navmesh_layer_separation_check`:** `LevelFixture` records this Level-owned
  check in every certification run; it enumerates authored NavMesh colliders and
  fails closed if any layer intersects the E20 World mask. `UNCAPTURED` is
  non-passing and cannot be replaced by a synthetic pass.
- **`receiver_phase_window[]`:** every gate-open receiver record carries the
  recorded patrol-phase window(s) in which the receiver state was captured, bound
  to a declared patrol window via `receiver_phase_window_id` (rev 2026-09-01, B9 —
  the id must resolve to a `patrol_phase_window` the level manifest declares; the
  MVP level fixture binds `patrol_pre_spend_01`); the gate chain must be
  satisfiable inside a declared window (no unstated
  patrol-phase dependence; a null, unknown, or non-resolving id fails
  `INVALID_GATE_TRANSITION`).
- **`teaching_sequence[]` semantics:** ordered; places Walk/Run plus Crouch
  control before the first Burst spend. Its `noise_heard` observable is the
  **raw `NoisePublished` emission** (teaching beats demonstrate pip cadence at
  spawn, not relay arrival). Emission-only Crouch, Walk, and Run beats must
  explicitly declare their no-response outcome: Crouch proves no `step_event` or
  `NoisePublished`, while Walk and Run may publish raw movement noise outside
  `R_eff` without requiring a relay or FSM response. Hearing/response beats must
  declare the visible guard response; the certified MVP Burst-spend beat must
  declare the visible orient/Investigate response. The guard-state precondition
  before the gate-spend beat is `Patrol` — a re-anchor/give-up in flight at the
  spend beat voids the gate chain, so the sequence starts from a settled patrol
  phase inside the window named by the gate-open record's
  `receiver_phase_window_id`.

**AC20 route and validation record shapes (mandatory):** The route fixture uses
registry field names without aliases. A `RouteEvidenceRecord` carries
`route_id`, `from_marker`, `to_marker`, `obstruction_ids`,
`player_agent_radius_m`, `navmesh_area_mask`, `expected_path_status`,
`path_complete`, `path_samples`, `stable_path_hash`, `tolerance_class`, `pickup_reach`,
`e20_linecast`, `gate_state`, `failed_gate_spend`, `required_failed_cases`, and
`actual`. A `ValidThrowVolumeRecord` carries
`volume_id`, `geometry_variant_id`, `launch_position_ws`, `launch_direction_xz`,
`open_volume_bounds`, `landing_surface_id`, `landing_surface_y_m`,
`landing_contact_mode`, `target_marker_id`, `route_id`, `route_role`,
`causal_route_binding`, `source_pickup_id`, `throw_snapshot_id`,
`throw_snapshot_parity`, `v0_mps`, `theta_deg`, `band_reachable_theta_domain_deg`,
`h_release_m`, `h_landing_m`, `delta_y_m`, `projectile_radius_m`,
`epsilon_contact_m`, `contact_ambiguity_tolerance_m`, `angle_domain_result`,
`band_domain_result`, `discriminant_m2ps2`, `t_minus_s`, `t_plus_s`,
`selected_t_land_s`, `root_policy`, `r_actual_m`, `range_gate`,
`ceiling_margin_m`, `lateral_margin_m`, `collision`, `landing_envelope`,
`expected`, `formula_tolerance_class`, `formula_relative_tol`,
`placement_tolerance_class`, `placement_tol_m`,
`diagnostic_simulation_envelope_m`, and `actual`. A
`NegativeValidationCaseRecord` carries the registry's complete rejection fields:
`case_id`, `category`, `geometry_variant_id`, `complete_inputs`,
`expected_rejection_code`, `configuration_result`, `angle_domain_result`,
`band_reachable_theta_domain_deg`, `band_domain_result`,
`discriminant_m2ps2`, `root_result`, `r_actual_m`, `range_gate`,
`initial_overlap`, `geometry_validation_result`, `grazing_surface_id`,
`grazing_clearance_m`, `contact_ambiguity_tolerance_m`, `candidate_solids`,
`hit_distance_delta_m`, `no_runtime_launch`, `no_spend`, `flight_handle_id`,
`fact_id`, `source_event_id`, `burst_state`, and `actual`. Formula-bearing
records declare `formula_tolerance_class= pure_math_relative`; placement records
use `gameplay_contract` or `oracle_quantizer` as applicable. Formula fields may be
`NOT_EVALUATED` or null only when the expected rejection occurs before that
calculation. Initial overlap is either the bounded-query
`INITIAL_OVERLAP_QUERY_INCOMPLETE` fail-closed case (saturation or incomplete
enumeration) or the proven-overlap `INITIAL_OVERLAP_REJECTED` no-spend/no-launch
case; neither is a consumed-spend terminal.

**AC20 route-causality record (mandatory):** The LevelFixture serializes one
`route_causality` record with exactly the identity chain fields
`causality_id`, `pickup_marker_id`, `pickup_reach_route_id`,
`pickup_interaction_id`, `pickup_state_transition_id`, `causal_volume_id`,
`causal_geometry_variant_id`, `causal_landing_surface_id`,
`accepted_throw_source_event_id`, `throw_snapshot_id`, `throw_snapshot_parity`,
`flight_handle_id`, `landing_contact_probe_id`, `landing_surface_id`,
`landing_fact_id`,
`receiver_probe_id`, `hearing_relay_fact_id`, `entry_id`,
`investigate_commit_id`, `gate_transition_id`, `gate_entry_marker_id`,
`expected_gate_entry_transition`, `gate_exit_marker_id`,
`gate_exit_reach_route_id`, `expected_gate_exit_reachable`, `route_evidence_id`,
`expected_order`, `liveness_continuity`, and `actual`, matching the registry field
contract. The causal binding fields identify the single physical route volume,
geometry variant, and landing surface; `liveness_continuity` is required even when
its applicability is `investigate_to_chase_only`. The Burst source identity is the disjoint `flight:<flight_handle_id>` namespace and
is never reconstructed from a movement `step_id`; every downstream identity is
propagated from the captured upstream record.

A `GateTransitionRecord` carries
`transition_id`, `gate_marker_id`, `owner`, `from_state`, `to_state`,
`trigger_event`, `trigger_noise_kind`, `trigger_decision_id`,
`causal_session_id`, `causal_attempt_epoch`, `causal_fact_id`,
`landing_fact_id`, `step_id`, `causal_entry_id`, `expected_cause`,
`expected_order_after`, `expected_order_before`, `receiver_phase_window_id`,
`receiver_phase_window`, `receiver_binding`, `gate_state_after_failed_spend`,
`gate_exit_reachable_after_failed_spend`, and `actual`. A certified MVP gate
transition must carry `trigger_noise_kind=burst`, its non-null `landing_fact_id`,
and the same causal session, epoch, fact, entry, and route order as the
`route_causality` record. A movement trigger carries `step_id` only for the
negative gate case and cannot open the gate.

Every gate-open trigger is separately self-identifying with
`trigger_noise_kind ∈ {movement, burst}` and its responsible source id. The MVP gate
requires `trigger_noise_kind=burst` and a non-null
`landing_fact_id` resolving to the same captured landing fact in the causality
chain. A Movement `step_id`, missing landing fact, invalid/fallback landing, or
synthetic source cannot open the gate. `expected_order` and all expected gate
states remain distinct from `actual`; an uncaptured or contradictory actual
never satisfies the expected chain.

**AC21 `PerformanceFixture` record (mandatory):** Each profile record carries
`profile_id`, `purpose`, `active_guards`, `due_facts`,
`evaluated_guard_fact_pairs`, `total_candidate_pairs`, `expected_linecast_count`,
`expected_deferred_pairs`, `burst_flights`, `due_hearing_boundaries`,
`required_budget_ms`, `budget_role`, `platform_scope`, `cap_controls`, and `actual`, matching the registry
`workload_profiles` contract. Each profile also declares `budget_role` (`acceptance_gate` or
`diagnostic_only`) and `platform_scope` (`[WebGL]` for the current millisecond gate;
`[PC, WebGL]` for functional/replay execution). The complete performance sample record additionally
carries target/build/engine/scene/fixture/schema/config identity, warm-up count,
repetitions, sample count, excluded samples with reasons, frame and boundary
p50/p95/p99/max, typed allocation evidence `{managed_bytes, native_bytes, gc_alloc_count,
allocation_source}` (or explicit `unsupported`), guard/fact/pair/query/synchronization/
publication counts, queue depth/capacity, deferred/retry/rejection counts and
stable codes, Burst tick/remainder/backlog/clamp diagnostics, stall magnitude and
sliding one-second hitch rate, and the first differing replay field. It also carries
`minimum_steady_state_samples`, `percentile_method`, `budget_role`, `platform_scope`,
and `component_timer_contract`. The minimum is 100 non-excluded steady-state frame
samples per profile; percentiles use nearest-rank with rank `ceil(p × N)` over the
ascending non-excluded sample list (`p50`, `p95`, `p99`), while `max` is the largest
non-excluded sample. A run with fewer than 100 eligible samples is `UNCAPTURED`, not
an extrapolated pass. Labeled stalls are excluded from steady-state percentiles but
must pass their independent magnitude/rate legs; any unlabeled frame above the
nominal frame interval is classified `spontaneous_hitch` with stable
`UNKNOWN_HITCH_SOURCE`, retained in the hitch ledger, and excluded only with that
explicit reason; its presence invalidates a passing run until classified and
resolved. Timer components are disjoint:
each owner starts/stops only its named boundary, child timers are subtracted from
the parent's raw interval before reconciliation, and an overlap or unexplained
residual beyond timer resolution is `TIMER_BOUNDARY_INVALID`. The
`supported_mvp` record uses the whole-frame `webgl_whole_frame_budget_p95_ms =
33.0 ms` p95 gate; hearing/Burst/Perception `≤ 2.0 ms` and aggregate `≤ 12.0 ms`
are decomposition diagnostics. `stress_30_guard_8_fact` is diagnostic-only and
never gates. The `supported_mvp` record therefore reports two distinct classes:
`webgl_whole_frame_budget_p95_ms=33.0` ms is the sole AC21 pass gate, while
`hearing_burst_perception_budget_ms=2.0` ms and
`aggregate_webgl_frame_budget_ms=12.0` ms are decomposition diagnostics only.
No diagnostic value can substitute for the whole-frame sample. Every profile also
serializes `budget_role` (`acceptance_gate` for `supported_mvp`, `diagnostic_only`
for `stress_30_guard_8_fact`) and `platform_scope`. The stress profile therefore
uses `required_budget_ms=null`; a diagnostic profile has no required budget and can
never satisfy AC21. All AC21 budget fields use the typed union
`budget_evidence={status: PENDING_OQ6, value_ms: null} | {status: CAPTURED, value_ms: finite}`;
`PENDING_OQ6` is never a scalar measurement or a pass value. The WebGL record is the
only current millisecond acceptance gate. PC remains the primary development target
and must run the same functional/replay checks, but has no separate millisecond pass
threshold until a PC hardware profile is registered. An `actual=UNCAPTURED` profile
is also non-passing.

### Schema Versions

| Schema | Version | Used by |
|--------|---------|---------|
| `NoisePublished` envelope | `WW-NOISE-1.1` (source_event_class_rank added 2026-08-30; fact_publication_state added 2026-09-01) | `NoiseEmitterFixture`, `EventBusFixture` |
| `NoiseHeardRelay` envelope | `WW-NOISE-RELAY-1.0` (guard_eid canonical identity confirmed 2026-09-01) | `PerceptionHearingFixture` |
| `ThrowSnapshot` | `WW-THROW-1.0` | `BurstLifecycleFixture` |
| `LevelFixture` manifest | `WW-MVP-BURST-ROUTE-1.2` | `LevelFixture` |
| `LivenessFact` | `WW-LIVENESS-1.0` (added 2026-08-30; source_timestamp/evaluated_at registered 2026-09-01) | `FsmFixture` |
| `trace_event_schema` | `WW-TRACE-1.0` | All fixtures |
| `PerformanceFixture` record | `WW-PERF-1.0` | `PerformanceFixture` / AC21 |

Every fixture records its schema version in the serialized record. Mismatched versions fail closed with a stable validator code.

### Cross-fixture Parity

Runtime, preview, fixture, and replay must agree on the **same** simulator, the **same** collision query, the **same** root selection, the **same** ordering, and the **same** queue admission. Parity violations are fixture-replay divergences, not gameplay bugs; the first differing field is recorded and the run fails closed.

## Formulas

- **Per-record identity projection:** Let `I = (session_id, attempt_epoch, source_timestamp, source_event_class_rank, source_event_id, fact_id, entry_id, guard_eid, consumption, publisher, evaluated_at)` be the canonical identity fields. Each record serializes only `Π_record(I)`, the subset declared and present in that record's schema; omitted fields are not synthesized or backfilled from another record. Raw records must not synthesize `entry_id`, `guard_eid`, or `consumption`.
- **Tolerance-class comparison:** `gameplay_contract` uses `|a - b| ≤ CompareTolerance = 5×10⁻³ m` for `Vector3` position and XZ-distance; `oracle_quantizer` uses `≤ 1×10⁻³ m` absolute for expected-vs-computed static-oracle records; `pure_math_relative` uses `≤ 1×10⁻⁶` relative for formula, discriminant, root, and pure-math range checks. Pure-math checks never use or describe a `1×10⁻³ m` tolerance.
- **Audio onset window:** convert the absolute sample index as `virtual_dsp_onset = (dsp_start_sample / sample_rate) + epoch_offset`, then assert `|virtual_dsp_onset - virtual_cue_request| ≤ audio_cue_onset_tolerance_ms × T_to_s`, where `T_to_s = 1/1000` seconds per millisecond. Missing/non-finite conversion metadata is non-passing. Verified against the injected virtual clock; wall-clock drift is irrelevant.
- **AC21 budget:** the acceptance gate is the **whole frame** — `webgl_whole_frame_budget_p95_ms = 33.0 ms` p95 at the WebGL 30 fps floor; the hearing/Burst/Perception slice (`hearing_burst_perception_budget_ms ≤ 2.0` p95/p99) and the subsystem aggregate (`aggregate_webgl_frame_budget_ms ≤ 12.0` p95/p99) are decomposition records inside that gate, not standalone acceptance. Each profile declares `budget_role` and `platform_scope`; diagnostic stress has no required budget. Budget evidence is the typed union `budget_evidence={status: PENDING_OQ6, value_ms: null} | {status: CAPTURED, value_ms: finite}`. PC functional/replay checks remain required as the primary development target, but no PC millisecond pass threshold exists until a hardware profile is registered.

## Edge Cases

| ID | Case | Required fixture behavior |
|----|------|---------------------------|
| E-FX-1 | Bounded queue retry exhausted | Event Bus and Perception reject the **newest** item, preserve older items, serialize identity, and record a stable code. No silent drop. |
| E-FX-2 | `attempt_epoch` transition mid-test | Lifecycle/session owner increments epoch, all prior-epoch queued items are discarded, current-epoch processing uses fresh phase. Stale-closure recorded for any live episode. |
| E-FX-3 | Listener subscribed/unsubscribed mid-boundary | Snapshot is stable for the boundary in progress; new listeners join only future boundaries. |
| E-FX-4 | Two events with equal `source_timestamp` | `fact_id` allocated in `(source_timestamp, source_event_class_rank, source_event_id)` order. Listener/thread arrival order is NOT an identity source. |
| E-FX-5 | Pause/resume mid-flight | Virtual clock freezes; Burst sim time does not advance; audio cues are NOT buffered; on resume, processing continues from the frozen virtual time with no paused-time catch-up. The capped catch-up path is tested only with an explicitly injected synthetic multi-tick delta. |
| E-FX-6 | `LivenessFact` published but relay never consumed | `op=open` is durable for the session; the FSM publishes it before the next hearing drain and Perception reads it at that next boundary. A publication after a drain is visible only at the following boundary. Stale-closure via `attempt_epoch` is the only `op=close` outside a normal terminal. |
| E-FX-7 | Pure-math oracle mismatch | Failure is recorded with the difference and quantizer; this is a fixture-oracle bug, not a gameplay bug. |
| E-FX-8 | Render stall during test | `Physics.autoSyncTransforms` cost and Event Bus/audio main-thread cost are inside the measurement boundary. Stall magnitude/rate are reported separately; non-hitch frames alone contribute to the p95/p99 steady-state gate. |
| E-FX-9 | Multiple Burst `flight_handle_id`s in one test | Each flight is independent; `flight_handle_id` is the per-flight internal allocation identity and serializes as `flight:<flight_handle_id>`; `fact_id` is allocated per landing. |
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
| `audio_cue_onset_tolerance_ms` | `[10, 40]` ms (default 22 — rev 2026-09-01, B5: must be ≥ the platform audio mix quantum; a 1024-sample block at 48 kHz ≈ 21.3 ms) | Player Noise GDD | Virtual-clock bound; wall-clock irrelevant |
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
| AC-FX-4 | Virtual-clock injection is used by every gameplay fixture; no fixture reads wall-clock time except the AC21 performance harness, which is the **sole** exception (AC19 audio-onset evidence is bound to the injected virtual clock — wall-clock drift is irrelevant). |
| AC-FX-5 | CompareTolerance is `5×10⁻³ m` for gameplay contracts; oracle checks quantize to `1×10⁻³ m` absolute; the quantizer is NOT a gameplay tolerance. |
| AC-FX-6 | Queue ordering is asserted as three independent keys: guard selection `guard_eid` ascending (with `stable_guard_snapshot_position` recorded but non-ordering), fact admission `(source_timestamp, source_event_class_rank, source_event_id, fact_id)`, and pair dispatch `(source_timestamp, source_event_class_rank, source_event_id, fact_id, guard_eid)`; the source-allocation key `(source_timestamp, source_event_class_rank, source_event_id)` is separate. Listener/thread arrival order is rejected as an identity source. |
| AC-FX-7 | Liveness closure matrix passes: every `op=open` has exactly one matching `op=close` keyed by `(session_id, attempt_epoch, guard_eid, entry_id)`; duplicate closes, mismatched keys, closes for rejected/disabled emitters, and closes across the wrong epoch fail; terminal and stale closes are both accepted only with their matching cause. The FSM is the sole writer, publishes `open/promote/close` before the next hearing drain, and Perception reads the latest published fact at that next boundary. A fact published after a drain is not applied retroactively; the fixture fails if a cross-boundary relay reserves a second `entry_id` before the opener is visible. |
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
