# Player Noise — Runtime Verification Note

Date: 2026-09-09  
Scope: Stream A — bounded runtime and deterministic verification  
Status: **PARTIAL / NOT A UNITY ACCEPTANCE RESULT**

## Scope and ownership

This note records only the runtime-lane checks for the bounded Player Noise implementation. No design approval, systems-index update, review-log update, commit, or push was performed.

The runtime lane touched only:

- `src/AI/Core/PhysicsQueryProfile.cs`
- `src/AI/Testing/BurstAdmissionVerificationSuite.cs`
- this QA note

## Changes verified by inspection

- `BurstThrowRequest` admission rejects non-stationary locomotion, unsupported stance, and positive resolved planar speed before overlap queries, handle allocation, or resource spend.
- `BurstTerminalRecord` preserves `SurfaceContactPosition` separately from the pushed-out `TerminalPosition`.
- `ClosestContactResult.FromSurfaceContact` provides a deterministic injected-query factory for fixture tests without invoking Unity physics.
- `FSMVerificationSuite` covers Investigate open/close, in-place Investigate → Chase promotion, entry continuity, suppression receipt emission, corroboration saturation, epoch barriers, and phase ordering.
- `BurstAdmissionVerificationSuite` now contains deterministic coverage for Walk, Run, unsupported stance, positive planar speed, pre-overlap/pre-spend rejection, and surface-contact versus published-center separation.

## Checks executed

| Check | Result | Evidence / limitation |
|---|---|---|
| `git diff --check -- src/AI` | PASS | No runtime-lane whitespace errors reported. |
| Repository `dotnet build --nologo` | BLOCKED | The checkout has no `.sln` or `.csproj`; MSBuild returned `MSB1003`. |
| Repository `dotnet test --nologo --no-restore` | BLOCKED | The checkout has no `.sln` or `.csproj`; MSBuild returned `MSB1003`. |
| Unity EditMode / NUnit execution | UNAVAILABLE | No Unity project scaffold or Unity test runner is present in this checkout. |
| Standalone C# harness execution | NOT EXECUTED | Temporary harness creation was blocked by the execution classifier; no result is claimed. |

## Evidence boundary

This note does **not** claim:

- Unity compilation or EditMode acceptance;
- production `PhysicsScene` acceptance;
- authoritative Player Controller/Input System producer acceptance;
- audio middleware, DSP, limiter, or onset evidence;
- NavMesh or route capture;
- WebGL performance or target-hardware profiling;
- full runtime integration or release readiness.

The source/test additions are therefore verified statically and by review only. The new NUnit tests remain pending actual Unity test-runner execution.

## Handoff

- Changed-file list is limited to the two runtime files and this note.
- No unresolved runtime semantic contradiction was silently repaired outside Stream A ownership.
- The final Player Noise status remains **In Review** pending the full design re-review and unavailable engine/platform evidence.
