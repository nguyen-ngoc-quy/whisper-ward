# Audio Boundary Evidence Template

> This template is a capture contract, not runtime evidence. Its field vocabulary is
> aligned with `docs/architecture/adr-0003-audio-virtual-timestamp-boundary.md`.

## Required Capture Rules

- Never infer gameplay time from wall-clock audio callbacks.
- Never treat a fire-and-forget scheduler acknowledgement, clip-relative
  `AudioSource.timeSamples`, or an estimated sample position as absolute DSP evidence.
- Never default a missing DSP sample start to `0`.
- Use `UNCAPTURED` when a required observation was not recorded.
- Use `PENDING_OQ6` for measurements that still require target-hardware capture.
- Unity stock PC and stock WebGL audio remain `unsupported` for qualifying absolute
  DSP onset evidence until an approved instrumented adapter exists; WebGL must not
  silently fall back to wall-clock or estimated evidence.
- Keep stale-epoch suppression and idempotent retry/cancel behavior visible in the record.
- `confirmed` is the only passing onset state. `estimated`, `pending`, `unsupported`,
  and `UNCAPTURED` are non-passing. `limiter_unsupported` is non-passing for the
  limiter leg.

## Record Schema

```yaml
audio_boundary_record:
  status: PASS | FAIL | UNCAPTURED | PENDING_OQ6
  capture_source: runtime | harness | target_hardware | manual_review
  session_id: REQUIRED_SESSION_ID
  attempt_epoch: REQUIRED_ATTEMPT_EPOCH
  source_event_id: REQUIRED_SOURCE_EVENT_ID_OR_NULL
  fact_id: REQUIRED_FACT_ID_OR_NULL
  audio_cue_id: REQUIRED_AUDIO_CUE_ID
  virtual_cue_request: REQUIRED_VIRTUAL_TIMESTAMP
  virtual_dsp_onset: REQUIRED_VIRTUAL_TIMESTAMP_OR_NULL
  voice_instance_id: REQUIRED_VOICE_INSTANCE_ID_OR_NULL
  dsp_start_sample: REQUIRED_ABSOLUTE_SAMPLE_OR_UNCAPTURED
  dsp_start_sample_estimate: null | ESTIMATED_ABSOLUTE_SAMPLE
  sample_rate: REQUIRED_SAMPLE_RATE_HZ_OR_NULL
  epoch_offset: REQUIRED_EPOCH_OFFSET_OR_NULL
  audio_clock_source: REQUIRED_ADAPTER_CLOCK_OR_UNSUPPORTED
  integration_source: REQUIRED_ADAPTER_OR_UNSUPPORTED
  onset_outcome: confirmed | estimated | pending | unsupported | UNCAPTURED
  onset_trace_history: []
  limiter_evidence: limiter_reported | limiter_unsupported | not_applicable
  cancel_reason: null
  pause_state: running | paused | resumed
  deduplication_state: admitted | duplicate | retry | canceled | stale_epoch_suppressed
  cue_identity:
    session_id: REQUIRED_SESSION_ID
    attempt_epoch: REQUIRED_ATTEMPT_EPOCH
    source_event_id: REQUIRED_SOURCE_EVENT_ID_OR_NULL
    fact_id: REQUIRED_FACT_ID_OR_NULL
    audio_cue_id: REQUIRED_AUDIO_CUE_ID
  presentation:
    accepted: true | false
    buffered_during_pause: false
    wall_clock_authority: false
    dsp_sample_start_authority: true | false
  measurement:
    percentile: p50 | p95 | p99 | max | null
    steady_state: true | false
    stall_magnitude_ms: null
    stall_rate_per_s: null
    notes: []
```

`requested_virtual_timestamp` and `reported_dsp_sample_start` are legacy aliases
from pre-ADR-0003 templates. They may appear only in imported historical records,
must be mapped to `virtual_cue_request` and `dsp_start_sample`, and must never be
used as an independent authority or as evidence that a capture occurred.

## Field Notes

- `virtual_cue_request` is the gameplay-side request timestamp.
- `virtual_dsp_onset` is derived only when the adapter supplies finite absolute
  metadata: `(dsp_start_sample / sample_rate) + epoch_offset`.
- `dsp_start_sample` is qualifying evidence only when it is an absolute sample index
  reported by the bound instrumented adapter for `voice_instance_id`. A clip-relative
  cursor, wall-clock callback, scheduler acknowledgement, or estimate cannot populate it.
- `dsp_start_sample_estimate` is diagnostic only and never upgrades `onset_outcome`.
- `audio_clock_source` and `integration_source` identify the clock/adapter that supplied
  the evidence. `unsupported` is explicit for stock Unity PC/WebGL paths without the
  required adapter; Wwise/OQ3 remains a candidate, not an approved dependency.
- `onset_trace_history` is append-only. A later confirmed report may be retained as
  trace after an earlier `estimated` or `pending` record, but it cannot retroactively
  turn that earlier outcome into a passing capture.
- `limiter_evidence` is required for the applicable routed bus. `not_applicable` is
  valid only for a declared visual-only or out-of-scope limiter path.
- `source_event_id` and `fact_id` are alternative stable cue identity anchors; at least
  one must be present, and both may be present when the runtime provides them.
- `pause_state` must reflect the state at the moment of the observation, not a guessed
  replay state.
- `percentile` is required for performance capture and remains `null` only when the
  record is not a performance measurement.

## Example Capture Rows

```yaml
- status: UNCAPTURED
  capture_source: target_hardware
  session_id: session_01
  attempt_epoch: 3
  source_event_id: burst_source_128
  fact_id: 512
  audio_cue_id: footstep_body
  virtual_cue_request: 12.4167
  virtual_dsp_onset: null
  voice_instance_id: null
  dsp_start_sample: UNCAPTURED
  dsp_start_sample_estimate: null
  sample_rate: null
  epoch_offset: null
  audio_clock_source: unsupported
  integration_source: UnityStockWebGL
  onset_outcome: unsupported
  onset_trace_history: []
  limiter_evidence: limiter_unsupported
  cancel_reason: null
  pause_state: running
  deduplication_state: admitted
  cue_identity:
    session_id: session_01
    attempt_epoch: 3
    source_event_id: burst_source_128
    fact_id: 512
    audio_cue_id: footstep_body
  presentation:
    accepted: false
    buffered_during_pause: false
    wall_clock_authority: false
    dsp_sample_start_authority: false
  measurement:
    percentile: null
    steady_state: false
    stall_magnitude_ms: null
    stall_rate_per_s: null
    notes:
      - stock WebGL has no qualifying absolute DSP adapter
```

```yaml
- status: PENDING_OQ6
  capture_source: target_hardware
  session_id: session_01
  attempt_epoch: 3
  source_event_id: burst_source_129
  fact_id: 513
  audio_cue_id: footstep_accent
  virtual_cue_request: 12.5
  virtual_dsp_onset: null
  voice_instance_id: REQUIRED_VOICE_INSTANCE_ID
  dsp_start_sample: UNCAPTURED
  dsp_start_sample_estimate: null
  sample_rate: REQUIRED_SAMPLE_RATE_HZ
  epoch_offset: REQUIRED_EPOCH_OFFSET
  audio_clock_source: REQUIRED_ADAPTER_CLOCK
  integration_source: REQUIRED_APPROVED_ADAPTER
  onset_outcome: pending
  onset_trace_history: []
  limiter_evidence: limiter_unsupported
  cancel_reason: null
  pause_state: paused
  deduplication_state: stale_epoch_suppressed
  cue_identity:
    session_id: session_01
    attempt_epoch: 2
    source_event_id: burst_source_129
    fact_id: 513
    audio_cue_id: footstep_accent
  presentation:
    accepted: false
    buffered_during_pause: false
    wall_clock_authority: false
    dsp_sample_start_authority: false
  measurement:
    percentile: p99
    steady_state: false
    stall_magnitude_ms: 33
    stall_rate_per_s: 1
    notes:
      - stale epoch suppressed before presentation
      - target-hardware capture remains pending OQ6
```

## Reviewer Checklist

- [ ] The record distinguishes requested virtual time from adapter-reported absolute
      DSP onset.
- [ ] Missing DSP starts remain `UNCAPTURED`; they are never defaulted to zero.
- [ ] The canonical ADR-0003 fields are present, and legacy aliases are not treated as
      independent evidence.
- [ ] Cue identity is stable across retry, duplicate, cancel, and stale epoch paths.
- [ ] Pause/resume behavior is represented explicitly.
- [ ] Stock Unity PC/WebGL unsupported states remain fail-closed until an approved
      absolute-DSP adapter is captured.
- [ ] Performance capture keeps `PENDING_OQ6` for uncaptured target-hardware data.
