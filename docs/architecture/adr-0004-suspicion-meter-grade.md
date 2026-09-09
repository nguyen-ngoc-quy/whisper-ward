# ADR-0004: Suspicion Meter and Room Grade

## Status
Accepted

## Date
2026-09-01

## Engine Compatibility

| Field | Value |
|---|---|
| **Engine** | Unity 6 LTS (6000.3.17f1) |
| **Domain** | AI presentation and room outcome measurement |
| **References Consulted** | `docs/superpowers/specs/2026-09-01-suspicion-grade-design.md`, `design/registry/entities.yaml`, `docs/architecture/adr-0001-event-messaging-bus.md` |
| **Verification Required** | Unity Test Framework execution after the Unity project scaffold is available |

## Context

Whisper Ward needs a readable per-guard suspicion meter and a deterministic room
outcome without creating a second owner for Perception, the Guard FSM, or session
lifecycle state. The meter must explain authoritative accumulator and residual
values, while the final grade must attribute penalties to concrete, immutable trace
incidents and remain stable under transport retries and reordered delivery.

A frame-polled singleton would introduce an independent update authority, make
lifecycle ownership ambiguous, and risk reading partially updated state. Recomputing
Perception or FSM state in the grading feature would create a second implementation
of gameplay rules and could disagree with the authoritative trace.

## Decision

Implement Suspicion Meter / Grade as pure projections over injected authoritative
inputs. Task 1 of the implementation plan delivers only the immutable
configuration contract, the registry mapping, and the focused configuration
tests; the boundaries below are the accepted target boundaries implemented by
Tasks 2-6, not already implemented:

- A meter projection (Task 2) consumes the existing accumulator, Chase threshold,
  residual, guard-state, and causal-event snapshot. It calculates display ratio
  and semantic region but never mutates the source values.
- A trace reducer (Task 3) consumes immutable FSM decision records and maintains
  a per-room, per-guard incident aggregate. It counts only `Chase-entry`,
  fruitless `investigate-resolution`, and `Capture` records.
- A room-grade operator (Task 3-4) is an injected boundary finalizer. It accepts
  only a valid completed-room boundary and an aggregate, then emits one immutable
  result with score, grade, completion status, breakdown, and contributing event
  IDs.
- The composition adapter (Tasks 4-6) sends no gameplay commands and cannot
  change Perception thresholds, FSM transitions, hearing/vision outcomes, or
  session state.

The production configuration is immutable and receives all tuning from the entity
registry adapter. Existing `T_base`, `k_res`, `T_floor`, `T_chase`, and `R_max`
entries remain authoritative dependencies and are referenced rather than copied
into the Suspicion/Grade registry namespace. Grade weights, score thresholds,
meter scale, and output schema version are registered under `suspicion_grade`.

## Identity, Deduplication, and Lifecycle

All projections are scoped by `(session_id, attempt_epoch)` and guard/room identity
where applicable. Incident deduplication uses:

```text
(session_id, attempt_epoch, entry_id, event_type)
```

Records from stale epochs are ignored. Duplicate delivery and retry do not create a
second incident. A room finalization is idempotent for one
`(session_id, attempt_epoch, room_id)` boundary. Restart and other lifecycle
transitions close the prior aggregate and begin the next epoch according to the
session owner contract.

Missing accumulator or threshold input produces an unavailable meter. Missing
mandatory trace or final residual evidence produces an unresolved grade; the
implementation never substitutes a fabricated zero. Capture or failed/incomplete
room completion produces `Failed` and never an S/A/B grade.

## Rejected Alternatives

### Frame-polled singleton

Rejected because it creates a competing time/update authority, complicates teardown
and epoch isolation, and can observe transient state between authoritative events.
Event-driven snapshots and an injected completion boundary keep ownership explicit
and testable.

### Recalculating Perception or FSM state

Rejected because the feature would duplicate threshold, accumulator, hearing, vision,
or transition logic. The meter and grade consume authoritative snapshots and trace
records instead; gameplay systems remain the sole owners of those rules.

## Consequences

Positive consequences:

- Meter behavior is deterministic, read-only, and explainable from authoritative
  data.
- Trace retries and delivery order cannot inflate the room penalty.
- Missing evidence fails visibly as unavailable or unresolved instead of looking like
  a clean run.
- Configuration and grade tuning can be changed through registry data without
  embedding fallback values in production code.

Trade-offs:

- The feature depends on complete session, Perception, FSM, and trace contracts.
- Finalization cannot produce a grade until the session owner supplies a valid room
  boundary and final residual snapshot.
- Unity runtime and target-hardware evidence remains a separate verification step.

## Verification Notes

Task 1's focused configuration tests cover the injected starter values, threshold
ordering, positive Chase/residual domains, and every constructor validation
branch. This repository currently has no Unity project scaffold
(`Packages/manifest.json` and `ProjectSettings/ProjectVersion.txt`), so the
Unity Test Framework command is pending the selected A2 prerequisite. The
registry-to-runtime composition wiring remains Task 5 work.
