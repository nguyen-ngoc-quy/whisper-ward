# Performance/Determinism Audit — Player Noise Hearing System

**Build/Date**: 2026-08-29 (contract synchronization; measurement pending)
**Audit Type**: Player Noise design and harness-readiness audit; no runtime capture performed
**Scope**: Player Noise hearing/Burst/Perception remains the audited subsystem. HideSpot is covered only as a target-tier integration workload and whole-frame decomposition; this document does not claim a standalone HideSpot runtime gate or capture.
**Engine**: Unity 6 LTS (6000.3.17f1), URP, PC/WebGL target
**Frame Budget**: 16.6 ms at 60 fps; WebGL floor gate: 33.0 ms p95 at 30 fps

This audit is a design contract and measurement boundary, not runtime evidence. The HideSpot additions below register workload dimensions, deterministic invariants, and evidence fields without changing Player Noise values or claiming that any gate has passed.

## Gate Status

| Gate | Workload | Required result | Current evidence | Status |
|---|---|---:|---|---|
| Subsystem slice | **Supported MVP:** 1 guard, 1 due fact, one Burst flight, one due hearing boundary; `stress_30_guard_8_fact` is diagnostic-only coverage | Record ≤ 2.0 ms p95 **and** p99 as a subsystem decomposition measurement; not an independent acceptance gate | No Unity/target-WebGL capture | `PENDING_OQ6` |
| Aggregate subsystem record (diagnostic) | Registered hearing, Burst, and Perception workload in the target scenario | Record ≤ 12.0 ms p95 **and** p99; this is not an independent acceptance gate | No Unity/target-WebGL capture | `PENDING_OQ6` |
| HideSpot integration decomposition | Target-tier HideSpot profiles defined below; phase-2 flush plus FSM hold/catch work | No independent HideSpot millisecond gate; work is measured as a named decomposition of the whole-frame gate, with deterministic caps and ordering conditions satisfied | No Unity/target-WebGL capture | `PENDING_OQ6` |
| Whole-frame WebGL | Complete target frame, including Player Noise path, HideSpot integration, FSM catch-gate work, rendering, and other main-thread work | ≤ 33.0 ms p95 at the 30 fps floor; decomposition reconciles to the frame sample | No Unity/target-WebGL capture | `PENDING_OQ6` |
| Render-stall replay | One injected 33 ms render stall | Shared virtual clock freezes during stall; no virtual-time backlog accrues during it; post-resume processing continues without virtual-time catch-up; every stall magnitude ≤ `stall_magnitude_budget_ms` and every sliding 1 s hitch count ≤ `stall_rate_cap_per_s` | No capture | `PENDING_OQ6` |
| Deterministic replay | Same fixture, inputs, config, and virtual-clock trace across repetitions | Identical event/decision/landing trace and stable counters | No capture | `PENDING_OQ6` |

These are implementation gates, not claims that the design has passed them. OQ6
remains pending until real Unity and target-WebGL measurements exist.

## Registered Workload Profiles

The fixture and audit distinguish the supported MVP envelope from stress coverage.
The stress profile is deliberately bounded by the registered pair cap: 30 guards × 8
facts produces 240 candidate pairs, but only 30 pairs and 30 Linecasts may be
selected/evaluated in one boundary; the remainder is ordered deferred work or an
explicit terminal diagnostic. This is stress coverage, not an unlimited support
promise.

| Profile | Active guards | Due facts | Candidate pairs | Evaluated pairs / Linecasts | Burst flights | Evidence requirement |
|---|---:|---:|---:|---:|---:|---|
| `supported_mvp` | 1 | 1 | 1 | 1 / 1 | 1 | `budget_role=acceptance_gate`, `platform_scope=[WebGL]`; capture duration, counters, queue state, and replay identity |
| `stress_30_guard_8_fact` | 30 | 8 | 240 | 30 / 30 | 1 | `budget_role=diagnostic_only`, `required_budget_ms=null`; capture cap/deferred accounting, duration, and replay identity |

Both profiles are normative harness inputs, not captured results. The supported MVP
has the current whole-frame WebGL millisecond gate; the stress profile has no required
budget and can never pass or substitute for that gate. PC remains the primary
development target and must run the same functional/replay checks, but has no separate
millisecond threshold until a PC hardware profile is registered. Their `actual` fields
remain `UNCAPTURED` until a runner records typed observations; absent or partial
observations fail closed and leave OQ6 `PENDING_OQ6`.

### HideSpot Integration Workload Profiles

HideSpot is Target scope rather than part of the supported Player Noise MVP profile.
These profiles exercise the approved occupancy and witnessed-entry integration without
reclassifying HideSpot as an MVP content requirement. The spot-count value is an audit
fixture ceiling, not a level-density budget or a promise that arbitrary authored content
is supported. Every `actual` field is `UNCAPTURED` until a Unity/target-WebGL runner
records it.

| Profile | Authored HideSpots | Max occupied spots | Simultaneous `HideSpotFront` holders / guards | Max phase-2 occupancy transitions | Lifecycle exercise | Evidence status |
|---|---:|---:|---:|---:|---|---|
| `hide_target_single_holder` | 1 | 1 | 1 / 1 | 2 | none | `UNCAPTURED` |
| `hide_target_multi_guard_hold` | 1 | 1 | 4 / 4 | 2 | none | `UNCAPTURED` |
| `hide_target_reset_pool` | 32 | 1 | 4 / 4 | 64 | one run for each death/capture, respawn, segment reset, scene reload, full-room restart, and pool/unpool barrier | `UNCAPTURED` |

The profiles are bounded diagnostic workloads. Initial implementation rules reject
overlapping HideSpot volumes and permit one player occupant, so `max occupied spots = 1`
is a fixture validity condition, not a runtime selection rule. Multiple guards may hold
the same spot; the four-holder profile exists specifically to exercise the FSM's
round-robin catch-gate query contract. A profile that exceeds a listed cap is a new
workload requiring explicit registration and capture, not an implicit pass or a silent
increase to a performance budget.

#### HideSpot normative controls and evidence

The following controls are normative for these audit profiles. They bound the measured
work; they do not alter gameplay tuning or the existing Player Noise budgets. The two
HideSpot-specific queue/count controls are audit-fixture controls pending corresponding
runtime/registry ownership; until that ownership and a capture exist, their evidence is
`UNCAPTURED` and OQ6 remains `PENDING_OQ6`.

| Work item | Normative cap / identity | Required measurement or behavior |
|---|---|---|
| Authored HideSpot population | `hide_spot_count_per_fixture ≤ 32` | Enumerate the complete authored population once; report count, duplicate IDs, overlap rejections, and the profile ID. This is not a content-density cap. |
| Live occupancy | `max_occupied_hide_spots = 1` | A certified fixture has at most one occupied spot because it has one player and overlapping volumes are rejected; any larger value is invalid fixture input. |
| Simultaneous spot holds | `max_hide_spot_front_holders_per_spot = 4`; participating guards `≤ 4` | Report holder/guard count per spot and per boundary. A hunch/noise visit must not create a holder or catch clock. |
| Phase-2 flush | `max_hide_spot_occupancy_transitions_per_phase2 = 64` | Collect Physics trigger transitions, synchronously flush `Empty`/`Occupied` before Perception and FSM, and record flush duration, transition count, publication count, and ordering indices. |
| Occupancy event queue | `hide_spot_occupancy_queue_capacity = 128` envelopes | Queue depth/capacity, enqueue/dequeue, retry, rejection, stale-drop, and allocated bytes are recorded. Retry/backpressure precedes stable reject-newest; no accepted transition is silently dropped. |
| Occupancy deduplication | Immutable occupancy identity `(session_id, attempt_epoch, hide_spot_id, transition_id)`; never `entry_id` or `occupied` alone | Duplicate delivery of one logical transition emits one state change and one zone fact. A later real `Empty → Occupied` cycle remains admissible, including same-tick re-entry deferral. |
| Reset reconciliation | `max_reset_reconciliation_spots = 32` per barrier | On every listed reset/pool barrier, clear stale transitions and transient association, then perform a fresh containment query. Record spot queries, stale facts rejected, old-epoch authority rejected, allocations, and completion time. Pool/unpool preserves scheduler phase and does not increment `attempt_epoch`. |
| FSM catch-gate path queries | `≤ 1` `CalculatePath` catch-gate query across all simultaneous holders per virtual tick; `≤ 1` per guard per tick | Record path-query count, Euclidean/backstop evaluations, Linecasts, queryless ticks, carried verdicts, and same-tick holder-pair count. The same-tick pair count must be zero; queryless ticks carry the prior verdict for the same live fix. |

The occupancy queue capacity and the 32/64/128 HideSpot workload controls are
measurement controls for this audit, not claims that a runtime implementation or a
registry gate already exists. The authoritative Player Noise controls remain
`max_hearing_guards_per_boundary = 30`, `max_hearing_facts_per_boundary = 8`,
`max_hearing_pairs_per_boundary = 30`, `perception_hearing_work_capacity = 512`, and
the existing Event Bus/perception queue capacities. HideSpot work must be reported
separately rather than charged to those Player Noise values.

## Measurement Contract

The harness must use the injected session virtual clock and a fixed configuration
snapshot. It must record:

- target, build identifier, engine version, scene/fixture ID, schema version, and
  configuration hash;
- warm-up frame count, measured repetitions, sample count, and excluded samples with
  an explicit reason;
- `minimum_steady_state_samples=100` eligible non-excluded frame samples per profile;
- frame and boundary duration p50, p95, p99, and maximum, using nearest-rank
  `rank=ceil(p × N)` over the ascending eligible sample list; `max` is the largest
  eligible sample;
- allocations and allocated bytes per sample; these are required measurements, but this audit registers no standalone allocation-byte pass/fail budget;
- guard-selection count, guard-fact count, guard/fact pair count, actual Linecast
  count, SphereCast count, `SyncTransforms` count, and publication count;
- HideSpot population count, occupied-spot count, simultaneous holder/guard count,
  Physics trigger-transition count, phase-2 flush duration p50/p95/p99/max, phase-2
  allocations/allocated bytes, occupancy publication count, dedup-hit count, duplicate
  suppression count, stale-drop count, reset-containment query count, and reset/pool
  completion duration;
- HideSpot occupancy-event queue depth/capacity, enqueue/dequeue/retry/rejection
  counts, stable overflow/rejection codes, and old-epoch rejection count;
- FSM HideSpotFront holder count, catch-gate `CalculatePath` count, backstop evaluation
  count, catch-gate Linecast count, queryless-tick count, carry-forward verdict count,
  and same-tick multi-guard query-pair count;
- Event Bus pending-envelope queue depth/capacity, Perception hearing queue
  depth/capacity, deferred-boundary count/capacity, retry count, rejection count,
  and stable overflow/rejection codes;
- Burst tick count, accumulator remainder, backlog state, and
  `burst-backlog-clamped` diagnostics;
- `Physics.autoSyncTransforms` ownership and one bounded `SyncTransforms` call per
  Burst batch;
- Event Bus handoff cost, audio main-thread cost, and hearing/Burst work cost
  inside the measured subsystem boundary;
- render-stall magnitude and sliding one-second hitch rate, reported separately
  from steady-state samples; labeled stalls and any unlabeled frame above the
  nominal interval are classified in the hitch ledger (`labeled_stall` or
  `spontaneous_hitch`) and excluded from percentile aggregation only with that
  explicit reason; a profile with fewer than 100 eligible samples is `UNCAPTURED`,
  not extrapolated;
- audio event count, callback measurement count, gameplay-side `virtual_cue_request`,
  adapter-owned `voice_instance_id`, absolute `dsp_start_sample`, dedicated nullable
  `dsp_start_sample_estimate` for diagnostic `estimated` records, explicit `sample_rate`,
  `audio_clock_source`, `integration_source`, explicit `epoch_offset`, converted
  `virtual_dsp_onset`, onset samples against the registered 22 ms tolerance, and
  limiter/true-peak result where audio is in scope; clip-relative Unity
  `AudioSource.timeSamples` is diagnostic input only and cannot establish confirmed
  absolute DSP onset evidence;
- a deterministic replay comparison against the reference trace, including first
  differing sample and field when a mismatch occurs.

The result is `PASS` only when all required fields are present, no required sample is
missing, no registered cap gate fails, and the replay comparison is identical.
Allocation counts and bytes remain mandatory evidence, but cannot pass or fail this
audit until a separate allocation budget is approved and registered. `UNCAPTURED`,
`UNKNOWN`, `PENDING_OQ6`, a missing percentile, fewer than 100 eligible samples, or
an invalid timer reconciliation is not a pass value. Budget evidence is serialized
as the typed union `{status: PENDING_OQ6, value_ms: null} | {status: CAPTURED, value_ms: finite}`;
`PENDING_OQ6` is never a scalar measurement.

### Explicit stall pass/fail rules

For every labeled stall sample, compute `stall_magnitude_ms` from the measured render
interval minus the configured nominal frame interval, using the same frame-time source
as the steady-state sample. The magnitude leg passes only when every labeled stall has
`stall_magnitude_ms ≤ stall_magnitude_budget_ms` (33 ms); one sample above the limit is
a failure. Count hitch samples by render-frame index in a sliding one-second wall-clock
window, using the same measured frame-time source as the magnitude leg; virtual trace
boundaries may label the sample but do not define the window. The rate leg passes only
when every window has `hitch_count ≤ stall_rate_cap_per_s` (1 hit/s); the `(N+1)`th
hitch in any one-second window fails the run. A run with no labeled stall sample is
`UNCAPTURED`, not a pass. An unlabeled frame that exceeds the nominal interval is
not silently discarded: it is classified `spontaneous_hitch`, entered into the
same hitch ledger, and either included in the stall-rate/magnitude checks when it
is a measured render stall or invalidates the sample when its source is unknown.
These two legs are independent of the steady-state p95/p99 exclusion: excluding a
labeled hitch from percentile aggregation does not waive either stall limit.

## Frame-Time Scope and Work Caps

The 2.0 ms value is the supported-MVP **1-guard/1-fact/one-flight hearing, Burst,
and Perception subsystem slice** decomposition measurement, not an independent
acceptance gate. The `stress_30_guard_8_fact` profile is diagnostic coverage only:
240 candidate pairs are intentionally bounded to 30 evaluations and 210 ordered
deferrals, and its result is never a supported-performance pass. The 12.0 ms value
is the aggregate slice record for those subsystems, also diagnostic/decomposition
only; neither value is the whole WebGL frame or a replacement for the authoritative
33.0 ms whole-frame WebGL p95 gate at the 30 fps floor. Hitch frames are recorded
separately and excluded from the steady-state percentile sample; they must still pass
the virtual-clock and replay assertions.

The following registered caps are normative workload controls. The stress profile
exercises them for coverage and accounting only; it has no required performance
budget and cannot be promoted into a support promise by a passing diagnostic
sample:

| Work item | Registered control | Required behavior |
|---|---|---|
| Hearing boundaries per render frame | `max_hearing_boundaries_per_frame` | Drain due boundaries in ascending order up to the cap; retain and diagnose deferred boundaries |
| Guard candidates per boundary | `max_hearing_guards_per_boundary` = 30 | Deterministic guard selection; records selected and deferred/rejected work |
| Event Bus pending envelopes | `event_bus_pending_envelope_capacity` | Retry/backpressure before stable rejection; no silent loss |
| Perception hearing facts | `perception_hearing_queue_capacity` | Retry/backpressure before stable rejection; preserve source identity |
| Deferred hearing boundaries | `max_deferred_hearing_boundaries` | Retain ordered backlog up to capacity; report overflow rather than dropping it |
| Event Bus ingress retries | `event_bus_retry_attempts` | Retry the immutable envelope before reporting rejection |
| Burst simulation | `max_backlog_ticks` = 8 | Run due fixed ticks up to the cap and record any excess clamp |

A 30-guard cap limits selected guard work for one boundary; it does not imply one
Linecast when multiple facts are due. The harness must report facts, selected
pairs, and actual queries independently. Unevaluated work is deferred or explicitly
rejected with serialized accounting.

### HideSpot Integration Acceptance Conditions

These conditions apply when one of the HideSpot profiles is captured. They are
acceptance rules, not evidence that the current repository has passed them; with no
Unity or target-WebGL capture, the result remains `PENDING_OQ6` and profile `actual`
fields remain `UNCAPTURED`.

1. **Bounded workload.** The fixture enumerates the complete authored population and
   remains within the declared 32-spot, one-occupant, four-holder/four-guard, and
   64-phase-2-transition caps. Overlap rejection is reported explicitly; it is not
   converted into a nearest-spot runtime path or a performance pass.
2. **Phase-2 ordering.** On every captured virtual boundary, Physics trigger
   collection precedes the synchronous HideSpot `Empty`/`Occupied` flush, which
   precedes Perception and then FSM. A same-tick exit records `Empty` before the FSM
   capture check and produces no `Capture`; the trace must expose both indices.
3. **Queue and dedup integrity.** Occupancy queue depth never exceeds its declared
   capacity without a retry/rejection record. Duplicate delivery of one immutable
   transition creates no second logical state change or zone fact, while a later real
   re-entry is not suppressed. No accepted transition is silently lost, and every
   rejection/stale drop carries a stable code and session/epoch identity.
4. **Reset and pool containment.** For each lifecycle barrier in the reset/pool
   profile, pre-barrier occupancy, hide-dive, and witnessed-authority work cannot be
   consumed afterward. A fresh containment query is the only source allowed to leave a
   spot `Occupied` in the new session/epoch. Pool/unpool increments no epoch and
   preserves the scheduler phase; reset reconciliation stays within the 32-spot cap.
5. **Multi-guard catch-gate determinism.** With four simultaneous holders/guards,
   no virtual tick emits more than one catch-gate `CalculatePath` query across the
   holders and no guard emits more than one. The same-tick query-pair count is zero;
   queryless holders use the prior verdict for the same live fix. Actual path,
   backstop, and Linecast counts are reported separately rather than inferred from
   holder count.
6. **WebGL whole-frame decomposition.** The captured frame sample uses disjoint,
   reconciled components:
   `whole_frame_ms = render_ms + player_noise_path_ms + hidespot_phase2_ms +`
   `fsm_catch_gate_ms + other_main_thread_ms`.
   `player_noise_path_ms` retains the existing Player Noise boundary and values;
   HideSpot phase-2 and FSM work are named additions, not silently charged to the
   2.0 ms or 12.0 ms Player Noise records. The whole-frame acceptance leg passes only
   when p95 is ≤ `webgl_whole_frame_budget_p95_ms` (33.0 ms) at the 30 fps floor and
   the residual is within the documented timer resolution. Missing, partial, or
   `UNCAPTURED` components fail closed.
7. **Replay identity.** Repeating a profile with the same fixture, input stream,
   configuration, lifecycle barriers, and virtual-clock trace produces identical
   occupancy transitions, phase indices, queue/dedup counters, reset rejections,
   holder scheduling, catch-gate query order, and landing/capture decisions. A first
   differing field is required on mismatch; aggregate duration alone is insufficient.

### Measured subsystem boundary

The timed Player Noise subsystem slice starts immediately before the Event Bus
phase-drain and includes ingress handoff bookkeeping, Perception raw-fact admission,
hearing guard/fact selection, every actual hearing Linecast, relay publication, Burst
fixed-tick work, the single pre-batch `SyncTransforms`, every Burst SphereCast,
terminalization, and the main-thread audio bridge work caused by those commits. It
ends after those publications and audio-bridge calls return. The report must
include component durations for Event Bus dispatch/handoff, Perception
selection/evaluation/deferred work, `SyncTransforms`, Burst simulation/queries,
and audio bridge work; their sum must reconcile with the measured Player Noise
subsystem slice within the timer's documented resolution. Timer ownership is
non-overlapping: each owner measures only its named boundary, child intervals are
subtracted from the parent's raw interval before reconciliation, and an overlap or
unexplained residual beyond the registered timer resolution records
`TIMER_BOUNDARY_INVALID` and cannot pass. DSP callback execution and speaker output
are recorded as separate audio evidence, not used as gameplay timing authority.

For a HideSpot integration profile, the report adds a separate timed boundary for
Physics trigger collection and the synchronous phase-2 HideSpot occupancy flush, plus
a separately tagged FSM `HideSpotFront` catch-gate component. Those components include
all occupancy queue/dedup/reset work and all actual catch-gate path/backstop/Linecast
work invoked by the profile. They are not retroactively included in the existing
2.0 ms Player Noise slice or the 12.0 ms noise-path subsystem-aggregate diagnostic
record; the complete frame decomposition reports them explicitly. Any NavMesh
catch-gate call count remains separately tagged, including its guard, spot, virtual
boundary, query-slot, and carry-forward verdict.

## Scheduler and Clock Verification

- Hearing runs on the dedicated 5 Hz `T_hearing=0.2 s` phase of the shared injected
  virtual clock, additive to the separate 2–5 Hz vision tick.
- When HideSpot content is enabled, the boundary order is Physics trigger collection
  → synchronous HideSpot phase-2 occupancy flush → Perception fact collection/hearing
  drain → FSM decision/catch check. The flush is part of the integration measurement,
  and `Empty` must be visible to the FSM before any same-tick capture decision.
- The scheduler phase is stable across pooling and component lifecycle changes.
  `Update()`, `Time.unscaledTime`, and enable/disable transitions are not gameplay
  timing authority. Pool/unpool does not increment `attempt_epoch`; reset barriers
  invalidate old work before a fresh containment query can restore occupancy.
- A fact at an exact due boundary is eligible in that boundary. Multiple due
  boundaries drain in ascending order, and facts in one boundary sort by
  `(source_timestamp, source_event_class_rank, source_event_id, fact_id)`.
- The Event Bus pending queue drains before the Perception hearing queue receives the
  exactly-once handoff. Queue depths and retry/rejection counters are separate.
- Each hearing pair executes at most one Linecast for that fact/boundary evaluation.
  The actual query counter, not an estimated per-guard microsecond figure, is the
  oracle.
- Vision/path query staggering remains a separate Perception contract. Hearing
  cadence does not share or steal the vision tick slot.
- During pause, virtual time does not advance, Burst time does not advance, and
  input edges or audio cues are not buffered across resume. Resume uses the
  registered capped catch-up path rather than replaying paused audio.

## Transform and Physics Verification

The Burst service finalizes authored solids and performs exactly one global
`Physics.SyncTransforms()` before each simulation batch. The E20 mask applies to the
subsequent Linecast/SphereCast queries, not to synchronization. The service keeps
`Physics.autoSyncTransforms=false` for its lifetime, never toggles it per query, and
records the prior setting on disposal.

Burst uses the custom fixed-step service with `dt_max=1/120 s`, retained fractional
remainder, and the registered backlog cap. A fixed tick uses one closest-hit
SphereCast over the center segment `p_current → p_next`; its `maxDistance` is the
center-segment length and its shape radius is `projectile_radius`. Initial overlap,
contact push-out, timeout precedence, and preview/runtime parity are separate
counters and assertions.

## Performance Risks Requiring Measurement

1. **NavMesh path sampling and catch-gate work** — the Perception path query is a
   synchronous operation with cost dependent on geometry. The Euclidean pre-gate,
   re-sample cap, and guard staggering must be visible in call-site-tagged counters;
   no unmeasured microsecond estimate substitutes for the p95/p99 capture.
2. **Multiple facts at one hearing boundary** — guard selection, fact evaluation,
   and pair counts can diverge. The bounded queues and deferred-boundary cap must
   preserve deterministic order while preventing a burst of due facts from becoming
   an invisible frame spike.
3. **Global transform synchronization** — `SyncTransforms` is one global operation
   per Burst batch and must be included in both duration and allocation reports.
4. **Backpressure and allocations** — retries, queue admission, stale-event
   recording, and rejection diagnostics must be allocation-counted. A rejected fact
   must not be mistaken for successfully evaluated work.
5. **Audio timing and presentation** — DSP callbacks are measurement-only. Gameplay
   timing remains on the virtual clock; the audit must distinguish requested
   virtual cue timestamp from adapter-owned absolute DSP provenance, including
   `voice_instance_id`, `dsp_start_sample`, `sample_rate`, `audio_clock_source`,
   `integration_source`, and `epoch_offset`. Unity `AudioSource.timeSamples` is
   clip-relative and cannot produce a confirmed onset by itself. Keep cue identity
   stable across retry/cancel,
   and report one logical footstep event in the audio integration (middleware-
   neutral — the concrete middleware is the OQ3 ADR's decision; Wwise is the
   leading candidate, not an approved dependency), its body/accent voices, onset
   tolerance, and true-peak limiter result separately from gameplay frame time.
6. **HideSpot phase-2 occupancy work** — trigger transition collection, synchronous
   `Empty`/`Occupied` publication, bounded queue admission, deduplication, and reset
   reconciliation scale with the authored spot population. The fixture cap and queue
   capacity must be visible in duration, allocation, depth, stale-drop, and rejection
   counters; no unbounded editor sweep or silent queue loss is a runtime result.
7. **Simultaneous HideSpotFront holders** — four guards holding one occupied spot
   exercise the FSM's round-robin catch-gate schedule. The expensive path query is
   capped at one across holders per virtual tick, while live backstop work and
   carry-forward verdicts remain separately measurable. A holder-count estimate is
   not a substitute for actual `CalculatePath`/Linecast counters.
8. **Whole-frame WebGL attribution** — the 33.0 ms p95 gate must include Player Noise,
   HideSpot phase-2, FSM catch-gate, rendering, and other main-thread work as disjoint
   components. A passing subsystem slice or a missing HideSpot component cannot be
   treated as a whole-frame pass; absent capture remains `PENDING_OQ6`.

## Render-Stall and Backlog Replay

The harness injects a 33 ms render stall and labels the affected frames. During the
stall the shared virtual clock freezes, so no virtual-time hearing boundary, Burst
flight time, or backlog accrues for the stalled interval. After resume, processing
continues from the frozen virtual time; there is no paused-time catch-up and no
replay of audio or sensing work. The separate `max_backlog_ticks` cap is exercised
only by an explicitly injected synthetic multi-tick delta, never by the render-stall
stimulus. A render stall must not be relabeled as an ordinary slow-device catch-up
frame, and a synthetic catch-up clamp must not delete already published sensing facts.

The replay compares event identities, boundary timestamps, queue transitions, query
counts, Burst landing/contact, timeout outcome, FSM decision order, and liveness
publication order. Every FSM `open/promote/close` must be published before the next
hearing drain; a publication after a drain is visible only at the following boundary.
Any mismatch reports the first differing field rather than passing on aggregate duration alone.

## Audio and Accessibility Measurement Boundary

The footstep implementation emits one logical blend-container event per
committed step (middleware-neutral — the concrete container type is fixed by the
OQ3 ADR; Wwise's Blend Container is the leading candidate, not an approved
dependency). Body/accent layers are two engine voices under that event;
selection is **bound-parameter-driven** (a bound crossfade parameter — never
named after a middleware-specific control surface). The audit records the
approximately `−18 LUFS` body
target, `−12 LUFS` accent target, the `≤ −1 dBTP` footstep-bus limiter result
(as an explicit evidence state — `limiter_reported`, `limiter_unsupported`, or
restricted `not_applicable`, per the Player Noise GDD's AC19 rev 2026-09-01;
`limiter_reported` is the only passing applicable-audio state, `limiter_unsupported`
remains fail-closed, and `not_applicable` is only for visual-only/out-of-scope cues;
stock Unity audio ships no true-peak limiter, so the state may be non-passing until the OQ3 middleware
lands), and `audio_cue_onset_tolerance_ms=22` (rev 2026-09-01, B5 — the prior
20 ms default sat below a 1024-sample/48 kHz mix quantum ≈ 21.3 ms). The
gameplay-side request timestamp is `virtual_cue_request`; the audio-side adapter-owned
absolute DSP sample start is separate. When provenance is complete, the converted
comparison value is `virtual_dsp_onset = (dsp_start_sample / sample_rate) + epoch_offset`.
**AC19/AC21 audio-evidence rule:** `onset_outcome=confirmed` is the only passing onset
state, and a confirmed record requires an adapter-reported `voice_instance_id` plus
absolute `dsp_start_sample`, with `sample_rate`, `audio_clock_source`,
`integration_source`, and `epoch_offset` identifying the instrumented sample
provenance. `estimated` stores an estimate only in `dsp_start_sample_estimate`; it
never populates `dsp_start_sample` or mutates the immutable outcome. Unity
`AudioSource.timeSamples` alone is clip-relative diagnostic input and cannot satisfy
this contract. `estimated`, `pending`, `unsupported`, missing, `UNCAPTURED`, and
`PENDING_OQ6` are non-passing states. OQ3 middleware authority remains unresolved;
this audit does not approve Wwise or any other middleware and does not define a
DSP-to-virtual-clock mapping. These targets are pending audio capture and do not
make DSP callbacks gameplay authority.

The stable cue identity for this boundary is at least
`(session_id, attempt_epoch, source_event_id or fact_id, audio_cue_id)`. Cancel,
duplicate, retry, and stale-epoch suppression are idempotent: a cue can be
rejected, retried, or canceled multiple times without producing multiple
presentations, and an older epoch cannot present audio after a newer epoch is
active. Paused time never accumulates buffered audio.

Accessibility verification records caption/haptic cue emission, fallback/mono audio,
static non-color indicators, reduced-motion/no-flash behavior, remapping/gamepad
coverage, pause edge suppression, browser-reserved-key fallback, and private
feedback for blocked or rejected Burst actions. Proxy feedback is counted only after
an authoritative noise-caused `investigate-commit` and must not expose guard
identity, count, hidden state, or position.

## Audit Conclusion

The current contract has explicit Player Noise caps, HideSpot integration workload
bounds, phase-2 ordering, occupancy queue/dedup and reset containment checks,
deterministic retry/backpressure, separate hearing and vision timing, transform-sync
accounting, FSM multi-guard catch-gate counters, and a whole-frame decomposition
record shape. HideSpot remains target-tier integration coverage only; this is still a
Player Noise audit and does not claim a standalone HideSpot performance gate. No
runtime, Unity Test Framework, Wwise, WebGL, or target-hardware evidence is present
in this repository. The gates and all new integration evidence therefore remain
`PENDING_OQ6`; profile `actual` values remain `UNCAPTURED`, and a design review or
static audit cannot convert them to passing evidence.
