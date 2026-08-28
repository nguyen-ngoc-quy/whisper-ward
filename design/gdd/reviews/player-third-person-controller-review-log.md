# Review Log — Player Third-Person Controller

> **Document**: `design/gdd/player-third-person-controller.md`
> **Log created**: 2026-08-24 (first review round)
> Entries are appended chronologically; each entry records the verdict, the panel,
> and the disposition of its findings.

---

## Review — 2026-08-24 — Verdict: NEEDS REVISION
Scope signal: L
Specialists: gameplay-programmer, ai-programmer, game-designer, systems-designer, qa-lead + creative-director (senior synthesis)
Blocking items: 9 | Recommended: 6
Summary: Conform to Perception's approved sensing convention (binary stance enum; Perception selects its own pinned eye offsets from the flag) instead of re-deriving ray geometry via h_cap/k_eye ratios; replace binary backpedal suspension with proportional rear damping; repair arithmetic/test-grade precision defects (F2 ceil notation breaching its own declaration, false-PASS verification row, phantom noise-pulse chain from time-sliced ring arming). Spine frozen — no scope expansion; anti-pillar held (no jump/climb/lean/cover).
Prior verdict resolved: First review

### Revision disposition (same session)

All 9 blocking items FIXED in `player-third-person-controller.md` + `design/registry/entities.yaml`:

| Blocker | Disposition |
|---------|-------------|
| B1 stance/h_cap coupling | FIXED — binary `stance {Crouched, Standing}` flipping at gamma 0.5; Perception selects its OWNED pinned eye offsets (crouch 0.5 m / stand 1.6 m) from the flag; h_cap demoted to catch-gate Linecast operand only; k_eye ratio model struck everywhere (GDD + entities.yaml) |
| B2 backpedal handling | FIXED — proportional rear damping `w(dot)` with rear_damp_floor 0.25 [0.15, 0.40]; continuous through dot=0, no flicker/hover trap; straight-back reversal ≈2.5–3 s deliberate cost |
| B3 capsule pivot | FIXED — feet-anchored `center.y = h_cap/2`; stand-gate upward ceiling probe gating gamma increase with `stand_blocked` readout (E10 — the sole documented totality exception); E11 no-airborne ledge/fall ruling |
| B4 animation contract | FIXED — in-place clips ONLY; animator params State / SpeedRatio / Gamma; controller exclusively owns translation |
| B5 noise-ring arming | FIXED — stride-keyed step_event (stride_length_walk 1.9 m / run 2.6 m, registered in entities.yaml); ring arms on FIRST completed stride; silent states force r_noise=0 on entry frame (phantom-pulse chain killed); tap-inching genuinely silent; respawn clears accumulators; audio plays exactly on step_event |
| B6 arithmetic/test-grade precision | FIXED — F2 runtime-max corrected to sup form (ceil struck); V_crouch JOINT config-time assert (every config V_crouch < V_patrol); accel_time domain bound (> 0, ≤ 0.10 s); AC-P11 de-tautologized; AC-P17 declaration-bound; catch-invariant starters row relabeled (= 4.375 < 5.5, corroborates guard-ai-fsm.md D5); false-PASS verification row 8 → DOCUMENTED (integration-test routing) |
| B7 publication contract | FIXED — ONE transition event max per frame (same-frame collapse to net from/to); cached readonly snapshot struct {state, stance, stand_blocked, v_eff, gamma, h_cap, r_noise, facing}, zero-allocation; sever-preempts-publication on capture frames |
| B8 crouch input model | FIXED — toggle latch on C key edge-trigger (WebGL-safe vs Chrome Ctrl+W); Shift+move under crouch latch stays Crouch speed; WebGL keyboard rationale preserved for #19 handoff |
| B9 grab ownership | FIXED — artifact-grab verb routed to Win/Lose (#9); C0 rewrite + dependency annotations |

**User decisions** (all `[A]` recommended): B1 conform-to-Perception · B2 proportional damping · B8 toggle-crouch on C · B5 stride-keyed ring + audio.

**Defaults applied without question**: E11 ledge/fall ruling (no airborne state, level pin, gravity → ADR/#18); Guard AI FSM status refresh → Approved rev 4.1; M-item rewrites (M2 45–60 min, M4 clip prerequisite + video); coverage battery expanded 18 → 22 BLOCKING (AC-P19 yaw-frame direction correctness, P20 single-event shape, P21 determinism replay canary, P22 readout completeness/copy-safety); H.0.9–H.0.12 test seams (injectable clock/tick-counter, synthetic atomic multi-key input port, capture-injection port, shared eps_compare policy + composite fixture builder + logger spy).

**Registry sync**: entities.yaml — V_crouch constraint → JOINT config-time assert form; accel_time safe_range domain bound; h_crouch k_eye strike → stance-flag reference (rev 2026-08-24 note); +3 knobs (rear_damp_floor 0.25, stride_length_walk 1.9, stride_length_run 2.6 — the latter two SHARED with Player Noise co-sign).

**Perception GDD**: zero edits (verify-only pass — its approved convention consumed correctly downstream).

---

## Review — 2026-08-25 — Verdict: NEEDS REVISION
Scope signal: L
Specialists: gameplay-programmer, game-designer, systems-designer, qa-lead, unity-specialist + creative-director (senior synthesis)
Blocking items: 8 | Recommended: 11
Summary: Re-review confirmed all nine Round-1 dispositions hold (B1–B9 verified independently by every specialist). The 8 new blockers are second-order consequences of those fixes, led by CD ruling D: replace per-state noise accumulators with a single global audible-distance ledger (accumulate only in audible states, freeze — never reset — during silent states, clear only on capture/respawn, ≤1 step_event/tick with remainder retention), which honestly retires the "tap-inching stays genuinely silent" doctrine. Remaining blockers: F3/E3 still narrate the pre-damping "2.5–3 s" reversal cost against a true ≈0.83 s (band [0.52, 1.24]); C3/C4 retain stale normative text (`dot < 0`, "facing holds") contradicting F3; stand_blocked must pin Reading A (gates the movement state, not just pose); capture/sever ingress ordering, stand-probe API parameters, animator clip-contract completion, and harness authorability (H.0.13 probe port, center.y accessor placement, AC-P23/P24, H.0.5 yaw sign pin) need explicit specification.
Prior verdict resolved: partial

---

## Review — 2026-08-26 — Verdict: NEEDS REVISION
Scope signal: L
Specialists: gameplay-programmer, game-designer, systems-designer, qa-lead, unity-specialist + creative-director (senior synthesis)
Blocking items: 8 | Recommended: 6
Summary: Re-review confirmed all eight Round-2 dispositions hold (panel consensus plus independent digit-exact math re-derivation by three agents). The 8 new blockers are third-order consequences, led by CD ruling GD-B1: the F3 rate-limited damped-turn model makes sustained straight-back retreating self-cancelling (facing converges ~0.83 s), so the "retreat while watching your threat" verb claims in E3/knob rationale/M4 were unsatisfiable as written — resolved by user decision as an honest copy-reframe into a transient damped window (arc-steering + turn-and-sprint are the sustained verbs; camera-yaw-anchored facing rejected as anti-pillar-risky). Remaining clusters: step_event pinned as the SOLE gameplay channel for guard hearing AND audio (r_noise demoted to debug/telemetry), closing the armed re-entry fairness window; step_event payload fully specified in-doc mirroring its entities.yaml registration; H.0.14 calculator port added so the "all 24 ACs headless" claim is true (AC-P24); stand-probe pins rewritten to overlap-query semantics (top-extent-only margin, explicit QueryTriggerInteraction.Ignore, fail-safe buffer) with evidence routed to the #18 physics gate; repeat-defect-class bundle (AC-P9 derive-from-registry timings, AC-P3 min()-form settle tick, honest V_walk ≈1.45× worst corner, four new validator legs); Idle-facing ruled hold-last-value with the respawn reset reshaped to Idle-standing + spawn-forward facing + from-state tracker reset.
Prior verdict resolved: Yes

### Revision disposition (same session)

All 8 blocking clusters FIXED in `player-third-person-controller.md` + `design/registry/entities.yaml` (user-approved changeset):

| Blocker | Disposition |
|---------|-------------|
| B1 F3/E3 self-cancelling retreat verb | FIXED — copy-reframe per user decision: F3 prose rewritten as transient damped window (~0.83 s at starters; no held-key retreat verb claimed); active arc-steering + turn-and-sprint named the sustained verbs; E3 + both knob-rationale rows + M4 aligned |
| B2 consumption-channel fairness pin | FIXED — F5(g) pin: the step_event stream is the SOLE gameplay channel for guard hearing (#3) and footstep audio; r_noise demoted to debug/telemetry with the ~0.53 s Walk re-entry window named; E5 + Visual/Audio footfall bullet aligned; AC-P10 adds `test_reentry_after_arm_restores_radius_immediately` |
| B3 step_event payload underspecified | FIXED — C7 payload-spec bullet {timestamp (H.0.9 seam), resolved audible state, post-movement-resolution world position, stride_length_m consumed, ledger_remainder_m, publisher} mirroring the registered entities.yaml schema entry verbatim; H.0.3 tap + H.0.9 clock seam + Event-bus dependency row cover BOTH channels |
| B4 AC-P24 headless claim false | FIXED — new H.0.14 animator-parameter calculator port (injectable pure SpeedRatio function fed by the H.0.6 fixture registry + call-record spy); AC-P24 runs via H.0.14; Summary "all 24 headless" claim now accurate |
| B5 probe-parameter pins lack evidence route | FIXED — Provisional flags paragraph routes the C6/H.0.13 pins to the #18 physics gate (binding pre-milestone-0, owner technical-director, verified against the production adapter's H.0.13 call record); stance→Perception-offsets and h_cap→catch-gate integration chains owned by the first Integration story under tests/integration/ |
| B6 stand-probe API semantics | FIXED — C6 rewritten: overlap-query semantics (OverlapCapsule-class, NOT a swept cast), TOP-extent-only margin inflation, explicit QueryTriggerInteraction.Ignore, player-layer mask exclusion, fail-safe buffer overflow direction; autoSyncTransforms staleness owned by the physics ADR / #18 gate list; E10 + Physics dependency row + H.0.13 aligned |
| B7 repeat-defect-class bundle | FIXED — AC-P9 derives first-event tick counts from the fixture registry via the F5c formula (starter literals documentation-only); AC-P3 min()-form settle tick; V_walk honest ≈1.45× worst-legal-corner statement (GDD knob rationale + entities.yaml constraint; config-review monitoring replaces the false "≥1.6× across bands" pin); validator legs extended (positive stride lengths, rear_damp_floor ∈ (0,1), backpedal_factor ≤ 1) in joint-constraint section + H.0.7 + AC-P18 with three new failing-leg test names; H.0.6 enumeration adds turn_rate/h_stand/h_crouch |
| B8 Idle facing + respawn reset shape | FIXED — hold-last-facing rule at C4 per user decision (no camera tracking, no autonomous turn); respawn reset = Idle (Standing latch, standing pose) with facing initialized from the spawn transform's forward (delivered by #9) and the from-state tracker reset so first input publishes Idle→X; C8/E7/Win-Lose-row/AC-P16 aligned (+2 respawn test names) |

**User decisions** (grouped widget): B1 remedy [a] copy-reframe (camera-yaw anchor option rejected — CD flagged anti-pillar risk: strafe-animation variant set expands the silent time sink) · Idle facing = hold last value · approved changeset = GDD + registry.

**Registry sync**: entities.yaml — V_walk constraint honesty repair (≈1.45× worst-corner statement replaces the unsatisfiable ≥1.6× claim; rev 2026-08-26 round-3 note); backpedal_factor constraint gains the H.0.7 validator-leg enforcement note; rear_damp_floor constraint annotates both bounds as validator legs; stride_length_run constraint notes the positive-stride legs.

---

### ERRATUM — 2026-08-26 (issued during Round-4 verification)

The Round-3 disposition table above records B8 as "FIXED — … C8/E7/Win-Lose-row/AC-P16 aligned (+2 respawn test names)". That claim was **false for two of the four cited locations at the time it was written**: `E7` (:254) and the Interactions Win/Lose row (:73) still carried the retired token "Walk-idle Standing" — vocabulary that exists among none of the declared movement states (Crouch/Walk/Run/Idle) and that mis-specifies respawn initialization to any implementer reading E7. C8 and AC-P16 were correctly aligned. The stale labels and the falsified disposition record were repaired during Round 4 (Round-4 blocker B2 below); this erratum amends history explicitly rather than silently rewriting the Round-3 entry.

---

## Review — 2026-08-26 — Verdict: NEEDS REVISION (Round 4 — verification-only confirmation pass)
Scope signal: L
Specialists: gameplay-programmer, game-designer, systems-designer, qa-lead, unity-specialist + creative-director (senior synthesis)
Blocking items: 5 | Recommended: 15 (+4 pre-milestone-0 items routed to named owners, not GDD blockers)
Summary: Verification-only re-review confirmed all eight Round-3 disposition clusters substantively applied — arithmetic digit-exact under independent re-derivation by two agents (T_reverse 0.83 s starter, [0.52, 1.24] band corners; F5c corners; catch invariants; chase ceiling 6.81), entities.yaml synced field-for-field, zero regressed previously-fixed surfaces. The five new blockers are third-order consequences under the CD pattern diagnosis: every semantic pivot lands faithfully on the normative surfaces while non-normative prose retains fossilized vocabulary, and the defect has now migrated into the verification apparatus itself — the Round-3 B8 disposition claimed alignment for two locations that were never aligned (see erratum above). Enforced fixes: ground-tangency bottom treatment for the stand-probe overlap query (C6/E10/H.0.13); retired-token sweep at E7 + Win/Lose row with the log erratum; consumption-channel residue trio (:41/:69/:282) reopening the F5(g) fairness pin; AC-P16 respawn seam via the capture-injection port; hold-last-facing coverage enforced as a batch rider (new AC-P25). Two procedural rules adopted so a Round-5 verification pass can be terminal (CD confidence ~85%): retired-token sweep after every semantic pivot, and per-location disposition citations.
Prior verdict resolved: partial (all 8 Round-3 clusters verified substantively landed; editorial residue at six surfaces escalated into Round-4's own blockers)

### Revision disposition (same session)

All 5 blocking items + user-approved batch riders FIXED in `player-third-person-controller.md` + `design/registry/entities.yaml` (Full-batch changeset):

| Blocker | Disposition |
|---------|-------------|
| B1 ground-tangency bottom treatment | FIXED — C6 declares the probe QUERY SHAPE insets its lower hemisphere ≥ skin_width above the feet-anchored floor plane (point0 raised accordingly; published h_cap / center.y never modified); inset recorded in the H.0.13 call record for the #18-gate audit; buffer-overflow ambiguity closed (`count == capacity` ⇒ blocked fail-safe; only `count == 0` reads clear); E10 + Physics dependency rows aligned; `queriesHitTriggers` phrasing pinned OFF as defense-in-depth |
| B2 retired-token sweep + log erratum | FIXED — "Walk-idle Standing" → "Idle (Standing latch, standing pose)" at E7 and the Interactions Win/Lose row; ERRATUM appended above correcting the Round-3 B8 disposition claim (history amended, never silently rewritten) |
| B3 consumption-channel residue trio | FIXED — r_noise bullet (:41) demoted to DEBUG OVERLAY AND TELEMETRY ONLY, never a gameplay channel; Player Noise interactions row (:69) consumes the step_event stream per F5(g); downstream #3 row (:282) names the step_event stream ALONE as the owed interface (armed-radius readout struck from what #3 receives) |
| B4 AC-P16 respawn seam | FIXED — H.0.11 extended: the capture-injection port doubles as the headless respawn trigger with spawn-forward as an injectable fixture parameter; homeless validator tests consolidated into `playercontroller_config_validation_test.cs` (P11/P17/P18; P21→publication, P24/P25→movement); headless-traceability claim restored at 25 blocking ACs |
| B5 C4 hold-last-facing coverage (batch rider, CD-enforced) | FIXED — new AC-P25 `test_idle_holds_last_facing_no_autonomous_turn` on the H.0.5 yaw fixture: yaw rotates across idle ticks, facing asserted bit-identical — the only Round-3 USER DECISION previously lacking a failing leg |

**Batch riders applied** (user-approved Full batch): h_crouch safe-range top capped 0.99 → 0.95 (the doc's own honest working range) · AC-P7 bound to registry-derived accel_time; AC-P9 exact first-tick derivation (Σ v_eff·dt against eps_compare) replacing the undeclared tick-grid slack window · backpedal_factor both-bounds validator legs ∈ (0, 1] with failing-leg test names; stride_length_run ≤ 0 leg; stride_length_walk positivity parity · F5c Run-corner honesty repair (worst legal slowest arming 0.68 s; V_run consumed-row relabeled — 6.81 is a solo-tuning ceiling, NOT the band top) · animator forward/backward variant input delivered adapter-side from snapshot planar velocity direction vs published facing (**ninth snapshot field rejected** — preserves AC-P22 shape); rig type pinned to the same pre-M4 decision slot; stride-retune gate as a config/import validation check (**playback-rate compensation subsystem rejected** — anti-pillar austerity held) · stand-probe XZ anchor pinned to the tick-entry (pre-move) position · spawn-forward antecedent resolved (delivered BY #9 per C8) · F5(g) consumer-side evidence owner routed (#3 design gate + first Integration story under tests/integration/) · M9 gear-distinguishability row added; M8 first-footstep reads-as-became-traceable clause; M1/M4 protocols require frame-count measurement (~0.83 s ± 0.15 s) · C7 intra-phase publication ordering pinned (net transition FIRST, then at most one step_event — determinism presupposed by AC-P21); transition payload gains publisher field · C8 initial session spawn applies the identical authoritative reset · AC-P14 payload-fields assertion `test_step_event_payload_fields_match_completed_snapshot` · E9 trig inventory corrected (atan2 every turning tick up to T_reverse; camera-yaw sin/cos ownership at the #19/#20 boundary); F3 dt symbol row · OQ1 ADR package: honesty note that the render-rate Update-class loop + per-tick center.y mutation effectively preclude kinematic Rigidbody (CharacterController presumptive default pending ADR rebuttal); the package additionally owns step_offset policy (pin ≈ 0 — anti-pillar protective) and skin_width constant resolution.

**Registry sync**: entities.yaml — h_crouch safe_range "[0.85, 0.99]" → "[0.85, 0.95]" (round-4 rev note on the constraint); backpedal_factor constraint upgraded to the both-bounds "(0, 1]" validator-leg form (floor leg round 4); stride_length_walk gains the positive-stride validator-leg constraint (parity with run); V_walk monitoring phrase names owner/artifact/trigger (game-designer config review triggered by any V_crouch/V_walk band change; artifacts production/qa/smoke-[date].md + manual M9).

**Procedural rules adopted (binding for future review rounds)**: (1) *Retired-token sweep* — after every semantic pivot, mechanically grep the document and its consumers for the pivot's retired vocabulary before any fix is declared complete; (2) *Per-location disposition citations* — every future disposition claim must cite each specific location changed; global assertions ("aligned", "swept") without per-location evidence are invalid dispositions.

---

## Review — 2026-08-26 — Verdict: NEEDS REVISION (Round 5 — verification pass)
Scope signal: L
Specialists: gameplay-programmer, game-designer, systems-designer, qa-lead, unity-specialist + creative-director (senior synthesis)
Blocking items: 5 | Recommended: 13 (~15 NICE-grade items noted, not in changeset)
Summary: Verification-only re-review confirmed ALL five Round-4 blocker dispositions and every batch rider landed (independent per-location citations from main review + all five specialists; zero regressed surfaces; independent math re-derivation concurred on T_reverse 0.832 s / band [0.52, 1.24], F5c starter values, catch invariants 4.375 & 5.05 < 5.5, chase ceiling 6.81). Design-level verdict SOUND: fantasy delivered structurally, gear ladder cannot invert within legal bands, anti-pillar discipline held, run retains a genuine niche via integral-of-exposure grading. The five new blockers are convergence-tail defects under the established pattern — normative surfaces fixed while prose/verification apparatus retains fossils — and have now migrated INTO the verification apparatus itself: AC-P9 conflated two oracles without declaring an Euler convention; the Round-4 adapter-side variant derivation named an input no harness seam publishes (undefined at zero velocity); the F3 knob rationale retained rejected-model vocabulary ("face-hold"); the Summary M-count missed M9; and the stride-retune gate implied headless enforceability of imported-asset metadata that no H.0.7 leg can verify.
Prior verdict resolved: Yes (all Round-4 dispositions verified substantively applied)

### Revision disposition (same session)

All 5 blocking items + full recommended batch FIXED in `player-third-person-controller.md` + `design/registry/entities.yaml` (user-approved Full-batch changeset):

| Blocker | Disposition (per-location citations) |
|---------|-------------|
| B1 Summary M-count stale at 8 | FIXED — Summary gate table row updated to "M1 … M9 \| 9" (Summary ADVISORY row) |
| B2 variant delivery names an unpublished input | FIXED per user decision **[a] State enum carries variant** — animator-parameter sentence rewritten: `State` int enum carries the EIGHT-value movement×variant pair {IdleStanding, IdleCrouched, CrouchFwd, CrouchBack, WalkFwd, WalkBack, RunFwd, RunBack} with forward/backward half resolved controller-side from C3's own threshold (`dot < −eps_d`) against TICK-ENTRY facing (**Visual/Audio animation bullet**, both sentences); **Variant-input delivery paragraph** replaced: zero-velocity-safe onset (directed variant on first moving tick), deceleration-tail flicker immune, adapter derives NOTHING from transform deltas (Round-4 mechanism struck as naming an input no seam publishes), snapshot still eight fields (AC-P22 intact), animator still query-free (AC-P14 intact) |
| B3 rejected-model vocabulary survives in F3 knob rationale | FIXED — rear_damp_floor rationale clause replaced: "≥ 0.40 lets the player steer freely throughout the reversal window, erasing the transient's cost identity" (**F1 knob rationale row**); no live "face-hold" token remains (grep appendix below) |
| B4 AC-P9 dual-oracle conflation | FIXED — ONE canonical oracle declared: smallest n such that Σ_{i≤n} v_eff(i)·dt over the H.0.4 telemetry series reaches the stride, eps_compare, no slack window; F5c closed form demoted to DOCUMENTATION-ONLY with the one-tick grid-boundary disagreement named (**AC-P9**); explicit-forward-Euler convention DECLARED in **H.0.12 shared utilities** (post-tick-i value, displacement billed v_eff(i)·dt); made exact by the **C7 movement sub-order pin** (dot-eval reads TICK-ENTRY facing → F1 slews v_eff → F3 advances facing second, w(dot) once per tick vs entry facing → displacement LAST as Δposition_i = v_eff(i)·dt — the billing convention shared by ledger and oracle), which also lands Recommended #2 |
| B5 stride-retune gate implies headless enforceability | FIXED per user decision **[a] re-route to import gate** — **Stride-retune gate paragraph**: authored cycle ≈ registry stride ±10 % is IMPORTED ASSET metadata, unverifiable headless; enforcement routed EXPLICITLY to the pre-M4 animation-import story (owner gameplay-programmer; evidence artifact measured clip cycle lengths under production/qa/evidence/; loud-fail, never silent compensation) mirroring the stand-probe/#18 evidence-routing pattern; doc states NO H.0.7 leg exists or is claimed |

**Recommended batch applied (13/13, per-location citations)**: (1) F5c ONE joint-sweep convention naming every swept axis — Walk ≈ [0.35, 0.78] s, Run ≈ [0.30, 0.69] s with worst legal slowest arming 3.2/5.0 + 5.0/(2×50) = 0.69 s (the Round-4 print had pinned accel_time at starter while sweeping other axes); solo-tuning sub-domain ≈ [0.32, 0.69] named (**F5c**) · (2) folded into B4's C7 movement sub-order pin (**C7**) · (3) publication-order asserting test `test_intra_tick_publication_order_transition_then_step` added to AC-P14; C7 justification repaired from "presupposed by AC-P21's bit-identical transcripts" to asserted-directly (replay canary cannot distinguish deterministically reordered publication) (**AC-P14 test list + C7**) · (4) AC-P14 payload ground truth split: timestamp/state/stride/remainder/publisher vs H.0.3 tap, post-movement position vs NEW H.0.4 debug planar-position accessor sampled after Tick returns; timestamp added to asserted field list (**AC-P14 + H.0.4 accessors**) · (5) H.0.13 call record gains anchor XZ position AT CALL TIME auditable against tick-entry pre-move position; #18 gate audit list gains anchor timing (**H.0.13**) · (6) crossfade sentence pins Animator.CrossFade NORMALIZED duration (seconds ÷ destination clip length, per-variant conversion adapter-side; Normal update mode matching render-rate tick; CrossFadeInFixedTime explicitly excluded) (**Visual/Audio clip-boundary bullet**) · (7) C6 inset parenthetical assigns roles: LOWER sphere center point1 raised implements the ground inset, point0's separate raise implements the TOP-extent margin — never a uniform shape lift (**C6**) · (8) #8 downstream dependency row: with step_offset pinned ≈ 0 (OQ1), certified routes must certify ZERO positive vertical seams on walkable paths, or ADR selects non-zero step_offset with anti-pillar justification (**Dependencies #8 row**) · (9) cadence-inversion disclosure: NEW constraint-table row (walk-max 2.93 vs run-min 1.56 steps/s legal corners — NOT GUARANTEED, DOCUMENTED); V_walk monitoring trigger extended to ANY stride_length_* band change; M9 runs TWO worst-corner configs (≈1.45× ratio corner + cadence-inversion corner) (**constraint table + V_walk rationale + M9**) · (10) M5 auto-stand-on-clearance filmed guard-visible (unprompted stance flip must read at gameplay camera distance — Pillar-2 risk made legible) (**M5**) · (11) M4 PREREQUISITE gains rig-type (generic vs humanoid) as third slot of the same pre-M4 decision (**M4**) · (12) H.2 preamble frame-count protocol shared by M1/M4: ≥60 fps, overlay visible, onset/resume observable definitions, thresholds derive-from-ACTIVE-config (**H.2 preamble**) · (13) provenance nits: GDD consumed-table V_chase row annotated "realized in-registry as rho_chase × runtime-max — starter product 7.50"; entities.yaml R_walk gains owned_by matching the GDD co-owner claim; V_walk constraint gains the cadence axis (**consumed table + entities.yaml**)

**User decisions** (grouped widget): B2 remedy [a] State-enum-carries-variant · B5 remedy [a] import-gate re-route · changeset scope [A] Full batch (5 blockers + all 13 recommended).

**Registry sync**: entities.yaml — V_walk constraint extended with cadence-inversion axis + trigger wording (rev round-5 note inline); R_walk gains `owned_by: "Player Noise GDD (co-owner; prototype-sourced via Perception F12/F13)"`.

### Rule-3 appendix (diff-derived evidence)

Post-changeset retired-token sweep, 2026-08-26, pattern `face-hold|0\.68|\[0\.34, 0\.56\]|5\.0/125|M1 … M8|Walk-idle Standing|presupposed by AC-P21|adapter derives the variant|ADAPTER derives` over `player-third-person-controller.md` → **1 matching line (:201)**, every occurrence inside INTENTIONAL historical-citation parentheticals quoting the retired prints to explain the repair; zero live claims. Sweep for live `[0.30, 0.68]`, `M1 … M8`, `Walk-idle Standing`, `face-hold` outside :201 → 0 matches. `entities.yaml` sweep for the same tokens → 0 matches. All 28 edits verified landed by exact-string Edit success (no fuzzy application).

**CD terminal bar for Round 6** (carried): full-composition diff-scoped pass, estimated ~90 % terminal; clean citations + NICE-grade-only residue = terminal, no Round 7.

---

## Review — 2026-08-26 — Verdict: NEEDS REVISION (Round 6 — diff-scoped verification pass)
Scope signal: L
Specialists: gameplay-programmer, game-designer, systems-designer, qa-lead + creative-director (senior synthesis); unity-specialist seat cut by user decision (C6 probe scope covered in Rounds 3–5 and cross-checked by remaining seats)
Blocking items: 4 (2 confirmed blockers + 2 CD-elevated gating) | Recommended: ~15 accepted-residue | NICE: ~13
Summary: Diff-scoped verification against the Round-5 disposition table confirmed ALL dispositions LANDED with per-location citations (independent multi-seat verification; registry sync verified incl. step_event schema mirror field-for-field; zero regressed prior-round surfaces; retired-token sweep independently reproduced clean). Design-level verdict SOUND — affirmed without qualification, unchanged from Round 5. The four gating items are convergence-tail defects of the established species — R5's fixes opened new surfaces whose downstream seams retained gaps: the B2 remedy resolved the variant where nothing could observe it (no animator delivery seam → G1), and the B4 oracle fix omitted the audible-state gate from its own billing identity (G2); plus the R5 provenance rider contradicted the operative pinned-V_chase model (G3) and the H.2 resume threshold collided with the backward-cruise definition (G4). CD ruled all three disagreements (oracle severity BLOCKING stands; B2 disposition PARTIAL-text/LANDED-closure; V_chase contradiction BLOCKING under the unfalsifiable-premise pattern) and set the Round-7 ratchet bar below.
Prior verdict resolved: Yes (all Round-5 dispositions verified substantively applied; one graded PARTIAL-closure → carried as G1)

### Revision disposition (same session)

All 4 gating items + 6 riders FIXED in `player-third-person-controller.md` + `design/registry/entities.yaml` (user-approved changeset; user decisions: G1 remedy **[B] dedicated polled animation feed**; batch scope **CD-scoped** — GP-R5 Idle SpeedRatio ÷0 contract and QA-R3 C2 priority tiebreak DEFERRED to implementation time as accepted residue):

| Item | Disposition (per-location citations) |
|---------|-------------|
| G1 animator delivery seam | FIXED per user decision [B] — C7 intro amended ("two GAMEPLAY channels…plus ONE visual-only polled feed") and new **Animation feed bullet** (:47): production accessor returning {`AnimState` 8-value enum, `v_eff`, `gamma`} computed at call time; sole delivery seam to the adapter; NOT in the eight-field snapshot (AC-P22 intact), NOT a gameplay channel; Idle-pair discriminator pinned (`IdleCrouched` iff published `stance` reads Crouched, E10-pose-truthful). Visual/Audio animation bullet: parameter renamed `State`→`AnimState` (name collision with snapshot `state` resolved); Variant-input delivery paragraph rewritten — feed named exclusive channel (mid-run flick flips deliver next poll, no state change required), adapter reads ONLY published seams |
| G2 AC-P9 oracle state gate | FIXED — oracle summand gated `Σ_{i≤n, state(i) ∈ {Walk, Run}} v_eff(i)·dt`; silent-state tails (Idle decel glide) billed to NEITHER oracle NOR ledger; billing identity restored as true rather than aspirational (**AC-P9**) |
| G3 V_chase provenance | FIXED — consumed-table row now reads "V_chase PINNED ABSOLUTE at 7.50 — historical derivation only: starter sized as rho_chase 1.2 × then-runtime-max 6.25"; non-auto-scaling premise explicit (keeps F2 solo ceiling + band-max DOCUMENTED row operative); `/consistency-check` routing note for registry/Guard-AI model agreement (**consumed table :321**) |
| G4 H.2 resume threshold | FIXED — resume = first frame `v_eff` returns to FULL state speed `S(state)` (forward cruise, post-convergence); backward cruise `V_state × backpedal_factor` explicitly NOT the threshold (**H.2 preamble :430**) |
| R1 AC-P22 enumeration | FIXED — debug-not-a-field list gains live planar world position AND the animation-feed accessor (**AC-P22**) |
| R2 eps_compare pin | FIXED — H.0.12 pins `eps_compare` ≤ 1e-9, strictly below `eps_d`, so tolerance can never mask the C3 boundary AC-P2 tests (**H.0.12 :366**) |
| R3 zero-vector guard scope | FIXED — E8 guard clause returns before ANY dot evaluation, BOTH consumers (speed path + variant resolver) downstream of the Idle short-circuit; AC-P23 aligned ("without EITHER dot consumer evaluating") (**E8 + AC-P23**) |
| R4 M8 frame reference | FIXED — audible cue lands on same frame as the EMITTING `step_event` (debug overlay field); guard-hearing parity routed to #3's design gate / first Integration story per F5g (**M8**) |
| R5 h_stand validator leg | FIXED — `h_stand > h_crouch` added to joint-constraint assert list, H.0.7 leg enumeration, and AC-P18 failing conditions (+ `test_validator_fails_on_stand_below_crouch_height`); entities.yaml `h_stand` entry gains the constraint annotation (**joint constraint + H.0.7 + AC-P18 + entities.yaml**) |
| R6 variant-resolution coverage | FIXED — H.0.14 extended (feed accessor injectable; spy records `AnimState`); AC-P24 gains three value-level legs: mid-run camera-flick flip without state change (feed-only delivery), direction-free Idle variant on release (E8 ordering), stance-pinned Idle discrimination incl. stand_blocked (**H.0.14 + AC-P24**) |

**Registry sync**: entities.yaml — `h_stand` constraint field added (JOINT validator-leg form, rev round-6 note).

### Rule-3 appendix (diff-derived evidence)

Post-changeset retired-token sweep, 2026-08-26, pattern `realized in-registry|active state's cruise speed|animator never queries|guard-noise event|\x60State\x60 \(int enum|Σ_\{i≤n\} v_eff` over `player-third-person-controller.md` → **0 matches**. New-content verification grep (`Animation feed \(polled|PINNED ABSOLUTE|state\(i\) ∈ \{Walk, Run\}|AnimState|test_validator_fails_on_stand_below_crouch_height|test_variant_flips_mid_run`) → matches at :47/:321/:334/:368/:396/:417/:426 as expected. All 11 GDD edits + 1 registry edit applied by exact-string Edit success.

### Accepted residue (CD dispositioned, non-gating — logged for Round 7+)

RECOMMENDED-grade owned items: AC-P14 cross-channel merge rule + independent ground-truth sources (**must-fix before test authoring**, owner qa-lead) · Idle SpeedRatio ÷0 contract (decide at implementation; freeze-last-ratio recommended) · C2 total-order priority tiebreak (enumerate at implementation) · AC-P3 tick-entry-facing pin · AC-P21 purpose softened to nondeterminism detection · P19–P23 home-file assignments · zero-allocation claim evidence route · stride rationale "ONLY" strike + ongoing step-density input to #3's design gate · AC-P18 test-name unbundling (12 conditions / 10 names). NICE-grade: F5c solo corner ≈0.32→0.31 print, a_max Range-column band, F5c axis inventory, M5 failure-consequence hook (crouch_time), gear-shift transition feel item, fantasy-copy clarifying clause, M1 tolerance scaling note, E9 event-clustering sentence, crossfade min-cap rationale, capture-window animator feed sentence, snapshot_rebuilt proxy observable, H.0.13 derivation chain, crossfade non-interference owner line.

### ROUND-7 RATCHET BAR (pre-committed by CD — binding)

Round 7 verifies ONLY this changeset (diff-scoped against the table above). **APPROVED-terminal iff**: (1) all ten items land with per-location citations + grep appendix per Rule 3; (2) zero regressed prior-round surfaces; (3) any new finding grades NICE unless it is a direct mutation of a Round-7 hunk AND blocks an operative or verification surface — everything else becomes accepted residue permanently; (4) criteria met ⇒ APPROVED-terminal, no Round 8. Round 7 is the LAST full-composition panel; afterward review is exception-based on mutated hunks only.

---

## Review — 2026-08-26 — Verdict: APPROVED (Round 7 — diff-scoped verification pass, TERMINAL)
Scope signal: L
Specialists: gameplay-programmer, game-designer, systems-designer, qa-lead + creative-director (senior synthesis); unity-specialist seat cut carried per Round-6 user decision
Blocking items: 0 | Recommended: 4 (all accepted residue permanently per ratchet-bar criterion 3)
Summary: Diff-scoped verification against the Round-6 disposition table returned the terminal verdict: ALL TEN items (G1 animation feed, G2 oracle state gate, G3 V_chase PINNED ABSOLUTE, G4 resume threshold, R1–R6 riders) verified LANDED by all four seats independently (40/40 seat-checks; per-location citations reproduced by direct read/grep, not trusted from the log); Rule-3 retired-token sweep clean (0 matches, reproduced independently by three seats) and new-content grep hits exactly at :47/:321/:334/:358/:368/:396/:417/:424/:426/:430/:441; registry sync field-for-field (`h_stand` constraint rev round-6 note; `step_event` schema mirror). ZERO regressed prior-round surfaces across audited adjacencies (C7 publication order, F5g pins at all four anchors, C6 probe semantics, C8/E7 respawn, F4 gamma/stance, gear ladder/constraint table, C0 anti-pillar cap, M protocols, AC-P16/P21/P12, H.0.7 enumeration; Summary gate recount 25 BLOCKING / 1 smoke / 9 manual correct). Digit-exact rederivations concurred: G2 billing identity TRUE across an eight-class tick sweep (identity is oracle==ledger; decel-glide distance deliberately free per F5 rule (b)); eps hierarchy coherent without forcing double precision; SpeedRatio never degenerate (min legal cruise 0.84 > 0); F5c corners exact; catch/chase invariants 4.375 & 5.05 < 5.5, 6.875 ≤ 7.50, solo ceiling 6.81 rounded down conservative. Fantasy lens: every operative hunk PRESERVES the stated Player Fantasy; anti-pillar held. Convergence complete after seven rounds — blockers 9→8→8→5→5→4→0.
Prior verdict resolved: Yes

### CD adjudication (ratchet-bar two-condition elevation test)

Zero elevations: QA-F1 (eps_compare pin unverifiable — a meta-verification gap; suite executes correctly and discriminates; precedent R6-D1/G2 reserved elevation for executed-but-WRONG verification layers) · GP-R3/QA-F2 (AC-P23 causation clause unobservable headless / unpublishable `w` token — named test authorable and discriminating; observable contract fully testable; `w` transitively bounded as pure function of finite inputs) · GP-R1 (H.0.12 convention sentence ungated — summation domain normatively declared at AC-P9) · GP-R2 (registry chase-model language untouched this round fails condition 1; :321 routing tense fails condition 2 — operative fact triple-anchored at F2/band-max row/:321). All four seats supported APPROVED-terminal before synthesis; CD spot-checked every seat claim against the files before endorsing.

### Permanent accepted-residue ledger (never re-litigated)

RECOMMENDED-grade: GP-R1 H.0.12 billing qualifier · GP-R2 yaml chase_speed_ratio/rho_chase legacy product-language + :321 routing tense · GP-R3/QA-F2 AC-P23 causation clause + `w` finite-assert token · QA-F1 eps_compare policy-value verification path · SD-N4 no absolute-height validator legs (pre-existing; h_stand 1.05/h_crouch 0.99 passes legs yet floats stand origin above capsule top — bands advisory-only). NICE-grade: mid-blend-block IdleStanding discriminator edge · "SOLE seam" vs "feed-and-snapshot" phrasing · immediately-vs-next-poll delivery wording · eps shared-arithmetic-width mirroring hazard · Idle ÷0 exposure spans decel tail · F5c solo-min print ≈0.32 (computes 0.3137) · residue-count drift (log printed 12/10; actual 13/11+1) · yaml:483 rationale overstates Perception ray-origin desync (eye_offset LOCKED, stance gamma-driven) · AC-P14 inline marker pending · "telemetry series" vs synchronous-getter phrasing · /consistency-check routing lacks owner/cadence.

### Standing obligations surviving into implementation

1. AC-P14 cross-channel merge rule + independent ground-truth sources land BEFORE any test authoring touches publication-order or payload tests (owner qa-lead — see Round-6 residue list; unchanged).
2. Expose CompareTolerance constant + one harness self-test leg asserting ≤ 1e-9 and < eps_d when implementing H.0.12 (closes QA-F1 code-side).
3. Run /consistency-check to reconcile registry chase-model language against the controller's pinned-absolute model (documented G3 corollary; CD dissent flags live risk if skipped).
4. Unchanged evidence routings: #18 physics gate (stand-probe pins, pre-milestone-0, technical-director) · pre-M4 animation-import story (stride retune ±10 %) · ADR package before first Logic story (OQ1).

CD dissent recorded for history: approval stands despite the known entities.yaml model contradiction solely because it is logged, owned, and routed; skipping /consistency-check converts that residue into live risk.

**ROUND-7 RATCHET BAR: SATISFIED — APPROVED-terminal. NO ROUND 8.** Future review of this GDD is exception-based on mutated hunks only.
