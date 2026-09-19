# Player Noise — Runtime Verification Note

Date: 2026-09-14
Scope: Stream A — bounded runtime and cross-file contract verification
Status: **PARTIAL / EDITMODE TESTS PASS; NOT A UNITY RUNTIME ACCEPTANCE RESULT**

## Scope and ownership

This note records only the runtime-lane checks for the bounded Player Noise implementation. No design approval, systems-index update, review-log update, commit, or push was performed.

The bounded runtime revision touched:

- `src/AI/Core/NoiseRejectionCodes.cs`
- `src/AI/Core/NoiseSourceOrdering.cs`
- `src/AI/Core/NoiseSourceRecord.cs`
- `src/AI/Core/BurstRuntimeConfiguration.cs`
- `src/AI/Core/PhysicsQueryProfile.cs`
- `src/AI/Core/NoiseRuntimeConfiguration.cs`
- `src/AI/Core/VirtualTickClock.cs`
- `src/AI/FSM/GuardFSM.cs`
- `src/AI/FSM/States/ChaseState.cs`
- `src/AI/FSM/States/InvestigateState.cs`
- `src/AI/FSM/SuppressionVisibilityPolicy.cs`
- `src/AI/Perception/BurstSimulationService.cs`
- `src/AI/Perception/NoiseEmitter.cs`
- `src/AI/Perception/PerceptionHearingService.cs`
- `src/AI/Perception/R12Schema.cs`
- `src/AI/Testing/BurstAdmissionVerificationSuite.cs`
- `src/AI/Testing/FSMVerificationSuite.cs`
- `src/AI/Testing/FsmVerificationHarness.cs`
- `src/AI/Testing/NoiseContractVerificationSuite.cs`
- `src/AI/Testing/PerceptionHearingVerificationSuite.cs`
- `src/AI/GuardAISystem.cs`
- this QA note

## Changes verified by inspection

- `BurstThrowRequest` admission rejects non-stationary locomotion, unsupported stance, and positive resolved planar speed before overlap queries, handle allocation, or resource spend, using stable rejection families plus diagnostic subcodes.
- Typed initial-overlap results distinguish `Clear`, `Blocked`, and fail-closed `Incomplete`; the production profile is bound to an injected `PhysicsScene`.
- Accepted Burst snapshots carry canonical identity, launch geometry, resolved velocity, pickup identity, and geometry variant; flight startup uses the accepted resolved velocity.
- `BurstTerminalRecord` preserves `SurfaceContactPosition` separately from the pushed-out `TerminalPosition` and records the authoritative callback tick/time.
- `FSMVerificationSuite` covers Investigate open/close, in-place Investigate → Chase promotion, entry continuity, stale-position handoff, suppression publisher/visibility/rate-limit behavior, retry-safe receipt staging, retry exhaustion, boundary discard, corroboration saturation, epoch barriers, and phase ordering.
- `NoiseContractVerificationSuite` covers canonical Movement/Burst source IDs at both factory and constructor boundaries, numeric namespaced ordering, safe legacy Burst adaptation, source-kind publication time, raw-to-relay provenance parity, emitter Retry/Pending/Published transitions, queue-overflow rejection, and same-session boundary invalidation.
- `BurstAdmissionVerificationSuite` covers Walk, Run, unsupported stance, positive planar speed, incomplete and untyped overlap adapters, snapshot authority, pre-overlap/pre-spend rejection, fixed-tick publication, malformed-contact fail-closed cancellation without timeout publication, and surface-contact versus published-center separation.
- `PerceptionHearingVerificationSuite` covers not-before-`t_publish` retention, exact-boundary eligibility, deadline expiry, vertical hard-cutoff exclusion, derived-eye overflow rejection, and malformed raw geometry rejection.
- The latest hardening pass adds explicit session/attempt envelopes for authoritative Movement submissions, stale-envelope rejection, timeout-ratio overflow rejection, non-finite snapshot geometry guards, canonical clock cadence/max-frame configuration, typed PhysicsScene content proofs, same-tick crouched-stand admission coverage, and zero-planar-aim facing fallback.
- The cross-file revision adds the typed `noise-reanchor` outcome, separates initial commit/re-anchor/suppression micro-tell presentation, partitions acceptance gates, propagates Burst terminal/source causality and serializable spies, and adds route patrol-window, negative-case, and pickup telemetry schema parity.
- The latest authority pass adds typed suppression visibility query provenance, a post-FSM decision drain, durable new-envelope stale-close staging, retry-safe relay outcome reservations, canonical liveness position-source vocabulary, and non-noise episode-open timestamps. These changes remain static-source verified only.
- The follow-up hardening adds explicit DeathCancelled terminal-publication timing without a hearing `t_publish`, preserves zero-valued authoritative episode timestamps, accepts qualifying noise corroboration for non-noise episodes, consumes reachability facts in Investigate timing, and uses the sampled HideSpot interior proxy consistently for catch backstops. These changes remain static-source verified only.

## Checks executed

| Check | Result | Evidence / limitation |
|---|---|---|
| Focused source `git diff --check` | PASS | The focused C# source/test files pass with `core.whitespace=cr-at-eol`. |
| Repository-wide `git diff --check` | NOT CLEAN | The current mixed LF/CRLF working tree returns CRLF trailing-whitespace diagnostics in changed Markdown; no source defect is inferred and no normalization was performed. |
| Repository `dotnet build --nologo` | BLOCKED | The checkout has no `.sln` or `.csproj`; MSBuild returned `MSB1003`. |
| Repository `dotnet test --nologo --no-restore` | BLOCKED | The checkout has no `.sln` or `.csproj`; MSBuild returned `MSB1003`. |
| Unity EditMode / NUnit execution | PASS | Unity 6000.3.17f1 EditMode runner executed 74/74 tests: 74 passed, 0 failed, 0 inconclusive, 0 skipped. Result: `C:\Users\QUY\AppData\LocalLow\DefaultCompany\Whisper Ward\TestResults.xml`. |
| Standalone C# harness execution | NOT EXECUTED | Temporary harness creation was blocked by the execution classifier; no result is claimed. |
| Markdown fence / required-section scan | PASS | All touched Markdown files have balanced fences; `player-noise.md` contains all eight required design sections. |
| Focused static source scan | PASS (static only) | The focused C# files have balanced braces and the Burst terminal constructor/call-count scan remains consistent. This is not a compiler result. |
| YAML duplicate-key-aware parse | UNAVAILABLE | No approved YAML parser is installed in the checkout; no parser result is claimed. |

## Evidence boundary

This note claims only the recorded Unity EditMode compilation and deterministic test execution. It does **not** claim:

- production `PhysicsScene` acceptance;
- authoritative Player Controller/Input System producer acceptance;
- audio middleware, DSP, limiter, or onset evidence;
- NavMesh or route capture;
- WebGL performance or target-hardware profiling;
- full runtime integration or release readiness.

The source/test additions are verified by the recorded Unity EditMode run plus static inspection. This remains a test-runner result only, not production scene, route, platform, or release acceptance.

## Terminal-authority and FSM continuity pass (2026-09-13, static only)

The follow-up pass resolved the remaining verified documentation blockers and the lead-programmer diff findings; all changes remain static-source verified only:

- **Collision `terminal_publication_time` reconciled across files.** Runtime (fixed-tick boundary `(start_tick + winning_tick) × dt_max`), tests, GDD, and registry now agree: the sub-tick interpolated `contact_event_time` is a diagnostic field and never the collision publication time. Updated `design/gdd/player-noise.md` (two-timestamp contract and the `contact_event_time` feed sentence), `design/registry/entities.yaml` (Burst terminal event contract and `burst_source_timestamp_policy` note).
- **`BoundaryCancelled` added to the registry vocabulary.** `design/registry/entities.yaml` `trace_event_schema` Burst terminal now lists `Collision|Timeout|DeathCancelled|BoundaryCancelled` with fail-closed semantics (no publication time, no `t_publish`, no fact ID, no hearing deadline), matching the GDD and fixture spec.
- **Route `failed_death_01` clarified.** `design/levels/mvp-burst-route-fixture.md` now states `terminal_publication_time` on this case is the death-cancellation lifecycle/trace ordering value only — never a fact publication time — consistent with `fact_id: null`, `noise_count: 0`, no `t_publish`, no relay, no hearing deadline.
- **GDD revision header/status dates reconciled** to 2026-09-13 with the ninth terminal-authority normalization revision entry.
- **Registry `liveness_fact_lifecycle` Chase-end cause vocabulary synced to runtime**: `[capture, duration_cap, giveup, resight_cap, occupancy-exit, sight-committed, abandoned]` (previously `[time, sight-committed, capture, abandoned]` — a genuine cross-file contradiction with the runtime's `giveup`/`resight_cap`/`occupancy-exit` causes); stale close remains `cause=stale` under `lifecycle_closure`.
- **`NoiseSourceRecord.TPublish` fail-closed adapter guard**: a Burst record reaching the getter without a terminal publication time (adapter bypassing constructor validation) now throws `InvalidOperationException` with the `noise-source-burst-terminal-publication-time-missing` diagnostic instead of an unguarded `.Value` throw.
- **Suppression receipt provenance without a policy**: an eligible relay with no injected visibility policy now records `not-performed:policy-unavailable` provenance instead of empty strings, and the redundant `eligible &&` condition was removed. No micro-tell is emitted on that path.
- **FSM promotion/continuity fixes verified by an independent read-only reviewer (PASS, all eight points)**: sub-threshold retarget retains the live `entry_id`; direct Patrol→Chase sets `currentEntryId`; Chase opener timestamps are zero-safe (`float? openerTPublish`); stale close is retained exactly once across `BeginEpoch` with the old envelope; witnessed/threshold promotions are in-place and ordered; canonical position-source vocabulary; suppressed-receipt provenance; constructor/call-site consistency. 21 focused tests are authored in `FSMVerificationSuite.cs`; none are executed (no runner).
- **Remaining advisory items (open, non-blocking)**: no authored Chase-opener zero-preservation test yet (A1 — may be landing with the implementer's current pass); Chase-end cause vocabulary is now reconciled (A3 resolved by the registry edit above).

No Unity compilation, NUnit execution, harness run, PhysicsScene capture, or YAML parse was performed for this pass; `git diff --check` and brace-scan status for the newest edits are recorded in the section below.

## Fresh review handoff (partial panel, 2026-09-13)

The available independent reviews found no new runtime contradiction in the scoped Perception or Burst/Physics contracts, but they did not produce an approval verdict:

- Perception cross-file audit: scoped documentation contract **PASS**; runtime and platform evidence remain unavailable.
- Burst/Physics/route audit: documentation contract **PASS**; route, PhysicsScene, NavMesh, Unity, and AC21 evidence remain unavailable.
- FSM audit (initial partial panel): **NEEDS REVISION** because AC-FSM-EX still said every Chase entry emits `op=open` without excluding Investigate-to-Chase promotion, which must emit `op=promote` with the existing `entry_id`; the criterion and runtime continuity were corrected afterward and independently re-verified **PASS** (see the terminal-authority/FSM continuity pass above).
- Audio/performance audit: **NEEDS REVISION** because runtime/DSP/WebGL/hardware evidence remains unavailable. The ADR vocabulary alias and AC21 whole-frame decomposition binding were reconciled afterward to the canonical `virtual_dsp_onset` field and the explicit render + Player Noise + HideSpot phase 2 + FSM catch-gate + residual main-thread equation; no runtime or DSP pass is inferred.
- The independent acceptance/testability review did not complete and no verdict is inferred from its absence.

These results are review evidence only; they do not claim Unity, NUnit, PhysicsScene, route, NavMesh, audio/DSP, WebGL, target-hardware, or YAML-parser execution.

## Capacity and snapshot hardening pass (2026-09-13, static only)

The latest bounded revision adds registry-backed capacity validation and closes a
concrete deferred-work loss path. These results remain static-source verified only:

- `NoiseRuntimeConfiguration` now names the registered raw-fact (`128`) and
  retained-relay (`512`) defaults, enforces the documented safe ranges, and locks
  the MVP guard/fact/pair/boundary/retry values to their registry values. Target
  profiles retain the documented safe-range flexibility.
- `SessionEventBus` defaults now resolve to the registered capacity (`256`) and
  retry limit (`3`), and exposes its configured retry limit for diagnostics/tests.
- The compatibility Perception constructor now uses the registered raw-fact and
  relay defaults. Configuration-backed and compatibility paths reject an
  unregistered vertical hearing cutoff.
- Perception retains the complete ordered guard snapshot in each pending fact;
  the per-boundary guard cap advances a cursor over deferred guards rather than
  truncating the captured snapshot. Focused coverage proves a 31st guard is
  evaluated from the original snapshot even after the provider changes.
- The relay queue is now represented by an explicit registry entry and runtime
  constants with the documented `[64, 1024]` safe range.
- Route fixture `failed_gate_spends.terminal_timing` records now serialize the
  authoritative `winning_tick_index` (`REQUIRED_RUNTIME_WINNING_TICK_INDEX` until
  captured; `null` for death-cancelled records), and the registry note binds it to
  the E-FX-10 Collision/Timeout adjudicator, removing the nested-schema omission.
- **Fixture timing defect found by static execution trace and corrected.** The
  hearing fixture's `AdvanceTo(virtualTime)` helper advanced a *relative* tick
  count computed from the *absolute* target, so successive calls drifted the
  clock (0.2 → 0.6 → 1.2). Both the original 31-guard deferral test
  (`t_publish 0`, deadline exceeded at its second boundary) and a first re-edit
  (`t_publish 0.4` with the still-buggy helper) would have failed at actual
  execution. The helper now advances to absolute virtual time from the remaining
  delta, and the 31-guard test publishes at `t_publish 0.4` so the deferred 31st
  guard is evaluated at the boundary exactly on its hearing deadline (within the
  1e-6 tolerance). The other multi-advance tests were re-traced under absolute
  semantics and remain valid. No test execution is claimed — no runner exists.
- The initial-overlap result buffer is now registry-owned as
  `initial_overlap_result_capacity=64` with safe range `[16, 128]`; profile
  construction validates and fixes the injected capacity for the profile
  lifetime, exposes it for diagnostics, and the focused suite covers the
  registered default plus below/above-range rejection. Saturation remains
  fail-closed before Burst allocation/spend.
- Focused C# brace and authority-symbol scans pass. The composition retry gate
  now preserves the MVP lock at `3` while allowing Target values across the
  registered `[1, 8]` range; focused regression coverage is authored but not
  executed because no Unity/NUnit runner exists. A repository `dotnet build`
  remains blocked by the absence of a solution/project (`MSB1003`).

No Unity compilation, NUnit execution, production PhysicsScene capture, route or
NavMesh capture, audio/DSP evidence, WebGL or target-hardware profiling, or
YAML duplicate-key-aware parse was performed.

## Unity EditMode execution pass (2026-09-14)

The local Unity project and Test Framework runner were available for this pass. The exact command was:

```text
"C:/Program Files/Unity/Hub/Editor/6000.3.17f1/Editor/Unity.exe" -batchmode -projectPath "C:/Users/QUY/Documents/GitHub/Whisper Ward" -runTests -testPlatform editmode -testResults "Temp/player-noise-editmode-results.xml" -logFile "Temp/player-noise-editmode.log"
```

Unity compiled the `WhisperWard.AI` runtime assembly and `WhisperWard.AI.Tests` test assembly, then executed the complete EditMode suite. The reliable result file reported:

- total: `74`
- passed: `74`
- failed: `0`
- inconclusive: `0`
- skipped: `0`
- result: `Passed`

The run included the focused Burst admission, FSM, NoiseContract, and PerceptionHearing suites. This evidence does not establish production scene/PhysicsScene, route/NavMesh, audio/DSP, WebGL, target-hardware, or release acceptance.

## Handoff

- The changed-file list includes the bounded runtime contracts, focused tests, canonical documentation, and Unity synchronization/test-runner support; no approval/tracking files were changed.
- Unity compilation and EditMode/NUnit execution are claimed only for the exact local run recorded above. No standalone harness result or production PhysicsScene capture is claimed.
- The final Player Noise status remains **In Review**; fresh review attempts did not return a usable verdict, so no approval is inferred from timeout or missing output.
- The retrievable completed scoped audit identified undocumented public API declarations in `GuardFSM.cs` and `R12Schema.cs`. XML summaries were added across both public surfaces; the focused public-documentation scan now passes. This is source/documentation verification only and not a compiler result.
