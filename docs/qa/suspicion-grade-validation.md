# Suspicion Meter / Grade — Presenter Validation

**Task:** Ledger Task #9 — approved Suspicion Meter presenter implementation
**Specification:** `docs/superpowers/specs/2026-09-01-suspicion-grade-design.md`
**Implementation plan:** `docs/superpowers/plans/2026-09-01-suspicion-grade-implementation-plan.md`
**Validation date:** 2026-09-02
**Scope:** Read-only presenter seam and focused tests only

## 1. Authorized files

| File | Purpose |
|---|---|
| `src/AI/SuspicionGrade/SuspicionMeterPresenter.cs` | Injected view seam and unavailable-safe presenter. |
| `tests/unit/suspicion-grade/SuspicionMeterPresenterTests.cs` | Focused presenter contract and sink-isolation tests. |
| `docs/qa/suspicion-grade-validation.md` | Presenter validation evidence and limitations. |

No Event Bus, calculator, reducer, finalizer, `GuardAISystem`, perception, FSM, or
unrelated UI/gameplay file was changed.

## 2. Contract coverage

- Null or unavailable read-model input calls `ShowUnavailable()` and never forwards
  a fabricated `0%` value.
- Available read-model input forwards the meter percentage, semantic region,
  separate residual value, and latest causal event.
- The presenter has no frame polling, score calculation, threshold mutation, gameplay
  command path, or singleton state.
- A null view is a safe no-op.
- Exceptions from each individual view operation are isolated so a throwing
  presentation sink cannot escape into gameplay or suppress later presentation
  operations.
- Repeated presentation of the same immutable model uses a deterministic operation
  order.

## 3. Verification evidence

### Focused external compilation/tests

**Command:**

```text
dotnet msbuild C:\Users\QUY\AppData\Local\Temp\ww-task5-compile\ww-task9-tests-full.csproj --target:Build --property:Configuration=Release --property:RestoreIgnoreFailedSources=true --nologo
dotnet msbuild C:\Users\QUY\AppData\Local\Temp\ww-task5-compile\ww-task9-runner-full.csproj --target:Build --property:Configuration=Release --property:RestoreIgnoreFailedSources=true --nologo
dotnet exec C:\Users\QUY\AppData\Local\Temp\ww-task5-compile\bin\Release\net8.0\ww-task9-runner-full.dll
```

**Result:** PASS — focused production/test compilation succeeded with 0 reported
errors or warnings; all 6 presenter tests passed (`RESULT 6/6`). The temporary
harness initially omitted the existing `RoomGradeOperator.cs` dependency of the
already-established event contracts; after adding that existing source outside the
repository, the corrected harness passed.

The repository itself has no `.csproj`/`.sln` or Unity test project.

### Unity Test Framework

**Command:**

```text
C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform editmode -testFilter SuspicionMeterPresenterTests -testResults C:\Users\QUY\AppData\Local\Temp\ww-task5-compile\suspicion-meter-presenter.xml -quit
```

**Result:** NOT RUN — Unity 6000.3.17f1 is installed, but it aborted before test
discovery with `Couldn't set project path` because this checkout lacks the Unity
project scaffold, including `Packages/manifest.json` and
`ProjectSettings/ProjectVersion.txt`. No Unity EditMode, runtime, scene, or
target-hardware evidence is claimed.

### Manual UI walkthrough

**Status:** Not performed. No Unity HUD adapter or scene exists in this checkout, so
there is no visual screenshot or runtime UI evidence to claim. The presenter seam is
ready for the later UGUI adapter authorized by the vertical-slice plan.

### Whitespace

**Command:**

```text
git diff --check
```

**Status:** PASS — no whitespace errors reported.

## 4. Remaining limitations

- The view seam is intentionally engine-agnostic because the approved plan defers
  the concrete UGUI adapter until the Unity scaffold is available.
- Accessibility and mouse-navigation behavior require validation in the eventual HUD
  adapter and cannot be proven by this pure presenter test.
- No commit or push was performed.
