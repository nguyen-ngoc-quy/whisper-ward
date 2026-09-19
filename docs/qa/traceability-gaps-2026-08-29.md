# QA Traceability Gap Report — 2026-08-29

## Scope

This report records the current design-to-test traceability state for the systems
that are being prepared around the Player Noise review. It is an evidence report,
not a design verdict and not a request to modify the Player Noise GDD.

### Sources inspected

- `design/gdd/player-noise.md`
- `design/gdd/player-movement-hide.md`
- `design/gdd/perception.md`
- `design/gdd/guard-ai-fsm.md`
- `design/gdd/systems-index.md`
- `design/registry/entities.yaml`
- `src/AI/Core/EventBus.cs`
- `src/AI/Core/IEvent.cs`
- `src/AI/Core/VirtualTickClock.cs`
- `src/AI/Testing/DecisionTap.cs`
- `src/AI/Testing/FSMVerificationSuite.cs`
- `src/AI/Testing/PerceptionDriver.cs`

### Directories checked

- `tests/` — not found
- `design/qa/` — not found
- `production/qa/` — exists but contains no files
- `production/qa/evidence/` — not found

## Overall Verdict

**MISSING** — the design documents contain detailed acceptance criteria and named
fixtures, but the corresponding test/evidence artifacts are not yet present in the
expected project locations.

This verdict describes QA readiness only. It does not invalidate the current Player
Noise design review and does not change any canonical design status.

## System-by-System Results

### Player Noise (`NoiseEmitter`) — Logic / Integration — MISSING

**Expected evidence**: automated fixtures described in `design/gdd/player-noise.md`.

Named fixtures found in the GDD but not found as test files:

- `NoiseEmitterFixture`
- `EventBusFixture`
- `PerceptionHearingFixture`
- `BurstLifecycleFixture`
- `ConfigValidatorFixture`
- `AudioFeedbackFixture`
- `LevelFixture`
- `HideSpotFixture`
- `FsmFixture`
- `PerceptionFixture`

Acceptance coverage currently cannot be executed from the repository because there
is no `tests/` directory and no Unity Test Framework test assembly was found.

Important unverified areas include:

- `fact_id` exactly-once delivery and retry deduplication;
- latch divergence for Walk/Run radius selection;
- epoch-safe stale-fact removal;
- fixed-step Burst trajectory and collision contact;
- initial-overlap rejection before resource spend;
- hearing Linecast geometry and soft Y falloff;
- MVP single-guard re-anchor behavior;
- audio/UI cue deduplication;
- level route certification;
- AC21 performance and hitch determinism.

**Blocking QA gap**: Player Noise acceptance criteria cannot be mechanically verified
until the test harness and Unity test setup exist.

### Event/Messaging Bus (`#15`) — Core Infrastructure — INCOMPLETE

Implementation surfaces exist:

- `src/AI/Core/EventBus.cs`
- `src/AI/Core/IEvent.cs`

However, no tests were found for:

- unsubscribe correctness;
- duplicate subscription prevention;
- per-publisher ordering;
- `(session_id, attempt_epoch, fact_id)` deduplication;
- epoch queue invalidation;
- active-listener exactly-once delivery;
- callback mutation during dispatch.

`docs/architecture/adr-0001-event-messaging-bus.md` now records the proposed
contract. The ADR is still **Proposed**, so it is not an implementation sign-off.

### Physics & Collision (`#18`) — Core Infrastructure — INCOMPLETE

`docs/architecture/adr-0002-physics-collision-contract.md` now records the proposed
shared contract, but no implementation or automated evidence was found for:

- E20 layer validation and forbidden-bit rejection;
- `QueryTriggerInteraction.Ignore` on every shared query;
- hearing origin and guard-eye endpoint;
- Burst initial-overlap handling;
- single-hit closest `SphereCast` contact;
- fixed `dt_max = 1/120 s` replay;
- one `SyncTransforms()` per simulation batch;
- preview/runtime collision parity;
- backlog clamp and timeout boundaries.

**Blocking implementation gate**: Physics remains a proposed contract until its
validator and deterministic fixtures exist.

### Input / Camera / Recovery — Dependency Trace — MISSING

The rules are specified in `player-noise.md`, including `Throw` edge capture,
camera-azimuth sampling, facing fallback, death/segment reset, full-room restart,
and session-scoped `fact_id` behavior.

No dedicated Input, Camera, Save/Session, or recovery implementation surface was
found under `src/`. No test artifact was found for the ordering and boundary cases.

**Gap**: These requirements have design traceability but no executable verification
path yet.

### Player Movement & Hide (`HideSpot`) — Logic / Integration — MISSING

The GDD specifies `HideSpotFixture`, an editor-sweep harness and shared H.0 virtual
tick coverage. No corresponding test or evidence artifact was found.

Additional known dependency gap:

- the GDD records a joint-contingent FSM rev 4.2 requirement for occupancy gating,
  exit-abort, four-phase ordering, same-tick re-entry deferral and stagger accrual;
- the required FSM amendment is not yet represented by a dedicated test artifact.

The document remains suitable for a separate lean design re-review, but it is not
implementation-ready from a QA evidence perspective.

### Prototype Playtest Plan — Config / Process — MISSING

The project gate records `design/qa/prototype-playtest-plan.md` as a carried,
blocking pre-milestone-0 artifact. That file and its directory were not found.

The plan must eventually cover the concept claims, first-attempt wording versus the
K=3 rule, committed-escalation production, and the trace protocol delegated by the
approved concept review.

## Existing Test-Adjacent Code

The repository contains test-support or verification-adjacent code under `src/AI/Testing/`:

- `DecisionTap.cs` — observes decision records;
- `FSMVerificationSuite.cs` — FSM verification support;
- `PerceptionDriver.cs` — scripted sensing-fact injection.

These files are useful starting seams, but their presence is not equivalent to a
Unity test suite or acceptance evidence. No assertion inventory or passing test run
can be established from the current repository state.

## Priority Actions

Ordered from lowest risk to highest integration risk:

1. Confirm the Unity Test Framework/NUnit package and create a test assembly without
   changing gameplay code.
2. Add Event Bus contract tests for subscription lifecycle, ordering, deduplication
   and epoch invalidation.
3. Add Physics validator and deterministic collision tests.
4. Add Input/Camera/Recovery boundary tests using injected virtual time.
5. Add Player Noise fixtures and map each AC to a named test.
6. Add the HideSpot editor-sweep and joint FSM/HideSpot tests.
7. Create and obtain QA approval for `design/qa/prototype-playtest-plan.md`.
8. Run the full traceability and performance gates only after the Player Noise review
   reaches its approved state.

## Safety Boundary

This report intentionally does not modify:

- `design/gdd/player-noise.md`;
- `design/gdd/perception.md`;
- `design/gdd/guard-ai-fsm.md`;
- `design/gdd/systems-index.md`;
- `design/registry/entities.yaml`;
- `design/levels/`;
- active review logs or session state.

No test or production code was changed while producing this report.
