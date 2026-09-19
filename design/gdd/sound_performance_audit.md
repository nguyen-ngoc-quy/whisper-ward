# Performance/Determinism Audit — Player Noise Hearing System

**Build/Date**: 2026-08-29 (contract synchronization; measurement pending)
**Audit Type**: design and harness-readiness audit; no runtime capture performed
**Engine**: Unity 6 LTS (6000.3.17f1), URP, PC/WebGL target
**Frame Budget**: 16.6 ms at 60 fps

This audit is a design contract and measurement boundary, not runtime evidence.

## Gate Status

| Gate | Workload | Required result | Current evidence | Status |
|---|---|---:|---|---|
| Subsystem slice | **Supported MVP:** 1 guard, 1 due fact, one Burst flight, one due hearing boundary; `stress_30_guard_8_fact` is diagnostic-only coverage | ≤ 2.0 ms p95 **and** p99 | No Unity/target-WebGL capture | `PENDING_OQ6` |
| Aggregate subsystem slice | Registered hearing, Burst, and Perception workload in the target scenario | ≤ 12.0 ms p95 **and** p99 | No Unity/target-WebGL capture | `PENDING_OQ6` |
| Render-stall replay | One injected 33 ms render stall | Shared virtual clock freezes during stall; no virtual-time backlog accrues during it; post-resume catch-up follows the registered cap; every stall magnitude ≤ `stall_magnitude_budget_ms` and every sliding 1 s hitch count ≤ `stall_rate_cap_per_s` | No capture | `PENDING_OQ6` |
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
| `supported_mvp` | 1 | 1 | 1 | 1 / 1 | 1 | Capture duration, counters, queue state, and replay identity |
| `stress_30_guard_8_fact` | 30 | 8 | 240 | 30 / 30 | 1 | Capture cap/deferred accounting, duration, and replay identity |

Both profiles are normative harness inputs, not captured results. Their `actual`
fields remain `UNCAPTURED` until a runner records typed observations; absent or partial
observations fail closed and leave OQ6 `PENDING_OQ6`.

## Measurement Contract

The harness must use the injected session virtual clock and a fixed configuration
snapshot. It must record:

- target, build identifier, engine version, scene/fixture ID, schema version, and
  configuration hash;
- warm-up frame count, measured repetitions, sample count, and excluded samples with
  an explicit reason;
- frame and boundary duration p50, p95, p99, and maximum;
- allocations and allocated bytes per sample;
- guard-selection count, guard-fact count, guard/fact pair count, actual Linecast
  count, SphereCast count, `SyncTransforms` count, and publication count;
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
  from steady-state samples;
- audio event count, callback measurement count, requested virtual cue timestamp,
  reported DSP sample start, explicit `sample_rate`, explicit `epoch_offset`, onset
  samples against the 20 ms tolerance, and limiter/true-peak result where audio is
  in scope;
- a deterministic replay comparison against the reference trace, including first
  differing sample and field when a mismatch occurs.

The result is `PASS` only when all required fields are present, no required sample is
missing, no allocation or cap gate fails, and the replay comparison is identical.
`UNCAPTURED`, `UNKNOWN`, `PENDING_OQ6`, or a missing percentile is not a pass
value.

### Explicit stall pass/fail rules

For every labeled stall sample, compute `stall_magnitude_ms` from the measured render
interval minus the configured nominal frame interval, using the same frame-time source
as the steady-state sample. The magnitude leg passes only when every labeled stall has
`stall_magnitude_ms ≤ stall_magnitude_budget_ms` (33 ms); one sample above the limit is
a failure. Count hitch timestamps in a sliding one-second window using the same virtual
trace boundaries. The rate leg passes only when every window has
`hitch_count ≤ stall_rate_cap_per_s` (1 hit/s); the `(N+1)`th hitch in any one-second
window fails the run. A run with no labeled stall sample is `UNCAPTURED`, not a pass.
These two legs are independent of the steady-state p95/p99 exclusion: excluding a
labeled hitch from percentile aggregation does not waive either stall limit.

## Frame-Time Scope and Work Caps

The 2.0 ms gate is the supported-MVP **1-guard/1-fact/one-flight hearing, Burst,
and Perception subsystem slice**. The `stress_30_guard_8_fact` profile is diagnostic
coverage only: 240 candidate pairs are intentionally bounded to 30 evaluations and
210 ordered deferrals, and its result is never a supported-performance pass. The
12.0 ms gate is the aggregate slice for those subsystems, not the whole WebGL frame
and not a replacement for the 16.6 ms frame budget. Hitch
frames are recorded separately and excluded from the steady-state percentile sample;
they must still pass the virtual-clock and replay assertions.

The following registered caps are normative workload controls:

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

### Measured subsystem boundary

The timed subsystem slice starts immediately before the Event Bus phase-drain and
includes ingress handoff bookkeeping, Perception raw-fact admission, hearing
guard/fact selection, every actual hearing Linecast, relay publication, Burst
fixed-tick work, the single pre-batch `SyncTransforms`, every Burst SphereCast,
terminalization, and the main-thread audio bridge work caused by those commits. It
ends after those publications and audio-bridge calls return. The report must
include component durations for Event Bus dispatch/handoff, Perception
selection/evaluation/deferred work, `SyncTransforms`, Burst simulation/queries,
and audio bridge work; their sum must reconcile with the measured subsystem slice
within the timer's documented resolution. DSP callback execution and speaker
output are recorded as separate audio evidence, not used as gameplay timing
authority. NavMesh catch-gate work is included only when the measured fixture
invokes it, and its call count remains separately tagged.

## Scheduler and Clock Verification

- Hearing runs on the dedicated 5 Hz `T_hearing=0.2 s` phase of the shared injected
  virtual clock, additive to the separate 2–5 Hz vision tick.
- The scheduler phase is stable across pooling and component lifecycle changes.
  `Update()`, `Time.unscaledTime`, and enable/disable transitions are not gameplay
  timing authority.
- A fact at an exact due boundary is eligible in that boundary. Multiple due
  boundaries drain in ascending order, and facts in one boundary sort by
  `(source_timestamp, fact_id)`.
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
   virtual cue timestamp from reported DSP sample start, preserve explicit
   `sample_rate` and `epoch_offset`, keep cue identity stable across retry/cancel,
   and report one logical Wwise footstep event, its body/accent voices, onset
   tolerance, and true-peak limiter result separately from gameplay frame time.

## Render-Stall and Backlog Replay

The harness injects a 33 ms render stall and labels the affected frames. During the
stall the shared virtual clock freezes, so no virtual-time hearing boundary or Burst
backlog accrues for the stalled interval. After resume, ordinary fixed-tick catch-up
runs under `max_backlog_ticks`; only excess accumulated virtual time beyond that cap
is discarded and diagnosed. A render stall must not be relabeled as an ordinary
slow-device catch-up frame, and a catch-up clamp must not delete already published
sensing facts.

The replay compares event identities, boundary timestamps, queue transitions, query
counts, Burst landing/contact, timeout outcome, and FSM decision order. Any mismatch
reports the first differing field rather than passing on aggregate duration alone.

## Audio and Accessibility Measurement Boundary

The footstep implementation emits one logical Wwise Blend Container event per
committed step. Body/accent layers are two engine voices under that event;
selection is RTPC-driven. The audit records the approximately `−18 LUFS` body
target, `−12 LUFS` accent target, `≤ −1 dBTP` footstep-bus limiter result, and
`audio_cue_onset_tolerance_ms=20`. The gameplay-side request timestamp is the
virtual cue request; the audio-side reported DSP sample start is separate and may
only be recorded when the integration actually reports it. These targets are
pending audio capture and do not make DSP callbacks gameplay authority.

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

The current contract has explicit caps, queue ownership, deterministic retry and
backpressure, separate hearing and vision timing, transform-sync accounting, and a
complete measurement record shape. No runtime, Unity Test Framework, Wwise, WebGL,
or target-hardware evidence is present in this repository. The gates therefore remain
`PENDING_OQ6`; a design review or static audit cannot convert them to passing evidence.
