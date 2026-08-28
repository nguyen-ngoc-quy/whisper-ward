# Player Movement & Hide (HideSpot)

> **Status**: In Review — `/design-review full` NEEDS REVISION (2026-08-27 re-review — 7 pins: D1 hold-match, ordering+datum, witnessable AC10, 2-pose telegraph); revision pass COMPLETE 2026-08-27 — 7/7 pins resolved (hold-match D1, 4-phase ordering+datum+re-entry deferral, witnessable AC10, 2-pose telegraph+onboarding, AC rewrites). **Pending lean re-review.**
> **Author**: game-designer + user (full review — 6 specialists + CD synthesis)
> **Last Updated**: 2026-08-27
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
AC(d) certification sweep. Design order 4 — it sits directly on the Approved Player
Controller's published state (stance, movement events) and is consumed by the Approved
Perception and Guard AI FSM systems.

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

**Readability contract (revision note 2026-08-26 B1/D3 + 2026-08-27 7-pin re-review, CD synthesis UPHELD with sharpening):** a loss at a hide spot is *always readable* — including the gravest case, a dive that crossed no Investigate threshold (no meter signal at entry, `a_pre_break ≥ threshold_applied` via `min(k/d,rate_max)*dt`積 and `T_entry(R)=max(T_base - k_res*R,0.2*T_base)`) yet is still witnessed and captured. The design deliberately does **not** leak which dives were witnessed via a UI meter or a dive-instant "he saw you" ping (that would collapse the "did they see me dive?" tension and, on the inverse face, leak perfect safety when no ping appears — collapsing P2/P4). Instead the **telegraph must be sharp enough to carry the whole honesty contract** — sustained as blocking underspecification on 2026-08-27 and closed by the minimal sharpening amendment (no new mechanic, no HUD):

- **Commit-tier discrimination (2 poses, not 1):** `HideSpotFront` Investigate-tier inspect = **standing lean/scan** at `spot_front_anchor` (~1.0 s, torch sweep, no kneel); Chase-tier or witnessed sub-threshold hold = **committed kneel** (1.5 s, distinct anim). A hunch (noise/alert, Investigate with no `witnessed_entry_authority`) **never kneels** — by FSM M4 gate — so kneel itself discriminates witnessed vs hunch; both tiers share single-clock `gate ∧ timer≥t_catch ∧ dwellComplete` but read differently for the full dwell.
- **Commitment onset at detection, not arrival:** on witnessed detection the guard orientation snaps to the spot, movement retargets, and a **2D-private audio bark** fires within **0.2 s** ("check that wardrobe" / gear shift) — causal link before transit, not after 4–6 s arrival.
- **Occlusion-safe audio leg:** the spot-front verify carries **locational audio** (fabric/gear + spot-front SFX) audible through an enclosed wardrobe, plus the private enter/exit cue (Visual/Audio), so a back-wall-facing spot (30–60° FOV) is not silent.
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

2. **LOS invisibility — the core rule.** While a player is inside a spot (trigger contains
   the player capsule) and the spot's state is `Occupied`, the player is **not perceptible
   to any guard's vision LOS** — a genuine, complete LOS break. Perception consumes this as
   a published zone fact; sight accumulation toward Investigate/Chase does not advance; on
   LOS break the accumulator fully resets (Perception R1). *This is the `HideSpot`-owned
   rule; Perception owns the ray geometry.*

3. **Entry → the zone publishes events.** On the player's capsule entering an `Empty` spot's
   trigger, the system: (a) sets state `Occupied`, (b) registers the player's
   **`interior_position`** as the spot reference, (c) publishes a `HideSpot` occupied-zone
   event (topic-based; consumed by Perception's R4 invisibility and by the FSM's hide-entry
   detection). Exit publishes the corresponding empty event. Both events carry
   `hide_spot_id` + `position`.

4. **Witnessed entry is the single defeat condition.** Hiding does **not** protect against a
   guard who *witnessed* the player enter — the whole `witnessed_entry_authority` contract
   (FSM #1 C1.0–C1.4, CR-CONCEPT-01) is consumed unchanged. This GDD does not re-derive it;
   it only guarantees the zone publishes the entry/exit facts the FSM's hide-dive detection
   runs on, and that the interior position the FSM verifies against is this GDD's
   `interior_position`.

5. **Interior reach-and-verify closure.** A guard holding a live `HideSpotFront` fix (M4)
   resolves catch against the spot's `interior_position` via the shared catch-gate metric.
   The three Level-GDD **authored pins** make this always-resolvable (never capture
   immunity), enforced/verified as part of AC(d):
   - **Guard-reachability** — a NavMesh path guard → spot front exists (within certified-route
     vicinity).
   - **Interior depth / offset** — the interior datum satisfies the **D1 reach-resolvability invariant** (Section D): at least one catch-gate leg resolves from the hold position `guard_hold = spot_front_anchor + 0.40` (not anchor alone, B5 hold-match) — the NavMesh path leg (`path(guard_hold → proxy(interior_position)) ≤ catch_range`) OR the occlusion-clear backstop leg (`EuclidXZ(guard_hold→interior) ≤ catch_range ∧ |ΔY| ≤ delta_y_tolerance ∧ Linecast_clear(eye 1.6→interior+0.25)`). A guard at the front can always resolve the reach check vs `interior_position`, so false "confirm empty" cannot occur; `proxy` hit==false is a leg failure, not a shifted datum. *Pure Euclidean depth alone is NOT sufficient (D1) — a circuitous route or blocked sightline would otherwise re-open the H1/H2 capture-immunity hole.* Note: `catch_range` (5.5), not `catch_range + margin` (6.0), is the **certification** bound — the FSM's catch timer only ever *engages* at ≤ `catch_range`; `margin` is a retained-accrual/pause tolerance, not a resolvability allowance (see D1 revision note 2026-08-26).
   - **Interior vertical offset** — the spot interior's vertical offset above the guard's
     navmesh floor is **strictly below the committed |ΔY| tolerance** (an over-elevated spot
     would resolve "confirm empty" = capture immunity; spots sit at/near floor level).

6. **Noise is not silenced by hiding.** Movement state while inside a spot still drives the
   player's hearing signature (Player Noise / NoiseEmitter) exactly as outside — a hidden
   player who walks/rushes inside the spot can still be heard (a **noise-heard** can seed a
   *noise-directed* Investigate, which **never breaks spots** — hunches resolve fruitless,
   FSM C1.0/C1.3). Only *vision* is disabled; hearing, spot-front verification, and every
   other perception channel are unaffected (Perception E3).

7. **Composition with confirm-window / re-sight:** entering a spot is a genuine LOS break, so
   it counts as a break for the confirm-window (a window-cancelled peek is free) and for
   re-sight rules. Hiding is never a corner-stalemate the player exploits for free: the
   spot-front verification hold keeps the give-up and caps suspended, so a guard who
   witnessed the dive resolves the spot (catch or confirm-empty) rather than drifting off.

8. **Per-spot escape invariant (this GDD owns — guides Level #8).** Every spot whose
   entrance is **witnessable** must guarantee an escape route meeting the E2 relocation
   window: a NavMesh-reachable point at/beyond the back exit, exiting to a point **outside
   the guard's current LOS cone** within the approach transit + dwell. This converts
   "witnessed entry" from a survival coin-flip into a *planned* escape (AC10). A
   **sanctuary-only** spot (unauthorable as a trap — e.g. a flush-to-wall dead-end) is
   permitted ONLY if it is verifiably not witnessable (no certified guard can see its
   entrance); otherwise it must carry the escape pin. **Hide-density budget:** the Level GDD
   must plan hide spots as a *pacing* knob — a committed minimum/pacing target per segment
   (e.g. 2–3 good spots spaced for encounters) — so authoring rejections shape pacing rather
   than silently taxing density.

### States and Transitions (HideSpot)

| Current | Event | Next | Notes |
|---|---|---|---|
| Empty | player capsule enters trigger | Occupied | registers interior_position; publishes occupied zone event |
| Empty | — | Empty | guard may sit at anchor; spot unoccupied |
| Occupied | player capsule exits trigger | Empty | publishes empty zone event |
| Occupied | spot cleared by guard's confirm-empty | Empty | **never** via give-up expiry / LOS break — only catch or confirm-empty (FSM C1.2/C1.3) |
| Occupied | guard's spot-front catch resolves | (spot stays Occupied; Capture occurs) | the *guard-gang* is separate from spot state; spot empties when player leaves |

*(The guard's `MutableState`/goal-mode — `HideSpotFront` — is FSM-owned, not this GDD's.)*

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| **Player Controller** (#11, Approved) | upstream | Consumes published stance/movement events + capsule bounds; provides the trigger-enter/exit detection origin. Movement-state → noise signature (unchanged outside/inside). |
| **Perception** (#2, Approved) | consumed-by | Zone occupies publishes `HideSpot` occupied/empty events; Perception R4 (LOS-invisible while occupied), E3 (fix persists while held). |
| **Guard AI FSM** (#1, Approved rev 4.1) | consumed-by | Zone events + `interior_position` feed the FSM's hide-dive detection (CR-FSM-01b) and M4 spot-front hold resolution; `spot_front_anchor` is the FSM's M4 hold datum (FSM #1 :416 is authored on the spot's approachable face). This GDD's pins make the resolutions always-terminating. **Datum identity pinned:** the zone publishes `interior_position` into the break fact's `spot_position` field; D1 certifies the same datum the FSM's C1.4 catch-gate resolves against. **⚠ Blocking dependency:** the occupancy-gated capture (D3) is an FSM rev 4.1 amendment not yet present — see Cross-system note. |
| **Event bus** (#15, undesigned, provisional) | upstream | Zone events (topic-based, provisional). |
| **Physics** (#18, undesigned, provisional) | upstream | Trigger setup: hide-spot zone on a dedicated layer, `queryHitTriggers` OFF; capsule/LOS shared config. |
| **Suspicion/Grade** (#7, undesigned) | downstream | hide-entry Chase + witnessed capture are Chase escalation events (per concept/CR-CONCEPT-02); this GDD does not grade — it guarantees the entry facts that make it legible. |
| **Level** (#8, undesigned) | downstream | Authors spots + the three pinned geometry values; AC(d) verifies guard-reachability/depth/offset; plans hide density as a pacing knob (Core Rule 8). |

Provisional flags: Event bus (#15) + Physics (#18) + Level (#8) undesigned — contracts
defined here, finalized by those GDDs.

## Formulas

### D1 — HideSpot reach-resolvability invariant (authored closure, per spot)

A guard holding a live `HideSpotFront` fix must always resolve catch against the
spot's interior datum — never hang and never falsely confirm-empty. Reach-presence is
the FSM's catch-gate over the static `interior_position` datum, whose gate is an OR
over two legs (FSM #1 C1.4). This GDD's authored pins must guarantee **at least one
leg resolves** from the hold position:

```
resolvable(spot) ⟺ path(guard → proxy(interior_position)) ≤ catch_range
                 ∨ ( EuclidXZ(guard → interior_position) ≤ catch_range
                     ∧ |ΔY(interior_position, floor_worst)| ≤ delta_y_tolerance
                     ∧ Linecast_clear(guard_hold, interior_position) )
```

Variables:
- `path(a→b)` — NavMesh path distance (surface-constrained; always ≥ Euclidean), evaluated with the FSM's NavMesh agent pin (`agentType`/`areaMask` identical to runtime).
- `proxy(x)` — `NavMesh.SamplePosition(x, maxdistance 0.4 inclusive)`, same-surface projection. **If `SamplePosition` returns `hit==false`, the path leg is `FAIL` (closed, not shifted datum)** — the 0.4 m disc is not an authoring fudge.
- `guard_hold` — the FSM's M4 hold position: `spot_front_anchor + guardRadius standoff 0.40` along the approach normal (FSM #1 M4). **D1 certifies from `guard_hold`, not from the anchor alone** (revision note 2026-08-27 B5 hold-match). The 0.40 m standoff + 0.40 m proxy shift = 0.8 m combined slack is accounted: a spot that passes from the anchor but fails from `guard_hold` would otherwise yield a certified-pass → runtime-false-confirm-empty immunity; `margin 0.5` leaves only `0.5−0.40=0.1` standoff slack, so certification must use `guard_hold`.
- `floor_worst` — highest guard NavMesh floor among all spot-capable guards in the
  certified-route vicinity (worst-case |ΔY| reference — a spot certified only against a
  low route is immune against a higher-floor witness).
- `EuclidXZ` — horizontal Euclidean distance, ignoring Y.
- `Linecast_clear(a,b)` — shared sensing-config `Physics.Linecast` from `guard eye 1.6 m → interior_position +0.25 m (aperture height)`, World/solid mask, `QueryTriggerInteraction.Ignore`, unimpeded from `a` to `b`. A blocked backstop leg is a D1 failure (E5), not graceful handling.

**Certification bound is `catch_range` (5.5), NOT `catch_range + margin` (6.0).**
Revision note 2026-08-26 (review Blk-2, systems-designer): the FSM's catch timer only
*engages* at ≤ `catch_range` and *retains* while ≤ `catch_range + margin` (hysteresis,
margin = hyst = 0.5). If D1 certified resolvability at 6.0, a spot whose only resolving
leg sat in the (5.5, 6.0] band would pass D1 yet never *accrue* the timer — the guard
holds indefinitely, the player inside is never caught and never resolved empty: a
stalemate form of capture immunity. D1 therefore certifies only legs that can actually
reach the *engage* bound; `margin` remains a retained-accrual/pause tolerance, not a
certifiable resolvability allowance.

**Datum identity (revision note 2026-08-26 I2 + 2026-08-27 B4, ai-programmer):** the datum D1 certifies against — `proxy(interior_position)` — must be the SAME datum the FSM's runtime catch-gate resolves against (FSM C1.4, `proxy(spot_position)`). This GDD pins **verbatim copy** `interior_position == spot_position` (the zone publishes `interior_position` **verbatim** into the break fact's `spot_position` field, not collider center) — asserted by harness and runtime `Debug.Assert(dist(interior_position, spot_position) < 1e-4)` exact vector (AC12 `REJECT_PAYLOAD_DATUM_MISMATCH`). If Level ever authors them as distinct (deep datum vs zone-center), D1 and the FSM diverge by the interior depth and AC2's no-livelock claim breaks — this identity is a hard authoring invariant, not an inference; a 0.5 m delta moves `proxy` from pass to `PathInvalid` while certification used aperture.

Constants (locked, `entities.yaml`): `catch_range` 5.5, `margin` 0.5 (⇒ retained-accrual
pause bound 6.0), `delta_y_tolerance` 1.0 (inclusive), `navmesh_sample_maxdistance` 0.4.

The invariant is **OR (not AND)** because the FSM's gate is OR over the two legs; the
authoring contract is "at least one leg certifiably resolves." Pure Euclidean depth is
retired as the operative pin — a circuitous on-navmesh route (H1) or an enclosed
interior whose sightline is blocked (H2) would otherwise fail both legs and yield a
false "confirm empty" with the player inside, i.e. capture immunity.

### D2 — Authoring-margin for vertical offset (input-pin closure)

Size the interior datum to a **certified margin below the |ΔY| gate**, measured against
the worst-case guard floor (`floor_worst`, a dependency of D1):

```
offset(interior_position, floor_worst) < delta_y_tolerance − auth_margin
```

- `auth_margin` = 0.2 m (starter, tunable). E.g. offset ≤ 0.8 m for a 1.0 m tolerance.
- The milestone-0 committed value is asserted against the gate's **inclusive**
  `|ΔY| ≤ delta_y_tolerance` check. "Strictly below" alone invites a lazy
  over-tolerance datum (e.g. 1.05) that fails the backstop and leaves a path-failed
  spot unresolvable = capture immunity.

**D2 is the *binding* vertical gate (revision note 2026-08-26, review I2, systems-designer).**
D1's backstop allows `|ΔY| ≤ 1.0` *inclusive*, but D2 certifies only `offset < 0.8`
*strict* (and 0.8 exactly is REJECTED). A spot with `|ΔY| ∈ [0.8, 1.0]` is therefore
D1-backstop-passable yet **D2-not-buildable**. This is intentional (conservative authoring
margin) but the two ACs must not mislead: a spot verifiable by D1's resolvability leg alone
is NOT buildable unless D2 also passes. The AC(d) sweep reports them as **separate, named
failing signals** (a `|ΔY| = 0.999` D1-pass/D2-pass vs `1.001` D1-fail vs `0.801`
D2-fail must never collapse into a single "rejected") — see AC4b and the harness spec.

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
- The spot-front hold's reach-presence/capture is **contingent on
  `spot.state == Occupied`**; on the player-exit event the hold aborts to
  confirm-empty/fruitless with **no capture** (the relocation-window promise). This
  is the state predicate feeding FSM C1.4's dwell cancellation.

**Virtual-tick ordering (revision note 2026-08-27 B2/B3, ai-programmer):** the same-tick race `Empty before capture check` and the `Empty(e1 close) → Occupied(fresh)` re-entry are pinned to a **4-phase virtual tick** (not wall-clock `FixedUpdate/Update` vs fact topics): (1) Physics triggers collected → (2) HideSpot flushes `Empty`/`Occupied` synchronously → (3) `Perception.CollectFacts()` consumes the zone fact → (4) `FSM.Tick()` evaluates the capture single-clock. Same-tick exit wins: `spot.state==Empty` is evaluated **before** `gate ∧ timer≥t_catch ∧ dwellComplete`. Same-tick exit+re-entry defers allocation: if `Empty` was published this tick before allocation phase, the fresh `Occupied` does **not** allocate a new `witnessed_entry_authority` episode until the next tick (and the FSM's `no-alloc-when-live` is evaluated at tick-start OR if `Empty` published this tick → no allocation) — so a fresh unwitnessed re-entry never inherits stale authority (E3). HideSpot owns the synchronous flush/order (verifiable on H.0 trace: Empty index < Capture index); the FSM owns the abort outcome — **JOINT-CONTINGENT** (see Cross-system note, AC8).

## Edge Cases

**E1 — Player dives in, spotted mid-flight (witnessed entry lands as guard arrives).**
The guard witnessed the dive, so the spot becomes that guard's LKP and it holds
`HideSpotFront`. The spot's LOS-invisibility still applies to **other** guards who did
NOT witness; the witness's fix persists via the sight-of-entry contract (FSM C1.x), and
the reach/verify resolves at the front. Result: no immunity for the witness — the
player's only escape is the back exit inside the relocation window.

**E2 — Player exits via the back exit before the dwell completes.**
The occupant is genuinely gone. The hold's reach-presence is contingent on
`spot.state == Occupied` (D3); on the player-exit event the FSM aborts the hold to
confirm-empty/fruitless — **no capture** (the relocation-window promise). The back
exit must be on-navmesh/walkable AND escape to a point outside the guard's current LOS
cone within the approach transit + dwell (Core Rule 8/AC10), so the escape is real —
not a bolt into a sightline that re-sights immediately.

**E3 — Player re-enters the same spot (exit then dive again) during a hold.**
The player exits (spot → Empty) then re-enters (spot → Occupied). A witness who saw the
*original* entry may or may not re-acquire sight; if the re-entry is unwitnessed and sight
was lost, the spot is a fresh clean entry. **Closure requirement (revision note 2026-08-26 I4 + 2026-08-27 B3, ai-programmer):** the stale-episode authority is closed only by the FSM's **exit-abort** (the Blk-1 amendment — hold aborts to confirm-empty on player-exit, episode e1 closes, timer resets per C1.4 "resets only on … confirm-empty"). The spot's own re-published zone facts do **not** re-seed the FSM by themselves (the FSM's hide-entry input is Perception's sight-of-entry break fact, published only on a *witnessed* entry). Ordered `Empty(e1 close) → Occupied(fresh)` in the **same tick defers allocation** to the next tick (D3 4-phase pin: if `Empty` published this tick before allocation, no new episode allocates this tick) — so a fresh unwitnessed re-entry never inherits stale `witnessed_entry_authority` even when exit+re-entry land same tick. So a fresh unwitnessed re-entry inherits no stale witnessed-authority *iff* the exit-abort + deferral exist; without it (FSM rev 4.1 as-is), the stale hold's timer continues and an unwitnessed re-entry can be falsely captured. This GDD records the abort as blocking (Cross-system note); it does not assume it.

**E4 — Two guards both hold `HideSpotFront` on the same spot.**
Both witnessed the entry. Each guard runs its own dwell/catch against the same static
`interior_position` datum (D1), so both **certify identically** — the D1/AC(d) verdict is
computed against the single shared `floor_worst` scalar, so both spots are certifiable or
both are not. **This is a certification claim, not a guarantee of identical runtime
outcome** (revision note 2026-08-26, review I3, systems/ai): the FSM's runtime backstop
computes `|ΔY|` *per guard* against that guard's own floor, and capture is per-guard
single-clock with a staggered catch-gate query — two guards on different floors can
legitimately split one-catch/one-confirm-empty, and an asymmetric approach can occlude the
backstop Linecast for one guard. E6's "both pass or both fail" is therefore scoped to
**certification**, not runtime resolution. Spot state stays player-occupancy-authoritative
(D3): one guard's confirm-empty must NOT flip the spot to `Empty` while the player is
inside (that would strip LOS-invisibility for the other — H4). Capture is per-guard
terminal; the first catch ends the session.

**E5 — Guard at the front but interior sightline blocked (enclosed spot).**
A cupboard/crate spot whose datum is behind geometry from the hold. The backstop
`Linecast` is blocked and the path leg is `PathInvalid` into an enclosed zone → both
legs fail → false confirm-empty = capture immunity. This is a **level-authoring
violation** of pin D1 (no leg resolves) and is rejected at AC(d) before build — never a
runtime "handle gracefully."

**Authoring rule — the aperture datum (revision note 2026-08-26, review F2, level-designer).**
An enclosed *looking* spot (wardrobe, cupboard, under-bed — the concept's iconic hides)
is NOT banned; the D1 datum must simply be **sightline-resolvable from the hold**. The
author should place `interior_position` at the **aperture plane** (the opening), NOT at
the geometric back of the enclosed volume: the backstop leg then resolves (Linecast clear
to the aperture) while the player capsule (occupancy is trigger-capsule based, not
datum-based) can still physically tuck deeper. Visual enclosure ≠ D1-resolvability —
separate the two. The AC(d) sweep must be able to tell "full-closure no-sightline"
(reject) from "enclosed-looking but aperture-resolvable" (pass). A full-closure spot with
no sightline into the datum remains a rejection.

**E6 — Interior datum on a circuitous navmesh route (Euclidean short, path long).**
Euclidean depth passes but NavMesh path distance exceeds the D1 certification bound
`catch_range` (5.5). Under D1 the backstop leg rescues if the sightline is clear; if both
legs fail, it is the same authoring violation as E5 and is rejected at AC(d). The depth pin
is no longer "just a number" — it is the D1 invariant.

**E7 — Noisy player hides (walks/runs inside spot) → guard hears and investigates.**
Hiding silences only vision, not hearing (Core Rule 6). A noise-heard seeds a
noise-directed Investigate which **never breaks the spot** (hunches resolve fruitless,
FSM C1.0/C1.3). The spot stays `Occupied`; spot-front capture only triggers on a
witnessed entry, not on a noise hunch. The player keeps LOS-invisibility but must not
assume silence protects them from being *located* by sound.

**E8 — Player hosts in a spot near a guard's patrol LKP (spot overlaps an LKP).**
A spot's front anchor sits within a guard's certified-route vicinity and near a known
last-known-position. **Correction (revision note 2026-08-26, review M5, ai-programmer):**
the FSM's M4 hold (`HideSpotFront`) engages **only** for episodes carrying
`witnessed_entry_authority` — a non-witnessed LKP/noise visit does NOT enter the hold at
all; it follows the non-hide-spot StaleLKP give-up path (zero hold, zero dwell, zero
capture, AC-FSM-16(a)). So an "unrelated spot-front hold" cannot occur; the never-captures
safety the design wants is guaranteed by that witnessed-only M4 gate, not by a
hunch-resolves-fruitless rule. The player-visible outcome is unchanged — a hunch visit
looks around and walks away, never captures — but the mechanism text above now matches the
FSM. A re-sight during the visit can only re-enter `HideSpotFront` through a fresh
witnessed entry, which is closed by the same authority rule.

**E9 — Spot trigger overlaps a second spot's trigger.**
Level-authoring error; a player inside the overlap is inside both spots. The system
deterministically picks the innermost/nearest `interior_position` as the active datum
for LOS-invisibility and event publication; both spots still flip to `Occupied` on
entry and `Empty` on exit. Overlapping spots are discouraged at AC(d) but not, on their
own, a capture-immunity or false-exposure route (deterministic selection keeps
perception/FSM unambiguous).

**E10 — Guard resolves catch exactly at the player-exit tick.**
The catch single-clock (gate ∧ timer ≥ t_catch ∧ dwell complete) and the player-exit event land on the same tick. Resolution order is pinned to the **4-phase virtual tick** (D3): the player-exit event transitions the spot to `Empty` in phase 2 **before** the hold's capture check runs in phase 4 (D3), so the catch is cancelled and the hold resolves confirm-empty/fruitless. The player who exits in the same tick escapes — no false capture, no race — and both boundary simultaneities (exit-at-dwell-tick and exit-one-tick-after) escape. Verifiable on H.0 trace: `Empty` publish index < `Capture` check index on that tick (AC8 joint).

## Dependencies

| System | Dir | Dependency | Bidirectional? |
|---|---|---|---|
| **Player Controller** (#11, Approved) | upstream | Consumes published stance/movement events + capsule bounds; provides the trigger-enter/exit detection origin. Movement-state → noise signature (unchanged outside/inside spot). | Yes — the controller's Movement-state transition + noise signature must list HideSpot as a consumer |
| **Perception** (#2, Approved) | consumed-by | Consumes `HideSpot` occupied/empty zone events for R4 LOS-invisibility (while `Occupied`) + E3 fix-persists-while-held. | Yes — Perception R4/E3 must cite the hide-zone fact as upstream |
| **Guard AI FSM** (#1, Approved rev 4.1) | consumed-by | Zone events + `interior_position` (verbatim `1e-4`) feed hide-dive detection (CR-FSM-01b) + M4 spot-front hold; `spot_front_anchor` +0.40 standoff = `guard_hold` certification origin (B5 hold-match); D1/D2/D3 pins make the resolutions always-terminating (never capture immunity). **⚠ BLOCKING DEPENDENCY FOR IMPLEMENTATION, JOINT-CONTINGENT FOR APPROVAL (2026-08-26 Blk-1 → expanded 2026-08-27 B2/B3):** D3's occupancy-gated, exit-aborting, 4-phase-ordered capture (AC3/AC6/AC8/AC13/E3/E10) + re-entry deferral + stagger accrual requires an FSM amendment that rev 4.1 does not contain — `spot.state == Occupied` conjunct on the single-clock + player-exit/Empty consumption + 4-phase `Empty`-before-capture ordering + same-tick re-entry deferral + mid-dwell-exit-no-capture AC + stagger carry-forward. Hide owns the synchronous flush/order + datum (verifiable on H.0 trace); FSM owns the abort outcome. Recorded; see Cross-system note below OQ — GDD may be Approved-Contingent after its 7 pins, implementation BLOCKED until rev 4.2. | Yes — FSM M4/C1.3/C1.4 must cite these pins + the occupancy-gated, ordered capture (bounded rev 4.1 amendment, standing obligation) |
| **Suspicion/Grade** (#7, undesigned, provisional) | downstream | hide-entry Chase + witnessed capture are Chase escalation events (concept/CR-CONCEPT-02); this GDD does not grade — it guarantees the entry facts that make grading legible. |
| **Level** (#8, undesigned, provisional) | downstream | Authors spots + the three pinned geometry values; AC(d) verifies D1 resolvability, D2 margin, guard-reachability. |
| **Event bus** (#15, undesigned, provisional) | upstream | Zone events (topic-based, provisional). |
| **Physics** (#18, undesigned, provisional) | upstream | Trigger setup — hide-spot zone on a dedicated layer, `queryHitTriggers` OFF, capsule/LOS shared config. |

### Bidirectional cross-refs (rule: if A depends on B, B's doc must mention A)

The **Approved** upstream systems (Player Controller, Perception, FSM) were authored
before this GDD. Their GDDs already describe the hide contract from their own side;
this GDD registers the contract they consume. The reverse "mentions this GDD" legs are
satisfied where those GDDs already cite `HideSpot` / `interior_position` /
`spot_front_anchor` (FSM M4 :416; Perception R4). Any reverse leg found missing is a
**consistency-check item**, not silently left one-way — it is scheduled at the next
`/consistency-check` run.

### Provisional flags
Event bus (#15) + Physics (#18) + Level (#8) undesigned — contracts defined here,
finalized by those GDDs. **Level AC(d) is the long-term enforcement point** for the
authored pins (D1/D2 + guard-reachability); **until Level lands, the standalone
editor-sweep harness (OQ-H1) exercises them**, so they are testable now rather than
contract-only.

## Tuning Knobs

### Gameplay-feel knobs (this GDD owns)

| Knob | Safe range | Default | Gameplay aspect affected |
|---|---|---|---|
| `auth_margin` (D2) | 0.1–0.4 m | 0.2 | Vertical authoring margin below the $\lvert\Delta Y\rvert$ gate. Larger = safer against lazy authoring but rejects more spot geometry; smaller = permissive but risks over-tolerance spots reaching the gate (potential capture-immunity). |

### Authored per-spot values (Level-authored, not runtime config — verified at AC(d))

These are the spot's adjustable **inputs**, set per spot by the Level GDD and certified
by AC(d). They are not tuning knobs in the runtime sense; listed for completeness.

| Value | Constraint (D1/D2) | Gameplay aspect affected |
|---|---|---|
| `interior_position` (depth + vertical offset) | D1 resolvability + D2 margin | How deep/high the player can be while the spot still resolves catch (not falsely confirm-empty) |
| `spot_front_anchor` | guard-reachability (NavMesh path guard → front) | Where the guard halts to verify; the M4 hold datum |

### Knobs locked upstream (entities.yaml — consumed, never re-tuned here)

`catch_range` 5.5 [5.2–6.5], `margin` 0.5, `delta_y_tolerance` 1.0 [0.5–2.0],
`navmesh_sample_maxdistance` 0.4, `t_catch` 1.0, `t_spotfront_verify` 1.5.
Retuning any of these upstream changes the **D1 certification bound** (`catch_range`) or
the retained-accrual pause bound (`catch_range + margin`) — a **coordinated retune**
(per the registry note), not a local edit.

### Rationale links
`auth_margin` and the D1/D2 pins trace to the D1 formula (Section D). The $\lvert\Delta Y\rvert$
and depth bounds trace to the FSM C1.4 gate (source: `guard-ai-fsm.md`) — consumed,
never re-derived.

## Visual/Audio Requirements

- **Spot-front telegraph (Pillar 4, revision note 2026-08-27 B7 — BLOCKING for P2, not merely advisory):** the M4 hold is **tier-discriminated at a glance** (no dive-instant "saw-you" ping, no occupied-indicator — no UI leak):
  - *Hunch / Investigate-tier* (noise/alert, no `witnessed_entry_authority`): **standing lean/scan** at `spot_front_anchor` (~1.0 s, torch sweep, no kneel, no capture clock).
  - *Witnessed / Chase-tier* (including sub-threshold `a_pre_break ≥ threshold_applied`): **committed kneel** at `spot_front_anchor` (1.5 s `t_spotfront_verify`, distinct anim, single-clock `gate ∧ timer≥t_catch ∧ dwellComplete`).
  - A hunch **never kneels** — by FSM M4 gate — so kneel itself discriminates witnessed vs hunch for the full dwell; both tiers would otherwise share the same 1.5 s hold and be indistinguishable until dwell end.
  - **Commitment onset at detection:** on witnessed detection, guard orientation snaps to the spot and movement retargets *immediately*; a **2D-private audio bark** fires within **0.2 s** ("check that wardrobe" / gear shift) — causal link before the 4–6 s approach transit, not after arrival.
  - **Occlusion-safe leg:** the verify carries **locational audio** (fabric/gear + spot-front SFX) audible through an enclosed wardrobe (back-wall-facing 30–60° FOV), so the telegraph is not single-point visual failure (UX-01). Any world-space marker, if used, must be tied to `GuardGoalMode==HideSpotFront` regardless of tier, not to cue timing, or it leaks witnessed vs clean.
  - It must read unambiguously as *"this guard is coming for THIS spot, and that means he saw you dive"* — because it is the player's only readable signal that the catch is being resolved (readability contract, Section B). This is a **telegraph-sharpening** requirement (poses + onset + audio), not a new mechanic.
- **Onboarding (3 beats / 2 segments, not 1 room):** Room A clean still → walk-past (LOS break is sanctuary); Room B clean + walk-inside → hunch stand-look walk-away (noise inside ≠ capture, hunches resolve fruitless — stillness-is-the-price, not "moving breaks hiding"); Room C graze dive under a **wary guard with sinking threshold marker visible** → kneel+bolt (sub-threshold witnessed possible when wary). Teaches the 5 distinctions without collapsing them into one laundry that teaches "moving breaks hiding."
- **Occupied-spot clarity (anti-leak):** a spot whose interior is occupied should not be visually distinguishable to the player as "safe/unsafe" by geometry alone. Safety is earned by *clean entry*, not appearance — no "safe-house glow" that leaks game state.
- **Audio:** HideSpot zone transitions play a **player-private 2D UI SFX** (bus `SFX_UI`, 0% spatialize, **not** routed to hearing, −18 LUFS, 200–400 ms cloth/rustle, debounced per active-datum per AC9/AC14 — not per overlapping volume, identical for clean/witnessed so it does not leak authority). `HideSpot empty` has priority over `Capture` on the same tick (E10). A noise-heard while inside does not alter this cue. Interior ambience may duck `4 dB ±2` lerp `0.3 s in / 0.4 s out` low-pass `1200 Hz` on `Ambience/Music` buses never `Player_Foley` (optional, director's call) — or strike as post-MVP via single `AudioMixer Snapshot`.
- All visual/audio here is **ADVISORY** (Visual/Feel evidence tier) — not BLOCKING for Logic, **except** the tier-discriminated telegraph + onset + audio leg which is **BLOCKING for P2 readability** (without it `must read unambiguously` is unfalsifiable).

## UI Requirements

- **None functional for the core hide loop.** A hidden player is *unknown* to the guard
  AI by design; no "you are hidden" indicator is required (and a persistent one would
  weaken Pillar 2's "did they see me dive?" tension).
- Optional, **ADVISORY:** the spot-front telegraph (kneel) may surface as a subtle
  world-space marker if legibility testing shows players miss the animation in motion.
- All UI must support mouse navigation (PC/WebGL per technical preferences), but no new
  UI screen is introduced by this system.

## Acceptance Criteria

**Ownership:** this GDD asserts only HideSpot-owned facts. FSM/Perception reactions
(catch-gate, M4 hold, R4 invisibility, accumulator) are **delegated** to their own
unit/integration suites against the shared fixture — never re-tested here.

### Authoring-time (exercisable now via the standalone editor-sweep harness in OQ-H1; certified at Level #8 AC(d) once it lands)

- **AC4 — D1 resolvability (blocking).** Driven by the **hide-spot editor sweep harness** (OQ-H1), a menu tool that loads the current scene, enumerates all authored `HideSpot` volumes, and for each spot evaluates D1 against every guard in the spot's guard population. Assert **at least one** D1 leg resolves from the hold position `guard_hold = spot_front_anchor + 0.40 standoff` (B5 hold-match) against `floor_worst` = highest guard floor in that population — `path(guard_hold→proxy(interior_position))` ≤ 5.5 **OR** (`EuclidXZ(guard_hold→interior_position)` ≤ 5.5 ∧ `|ΔY(interior_position, floor_worst)|` ≤ 1.0 *inclusive* ∧ `Linecast_clear(guard eye 1.6→interior+0.25, World/solid, Ignore triggers)`) — where `proxy` = `NavMesh.SamplePosition(interior_position, 0.4 inclusive)` with `hit==false ⇒ path leg FAIL` (not shifted datum), evaluated with the FSM's `agentType`/`areaMask` and bake version. The sweep **materializes `floor_worst`** per spot (logs the inducing guard + value), the per-leg numeric result (path distance, EuclidXZ, |ΔY|, Linecast pass/fail, proxy hit), and the D2 verdict, so a human can audit which witness certified the spot and *which pin drove any rejection*. It asserts against `proxy(interior_position)` **verbatim**, never raw `spot_position` or collider center; datum mismatch fails with `REJECT_PAYLOAD_DATUM_MISMATCH` (AC12).
  - **Temporary population pin (replacing OQ-H3 for now):** all `GuardNPC` instances whose NavMesh route's closest point to `spot_front_anchor` ≤ `catch_range` (5.5) — where closest point = `min Euclid to waypoint polyline` (not NavMesh distance), harvestable at edit time, re-evaluated when Level #8 lands. If this set is **empty** for a spot, the spot is **not buildable** (certification cannot be proven) — an empty population is a FAIL with `REJECT_EMPTY_POPULATION`, never a vacuous pass. A guard outside the set can still witness (Perception is global) — Level must reconcile "can witness" vs "in certified population" (OQ-H3).

- **AC4b — D1/D2 boundary discrimination (blocking).** Four synthetic probe spots against **pinned `floor_worst=0.0` with `offset = |ΔY|` absolute** (so offset and |ΔY| are not dual values) prove the "most common re-authoring error" is *observable* with **distinct failure substrings** and **≥0.05 straddle** (per H.0b, not 0.001 flake): (a) `|ΔY|` = 0.75 → both D1 backstop (`≤1.0` inclusive) and D2 (`<0.8` strict) **PASS**, certifies; (b) `|ΔY|` = 1.05 (derived as `delta_y_tolerance + 0.05`, not hard-coded `1.001`) → D1 backstop **FAILS** (`REJECT_D1_BACKSTOP_DELTA_Y`), D2 passes — sweep reports D1 distinctly; (c) `|ΔY|` = 0.85 (derived as `delta_y_tolerance − auth_margin + 0.05`, not `0.801`) → D2 **strict FAILS** (`REJECT_D2_AUTH_MARGIN_STRICT`, `offset <0.8` strict), D1 passes — sweep reports D2 distinctly; (d) **path-vs-backstop probe** (E6): Euclidean depth short but `path(guard_hold→proxy) >5.5` with `Linecast_clear==true` → backstop leg rescues (PASS), and with `Linecast_clear==false` → both legs fail (`REJECT_BOTH_LEGS`). Literals are registry-derived at setup (`delta_y_tolerance=1.0`, `auth_margin=0.2`); a tester must be able to tell which pin a re-authoring error violates by substring alone.

- **AC5 — Authoring rejection (blocking).** (i) A spot failing D1 (both legs) or D2 is
  **not buildable** — the authoring tool / CI sweep refuses it (venue: Level AC(d) once it
  lands; exercised now by the harness in OQ-H1). (ii) **Runtime backstop is FSM-suite-owned
  (revision note 2026-08-26, review I1, qa-lead):** AC5(ii) as previously written was
  mutually unsatisfiable with AC5(i) — it needed a D1-failing spot *in the runtime scene*
  while AC5(i) refuses D1-failing spots at build. The backstop is proven by FSM AC-FSM-15
  on a **synthetic** D1-failing spot authored in the FSM fixture, not a shipped spot.
  HideSpot owns only AC5(i); it does NOT own the runtime backstop.

- **AC10 — Back-exit walkability / escape invariant (blocking).** Every **witnessable** spot (Core Rule 8, defined as `witnessable(spot) := ∃G` in the spot's certified population where G's LOS cone can cover the entrance at dive or within `dwell + pathTime(G, spot_front_anchor)` at `V_investigate`) has a NavMesh-reachable point at/beyond its back exit, sampleable within `navmesh_sample_maxdistance` 0.4 **inclusive** (`NavMesh.SamplePosition(backExit, 0.4) hit==true`), with a **path `front→escape` that exists** and that escape point is **outside G's current LOS cone** within the approach transit + dwell (E2 relocation window, cone-exit achieved; `pathLen/walkSpeed ≤ transit+dwell` with `transit = path(guard spawn→anchor)/V_investigate` and dwell `t_spotfront_verify 1.5`; e.g. 2 m at `V_chase 7.5` ⇒ ~1.77 s). The harness asserts per witnessable spot: (i) Sample success (ii) path front→escape exists (iii) escape outside G's LOS cone at escape time (iv) `pathLen/walkSpeed ≤ transit+dwell`; logs inducing guard + values; fails CI non-zero; persists artifact `production/qa/evidence/hide-spot-sweep-[date].md`. A sanctuary-only spot (not witnessable) is exempt from AC10 but must be **verifiably non-witnessable via exhaustive LOS sweep** sampled at guard waypoints +5 m approach positions covering the entrance — empty population FAIL alone is insufficient (a global witness outside the certified set could still witness; AC4 population hole). *AC10 is kept binding* (CD ruling on disagreement D1): the back exit is load-bearing for the "you created the situation" escape fantasy — not relaxed, but scoped to witnessable spots so density is protected via placement/pacing (Core Rule 8's density budget) rather than by gutting the rule. Expected escape movement is **crouch-walk silent** (walk 4.0 m / run 6.0 m inside is audible per Core Rule 6 and self-defeats escape; interiors must suppress footstep to crouch level or require crouch for silent exit).

- **AC11 — D3 state-table never-edges (blocking).** For every guard/FSM outcome (give-up expiry, LOS break, confirm-empty, catch, post-chase sweep) against a player-inside spot, `state` remains `Occupied`; only the capsule-exit event writes `Empty` (player-occupancy-authoritative). A guard's confirm-empty must not flip a spot to `Empty` while the player is inside (would strip LOS-invisibility for another guard's witness — two-guard case, D3).

- **AC12 — Zone-event payload integrity (blocking).** Each occupied/empty event carries the correct `hide_spot_id` + `interior_position` **verbatim** within pinned epsilon `1e-4` exact vector (`dist(interior_position, spot_position) < 1e-4`, not `0.4`), so downstream consumers never act on a wrong datum; mismatch fails with `REJECT_PAYLOAD_DATUM_MISMATCH`. The `0.4` is only the NavMesh proxy sample disc, not a datum tolerance to launder a 0.6 m deep-vs-aperture error.

### Runtime (owned facts + delegation)

- **AC1 — Occupied-zone fact (owned).** While the capsule is inside and `Occupied`, the
  spot posts exactly one occupied-zone fact per continuous occupancy (no duplicate /
  re-fire), carrying `hide_spot_id` + `interior_position`, on the declared named channel
  (`HideSpotOccupied`). LOS-invisibility + accumulator behavior is delegated to the
  Perception R4/R1 suite (R4 requires the fact; R1 requires reset-on-break).

- **AC2 — Entry-fact + D1 termination (owned).** On capsule entry to an `Empty` spot, one
  occupied fact posts (the FSM's hide-dive ingredient). The hold's reach-gate cannot
  livelock on a reachable spot because D1 resolves (AC4). M4 hold / catch resolution is
  FSM-suite-owned.

- **AC3 — Same-tick exit ordering (owned ordering; capture-abort is JOINT-CONTINGENT).** The `Empty` fact publishes **synchronously in phase 2 before any FSM tick phase 4 consumes the current-tick fact set** (D3 4-phase: Physics→HideSpot flush→Perception→FSM, `Empty` before `gate ∧ timer≥t_catch ∧ dwellComplete`). HideSpot owns the **order** (verifiable on H.0 trace: Empty publish index < Capture check index same tick); the FSM owns the **abort outcome**. Delegated-contingent: capture aborts to confirm-empty/fruitless on player-exit, resolving both boundary simultaneities (exit-at-dwell-tick and exit-one-tick-after still escape) — FSM suite (E10) — marked **JOINT-CONTINGENT / DELEGATED-BLOCKED** until FSM rev 4.2 lands the occupancy conjunct + exit-ordering + mid-dwell-exit AC.

- **AC6 — D3 authority + datum identity (owned).** Spot state is player-occupancy-authoritative; a guard's confirm-empty never writes `Empty` while the capsule is inside (two-guard case: A's empty verification does not strip B's LOS-invisibility). Two guards at the same spot resolve the **same D1 certification verdict** against the same `floor_worst` shared datum — i.e. both spots are D1/AC4-certifiable or both are not. This is **not** a claim of identical *runtime* resolution: the FSM's runtime backstop computes `|ΔY|` **per guard** against that guard's own floor (not `floor_worst`), and capture is per-guard single-clock with a **staggered catch-gate query with carry-forward accrual** across simultaneous `HideSpotFront` holds (cycle `K`, `t_spotfront_verify 1.5 = 3 ticks` may be < K, accrual correct) — so two guards on different floors can legitimately split one-catch/one-confirm-empty, and an asymmetric approach can occlude the backstop Linecast for one guard. That split is FSM-owned (E4).

- **AC7 — Noise-not-silenced (owned) + fruitless delegation.** (i) A player's movement state inside an `Occupied` spot emits the same hearing signature as outside (walk/run → noise-heard-eligible; crouch-still → none) — only vision disabled (Core Rule 6). Expected escape is crouch-walk silent; walk/run inside is audible and self-defeating (see AC10), but hunches never break the spot. (ii) A visit without `witnessed_entry_authority` resolves fruitless and the spot stays `Occupied` — FSM suite (E7/E8).

- **AC8 — Same-tick exit, automated integration (owned ordering + FSM delegation — JOINT-CONTINGENT).**
  Runs on the shared **H.0 virtual-tick fixture** (seeded RNG, `T_sample`-derived tick schema, ceiling-derived termination bound, no wall-clock `[Timeout]`): a tick where the capture single-clock is true **AND** player-exit is queued — the `Empty` fact publishes in phase 2 before the capture check in phase 4, the hold aborts, no `Capture` posts. Both timing boundary cases escape (`T==T_dwell` and `T==T_dwell+1`, `max(t_spotfront_verify,t_catch)/T_sample ×1.2` ceiling). HideSpot owns the Empty-publish-before-capture-check *order* (reads the trace/decision tap: Empty index < Capture index); the FSM owns the *capture-abort* outcome (reuses the AC-FSM suite assertion). If the H.0 fixture is not upstream or FSM rev 4.2 not landed, AC8 re-grades to **DELEGATED/BLOCKED** and its determinism claim is struck (see Testability prerequisites).

- **AC9 — Overlap determinism (owned).** Active datum = spot with minimum Euclidean XZ `interior_position` to the capsule origin; ties within `1e-4` XZ break to the lower authored `spot_id`; stable (non-flap) while the capsule does not materially move (`displacement ≤0.01 m` retains prior active datum; `>0.01 m` may switch; hysteresis `0.05 m` prevents flap while stationary). Both overlapping spots flip `Occupied`/`Empty` and re-publish facts on re-entry (E3/E9).

- **AC13 — Re-entry re-publish (owned) + stale-episode closure (delegated — JOINT-CONTINGENT).** (i) Owned: exiting then re-entering during a hold re-posts occupied facts (E3); same-tick `Empty→Occupied` defers fresh allocation to next tick (D3). (ii) Delegated (FSM-suite, **contingent on the Blk-1 exit-abort + deferral, marked DELEGATED/BLOCKED until FSM rev 4.2**): the stale-episode closure — the exit-abort closes episode e1 and resets the timer so a fresh unwitnessed re-entry inherits no witnessed authority. HideSpot does NOT claim zone events alone re-seed the FSM; the closure is the FSM's exit-abort; an unwitnessed re-entry that re-seeds authority without abort is a FSM failure, not a HideSpot failure.

- **AC14 — Active-datum non-flap (owned).** Two spots within sampling noise: the active datum is stable over **N=10 consecutive ticks (≈5 s at `T_sample 0.5`)** while the capsule is stationary (`displacement ≤0.01 m`, hysteresis `0.05 m`) — no alternation. Material move `>0.01 m` may switch datum per AC9.

### Testability prerequisites (instrumentation required before ACs are falsifiable)
- The AC(d) sweep **materializes `floor_worst`** per spot (inducing guard + value, `floor_worst` = highest guard floor in population) and **per-pin reject reasons** — which leg (reachability / D1-path / D1-backstop / D2 / AC10 / datum / population) and *which* population member drove the rejection, so re-authoring is targeted; each failure carries its distinct substring (`REJECT_EMPTY_POPULATION`, `REJECT_PAYLOAD_DATUM_MISMATCH`, `REJECT_D1_BACKSTOP_DELTA_Y`, `REJECT_D2_AUTH_MARGIN_STRICT`, `REJECT_BOTH_LEGS`).
- The sweep asserts `|ΔY| ≤ 1.0` **inclusive** and D2 `offset < 0.8` **strict** (and `offset = |ΔY|` absolute, E2) — the D2-vs-D1 distinction is the most common re-authoring error; AC4b's **four** synthetic probes make it observable with distinct failure text plus path-vs-backstop discrimination; literals are registry-derived (`delta_y_tolerance=1.0`, `auth_margin=0.2`, straddle `≥0.05`).
- A **debug navmesh overlay** (Gizmo: `guard_hold`, `spot_front_anchor`, both datums, `proxy` disc 0.4 m, path polyline, Linecast ray with pass/fail color, `floor_worst` probe, D2 bands 0–0.8/0.8–1.0/>1.0, AC10 back-exit marker, per-population reveal) makes E5/E6 and the aperture datum inspectable by a human tester — **editor-only** `#if UNITY_EDITOR` + `EditorTool/Handles`, CI fails if left in player build.
- Same-tick races (AC3/AC8) run on the FSM/Perception shared **H.0 virtual-tick fixture** — not wall-clock manual checks — with **4-phase ordering** (Physics→HideSpot→Perception→FSM) and 1e-4 datum assert; cross-tick latent decisions accrue to the next tick.
- **AC8 is a JOINT AC** (revision note 2026-08-26 B1 + 2026-08-27 B2, qa-lead): it runs on the shared H.0 virtual-tick fixture (seeded RNG, `T_sample`-derived tick schema, `max(t_spotfront_verify,t_catch)/T_sample×1.2` ceiling, both `T==T_dwell` and `T==T_dwell+1` escapes, no wall-clock `[Timeout]`); HideSpot owns the Empty-publish-before-capture-check *order* (reads the trace/decision tap: Empty index < Capture index); the FSM owns the *capture-abort* outcome (reuses the AC-FSM suite assertion). If the H.0 fixture is not upstream or FSM rev 4.2 not landed, AC8 re-grades to **DELEGATED/BLOCKED** and its determinism claim is struck.

## Open Questions

- **OQ-H1 — Level AC(d) venue + the standalone harness (RESOLVED 2026-08-26 spec'd → REVISED 2026-08-27 to full 7-pin spec).**
  The authored pins (D1/D2/guard-reachability) and AC4/AC5/AC10 are enforced twice: now by the **hide-spot editor sweep harness**, and at Level (#8) AC(d) once that GDD lands. The harness is a first-class deliverable, not a placeholder:
  - **What it loads:** the current scene; enumerates all authored `HideSpot` volumes and the temporary guard population (AC4's pin: guards whose **route's closest point to `spot_front_anchor` ≤ 5.5**, where closest point = `min Euclid to waypoint polyline`, not NavMesh distance).
  - **What it asserts (per spot):** D1 at least-one-leg from **`guard_hold = spot_front_anchor + 0.40`** (B5 hold-match, not anchor alone; combined 0.8 m slack accounted): `path(guard_hold→proxy(interior_position))` ≤ 5.5 **OR** backstop (`EuclidXZ(guard_hold→interior)` ≤ 5.5 ∧ `|ΔY|` ≤ 1.0 *inclusive* ∧ `Linecast_clear(eye 1.6→interior+0.25, World/solid, Ignore triggers)`), where `proxy` = `NavMesh.SamplePosition(interior, 0.4 inclusive)` with `hit==false ⇒ path leg FAIL` and `agentType`/`areaMask` pinned; D2 `offset(interior, floor_worst) < 0.8` *strict* (`offset = |ΔY|` absolute) against `floor_worst` = highest guard floor in the population (with inducing guard materialized); datum identity `dist(interior, spot_position) <1e-4` (`REJECT_PAYLOAD_DATUM_MISMATCH`); and AC10's **four checks** (Sample success within 0.4, path front→escape exists, escape outside G's LOS cone at escape time, `pathLen/walkSpeed ≤ transit+dwell` with cone-exit within window, crouch-walk silent). Sanctuary-only spots must pass exhaustive LOS proof that no certified guard can see the entrance (empty population alone is `REJECT_EMPTY_POPULATION`, not sanctuary proof).
  - **Pass/fail:** a per-spot table — each leg's numeric value, proxy hit, Linecast pass/fail, `floor_worst` and inducing guard, D2 verdict, datum verdict, AC10's four sub-checks with inducing guard and `transit+dwell` vs `pathLen`. Any spot failing a required pin fails the sweep, which the CI gate consumes as a **hard failure (non-zero exit, `AC5` refusal path)** and persists artifact `production/qa/evidence/hide-spot-sweep-[date].md`. Each failure names the exact pin and population member via distinct substring (`REJECT_EMPTY_POPULATION`, `REJECT_PAYLOAD_DATUM_MISMATCH`, `REJECT_D1_BACKSTOP_DELTA_Y`, `REJECT_D2_AUTH_MARGIN_STRICT`, `REJECT_BOTH_LEGS`).
  - **Note on reachability pin:** guard-reachability is measured `guard → spot_front_anchor` (on-navmesh by construction, the FSM's M4 hold datum) — NOT `guard → proxy(interior_position)`, which is a runtime catch-callback bound and would inherit the 0.4 m interior-sample fragility as an authoring pin; `guard_hold` standoff is the only correct certification origin for D1.
- **OQ-H2 — Overlap policy.** E9/AC9 pin a deterministic selection rule, but whether
  overlapping spots are merely tolerated or should be authored-out (warning vs. rejection
  at AC(d)) is open — leaning "reject at AC(d)" for hygiene.
- **OQ-H3 — Authored-pin population (temporarily pinned in AC4).** "Certified-route guard
  population" is pinned for now as "guards whose route's closest point to
  `spot_front_anchor` ≤ `catch_range` (5.5)" so AC4 is testable. Level #8 must confirm or
  revise this — an **empty set FAILS** a spot (never vacuous-pass), and a guard whose route
  is outside the set can still witness an entry (Perception is global), so Level should
  reconcile "can witness" vs "in certified population."
- **OQ-H4 — auth_margin value.** 0.2 m starter; the milestone-0 gate asserts the committed
  |ΔY| value, and auth_margin may need re-up after the first real level blockout.
- **OQ-H5 — Debug overlay scope → authoring console.** The navmesh overlay (AC8 prerequisite)
  is upgraded to a **full authoring console**: pre-placement live probes for every pin
  (a `floor_worst` probe + D2 pass-band visualization showing pass 0–0.8 / D1-lived-but-D2-dead
  0.8–1.0 / reject >1.0, a 0.4 m proxy sample disc, an AC10 back-exit marker, and a
  per-population reveal of which guards/routes are in the certified set). Ownership to settle
  with Level/tools.
- **OQ-H6 — Registry candidates.** `HideSpot` (entity: type, state, interior_position,
  spot_front_anchor), `interior_position` (entity), and updated `spot_front_anchor`
  referenced_by — to register in Phase 5.

### Cross-system note (FSM rev 4.1 gap — Blk-1 + B2/B3, recorded 2026-08-26 → expanded 2026-08-27)

This GDD's D3/AC3/AC6/AC8/AC13/E3/E10 depend on an **occupancy-gated, exit-aborting, 4-phase-ordered capture** that the Approved FSM rev 4.1 does **not** currently implement: the FSM's single-clock capture (C1.3/C1.4) has no `spot.state == Occupied` conjunct, no player-exit/Empty-event consumption, no **4-phase virtual-tick ordering** (`Empty` before `gate ∧ timer≥t_catch ∧ dwell` on same tick), no **same-tick exit+re-entry deferral** (fresh allocation deferred next tick), and no mid-dwell-exit-no-capture AC (and no stagger carry-forward accrual pin for simultaneous `HideSpotFront` holds). **This is a blocking dependency for *implementation*, recorded here and in Section F — a player who exits mid-dwell (or exits+re-enters same tick) is falsely captured until the FSM is amended — but it is correctly governed as JOINT-CONTINGENT for *GDD approval* (Hide owns the synchronous flush/order + datum + witnessable harness, FSM owns the abort outcome; AC3/AC8/AC13 re-grade to DELEGATED/BLOCKED until the amendment lands).** The required change (bounded FSM amendment: occupancy conjunct + Empty consumption + 4-phase ordering + re-entry deferral + mid-dwell-exit AC + stagger accrual) is a **standing obligation** tracked in `systems-index.md` row 1; it is NOT silently assumed to exist. See review log. This GDD may be **APPROVED-CONTINGENT** after its own 7 pins land; implementation remains BLOCKED until FSM rev 4.2.
