# ADR-0003: Audio Virtual Timestamp Boundary

## Status
Proposed

## Date
2026-08-30

## Engine Compatibility

| Field | Value |
|---|---|
| **Engine** | Unity 6 LTS (6000.3.17f1) |
| **Domain** | Audio / Gameplay Contracts |
| **Knowledge Risk** | LOW |
| **References Consulted** | `docs/architecture/adr-0001-event-messaging-bus.md`, `docs/architecture/adr-0002-physics-collision-contract.md`, `design/gdd/sound_performance_audit.md`, `design/registry/entities.yaml`, `docs/qa/noise-fixture-contract-report-2026-08-30.md` |
| **Verification Required** | Contract review and target-hardware measurement before any performance claim can leave `PENDING_OQ6` |

## Contract Notice

This ADR is a design contract, not runtime evidence. It defines the boundary
between gameplay evaluation time, audio request time, and audio integration
reporting so later capture can be compared against a stable spec.

## Context

The Player Noise review needs a stable audio boundary that does not confuse a
gameplay commit timestamp with a reported DSP sample start. The runtime may know
when a cue was requested from gameplay, and the audio integration may know when
the device reported the first sample, but those are different domains.

Without an explicit boundary, a design note can accidentally imply that wall-clock
time, a scheduled callback, or an inferred sample index is authoritative gameplay
timing. That would weaken deterministic replay, make pause/resume ambiguous, and
allow fabricated audio evidence to slip into the contract.

## Decision

Use a virtual-timestamp boundary with explicit fields for both the gameplay-side
request and the audio-integration report:

- `virtual_cue_request` is the gameplay commit or evaluation timestamp.
- `voice_instance_id` is the adapter-owned identity of the rendered voice.
- `dsp_start_sample` is the adapter-owned absolute DSP sample index reported by
  the audio integration; clip-relative or source-relative counters are diagnostic
  inputs only and cannot establish `confirmed` onset evidence.
- `dsp_start_sample_estimate` is the dedicated nullable estimate field for
  `onset_outcome=estimated`; it never populates `dsp_start_sample` and never
  mutates the immutable `onset_outcome`.
- `sample_rate` is an explicit session/device parameter.
- `epoch_offset` is the session/attempt mapping used to compare the virtual and DSP
  domains.

The boundary is one-way: gameplay may request audio, and the audio system may
report what it actually started, but gameplay timing never derives itself from a
wall-clock callback. If the DSP start is not reported, the contract records that as
missing evidence rather than inventing zero or another placeholder start.

For a reported sample index, the comparison value is derived only from registered
metadata. The canonical field vocabulary is `virtual_cue_request` (the
gameplay-side request timestamp) and the converted comparison value
`virtual_dsp_onset` (the fixture-spec name for this ADR's `virtual_dsp_start_s`;
both names denote the same converted quantity and the audio ADR must pin one
canonical spelling at OQ3 resolution):

```text
virtual_dsp_start_s = (dsp_start_sample / sample_rate) + epoch_offset
```

Here `sample_rate` is samples per second and `epoch_offset` is the virtual-session
time corresponding to DSP sample zero. If the integration reports seconds rather
than a sample index, it must still identify the registered sample-rate/epoch mapping
used for the conversion. Missing or non-finite metadata makes the onset non-passing.

### Onset outcome and evidence states

Each cue records one immutable `onset_outcome` and an append-only
`onset_trace_history[]`. The outcome is one of:

- `confirmed`: the adapter reports `voice_instance_id`, an absolute
  `dsp_start_sample`, `sample_rate`, `audio_clock_source`, `integration_source`,
  and `epoch_offset`; the derived start converts into the virtual domain within
  the registered `audio_cue_onset_tolerance_ms`;
- `estimated`: the integration supplies an estimate serialized only in the
  dedicated `dsp_start_sample_estimate` field (derived from the integration's
  own reported markers/duration — never an unregistered hardcoded latency
  constant); diagnostic only and non-passing;
- `pending`: no onset report has arrived yet; or
- `unsupported`: no qualifying audio integration is available.

Only `confirmed` is a passing onset state. A late confirmed report appends a
trace entry but cannot change an earlier `estimated`, `pending`, or `unsupported`
`onset_outcome` into a pass. The trace entry records the report status, source,
virtual comparison, and any diagnostic reason without mutating the immutable
outcome.

DSP conversion requires explicit `sample_rate` and `epoch_offset`; the boundary
must reject or mark non-passing any sample start that lacks those metadata. The
conversion never uses a wall-clock callback, an inferred pipeline latency, or a
numeric-zero placeholder as evidence. Limiter evidence is a separate locked
state: `limiter_reported`, `limiter_unsupported`, or restricted `not_applicable`.
`limiter_reported` is a typed record requiring finite `true_peak_dbTP`, nonempty
`limiter_stage_id`, `bus_id`, and `measurement_source`, and is the only passing
state for applicable audio. `limiter_unsupported` requires a reason and null
`true_peak_dbTP`; it is non-passing and remains the expected state until the OQ3
middleware decision supplies a qualifying true-peak report. `not_applicable`
requires reason `visual_only` or `out_of_scope`, null `true_peak_dbTP`, and is
allowed only for visual-only or explicitly out-of-scope cues; it is excluded from
passing audio calculations. A logical two-voice blend groups role-typed `body`
and `accent` child voice identities and absolute sample/provenance records under
one `audio_cue_id`; clip-relative child cursors do not establish confirmation. The platform binding is explicit but remains a design dependency rather than runtime evidence:

| Platform/profile | One named confirmed-readback mechanism | Evidence status |
|---|---|---|
| Unity PC stock audio | `AudioSource.timeSamples` is clip-relative adapter input/diagnostic data only; it cannot produce `confirmed` absolute DSP onset evidence | `unsupported`/`UNCAPTURED` until an approved adapter reports absolute DSP provenance |
| Unity WebGL stock audio | No qualifying DSP readback mechanism is registered; records remain `unsupported` | non-passing by contract |
| Wwise/OQ3 candidate | `GetSourcePlayPosition` remains pending; it may be `confirmed` only after an adapter supplies the full absolute-DSP contract above | pending OQ3 selection; no capture claimed |

No generic scheduled-start callback is evidence. This ADR does not claim an implementation or capture.

## Cue Identity and Deduplication

Every cue must carry a stable identity that survives retry and deduplication. The
minimum identity set is:

- `session_id`
- `attempt_epoch`
- `source_event_id` or `fact_id`
- `audio_cue_id`

An implementation may add more fields, but it may not weaken the identity or
reconstruct it from an unstable timestamp. Duplicate publish attempts, retries,
and cancel/retry loops are idempotent with respect to this identity. A stale cue
from an older epoch is suppressed before presentation and cannot resurrect audio
for a new attempt.

## Pause, Resume, and Cancellation

Pause freezes virtual gameplay time and Burst time together. While paused:

- virtual time does not advance;
- Burst simulation does not advance;
- audio cues are not buffered for later playback as if paused virtual time had
  elapsed.

Resume uses the registered capped catch-up path. The resume path may advance only
through the bounded backlog policy already registered elsewhere; it does not create
an unbounded audio replay queue.

Cancel paths are idempotent. A canceled cue stays canceled even if duplicate or
retry messages arrive later. If the cue belongs to a stale epoch, the stale epoch
suppression wins first and prevents presentation entirely.

## Measurement Boundary

The audio performance boundary is a design and measurement contract, not a
claim of captured evidence. Target-hardware measurements must record:

- `Physics.autoSyncTransforms` ownership as part of the shared simulation boundary;
- one bounded `SyncTransforms` call per Burst batch;
- Event Bus handoff cost;
- audio main-thread cost;
- hearing and Burst work cost;
- steady-state `p95` and `p99` for the relevant slice; and
- a separate 33 ms stall magnitude/rate record for labeled hitch samples.

The boundary must distinguish requested virtual cue timestamp from reported DSP
sample start, and it must preserve `PENDING_OQ6` for any measurement not captured
on target hardware. `UNCAPTURED` and `PENDING_OQ6` are fail-closed states, not
pass states.

## Consequences

### Positive

- Gameplay replay stays anchored to virtual time instead of wall-clock audio
  timing.
- The audio integration can report actual device start information without
  redefining gameplay truth.
- Retry, duplicate, cancel, and stale-epoch behavior are comparable across runtime
  and captured evidence.

### Negative

- Captured audio evidence must carry more explicit provenance fields.
- The implementation cannot rely on implied callback timing or inferred DSP start.
- Pause/resume behavior needs dedicated validation in the audio boundary harness.

## Validation Criteria

- A cue request records `virtual_cue_request` and never substitutes wall-clock
  time for gameplay evaluation time.
- A DSP start is stored as adapter-owned absolute `dsp_start_sample` only when
  the integration reports it with `voice_instance_id`, `sample_rate`,
  `audio_clock_source`, `integration_source`, and `epoch_offset` sufficient for
  conversion; Unity clip-relative `AudioSource.timeSamples` alone is rejected.
- Each cue has one immutable `onset_outcome` plus append-only
  `onset_trace_history[]`; only `confirmed` is passing, and late reports cannot
  rewrite the outcome.
- Limiter evidence is `limiter_reported`, `limiter_unsupported`, or restricted
  `not_applicable`; only `limiter_reported` passes applicable audio, while
  `limiter_unsupported` remains non-passing and `not_applicable` is excluded from
  passing calculations.
- Duplicate, retry, and cancel flows remain idempotent for the same cue identity.
- A stale-epoch cue is suppressed before presentation and never produces audio.
- Pause freezes virtual and Burst time, and resume uses the capped catch-up path.
- Performance records keep `PENDING_OQ6` until target-hardware capture exists.

