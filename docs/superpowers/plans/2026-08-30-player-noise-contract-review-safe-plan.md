# Player Noise Contract Review-Safe Coordination Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hoàn thiện các contract Burst, fixture/QA, static verification, audio boundary và FSM test alignment mà không chỉnh sửa trực tiếp `design/gdd/player-noise.md` hoặc review log của nó.

**Architecture:** Plan này dùng một coordination sequence gồm các workstream có thể review độc lập, nhưng serialize mọi thay đổi chạm file hội tụ. Canonical Player Noise GDD chỉ là input read-only; các quyết định mới được ghi vào fixture/registry, ADR audio riêng, source Burst và test support đúng phạm vi. Mỗi workstream phải để lại traceable expected/actual evidence hoặc stable failure code, và mọi evidence chưa đo được vẫn fail-closed.

**Tech Stack:** Unity 6 LTS `6000.3.17f1`, C#, Unity Test Framework/NUnit, YAML registry, Markdown design/QA artifacts, Git worktree hiện có.

**Spec:** `design/fixtures/noise-fixture-spec.md`, `design/levels/mvp-burst-route-fixture.md`, `design/qa/prototype-playtest-plan.md`, `design/gdd/player-noise.md` (read-only), `design/gdd/sound_performance_audit.md`, `design/registry/entities.yaml`, `design/gdd/guard-ai-fsm.md`.

## Global Constraints

- Không sửa `design/gdd/player-noise.md` trong bất kỳ task nào.
- Không sửa `design/gdd/reviews/player-noise-review-log.md` trong bất kỳ task nào.
- Giữ `PENDING_OQ6` cho mọi performance/audio evidence chưa có capture trên target hardware; `UNCAPTURED`, `UNKNOWN` và missing percentile không được coi là PASS.
- Gameplay comparison tolerance là `CompareTolerance ≤ 5×10⁻³ m`; pure-math oracle dùng relative tolerance `1×10⁻⁶`, không trộn hai loại tolerance.
- `flight_handle_id` là `string` producer-owned, dùng làm Burst `source_event_id`; `fact_id` là `ulong` do emitter cấp một lần tại publish ingress. Retry hoặc duplicate không cấp identity mới.
- Burst contact tại `contact_event_time ≤ 3.0 s` thắng timeout; contact sau mốc này thua timeout; death cancellation thắng callback cũ và không hoàn tiền resource đã commit.
- `flight_handle_id` độc nhất trong `(session_id, attempt_epoch)`; `fact_id` độc nhất trong session envelope theo contract hiện hành; `entry_id` vẫn là identity do Perception sở hữu.
- Không chỉnh song song `entities.yaml`, `VirtualTickClock.cs`, `SessionPhaseCoordinator.cs`, `PerceptionHearingService.cs`, `GuardAISystem.cs` hoặc `player-noise.md`; mỗi file chỉ có một owner trong một task tại một thời điểm.
- Worktree đang có thay đổi của người dùng. Không dùng `git reset`, `git checkout`, thao tác xoá, hoặc commit nếu chưa có chỉ thị riêng.

---

### Task 0: Baseline, scope lock và dependency snapshot

**Files:**
- Read: `docs/context/context-manifest.yaml`
- Read: `docs/context/project-brief.md`
- Read: `docs/context/review-index.md`
- Read: `design/gdd/systems-index.md`
- Read: all files named in **Spec** and the current Burst/FSM source files
- Modify: none

**Interfaces:**
- Consumes: current worktree and canonical design sources.
- Produces: a written baseline in the task handoff containing current dirty paths, source-of-truth paths, locked no-touch files, and the exact task owner for each shared file.

- [ ] **Step 1: Record the existing worktree boundary**

Run:

```powershell
git status --short
git diff --name-only
git ls-files --others --exclude-standard
```

Expected: the result is captured in the handoff; no existing path is reverted or re-staged.

- [ ] **Step 2: Load the planning context in policy order**

Read `docs/context/context-manifest.yaml`, then `docs/context/project-brief.md`, `docs/context/review-index.md`, the Player Noise systems-index row, and only the directly referenced Burst/FSM/audio/fixture sections.

Expected: the handoff identifies `design/gdd/player-noise.md` as read-only input and does not treat a review summary as a replacement for the canonical GDD.

- [ ] **Step 3: Publish the ownership matrix before any edit**

Use this exact ownership matrix:

| File | Owner task | Parallel edit allowed? |
|---|---|---|
| `design/fixtures/noise-fixture-spec.md` | Task 1, then Task 3 Burst subsection | No |
| `design/levels/mvp-burst-route-fixture.md` | Task 1, then Task 3 Burst subsection | No |
| `design/registry/entities.yaml` | Task 3 only | No |
| `src/AI/Perception/BurstSimulationService.cs` | Task 3 only | No |
| `src/AI/Core/NoiseSourceRecord.cs` | Task 3 only | No |
| `src/AI/Testing/FSMVerificationSuite.cs` | Task 5 only | No |
| `src/AI/Core/VirtualTickClock.cs` | No task in this plan | No |
| `src/AI/Core/SessionPhaseCoordinator.cs` | No task in this plan | No |
| `src/AI/Perception/PerceptionHearingService.cs` | No task in this plan | No |
| `src/AI/GuardAISystem.cs` | No task in this plan | No |
| `design/gdd/player-noise.md` | Review owner only | Never |

Expected: all implementers use the matrix and stop if an unrelated change overlaps their assigned file.

- [ ] **Step 4: Stop for a review checkpoint**

Do not start Task 1 until the baseline and ownership matrix are visible to the reviewer. Do not commit this checkpoint without explicit instruction.

---

### Task 1: Fixture and QA contract completion

**Files:**
- Modify: `design/fixtures/noise-fixture-spec.md`
- Modify: `design/levels/mvp-burst-route-fixture.md`
- Modify: `design/qa/prototype-playtest-plan.md`
- Create: `docs/qa/noise-fixture-contract-report-2026-08-30.md`
- Read-only reference: `design/gdd/player-noise.md`, `design/registry/entities.yaml`, `design/gdd/perception.md`, `design/gdd/guard-ai-fsm.md`, `design/gdd/sound_performance_audit.md`

**Interfaces:**
- Consumes: the existing 10-fixture list, `WW-TRACE-1.0`, `WW-NOISE-1.1`, `WW-NOISE-RELAY-1.0`, `WW-THROW-1.0`, `WW-MVP-BURST-ROUTE-1.1`, and `WW-LIVENESS-1.0`.
- Produces: one common result record, one explicit execution order, one expected/actual schema, one failure-code vocabulary reference, and a QA report that later Burst/audio/FSM tasks can consume without editing `player-noise.md`.

- [ ] **Step 1: Freeze the mandatory fixture order**

Keep the 10 fixtures in this order in `noise-fixture-spec.md` and `prototype-playtest-plan.md`:

```text
NoiseEmitterFixture
EventBusFixture
PerceptionHearingFixture
BurstLifecycleFixture
ConfigValidatorFixture
AudioFeedbackFixture
LevelFixture
FsmFixture
PerceptionFixture
HideSpotFixture
```

Expected: one harness entry point and no criterion refers to an unnamed fixture.

- [ ] **Step 2: Normalize the serialized result record**

Use this field set for every fixture result in `docs/qa/noise-fixture-contract-report-2026-08-30.md`:

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

Expected: `expected` and `actual` are always present for an assertion; missing capture is represented by status, never by a fabricated value.

- [ ] **Step 3: Align trace fields and identity ownership**

Verify every emitted, enqueued, consumed, rejected, terminal, and liveness record includes the applicable fields from the master spec. Keep these ownership rules exact: source owns `source_event_id`; emitter owns `fact_id`; Perception owns `entry_id`; FSM records consumption outcome; lifecycle/session owner owns `session_id`, `attempt_epoch`, and `epoch_transition_id`.

Expected: no fixture allocates, mutates, or re-derives an identity owned by another stage.

- [ ] **Step 4: Encode retry/reject and fail-closed behavior**

Add explicit assertions for:

```text
retry before rejection
reject-newest after retry exhaustion
older queued work preserved
rejected item serialized with identity and stable code
rejected item produces no relay and no decision
no silent drop
```

Expected: `UNCAPTURED` means no typed observation exists; it is not a failure code and never passes a gate.

- [ ] **Step 5: Separate OQ6 from ordinary fixture status**

Mark only the performance/audio measurements that require real capture as `PENDING_OQ6`. Keep `stress_30_guard_8_fact` diagnostic-only and fail-closed if the supported-MVP profile is not measured. Keep audio onset tolerance at `20 ms` and do not infer DSP sample starts from a Wwise scheduled-start callback that is not actually reported.

Expected: `PENDING_OQ6` remains visible in the report and cannot be converted to PASS by a static/document review.

- [ ] **Step 6: Add Burst edge-case rows without changing the Player Noise GDD**

Add rows for:

```text
multiple flight_handle_id values in one attempt
contact at exactly 3.0 s
contact after 3.0 s
death during flight
initial overlap before spend
pause/resume during flight
retry/duplicate publish of one terminal fact
```

Expected: each row has a fixture owner, expected fields, actual capture location, and stable failure code path.

- [ ] **Step 7: Verify the doc-only contract**

Run:

```powershell
rg -n "NoiseEmitterFixture|EventBusFixture|PerceptionHearingFixture|BurstLifecycleFixture|ConfigValidatorFixture|AudioFeedbackFixture|LevelFixture|FsmFixture|PerceptionFixture|HideSpotFixture" design/fixtures/noise-fixture-spec.md design/qa/prototype-playtest-plan.md
rg -n "UNCAPTURED|PENDING_OQ6|expected:|actual:|failure_code|source_event_class_rank|flight_handle_id|fact_id|entry_id" design/fixtures design/levels design/qa docs/qa
```

Expected: all 10 fixture names and all required result/trace terms are present; `player-noise.md` and its review log remain untouched.

- [ ] **Step 8: Stop for fixture/QA review**

Review the report and fixture diff independently. Do not begin Task 3 until the fixture contract is accepted or its requested corrections are recorded.

---

### Task 2: Static consistency and YAML verification

**Files:**
- Read-only: `design/gdd/*.md`, `design/registry/entities.yaml`, `src/**/*.cs`
- Create: `docs/qa/static-contract-audit-2026-08-30.md`
- Modify: none of the canonical GDDs, registry, or runtime source

**Interfaces:**
- Consumes: Task 0 baseline and Task 1 fixture vocabulary.
- Produces: a read-only audit report with exact file/line findings, severity, owner, and whether a finding is safe to fix outside the protected Player Noise GDD.

- [ ] **Step 1: Scan stale terminology**

Run:

```powershell
rg -n "earliest_strictly_positive|earliest strictly positive|earliest-positive|s_diff\\s*=\\s*1\\.0|stress_30_guard_8_fact|UNCAPTURED|PENDING_OQ6" design src docs
```

Expected: every stale term is classified as canonical, historical, test-only, or documentation drift. `minimum_strictly_positive` is the active Burst root policy.

- [ ] **Step 2: Detect duplicate YAML keys without rewriting the registry**

Run a YAML parser that rejects duplicate mapping keys and records the path/line of each duplicate. If the repository does not provide a parser command, use the project’s available Python YAML library in a temporary, non-repository command; do not create a generated registry or overwrite `entities.yaml`.

Expected: the report says `duplicate-key: none` or lists exact key paths. A duplicate key is a blocker until the registry owner resolves it.

- [ ] **Step 3: Build the ownership/schema matrix**

Check these exact ownerships against the canonical registry and trace schema:

```text
NoiseEmitter: source dedup, fact_id allocation, raw NoisePublished
Perception: hearing admission, relay creation, residual_at_hearing, entry_id reservation
FSM: relay consumption outcome, decisions, LivenessFact publication
Lifecycle/session owner: session_id, attempt_epoch, epoch_transition_id, stale-work barrier
Level: authored geometry, gate transition, certification_status/evidence state
Audio bridge: virtual cue request, reported DSP sample start, cue identity/accessibility path
```

Expected: no owner allocates or mutates an identity belonging to another stage.

- [ ] **Step 4: Verify phase and ordering semantics**

Check that the static vocabulary preserves:

```text
stable_guard_snapshot at boundary start
facts ordered by (source_timestamp, fact_id)
pairs ordered by (source_timestamp, fact_id, guard_eid)
phase order: GameplayIngress -> Hearing -> FsmDecision -> Presentation
pause freezes virtual time and phase
attempt_epoch transition invalidates prior-epoch work atomically
```

Expected: no listener arrival order or subscription churn is treated as identity/order input.

- [ ] **Step 5: Verify S_DIFF and 30×8 semantics**

Record these exact checks:

```text
S_DIFF is a registered speed-ratio s_diff(r_actual, r_investigate), not a fixed 1.0 test scalar.
re-anchor budget = max(t_giveup_base * S_DIFF * (1 + k_thorough * (R_reanchor / R_max)), t_giveup_base + t_noise_reanchor_extend)
stress_30_guard_8_fact is diagnostic-only and cannot certify the supported MVP budget.
supported MVP budget is 1 guard / 1 fact / 1 flight at <= 2.0 ms p95 and p99, pending target capture.
```

Expected: any test or document that asserts `s_diff = 1.0` as the only behavior is flagged for Task 5.

- [ ] **Step 6: Write and review the audit report**

For each finding, record `finding_id`, `file`, `line`, `term/contract`, `expected`, `actual`, `owner`, `safe_fix_scope`, and `status`. Do not change canonical files as part of this task.

Expected: the report is actionable and does not falsely claim that a static pass supplies OQ6 evidence.

---

### Task 3: Burst identity and lifecycle boundary

**Files:**
- Modify: `src/AI/Perception/BurstSimulationService.cs`
- Modify: `src/AI/Core/NoiseSourceRecord.cs`
- Modify: `design/registry/entities.yaml`
- Modify: Burst sections only in `design/fixtures/noise-fixture-spec.md`
- Modify: Burst sections only in `design/levels/mvp-burst-route-fixture.md`
- Create: `src/AI/Testing/BurstSimulationServiceVerificationSuite.cs`
- Read-only reference: `design/gdd/player-noise.md`, `design/gdd/reviews/player-noise-review-log.md`

**Interfaces:**
- Consumes: Task 1 fixture result schema and Task 2 static findings.
- Produces: a stable Burst identity contract and executable tests for terminal winner, fact propagation, retry/dedup, rejection-before-spend, death cancellation, pause/resume, and no-refund behavior.

- [ ] **Step 1: Preserve the public source identity shape**

Keep `BurstThrowSnapshot(string flightHandleId, ...)`, `BurstTerminalRecord.FlightHandleId`, `BurstSimulationService.CurrentFlightHandleId`, and `NoiseSourceRecord.Burst(string sourceEventId, ...)` string-compatible. Reject empty/whitespace IDs at construction or launch admission.

Expected: existing string fixtures remain loadable and the type decision is explicit in the registry/fixture contract.

- [ ] **Step 2: Enforce source identity uniqueness and source dedup**

Use the source dedup key:

```text
(session_id, attempt_epoch, flight_handle_id)
```

A duplicate Burst source event is ignored without allocating another `fact_id`. Distinct flight handles remain independent even when they share a source timestamp.

Expected: two flights never collapse into one source identity and one flight never receives two source identities.

- [ ] **Step 3: Enforce terminal fact identity**

Use `fact_id` as emitter-owned `ulong`, allocated once at publish ingress for a landing noise fact. Retry and Event Bus duplicate admission preserve the same `(session_id, attempt_epoch, fact_id)` envelope. A death-cancelled flight that never lands keeps terminal `FactId = 0` and publishes no noise fact.

Expected: terminal inspection can resolve the eventual fact ID without inventing a new identity, and no-refund cancellation cannot accidentally publish a late fact.

- [ ] **Step 4: Lock terminal winner ordering**

Implement/test this predicate:

```text
contact_event_time <= timeout_boundary_s (3.0) => Contact wins
contact_event_time > timeout_boundary_s => Timeout wins
death cancellation before collision/timeout callback => DeathCancelled wins
initial overlap => INITIAL_OVERLAP_REJECTED before spend; no flight identity allocated
```

Expected: exactly one terminal record exists per committed flight, with contact fields null for timeout/death cancellation as specified.

- [ ] **Step 5: Verify no-refund and resource lifecycle**

Assert these transitions:

```text
Placed -> Carried only after valid interaction
Carried -> InFlight on accepted throw; spend is committed
InFlight -> Contact/Timeout/DeathCancelled terminal
DeathCancelled does not restore the spent pickup or carried slot
initial-overlap rejection leaves the pickup Carried
pickup-reached telemetry never mutates resource state or publishes noise
```

Expected: rejection-before-spend and cancellation-after-spend are distinguishable by stable code and state snapshot.

- [ ] **Step 6: Add executable Burst tests**

Add NUnit tests with the existing service constructor and injected clock/event bus. The suite must include these test names or equivalent unambiguous names:

```csharp
[Test] public void FlightHandleId_IsStringSourceIdentity_AndIsNotReused()
[Test] public void ContactAtTimeoutBoundary_WinsAndPublishesOneFact()
[Test] public void ContactAfterTimeoutBoundary_ResolvesTimeoutWithoutLateFact()
[Test] public void DeathCancellation_WinsBeforeLateCallback_AndDoesNotRefund()
[Test] public void InitialOverlap_IsRejectedBeforeSpend()
[Test] public void RetryAndDuplicatePublish_PreserveFactId()
[Test] public void PauseResume_FreezesFlightTime_AndDoesNotBufferAudio()
```

Expected: each test asserts state, terminal fields, identity fields, and failure code rather than only a boolean result.

- [ ] **Step 7: Update only Burst registry/fixture entries**

Add or reconcile the registry/fixture fields for `flight_handle_id`, `fact_id`, `source_event_class_rank`, `terminal_event`, `contact_event_time`, `timeout_boundary_s`, `comparison_result`, `cancellation_reason`, `rejection_code`, and `no_refund`. Do not change unrelated registry entries and do not edit `player-noise.md`.

Expected: registry schema and Burst fixture records agree exactly; duplicate YAML keys remain absent.

- [ ] **Step 8: Run the focused Burst verification**

Run the Unity Test Framework filter for `BurstSimulationServiceVerificationSuite`, then run the static commands from Task 2. Expected: all focused Burst tests pass; no late `NoisePublished`; no identity duplication; no player-noise files changed.

- [ ] **Step 9: Stop for Burst contract review**

Review the source diff, registry diff, and Burst fixture diff together. Do not proceed to audio/FSM integration if the identity owner or terminal winner remains ambiguous.

---

### Task 4: Audio boundary design without fabricated evidence

**Files:**
- Create: `docs/architecture/adr-0003-audio-virtual-timestamp-boundary.md`
- Create: `docs/qa/audio-boundary-evidence-template-2026-08-30.md`
- Modify: `design/gdd/sound_performance_audit.md`
- Read-only: `design/gdd/player-noise.md`, `design/registry/entities.yaml`, `docs/qa/noise-fixture-contract-report-2026-08-30.md`

**Interfaces:**
- Consumes: Task 1 trace/result contract and Task 3 Burst terminal/source identity.
- Produces: a separate audio boundary design that defines the bridge contract while retaining `PENDING_OQ6` for measurements not captured on target hardware.

- [ ] **Step 1: Define the virtual timestamp boundary**

Record the distinction:

```text
virtual_cue_request = gameplay commit/evaluation timestamp
dsp_sample_start = sample index/time reported by the audio integration
sample_rate = explicit audio device/session parameter
epoch_offset = session/attempt mapping used to compare virtual and DSP domains
```

Expected: no wall-clock callback is treated as a gameplay timestamp and no unreported DSP sample start is synthesized.

- [ ] **Step 2: Define cue identity and deduplication**

Specify a stable cue identity containing at least `(session_id, attempt_epoch, source_event_id or fact_id, audio_cue_id)`. Cancel, duplicate, and retry paths must be idempotent; a rejected or stale old-epoch fact cannot present audio.

Expected: one accepted gameplay event produces at most one internal proxy cue per required accessibility mode.

- [ ] **Step 3: Define cancel/pause/resume semantics**

Document:

```text
pause freezes virtual gameplay time
Burst simulation does not advance while paused
audio cues are not buffered for paused virtual time
resume uses the registered capped catch-up path
old-epoch cancel suppresses collision, relay, decision, and presentation
```

Expected: the audio boundary agrees with the fixture and Burst lifecycle contract without changing `player-noise.md`.

- [ ] **Step 4: Preserve OQ6 status and measurement boundary**

Update `sound_performance_audit.md` only to clarify measurement fields, capture provenance, `Physics.autoSyncTransforms`, Event Bus/audio main-thread cost, steady-state p95/p99, and separate 33 ms stall magnitude/rate. Leave unsupported values as `PENDING_OQ6`.

Expected: the document never claims a performance pass from design inspection alone.

- [ ] **Step 5: Review the ADR and evidence template**

The evidence template must contain `requested_virtual_timestamp`, `reported_dsp_sample_start`, `sample_rate`, `epoch_offset`, `audio_cue_id`, `cancel_reason`, `pause_state`, `capture_source`, `percentile`, and `status`.

Expected: missing capture produces `UNCAPTURED` or `PENDING_OQ6` according to the field’s gate, never a default zero or guessed sample start.

---

### Task 5: FSM test alignment and injected harness

**Files:**
- Modify: `src/AI/Testing/FSMVerificationSuite.cs`
- Create: `src/AI/Testing/FsmVerificationHarness.cs`
- Read-only: `src/AI/Core/EventBus.cs`, `src/AI/Core/VirtualTickClock.cs`, `src/AI/Core/SessionPhaseCoordinator.cs`, `src/AI/FSM/GuardFSM.cs`, `design/gdd/guard-ai-fsm.md`, `design/registry/entities.yaml`

**Interfaces:**
- Consumes: Task 2 S_DIFF findings and current injection APIs `IEventBus`, `IVirtualTickClock`, `SessionPhaseCoordinator`, `GuardFSM.ConfigureWithPhaseCoordinator(...)`.
- Produces: deterministic FSM tests driven by an injected bus, virtual clock, and phase coordinator, with liveness/re-anchor coverage and no hard-coded `s_diff = 1.0` as the only path.

- [ ] **Step 1: Create the shared injected test context**

Implement a test-only context exposing:

```csharp
public sealed class FsmVerificationHarness
{
    public SessionEventBus Bus { get; }
    public VirtualTickClockService Clock { get; }
    public SessionPhaseCoordinator Coordinator { get; }
    public GuardFSM CreateFsm(string sessionId, long attemptEpoch);
    public void Advance(float gameplaySeconds);
}
```

Construct `SessionEventBus`, `VirtualTickClockService`, and `SessionPhaseCoordinator` explicitly; call `GuardFSM.ConfigureWithPhaseCoordinator` rather than relying on `EventBus.Default`, scene lookup, or Unity wall-clock update.

Expected: tests share one deterministic phase graph and can inspect bus records and virtual time.

- [ ] **Step 2: Replace the fixed S_DIFF formula fixture**

Change `FSM_Giveup_Clock_Formula` so it derives `S_DIFF` from injected/configured actual and investigate speeds. Test at least:

```text
r_actual = r_investigate => S_DIFF = 1.0
r_actual > r_investigate => S_DIFF > 1.0
r_actual < r_investigate => S_DIFF < 1.0
```

Use the registered re-anchor formula and assert both the exact derived value and the configured floor/extension relation. Do not make a fixed `1.0` the only tested input.

Expected: the test fails if production behavior silently ignores the speed ratio.

- [ ] **Step 3: Add liveness publication tests**

Assert that an Investigate/Chase open or close publishes a `LivenessFact` carrying `session_id`, `attempt_epoch`, `entry_id`, `op`, `tier`, `cause`, `source_timestamp`, and `publisher`. Assert that stale closure is the only close path created by an epoch barrier outside normal terminal resolution.

Expected: liveness is durable on the event bus and is observable without a synchronous Perception query.

- [ ] **Step 4: Add re-anchor and saturation tests**

Drive a harness with two noise facts at distinct timestamps and assert:

```text
same live entry_id is carried while the episode is open
no new entry_id is allocated during a live episode
re-anchor extends the budget using S_DIFF and R_reanchor
corroboration saturates at the registered cap
an epoch transition clears old work and records stale closure
```

Expected: tests assert event order and field values, not only final FSM state.

- [ ] **Step 5: Add phase/liveness ordering tests**

Advance only the injected clock and assert the coordinator drains `GameplayIngress`, `Hearing`, `FsmDecision`, then `Presentation` in order. Assert that pause freezes clock and does not cause a hidden backlog or out-of-phase decision.

Expected: test failures identify phase, tick, event identity, and expected/actual ordering.

- [ ] **Step 6: Run focused and full test filters**

Run the Unity Test Framework filters for `FSMVerificationSuite` and `BurstSimulationServiceVerificationSuite`, then run the full available edit-mode test suite.

Expected: all focused tests pass; failures contain stable test names and do not require changes to the protected Player Noise GDD.

---

### Task 6: Final cross-workstream verification and handoff

**Files:**
- Read: all files changed by Tasks 1–5
- Create: `docs/qa/contract-review-handoff-2026-08-30.md`
- Modify: none of the protected canonical files

**Interfaces:**
- Consumes: fixture report, static audit, Burst tests, audio ADR/evidence template, and FSM tests.
- Produces: one final handoff showing coverage, unresolved review items, exact changed files, and explicit no-touch verification.

- [ ] **Step 1: Verify the protected files are unchanged**

Run:

```powershell
git diff --name-only -- design/gdd/player-noise.md design/gdd/reviews/player-noise-review-log.md
```

Expected: no output attributable to this plan. Existing user changes must remain distinguishable and must not be discarded.

- [ ] **Step 2: Re-run identity and stale-term scans**

Run:

```powershell
rg -n "flight_handle_id|fact_id|entry_id|source_event_class_rank|UNCAPTURED|PENDING_OQ6|S_DIFF|s_diff|30.?8|earliest_strictly_positive|minimum_strictly_positive" design docs src
```

Expected: active terms match the locked contract; historical mentions are labeled; no active test depends exclusively on `s_diff = 1.0`.

- [ ] **Step 3: Verify YAML and schema integrity**

Run the duplicate-key parser from Task 2 and compare every changed registry/fixture schema version against the master fixture spec.

Expected: no duplicate keys, no mismatched schema versions, and no evidence field silently omitted.

- [ ] **Step 4: Verify fail-closed status semantics**

Review every `status` in the reports and assert:

```text
UNCAPTURED != PASS
PENDING_OQ6 != PASS
stress_30_guard_8_fact cannot certify supported MVP
missing expected/actual pair is a failure of the fixture contract
rejected queue item has no relay/decision
death-cancelled Burst has no late fact/relay/decision/audio
```

Expected: final handoff distinguishes implemented verification from evidence still pending capture.

- [ ] **Step 5: Produce the final review handoff**

Record for each task: changed files, tests run, result, unresolved findings, owner, and whether the result is safe to merge after explicit user review. Include the no-touch confirmation for `player-noise.md` and its review log.

Expected: the handoff is complete without creating a commit or claiming approval of the Player Noise GDD.

---

## Execution Order and Review Gates

1. Task 0 baseline and ownership lock.
2. Task 1 fixture/QA contract; review gate.
3. Task 2 static consistency audit; review gate.
4. Task 3 Burst identity/lifecycle; review gate.
5. Task 4 audio boundary ADR/evidence template; review gate.
6. Task 5 FSM test alignment; focused/full test gate.
7. Task 6 final handoff; user decides whether any changes may be committed or merged.

Tasks 1 and 2 can be prepared independently after Task 0, but edits to shared fixture files are serialized. Tasks 3–5 must consume the accepted vocabulary from earlier gates. No task author may “repair” a finding by editing `design/gdd/player-noise.md`; such a finding is recorded for the active review owner.

## Plan Self-Review

- Spec coverage: Burst identity/lifecycle, terminal `fact_id`, retry/reject/no-refund, all 10 fixtures, trace fields, expected/actual/failure codes, `UNCAPTURED`, `PENDING_OQ6`, tolerance, stress-only fail-closed, stale terms, duplicate YAML keys, ownership, schema, phase order, `S_DIFF`, 30×8 semantics, audio timestamp/DSP/cue/cancel/pause/resume, and injected FSM tests are each assigned to a task.
- Protected scope: neither `design/gdd/player-noise.md` nor its review log is listed as a modification target.
- Shared-file safety: `entities.yaml`, fixture docs, and other contract-center files have one owner at a time and explicit review gates.
- Placeholder scan: no task relies on `TBD`, `TODO`, “appropriate handling”, or an unspecified test target; unresolved external evidence is represented by the explicit statuses `UNCAPTURED` and `PENDING_OQ6`.
- Commit policy: the execution plan stops before commit; commit/merge requires a separate explicit user instruction.



