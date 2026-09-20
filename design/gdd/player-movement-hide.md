# Player Movement & Hide (HideSpot)

> **Status**: Approved (2026-09-20 — confirmation re-review in lean mode; all review blockers and recommendations verified resolved; D1 parameterized standoff, D2 joint config validation, D4 through-spot escape geometry, corridor parallel props, pure-pivot translational lock, and decoupled headless CI acceptance criteria clean ⇒ APPROVED final)
> **Author**: game-designer + user (full review + senior revision panel)
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Pillar 2 (Fair Mind-Challenge) — primary; Pillar 4 (Visible Intelligence); supports Pillar 3

## Overview

The **Player Movement & Hide** system is the player's survivorship layer of the stealth
trio — the mechanism that makes *crouch-to-hide* a genuine escape instead of a
corner-stalemate. It owns one named type, `HideSpot`: an authored 3D trigger volume (the
zone's interior) that, while the player is inside it, makes them **not perceptible to any
guard's vision LOS** — a real, complete line-of-sight break, not a partial-reduce. It
composes with the full AI contract: hiding is a true break that the confirm-window and
re-sight rules respect, so a hidden player is untraceable by sight — **unless a guard
witnessed them enter** (a *witnessed entry*), in which case the spot becomes that guard's
last-known-position and the capture contract takes over at the spot's front, so a hide
spot is **never capture immunity**.

Beneath the player-facing fantasy, the system is a tightly-constrained data layer: every
`HideSpot` carries an authored `spot_front_anchor` (the approachable face where a guard
halts to verify), and every spot the level can author within the certified-route vicinity
must satisfy three Level-GDD pins that the AI contract depends on — guard-reachability (a
NavMesh path from the guard to the spot front exists), an interior depth no deeper than the
guard's reach-and-verify envelope, and an interior vertical offset safely below the
committed |ΔY| tolerance. These pins are what close the "no capture-immunity" hole: a chase
whose last-known-position is a hide spot always resolves at the spot — catch or
confirm-empty — never hangs forever.

The system therefore serves both planes at once: to the player it is the heart of the
"None the wiser" fantasy (Pillar 2); to the AI it is a predictable, guard-reachable
contract surface (Pillar 4) whose authored geometry is verified as part of the level's
AC(d) certification sweep. HideSpot is **Target scope**, not part of the smallest MVP
room; the MVP fixture therefore does not need to contain a HideSpot. Design order 4 —
it sits directly on the Approved Player Controller's published state (stance, movement
events) and is consumed by the Approved Perception and Guard AI FSM systems.

*Provisional assumptions — Physics (#18), Event bus (#15), Level (#8) are undesigned.
HideSpot zones use the pinned trigger setup (`queryHitTriggers` OFF, dedicated layers) the
Physics GDD will finalize. Zone entry/exit events flow on a topic-based event stream the
Event bus GDD will finalize. The three authored pins (guard-reachability, interior depth,
vertical offset) are defined here as contract; their per-spot values are authored in the
Level GDD.*

## Player Fantasy

**The emotion this serves: mastery-through-invisibility.** The fantasy anchor is the exact
beat the concept names — *"the moment a guard walks past your hiding spot, none the wiser"*
— delivered as the reward for correct planning (Pillar 2: Fair Mind-Challenge) and legible
AI behavior (Pillar 4: Visible Intelligence). It is **not** a survival-horror "where do I
hide" tension; it is a *planner's* relief — the player set up the situation, read the
patrol, timed the entry, and the world confirms their read by walking past.

The **risk inversion** is what makes the fantasy work: hiding is powerful (a true LOS break
— sight is fully disabled), which is why the game hedges it with the one thing that defeats
it — a **witnessed entry**. So the fantasy is honest: *a spot you reached unseen is
sanctuary; a spot a guard saw you dive into is a trap you must escape before their approach
lands.* The player learns both halves — that a clean entry is safe and that a sloppy entry
converts the sanctuary into a last-known-position under active capture. This is the same
fairness contract as the meter: a loss at a hide spot is always readable ("I let him see me
dive in"), never cheap (Pillar 2).

The **telegraph** (the guard's commit-to-verify-this-spot kneel-and-verify beat) converts
the threat from invisible to readable: before the catch resolves, the player sees the guard
commit to the spot they're in, and the last half-second is a meaningful choice — break for
the pre-scouted back exit, or stay and pray the confirm-empty pass resolves. That
agent-visible intent is Pillar 4 in its purest form: the AI's cognition renders as a
committed, anticipatable action, not a randomizer.

**Readability contract (revision note 2026-08-26 B1/D3 + 2026-08-27 7-pin re-review + 2026-09-20 CD synthesis UPHELD with sharpening):** a loss at a hide spot is *always readable* — including the gravest case, a dive that crossed no Investigate threshold (no meter signal at entry, `a_pre_break ≥ threshold_applied` via `min(k/d,rate_max)*dt` and `T_entry(R)=max(T_base - k_res*R,0.2*T_base)`) yet is still witnessed and captured. The design deliberately does **not** leak which dives were witnessed via a UI meter or a dive-instant "he saw you" ping (that would collapse the "did they see me dive?" tension and, on the inverse face, leak perfect safety when no ping appears — collapsing P2/P4). Instead the **telegraph carries the whole honesty contract**:

- **Commit-tier discrimination (2 poses, not 1):** `HideSpotFront` Investigate-tier inspect = **standing lean/scan** at `spot_front_anchor` (~1.0 s, torch sweep, head bone height $\ge 1.5\text{ m}$, no kneel); Chase-tier or witnessed sub-threshold hold = **committed kneel** (1.5 s, distinct anim, head bone height $\le 1.1\text{ m}$). A hunch (noise/alert, Investigate with no `witnessed_entry_authority`) **never kneels** — by FSM M4 gate — so kneel itself discriminates witnessed vs hunch; both tiers share single-clock `gate ∧ timer≥t_catch ∧ dwellComplete` but read differently for the full dwell.
- **Commitment onset and transit demeanor at detection:** on witnessed detection the guard orientation snaps to the spot and movement retargets before transit, so the causal link is readable rather than arriving as a surprise after 4–6 s. The guard's transit gait is tier-discriminated: a Chase-tier approach uses a locked-sprint alert posture focused directly on the spot aperture, whereas an Investigate-tier approach uses an alert searching jog with lateral head turns. A bark, spoken line, or cause-revealing ping is **not** required and must not be used as authoritative detection feedback.
- **Enclosed prop feedback & diegetic audio contract:** In enclosed spots (wardrobes, lockers, dumpsters), the player cannot rely on third-person line-of-sight to see the guard outside. To preserve Pillar 2 readability and prevent sensory blindness:
  - *Visual Peep Slats / Door Cutouts:* Enclosed props feature door slats or mesh grates allowing the player to observe exterior guard silhouettes, flashlight sweeps, and the kneel/lean pose from inside.
  - *Tier-Discriminated Diegetic Audio Palette:* Unwitnessed hunch = standard footstep halt and faint electronic flashlight hum/sweep (~1.0 s, zero threat); Witnessed dwell = heavy aggressive boot halt, prop-appropriate strike (metallic door latch rattle for lockers, wood creak for wardrobes, floor scuff for low furniture, hollow resonance for vents), and a low-frequency dread stinger (1.5 s dwell, active capture threat).
  - *Directional Closed Captions:* Localized directional captions (`[Heavy footsteps halt]`, `[Latch rattles aggressively]`, `[Flashlight beam hums]`) support players with audio muted or hearing disabilities.
- **Threshold visibility pre-dive (Perception-owned):** a wary guard's residual-lowered `T_entry` is readable via the sinking threshold marker *before* entry, so 1 vs 4 tick window (0.30 vs 0.40 at point-blank with `T_sample 0.2–0.5`) is not a meter lottery — closes hedged-power feel.
- **Onboarding decomposition (3 beats / 2 segments, not 1 room 5-way):** Beat A clean still → walk-past (LOS break), Beat B clean + walk-inside → hunch stand-look walk-away (noise≠capture, stillness-is-the-price), Beat C graze dive under wary guard → kneel+bolt (sub-threshold when wary, sinking marker visible). The player then has the full approach-transit + dwell to read the tell and plan the bolt. The readable *cause* is the committed, tier-discriminated, audio-backed approach starting at witnessed detection, not a hidden accumulator value.

**Reference anchors**: Dishonored's hiding is ho-hum because it's everywhere and never
threatens; the *tension* comes from Splinter Cell's conviction that a spot is only as safe
as the entry — the memorable moments are the "did they see me dive?" beats. Alien:
Isolation's hiding is survival-fear; ours is a *surgical* hide — you chose it, you set it
up, and the payoff is the passing silhouette.

Alignment: Pillar 2 (fair, readable, planned escape) · Pillar 4 (visible intent via
telegraph) · supports Pillar 3 (hide composes with Burst/Lure to create situations).

## Detailed Design

### Core Rules

1. **`HideSpot` is an authored 3D trigger volume** (ProBuilder blockout; Physics layer pinned
   for hide-spot zones under #18). Each spot has a `spot_front_anchor` (authored world
   position on its approachable face — the FSM's M4 hold datum) and a single
   **`interior_position`** (the spot's reference point; the player occupies it while hidden).
   The spot owns a **`state: {Empty, Occupied}`**.

2. **LOS invisibility and pure-pivot interior locomotion.** While a player is inside a spot (trigger contains
   the player capsule) and the spot's state is `Occupied`, the player is **not perceptible
   to any guard's vision LOS** — a genuine, complete LOS break. Perception consumes this as
   a published zone fact; sight accumulation toward Investigate/Chase does not advance; on
   LOS break the accumulator fully resets (Perception R1). *This is the `HideSpot`-owned
   rule; Perception owns the ray geometry.*
   - **Phase 2 / Phase 3 Perception ordering:** In the 4-phase virtual tick execution pipeline,
     when the player enters a spot trigger in Phase 2, trigger contact is staged synchronously.
     In Phase 3, `Perception.CollectFacts()` evaluates exposure and line-of-sight *at the entry
     moment* before applying the LOS invisibility mask. If an eligible guard had clear LOS to
     the player crossing into the spot, `witnessed_entry_authority` is published with an
     immutable `entry_id`. Once the player is inside and state is `Occupied`, the LOS invisibility
     mask takes full effect.
   - **Interior Locomotion Clamp / Pure Pivot Zone:** While inside an `Occupied` spot, character
     translational velocity is clamped to zero ($\Delta \vec{p} == \vec{0}$, $\pm 1\times 10^{-5}\text{ m}$). Analog stick or directional input rotates the
     character model and camera to inspect peep slats with zero translational creep and **zero noise (0 dB)**.
     Stance toggles (crouch/stand) while stationary emit zero noise. Camera orbit is completely
     decoupled from the character model and emits zero sound events. Capsule exit requires intentional,
     sustained stick deflection toward an exit aperture (> 0.6 magnitude for > 0.1 s; for through-spots featuring
     dual apertures, deflection toward either the entrance or rear aperture initiates capsule exit toward that respective aperture).
     Once the player capsule exits the trigger, standard locomotion and movement noise resume immediately.

3. **Entry → the zone publishes typed occupancy transitions only.** Only the player's
   capsule may cause an occupancy transition; guard movement, verification, confirm-empty,
   and capture never write `HideSpot.state`. On the player's capsule entering an `Empty`
   spot's trigger, the system: (a) sets state `Occupied`, (b) retains the authored
   **`interior_position`** as the spot reference, and (c) publishes exactly one typed
   `HideSpotOccupied` transition for the continuous occupancy. Capsule exit publishes
   exactly one typed `HideSpotEmpty` transition. Each transition carries
   `session_id`, `attempt_epoch`, `hide_spot_id`, `interior_position`, immutable
   `transition_id`, `occupied`, `timestamp`, and `publisher=HideSpot`; raw occupancy
   transitions contain **no `entry_id`** and never grant witnessed authority. A
   `transition_id` is unique within `(session_id, attempt_epoch, hide_spot_id)` and is
   never reused. Perception owns hide-dive classification and allocates or relays
   `entry_id` only in its derived hide-dive fact. The authoritative `Occupied`/`Empty`
   state remains retained and queryable after publication until the next player-triggered
   transition; consumers must not infer current occupancy from event absence.

4. **Witnessed entry is the single defeat condition.** Hiding does **not** protect against a
   guard who *witnessed* the player enter — the whole `witnessed_entry_authority` contract
   (FSM #1 C1.0–C1.4, CR-CONCEPT-01) is consumed unchanged. This GDD does not re-derive it;
   it only guarantees the zone publishes the entry/exit facts the FSM's hide-dive detection
   runs on, and that the interior position the FSM verifies against is this GDD's
   `interior_position`.
   - **Noise Re-anchor Reconciliation & Active Dwell Immunity:** Reconciled strictly with FSM rev 4.1
     (§C1.0 lines 38 & 295): Chase-tier approaches ignore all noise stimuli. Investigate-tier approaches
     only admit qualifying same-area corroboration ($\le t_{\text{noise\_corroborate\_radius}}$ from
     `spot_front_anchor`). An unrelated distant noise does not distract the guard. Once the guard arrives
     at `guard_hold` and enters the active 1.5 s spot-front dwell (`t_spotfront_verify`), the dwell is
     **100% immune to noise interruption** until verification (catch or confirm-empty) completes.
   - **Re-entry Deferral State Formalization:** If a player exits a spot during an approach or dwell,
     the spot transitions to `Empty` in Phase 2. In Phase 4, the FSM cancels the active spot-front hold
     and drops `GuardGoalMode` to `StaleLKP`. If the player immediately re-enters during the 1-tick
     deferral window, the fresh occupancy does not allocate authority on that boundary. If the guard
     maintained continuous LOS to the player during the brief un-hidden window, `witnessed_entry_authority`
     re-arms on the subsequent tick as a fresh witnessed entry. If the player re-entered outside the guard's
     LOS (e.g. around an occluder), the re-entry is classified as clean and unwitnessed.

5. **Interior reach-and-verify closure (2.5D Catch Gate).** A guard holding a live `HideSpotFront` fix (M4)
   resolves catch against the spot's `interior_position` via the shared catch-gate metric.
   The catch gate operates as a canonical **2.5D cylinder check**: horizontal Euclidean distance
   $\text{EuclidXZ} \le \text{catch\_range}$ AND vertical offset $|\Delta Y| \le \text{delta\_y\_tolerance}$.
   The three Level-GDD **authored pins** guarantee this is always-resolvable (never capture immunity):
   - **Guard-reachability** — a NavMesh path guard → spot front exists (within certified-route vicinity).
   - **Interior depth / offset** — the interior datum satisfies the **D1 reach-resolvability invariant** (Section D):
     at least one catch-gate leg resolves from the hold position `guard_hold = spot_front_anchor + r_guard * hold_vector`
     (using the authored static outward vector, line-synced with `entities.yaml :738`) — the NavMesh path leg
     (`path(guard_hold → proxy(interior_position)) ≤ catch_range`) OR the occlusion-clear backstop leg
     (`EuclidXZ(guard_hold→interior) ≤ catch_range ∧ |ΔY| ≤ delta_y_tolerance ∧ Linecast_clear(eye → aperture_portal_target)`).
     Nominal horizontal standoff must satisfy the parameterized condition
     $\text{EuclidXZ}(\text{guard\_hold}, \text{interior\_position}) \le \text{catch\_range} - \text{eps\_arrive} - \text{standoff\_margin}$
     (where $\text{standoff\_margin} = 0.10\text{ m}$). At default values ($5.5 - 0.3 - 0.1\text{ m}$), $\text{EuclidXZ} \le 5.10\text{ m}$;
     at the certified minimum $\text{catch\_range} = 5.20\text{ m}$, $\text{EuclidXZ} \le 4.80\text{ m}$, ensuring that even with worst-case
     arrival slop (`eps_arrive = 0.3 m` or `0.5 m`), stopped position never exceeds `catch_range` or causes dead-band freezes.
   - **Aperture portal target clearance** — `aperture_portal_target` is centered in the prop's entrance aperture
     at height $h_{\text{portal}} = \min(0.8\text{ m}, \text{aperture\_height} / 2)$ with an outward clearance offset
     `skin_width = 0.05 m` outward along `hold_vector` to prevent self-intersection with door trim, table aprons, or low prop lips.
   - **Interior vertical offset** — the spot interior's absolute vertical offset above the guard's
     navmesh floor is strictly below the committed $|ΔY|$ tolerance ($|\Delta Y| < \text{delta\_y\_tolerance} - \text{auth\_margin}$).

6. **Noise is not silenced by hiding.** While pure pivoting and stance toggles inside an `Occupied` spot are
     silenced (Core Rule 2), any intentional translational movement that breaks the spot boundary engages the
     footstep noise system per Player Noise / NoiseEmitter rules. Unwitnessed noise-directed investigations
     never break spots (hunches resolve fruitless, FSM C1.0/C1.3). Only *vision* is disabled; hearing,
     spot-front verification, and every other perception channel are unaffected (Perception E3).

7. **Composition with confirm-window / re-sight:** entering a spot is a genuine LOS break, so
   it counts as a break for the confirm-window (a window-cancelled peek is free) and for
   re-sight rules. Hiding is never a corner-stalemate the player exploits for free: the
   spot-front verification hold keeps the give-up and caps suspended, so a guard who
   witnessed the dive resolves the spot (catch or confirm-empty) rather than drifting off.

8. **Spot classification and architectural quarantine (guides Level #8).**
   - **Through-Spots (Flankable / Dual-Exit):** Vents, crawlspaces, open-backed industrial racks.
     Must guarantee an authored `back_exit` meeting the D4/AC10 escape window: a NavMesh-reachable
     point at/beyond the back exit outside the guard's LOS cone within approach transit + extended through-spot dwell
     ($t_{\text{spotfront\_verify\_through}} = 2.5\text{ s}$). Candidate witness route distance for certified through-spots
     is $\text{path}(\text{guard\_spawn}, \text{guard\_hold}) \ge 6.5\text{ m}$, guaranteeing a realistic player escape path
     budget ($\ge 5.0\text{--}6.0\text{ m}$) for believable facility shafts and L-bends.
     For through-vents with L-bends or culled guard NavMesh, the interior datum must sit within clear
     line-of-sight of the entrance portal or back exit portal, or utilize an intermediate aperture waypoint.
   - **Dead-End Spots (Sanctuary / Single-Entry):** Lockers against walls, dumpsters, under beds.
     Exempt from the back-exit requirement.
     - *Sanctuary Contract:* 100% absolute safe sanctuary when entered unseen.
     - *Lethal Trap Risk:* If entered while witnessed under active Chase, dead-end spots provide no rear escape;
       the guard's spot-front dwell will resolve catch against the interior datum. Players must use through-spots
       for tactical mid-pursuit evasion.
     - *Architectural Quarantine & Corridor Rules:* Single-entry dead-end hide spots **MUST NOT** be placed along primary chase
       corridors or narrow bottleneck hallways. In corridors $\le 2.4\text{ m}$ wide, hide spots must either be **oriented parallel
       to corridor walls** (aperture and `hold_vector` aligned with corridor axis so `guard_hold` does not push the guard across
       the hallway into the opposing wall's exclusion zone) OR recessed in wall alcoves ($\ge 0.3\text{ m}$ deep) if facing
       perpendicularly across the corridor. Minimum spatial separation between distinct `spot_front_anchor` positions is
       $\ge 2.0\text{ m}$ to prevent telegraph ambiguity.
   - **Hide-density budget:** Level GDD must plan hide spots as a pacing knob with a negative space constraint
     (hide spots separated by at least $10\text{–}15\text{ m}$ of **NavMesh patrol traversal distance along shared patrol routes**,
     preventing turtling). Distinct enclosed rooms separated by physical partition walls (e.g. adjacent examination rooms)
     are permitted 1 hide spot per distinct patrol zone regardless of Euclidean wall proximity.
   - **Enclosed prop prefab standard (`ApertureVisionWindow`):** Enclosed prop prefabs (lockers, wardrobes) bundle a dedicated
     `ApertureVisionWindow` box collider/layer. Raycasts for peep-slat visibility checks query this window and ignore the prop's
     own exterior mesh colliders, preventing false occlusion from door trims, handles, or decorative geometry.

### States and Transitions (HideSpot)

| Current | Event | Next | Notes |
|---|---|---|---|
| Empty | player capsule enters trigger | Occupied | registers interior_position; clamps translational locomotion; publishes occupied zone event |
| Empty | — | Empty | guard may sit at anchor; spot unoccupied |
| Occupied | player capsule exits trigger | Empty | publishes empty zone event; restores normal locomotion |
| Occupied | guard's confirm-empty while player remains inside | Occupied | confirm-empty is an FSM outcome only; it never writes spot state or clears occupancy while the capsule remains inside (FSM C1.2/C1.3) |
| Occupied | guard's spot-front catch resolves | (spot stays Occupied; Capture occurs) | the *guard-gang* is separate from spot state; spot empties when player leaves |

*(The guard's `MutableState`/goal-mode — `HideSpotFront` — is FSM-owned, not this GDD's.)*

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| **Player Controller** (#11, Approved) | upstream | Consumes published stance/movement events + capsule bounds; provides the trigger-enter/exit detection origin. Movement-state → noise signature (unchanged outside/inside). |
| **Perception** (#2, Approved rev 3.4) | consumed-by | HideSpot publishes typed `HideSpotOccupied`/`HideSpotEmpty` transitions; Perception R4 applies LOS-invisibility while occupied and E3 preserves a witnessed fix while held. Perception owns derived `entry_id` allocation; raw occupancy contains none. |
| **Guard AI FSM** (#1, Approved rev 4.1) | consumed-by | Perception's derived hide-dive fact carries `interior_position` verbatim as `spot_position`, `hide_spot_id`, and the Perception-owned `entry_id`; the FSM consumes that fact for witnessed authority and M4 spot-front resolution. `spot_front_anchor` plus guard-radius standoff defines the shared `guard_hold`. Approved FSM rev 4.1 C1.4a already specifies the occupancy gate, four-phase ordering, exit abort, and same-tick re-entry deferral. HideSpot satisfies its occupancy publication and ordering contract against this frozen baseline. |
| **Event bus** (#15, undesigned, provisional) | upstream | Zone events (topic-based, provisional). |
| **Physics** (#18, undesigned, provisional) | upstream | Trigger setup: hide-spot zone on a dedicated layer, `queryHitTriggers` OFF; capsule/LOS shared config. |
| **Suspicion/Grade** (#7, undesigned) | downstream | hide-entry Chase + witnessed capture are Chase escalation events (per concept/CR-CONCEPT-02); this GDD does not grade — it guarantees the entry facts that make it legible. |
| **Level** (#8, undesigned) | downstream | Authors spots + the three pinned geometry values; AC(d) verifies guard-reachability/depth/offset; plans hide density as a pacing knob (Core Rule 8). |

## Formulas

### D1 — HideSpot reach-resolvability invariant (authored closure, per spot)

A guard holding a live `HideSpotFront` fix must always resolve catch against the
spot's interior datum — never hang and never falsely confirm-empty. Reach-presence is
the FSM's catch-gate over the static `interior_position` datum, evaluated as a **2.5D cylinder check**:

$$\text{resolvable}(\text{spot}) \iff \text{path}(\text{guard\_hold} \to \text{proxy}(\text{interior\_position})) \le \text{catch\_range}$$
$$\lor \Big( \text{EuclidXZ}(\text{guard\_hold} \to \text{interior\_position}) \le \text{catch\_range} \land |\Delta Y(\text{interior\_position}, \text{floor\_worst})| \le \text{delta\_y\_tolerance} \land \text{Linecast\_clear}(\text{eye} \to \text{aperture\_portal\_target}) \Big)$$

#### Variable Specification (D1)

| Symbol | Type | Range / Domain | Source | Description |
|---|---|---|---|---|
| `path(a → b)` | Function | $[0, \infty)\text{ m}$ | NavMesh | Surface-constrained NavMesh path distance using guard `agentType` and `areaMask`. |
| `proxy(x)` | Function | Vector3 or `FAIL` | NavMesh | `NavMesh.SamplePosition(x, 0.4 m, mask)`. If `hit == false`, path leg evaluates to `FAIL`. |
| `guard_hold` | Vector3 | World coordinate | Computed | Hold position: `spot_front_anchor + r_guard * hold_vector` ($r_{\text{guard}} = 0.40\text{ m}$). |
| `hold_vector` | Vector3 | Normalized XZ | entities.yaml | Unit vector directed outward from spot aperture along the approachable face. |
| `interior_position` | Vector3 | World coordinate | Level GDD | The authored reference point occupied by the player capsule while hidden. |
| `EuclidXZ(a, b)` | Function | $[0, \infty)\text{ m}$ | Math | Planar Euclidean distance $\sqrt{(a_x - b_x)^2 + (a_z - b_z)^2}$, ignoring vertical delta. |
| `floor_worst` | Scalar | Real | Level GDD | Highest guard NavMesh floor among all spot-capable guards in certified-route vicinity. |
| `aperture_portal_target`| Vector3 | World coordinate | Level GDD | Center of spot entrance portal at height $h_{\text{portal}} = \min(0.8\text{ m}, \text{aperture\_height}/2)$ with $+0.05\text{ m}$ outward offset along `hold_vector`. |
| `catch_range` | Constant | $5.5\text{ m}$ | entities.yaml | Certified engagement boundary of the FSM catch timer ($[5.2, 6.5]\text{ m}$). |
| `delta_y_tolerance` | Constant | $1.0\text{ m}$ | entities.yaml | Maximum inclusive vertical offset for catch-gate backstop ($[0.5, 2.0]\text{ m}$). |
| `eps_arrive` | Constant | $0.3\text{ m}$ | entities.yaml | Arrival tolerance circle for NavMesh agent stopping at `guard_hold` ($[0.2, 0.5]\text{ m}$). |
| `standoff_margin` | Constant | $0.10\text{ m}$ | Tuning | Authoring standoff cushion against boundary float ($[0.05, 0.20]\text{ m}$). |

- **Output Range**: $\text{Boolean} \in \{\text{PASS}, \text{FAIL}\}$.
- **Parameterized Standoff Constraint**: Horizontal standoff distance must satisfy $\text{EuclidXZ}(\text{guard\_hold}, \text{interior\_position}) \le \text{catch\_range} - \text{eps\_arrive} - \text{standoff\_margin}$.
  - Under default values ($5.50 - 0.30 - 0.10\text{ m}$): $\text{EuclidXZ} \le 5.10\text{ m}$, giving stopped position $\le 5.40\text{ m} < 5.50\text{ m}$.
  - Under certified lower bound ($\text{catch\_range} = 5.20\text{ m}$, $\text{eps\_arrive} = 0.30\text{ m}$): $\text{EuclidXZ} \le 5.20 - 0.30 - 0.10 = 4.80\text{ m}$, giving stopped position $\le 5.10\text{ m} < 5.20\text{ m}$. This parameterization strictly prevents dead-band freeze under all allowable tuning ranges.
- **Worked Numerical Example (D1 Pass via Backstop)**:
  - Guard halts at `guard_hold`: $(10.0, 0.0, 5.0)$. Interior datum at $(13.5, 0.4, 8.0)$.
  - $\text{EuclidXZ} = \sqrt{(13.5 - 10.0)^2 + (8.0 - 5.0)^2} = \sqrt{3.5^2 + 3.0^2} = \sqrt{12.25 + 9.0} = \sqrt{21.25} \approx 4.61\text{ m} \le 5.5\text{ m}$ (PASS).
  - $|\Delta Y| = |0.4 - 0.0| = 0.4\text{ m} \le 1.0\text{ m}$ (PASS).
  - `Linecast_clear` from eye $(10.0, 1.0, 5.0)$ to portal target $(10.85, 0.4, 5.75)$ returns `true`.
  - Result: Leg 2 evaluates `true ∧ true ∧ true = true` $\implies \text{resolvable}(\text{spot}) = \text{PASS}$.

### D2 — Authoring-margin for vertical offset (input-pin closure)

Sizes the interior datum to a certified margin below the $|\Delta Y|$ gate:

$$|\Delta Y(\text{interior\_position}, \text{floor\_worst})| < \text{delta\_y\_tolerance} - \text{auth\_margin}$$

#### Variable Specification (D2)

| Symbol | Type | Range / Domain | Source | Description |
|---|---|---|---|---|
| $|\Delta Y|$ | Scalar | $[0, \infty)\text{ m}$ | Geometry | Absolute vertical difference $|y_{\text{interior}} - y_{\text{floor\_worst}}|$. |
| `delta_y_tolerance` | Constant | $1.0\text{ m}$ ($[0.5, 2.0]$) | entities.yaml | Inclusive runtime backstop gate limit. |
| `auth_margin` | Constant | $0.2\text{ m}$ ($[0.1, 0.4]$) | entities.yaml | Conservative authoring buffer preventing boundary slop. |

- **Joint Parameter Constraint**: $\text{delta\_y\_tolerance} - \text{auth\_margin} \ge 0.3\text{ m}$ in every loaded config (guarantees $\ge 0.20\text{ m}$ step/curb allowance). Any configuration violating this inequality fails configuration validation at load time (`REJECT_CONFIG_AUTH_MARGIN_JOINT_VIOLATION`).
- **Output Range**: $\text{Boolean} \in \{\text{PASS}, \text{FAIL}\}$.
- **Worked Numerical Example (D2)**:
  - For default values $\text{delta\_y\_tolerance} = 1.0\text{ m}$, $\text{auth\_margin} = 0.2\text{ m}$, authoring bound is $|\Delta Y| < 0.80\text{ m}$ (strict).
  - Case A ($|\Delta Y| = 0.45\text{ m}$): $0.45 < 0.80 \implies \text{PASS}$.
  - Case B ($|\Delta Y| = 0.80\text{ m}$): $0.80 < 0.80 \implies \text{FAIL}$ (`REJECT_D2_AUTH_MARGIN_STRICT`).
  - Case C ($|\Delta Y| = 1.05\text{ m}$): $1.05 > 1.00 \implies \text{FAIL}$ (`REJECT_D1_BACKSTOP_DELTA_Y`).

### D4 — Through-Spot Transit & Escape Invariant (AC10)

For witnessable dual-exit Through-Spots, the player's crouched escape path duration must not exceed the guard's transit and verification window:

$$\text{transit} = \frac{\text{path}(\text{guard\_spawn}, \text{guard\_hold})}{\max(V_{\text{relevant}}, 0.1\text{ m/s})}$$
$$\frac{\text{pathLen}}{V_{\text{crouch}}} + t_{\text{margin\_react}} \le \text{transit} + t_{\text{spotfront\_verify\_through}}$$

#### Variable Specification (D4)

| Symbol | Type | Range / Domain | Source | Description |
|---|---|---|---|---|
| `pathLen` | Scalar | $(0, \infty)\text{ m}$ | NavMesh | Player path length from interior origin to rear escape point outside guard LOS cone. |
| $V_{\text{crouch}}$ | Constant | $1.8\text{ m/s}$ ($[1.4, 2.2]$)| entities.yaml | Player crouch-walk locomotion velocity. |
| $t_{\text{margin\_react}}$| Constant | $0.3\text{ s}$ | Tuning | Human telegraph recognition and turn slop buffer. |
| `guard_spawn` | Vector3 | World coordinate | Level GDD | Authored guard origin or route waypoint ($\text{path} \ge 6.5\text{ m}$ for certified through-spots). |
| $V_{\text{relevant}}$ | Scalar | $[1.4, 7.5]\text{ m/s}$ | entities.yaml | Maximum guard pursuit speed ($V_{\text{chase}} = 7.5\text{ m/s}$). |
| $t_{\text{spotfront\_verify\_through}}$| Constant | $2.5\text{ s}$ ($[2.0, 3.5]$)| entities.yaml | Duration of the guard's spot-front inspection dwell at through-spots (vents/apertures). |

- **Output Range**: $\text{Boolean} \in \{\text{PASS}, \text{FAIL}\}$.
- **Worked Numerical Example (D4)**:
  - Guard witnesses dive from distance $6.5\text{ m}$ at $V_{\text{chase}} = 7.5\text{ m/s}$.
  - $\text{transit} = 6.5 / 7.5 \approx 0.87\text{ s}$. Total budget $= 0.87 + 2.50 = 3.37\text{ s}$.
  - Available player travel time $= 3.37 - 0.30 = 3.07\text{ s}$.
  - Maximum allowable escape path $\text{pathLen} \le 3.07\text{ s} \times 1.8\text{ m/s} = 5.52\text{ m}$.
  - If authored through-spot escape path is $5.00\text{ m}$: $5.00 / 1.8 + 0.30 = 2.78 + 0.30 = 3.08\text{ s} \le 3.37\text{ s} \implies \text{PASS}$.

### D3 — Spot-state authority (guard-episode vs. player-presence closure)

```
spot.state = Occupied  ⟺  player capsule ∈ trigger
spot.state = Empty     ⟺  player capsule ∉ trigger        (player-exit authoritative)
```

Spot state is **player-occupancy-authoritative**. Confirm-empty and catch are
**guard-episode outcomes** that never write spot state directly (a confirm-empty
clears an already-empty spot at most). Consequences the FSM depends on:
- A guard's confirm-empty cannot flip a spot to `Empty` while the player is still
  inside — that would strip LOS-invisibility (false exposure, two-guard case).
- The spot-front hold's reach-presence/capture is **contingent on `spot.state == Occupied`**;
  on the player-exit event the hold aborts to confirm-empty/fruitless with **no capture** (the relocation-window promise).

**Virtual-tick ordering:** the same-tick race `Empty before capture check` and the `Empty(e1 close) → Occupied(fresh)` re-entry are pinned to a **4-phase virtual tick**: (1) Physics triggers collected → (2) HideSpot flushes `Empty`/`Occupied` synchronously → (3) `Perception.CollectFacts()` consumes the zone fact → (4) `FSM.Tick()` evaluates the capture single-clock. Same-tick exit wins: `spot.state==Empty` is evaluated **before** `gate ∧ timer≥t_catch ∧ dwellComplete`. Same-tick exit+re-entry defers allocation: if `Empty` was published this tick before allocation phase, the fresh `Occupied` does **not** allocate a new `witnessed_entry_authority` episode until the next tick; the guard's goal mode drops to `StaleLKP`. If continuous LOS was maintained during the un-hidden window, authority re-arms on the next tick; if re-entered outside LOS, the re-entry is clean.

### Lifecycle and epoch reset

Capture, death, respawn, scene reload, full-room restart, segment reset, pool/unpool,
and every `attempt_epoch` transition pass through the shared lifecycle barrier. At that
barrier HideSpot: (1) clears queued occupancy transitions and transient player
associations; (2) invalidates old-epoch occupancy, hide-dive, and witnessed-authority
facts; (3) prevents old-epoch facts from suppressing LOS or producing capture; and
(4) reconciles each spot from a fresh capsule-containment query in the new
`(session_id, attempt_epoch)`. A pooled or unpooled spot is not restored to `Occupied`
merely because its previous state was occupied; it becomes `Occupied` only when that
fresh query confirms the player's capsule is inside, then publishes a new typed
transition with a new immutable `transition_id`. The lifecycle owner increments `attempt_epoch`;
reset completion is atomic before the next playable virtual tick.

## Edge Cases

**E1 — Player dives in, spotted mid-flight (witnessed entry lands as guard arrives).**
The guard witnessed the dive, so the spot becomes that guard's LKP and it holds
`HideSpotFront`. The witness population includes patrolling guards and stationary
sentries within detection range ($\le 6.0\text{ m}$) having clear LOS to the entrance.
The spot's LOS-invisibility still applies to **other** guards who did NOT witness; the witness's
fix persists via the sight-of-entry contract (FSM C1.x), and reach/verify resolves at the
front. Result: no immunity for the witness — the player must utilize the relocation window.

**E2 — Player exits during the guard's approach / dwell.**
The occupant is genuinely gone. The hold's reach-presence is contingent on
`spot.state == Occupied` (D3); on the player-exit event the FSM aborts the hold to
confirm-empty/fruitless — **no capture** (the relocation-window promise).
- *Through-Spots:* Player exits via the back exit on-navmesh outside the guard's LOS cone within the D4 escape window.
- *Dead-End Spots:* Single-entry spots provide no rear escape. If entered unspotted, the guard never commits to `HideSpotFront` (100% sanctuary). If entered witnessed under active Chase, the player cannot slip out safely once the guard arrives at `guard_hold`; the spot is a lethal trap. Early exit onto open NavMesh while the guard is distant ($> 4.0\text{ m}$) is permitted as a high-risk scramble, but no magical front reverse-slip is granted.

**E3 — Player re-enters the same spot (exit then dive again) during a hold.**
The player exits (spot → Empty) then re-enters (spot → Occupied). On `Empty`, the FSM cancels
`HideSpotFront` and sets goal mode to `StaleLKP`. During the 1-tick deferral interval, if the guard maintains continuous LOS, authority re-arms on the next tick as a fresh witnessed entry. If re-entered outside LOS, the entry is clean and unwitnessed.

**E4 — Two guards both hold `HideSpotFront` on the same spot.**
Both witnessed the entry. Both certify identically under D1/AC(d) against shared `floor_worst`.
At runtime, each guard evaluates $|ΔY|$ and Linecast from their own position; one may catch while the other confirms empty. Spot state stays `Occupied` until the player physically leaves.

**E5 — Guard at the front but interior sightline blocked (enclosed spot).**
The design strictly separates physical containment from verification raycasting:
1. *Containment:* The player capsule resides physically inside the prop volume; trigger containment determines `Occupied` state.
2. *Verification Raycast:* The backstop `Linecast_clear` targets `aperture_portal_target` at height $h_{\text{portal}} = \min(0.8\text{ m}, \text{aperture\_height}/2)$ with $+0.05\text{ m}$ outward offset along `hold_vector`, eliminating occlusion from prop doors, lips, and furniture aprons.

**E6 — Interior datum on a circuitous navmesh route (Euclidean short, path long).**
Euclidean depth passes but NavMesh path distance exceeds `catch_range` (5.5). The backstop leg rescues if the sightline to portal target is clear; if both legs fail, it is an authoring rejection (`REJECT_D1_BOTH_LEGS`).

**E7 — Noisy player hides (walks/runs inside spot) → guard hears and investigates.**
Locomotion inside the spot is clamped to pure rotation (Core Rule 2). If the player breaks out and generates footstep noise, a noise-directed Investigate seeds, which **never breaks spots** (hunches resolve fruitless, FSM C1.0/C1.3).

**E8 — Player hosts in a spot near a guard's patrol LKP (spot overlaps an LKP).**
The FSM's M4 hold (`HideSpotFront`) engages **only** for episodes carrying `witnessed_entry_authority`. A non-witnessed LKP/noise visit does NOT enter the hold; it performs a standing lean/scan (~1.0 s, torch sweep, never kneels) and walks away.

**E9 — Spot trigger overlaps a second spot's trigger.**
Overlapping HideSpot internal trigger volumes are an authoring error (`REJECT_OVERLAPPING_HIDESPOT_TRIGGERS`). Modular contiguous props (locker banks, bathroom stalls) are permitted provided boundary overlap is within floating-point skin tolerance (overlap volume $< 0.03\text{ m}^3$ or penetration depth $< 0.05\text{ m}$) and internal player capsule containment is strictly disjoint.

**E10 — Guard resolves catch exactly at the player-exit tick.**
Pinned to the **4-phase virtual tick** (D3): player-exit transitions the spot to `Empty` in phase 2 **before** the capture check runs in phase 4, cancelling the catch. The player who exits in the same tick escapes.

**E11 — Invalid or unbounded certification input & Sanctuary Culling.** A missing `guard_spawn`,
missing approach/escape sample, failed `NavMesh.SamplePosition`, non-finite coordinate, negative/zero speed,
or malformed route fails closed. A spot with 0 eligible guards on route is certifiable as an `AUTHORED_SANCTUARY`
if tagged by Level Design and the route sampler proves no guard route or waypoint within maximum sight distance
($\le 6.0\text{ m}$) has line-of-sight to the entrance. Linecasts beyond $6.0\text{ m}$ through open doorways are culled and do not invalidate sanctuary status. Untagged empty spots fail with `REJECT_EMPTY_POPULATION`.

## Dependencies

| System | Dir | Dependency | Bidirectional? |
|---|---|---|---|
| **Player Controller** (#11, Approved) | upstream | Consumes published stance/movement events + capsule bounds; provides trigger detection origin. Movement-state → noise signature. | Yes — Player Controller lists HideSpot as consumer |
| **Perception** (#2, Approved rev 3.4) | consumed-by | Consumes typed `HideSpotOccupied`/`HideSpotEmpty` transitions for R4 LOS-invisibility + E3 fix persistence; allocates derived `entry_id`. | Yes — Perception R4/E3 cites hide-zone fact |
| **Guard AI FSM** (#1, Approved rev 4.1) | consumed-by | Perception's derived hide-dive fact supplies witnessed authority, `entry_id`, `hide_spot_id`, and `spot_position` for M4. Approved FSM rev 4.1 §C1.4a already contains the occupancy conjunct, four-phase ordering, exit abort, and same-tick re-entry deferral. | Yes — FSM M4/C1.3/C1.4 cites these pins |
| **Suspicion/Grade** (#7, undesigned) | downstream | Hide-entry Chase + witnessed capture are Chase escalation events; guarantees entry facts for grading. |
| **Level** (#8, undesigned) | downstream | Authors spots + three pinned geometry values; AC(d) verifies D1 resolvability, D2 margin, guard-reachability. |
| **Event bus** (#15, undesigned) | upstream | Zone events (topic-based, provisional). |
| **Physics** (#18, undesigned) | upstream | Trigger setup — dedicated layer, `queryHitTriggers` OFF, capsule/LOS shared config. |
| **Player Noise** (#3, Approved) | upstream | Burst pickup state and full-room restart restoration per canonical lifecycle table. |

### Bidirectional cross-refs
The Approved upstream systems (Player Controller, Perception, FSM rev 4.1) describe the hide contract from their own side; this GDD registers the contract they consume.

## Tuning Knobs

### Gameplay-feel knobs (this GDD owns)

| Knob | Safe range | Default | Gameplay aspect affected |
|---|---|---|---|
| `auth_margin` (D2) | 0.1–0.4 m | 0.2 m | Vertical authoring margin below the $|\Delta Y|$ gate. Enforces joint constraint $\text{delta\_y\_tolerance} - \text{auth\_margin} \ge 0.3\text{ m}$. |
| `standoff_margin` (D1) | 0.05–0.2 m | 0.1 m | Horizontal standoff cushion preventing dead-band freeze against minimum `catch_range`. |
| $t_{\text{margin\_react}}$ (D4) | 0.2–0.5 s | 0.3 s | Player human reaction and camera re-alignment buffer during through-spot escapes. |
| $t_{\text{spotfront\_verify\_through}}$ (D4) | 2.0–3.5 s | 2.5 s | Extended guard inspection dwell at through-spot apertures (vents/crawlways). |

### Authored per-spot values (Level-authored — verified at AC(d))

| Value | Constraint (D1/D2) | Gameplay aspect affected |
|---|---|---|
| `interior_position` | D1 resolvability + D2 margin | Depth and vertical offset where player capsule resides while hidden |
| `spot_front_anchor` | Guard-reachability (NavMesh path guard → front) | Where guard halts to verify; M4 hold datum (`guard_hold = anchor + 0.40 * hold_vector`) |

### Knobs locked upstream (entities.yaml)

`catch_range` 5.5 m [5.2–6.5], `margin` 0.5 m, `delta_y_tolerance` 1.0 m [0.5–2.0],
`navmesh_sample_maxdistance` 0.4 m, `t_catch` 1.0 s, `t_spotfront_verify` 1.5 s [1.0–2.5].

## Behavioral Feedback Requirements

- **Spot-front telegraph (Pillar 4):** the M4 hold is tier-discriminated:
  - *Hunch / Investigate-tier:* standing lean/scan at `spot_front_anchor` (~1.0 s, torch sweep, head bone Y $\ge 1.5\text{ m}$, no kneel, no capture clock).
  - *Witnessed / Chase-tier:* committed kneel at `spot_front_anchor` (1.5 s `t_spotfront_verify`, head bone Y $\le 1.1\text{ m}$, distinct anim, single-clock capture).
  - *Approach transit demeanor:* Chase-tier approach uses locked-sprint alert posture focused directly on the aperture; Investigate-tier approach uses an alert searching jog with sweeping head turns.
- **Occlusion-safe leg & Enclosed Prop Sensory Delivery:**
  1. *Visual Peep Slats / Door Cutouts:* Enclosed prop doors incorporate view slits or open grates allowing the player to observe exterior guard silhouettes, flashlight beams, and kneel/lean poses.
  2. *Prop-Family Diegetic Audio:* Locker door latch rattle / metallic strike; wardrobe wood creak; low furniture floor scuff / cloth rustle; vent hollow metallic resonance.
  3. *Directional Closed Captions:* Localized captions (`[Heavy footsteps halt]`, `[Latch rattles aggressively]`, `[Flashlight beam hums]`).
- **Accessibility fallback:** Witnessed-entry commitment must remain identifiable with audio muted, captions disabled, reduced-motion enabled, or camera facing away via peep slats and multi-ray sightlines.

## UI Requirements

- **None functional for the core hide loop.** No "you are hidden" indicator is required.
- Optional, **ADVISORY:** Subtle world-space marker if legibility testing shows players miss the animation in motion.

## Acceptance Criteria

### Authoring-time (Hide-Spot Editor Sweep Harness / Level AC(d))

- **AC4 — D1 resolvability (blocking).** Driven by the hide-spot editor sweep harness (OQ-H1). Assert at least one D1 leg resolves from `guard_hold = spot_front_anchor + r_guard * hold_vector` against `floor_worst`:
  - Path leg: `path(guard_hold → proxy(interior_position)) ≤ 5.5 m`, where `proxy` = `NavMesh.SamplePosition(interior, 0.4 m)` with `hit == false ⇒ path leg FAIL`.
  - Backstop leg: $\text{EuclidXZ}(\text{guard\_hold}, \text{interior\_position}) \le \text{catch\_range} - \text{eps\_arrive} - \text{standoff\_margin}$ ($\le 5.10\text{ m}$ default; $\le 4.80\text{ m}$ at $\text{catch\_range} = 5.2\text{ m}$) $\land |\Delta Y(\text{interior}, \text{floor\_worst})| \le 1.0\text{ m}$ $\land$ `Linecast_clear(guard eye 1.0/1.6 m → aperture_portal_target, World, Ignore triggers)`.
  - Corridor check: In corridors $\le 2.4\text{ m}$ wide, spots must either be parallel-oriented (aperture vector aligned with corridor axis) OR recessed $\ge 0.3\text{ m}$ in wall alcoves, and `NavMesh.SamplePosition(guard_hold, 0.2 m)` must strictly succeed on walkable NavMesh. Spots failing this emit `REJECT_CORRIDOR_ALCOVE_NONCOMPLIANT`.
  - Witness population pin: Guards whose route closest point to `spot_front_anchor` $\le 5.5\text{ m}$, plus stationary sentries within $\le 6.0\text{ m}$ sight. Untagged empty populations fail with `REJECT_EMPTY_POPULATION`.

- **AC4b — D1/D2 boundary discrimination & Failure Taxonomy (blocking).** Four synthetic probe spots against pinned `floor_worst = 0.0` prove observable failure substrings with $\ge 0.05\text{ m}$ straddle:
  (a) $|\Delta Y| = 0.75\text{ m} \implies$ PASS;
  (b) $|\Delta Y| = 1.05\text{ m} \implies \text{FAIL}$ (`REJECT_D1_BACKSTOP_DELTA_Y`);
  (c) $|\Delta Y| = 0.85\text{ m} \implies \text{FAIL}$ (`REJECT_D2_AUTH_MARGIN_STRICT`);
  (d) Corridor partition probe: path $> 5.5\text{ m}$ with Linecast clear $\implies$ PASS; path $> 5.5\text{ m}$ with Linecast blocked $\implies \text{FAIL}$ (`REJECT_D1_BOTH_LEGS`).
  Full taxonomy includes: `REJECT_EMPTY_POPULATION`, `REJECT_SANCTUARY_LEAK_WITNESSABLE`, `REJECT_D1_PROXY_SAMPLE_FAIL`, `REJECT_D1_LINECAST_BLOCKED`, `REJECT_D1_BOTH_LEGS`, `REJECT_D1_BACKSTOP_DELTA_Y`, `REJECT_D2_AUTH_MARGIN_STRICT`, `REJECT_PAYLOAD_DATUM_MISMATCH`, `REJECT_OVERLAPPING_HIDESPOT_TRIGGERS`, `REJECT_THROUGH_SPOT_ESCAPE_TIME`, `REJECT_CORRIDOR_ALCOVE_NONCOMPLIANT`, `REJECT_PORTAL_TARGET_OCCLUDED`, and `REJECT_CONFIG_AUTH_MARGIN_JOINT_VIOLATION`.

- **AC5 — Authoring rejection (blocking).** A spot failing D1 or D2 is not buildable; CI sweep refuses the scene with non-zero exit code.

- **AC10 — Back-exit walkability & escape invariant (blocking).**
  - Through-spots must have an authored `back_exit` marker. Candidate witness evaluation is constrained to guard routes with $\text{path}(\text{guard\_spawn}, \text{guard\_hold}) \ge 6.5\text{ m}$ using sector pruning ($R_{\text{sight\_max}} = 6.0\text{ m}$, $\theta_{\text{aperture\_fov}} \le 160^\circ$).
  - Evaluates D4: $\text{pathLen} / V_{\text{crouch}} + t_{\text{margin\_react}} \le \text{transit} + t_{\text{spotfront\_verify\_through}}$ ($V_{\text{crouch}} = 1.8\text{ m/s}$, $t_{\text{margin\_react}} = 0.3\text{ s}$, $t_{\text{spotfront\_verify\_through}} = 2.5\text{ s}$). Failure outputs `REJECT_THROUGH_SPOT_ESCAPE_TIME`.
  - Single-entry dead-end spots are exempt from back-exit requirements.

- **AC11 — D3 state-table never-edges (blocking).** Guard FSM outcomes against an occupied spot never write `Empty`; only the player capsule exit writes `Empty`.

- **AC12 — Typed occupancy payload integrity (blocking).** Transitions carry `hide_spot_id`, `interior_position` verbatim within $1\times 10^{-4}\text{ m}$, immutable `transition_id`, and no raw `entry_id`. Datum mismatch fails with `REJECT_PAYLOAD_DATUM_MISMATCH`.

### Runtime (Owned Facts + FSM Rev 4.1 Composition)

- **AC1 — Occupied-zone fact (owned).** Posts exactly one `HideSpotOccupied` transition per continuous occupancy, retaining state queryable until exit.

- **AC2 — Entry occupancy fact + D1 termination (owned).** Exactly one occupied fact posts on entry. In an integration fixture where a guard pursues with `witnessed_entry_authority` and halts at `guard_hold`, if the player remains inside, the guard's M4 hold state MUST resolve to `FSMState.Capture` within a deterministic ceiling of $t_{\text{spotfront\_verify}} + 2 \cdot \text{tick\_interval}$ ($1.5\text{ s} + 0.1\text{ s} = 1.6\text{ s}$). If the hold timer exceeds this ceiling without transitioning, the test fails with `ASSERT_M4_HOLD_LIVELOCK`.

- **AC3 — Same-tick exit ordering (owned ordering, FSM rev 4.1 composed).** `HideSpotEmpty` publishes synchronously in Phase 2 before FSM Phase 4 evaluates capture. On `Empty`, FSM rev 4.1 §C1.4a cancels the hold with no capture.

- **AC6 — D3 authority + multi-guard evaluation (owned).** Spot state is player-authoritative. In a test fixture with 1 occupied spot and 2 guards holding `HideSpotFront` (Guard 1 floor 0.0 m, Guard 2 floor 0.5 m): (a) Both guards execute independent D1 backstop evaluations without mutating `spot.State`. (b) If Guard 1 linecast is blocked while Guard 2 linecast is clear, Guard 1 aborts to `ConfirmEmpty` while Guard 2 executes `Capture`. (c) `spot.State` remains strictly `Occupied` until the player capsule physically exits the trigger.

- **AC7 — Noise-not-silenced & pure pivot (owned).** While player is inside an `Occupied` spot, applying movement input must maintain $\Delta \vec{p} == \vec{0}$ ($\pm 1\times 10^{-5}\text{ m}$) and emit $0\text{ dB}$ noise while rotating. Once the capsule exits the spot boundary, normal translational movement and surface footstep noise events resume immediately.

- **AC8 — Same-tick exit automated integration (owned ordering, FSM rev 4.1 composed).** Shared H.0 virtual-tick fixture verifies Phase 2 `Empty` index < Phase 4 `Capture` check index; hold aborts cleanly for both $T == T_{\text{dwell}}$ and $T == T_{\text{dwell}}+1$.

- **AC9 — Overlap rejection & contiguous prop tolerance (owned).** Internal trigger volume intersections fail with `REJECT_OVERLAPPING_HIDESPOT_TRIGGERS`. Modular touching props are permitted within penetration $< 0.05\text{ m}$ ($5\text{ cm}$) or volume $< 0.03\text{ m}^3$ provided internal capsule containment is strictly disjoint.

- **AC13 — Re-entry re-publish & deferral (owned ordering, FSM rev 4.1 composed).** Exiting drops guard goal to `StaleLKP`. Re-entering during deferral re-arms authority on next tick if continuous LOS was maintained; if outside LOS, fresh entry is unwitnessed.

- **AC14 — Active-datum non-flap & boundary hysteresis (owned).** Applying a high-frequency input oscillation causing the player capsule center to oscillate across the trigger boundary plane at 60 Hz ($\pm 0.01\text{ m}$ amplitude) must not publish more than 1 transition pair (`Occupied` $\to$ `Empty` $\to$ `Occupied`) within any 3-physics-tick window ($0.06\text{ s}$), enforcing boundary hysteresis and preventing event bus flooding.

- **AC15 — Lifecycle reconciliation and unpooling initialization (owned, blocking).** Scene reload, respawn, or epoch increment clears stale transitions. Spots become `Occupied` only upon a fresh containment query in the new session.

- **AC16 — Commitment onset and tier readability (joint, deterministic telemetry).** On witnessed entry:
  - Guard orientation aligns facing `spot_front_anchor` / `aperture_portal_target` (along $-\text{hold\_vector}$) within $\pm 5.0^\circ$ within $0.2\text{ s}$.
  - Guard deceleration clamps speed to $0\text{ m/s}$ at `guard_hold` within $0.1\text{ s}$ of arrival.
  - Headless automated gate asserts: `GuardController.CurrentHoldTier == HoldTier.WitnessedKneel` within $0.2\text{ s}$ of dive classification; mock `AudioBus` captures `AudioEvent.SpotFront_WitnessedDwell` within $0.2\text{ s}$.
  - *Visual/Feel (Advisory):* Head bone Y height $\le 1.1\text{ m}$ for committed kneel vs $\ge 1.5\text{ m}$ for standing lean verified in graphics-enabled staging.

- **AC17 — Accessibility and presentation fallback (joint, deterministic multi-ray).** Evaluated with audio muted, captions disabled, and camera rotated:
  - 3-ray peep-slat visibility test via `ApertureVisionWindow` at heights $h \in \{0.7\text{ m}, 0.8\text{ m}, 0.9\text{ m}\}$ from player eye toward guard silhouette returns `hit == GuardCollider` on $\ge 1$ ray when guard is at `guard_hold`.
  - Typed subtitle event `SubtitleEventId.SpotLatchRattleAggressive` emits on SubtitleBus within $0.1\text{ s}$ of dwell start (localizer string formatting is decoupled from this assertion).

## Open Questions

- **OQ-H1 — Level AC(d) venue + the standalone harness (RESOLVED 2026-09-20).** Standalone editor sweep harness loads scene, tests D1/D2/D4, asserts 2.5D bounds, enforces corridor alcoves, and logs structured JSON telemetry to `production/qa/evidence/hide-spot-sweep-[scene]-[date].json`.
- **OQ-H2 — Overlap policy (RESOLVED 2026-09-20).** Internal triggers strictly disjoint; modular prop boundary tolerance expanded to $0.05\text{ m}$ ($5\text{ cm}$) / $0.03\text{ m}^3$.
- **OQ-H3 — Authored-pin population & Sanctuary Tagging (RESOLVED 2026-09-20).** Capped at $6.0\text{ m}$ max sight distance to prevent distant linecast leaks through open doors.
- **OQ-H4 — auth_margin value.** Locked at $0.2\text{ m}$ with joint constraint $\text{delta\_y\_tolerance} - \text{auth\_margin} \ge 0.3\text{ m}$.
- **OQ-H5 — Debug overlay & authoring console.** Gizmo visualizer renders `guard_hold`, `aperture_portal_target` with $+0.05\text{ m}$ offset, D2 pass/fail bands, and 3-ray peep slat check.
- **OQ-H6 — Registry candidates.** Register `D1`, `D2`, `D4`, `auth_margin` joint constraint, and failure codes into `entities.yaml`.

### Cross-system note (FSM Rev 4.1 Synchronization — Contract Satisfied)

This GDD's D3/AC3/AC6/AC8/AC13 contracts depend on an occupancy-gated, exit-aborting, 4-phase-ordered capture. Approved FSM rev 4.1 already contains §C1.4a verbatim, including the `spot.state == Occupied` conjunct, authoritative player-exit/Empty consumption, strict `Empty`-before-`gate ∧ timer≥t_catch ∧ dwell` ordering, and same-tick exit-plus-re-entry allocation deferral. No further FSM design amendments are required.
