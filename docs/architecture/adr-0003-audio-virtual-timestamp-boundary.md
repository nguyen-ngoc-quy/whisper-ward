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
- `reported_dsp_sample_start` is the sample index or time returned by the audio
  integration.
- `sample_rate` is an explicit session/device parameter.
- `epoch_offset` is the session/attempt mapping used to compare the virtual and DSP
  domains.

The boundary is one-way: gameplay may request audio, and the audio system may
report what it actually started, but gameplay timing never derives itself from a
wall-clock callback. If the DSP start is not reported, the contract records that as
missing evidence rather than inventing zero or another placeholder start.

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
- A reported DSP start is stored only when the integration actually reports it.
- Duplicate, retry, and cancel flows remain idempotent for the same cue identity.
- A stale-epoch cue is suppressed before presentation and never produces audio.
- Pause freezes virtual and Burst time, and resume uses the capped catch-up path.
- Performance records keep `PENDING_OQ6` until target-hardware capture exists.

