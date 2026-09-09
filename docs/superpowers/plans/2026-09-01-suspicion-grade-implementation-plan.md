# Suspicion Meter / Grade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved Suspicion Meter / Grade MVP as a deterministic, read-only projection of authoritative Perception/FSM/session data, with per-guard meter output and trace-weighted room grading.

**Architecture:** Keep the feature in a focused `src/AI/SuspicionGrade/` module. `SuspicionMeterCalculator` projects an authoritative accumulator snapshot; `GradeTraceReducer` validates, filters, and deduplicates immutable trace records; `RoomGradeOperator` finalizes one immutable result at a valid room boundary. `SuspicionGradeSystem` is the injected composition adapter for the existing Event Bus and `SessionBoundaryService`; it never mutates Perception, FSM thresholds, or gameplay state.

**Tech Stack:** Unity 6 LTS `6000.3.17f1`, C#, Unity Test Framework/NUnit, existing typed Event Bus, existing virtual clock/session boundary contracts, YAML entity registry, and URP-compatible UI presentation.

## Global Constraints

- Engine is Unity 6 LTS `6000.3.17f1`.
- All production gameplay values are loaded from registry/configuration; the HUD, reducer, FSM, and Perception code must not embed tuning constants.
- Existing registry-owned `T_base`, `T_floor`, `k_res`, `T_chase`, `R_max`, and `residual_decay` remain authoritative; Suspicion/Grade consumes them and does not redefine Perception formulas.
- The grade configuration owns only `grade_weight_chase=25`, `grade_weight_fruitless=10`, `grade_weight_capture=50`, `grade_weight_residual=10`, `grade_penalty_cap=90`, `grade_threshold_s=90`, `grade_threshold_a=75`, `grade_threshold_b=60`, `meter_display_scale=100`, and `grade_operator_schema_version=1`; these values must be represented in the registry and injected into runtime code.
- Identity is always `(session_id, attempt_epoch)`; stale epochs are ignored and a restart cannot leak incidents into the new epoch.
- Deduplication key is `(session_id, attempt_epoch, entry_id, event_type)`.
- Finalization is idempotent for one `(session_id, attempt_epoch, room_id)` boundary.
- Missing accumulator/threshold data renders as `Unavailable`; missing mandatory trace or residual data produces `Unresolved`, never a fabricated zero.
- Capture or failed/incomplete room completion produces `Failed` and never S/A/B.
- No direct penalty is created for noise emission, relay receipt, clean diversion, near miss, clean LOS break, suppression micro-tell, residual decay, or non-fruitless investigation resolution.
- Production APIs have XML documentation; public methods are dependency-injected and unit-testable.
- Do not add a second ADR for an existing unrelated decision; this feature gets its own ADR because every system requires one.
- AI update budget remains 2 ms per frame; the feature is event/boundary driven and must not poll or allocate per frame.
- Do not claim Unity, runtime, WebGL, Wwise, or target-hardware evidence until it has actually been captured.
- Do not commit or push unless the user explicitly requests it.

---

## File Map

### Create

- `src/AI/SuspicionGrade/SuspicionGradeConfiguration.cs` — immutable registry-backed tuning contract and validation.
- `src/AI/SuspicionGrade/SuspicionGradeModels.cs` — immutable input, read-model, trace aggregate, and final-output types.
- `src/AI/SuspicionGrade/SuspicionMeterCalculator.cs` — pure meter ratio/region projection.
- `src/AI/SuspicionGrade/GradeTraceReducer.cs` — pure identity validation, filtering, deduplication, and incident aggregation.
- `src/AI/SuspicionGrade/RoomGradeOperator.cs` — deterministic penalty, score, grade, completion-status, and idempotent finalization.
- `src/AI/SuspicionGrade/SuspicionGradeEvents.cs` — typed snapshot, trace, and room-boundary event contracts used by the adapter.
- `src/AI/SuspicionGrade/SuspicionGradeSystem.cs` — injected Event Bus/session lifecycle adapter and read-model ownership.
- `src/AI/SuspicionGrade/SuspicionMeterPresenter.cs` — presentation interface and unavailable-state-safe adapter.
- `tests/unit/suspicion-grade/SuspicionGradeTestFixtures.cs` — deterministic fixture factories and registry-backed test values.
- `tests/unit/suspicion-grade/SuspicionGradeConfigurationTests.cs` — configuration validation and registry authority tests.
- `tests/unit/suspicion-grade/SuspicionMeterCalculatorTests.cs` — SG-AC1, SG-AC2, and missing meter input tests.
- `tests/unit/suspicion-grade/GradeTraceReducerTests.cs` — SG-AC3–SG-AC5, SG-AC8–SG-AC10 trace tests.
- `tests/unit/suspicion-grade/RoomGradeOperatorTests.cs` — SG-AC6–SG-AC8, SG-AC11, SG-AC12, and multi-guard tests.
- `tests/unit/suspicion-grade/SuspicionGradeSystemTests.cs` — Event Bus/session/finalization integration tests.
- `docs/architecture/adr-0004-suspicion-meter-grade.md` — architecture decision record.
- `docs/qa/suspicion-grade-validation.md` — executed test commands, results, and evidence limitations.

### Modify

- `design/registry/entities.yaml` — add the Suspicion/Grade registry namespace and values while preserving existing source references and YAML style.
- `src/AI/GuardAISystem.cs` — inject the registry-backed configuration, subscribe the feature to the existing Event Bus, and bind session boundaries.
- `src/AI/Perception/R12Schema.cs` — only if the existing decision schema lacks a stable immutable field required by the adapter; preserve existing event names and wire compatibility.
- `src/AI/Testing/DecisionTap.cs` — add capture/assertion helpers only if the new system tests cannot use the existing API without duplicating Event Bus behavior.
- `Packages/manifest.json` and `ProjectSettings/ProjectVersion.txt` — only as part of the selected A2 Unity skeleton prerequisite if those files are still absent.

### Do not modify

- Perception threshold calculations, FSM transitions, noise scoring, or gameplay commands.
- `production/session-state/active.md` except for the required milestone checkpoint after implementation work begins.
- Player Noise approval/review records.

---

## Interfaces and Data Contracts

The implementation must use these names and meanings consistently across all tasks.

```csharp
public enum SuspicionMeterRegion
{
    Unavailable,
    Quiet,
    Investigate,
    Chase
}

public enum GradeCompletionStatus
{
    Pending,
    Completed,
    Failed,
    Unresolved
}

public enum RoomGrade
{
    None,
    S,
    A,
    B,
    NeedsImprovement
}

public enum GradeIncidentType
{
    ChaseEntry,
    FruitlessResolution,
    Capture
}

public enum InvestigationResolutionCause
{
    Noise,
    Fruitless,
    Vision,
    Other
}
```

```csharp
public readonly struct SuspicionGradeIdentity : IEquatable<SuspicionGradeIdentity>
{
    public string SessionId { get; }
    public long AttemptEpoch { get; }
    public string RoomId { get; }
    public string GuardEid { get; }

    public SuspicionGradeIdentity(string sessionId, long attemptEpoch,
        string roomId, string guardEid);

    public bool Equals(SuspicionGradeIdentity other);
    public override bool Equals(object obj);
    public override int GetHashCode();
}
```

```csharp
public sealed class SuspicionGradeConfiguration
{
    public float TBase { get; }
    public float KResidual { get; }
    public float TFloor { get; }
    public float TChase { get; }
    public float RMax { get; }
    public float GradeWeightChase { get; }
    public float GradeWeightFruitless { get; }
    public float GradeWeightCapture { get; }
    public float GradeWeightResidual { get; }
    public float GradePenaltyCap { get; }
    public float GradeThresholdS { get; }
    public float GradeThresholdA { get; }
    public float GradeThresholdB { get; }
    public float MeterDisplayScale { get; }
    public int GradeOperatorSchemaVersion { get; }

    public SuspicionGradeConfiguration(
        float tBase, float kResidual, float tFloor, float tChase, float rMax,
        float gradeWeightChase, float gradeWeightFruitless,
        float gradeWeightCapture, float gradeWeightResidual,
        float gradePenaltyCap, float gradeThresholdS, float gradeThresholdA,
        float gradeThresholdB, float meterDisplayScale,
        int gradeOperatorSchemaVersion);
}
```

```csharp
public readonly struct GuardSuspicionSnapshot
{
    public SuspicionGradeIdentity Identity { get; }
    public bool HasAccumulator { get; }
    public float ACurrent { get; }
    public bool HasResidual { get; }
    public float RCurrent { get; }
    public bool HasChaseThreshold { get; }
    public float TChase { get; }
    public float InvestigationEntryThreshold { get; }
    public string GuardState { get; }
    public string LastCausalEvent { get; }
    public string SourceEntryId { get; }
}
```

```csharp
public sealed class SuspicionMeterReadModel
{
    public SuspicionGradeIdentity Identity { get; }
    public bool IsAvailable { get; }
    public float ACurrent { get; }
    public float TChase { get; }
    public float MeterRatio { get; }
    public int MeterPercent { get; }
    public SuspicionMeterRegion Region { get; }
    public float RCurrent { get; }
    public string GuardState { get; }
    public string LastCausalEvent { get; }
    public string SourceEntryId { get; }
}
```

```csharp
public sealed class GradeTraceRecord
{
    public string SessionId { get; }
    public long AttemptEpoch { get; }
    public string RoomId { get; }
    public string GuardEid { get; }
    public string EntryId { get; }
    public GradeIncidentType? IncidentType { get; }
    public InvestigationResolutionCause? ResolutionCause { get; }
    public string EventId { get; }
}
```

```csharp
public sealed class RoomCompletionBoundary
{
    public string SessionId { get; }
    public long AttemptEpoch { get; }
    public string RoomId { get; }
    public bool IsCapture { get; }
    public bool IsCompletedRoom { get; }
    public bool HasMandatoryTrace { get; }
    public bool HasFinalResidualSnapshot { get; }
    public IReadOnlyDictionary<string, float> FinalResidualByGuard { get; }
    public IReadOnlyDictionary<string, float> ExposureWeightByGuard { get; }
}
```

```csharp
public sealed class GuardGradeBreakdown
{
    public string GuardEid { get; }
    public int ChaseEntryCount { get; }
    public int FruitlessResolutionCount { get; }
    public int CaptureCount { get; }
    public float IncidentPenalty { get; }
    public float ResidualPenalty { get; }
    public float QualityScore { get; }
    public float ExposureWeight { get; }
}

public sealed class GradeTraceAggregate
{
    public string SessionId { get; }
    public long AttemptEpoch { get; }
    public string RoomId { get; }
    public int ChaseEntryCount { get; }
    public int FruitlessResolutionCount { get; }
    public int CaptureCount { get; }
    public IReadOnlyList<GuardGradeBreakdown> PerGuard { get; }
    public IReadOnlyList<string> ContributingEventIds { get; }
}

public sealed class RoomGradeFinalized
{
    public string SessionId { get; }
    public long AttemptEpoch { get; }
    public string RoomId { get; }
    public int GradeVersion { get; }
    public GradeCompletionStatus CompletionStatus { get; }
    public IReadOnlyList<GuardGradeBreakdown> PerGuardBreakdown { get; }
    public float IncidentPenalty { get; }
    public float ResidualPenalty { get; }
    public float QualityScore { get; }
    public RoomGrade Grade { get; }
    public IReadOnlyList<string> ContributingEventIds { get; }
}
```

```csharp
public static class SuspicionGradeTestFixtures
{
    public static SuspicionGradeConfiguration CreateStarterConfiguration(
        float tChase = 100f, float rMax = 100f, float tBase = 30f,
        float kResidual = 0.5f, float tFloor = 10f);
    public static SuspicionGradeConfiguration CreateConfiguration(
        float gradeThresholdS, float gradeThresholdA, float gradeThresholdB,
        float tChase = 100f, float rMax = 100f, float tBase = 30f,
        float kResidual = 0.5f, float tFloor = 10f);
    public static GuardSuspicionSnapshot Snapshot(
        float accumulator, float residual, float tChase,
        float entryThreshold, string guardState);
    public static GuardSuspicionSnapshot SnapshotWithoutAccumulator();
    public static GuardSuspicionSnapshot SnapshotWithoutChaseThreshold();
    public static GradeTraceRecord ChaseEntry(
        string sessionId, long attemptEpoch, string roomId, string guardEid,
        string entryId, string eventId);
    public static GradeTraceRecord Resolution(
        string sessionId, long attemptEpoch, string roomId, string guardEid,
        string eventId, InvestigationResolutionCause cause);
    public static GradeTraceRecord NonIncident(
        string sessionId, long attemptEpoch, string roomId, string guardEid,
        string eventId);
    public static IReadOnlyList<GradeTraceRecord> MixedTrace(
        string sessionId, long attemptEpoch, string roomId);
    public static GradeTraceAggregate EmptyAggregate(
        string sessionId, long attemptEpoch, string roomId);
    public static GradeTraceAggregate Aggregate(
        int chaseEntries, int fruitlessResolutions, int captures);
    public static GradeTraceAggregate AggregateForScore(float targetScore);
    public static RoomCompletionBoundary CompletedBoundary(
        string sessionId, long attemptEpoch, string roomId, float residual);
    public static RoomCompletionBoundary CaptureBoundary(
        string sessionId, long attemptEpoch, string roomId);
    public static RoomCompletionBoundary IncompleteTraceBoundary(
        string sessionId, long attemptEpoch, string roomId);
    public static RoomCompletionBoundary MissingResidualBoundary(
        string sessionId, long attemptEpoch, string roomId);
    public static SuspicionMeterReadModel UnavailableReadModel();
    public static SuspicionMeterReadModel AvailableReadModel(
        int percent, SuspicionMeterRegion region, float residual,
        string causalEvent);
    public static SuspicionGradeSystemHarness CreateSystemHarness();
}

public sealed class SuspicionGradeSystemHarness
{
    public ISuspicionGradeSystem System { get; }
    public SessionBoundaryService SessionBoundary { get; }
    public FakeSuspicionMeterPresenter Presenter { get; }
    public int FinalizedCount { get; }
}

public sealed class FakeSuspicionMeterPresenter : ISuspicionMeterPresenter
{
    public SuspicionMeterReadModel LastReadModel { get; }
    public void Present(SuspicionMeterReadModel readModel);
}

public interface ISuspicionMeterView
{
    void ShowUnavailable();
    void SetMeterPercent(int percent);
    void SetRegion(SuspicionMeterRegion region);
    void SetResidual(float residual);
    void SetCausalEvent(string causalEvent);
}

public interface ISuspicionMeterPresenter
{
    void Present(SuspicionMeterReadModel readModel);
}

public sealed class SuspicionMeterPresenter : ISuspicionMeterPresenter
{
    public SuspicionMeterPresenter(ISuspicionMeterView view);
    public void Present(SuspicionMeterReadModel readModel);
}

public interface ISuspicionGradeSystem
{
    void AcceptSnapshot(GuardSuspicionSnapshot snapshot);
    void AcceptTrace(GradeTraceRecord traceRecord);
    RoomGradeFinalized FinalizeRoom(RoomCompletionBoundary boundary);
    void ResetForBoundary(string sessionId, long attemptEpoch);
}
```

---

## Task 1: Establish Unity/Test Runner Prerequisite and Registry Contract

**Files:**
- Create or modify: `Packages/manifest.json`
- Create or modify: `ProjectSettings/ProjectVersion.txt`
- Modify: `design/registry/entities.yaml`
- Create: `src/AI/SuspicionGrade/SuspicionGradeConfiguration.cs`
- Create: `tests/unit/suspicion-grade/SuspicionGradeConfigurationTests.cs`
- Create: `tests/unit/suspicion-grade/SuspicionGradeTestFixtures.cs`
- Create: `docs/architecture/adr-0004-suspicion-meter-grade.md`

**Dependencies:** The selected A2 Unity skeleton must exist before Unity Test Framework commands can run. If A2 has already created the two Unity files, reuse them and only add the feature package/configuration entries.

**Interfaces:** Produces `SuspicionGradeConfiguration` for Tasks 2–6 and the registry field names consumed by the composition root.

- [ ] **Step 1: Add failing configuration tests.**

```csharp
[Test]
public void Constructor_accepts_registry_starter_values()
{
    var configuration = SuspicionGradeTestFixtures.CreateStarterConfiguration();

    Assert.That(configuration.GradeWeightChase, Is.EqualTo(25f));
    Assert.That(configuration.GradeWeightFruitless, Is.EqualTo(10f));
    Assert.That(configuration.GradeWeightCapture, Is.EqualTo(50f));
    Assert.That(configuration.GradeWeightResidual, Is.EqualTo(10f));
    Assert.That(configuration.GradePenaltyCap, Is.EqualTo(90f));
    Assert.That(configuration.GradeThresholdS, Is.EqualTo(90f));
    Assert.That(configuration.GradeThresholdA, Is.EqualTo(75f));
    Assert.That(configuration.GradeThresholdB, Is.EqualTo(60f));
    Assert.That(configuration.MeterDisplayScale, Is.EqualTo(100f));
    Assert.That(configuration.GradeOperatorSchemaVersion, Is.EqualTo(1));
}

[Test]
public void Constructor_rejects_non_monotonic_grade_thresholds()
{
    Assert.Throws<ArgumentOutOfRangeException>(() =>
        SuspicionGradeTestFixtures.CreateConfiguration(60f, 75f, 90f));
}

[Test]
public void Constructor_rejects_non_positive_chase_threshold_or_residual_max()
{
    Assert.Throws<ArgumentOutOfRangeException>(() =>
        SuspicionGradeTestFixtures.CreateConfiguration(
            gradeThresholdS: 90f, gradeThresholdA: 75f, gradeThresholdB: 60f,
            tChase: 0f));

    Assert.Throws<ArgumentOutOfRangeException>(() =>
        SuspicionGradeTestFixtures.CreateConfiguration(
            gradeThresholdS: 90f, gradeThresholdA: 75f, gradeThresholdB: 60f,
            rMax: 0f));
}
```

- [ ] **Step 2: Run the focused test before implementation.**

Run:

```text
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform editmode -testFilter SuspicionGradeConfigurationTests -testResults TestResults/suspicion-grade-config-before.xml -quit
```

Expected: FAIL because the feature configuration and fixture types do not yet exist.

- [ ] **Step 3: Add the registry keys.** Add a `suspicion_grade` mapping using the project’s existing registry schema and preserve the exact starter values:

```yaml
suspicion_grade:
  grade_weight_chase: 25
  grade_weight_fruitless: 10
  grade_weight_capture: 50
  grade_weight_residual: 10
  grade_penalty_cap: 90
  grade_threshold_s: 90
  grade_threshold_a: 75
  grade_threshold_b: 60
  meter_display_scale: 100
  grade_operator_schema_version: 1
```

Reference, do not duplicate, the existing registry entries for `T_base`, `k_res`, `T_floor`, `T_chase`, and `R_max`.

- [ ] **Step 4: Implement the immutable configuration and fixture factory.** The production constructor must validate `TChase > 0`, `RMax > 0`, `MeterDisplayScale > 0`, all weights/cap values `>= 0`, thresholds within `[0, 100]`, and `ThresholdS >= ThresholdA >= ThresholdB`. The fixture factory may use the approved starter values as test data but production code may not contain those literals as fallback tuning.

- [ ] **Step 5: Add the ADR.** Record that the feature uses pure projections plus an injected boundary finalizer, consumes registry-owned thresholds, deduplicates by identity/event key, and does not send gameplay commands. Record rejection of a frame-polled singleton and rejection of recalculating Perception/FSM state.

- [ ] **Step 6: Run the focused test after implementation.**

Expected: PASS for all configuration tests.

---

## Task 2: Implement the Pure Per-Guard Meter Projection

**Files:**
- Create: `src/AI/SuspicionGrade/SuspicionGradeModels.cs`
- Create: `src/AI/SuspicionGrade/SuspicionMeterCalculator.cs`
- Create: `tests/unit/suspicion-grade/SuspicionMeterCalculatorTests.cs`

**Dependencies:** Task 1 configuration contract.

**Interfaces:** Produces `SuspicionMeterCalculator.Calculate(GuardSuspicionSnapshot snapshot, SuspicionGradeConfiguration configuration)` returning `SuspicionMeterReadModel`.

- [ ] **Step 1: Write boundary and missing-data tests.**

```csharp
[TestCase(0f, 0)]
[TestCase(50f, 50)]
[TestCase(100f, 100)]
[TestCase(125f, 100)]
public void Calculate_clamps_meter_percent_to_zero_through_one_hundred(
    float accumulator, int expectedPercent)
{
    var snapshot = SuspicionGradeTestFixtures.Snapshot(
        accumulator, residual: 0f, tChase: 100f,
        entryThreshold: 25f, guardState: "Patrol");

    var model = SuspicionMeterCalculator.Calculate(
        snapshot, SuspicionGradeTestFixtures.CreateStarterConfiguration());

    Assert.That(model.IsAvailable, Is.True);
    Assert.That(model.MeterPercent, Is.EqualTo(expectedPercent));
}

[Test]
public void Calculate_uses_snapshot_chase_threshold_not_a_second_fsm_threshold()
{
    var snapshot = SuspicionGradeTestFixtures.Snapshot(
        accumulator: 40f, residual: 0f, tChase: 80f,
        entryThreshold: 20f, guardState: "Investigate");

    var model = SuspicionMeterCalculator.Calculate(
        snapshot, SuspicionGradeTestFixtures.CreateStarterConfiguration(tChase: 80f));

    Assert.That(model.MeterPercent, Is.EqualTo(50));
    Assert.That(model.TChase, Is.EqualTo(80f));
}

[Test]
public void Calculate_marks_missing_accumulator_or_threshold_unavailable()
{
    var missingAccumulator = SuspicionGradeTestFixtures.SnapshotWithoutAccumulator();
    var missingThreshold = SuspicionGradeTestFixtures.SnapshotWithoutChaseThreshold();
    var configuration = SuspicionGradeTestFixtures.CreateStarterConfiguration();

    Assert.That(SuspicionMeterCalculator.Calculate(missingAccumulator, configuration).IsAvailable,
        Is.False);
    Assert.That(SuspicionMeterCalculator.Calculate(missingThreshold, configuration).IsAvailable,
        Is.False);
}

[Test]
public void Calculate_separates_residual_from_live_accumulator_and_selects_regions()
{
    var configuration = SuspicionGradeTestFixtures.CreateStarterConfiguration(
        tChase: 100f, tBase: 30f, tFloor: 10f, kResidual: 0.5f);

    var model = SuspicionMeterCalculator.Calculate(
        SuspicionGradeTestFixtures.Snapshot(
            accumulator: 20f, residual: 20f, tChase: 100f,
            entryThreshold: 20f, guardState: "Investigate"), configuration);

    Assert.That(model.MeterPercent, Is.EqualTo(20));
    Assert.That(model.RCurrent, Is.EqualTo(20f));
    Assert.That(model.Region, Is.EqualTo(SuspicionMeterRegion.Investigate));
}
```

- [ ] **Step 2: Run the focused tests before implementation.**

Expected: FAIL because `SuspicionGradeModels` and `SuspicionMeterCalculator` do not exist.

- [ ] **Step 3: Implement the pure calculator.** Use `clamp(ACurrent / TChase, 0, 1)` and `round(ratio * MeterDisplayScale)`. Use the supplied `InvestigationEntryThreshold`; do not recalculate `T_entry(R)` inside this feature. Select `Chase` when the accumulator is at least `TChase` or the authoritative state is `Chase`, `Investigate` when at or above the supplied entry threshold, otherwise `Quiet`. Copy residual and causal fields without integrating or decaying them.

- [ ] **Step 4: Run the focused tests after implementation.**

Expected: PASS for zero, normal, threshold, overshoot, unavailable, residual-separation, and semantic-region tests.

- [ ] **Step 5: Add explicit SG-AC comments to the test fixture names or test case metadata.** Map SG-AC1 to the clamp tests and SG-AC2 to the threshold-source test without adding runtime behavior.

---

## Task 3: Build the Immutable Trace Adapter and Deterministic Reducer

**Files:**
- Create: `src/AI/SuspicionGrade/GradeTraceReducer.cs`
- Create: `src/AI/SuspicionGrade/SuspicionGradeEvents.cs`
- Create: `tests/unit/suspicion-grade/GradeTraceReducerTests.cs`
- Modify: `src/AI/Perception/R12Schema.cs` only if the adapter cannot obtain an immutable identity/entry/event classification from existing records.

**Dependencies:** Task 1 identity/configuration types and existing `DecisionRecord` subclasses (`ChaseEntry`, `InvestigateResolution`, `Capture`).

**Interfaces:** `GradeTraceReducer.Accept(GradeTraceRecord record)`, `GradeTraceReducer.Aggregate(string sessionId, long attemptEpoch, string roomId)`, and `GradeTraceReducer.Reset(string sessionId, long attemptEpoch, string roomId)`.

- [ ] **Step 1: Write failing reducer tests.**

```csharp
[Test]
public void Accept_counts_one_chase_entry_once_by_entry_id()
{
    var reducer = new GradeTraceReducer();
    var first = SuspicionGradeTestFixtures.ChaseEntry("session", 1, "room", "guard-1", "entry-7", "event-1");
    var retry = SuspicionGradeTestFixtures.ChaseEntry("session", 1, "room", "guard-1", "entry-7", "event-1-retry");

    reducer.Accept(first);
    reducer.Accept(retry);

    var aggregate = reducer.Aggregate("session", 1, "room");
    Assert.That(aggregate.ChaseEntryCount, Is.EqualTo(1));
    Assert.That(aggregate.ContributingEventIds, Is.EqualTo(new[] { "event-1" }));
}

[Test]
public void Accept_counts_only_fruitless_investigation_resolution()
{
    var reducer = new GradeTraceReducer();
    reducer.Accept(SuspicionGradeTestFixtures.Resolution(
        "session", 1, "room", "guard-1", "resolve-noise", InvestigationResolutionCause.Noise));
    reducer.Accept(SuspicionGradeTestFixtures.Resolution(
        "session", 1, "room", "guard-1", "resolve-fruitless", InvestigationResolutionCause.Fruitless));

    var aggregate = reducer.Aggregate("session", 1, "room");
    Assert.That(aggregate.FruitlessResolutionCount, Is.EqualTo(1));
}

[Test]
public void Accept_ignores_noise_relay_clean_los_and_near_miss_records()
{
    var reducer = new GradeTraceReducer();
    reducer.Accept(SuspicionGradeTestFixtures.NonIncident("session", 1, "room", "guard-1", "noise-1"));
    reducer.Accept(SuspicionGradeTestFixtures.NonIncident("session", 1, "room", "guard-1", "relay-1"));
    reducer.Accept(SuspicionGradeTestFixtures.NonIncident("session", 1, "room", "guard-1", "los-break-1"));

    var aggregate = reducer.Aggregate("session", 1, "room");
    Assert.That(aggregate.ChaseEntryCount, Is.EqualTo(0));
    Assert.That(aggregate.FruitlessResolutionCount, Is.EqualTo(0));
    Assert.That(aggregate.CaptureCount, Is.EqualTo(0));
}

[Test]
public void Accept_ignores_stale_epoch_without_erasing_current_aggregate()
{
    var reducer = new GradeTraceReducer();
    reducer.Accept(SuspicionGradeTestFixtures.ChaseEntry("session", 2, "room", "guard-1", "current", "current-event"));
    reducer.Accept(SuspicionGradeTestFixtures.ChaseEntry("session", 1, "room", "guard-1", "stale", "stale-event"));

    var aggregate = reducer.Aggregate("session", 2, "room");
    Assert.That(aggregate.ChaseEntryCount, Is.EqualTo(1));
    Assert.That(aggregate.ContributingEventIds, Does.Not.Contain("stale-event"));
}

[Test]
public void Aggregate_is_order_invariant()
{
    var records = SuspicionGradeTestFixtures.MixedTrace("session", 1, "room");
    var forward = new GradeTraceReducer();
    var reverse = new GradeTraceReducer();

    foreach (var record in records) forward.Accept(record);
    for (var i = records.Count - 1; i >= 0; i--) reverse.Accept(records[i]);

    Assert.That(forward.Aggregate("session", 1, "room"), Is.EqualTo(
        reverse.Aggregate("session", 1, "room")));
}
```

- [ ] **Step 2: Run the reducer tests before implementation.**

Expected: FAIL because the reducer, immutable records, aggregate, and test factories do not exist.

- [ ] **Step 3: Implement `GradeTraceRecord` factories/adaptation.** Convert existing `DecisionRecord` deliveries at the adapter boundary into immutable records. Map only `ChaseEntry`, `InvestigateResolution(cause=Fruitless)`, and `Capture` to incident records. Preserve `event_id`, `entry_id`, session, epoch, room, and guard identity. Any malformed record is rejected with a stable diagnostic and cannot mutate a valid aggregate.

- [ ] **Step 4: Implement reducer identity and dedupe.** Reject empty session/room/entry/event IDs, reject negative epochs, ignore records whose session/epoch/room does not match the queried aggregate, and use a `HashSet` keyed by `(session, epoch, entry, eventType)`. Store the first valid event ID for breakdown output. Do not dedupe by delivery timestamp or object reference.

- [ ] **Step 5: Make aggregate output deterministic.** Sort contributing event IDs ordinally before exposing them. Aggregate counts by guard as well as room so the finalizer can support one-guard and forward-compatible weighted multi-guard scoring.

- [ ] **Step 6: Run the reducer tests after implementation.**

Expected: PASS for chase dedupe, fruitless-only accounting, clean-noise exclusion, stale epoch rejection, malformed-record rejection, retry idempotency, and delivery-order invariance.

---

## Task 4: Implement Deterministic Room Finalization

**Files:**
- Create: `src/AI/SuspicionGrade/RoomGradeOperator.cs`
- Create: `tests/unit/suspicion-grade/RoomGradeOperatorTests.cs`

**Dependencies:** Tasks 1 and 3.

**Interfaces:** `RoomGradeOperator.FinalizeRoom(RoomCompletionBoundary boundary, GradeTraceAggregate aggregate, SuspicionGradeConfiguration configuration)` returns `RoomGradeFinalized`.

- [ ] **Step 1: Write failing formula and status tests.**

```csharp
[TestCase(0f, 0f, 100f, RoomGrade.S)]
public void Finalize_maps_clean_completed_room_to_s(
    float residual, float expectedPenalty, float expectedScore, RoomGrade expectedGrade)
{
    var result = RoomGradeOperator.FinalizeRoom(
        SuspicionGradeTestFixtures.CompletedBoundary("session", 1, "room", residual),
        SuspicionGradeTestFixtures.EmptyAggregate("session", 1, "room"),
        SuspicionGradeTestFixtures.CreateStarterConfiguration());

    Assert.That(result.CompletionStatus, Is.EqualTo(GradeCompletionStatus.Completed));
    Assert.That(result.IncidentPenalty, Is.EqualTo(expectedPenalty));
    Assert.That(result.QualityScore, Is.EqualTo(expectedScore));
    Assert.That(result.Grade, Is.EqualTo(expectedGrade));
}

[Test]
public void Finalize_applies_incident_weights_and_cap()
{
    var aggregate = SuspicionGradeTestFixtures.Aggregate(
        chaseEntries: 4, fruitlessResolutions: 2, captures: 0);
    var result = RoomGradeOperator.FinalizeRoom(
        SuspicionGradeTestFixtures.CompletedBoundary("session", 1, "room", 0f),
        aggregate, SuspicionGradeTestFixtures.CreateStarterConfiguration());

    Assert.That(result.IncidentPenalty, Is.EqualTo(90f));
    Assert.That(result.QualityScore, Is.EqualTo(10f));
    Assert.That(result.Grade, Is.EqualTo(RoomGrade.NeedsImprovement));
}

[Test]
public void Finalize_marks_capture_failed_even_when_numeric_score_is_high()
{
    var result = RoomGradeOperator.FinalizeRoom(
        SuspicionGradeTestFixtures.CaptureBoundary("session", 1, "room"),
        SuspicionGradeTestFixtures.EmptyAggregate("session", 1, "room"),
        SuspicionGradeTestFixtures.CreateStarterConfiguration());

    Assert.That(result.CompletionStatus, Is.EqualTo(GradeCompletionStatus.Failed));
    Assert.That(result.Grade, Is.EqualTo(RoomGrade.None));
}

[Test]
public void Finalize_marks_missing_trace_or_residual_unresolved()
{
    var configuration = SuspicionGradeTestFixtures.CreateStarterConfiguration();
    var aggregate = SuspicionGradeTestFixtures.EmptyAggregate("session", 1, "room");

    var missingTrace = RoomGradeOperator.FinalizeRoom(
        SuspicionGradeTestFixtures.IncompleteTraceBoundary("session", 1, "room"),
        aggregate, configuration);
    var missingResidual = RoomGradeOperator.FinalizeRoom(
        SuspicionGradeTestFixtures.MissingResidualBoundary("session", 1, "room"),
        aggregate, configuration);

    Assert.That(missingTrace.CompletionStatus, Is.EqualTo(GradeCompletionStatus.Unresolved));
    Assert.That(missingResidual.CompletionStatus, Is.EqualTo(GradeCompletionStatus.Unresolved));
    Assert.That(missingTrace.Grade, Is.EqualTo(RoomGrade.None));
    Assert.That(missingResidual.Grade, Is.EqualTo(RoomGrade.None));
}

[TestCase(90f, RoomGrade.S)]
[TestCase(75f, RoomGrade.A)]
[TestCase(60f, RoomGrade.B)]
[TestCase(59f, RoomGrade.NeedsImprovement)]
public void Finalize_uses_exact_grade_boundaries(float score, RoomGrade expectedGrade)
{
    var aggregate = SuspicionGradeTestFixtures.AggregateForScore(score);
    var result = RoomGradeOperator.FinalizeRoom(
        SuspicionGradeTestFixtures.CompletedBoundary("session", 1, "room", 0f),
        aggregate, SuspicionGradeTestFixtures.CreateStarterConfiguration());

    Assert.That(result.Grade, Is.EqualTo(expectedGrade));
}
```

- [ ] **Step 2: Run finalizer tests before implementation.**

Expected: FAIL because finalizer and output types do not exist.

- [ ] **Step 3: Implement incident and residual formulas exactly.**

```text
P_incident = min(cap,
    weight_chase * chase_count
  + weight_fruitless * fruitless_count
  + weight_capture * capture_count)
P_residual = weight_residual * clamp(R_final / R_max, 0, 1)
Q = clamp(100 - P_incident - P_residual, 0, 100)
```

Use the authoritative final residual map. Do not add residual to `A_current` and do not count fruitless resolution again in `P_residual`.

- [ ] **Step 4: Implement grade/status precedence.** Evaluate `Capture`/failed completion first, then missing mandatory trace/residual as `Unresolved`, then completed numeric scoring. Completed scores map `Q >= S` to S, `A <= Q < S` to A, `B <= Q < A` to B, and lower values to Needs Improvement. Failed and Unresolved use `RoomGrade.None`.

- [ ] **Step 5: Implement multi-guard weighted aggregation.** For multiple valid guard aggregates, calculate each guard’s score and weighted-mean by `ExposureWeightByGuard`; reject zero/negative or missing exposure weights as unresolved. For one guard, return that guard’s score directly. Do not use frame time or raw guard count as exposure.

- [ ] **Step 6: Ensure output immutability and contributing IDs.** Copy and ordinal-sort event IDs. Include schema version, component penalties, quality score, completion status, per-guard breakdown, and all contributing incident IDs.

- [ ] **Step 7: Run finalizer tests after implementation.**

Expected: PASS for exact S/A/B thresholds, incident cap, residual clamp, capture precedence, unresolved missing data, output fields, one-guard scoring, weighted multi-guard scoring, and order invariance.

---

## Task 5: Add Typed Event Contracts and Session/Event Bus Wiring

**Files:**
- Modify: `src/AI/SuspicionGrade/SuspicionGradeEvents.cs` (event types were established in Task 3)
- Create: `src/AI/SuspicionGrade/SuspicionGradeSystem.cs`
- Create: `tests/unit/suspicion-grade/SuspicionGradeSystemTests.cs`
- Modify: `src/AI/GuardAISystem.cs`
- Modify: `src/AI/Testing/DecisionTap.cs` only if a focused capture helper is required.

**Dependencies:** Tasks 1–4 and existing `IEventBus`, `SessionBoundaryService`, and `DecisionRecord` contracts.

**Interfaces:** `SuspicionGradeSystem` implements `ISuspicionGradeSystem`; constructor receives `IEventBus`, `SessionBoundaryService`, `SuspicionGradeConfiguration`, `ISuspicionMeterPresenter`, and a diagnostic sink. It must expose no static singleton.

- [ ] **Step 1: Write failing lifecycle and idempotency tests.**

```csharp
[Test]
public void System_accepts_snapshot_and_exposes_read_model_without_mutating_source()
{
    var harness = SuspicionGradeTestFixtures.CreateSystemHarness();
    var snapshot = SuspicionGradeTestFixtures.Snapshot(
        accumulator: 25f, residual: 10f, tChase: 100f,
        entryThreshold: 20f, guardState: "Investigate");

    harness.System.AcceptSnapshot(snapshot);

    var model = harness.Presenter.LastReadModel;
    Assert.That(model.MeterPercent, Is.EqualTo(25));
    Assert.That(model.RCurrent, Is.EqualTo(10f));
}

[Test]
public void System_ignores_old_epoch_after_session_boundary()
{
    var harness = SuspicionGradeTestFixtures.CreateSystemHarness();
    harness.System.AcceptTrace(SuspicionGradeTestFixtures.ChaseEntry(
        "session", 1, "room", "guard-1", "old-entry", "old-event"));
    harness.SessionBoundary.BeginEpoch(2);
    harness.System.AcceptTrace(SuspicionGradeTestFixtures.ChaseEntry(
        "session", 1, "room", "guard-1", "late-old-entry", "late-old-event"));

    var result = harness.System.FinalizeRoom(
        SuspicionGradeTestFixtures.CompletedBoundary("session", 2, "room", 0f));

    Assert.That(result.IncidentPenalty, Is.EqualTo(0f));
    Assert.That(result.ContributingEventIds, Does.Not.Contain("old-event"));
}

[Test]
public void System_finalizes_one_result_for_repeated_boundary_callbacks()
{
    var harness = SuspicionGradeTestFixtures.CreateSystemHarness();
    var boundary = SuspicionGradeTestFixtures.CompletedBoundary("session", 1, "room", 0f);

    var first = harness.System.FinalizeRoom(boundary);
    var second = harness.System.FinalizeRoom(boundary);

    Assert.That(second, Is.SameAs(first));
    Assert.That(harness.FinalizedCount, Is.EqualTo(1));
}
```

- [ ] **Step 2: Run system tests before implementation.**

Expected: FAIL because event contracts, system, harness, and presenter capture do not exist.

- [ ] **Step 3: Implement typed event contracts.** Add immutable `SuspicionSnapshotEvent`, `GradeTraceEvent`, `RoomCompletionBoundaryEvent`, and `RoomGradeFinalizedEvent` wrappers implementing the existing `IEvent`/identity contract. Preserve session and epoch identity in every event. Do not reimplement the Event Bus or bypass its typed delivery/retry behavior.

- [ ] **Step 4: Implement the system adapter.** On a snapshot, calculate and publish the read model to the injected presenter. On a decision record, adapt and pass it to the reducer. On session/epoch boundary, clear current-epoch read models and close the old aggregate. On room boundary, call the finalizer and publish exactly one immutable result.

- [ ] **Step 5: Wire `GuardAISystem`.** Construct the configuration from the registry-backed values, construct the reducer/finalizer/system, subscribe to existing decision/snapshot/boundary events, and dispose/unsubscribe with the same lifecycle pattern used by existing AI services. Do not add a per-frame `Update` loop.

- [ ] **Step 6: Run system tests after implementation.**

Expected: PASS for Event Bus delivery, snapshot projection, stale epoch isolation, reset behavior, repeated-boundary idempotency, and final-result publication.

- [ ] **Step 7: Run the existing AI tests.**

Run:

```text
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform editmode -testResults TestResults/suspicion-grade-existing-ai.xml -quit
```

Expected: existing FSM, Event Bus, Perception, and noise tests remain passing; any failure must be fixed without changing their authoritative formulas or thresholds.

---

## Task 6: Implement the Read-Only Presentation Boundary

**Files:**
- Create: `src/AI/SuspicionGrade/SuspicionMeterPresenter.cs`
- Create or modify: the existing HUD/UI adapter location discovered during Unity scaffold setup; keep the adapter outside the grade domain.
- Create: `tests/unit/suspicion-grade/SuspicionMeterPresenterTests.cs`

**Dependencies:** Tasks 2 and 5; selected Unity UI technology.

**Interfaces:** The presenter consumes only `SuspicionMeterReadModel` and must render unavailable as an explicit unavailable state, not zero.

- [ ] **Step 1: Write failing presenter tests.**

```csharp
[Test]
public void Present_unavailable_model_does_not_render_zero_percent()
{
    var view = new FakeSuspicionMeterView();
    var presenter = new SuspicionMeterPresenter(view);

    presenter.Present(SuspicionGradeTestFixtures.UnavailableReadModel());

    Assert.That(view.State, Is.EqualTo("unavailable"));
    Assert.That(view.PercentText, Is.EqualTo("unavailable"));
}

[Test]
public void Present_shows_meter_percent_region_residual_and_causal_event()
{
    var view = new FakeSuspicionMeterView();
    var presenter = new SuspicionMeterPresenter(view);

    presenter.Present(SuspicionGradeTestFixtures.AvailableReadModel(
        percent: 65, region: SuspicionMeterRegion.Investigate,
        residual: 20f, causalEvent: "resolve-fruitless"));

    Assert.That(view.Percent, Is.EqualTo(65));
    Assert.That(view.Region, Is.EqualTo(SuspicionMeterRegion.Investigate));
    Assert.That(view.Residual, Is.EqualTo(20f));
    Assert.That(view.CausalEvent, Is.EqualTo("resolve-fruitless"));
}
```

- [ ] **Step 2: Run presenter tests before implementation.**

Expected: FAIL because the presenter/view seam does not exist.

- [ ] **Step 3: Implement the view seam.** Define an injected view interface with `ShowUnavailable()`, `SetMeterPercent(int)`, `SetRegion(SuspicionMeterRegion)`, `SetResidual(float)`, and `SetCausalEvent(string)`. The adapter must not infer causes, mutate thresholds, recalculate score, or send gameplay commands.

- [ ] **Step 4: Add UI wiring and accessibility-safe semantics.** Expose region as text/state in addition to color, keep residual visually separate from live fill, and provide a readable unavailable label. Mouse navigation must continue to work under the existing PC/WebGL UI constraints.

- [ ] **Step 5: Run presenter tests and perform a manual UI walkthrough.**

Expected: automated presenter tests PASS. Record the manual walkthrough in `docs/qa/suspicion-grade-validation.md`; do not claim screenshot evidence unless screenshots are actually captured.

---

## Task 7: Full Verification, Acceptance Matrix, and Evidence Report

**Files:**
- Modify: `docs/qa/suspicion-grade-validation.md`
- Modify: `production/session-state/active.md` after each verified milestone, following the project context-management policy.

**Dependencies:** Tasks 1–6.

- [ ] **Step 1: Run the complete edit-mode suite.**

```text
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform editmode -testResults TestResults/suspicion-grade-full.xml -quit
```

Expected: all new Suspicion Grade tests and all existing AI tests pass. If Unity is unavailable, record the exact unavailable command/result and leave runtime evidence unclaimed.

- [ ] **Step 2: Run static authority scans.** Confirm that production HUD, FSM, Perception, reducer, and finalizer code do not introduce duplicate starter tuning values or independent `T_chase`/`T_entry` formulas. Confirm the composition root receives values from the registry/configuration object.

- [ ] **Step 3: Run YAML validation.** Use the repository’s available YAML validator to check syntax and duplicate keys. If no duplicate-key-aware parser is installed, record `NOT PERFORMED — no suitable parser installed`; do not convert that limitation into a pass.

- [ ] **Step 4: Verify SG-AC1 through SG-AC14.** Use this matrix in the validation report:

| Criterion | Evidence required |
|---|---|
| SG-AC1 | Calculator tests for 0%, 100%, overshoot clamp, and `[0,100]`. |
| SG-AC2 | Configuration/source test proves injected registry `T_chase` is used and no threshold is mutated. |
| SG-AC3 | Reducer duplicate `Chase-entry` test keyed by `entry_id`. |
| SG-AC4 | Fruitless-only resolution test and exclusion of every other cause. |
| SG-AC5 | Clean noise, relay, near miss, and LOS-break tests with zero direct incident penalty. |
| SG-AC6 | Capture and incomplete-failed-boundary tests return `Failed`, never S/A/B. |
| SG-AC7 | Residual zero/normal/above-maximum tests prove clamp and no fruitless recount. |
| SG-AC8 | Forward/reverse delivery tests compare score, grade, breakdown, and event IDs. |
| SG-AC9 | Stale epoch test proves no current aggregate mutation. |
| SG-AC10 | Retry/duplicate test proves no additional penalty. |
| SG-AC11 | Missing accumulator returns unavailable; missing trace/residual returns unresolved. |
| SG-AC12 | Output contract test checks score, grade, version, status, breakdown, and IDs. |
| SG-AC13 | Registry/configuration scan proves tuning is not embedded in HUD/FSM/Perception. |
| SG-AC14 | NUnit suite covers meter boundaries, thresholds, idempotency, stale epochs, failed completion, missing data, and order invariance. |

- [ ] **Step 5: Verify performance shape.** Confirm the system has no frame polling, no per-frame allocations, and no AI-path work. If a Unity profiler capture is available, record it; otherwise mark profiling as unavailable rather than claiming the 2 ms budget is proven.

- [ ] **Step 6: Update session state.** Record files changed, test command/results, evidence limitations, and the next user decision. Do not change Player Noise from In Review and do not create an approval record for unrelated work.

- [ ] **Step 7: Stop before commit.** Present the completed diff and validation report to the user. Commit/push only after an explicit user instruction.

---

## Test-First Execution Order

1. Unity/A2 prerequisite and registry/configuration validation.
2. Pure meter model/calculator.
3. Immutable trace adapter and reducer.
4. Room finalizer and grade mapping.
5. Event Bus/session lifecycle system.
6. Read-only presenter/UI adapter.
7. Full suite, static authority scan, YAML validation, and evidence report.

Each task must complete its failing-test, implementation, passing-test, and review checkpoint before the next task begins. No later task may compensate for missing data by substituting zero.

## Acceptance Gate

The feature is implementation-complete only when all SG-AC1–SG-AC14 have recorded passing automated evidence or an explicitly recorded unavailable/non-claim state where the environment prevents execution. A missing runtime environment is not a reason to weaken the contracts, hardcode fallback tuning, or mark unexecuted evidence as passing.
