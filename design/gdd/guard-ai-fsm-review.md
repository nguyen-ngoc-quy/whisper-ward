## Design Review: Guard AI FSM
Specialists consulted: AI Programmer, Systems Designer, QA Lead, Game Designer
Re-review: No — first review

### Completeness: [8/8 sections present]
All required sections present: Overview, Player Fantasy, Detailed Rules, Formulas, Edge Cases, Dependencies, Tuning Knobs, Acceptance Criteria

### Dependency Graph
- ✓ Perception.md — exists (Approved)
- ✗ NavMesh / Pathfinding.md — NOT FOUND (file does not exist yet)
- ✗ Event / Messaging bus.md — NOT FOUND (file does not exist yet)
- ✗ Player Third-Person Controller.md — NOT FOUND (file does not exist yet)
- ✗ Physics & collision config.md — NOT FOUND (file does not exist yet)
- ✗ Level / Content.md — NOT FOUND (file does not exist yet)
- ✗ Suspicion Meter / Grade.md — NOT FOUND (file does not exist yet)
- ✗ Suspicion Attribution telemetry.md — NOT FOUND (file does not exist yet)
- ✗ Alert propagation.md — NOT FOUND (file does not exist yet)
- ✗ VFX / telegraph layer.md — NOT FOUND (file does not exist yet)
- ✗ HUD / UI.md — NOT FOUND (file does not exist yet)

### Required Before Implementation
1. [AI Programmer] Undefined critical variables: A, T_chase, and unified fact are referenced but not defined, making Investigate→Chase transition and give-up mechanics impossible to implement.
2. [AI Programmer] Phantom variable: GuardGoalMode is referenced in the catch contract but never defined as an FSM-owned variable.
3. [AI Programmer] NavMesh dependency mismatch: FSM depends on specific NavMesh behaviors (PathPartial status handling, off-mesh logic, agent-parameterized CalculatePath) but NavMesh GDD is undesigned.
4. [AI Programmer] Performance violation: Catch contract calls CalculatePath every frame, violating the 2ms/frame budget for pathfinding operations.
5. [AI Programmer] Timer interaction ambiguity: Unclear interaction between catch timer and give-up clock on Chase exit - whether catch timer persists, resets, or freezes.
6. [AI Programmer] Suppression rule ambiguity: Episode/escalation tier and pinned order are undefined, making the suppression rule (one escalation decision per tier per episode) non-deterministic.
7. [Game Designer] Fantasy-breaking timing: Give-up clock base value of 4.0s is too short, undermining the "searching suspicion" tension core to the player fantasy.
8. [Game Designer] Insufficient post-Chase behavior: Post-Chase sweep duration of 2.0s is too brief to deliver the "none the wiser" satisfaction fantasy.
9. [Game Designer] Carve-out capture fantasy risk: Spot-front carve-out capture mechanic risks breaking the three-temperaments fantasy without clearer telegraphing of guard intent.
10. [QA Lead] Critical timing contradiction: AC-FSM-5 specifies 12s give-up suspension while AC-FSM-9 specifies 2s post-Chase sweep - these are mutually exclusive.
11. [QA Lead] Missing test infrastructure: 6 critical components missing from H.0 harness preventing proper test execution: timer capture, state transition logger, decision-record classifier, path telemetry aggregator, scan-state monitor, give-up/suspension detector.
12. [QA Lead] Unambiguous ACs: 7/10 acceptance criteria have ambiguous pass/fail criteria that cannot be reliably tested.
13. [QA Lead] Untestable ACs: 8/10 acceptance criteria require infrastructure not present in H.0, making them unautomatable.

### Recommended Revisions
1. [Game Designer] Increase n_resight_cap from 2 to 3 for more persistent pursuit feel.
2. [Game Designer] Add distinct visual/audio cues to differentiate Investigate and Chase states.
3. [Game Designer] Verify τ_res (residual decay constant) alignment with 45-60s wariness linger timeframe.
4. [Systems Designer] Define t_sweep_base as "bounded" with specific limits.
5. [Systems Designer] Verify V_patrol < V_investigate ≤ V_run joint constraint at corner cases.
6. [Systems Designer] Analyze formula interactions for unexpected compound effects.

### Specialist Disagreements
No substantive disagreements — specialists highlighted complementary concerns (technical implementability, formula correctness, testability, fantasy fulfillment) that collectively indicate revision is needed.

### Nice-to-Have
- Additional visualization of FSM state transitions for debugging
- More granular difficulty scaling beyond s_diff
- Extended telemetry for AI behavior analysis

### Senior Verdict [creative-director]
After synthesizing all specialist findings, the Guard AI FSM GDD contains multiple blocking issues that prevent implementation. The core FSM logic has undefined variables and phantom references that would cause runtime failures. Critical timing contradictions exist between acceptance criteria. The test infrastructure is insufficient to validate the complex timing-dependent behavior. While the formulas appear correct and the fantasy vision is compelling, the technical foundation requires significant revision before implementation can begin. The issues span from basic variable definitions to system-level timing conflicts, indicating foundational work is needed.

### Scope Signal
Rough scope signal: XL (producer should verify before sprint planning) — Cross-cutting concern touching 10+ systems, requires multiple new ADRs for timing constants and infrastructure, high dependency count on undesigned systems.

### Verdict: NEEDS REVISION