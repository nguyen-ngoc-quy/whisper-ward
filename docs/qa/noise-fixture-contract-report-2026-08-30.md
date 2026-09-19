# Noise Fixture Contract Report

**Status**: Task 1 fixture/QA contract report
**Date**: 2026-08-30
**Scope**: document-surface contract completion for fixture order, identity ownership, result schema, retry/reject semantics, Burst edge cases, and OQ6 fail-closed handling

## Normalized result record

```yaml
fixture_id: NoiseEmitterFixture
criterion_id: AC-FX-1
status: PASS | FAIL | UNCAPTURED | PENDING_OQ6
expected:
  field: value
actual:
  field: value
failure_code: null
trace_identity:
  session_id: example-value
  attempt_epoch: example-value
  source_timestamp: example-value
  source_event_class_rank: example-value
  source_event_id: example-value
  fact_id: example-value
  entry_id: null
  guard_eid: null
```

## Execution order

```yaml
fixture_order:
  - NoiseEmitterFixture
  - EventBusFixture
  - PerceptionHearingFixture
  - BurstLifecycleFixture
  - ConfigValidatorFixture
  - AudioFeedbackFixture
  - LevelFixture
  - FsmFixture
  - PerceptionFixture
  - HideSpotFixture
actual_capture_state: not_captured
```

## Failure-code vocabulary

```yaml
result_status_values:
  - PASS
  - FAIL
  - UNCAPTURED
  - PENDING_OQ6
level_validator_codes:
  - PASS
  - MISSING_REQUIRED_FIELD
  - MISSING_MARKER
  - INVALID_CARDINALITY
  - INVALID_TOLERANCE
  - INVALID_F4_RANGE
  - INVALID_ANGLE
  - INVALID_ROOT
  - INVALID_ROUTE
  - AMBIGUOUS_COLLISION
  - INITIAL_OVERLAP_REJECTED
  - EXPECTED_NEGATIVE_CASE
  - E20_LAYER_MISMATCH
  - TRIGGER_POLICY_MISMATCH
  - ACTUAL_CAPTURE_UNAVAILABLE
  - CONTRADICTORY_RECORD
  - INVALID_PICKUP_TRANSITION
  - INVALID_GATE_TRANSITION
  - ENDPOINT_MODE_MISMATCH
  - MISSING_LANDING_SURFACE
  - EPOCH_BARRIER_MISMATCH
  - STALE_WORK_NOT_CANCELLED
```

## Contract results

```yaml
- fixture_id: ConfigValidatorFixture
  criterion_id: AC-FX-ORDER-01
  status: PASS
  expected:
    fixture_order:
      - NoiseEmitterFixture
      - EventBusFixture
      - PerceptionHearingFixture
      - BurstLifecycleFixture
      - ConfigValidatorFixture
      - AudioFeedbackFixture
      - LevelFixture
      - FsmFixture
      - PerceptionFixture
      - HideSpotFixture
  actual:
    fixture_order_source:
      - design/fixtures/noise-fixture-spec.md
      - design/qa/prototype-playtest-plan.md
    harness_entry_point: not_captured
  failure_code: null
  trace_identity:
    session_id: null
    attempt_epoch: null
    source_timestamp: null
    source_event_class_rank: null
    source_event_id: null
    fact_id: null
    entry_id: null
    guard_eid: null

- fixture_id: ConfigValidatorFixture
  criterion_id: AC-FX-SCHEMA-01
  status: PASS
  expected:
    required_fields:
      - expected
      - actual
      - failure_code
      - source_event_class_rank
      - source_event_id
      - fact_id
      - entry_id
  actual:
    schema_location: docs/qa/noise-fixture-contract-report-2026-08-30.md
    write_set_locations:
      - design/fixtures/noise-fixture-spec.md
      - design/levels/mvp-burst-route-fixture.md
      - design/qa/prototype-playtest-plan.md
  failure_code: null
  trace_identity:
    session_id: null
    attempt_epoch: null
    source_timestamp: null
    source_event_class_rank: null
    source_event_id: null
    fact_id: null
    entry_id: null
    guard_eid: null

- fixture_id: EventBusFixture
  criterion_id: AC-FX-RETRY-01
  status: UNCAPTURED
  expected:
    assertions:
      - retry before rejection
      - reject-newest after retry exhaustion
      - older queued work preserved
      - rejected item serialized with identity and stable code
      - rejected item produces no relay and no decision
      - no silent drop
  actual:
    capture_state: not_captured
    capture_location: null
    stable_codes: null
  failure_code: null
  trace_identity:
    session_id: null
    attempt_epoch: null
    source_timestamp: null
    source_event_class_rank: null
    source_event_id: null
    fact_id: null
    entry_id: null
    guard_eid: null

- fixture_id: BurstLifecycleFixture
  criterion_id: AC-FX-BURST-01
  status: PASS
  expected:
    ownership:
      source_event_id: source-owned
      fact_id: emitter-owned
      entry_id: perception-owned
      session_id: lifecycle-owned
      attempt_epoch: lifecycle-owned
      epoch_transition_id: lifecycle-owned
  actual:
    ownership_locations:
      - design/fixtures/noise-fixture-spec.md
      - design/registry/entities.yaml
    capture_state: not_captured
  failure_code: null
  trace_identity:
    session_id: null
    attempt_epoch: null
    source_timestamp: null
    source_event_class_rank: null
    source_event_id: null
    fact_id: null
    entry_id: null
    guard_eid: null

- fixture_id: AudioFeedbackFixture
  criterion_id: AC-FX-OQ6-01
  status: PENDING_OQ6
  expected:
    measurements:
      - reported_dsp_sample_start
      - p95
      - p99
    tolerance_ms: 20
    supported_mvp_budget: 1 guard / 1 fact / 1 flight <= 2.0 ms p95 and p99
  actual:
    capture_state: target-hardware evidence not yet recorded
    allowed_placeholder: PENDING_OQ6
    forbidden_substitute: not_captured
  failure_code: null
  trace_identity:
    session_id: null
    attempt_epoch: null
    source_timestamp: null
    source_event_class_rank: null
    source_event_id: null
    fact_id: null
    entry_id: null
    guard_eid: null

- fixture_id: AudioFeedbackFixture
  criterion_id: AC-FX-STRESS-01
  status: PENDING_OQ6
  expected:
    probe: stress_30_guard_8_fact
    classification: diagnostic-only
    gate_rule: cannot certify supported MVP budget
  actual:
    capture_state: no target-hardware run attached
    fail_closed: true
  failure_code: null
  trace_identity:
    session_id: null
    attempt_epoch: null
    source_timestamp: null
    source_event_class_rank: null
    source_event_id: null
    fact_id: null
    entry_id: null
    guard_eid: null
```

## Burst edge-case rows

```yaml
- fixture_id: BurstLifecycleFixture
  criterion_id: AC-FX-BURST-EDGE-01
  status: UNCAPTURED
  expected:
    case: multiple flight_handle_id values in one attempt
    owner: flight_handle_id per flight
    publication: independent landing fact per flight
    failure_code_path: E-FX-9
  actual:
    capture_location: not_captured
    report_location: docs/qa/noise-fixture-contract-report-2026-08-30.md
    capture_state: UNCAPTURED
  failure_code: null

- fixture_id: BurstLifecycleFixture
  criterion_id: AC-FX-BURST-EDGE-02
  status: UNCAPTURED
  expected:
    case: contact at exactly 3.0 s
    owner: BurstLifecycleFixture
    publication: collision_landed_then_consumed
    failure_code_path: contact_before_or_at_timeout
  actual:
    capture_location: not_captured
    report_location: docs/qa/noise-fixture-contract-report-2026-08-30.md
    capture_state: UNCAPTURED
  failure_code: null

- fixture_id: BurstLifecycleFixture
  criterion_id: AC-FX-BURST-EDGE-03
  status: UNCAPTURED
  expected:
    case: contact after 3.0 s
    owner: BurstLifecycleFixture
    publication: timeout_fallback_then_consumed
    failure_code_path: timeout_wins
  actual:
    capture_location: not_captured
    report_location: docs/qa/noise-fixture-contract-report-2026-08-30.md
    capture_state: UNCAPTURED
  failure_code: null

- fixture_id: BurstLifecycleFixture
  criterion_id: AC-FX-BURST-EDGE-04
  status: UNCAPTURED
  expected:
    case: death during flight
    owner: BurstLifecycleFixture
    publication: death_cancelled_inflight
    failure_code_path: STALE_WORK_NOT_CANCELLED
  actual:
    capture_location: not_captured
    report_location: docs/qa/noise-fixture-contract-report-2026-08-30.md
    capture_state: UNCAPTURED
  failure_code: null

- fixture_id: BurstLifecycleFixture
  criterion_id: AC-FX-BURST-EDGE-05
  status: UNCAPTURED
  expected:
    case: initial overlap before spend
    owner: BurstLifecycleFixture
    publication: no_runtime_launch
    failure_code_path: INITIAL_OVERLAP_REJECTED
  actual:
    capture_location: not_captured
    report_location: docs/qa/noise-fixture-contract-report-2026-08-30.md
    capture_state: UNCAPTURED
  failure_code: null

- fixture_id: BurstLifecycleFixture
  criterion_id: AC-FX-BURST-EDGE-06
  status: UNCAPTURED
  expected:
    case: pause/resume during flight
    owner: BurstLifecycleFixture
    publication: virtual_clock_freeze_then_resume
    failure_code_path: E-FX-5
  actual:
    capture_location: not_captured
    report_location: docs/qa/noise-fixture-contract-report-2026-08-30.md
    capture_state: UNCAPTURED
  failure_code: null

- fixture_id: BurstLifecycleFixture
  criterion_id: AC-FX-BURST-EDGE-07
  status: UNCAPTURED
  expected:
    case: retry/duplicate publish of one terminal fact
    owner: EventBusFixture
    publication: reject_newest_after_retry_exhaustion
    failure_code_path: E-FX-1
  actual:
    capture_location: not_captured
    report_location: docs/qa/noise-fixture-contract-report-2026-08-30.md
    capture_state: UNCAPTURED
  failure_code: null
```

## Review outcome

- Fixture order is now explicit and shared between the fixture spec and playtest plan.
- The expected/actual result schema is normalized and serialized in one place.
- Identity ownership, retry/reject behavior, Burst edge cases, and fail-closed OQ6 handling are documented without editing `design/gdd/player-noise.md`.
- Target-hardware audio and performance evidence remain pending and visibly non-passing.
