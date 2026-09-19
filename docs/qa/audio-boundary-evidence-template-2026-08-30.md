# Audio Boundary Evidence Template

> This template is a capture contract, not runtime evidence.

## Required Capture Rules

- Never infer gameplay time from wall-clock audio callbacks.
- Never default a missing reported DSP sample start to `0`.
- Use `UNCAPTURED` when a required observation was not recorded.
- Use `PENDING_OQ6` for measurements that still require target-hardware capture.
- Keep stale-epoch suppression and idempotent retry/cancel behavior visible in the record.

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
  requested_virtual_timestamp: REQUIRED_VIRTUAL_TIMESTAMP
  reported_dsp_sample_start: UNCAPTURED
  sample_rate: REQUIRED_SAMPLE_RATE_HZ
  epoch_offset: REQUIRED_EPOCH_OFFSET
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
    dsp_sample_start_authority: false
  measurement:
    percentile: p50 | p95 | p99 | max | null
    steady_state: true | false
    stall_magnitude_ms: null
    stall_rate_per_s: null
    notes: []
```

## Field Notes

- `virtual_cue_request` and `requested_virtual_timestamp` are the gameplay-side
  timestamps.
- `reported_dsp_sample_start` is only filled when the audio integration actually
  reports a start value.
- `sample_rate` and `epoch_offset` are explicit provenance fields and must be
  present when evidence is captured.
- `source_event_id` and `fact_id` are alternative stable cue identity anchors; at
  least one must be present, and both may be present when the runtime provides
  them.
- `pause_state` must reflect the state at the moment of the observation, not a
  guessed replay state.
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
  requested_virtual_timestamp: 12.4167
  reported_dsp_sample_start: UNCAPTURED
  sample_rate: 48000
  epoch_offset: 0.125
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
    accepted: true
    buffered_during_pause: false
    wall_clock_authority: false
    dsp_sample_start_authority: false
  measurement:
    percentile: p95
    steady_state: true
    stall_magnitude_ms: null
    stall_rate_per_s: null
    notes: []
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
  requested_virtual_timestamp: 12.5
  reported_dsp_sample_start: UNCAPTURED
  sample_rate: 48000
  epoch_offset: 0.125
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
```

## Reviewer Checklist

- [ ] The record distinguishes requested virtual time from reported DSP sample
      start.
- [ ] Missing DSP starts remain `UNCAPTURED`; they are never defaulted to zero.
- [ ] Cue identity is stable across retry, duplicate, cancel, and stale epoch
      paths.
- [ ] Pause/resume behavior is represented explicitly.
- [ ] Performance capture keeps `PENDING_OQ6` for uncaptured target-hardware data.

