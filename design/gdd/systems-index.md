# Systems Index: Whisper Ward

> **Status**: Under Review (fresh decomposition, 2026-08-17)
> **Created**: 2026-08-17
> **Last Updated**: 2026-09-20
> **Source Concept**: design/gdd/game-concept.md (APPROVED conditional — review #24, saturation)
>
> **Cross-document authority note (lifecycle / attempt_epoch).** The canonical
> lifecycle transition table for `attempt_epoch` increments — covering death,
> capture, respawn, segment reset, full-room restart, scene reload, new
> playable attempt, pause/resume, and pool/unpool — is authoritative in
> `design/registry/entities.yaml` and reproduced verbatim in
> `player-noise.md §Ownership`. Player Noise, Perception, Guard AI FSM, and
> Player Movement & Hide all reference the same table; no file owns a distinct
> version. Status for row 3 (Player Noise) is now **Approved**; this note is
> additive only and does not change any system status.

---

## Overview

Whisper Ward is a third-person stealth/adventure game where the player infiltrates a
moonlit, guard-patrolled facility and outwits a self-written thinking AI. The core
loop — *observe → route → act → slip past* — is driven by a **suspicion meter**
(two-component: sustained-LOS accumulator + residual wariness) that reads fairness
and learnability instead of reflexes. The game is built around four pillars:
Thinking Enemies (a hard 3-state FSM core — Patrol/Investigate/Chase), Fair
Mind-Challenge (every detection must be readable), You Create the Situation (two
manipulation verbs — Burst noise-maker MVP, Lure Target), and Visible Intelligence
(grade legibility + Suspicion Attribution). The AI family (Perception, Guard AI FSM,
Alert propagation, Player Noise, Camera system) is the centerpiece; the grade operator
and telemetry feed a mastery loop (S/A/B segment grading) and the core-hypothesis
claims (learnability, fairness, tool adoption). Target platforms: PC (Windows) + WebGL
demo. See the concept's review history for the fully-pinned contract surface
(24 adversarial review rounds, saturated at its round-23 state).

---

## Systems Enumeration

| # | System Name | Category | Priority | Status | Design Doc | Depends On |
|---|-------------|----------|----------|--------|------------|------------|
| 0 | Game Concept | Meta | — | **Approved** (2026-08-17, review #24 — conditional, saturation) | `design/gdd/game-concept.md` | — |
| 1 | Guard AI (FSM core) | AI & Perception | MVP | **Approved** (2026-08-24 — round 8 TERMINAL CONFIRMATION PANEL: charter sweeps clean (A–F diff audit exact, B1–B10 regression 10/10, R6-B1..B4 clean), zero HIGH; sole MED (R8-M1: AC-FSM-11(c) watchdog ceiling omitted the new leg's post-flip commit span) ruled test-arithmetic by CD and repaired same session via CD-sanctioned micro-amendment **rev 4.1** — five-step verification bar V1–V5 passed ⇒ APPROVED final, after 8 review rounds) | `design/gdd/guard-ai-fsm.md` | Perception, NavMesh, Event bus |
| 2 | Perception systems (vision + hearing + meter) | AI & Perception | MVP | **Approved** (2026-08-18 — confirmation re-review of revision 3.4 in lean mode; all 8 J-gates J1–J8 verified landed; J1 suppression rule re-scoped per-tier closes cleanly; registry lock-step re-established via /consistency-check 2026-08-18 — schema direction_note corrected to the per-tier suppression rule) | `design/gdd/perception.md` | Player Controller, NavMesh, Physics, Event bus |
| 3 | Player Noise (`NoiseEmitter`) | Gameplay | MVP | **Approved** (2026-09-20 — re-review panel confirmation; all 9 prior blockers and 4 cross-file formula/registry synchronization blockers resolved; monotonic budget formula $t_{reanchor} = \min(t_{investigate\_max}, \max(t_{remaining}, t_{reanchor\_floor}))$ bound per AC15b-MVP and synchronized across entities.yaml, guard-ai-fsm.md, and player-noise.md; missing ballistics parameters input_buffer_window_s and throw_velocity_snap_threshold and ceiling t_investigate_max registered; diagnostic codes aligned; git diff --check clean ⇒ APPROVED final) | `design/gdd/player-noise.md` | Player Controller, Physics, Event bus, Input |
| 4 | Alert propagation | AI & Perception | Vertical Slice | Not Started | — | Perception, Guard AI, Event bus |
| 5 | Player Movement & Hide (`HideSpot`) | Gameplay | Target | **In Review** (2026-09-01 — full review returned MAJOR REVISION NEEDED; revision decisions lock Target scope, occupancy-only HideSpot ownership, non-authoritative hunches, rev 4.2 FSM dependency, overlap rejection, formal AC10 escape, and accessibility fallback; pending fresh full re-review) | `design/gdd/player-movement-hide.md` | Player Controller, Physics, Event bus, Level |
| 6 | Camera system (static, low-scope) | Gameplay | Target | Not Started | — | Perception, Alert propagation, Level, Event bus |
| 7 | Suspicion Meter / Grade operator | Gameplay | MVP | Not Started | — | Perception, Guard AI, Event bus, Save/Session state |
| 8 | Level / Content | Content | Vertical Slice | Not Started | — | NavMesh, Scene/Asset mgmt, Event bus |
| 9 | Win/Lose & Respawn | Meta | Target | Not Started | — | Guard AI, Grade, Player Controller |
| 10 | Suspicion Attribution telemetry (FSM trace) | UI & Feedback | MVP (dev-only in MVP) | Not Started | — | Guard AI, Perception, Event bus |
| 11 | Player Third-Person Controller *(inferred)* | Core | MVP | **Approved** (2026-08-26 — Round 7 `/design-review` TERMINAL: last full-composition panel per the Round-6 binding ratchet bar; all ten Round-6 changeset items verified LANDED by 4-specialist panel (40/40 independent seat-checks, per-location citations, Rule-3 grep appendix clean both directions), registry sync field-for-field, ZERO regressed prior-round surfaces, digit-exact rederivations concurred (G2 billing identity true across an eight-class tick sweep; F5c corners; catch/chase invariants 4.375 & 5.05 < 5.5, solo ceiling 6.81); ZERO blocking findings — convergence complete after 7 rounds (blockers 9→8→8→5→5→4→0); design-level verdict SOUND affirmed unchanged since Round 5; fantasy-delivery PRESERVES on every operative hunk; CD adjudicated zero elevations under the two-condition bar test ⇒ **APPROVED-terminal, NO ROUND 8** — future review exception-based on mutated hunks only. Accepted-residue ledger permanent in review log; surviving implementation obligations: AC-P14 cross-channel merge rule BEFORE test authoring (qa-lead), CompareTolerance self-test leg at H.0.12, /consistency-check chase-model reconciliation vs entities.yaml legacy language) | `design/gdd/player-third-person-controller.md` | Input, Physics, Event bus |
| 12 | NavMesh / Pathfinding *(inferred)* | Core | MVP | Not Started | — | Physics, Scene/Asset mgmt |
| 13 | Audio & UI feedback *(inferred)* | UI & Feedback | MVP | Not Started | — | Player Controller, Event bus, Level |
| 14 | HUD / UI *(inferred)* | UI & Feedback | Vertical Slice | Not Started | — | Grade, Telemetry, Event bus |
| 15 | Event / Messaging bus *(inferred)* | Core | MVP | Not Started | — | — |
| 16 | Save / Session state *(inferred)* | Persistence | Target | Not Started | — | Grade, HUD |
| 17 | Scene / Asset management *(inferred)* | Core | Vertical Slice | Not Started | — | Event bus |
| 18 | Physics & collision config *(inferred)* | Core | MVP | Not Started | — | — |
| 19 | Input system *(inferred)* | Core | MVP | Not Started | — | — |
| 20 | Camera (Cinemachine rig) *(inferred)* | Core | MVP | Not Started | — | Player Controller, Input |
| 21 | VFX / telegraph layer *(inferred)* | UI & Feedback | Target | Not Started | — | Guard AI, Perception, Event bus |

---

## Categories

> **Player Noise review amendment (2026-08-28):** the source GDD now requires direct contracts with Camera/Aim and Save/Session in addition to Player Controller, Physics, Event bus, and Input. The revised acceptance surface also separates MVP one-guard behavior from Target-tier HideSpot/corroboration and adds the shared virtual-clock, epoch, and level-certification gates. The row above remains the historical review record; its status is superseded by the latest Player Noise document header and review output.

| Category | Description | Typical Systems |
|----------|-------------|-----------------|
| **Core** | Foundation infrastructure everything plugs into | Player controller, input, physics, NavMesh, event bus, scene/asset mgmt, camera rig |
| **AI & Perception** | The thinking-enemy family — the game's centerpiece | Guard AI FSM, perception, alert propagation |
| **Gameplay** | Systems that make the game fun | Player noise, movement & hide, camera system, grade/meter |
| **Content** | The facility and its spatial-functional zones | Level/Content |
| **UI & Feedback** | Player-facing information and legibility | HUD, telemetry, audio feedback, VFX/telegraph |
| **Persistence** | Save state and continuity | Save/session state |
| **Meta** | Systems outside the immediate loop | Win/lose flow, game concept, grade-mastery meta-layer |

---

## Priority Tiers

| Tier | Definition | Target Milestone | Design Urgency |
|------|------------|------------------|----------------|
| **MVP** | Required for the one-room core loop to function and for core-hypothesis claims 1-3 | One room, one guard (4-6 wk) | Design FIRST |
| **Vertical Slice** | Required for one complete, polished area with coordinated behavior | First room + entrance/exit, 2 guards + alert (target ~4 wk) | Design SECOND |
| **Target** | Required for the committed full level (2-3 guards, cameras, hide spots, Lure, HUD) | One full level (~10-12 wk, planning datum 7 segments) | Design THIRD |
| **Full Vision** | Polish, narrative layer, minimal BT showcase, forced-chase set-piece | Release (~14-16 wk) | Design as needed |

---

## Dependency Map

### Foundation Layer (no dependencies)

1. **15 Event/Messaging bus** — every producer/consumer (noise, alerts, grade events, trace) talks through it; it breaks both cycles below.
2. **18 Physics & collision config** — foundational pins (`queriesHitTriggers`, collider layer masks, Linecast contract) land here — TD pinned before milestone-0.
3. **19 Input system** — WASD/crouch/walk/run/throw action map; new Input System.

### Core Layer (depends on Foundation)

1. **11 Player Third-Person Controller** — depends on: 19 (input), 18 (physics), 15 (events). **Bottleneck** — 6 systems hang off it.
2. **12 NavMesh / Pathfinding** — depends on: 18 (geometry/layers), 15. **Bottleneck** — the entire catch contract is navmesh-defined.
3. **20 Camera (Cinemachine rig)** — depends on: 11 (follow target), 19 (orbit input).
4. **17 Scene / Asset management** — depends on: 15 (load events). Feeds content / segment loading.

### Feature Layer (depends on Core)

1. **2 Perception** — depends on: 11 (player state), 12, 18, 15. **Bottleneck** — 5 systems consume it.
2. **3 Player Noise (`NoiseEmitter`)** — depends on: 11 (movement state), 18 (occlusion), 15.
3. **5 Player Movement & Hide** — depends on: 11, 18, 15.
4. **8 Level / Content** — depends on: 12 (bake + budget verification), 17, 15. Consumes guard/perception budgets.
5. **6 Camera system (static)** — depends on: 2, 4, 15, 8.

### Feature II Layer (depends on Feature)

1. **1 Guard AI FSM** — depends on: 2 (perception events), 12 (navmesh pathing), 15. **Bottleneck** — core-loop heart; everything grades on its trace.
2. **4 Alert propagation** — depends on: 2 (detection events), 1 (guard reactions), 15. Feeds 6.
3. **21 VFX / telegraph** — depends on: 1 (state telegraphs), 2 (vision-cone meshes), 15.

### Presentation Layer (depends on Features)

1. **10 Suspicion Attribution telemetry** — depends on: 1 (FSM trace), 2, 15.
2. **7 Suspicion Meter / Grade** — depends on: 2 (two-component inputs), 1 (escalation events), 15, 16.
3. **13 Audio & UI feedback** — depends on: 11, 15, 8.
4. **14 HUD / UI** — depends on: 7, 10, 15.

### Polish Layer (depends on everything)

1. **9 Win/Lose & Respawn** — depends on: 1, 7, 11.
2. **16 Save / Session state** — depends on: 7 (grade records), 14 (best records).

---

## Recommended Design Order

Design order combines dependency sort + priority tier. **Bottleneck systems first.**
Each system's GDD should be completed and reviewed before starting the next, though
independent systems at the same layer can be designed in parallel.

| Order | System | Priority | Layer | Agent(s) | Est. Effort |
|-------|--------|----------|-------|----------|-------------|
| 1 | **2 Perception** *(carries C-2/C-4/C-6 + prototype learnings; time-boxed per producer)* | MVP | Feature | game-designer, systems-designer | L |
| 2 | **1 Guard AI FSM** *(C-4 trace owner)* | MVP | Feature II | ai-programmer, game-designer | L |
| 3 | **11 Player Third-Person Controller** | MVP | Core | gameplay-programmer | M |
| 4 | **5 Player Movement & Hide** | MVP | Feature | game-designer | M |
| 5 | **3 Player Noise (`NoiseEmitter`)** | MVP | Feature | game-designer | M |
| 6 | **7 Suspicion Meter / Grade** *(C-6 forgiveness_floor)* | MVP | Presentation | systems-designer | M |
| 7 | **12 NavMesh / Pathfinding** | MVP | Core | ai-programmer | M |
| 8 | **18 Physics & collision config** *(pre-milestone-0 pins)* | MVP | Foundation | technical-director, unity-specialist | S |
| 9 | **19 Input system** | MVP | Foundation | gameplay-programmer | S |
| 10 | **15 Event / Messaging bus** | MVP | Foundation | lead-programmer | S |
| 11 | **13 Audio & UI feedback** | MVP | Presentation | gameplay-programmer | S |
| 12 | **20 Camera (Cinemachine rig)** | MVP | Core | gameplay-programmer | S |
| 13 | **4 Alert propagation** | Vertical Slice | Feature II | ai-programmer | M |
| 14 | **8 Level / Content** *(carries C-1/C-3)* | Vertical Slice | Feature | level-designer | L |
| 15 | **14 HUD / UI** | Vertical Slice | Presentation | ui-programmer, ux-designer | M |
| 16 | **17 Scene / Asset management** | Vertical Slice | Core | engine-programmer | M |
| 17 | **6 Camera system (static)** | Target | Feature | game-designer | M |
| 18 | **21 VFX / telegraph** | Target | Presentation | technical-artist | M |
| 19 | **16 Save / Session state** | Target | Persistence | engine-programmer | S |
| 20 | **9 Win/Lose & Respawn** | Target | Polish | game-designer | S |

> **Gate note**: C-1/C-3 bind the **Level GDD** (before first hide-spot build: zero-hang
> confinement, hide-spot guard-reachability, per-segment pressure-event cadence).
> C-4 binds the **trace protocol before milestone-0** (records Investigate-threshold
> crossing + per-escalation trigger cause/position; reconcile concept lines 231/238).
> C-5 is a **BLOCKING pre-milestone-0 dependency**: `design/qa/prototype-playtest-plan.md`
> must exist and pass qa-lead review before the engine milestone-0 build.

---

## Circular Dependencies

- **[1 Guard AI] ↔ [4 Alert propagation]** — guards act on alerts; alerts arise from
  guards detecting. **Resolution**: Alert is a *message* on the Event bus (15), not a
  direct system call — Guard AI consumes alert *events*, Alert propagation consumes
  detection *events*. No direct system-to-system reference.
- **[1 Guard AI] ↔ [2 Perception]** — FSM drives perception sampling cadence; perception
  reports sightings back. **Resolution**: by contract, perception outputs *events*
  ("LOS gain/break", "noise heard"), never system references. The FSM reads the event
  stream; perception never calls into the FSM.

---

## High-Risk Systems

| System | Risk Type | Risk Description | Mitigation |
|--------|-----------|-----------------|------------|
| **1 Guard AI FSM** | Technical + Design | Densest contract surface in the project — 25 review rounds of pinning (catch contract, give-up windows, hide-spot suspension, caps). FSM correctness is the grade heart. | GDD-first with the full C-delegation set; milestone-0 prototype validates FSM terminate-in-every-state; unit tests for state transitions. |
| **2 Perception** | Design | Two-component meter semantics (accumulator/residual/inverse-distance charge/confirm-window) — fairness contract vs tuning fragility. | GDD first in design order, time-boxed; prototype learnings carried (distance falloff, ~4s investigate, noise radii); AC(d) sweep verification. |
| **11 Player Controller** | Scope | "First-time 3D plumbing" — the concept's named silent time sink. | Scope discipline (anti-pillar: no jump/climb/lean); MVP-room-proven before any level work. |
| **12 NavMesh / Pathfinding** | Technical | Entire catch contract is navmesh-defined (path-arrival, backstop, partials, off-mesh). Bake/layer config mistakes silently break fairness. | AC(d) editor-time sweep with the committed input set; agent-parameterized `CalculatePath` sole path source. |
| **18 Physics & collision config** | Technical | `queriesHitTriggers`, collider layer-mask separation, Linecast contract — pins TD requires before milestone-0. Wrong config reproduces the through-wall catch / trigger-blocked ray classes. | Pinned at Technical Setup; ADR; pre-milestone-0 gate asserts. |
| **8 Level / Content** | Scope | Binding budget band (7-9 segments, S ∈ [32.14, 41.38], C-3 pacing). Level authoring velocity is the timeline's constraint. | Segment count is the first scope dial (cut segments before AI depth); Level GDD carries C-1/C-3; per-segment geometry budgets committed. |

---

## Progress Tracker

**Game Concept** — `design/gdd/game-concept.md` — **Approved** (2026-08-17, review #24 — conditional, saturation). Approval NOT a milestone-0 unblock. Remaining pre-milestone-0 gates: `design/qa/prototype-playtest-plan.md` (C-5, BLOCKING), physics project pins, NUnit framework confirmation.

| Metric | Count |
|--------|-------|
| Total systems identified | 21 |
| MVP systems | 13 |
| Vertical Slice systems | 4 |
| Target systems | 4 |
| Design docs started | 3 |
| Design docs reviewed | 3 |
| Design docs approved | 1 |
| High-risk systems | 6 |

---

## Next Steps

- [x] Concept approved (saturation, conditional)
- [x] Gate check Concept → Systems Design passed (CONCERNS/advanceable, 2026-08-17)
- [x] Systems enumeration + dependency map + priorities approved by user
- [x] Systems index populated and written (this file)
- [ ] Design MVP-tier systems first — **`/design-system [perception]` (order 1)**, then Guard AI FSM (order 2)
- [ ] Run `/design-review` on each completed MVP GDD (fresh session recommended)
- [ ] Author `design/qa/prototype-playtest-plan.md` + qa-lead approval (**BLOCKING before milestone-0**, C-5)
- [ ] Pin physics/collision config + NUnit framework before milestone-0
- [ ] Run `/gate-check systems-design` when all MVP GDDs are complete (triggers CD-SYSTEMS + TD-SYSTEM-BOUNDARY director gates)
- [ ] Validate highest-risk systems (Guard AI, Perception) with `/vertical-slice` before committing to Production

---

*Populated by `/map-systems` (2026-08-17). Design order 1 is Perception — carries C-2/C-4/C-6 pins and the prototype learnings (distance-based charge falloff, ~4s investigate give-up timeout, noise radii). Producer time-box note: Perception GDD scope = MVP-committed tuning set, not the full Target surface.*
