# Review Log — Guard AI FSM (`design/gdd/guard-ai-fsm.md`)

## Review — 2026-08-20 — Verdict: NEEDS REVISION
Scope signal: XL
Specialists: ai-programmer, systems-designer, game-designer, qa-lead
Blocking items: 13 | Recommended: 6
Summary: Undefined critical variables (A/T_chase/unified fact), phantom GuardGoalMode,
NavMesh dependency mismatch, per-frame CalculatePath performance violation, timer and
suppression-rule ambiguities; fantasy issues (4.0 s give-up too short, 2.0 s post-Chase
sweep too brief, carve-out telegraph missing); QA issues (AC timing contradiction,
harness gaps, ambiguous/unautomatable ACs). Full report preserved at
`design/gdd/guard-ai-fsm-review.md` (pre-log-era file).
Prior verdict resolved: First review

## Review — 2026-08-22 — Verdict: MAJOR REVISION NEEDED → revised same session (rev 3.5)
Scope signal: XL
Specialists: ai-programmer, systems-designer, game-designer, qa-lead + creative-director (senior synthesis)
Blocking items: 10 (merged from specialists) | Recommended: 8
Summary: Escalated from NEEDS REVISION — rev 3.4 regressed below its reviewed state
(AC-FSM battery lost from the file), introduced a contract-level conflict with Approved
Perception (D0b folded the FSM goal-mode gate into the published reachability fact), and
still contained a core-loop livelock (give-up clock suspended on reachable targets ⇒ noise
investigations never self-resolve). Senior ruling: skeleton is sound (3-state cap,
tier-scoped suppression, record schema, D1/D2/D4/D5 arithmetic verified); the failures are
in promise-vs-case semantics. All three specialist disagreements adjudicated by
creative-director: D0 integration deleted (consumption-only), give-up fixed on both layers
(canonical OR-form predicate + search-budget start-on-arrival), spot-front hold Chase-only.
Prior verdict resolved: Partially — prior items 1-6 partially resolved, 7-9 carried/evolved,
10-13 moot (ACs deleted); all surfaced blockers addressed in rev 3.5 same session.

### Rev 3.5 revision record (2026-08-22, user decisions on record)
- Give-up clock: keep knob `t_giveup`, change execution semantics — search budget starts on
  arrival; canonical predicate ticks iff ¬(sustained LOS ∧ reachable). No Perception ripple.
- Hide-entry gate: A ≥ T_entry at sight-of-entry (graze-frame dives forgiven).
- Determinism pin: NavMeshAgent per-frame steering + tick-boundary verdicts; virtual-tick timers;
  per-frame backstop non-authoritative; pinned per-tick processing order written into C1.0.
- Test framework: NUnit confirmed by user (Gap-4); src/AI Xunit suite migration = dedicated story.
- D0b split (published ungated fact vs FSM-local catch_gate_pass); D0 integration deleted;
  D0c GuardGoalMode transition table M1–M6 added; cap-forced target gains error radius;
  registry lock-step overhaul (n_resight_cap 2 [1,3], six "fixed" mislabels corrected,
  seven consumed knobs added, Gap-2 closed); AC-FSM-1…14 battery restored; postchase-sweep
  trace marker added; diagram/duplicate-D1/sweep-scaling τ² fixes.

**Next review scope (per creative-director): targeted verification of the 5 merged blockers only —
(1) give-up/livelock family, (2) AC battery, (3) D0b layering, (4) D0 deletion, (5) hold tier +
hide-entry gate. Do not reopen settled sections.**

## Review — 2026-08-22 — Verdict: MAJOR REVISION NEEDED → revised same session (rev 3.6)
Scope signal: XL
Specialists: ai-programmer, systems-designer, game-designer, qa-lead + creative-director (senior synthesis)
Blocking items: 7 (merged from specialists) | Recommended: several (advisory)
Summary: Targeted re-review of the 5 rev-3.5 blockers — 2 closed cleanly (D0 deletion,
AC battery restored), but the hide-entry gate was found NON-FUNCTIONAL four ways
(unsourced T_entry colliding with Perception F5; no numeric-A channel in the Approved
R12 break-fact schema; R6-reset sampling order making the gate ill-defined; sub-threshold
dives granted total immunity against concept no-immunity pins). The give-up livelock was
relocated behind unreachable targets (clock never starts without arrival); D0b misquoted
the Approved unified fact a second time (path-arrival conjunct dropped); error radius and
spot-front confirm-empty were unbounded; D2/D3 tau-cancellation made thoroughness a
no-op. Senior ruling adopted the Investigate-tier fallback for sub-threshold dives.
Prior verdict resolved: Partially — blockers 3 (D0 deletion) and part of 2 (battery)
held; the rest evolved or regressed under scrutiny; all 7 surfaced blockers addressed in
rev 3.6 same session.

### Rev 3.6 revision record (2026-08-22, user decisions on record)
- Hide-entry gate: renamed `T_hide_entry`, bound to Perception F5 `T_entry(R)` (domain
  [0.06–0.30]); evaluated at the last PRE-break sight tick (before R6 reset); schema
  amendment CR-FSM-01 adds `a_pre_break` + `threshold_applied` to hide-dive LOS-break
  events (perception.md R12 + entities.yaml trace_event_schema in lock-step);
  sub-threshold dives emit `investigate-commit cause=hide-entry` targeting the spot
  (no silent immunity); point-blank forgiveness envelope preserved.
- Give-up clock start = min(arrival, path-end) — closest-reachable-point stop also
  starts the budget; hang-fail backstop; AC-FSM-15 added to the battery.
- D0b corrected verbatim to the three-conjunct unified fact (same-surface AND
  path-arrival AND |ΔY| ≤ tolerance); worked example rewritten.
- Single knob `r_investigate_error` 1.5 [1.0–2.5] on noise/alert/cap-forced targets only.
- Tau thoroughness on the count axis: n_sweep = ceil(n_sweep_ref × tau), n_sweep_ref = 4
  locked, t_sweep fixed at base; config-loader assert n_sweep × t_sweep_base ≤ t_giveup.
- Spot-front confirm-empty operationalized: verify dwell `t_spotfront_verify` 1.5
  [1.0–2.5] → Chase-end → Patrol.
- Registry lock-step: margin, r_investigate_error, n_sweep_ref, t_spotfront_verify,
  t_route_budget_max (provisional, Level-owned) registered; T_hide_entry alias +
  consumed_by updates (s_diff, R_max, navmesh_sample_maxdistance); Gap-5 logged;
  perception.md R4 tier-selection note (FSM owns tier choice from the break fact).
- Diagram fixed (Capture as terminal-outcome annotation); investigate visual cue
  mandatory on muted WebGL.

**Next review scope: fresh-session verification of the rev 3.6 changeset — priority order:
(1) T_hide_entry gate end-to-end (F5 binding, pre-break tick evaluation, CR-FSM-01 fields,
fallback path), (2) min(arrival, path-end) clock + AC-FSM-15, (3) D0b three-conjunct
verbatim check vs Perception R1/J2, (4) D2/D3 count-axis arithmetic + config asserts,
(5) registry lock-step sweep (entities.yaml vs Tuning Knobs table). Do not reopen other
settled sections. Open item carried: noise/alert-directed spot-capture carve-out undecided.**

## Review — 2026-08-22 — Verdict: NEEDS REVISION → revised same session (rev 3.7)
Scope signal: XL
Specialists: ai-programmer, systems-designer, game-designer, qa-lead + creative-director (senior synthesis)
Blocking items: 12 (merged from specialists) | Recommended: several (advisory)
Summary: Targeted re-review of the 5 rev-3.6 priorities — all 5 held at doc level
(D0b verbatim match, gate consistency, arithmetic correct, registry lock-step with 2
typos), so no settled section was reopened. However, the rev 3.6 additions themselves
generated 12 second-order contract blockers: CR-FSM-01b schema gap (break facts lack
spot_position/hide_spot_id/entry_id needed for spot capture), spot-front hold undefined
+ telegraph preemption, missing detection predicates (eps_arrive/eps_stop/n_pathend_ticks),
unsatisfiable D3 sweep assert + t_giveup/t_sweep_base venue conflict, C1.4 restatement
drift vs Perception R1, catch_gate_pass AND-form including the gated `reachable` conjunct,
determinism seams (error sampler, watchdog), registry domain/unit fixes, C1.0 fall-through
disposition, AC battery coverage, D0c table repair. Creative-director adjudicated 3
cross-specialist disagreements: D-x1 truncation-aware expected values win, D-x2
t_sweep_base 1.0→0.8 s (user-approved), D-x3 strike `reachable` from catch_gate_pass,
keep dual-query architecture. Carried open item resolved: knowledge-line carve-out —
spot capture authority keys on knowledge source; only witnessed entries (cause=hide-entry)
break spots; noise/alert-directed investigations end fruitless.
Prior verdict resolved: Yes — all 5 targeted priorities held; no settled section reopened;
all 12 surfaced blockers addressed in rev 3.7 same session.

### Rev 3.7 revision record (2026-08-22, user decisions on record)
- Schema extension **CR-FSM-01b**: hide-dive LOS-break facts additionally carry
  `spot_position` + `hide_spot_id` + `entry_id` (perception.md R12 + entities.yaml
  trace_event_schema in lock-step; absent on wall/segment/guard-sweep breaks).
- Spot-front hold operationalized via authored `spot_front_anchor` + guard-radius
  standoff; presence semantics pinned (sight-presence → M5 LivePursuit, dwell abandoned;
  reach-presence capture only after `t_spotfront_verify` dwell; telegraph always completes).
- Detection predicates added: eps_arrive 0.3 [0.2–0.5], eps_stop 0.1, v_stop_eps 0.1,
  n_pathend_ticks 2; mid-transit retarget resets the start arm.
- D3: `t_sweep_base` lowered to **0.8 s** (user decision, D-x2); truncation
  `n_perf = min(n_sweep, floor(t_giveup/t_sweep_base))`; per-arc bound = only
  config-time assert; venue resolution paragraph; starter numbers recomputed.
- catch_gate_pass = live_fix_held AND goal_mode ∈ {LivePursuit, HideSpotFront} AND
  path_arrival_metric_pass (`reachable` conjunct struck per D-x3).
- C1.0 fall-through disposition pinned: failed-gate dive under a higher-priority winner
  = consumed, no record, no deferred queue.
- Determinism: seeded/injectable error sampler (H.0(8)); virtual-tick episode watchdog
  ceiling (H.0(9)) replacing wall-clock timeouts; call-site tagging.
- Registry: t_sweep_base 0.8 [0.6–1.0], entry_threshold full-knob domain note,
  margin safe_range fix, eps_arrive/eps_stop/v_stop_eps/n_pathend_ticks/
  spot_front_anchor registered; trace_event_schema extended (CR-FSM-01b); Gap-6 logged.
- AC battery: rows 3/4/6/8/11/12/13/14 revised (polarity legs, presence legs, corner
  negative test, truncation corner), AC-FSM-15 rewritten to watchdog ceiling ≈32 ticks.

## Review — 2026-08-22 — Verdict: NEEDS REVISION → revised same session (rev 3.8)
Scope signal: XL
Specialists: ai-programmer, systems-designer, game-designer, qa-lead + creative-director (senior synthesis)
Blocking items: 11 (merged from specialists) | Recommended: several (advisory)
Summary: Targeted fresh-session verification of the rev-3.7 changeset — 11.5/12 blockers
verified landed with zero registry mismatches; no settled section reopened. The rev 3.7/3.8
additions themselves surfaced 11 consolidated blockers, resolved same revision: single-clock
spot-front capture condition (gate ∧ elapsed ≥ t_catch ∧ dwell complete), witnessed either-tier
interior-inspection Capture (user-approved; publishes Capture alone, non-promoting), C1.0
fallback re-rank to escalation rank 4 with one documented bounded supersession cell, live-fix
definition FSM-localized, path-end arm de-preconditioned ("regardless of path status"), concept
amendment CR-CONCEPT-01 (user-approved; lines 74/77 applied), entry_id no-alloc-when-live,
forgiveness-envelope formula restatement, 4 new AC rows (16–19).
Prior verdict resolved: Yes — all 12 targeted blockers held; all 11 surfaced blockers addressed in rev 3.8 same session.

### Rev 3.8 revision record (2026-08-22, user decisions on record)
- Single-clock spot-front capture: capture fires on first tick where gate pass ∧ elapsed ≥ t_catch ∧ dwell complete — the "dwell-end + t_catch" offset arithmetic revoked (double-counted the already-running timer).
- Witnessed-entry interior inspection (user-approved): sub-threshold witnessed episodes gain full spot-inspection/capture authority on either tier; Capture publishes ALONE at the Investigate tier (carve_out=true); approach transit = relocation window.
- C1.0 fallback re-rank (CD ruling D-B): sub-threshold fallback holds rank 4; sole surviving supersession cell {graze-dive + cap-reached} documented for playtest triage.
- CR-CONCEPT-01 (user-approved): game-concept.md carve-out clauses re-scoped to witnessed-entry authority.
- entry_id no-alloc-when-live: hide-entrance open allocates only when guard signals no live episode via published live-Investigate/GuardGoalMode facts.
- Path-end arm extension, forgiveness envelope in formula form, AC-FSM-16…19 added.
NOTE (process debt): this entry was reconstructed and appended 2026-08-24 (round 5) — it was omitted from the log at round-4 time; provenance for rev 3.8 is the doc header + session state of that date.

**Next review scope (per creative-director): FRESH SESSION, full panel — verify the 12
rev-3.7 blockers landed correctly; do not reopen settled sections.**

## Review — 2026-08-24 — Verdict: NEEDS REVISION → revised same session (rev 3.9)
Scope signal: L (borderline XL) — producer should verify before sprint planning; the rev 3.9 changeset itself is M
Specialists: ai-programmer, systems-designer, game-designer, qa-lead + creative-director (senior synthesis)
Blocking items: 10 (merged from specialists) | Recommended: ~19 (advisory)
Summary: Fresh-session full-panel verification of rev 3.7+3.8 — the additions were seam-heavy,
not design-broken (CD: "bộ xương chắc chắn", third consecutive seams-only round). 10 blockers:
B1 CR-CONCEPT-01 incomplete (game-concept.md:120 unamended — deterministic S-clean death for the
witnessed Investigate-tier capture path); B2 unconditional "Capture implies Chase-end" left the
Capture-alone meter lifecycle undefined (CR-PERCEPTION-01); B3 hold-suspension still Chase-tier-scoped
(legal corner t_giveup 2.45 s < t_spotfront_verify 2.5 s resurrects witnessed-dive immunity);
B4 live-fix per-tick reading dead-codes the carve-out chain against R4-invisible occupants;
B5 goal-mode-only catch gate lets a peek-out capture out-of-taxonomy on the retained timer (CD:
state conjunct + witnessed-hold predicate exception); B6 de-preconditioned path-end arm false-fires
during SetDestination's async pending span (CD: arming triple + force-arm fallback knob);
B7 no-alloc-when-live channel had zero AC coverage (AC-FSM-20 added); B8 missing watchdog ceilings
(AC-FSM-11(b)/16(b)/17/18); B9 vacuous ≤7/s ceiling + envelope worst-corner pinned starter rate_max
(true domain-worst ceil(0.40/(0.40×0.20)) = 5 ticks); B10 witnessed fruitless inspections had no F6
disposition (CD ruling: seed the same bounded share — "residual tracks a search completed with the
player unfound"). No cross-specialist disagreements required user adjudication; two independent
findings of B3 treated as convergent validation.
Prior verdict resolved: Yes — the 12 rev-3.7 blockers verified landed; all 10 surfaced blockers addressed in rev 3.9 same session.

### Rev 3.9 revision record (2026-08-24, user decisions on record)
- CR-CONCEPT-01 completion: game-concept.md:120 amended (user-approved) — witnessed-entry authority both tiers + one-event-per-spot dedup extended to mixed witnessed-Chase/witnessed-inspection incidents.
- **CR-PERCEPTION-01 atomic amendment** (perception.md, user-approved): R12 Capture-implies-Chase-end scoped Chase-tier only + meter lifecycle for Capture-alone exits (Idle, decay resumes); R10/F6 share scope extended to witnessed-entry investigations; GuardGoalMode feed registered (informational, id-allocation choice only); liveness snapshot = previous tick boundary; trace-marker class registered (`postchase-sweep`, `suppressed-hide-fallback`).
- Either-tier sweep: C1.2 hold suspension struck "Chase-tier"; state table row 2 exit/behavior cells updated.
- Live-fix hide-spot persistence pin (C1.4): sight-of-entry fix remains live while held; continuity = nothing on reset list occurred.
- Catch-gate state conjunct (C1.4/D0b): `catch_gate_pass` gains `(state == Chase OR witnessed-entry-hold-engaged)`; interior-check metric targets the spot-derived position.
- Path-end arming suppression (C1.2): triple `hasPath && !pathPending && destination == current episode target` + force-arm knob **`t_pathend_suppress_max` 1.0 s [0.5–2.0]** (user-approved name/value) registered; `.speed` → `velocity.magnitude`; ":94 no async pathPending" parenthetical corrected (SetDestination is async; catch-gate CalculatePath is not).
- Pause-scope reconciliation (CD concept-parent-wins): only the LOS-break pause is suspended during a hold.
- Envelope general bound: ceil(threshold_max/(rate_max_min × T_sample_min)) = 5 ticks (:75/:326).
- AC battery: AC-FSM-20 added (both gate legs + entry_id stability + sight-committed close); ceilings on 11(b)/16(b)/17/18; AC-FSM-19 de-vacuated (config-derived ceil(1/T_sample)+2 ceiling, two-guard stagger fixture); AC-FSM-8 state-conjunct note; b1-window non-vacuity reworded; evidence-gate enumeration extended to 20.
- Registry lock-step: Chase-end/Capture notes scoped + de-keyed from tier-blind "concept line 120" (+ Grade-GDD TODO); t_spotfront_verify note updated either-tier/single-clock; t_pathend_suppress_max registered; trace-marker schema entry added.
- Dependencies label corrected: Player Controller Undesigned → Drafted; Gap-7 register corrected (line 120 had not received CR-CONCEPT-01).

## Review — 2026-08-24 — Verdict: NEEDS REVISION → revised same session (rev 3.10)
Scope signal: changeset M (overall doc borderline XL)
Specialists: ai-programmer, systems-designer, game-designer, qa-lead + creative-director (senior synthesis)
Blocking items: 4 (merged from 7 specialist findings) | Recommended: ~13 (advisory; most applied same-pass at touched lines)
Summary: Round 6 — pure-verification panel per directive. All 10 rev-3.9 blockers (B1–B10) verified LANDED (10/10); zero settled sections reopened; either-tier sweep clean (13 tier-scoped clauses checked). The rev 3.9 additions themselves surfaced 7 findings, adjudicated by CD into 4 blockers (residual tail judged NOT a structural wave — consolidation directive held in reserve): R6-B1 witnessed-entry authority still keyed on immutable opening causes at check sites (semantic dead-code for converted retarget episodes) → mutable episode-level property `witnessed_entry_authority` (user-approved name) replaces cause tests at all authority sites + AC-FSM-8 negative row + AC-FSM-20 leg (c); R6-B2 interior-check datum ambiguity (arrival anchor vs path-metric vs backstop targets differ ≈0.9 m > margin 0.5) → three-datum contract with sampled same-surface navmesh proxy at M4 engagement; R6-B3 stagger had no falsifiable numeric contract → round-robin locked invariant, zero same-tick overlap assert == 0 exactly; R6-B4 dedup law had no incident delimiter (two dives minutes apart = one grade event) → CR-CONCEPT-02 episode-scoped delimiter (user-approved concept amendment).
Prior verdict resolved: Yes — B1–B10 all landed; all 4 surfaced blockers addressed in rev 3.10 same session.

### Rev 3.10 revision record (2026-08-24, user decisions on record)
- **R6-B1a**: `witnessed_entry_authority` — new mutable episode-level property set TRUE when an episode opens from a witnessed entry OR is converted by a witnessed sub-threshold retarget; every authority check (C1.2 suspension, C1.3 hold opener, C1.4 predicate exception, D0c M4, telegraph row) now references the property, not opening causes. C1.4 formalizes `witnessed-entry-hold-engaged := goal_mode == HideSpotFront ∧ episode carries witnessed_entry_authority`.
- **R6-B1b**: AC-FSM-8 state conjunct made falsifiable — mandatory negative row (Patrol ∧ LivePursuit ∧ path-pass ⇒ gate MUST FAIL); Investigate-state HideSpotFront variant now mandatory.
- **R6-B1c**: AC-FSM-20 leg (c) added — converted-episode end-to-end (occupant through dwell ⇒ M4 via property, exactly one Capture carve_out=true, ZERO Chase records), convicting opening-cause-keyed implementations.
- **R6-B2**: interior-check three-datum contract (C1.4): arrival → spot_front_anchor hold position; path metric → same-surface navmesh proxy of spot_position (SamplePosition once at M4 engagement, failure ⇒ shallow off-mesh fallback branch); backstop → raw spot_position XZ+|ΔY| with occlusion-gated Linecast; datums never interchangeable; fixture fabric pinned off-navmesh-interior (H.0(6)).
- **R6-B3**: catch-gate query round-robin stagger locked (zero same-tick overlap invariant, "Not tunable"); AC-FSM-19 zero-same-tick-overlap assert == 0 EXACTLY added (falsifiable where per-guard quota alone passes).
- **R6-B4 / CR-CONCEPT-02** (user-approved): game-concept.md:120 incidents episode-scoped — closes when episode(s) resolve; later witnessed dive = new incident + own event. Applied in lock-step to perception.md R12 Capture note + entities.yaml Capture note + Gap-9 (RESOLVED) register entry.
- Peek-out window arithmetic corrected (~≤ t_resight_min understated; commit-bound ≈ T_chase/rate_max ≈ 1.25–1.67+ s); close-witness corner early-accrual note corrected (:95, masked by single-clock dwell bound); telegraph dwell-abandonment beat added (M5 kneel release readable, rising-tension cue carries to Chase sting); `t_pathend_suppress_max` row added to FSM-owned knobs table (lock-step fix); Player Fantasy :16 deterministic rephrase ("resolved, knowable gamble").
- AC battery: AC-FSM-16(i) resolution pinned first-tick ≥ T_dwell + leg (i) SUSPENDED telemetry named primary detector (ceil-insensitivity advisory recorded inline); ceiling literals corner-tagged — AC-FSM-18 "(corner literal = band-min; starter truth 60 ticks)", AC-FSM-11/16(iii) "(corner rates; starter truth ≈4 ticks)" — NOTE: the panel's "starter truth 6" for AC-FSM-11 did not survive independent recomputation from registry values (max(t_spotfront_verify 1.5, t_catch 1.0)/T_sample 0.5 × 1.2 = 3.6 ≈ 4); tag written with the recomputed value. H.0(3) consumer lists extended to AC-FSM-20; H.0(4) arming-suppression forcing flags added.

**Next review scope (per creative-director — ROUND 7 IS TERMINAL): fresh-session verification of the
rev 3.10 blockers ONLY (R6-B1..B4) + full B1–B10 regression sweep. No rev 3.11 under any outcome:
if round 7 surfaces any new HIGH finding or regression, the consolidation rewrite fires automatically;
if it verifies clean, the GDD proceeds to approval. src/AI propagation stays FROZEN until
post-round-7 verification (current src/AI was written against rev 3.9 and inherits SEAM-A/B dead paths).**

## Review — 2026-08-24 — Verdict: NEEDS REVISION → consolidation rev 4.0 same session (TERMINAL round executed)
Scope signal: changeset S (overall doc borderline XL)
Specialists: game-designer, ai-programmer, systems-designer, qa-lead + creative-director (senior synthesis)
Blocking items: 1 HIGH (R7-H1) + 3 ride-along consolidation items | Recommended: waived-with-log advisories (see below)
Summary: Round 7 — TERMINAL verification per round-6 directive. R6-B1..B4 all verified LANDED
(property at 8 authority sites incl. falsifiable AC coverage; three-datum contract non-substitutable
via convictions in existing ACs; stagger invariant schedulable + ==0 EXACTLY assert; CR-CONCEPT-02
lock-step across game-concept.md:120 / perception.md:52 / entities.yaml:539 / Gap-9).
B1–B10 regression sweep 10/10 intact; completeness 8/8. One new HIGH fired the pre-sanctioned
consolidation branch: **R7-H1** (ai-programmer, CD-confirmed overriding qa-lead's scope-bound "zero
HIGH") — stagger queryless-tick gate-verdict semantics unpinned: zero-overlap necessarily creates
K−1 queryless ticks per guard per cycle; only deferral language is status-scoped (E23); carry-forward
vs fail-by-default both pass every written AC yet diverge observably in multi-guard play, and
fail-by-default breaks the locked t_catch ≥ 2×T_sample_max floor + false-fails watchdog ceilings at
legal multi-guard corners. CD class-matched it to R6-B1/R6-B3 ("R6-B3's residual — invariant landed,
induced semantics didn't"). Consolidation rev 4.0 applied same session, scope A–F CD-frozen:
(A) C1.4 carry-forward pin (path metric holds last emitted verdict while live fix persists;
fail-by-default forbidden; backstop still live per-tick); (B) AC-FSM-8 stagger-silence legs on the
two-guard fixture; (C) AC-FSM-8 mandatory negative row 2 {Investigate × LivePursuit ⇒ FAIL} +
AC-FSM-11(c) ZERO-Capture-before-Chase-entry-commit assert; (D) AC-FSM-20(c) mirrors 16(iii)'s ZERO
investigate-resolution; (E) C1.5 duplicate-sentence strike; (F) :90 rate-band errata [0.60–0.80] →
[0.40–0.80], commit-bound restated ≈1.25–2.5 s.
Prior verdict resolved: Yes — R6-B1..B4 landed and verified this round.

### Waived advisories (accepted-with-log, reopenable at a future milestone)
- **TOP FLAG for Level/hide-system GDD pass**: H.0(6) enclosed-zone fabric should pin open-faced/anchor-sightline geometry — a fully-walled fixture makes reach-presence structurally impossible (occlusion gate), so correct implementations would false-fail AC-FSM-16(b)/20(c) watchdogs (qa-lead #2).
- Arming-suppression forcing flags (H.0(4)) have no consuming AC leg yet (qa-lead #3).
- D0c M4 Trigger-cell phrasing could be misread as cause-test in isolation (game-designer; normative defs unambiguous, AC-FSM-20(c) convicts).
- ai-programmer minors: property default-FALSE/clear-at-close implied-not-literal; :93 shallow-branch wording; Euclidean pre-gate datum during hold; stagger membership tiebreak unstated.
- systems-designer F-2/F-3/F-4: :95 magnitude illustration imprecise (proxy↔raw ≤ 0.4 < margin); AC-FSM-16(iii) ceiling omits approach transit (test-infra risk); AC-FSM-11(b) window assumes reach-presence injected ≈ engagement (pin injection tick when authoring fixtures).
- Registry trace_event_schema residual (carried since round 6): live-Investigate fact + GuardGoalMode feed prose-only, no event_types entries.

### Process rules on record (CD ruling)
- No rev 3.11 honored — consolidation shipped as rev 4.0 via the pre-sanctioned branch.
- Diff audit: only surfaces A–F + header/version metadata touched; deviation beyond scope re-gates.
- NEXT: ONE confirmation panel (verification-only charters: A–F diff + B1–B10 + R6-B1..B4 regression sweep) in a FRESH session — clean result approves the GDD.
- src/AI propagation stays FROZEN until post-consolidation verification lands.

### Rev 4.0 revision record (2026-08-24, user-approved changeset)
- All six touch-points written to `design/gdd/guard-ai-fsm.md` this session (header rev bump included as process metadata).
- Editorial choice on record: C1.5 duplicate resolved by keeping the richer first variant ("that were never converted").

## Review — 2026-08-24 — Verdict: APPROVED (contingent → contingency landed same session via rev 4.1; FINAL)
Scope signal: changeset S (overall doc borderline XL)
Specialists: ai-programmer, systems-designer, game-designer, qa-lead + creative-director (senior synthesis)
Blocking items: 0 HIGH / 0 swept-regression / 1 MED (R8-M1, repaired same session) | Recommended: waived-with-log (dispositions below)
Summary: Round 8 TERMINAL CONFIRMATION PANEL per round-7 directive — all charter sweeps clean
(completeness 8/8; A–F diff audit exact at :3/:90/:96/:486/:489/:498 with no out-of-scope edits,
confirmed independently by all four specialists' greps [presence+coherence method — no git repo];
B1–B10 regression 10/10 intact; R6-B1..B4 clean incl. CR-CONCEPT-02 four-file lock-step). Sole MED
affirmed — **R8-M1** (qa-lead): AC-FSM-11 leg (c)'s hang-watchdog ceiling was not extended for the
leg's newly added post-flip commit-bound span (~26 legitimate ticks vs ~16-tick ceiling at legal
corners ⇒ correct implementations false-fail the BLOCKING watchdog, violating H.0b's anti-false-fail
convention / AC-FSM-14 corner-green). CD ruled it test-arithmetic — an induced constant lagged its
dependent, categorically distinct from R7-H1's induced-semantics class: repair obligated, re-gate
refused. Verdict APPROVED contingent on micro-amendment rev 4.1 passing the five-step verification
bar — ALL FIVE PASSED in-session (V1 scope lock :3/:90/:489 · V2 corner arithmetic {29, 27, 9, 8}
all dominating with positive margin · V3 non-normativity attested · V4 adjacent spot-checks clean ·
V5 logging) ⇒ auto-flip clause never triggered. GDD APPROVED after 8 review rounds.
Prior verdict resolved: Yes — R7-H1 consolidation verified landed (A–F diff audit exact).

### Rev 4.1 micro-amendment record (2026-08-24, CD-sanctioned, user-approved)
- **R8-M1 FIX (:489)**: leg-(c) hang-watchdog ceiling gains the config-derived term
  `+ ceil(T_chase / (rate_max_min × T_sample))` — corner 16 + ceil(1.00/(0.40×0.20)) = ≈29 ticks,
  dominating the ≈26-tick legitimate span; starter truth 4 + 4 = 8; stale "(rev 3.9)" tag refreshed;
  legs (a)/(b) fixtures keep the base formula.
- **R8-L1 FIX (:90)**: timing-note qualifier — "commit-bound > t_catch max 1.2 s" holds at pinned
  T_chase 1.00 (D0a) and is config-derived across full registry bands; at the band-min corner
  (T_chase 0.85 / rate_max 0.80 ≈ 1.06 s) enforcement is structural via the catch-gate state
  conjunct (`witnessed-entry-hold-engaged` collapses on the M5 flip), never the inequality alone.
- Header (:3): Status In Design → **Approved** (rev 4.1 narrative prepended; rounds 6–7 history retained).
- systems-index.md row 1 updated to Approved.

### Dispositions (CD binding ruling)
| ID | Finding | Source | Sev | Disposition |
|----|---------|--------|-----|-------------|
| R8-M1 | AC-FSM-11(c) ceiling omits post-flip commit span | qa-lead | MED | FIXED rev 4.1 |
| R8-L1 | commit-bound inequality not band-corner-safe | systems-designer | LOW | FIXED rev 4.1 |
| R8-L2 | cold-start carry-forward cell ("last emitted verdict" undefined before a guard's first-ever query on a fix) | ai-programmer + systems-designer + game-designer (convergent) | LOW | WAIVED-WITH-LOG + **BINDING CANONICAL PIN: init-fail-until-first-query** (accrual delay ≤ K−1 ticks, player-favorable direction only, t_catch floor untouched, no unfair/unreadable outcome under any coherent implementation). Sealing sentence deferred to next C1.4 touch — amendment kept single-root-cause |
| R8-L3 | AC-FSM-11(c) ordering discriminator collapses to same-tick tie at corner {rate_max 0.80, T_sample 0.5, earliest flip} | ai-programmer | LOW | WAIVED — fixtures inherit the injection-time pin (F-4 class binding fixture-authoring guidance); valid when peek-out is injected mid-dwell |
| R8-L4 | stagger-silence completion bound insensitive at mandated starter corner (K=2) | qa-lead | LOW | WAIVED — conviction rests on the verdict-identity leg (+ accrual-continuity leg catches hybrids); b2-style forced-corner discriminator optional at fixture authoring |
| R8-L5 | :75 illustrative ΔA band holds rate_max at starter | systems-designer | LOW | WAIVED — settled rev 3.8/3.9 surface, do-not-reopen stands (CD-corrected true two-knob range [0.08, 0.40]) |
| R8-C1 | H-5 appendix "(2+1+1) × 10 = 40" garble vs normative D7 "(3+1) × 10 = 40" | systems-designer | cosmetic | WAIVED — non-normative appendix; note-for-next-touch |

### Process notes on record (CD ruling)
- Defect-provenance principle formalized: defects predating a changeset are waivable-with-log as debt;
  defects introduced by a sanctioned changeset obligate repair (obligation to repair ≠ obligation to re-gate).
- "The ceiling grows; the window never shrinks" — rushing fixtures to fit a stale ceiling must never
  substitute for ceiling repair when the rushed span is the property under test.
- Advisory: initialize Git before further src/AI work — every round-6–8 diff audit was necessarily
  presence+coherence based; a VCS makes future scope locks mechanical.
- **src/AI propagation freeze LIFTED** (post-consolidation verification landed): rev 3.9→4.1 contract
  changes now pending propagation to src/AI (written against rev 3.9, inherits SEAM-A/B dead paths);
  NUnit migration (Gap-4) still pending before AC battery CI legs run.
