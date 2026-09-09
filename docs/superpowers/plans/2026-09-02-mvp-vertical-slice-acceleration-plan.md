# MVP Vertical Slice Acceleration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Trong 10 ngày làm việc, tạo một room Unity playable với một player, một guard, hearing/vision, Investigate/Chase, hide/capture, Suspicion Meter và kết quả cuối phòng.

**Architecture:** Giữ Perception, Guard FSM, Event Bus và virtual clock hiện có làm các nguồn authoritative. Unity scene là composition root và presentation layer; Suspicion Grade chỉ là read-only projection, không gửi gameplay command. Runtime hiện tại được đưa vào một local UPM package duy nhất để Unity biên dịch đúng source mà không tạo bản sao.

**Tech Stack:** Unity 6 LTS `6000.3.17f1`, C#, URP, Input System, AI Navigation, Cinemachine, Unity Test Framework/NUnit, local UPM package, PowerShell CI wrappers.

**Spec:** `docs/superpowers/specs/2026-09-01-suspicion-grade-design.md`, `design/gdd/perception.md`, `design/gdd/guard-ai-fsm.md`, `design/gdd/player-noise.md`, `design/gdd/player-movement-hide.md`.

## Global Constraints

- Engine remains Unity 6 LTS `6000.3.17f1`.
- MVP contains one room, one player, one guard, one hide spot, and one exit.
- Required loop is `Start → Move/Noise → Investigate → Vision/Chase → Hide or Capture → Room Result`.
- Player Noise and Player Movement & Hide remain `In Review`; this plan does not change their review status or review logs.
- Existing dirty working-tree changes are user-owned and must not be reset, discarded, or reformatted wholesale.
- `src/AI` remains the single runtime source of truth; never maintain a second copied runtime under `Assets`.
- Gameplay tuning values are loaded from configuration/registry; no new gameplay fallback literals are allowed.
- No Wwise, WebGL benchmark, alert propagation, multi-guard grade, save system, VFX polish, or full documentation synchronization in this milestone.
- No per-render-frame perception polling; sensing and FSM work use the shared virtual-clock path.
- Missing data is fail-closed: meter displays unavailable and room grade becomes unresolved; never substitute zero.
- Every implementation task ends with a Unity test or a documented manual playtest.
- Do not commit or push unless the user explicitly asks.

## Scope Decisions

The following decisions are fixed for this plan:

1. Use a local package rooted at `src/AI` with a package manifest and assembly definition. If Unity requires the conventional `Runtime/` folder for this editor version, move the runtime files once into `src/AI/Runtime/`; do not create a duplicate copy.
2. Use UGUI Canvas for the MVP HUD because the slice needs only a meter, state label, causal text, and result panel.
3. Implement the one-guard grade path only. Keep the existing forward-compatible model types, but do not spend milestone time on exposure-weighted multi-guard scoring.
4. Define successful escape as leaving the room through the exit after breaking active pursuit; capture is the only failed gameplay result in this slice.
5. Keep review mode `lean` and defer full cross-GDD review until the runtime loop has evidence.

## File Map

### Create

- `src/AI/package.json` — local UPM package metadata.
- `src/AI/WhisperWard.AI.asmdef` — runtime assembly boundary.
- `src/AI/Tests/Editor/WhisperWard.AI.Tests.asmdef` — Unity EditMode test assembly.
- `src/AI/Perception/GuardVisionSensor.cs` — tick-driven MVP vision sensor.
- `src/AI/Perception/MvpSuspicionRuntimeAdapter.cs` — minimal authoritative snapshot bridge for the meter.
- `src/Gameplay/PlayerController.cs` — keyboard/mouse movement and stance state.
- `src/Gameplay/PlayerNoiseSourceAdapter.cs` — movement noise submission through `GuardAISystem.SubmitNoise`.
- `src/Gameplay/HideSpot.cs` — typed occupancy transition publisher.
- `src/Gameplay/RoomCompletionController.cs` — exit, capture, reset, and completed-boundary orchestration.
- `src/UI/SuspicionMeterHud.cs` — UGUI view adapter for meter, state, residual, causal event, and result.
- `src/AI/SuspicionGrade/RoomGradeOperator.cs` — deterministic one-room finalization.
- `src/AI/SuspicionGrade/SuspicionGradeSystem.cs` — Event Bus/session composition adapter.
- `src/AI/SuspicionGrade/SuspicionMeterPresenter.cs` — unavailable-safe presentation seam.
- `src/AI/Tests/PlayMode/MvpLoopPlayModeTests.cs` — end-to-end Unity loop test.
- `Assets/Scenes/MVPVerticalSlice.unity` — one playable room.
- `Assets/Prefabs/Player.prefab` — player composition.
- `Assets/Prefabs/Guard.prefab` — guard FSM, navigator, and sensor composition.
- `Assets/Prefabs/HideSpot.prefab` — one hide volume.
- `Assets/Prefabs/MvpHud.prefab` — minimal UGUI presentation.
- `Assets/Input/MVP.inputactions` — movement, crouch, interact, and restart actions.
- `tools/ci/Run-UnityTests.ps1` — reproducible EditMode/PlayMode test command.
- `tools/ci/Build-MVP.ps1` — reproducible Windows/WebGL build wrapper without benchmark claims.
- `production/milestones/2026-09-mvp-vertical-slice.md` — approved scope, owner map, gates, and backlog.
- `docs/qa/mvp-vertical-slice-validation.md` — executed commands, playtest observations, and evidence limits.

### Modify

- `Packages/manifest.json` — Unity packages and local `com.whisperward.ai` dependency.
- `Packages/packages-lock.json` — resolved package lock generated by Unity.
- `ProjectSettings/ProjectVersion.txt` — pin Unity `6000.3.17f1`.
- `ProjectSettings/ProjectSettings.asset` — input, physics, layer, and platform settings.
- `src/AI/GuardAISystem.cs` — compose vision, suspicion grade, and room-boundary services.
- `src/AI/SuspicionGrade/SuspicionGradeConfiguration.cs` — only if registry construction requires a compatibility seam.
- `src/AI/SuspicionGrade/SuspicionGradeEvents.cs` — only for typed boundary/snapshot events required by the existing bus contract.
- `src/AI/Perception/R12Schema.cs` — only if a required immutable MVP identity or snapshot field is absent.
- `design/registry/entities.yaml` — add or reconcile only the Suspicion Grade configuration namespace.
- Existing Suspicion Grade tests — move into the Unity test assembly without duplicating test logic.

### Do not modify

- `design/gdd/systems-index.md`.
- `design/gdd/reviews/player-noise-review-log.md`.
- Any GDD approval/status header.
- Existing Perception formulas, FSM thresholds, noise formulas, or catch rules unless a runtime blocker is proven by a failing Unity test.
- `production/stage.txt` until a formal gate-check approves the stage transition.

## Interfaces

The implementation must reuse these existing seams:

```csharp
IEventBus.Subscribe<T>(Action<T> handler);
IEventBus.Publish(IEvent evt);
GuardAISystem.ConfigureEventBus(IEventBus eventBus);
GuardAISystem.AdvanceGameplayTime(float gameplayDelta);
GuardAISystem.SubmitNoise(NoiseSourceRecord record);
GuardFSM.ConfigureWithPhaseCoordinator(
    IEventBus eventBus,
    IVirtualTickClock clock,
    string activeSessionId,
    long activeEpoch,
    SessionPhaseCoordinator phaseCoordinator);
SuspicionMeterCalculator.Calculate(
    GuardSuspicionSnapshot snapshot,
    SuspicionGradeConfiguration configuration);
RoomGradeOperator.FinalizeRoom(
    RoomCompletionBoundary boundary,
    GradeTraceAggregate aggregate,
    SuspicionGradeConfiguration configuration);
```

The Unity bridge may adapt scene objects into these seams, but may not bypass the Event Bus, replace the virtual clock with `Time.deltaTime`, or call FSM states directly.

## Execution Plan

### Task 0: Scope lock and baseline

**Files:**
- Create: `production/milestones/2026-09-mvp-vertical-slice.md`
- Read-only: all current dirty files and `git status --short`

**Dependencies:** None.

- [ ] Record the current dirty-file list and treat every listed change as protected user work.
- [ ] Copy the fixed loop and in/out scope from this plan into the milestone file.
- [ ] Assign one owner to each file group: Unity setup, runtime, content, QA, documentation.
- [ ] Create the acceptance table with one row for each S1–S8 behavior below.
- [ ] Mark the existing Suspicion Grade implementation plan as parked after its current model/config/reducer checkpoint; do not edit that plan in this task.

**Gate:** The scope is one room/one guard, all file ownership is unique, and no unrelated system is on the critical path.

### Task 1: Unity project and Test Runner scaffold

**Files:**
- Create/modify: `Packages/manifest.json`, `Packages/packages-lock.json`
- Create/modify: `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/ProjectSettings.asset`
- Create: `src/AI/package.json`, `src/AI/WhisperWard.AI.asmdef`, `src/AI/Tests/Editor/WhisperWard.AI.Tests.asmdef`
- Create: `tools/ci/Run-UnityTests.ps1`, `tools/ci/Build-MVP.ps1`

**Dependencies:** Task 0.

- [ ] Create/open a Unity project at the repository root using `6000.3.17f1`.
- [ ] Add URP, Input System, AI Navigation, Cinemachine, and Unity Test Framework packages using versions generated for the pinned editor; do not invent incompatible package versions.
- [ ] Register `src/AI` as a local package dependency with one runtime assembly.
- [ ] Place the existing unit tests in the Unity test assembly without keeping a second executable copy.
- [ ] If the editor does not compile the package-root scripts, move the runtime files once to `src/AI/Runtime/` and update only the assembly/package paths.
- [ ] Configure `Run-UnityTests.ps1` to accept `-UnityPath`, `-Platform`, and `-Filter`, then invoke:

```text
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform editmode -testResults TestResults/editmode.xml -quit
```

- [ ] Add one scaffold smoke test that asserts the Unity test assembly loads.
- [ ] Run the smoke test and save the XML result under `TestResults/` locally; do not commit generated results unless separately requested.

**Gate:** Unity opens the project, compiles with zero errors, the EditMode runner executes, and the scaffold smoke test passes.

### Task 2: Player, guard, camera, and blockout

**Files:**
- Create: `src/Gameplay/PlayerController.cs`, `src/Gameplay/PlayerNoiseSourceAdapter.cs`
- Create: `Assets/Scenes/MVPVerticalSlice.unity`, `Assets/Prefabs/Player.prefab`, `Assets/Prefabs/Guard.prefab`
- Modify: `src/AI/Navigation/GuardNavigator.cs` only when scene composition exposes a proven blocker

**Dependencies:** Task 1.

- [ ] Add a player capsule with keyboard/mouse movement, crouch/walk state, collider, and a stable player identity.
- [ ] Add a simple follow camera; camera orbit polish is out of scope.
- [ ] Add one guard with `GuardFSM`, `GuardNavigator`, `PatrolRoute`, `NavMeshAgent`, and the shared virtual clock.
- [ ] Create one room from primitives, one patrol loop, one exit, and one hide volume.
- [ ] Configure World, Player, Guard, and trigger layers according to the existing physics profile.
- [ ] Bake a NavMesh and verify that patrol and catch destinations are reachable on the authored floor.
- [ ] Play the scene and verify that the player moves and the guard patrols before adding perception.

**Gate:** A five-minute manual walkthrough can start the scene, move the player, and observe a stable guard patrol without console errors.

### Task 3: Runtime perception-to-FSM bridge

**Files:**
- Create: `src/AI/Perception/GuardVisionSensor.cs`, `src/AI/Perception/MvpSuspicionRuntimeAdapter.cs`
- Modify: `src/AI/GuardAISystem.cs`
- Modify: `src/AI/Perception/R12Schema.cs` only if required fields are missing

**Dependencies:** Task 2 and existing `EventBus`, `SessionPhaseCoordinator`, `PerceptionHearingService`, and `GuardFSM` APIs.

- [ ] Subscribe the vision sensor to the shared virtual clock or phase coordinator; never use a render-frame `Update` loop for sensing.
- [ ] Implement the MVP cone using the guard eye position, player perceived position, FOV/range configuration, World layer mask, and `Physics.Linecast` with triggers ignored.
- [ ] Publish existing `LOSGain` and `LOSBreak` facts through the Event Bus with session, epoch, guard, and entry identity.
- [ ] Route player movement noise through `NoiseEmitter` and keep `PerceptionHearingService` as the hearing consumer.
- [ ] Use the existing FSM's event handling to transition Patrol → Investigate on noise and Investigate → Chase on authoritative sight/threshold facts.
- [ ] Make `MvpSuspicionRuntimeAdapter` publish snapshots from authoritative runtime values; it must not recalculate a second threshold or mutate FSM state.
- [ ] Add focused EditMode tests for vision boundary, occlusion, stale epoch rejection, and no per-frame polling.
- [ ] Play the scene: create noise, wait for Investigate, enter visible range, and observe Chase.

**Gate:** The real Unity scene demonstrates `Patrol → Investigate → Chase` with no scripted `PerceptionDriver` dependency.

### Task 4: Hide, capture, escape, and room boundary

**Files:**
- Create: `src/Gameplay/HideSpot.cs`, `src/Gameplay/RoomCompletionController.cs`, `Assets/Prefabs/HideSpot.prefab`
- Modify: `src/AI/GuardAISystem.cs` only for boundary lifecycle registration

**Dependencies:** Task 3.

- [ ] Publish typed hide occupancy transitions with a stable `transition_id`, `session_id`, `attempt_epoch`, and `hide_spot_id`.
- [ ] Feed hide occupancy into the existing Perception/FSM contract; do not infer occupancy from absent LOS.
- [ ] Define escape as an exit trigger event after the player is no longer in active capture range.
- [ ] Define capture using the existing Chase catch contract and publish the existing `Capture` decision record.
- [ ] On reset, begin a new attempt epoch and clear scene-local occupancy/result state.
- [ ] Add PlayMode checks for hidden escape, capture failure, stale event isolation, and repeatable reset.

**Gate:** Both outcomes are playable from the scene: hide/exit produces a completed boundary; catch produces `Failed`.

### Task 5: Consolidated Suspicion Grade deliverable

**Files:**
- Create/modify: `src/AI/SuspicionGrade/RoomGradeOperator.cs`, `SuspicionGradeSystem.cs`, `SuspicionMeterPresenter.cs`
- Modify: `src/AI/GuardAISystem.cs`, `design/registry/entities.yaml`
- Create/modify: `src/AI/Tests/Editor/SuspicionGrade/*`

**Dependencies:** Tasks 1–4 and the existing `SuspicionGradeConfiguration`, `SuspicionMeterCalculator`, `GradeTraceReducer`, and model/event types.

- [ ] Add only the registry-backed grade configuration keys: incident weights, cap, S/A/B thresholds, meter scale, and schema version.
- [ ] Implement deterministic one-guard finalization:

```text
P_incident = min(cap,
    weight_chase * chase_count
  + weight_fruitless * fruitless_count
  + weight_capture * capture_count)
P_residual = weight_residual * clamp(R_final / R_max, 0, 1)
Q = clamp(100 - P_incident - P_residual, 0, 100)
```

- [ ] Apply status precedence: Capture/incomplete failed reset → `Failed`; missing mandatory data → `Unresolved`; otherwise map Q to S/A/B/Needs Improvement.
- [ ] Wire snapshot, decision trace, room boundary, session reset, stale epoch, and duplicate delivery through the existing Event Bus.
- [ ] Make repeated finalization for the same `(session_id, attempt_epoch, room_id)` return the same immutable result.
- [ ] Add tests for SG-AC1 through SG-AC14, excluding multi-guard tuning from the MVP runtime path.
- [ ] Add a fake view and presenter tests proving unavailable never renders as `0%`.

**Gate:** The meter changes in the scene, remains read-only, and the result panel receives a deterministic completed/failed/unresolved result.

### Task 6: HUD and result presentation

**Files:**
- Create: `src/UI/SuspicionMeterHud.cs`, `Assets/Prefabs/MvpHud.prefab`
- Modify: `Assets/Scenes/MVPVerticalSlice.unity`

**Dependencies:** Task 5.

- [ ] Show live meter percent, semantic region text, residual as a separate visual channel, and the last causal event.
- [ ] Show explicit `Unavailable` when the snapshot is missing; never show an inferred zero.
- [ ] Show room status and grade only after the room boundary callback.
- [ ] Include text/state semantics in addition to color so the MVP is not color-only.
- [ ] Add a manual UI walkthrough record to `docs/qa/mvp-vertical-slice-validation.md`.

**Gate:** A new player can identify Calm/Investigate/Chase and understand the final result without developer console output.

### Task 7: End-to-end smoke test and reproducible commands

**Files:**
- Create: `src/AI/Tests/PlayMode/MvpLoopPlayModeTests.cs`
- Modify: `tools/ci/Run-UnityTests.ps1`, `docs/qa/mvp-vertical-slice-validation.md`

**Dependencies:** Tasks 1–6.

- [ ] Implement one deterministic PlayMode test that advances the virtual clock and drives the complete room sequence.
- [ ] Assert the ordered behavior: movement, noise, Investigate, LOS, Chase, LOS break/hide, exit, completed result.
- [ ] Add a second failure path asserting Capture produces `Failed` regardless of numeric grade.
- [ ] Run EditMode and PlayMode commands through the wrapper and record exact pass/fail counts.
- [ ] Run a manual 3–5 minute playthrough from a clean launch with no developer shortcuts.
- [ ] Record any unavailable environment evidence explicitly instead of converting it to a pass.

**Gate:** One reproducible command and one manual playtest prove the full loop.

### Task 8: Minimal hardening and vertical-slice decision

**Files:**
- Modify: `docs/qa/mvp-vertical-slice-validation.md`
- Modify: `production/milestones/2026-09-mvp-vertical-slice.md`
- Modify: `production/session-state/active.md` only after an implementation milestone is actually verified

**Dependencies:** Task 7.

- [ ] Run static scans for duplicate tuning literals, direct FSM threshold mutation, render-frame sensing, and missing lifecycle unsubscribe/reset.
- [ ] Run malformed/stale/duplicate/missing-data tests that can affect the playable loop.
- [ ] Fix only Critical/Important findings or issues that break the eight MVP behaviors.
- [ ] Put Minor wording, line citation, refactor, polish, Wwise, benchmark, and full-review findings into the backlog.
- [ ] Record velocity by day and actual time to first meaningful player action.
- [ ] Decide `PROCEED`, `PIVOT`, or `STOP` from the observed loop, technical blockers, and velocity.
- [ ] Stop before commit and present the diff, test evidence, playtest result, and remaining backlog to the user.

**Gate:** The slice either proves the loop and is ready for the next milestone, or has an explicit pivot reason; no silent scope expansion is allowed.

## Acceptance Matrix

| ID | Required evidence | Pass condition |
|---|---|---|
| S1 | Unity project open + EditMode result | Project compiles and Test Runner executes. |
| S2 | Scene playtest | Player moves and guard patrols. |
| S3 | PlayMode/test evidence | Noise causes Investigate. |
| S4 | PlayMode/test evidence | Authoritative LOS causes Chase. |
| S5 | Scene playtest | Hide/escape completes; catch produces Failed. |
| S6 | HUD walkthrough | Meter, residual, state, and causal event update visibly. |
| S7 | Room boundary test | Completed/Failed/Unresolved result is shown and idempotent. |
| S8 | Full smoke test + static scan | No stale leakage, duplicate penalty, hardcoded tuning, or frame polling. |

## Execution Order

The critical path is:

```text
Scope lock
  → Unity scaffold/Test Runner
  → Player + Guard scene
  → Vision/Hearing → FSM
  → Hide/Capture/Exit
  → Suspicion Grade + HUD
  → End-to-end smoke test
  → Playtest and gate
```

Content blockout and QA test authoring may proceed in parallel after Task 1, but scene/prefab ownership and runtime composition remain serialized at the integration gates. No later task may compensate for unavailable data by substituting zero or by bypassing the authoritative Event Bus.

## Deferred Backlog

- Full Player Noise re-review and approval synchronization.
- Full Player Movement & Hide re-review.
- Alert propagation and multiple guards.
- Weighted multi-guard grade tuning.
- Wwise or other middleware integration.
- WebGL benchmark and target-hardware profiling.
- VFX, audio, animation, and lighting polish.
- Save/session persistence beyond room attempt reset.
- Full architecture review, telemetry standardization, and documentation cleanup.

## Completion Criteria

This plan is complete only when S1–S8 have evidence, the manual loop has been observed from a clean launch, the validation report records all unavailable evidence honestly, and the user has received the diff/backlog summary. Completion does not authorize commit, push, GDD approval, or stage transition.
