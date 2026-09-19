# Player Noise Contract Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development and review each task before accepting it.

**Goal:** Finish the unresolved FSM verification work and refresh the final handoff without changing the Player Noise GDD under active review.

**Architecture:** Execute only in the existing isolated worktree codex/player-noise-contract-review-safe. Use the current main checkout as the baseline for already-dirty files. Repair the test harness against the real session bus, virtual clock, phase coordinator, and GuardFSM injection seam; keep runtime evidence fail-closed.

**Tech Stack:** Unity 6 LTS 6000.3.17f1, C#, NUnit/Unity Test Framework, IEventBus, SessionEventBus, IVirtualTickClock, VirtualTickClockService, SessionPhaseCoordinator, GuardFSM.

**Spec:** docs/superpowers/plans/2026-08-30-player-noise-contract-review-safe-plan.md, docs/qa/contract-review-handoff-2026-08-30.md, and the Task 5 review package.

## Global Constraints

- Never modify design/gdd/player-noise.md or design/gdd/reviews/player-noise-review-log.md.
- Do not modify production EventBus, IEvent, VirtualTickClock, SessionPhaseCoordinator, GuardFSM, GuardAISystem, or entities.yaml.
- No commit, merge, push, reset, checkout, or destructive cleanup.
- Identity remains producer-owned string flight_handle_id/source_event_id, emitter-owned ulong fact_id, and Perception-owned entry_id.
- Runtime and target-hardware evidence remain UNCAPTURED or PENDING_OQ6 until a real runner/capture exists.
- Existing user changes in the main checkout are baseline data and must not be overwritten.

---

### Task 1: Align the FSM harness with real injected APIs

**Files:**
- Modify: src/AI/Testing/FsmVerificationHarness.cs
- Modify: src/AI/Testing/FSMVerificationSuite.cs
- Read-only: src/AI/Core/EventBus.cs, src/AI/Core/IEvent.cs, src/AI/Core/VirtualTickClock.cs, src/AI/Core/SessionPhaseCoordinator.cs, src/AI/FSM/GuardFSM.cs, src/AI/Perception/R12Schema.cs
- Report: .superpowers/sdd/2026-08-30-player-noise-contract-review-safe-plan/task-5-report.md

**Required result:**
- Remove the nested fake SessionEventBus, fake FactEnvelope queue, and reflection-driven tick/event path.
- Construct the real Core.SessionEventBus, VirtualTickClockService, and SessionPhaseCoordinator(bus, clock); bind and unbind the coordinator in the harness lifecycle.
- Expose Bus, Clock, and Coordinator aliases, plus descriptive aliases only when useful.
- CreateFsm(sessionId, attemptEpoch) must call GuardFSM.ConfigureWithPhaseCoordinator(bus, clock, sessionId, attemptEpoch, coordinator).
- Publish helpers may set a mutable SensingFact envelope and call real bus.Publish; they may not synthesize LivenessFact, fact_id, or entry_id.
- Advance(seconds) must call only the injected clock and must not use EventBus.Default or VirtualTickClock.Instance.

**Checks:**
- Search the two test files for EventBus.Default, VirtualTickClock.Instance, nested SessionEventBus, GetMethod, and reflection. Expected: no primary-path singleton or reflection use and a direct ConfigureWithPhaseCoordinator call.
- Compare every referenced type/signature with the current main checkout before editing.

---

### Task 2: Correct S_DIFF and re-anchor formula coverage

**Files:**
- Modify: src/AI/Testing/FsmVerificationHarness.cs
- Modify: src/AI/Testing/FSMVerificationSuite.cs
- Read-only: design/registry/entities.yaml and the approved consolidation plan
- Report: .superpowers/sdd/2026-08-30-player-noise-contract-review-safe-plan/task-5-report.md

**Required result:**
- Add ComputeRawReanchorSpeedRatio(actual, investigate) returning the raw positive quotient.
- Keep a separate canonical helper applying the registered min(1.0, raw) clamp and the explicit 1.0 fallback for zero or invalid speed.
- Assert raw ratios equal 1.0, 1.2, and 0.8 for equal, faster, and slower inputs.
- Assert canonical ratios equal 1.0, 1.0, and 0.8 for the same cases; do not claim a clamped faster ratio is above 1.0.
- Use the registered expression max(base*S_DIFF*(1+k*R/Rmax), base*S_DIFF+extension). For base 4.0, S_DIFF 0.8, k 0.5, Rmax 1.0, extension 1.0, assert 4.2 at residual 0 and 4.8 at residual 1.
- Record the plan-versus-registry wording discrepancy in the report; do not edit the registry.
- Update the static-audit handoff to show SCA-001 is exercised by non-unit tests.

---

### Task 3: Observe real typed liveness facts

**Files:**
- Modify: src/AI/Testing/FsmVerificationHarness.cs
- Modify: src/AI/Testing/FSMVerificationSuite.cs
- Read-only: src/AI/FSM/GuardFSM.cs, src/AI/Perception/R12Schema.cs, src/AI/Core/EventBus.cs

**Required result:**
- Subscribe to real LivenessFact events on the injected bus and store immutable observed fields: session, epoch, entry, operation, tier, cause, source timestamp, and publisher.
- Assert normal open and close using observed bus records. If a runtime path does not publish a requested record, mark it UNCAPTURED instead of fabricating a pass.
- Exercise epoch transition through the real bus/clock/FSM seam and assert any stale close from observed records. A test-only diagnostic list must be named diagnostic and never presented as publication evidence.
- Assert same live entry_id across re-anchor facts and no second open identity.

---

### Task 4: Verify phase and pause behavior

**Files:**
- Modify: src/AI/Testing/FSMVerificationSuite.cs
- Modify: src/AI/Testing/FsmVerificationHarness.cs
- Read-only: src/AI/Core/SessionPhaseCoordinator.cs, src/AI/Core/VirtualTickClock.cs, src/AI/Core/EventBus.cs

**Required result:**
- Record only phases observable through real coordinator hooks/events and assert GameplayIngress -> Hearing -> FsmDecision -> Presentation when all four are observable.
- If Presentation has no public observation hook, record a testability gap rather than manufacturing a phase event.
- Pause the injected clock, advance a finite interval, and assert no tick/decision/backlog release; resume for one registered boundary and assert one real processing step.
- Begin a new bus epoch and assert old facts cannot produce a decision or relay.

---

### Task 5: Verification and review gate

**Files:**
- Modify: .superpowers/sdd/2026-08-30-player-noise-contract-review-safe-plan/task-5-report.md
- Create: .superpowers/sdd/2026-08-30-player-noise-contract-review-safe-plan/task-5-r1-review-package.md

**Checks:**
- Run git diff --check.
- Search the test files for singleton paths, raw/canonical ratio helpers, ConfigureWithPhaseCoordinator, and LivenessFact.
- Run focused Unity filters for FSMVerificationSuite and BurstSimulationServiceVerificationSuite if a project/runner exists. Otherwise record the exact missing-entry-point result and retain UNCAPTURED/PENDING_OQ6.
- An independent reviewer compares the actual diff against current main, checks protected and production files are untouched, and writes the review package.
- Any Critical or Important finding reopens Tasks 1–4; runtime evidence limitations alone are not a PASS.

---

### Task 6: Refresh the final handoff

**Files:**
- Modify: docs/qa/contract-review-handoff-2026-08-30.md
- Read: all Task 1–5 reports and review packages
- Read-only: design/gdd/player-noise.md and its review log

**Required result:**
- Keep Tasks 0–4 as reviewed PASS.
- Mark Task 5 PASS only after its scoped review passes; otherwise keep CHANGES_REQUIRED with exact open findings.
- Run protected-file status and identity/stale-term scans.
- Record the isolated worktree, changed files, runtime evidence limits, and any artifact accidentally created in the original checkout.
- Deliver uncommitted; do not merge or delete user data.
