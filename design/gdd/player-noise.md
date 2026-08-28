# Player Noise (`NoiseEmitter`)

> **Status**: In Design — **behavioral revision applied 2026-08-28** (re-review verdict: MAJOR REVISION NEEDED, 10 blockers + 5 Recommended + 5 adjudications → behavioral revision pass) — raised-origin Burst (B1), `fact_id`/`entry_id` split (B2), cadence monotonicity hard-reject (B3), merged footstep voice (B4), controller-owned rustle (B5), guard-proxy contract (B6), throw-verb spec (B7), `T_hearing` 0.2 s (B8), binary-Linecast portals (B9), planar `d_noise` + soft `hearing_y_hard_cutoff` falloff (B10), authored E18 + cooldown (B11); **registry-dedup pass applied** (constants → `design/registry/entities.yaml`)
> **Author**: user + agents
> **Last Updated**: 2026-08-28
> **Implements Pillar**: Pillar 3 (You Create the Situation) — primary; supports Pillar 1 (Thinking Enemies) & Pillar 2 (Fair Mind-Challenge)

## Overview

The **Player Noise (`NoiseEmitter`)** system is the facility's hearing contract — a thin, **stateless, event-driven relay** that turns the Approved Player Controller's `step_event` stream into broadcast, attributable `Noise heard` facts, and the player's primary manipulation verb for *You Create the Situation*. In the MVP it owns two sources: **movement pips** (one `Noise heard` per controller stride-commit, at the committed state's radius — walk `R_walk 4.0 m`, run `R_run 6.0 m`) and the **throwable Burst tool** (a **limited-resource** pickup that lands as an `R_burst 10.5 m` point-source). The emitter **owns no ledger**: the global audible-distance ledger `L_audible`, stride thresholds, remainder, freeze-without-reset, and `audible_armed` all live in Controller F5 — the emitter consumes `step_event`s and publishes facts, recomputing nothing. Each event carries kind, source position, radius, attribution (`movement / Burst`), and an emitter-allocated transport **`fact_id`** on the provisional Event bus (the per-guard `entry_id` stays Perception-owned); the Approved Perception system (R5) consumes it as a **broadcast to every in-range guard** (each guard relays independently, one guard's consumption never invalidates another's), gated by a wall-blocking `Physics.Linecast` from a `f12_noise_origin_offset 0.25 m`-raised origin to the guard's eye, and the Approved Guard AI FSM consumes the relay as an Investigate/orientation trigger — **never accumulator charge, never Chase on noise alone**. Without it there is no way to bait or divert a guard, no bounded consequence for fruitless searches, and no *You Create the Situation* decision; with it, a player who reads a patrol can *reshape* the room. It sits directly on the Player Controller, Physics, Event bus, and Input; it is consumed by Perception, Guard AI, and Level acoustic authoring. Concrete AudioSource pooling, mixer routing, and trigger-layer pins become an ADR at Technical Setup — the GDD stays at behavior.

*Player fantasy in one line:* you are the quiet architect who chooses when to be heard — a silent crouch that buys safety, a walk that risks a whisper, a run that trades speed for sound, and a Burst you carry like a single bullet and place to pull a guard off his line and slip the gap you made.

## Player Fantasy

**You are the quiet architect — you choose when the facility hears you, and every sound you let it hear reshapes the room.**

This is a **direct** system: the player actively decides to be silent or to be loud, and the world answers visibly. The core emotion is *controlled risk* — the held-breath tension of moving silently and the deliberate release of throwing sound where you want attention to go. It serves **Pillar 3: You Create the Situation** first — Burst is the verb that lets a planner pull a guard off his line and slip the gap they made — and **Pillar 1: Thinking Enemies** second, because the guard's head-turn, orient, and investigate is the proof the machine was listening. **Pillar 2: Fair Mind-Challenge** is the guardrail: a detection after a noise is always readable as "I was too loud, too close, or too exposed after I called him," never as a random hearing cone.

**The anchoring moment:** you crouch-still behind sub-1.0 m cover, a guard's cone sweeps past, you walk three steps — the controller's stride ledger banks the distance; you sprint to a corner — the stride ledger commits, a `Noise heard` relay fires, the guard's head snaps and he walks to the sound's edge, not to you; you place a Burst on the far wall, hear the raised-origin Linecast clear, watch both in-range guards turn, and thread behind them while they investigate the *area with error* (never your exact position). The fantasy is not "I have a noisemaker" — it is "I made the patrol *wrong* and the facility proved it."

**What makes it sing vs. what kills it:**
- *Sings* when silence is a choice with cost (crouch is silent but slow — `V_crouch 1.8 < V_patrol 2.3`, so silence trades time), when walk/run risk is readable before you commit (stride thresholds 1.9 m / 2.6 m give ~0.55 s / ~0.46 s fresh-session windows to the first pip, and a **player-local rustle pre-warns the imminent commit**), and when a Burst feels like an authored placement, not a grenade spam (one precise throw, one investigation arc, bounded residual, a **limited resource you must reclaim by routing to a pickup**, not a free distraction).
- *Dies* if noise is noisy UI (a persistent "you are loud" glow), if hearing is a hidden radius with no orient tell, or if fruitless noise has no consequence (then Burst is a free win button — Pillar 3's anti-puppeting fails).

**Tone:** Cold Watch — sound is light's twin. A heard noise is the building exhaling: a head turn, a footstep toward the source, a torch sweep of an area. No horror stinger, no combat bark — surgical, institutional, visible intelligence.

## Detailed Design

### Core Rules

1. **Single `NoiseEmitter` on the player, two emission channels.** The component has exactly two sources, never more: **(a) Movement** — a stateless relay that publishes one pip per consumed controller `step_event` at the committed state's radius; **(b) Burst** — a limited-resource throwable point-source. Both publish the same `Noise heard` event shape on the provisional Event bus; Perception (R5) is the sole hearing consumer. The emitter **never decides** — it publishes attributable facts (`kind`, `position`, `radius`, `timestamp`, `publisher = NoiseEmitter`, emitter-allocated `fact_id`); guard reaction (Investigate vs ignore) is FSM-owned. **Two ids, two owners (B2):** the emitter allocates the transport/dedup token **`fact_id`** — one per published fact, shared by every hearing guard (a three-guard broadcast carries one `fact_id`); the **`entry_id`** — the *episode* id — remains **Perception-owned**, allocated at first-noise-heard per guard and **relayed, never derived**, by the emitter and FSM. The emitter never allocates an `entry_id`; Perception never allocates a `fact_id`.

2. **Movement emission is a relay of the controller's `step_event` stream — the emitter owns no ledger.** Controller F5 owns the global audible-distance ledger entirely: `L_audible(t+dt) = L_audible(t) + |Δposition_XZ|` in `{Walk, Run}`, frozen (never reset) in `{Idle, Crouch}`, with stride thresholds (`stride_length_walk 1.9 m` / `stride_length_run 2.6 m`), remainder retention, and `audible_armed` (cleared only on capture/respawn). When the ledger commits, the controller publishes **`step_event {state, position, stride_length_m, ledger_remainder_m, timestamp, publisher}`** (C7), at most once per Tick. **For each consumed `step_event`, the emitter publishes exactly one `Noise heard`:**
   - `state = Walk` → `kind = movement`, `radius = R_walk 4.0`, `position = step_event.position` (player feet, world XZ+Y)
   - `state = Run` → `kind = movement`, `radius = R_run 6.0`, same position
   - `fact_id` allocated at publish ingress (one per fact, shared by all hearing guards), `timestamp = step_event.timestamp`, `publisher = NoiseEmitter`
   
   The emitter **never** reads or writes `L_audible`, never applies a threshold, never retires a stride, and never runs a timer. It does not read the controller's polled `r_noise` readout (F5g: debug/telemetry only, **never a gameplay channel**). States outside `{Walk, Run}` produce no `step_event` (F5 freezes the ledger; displacement honesty bills actual meters), so the emitter has nothing to relay and publishes nothing — it never synthesizes an event from state alone.
   - **Fresh-session commit** is controller-owned (F5c): first `step_event` ≈ **0.551 s** Walk / **0.456 s** Run at starters (4.0 m / 6.0 m ledges, 3.6 / 6.25 m·s⁻¹). These are the pacing windows the player feels; this GDD consumes them, never re-derives them.

3. **Burst is a limited-resource, re-arm-at-pickup throwable.** MVP Burst lifecycle: **Placed pickup** in the level (level authors ≥1–2 per room, telemetry-anchored `pickup-reached` event at entry; pickup reach = `pickup_reach_radius 1.6 m [1.0, 2.5]`) → **Carried** (exactly one at a time; the `Throw` input is available) → **In-flight** (parabolic arc, gravity `−9.81 m/s²`, `BurstThrowConfig` starter `v0 = 10.0 m/s @ 38°`, **raised-origin range** `R = v₀·cosθ·(v₀·sinθ + √((v₀·sinθ)² + 2·g·h_release))/g` with `h_release 1.5 m` — starter lands **≈ 11.5 m**, in the `[8, 12]` safe band; collision with `World/solid` stops flight) → **Landed** → emit one `Noise heard` (`kind = Burst`, `position = landing point`, `radius = R_burst 10.5 m`) exactly once → **Consumed**. **Throw verb (B7):** the throw launches **instantly on the `Throw` input edge** (no wind-up in MVP), in the **camera azimuth**; throwing while moving is allowed; launch renders a **visible arc + landing ghost** (transient placement aid, not HUD). **Re-arm rule (limited resource):** a spent Burst **is not restored** by death or segment reset; the only re-arm path is reaching an **unconsumed** `Placed` pickup authored in the level. A **carried, unthrown** Burst survives death via checkpoint snapshot (you had it, you keep it); a **consumed** Burst does not — dying can never refund a spend. There is no cooldown, no ammo pool, no pickup stockpile — the pickup geometry *is* the resource gate. A Burst in flight that lands out of NavMesh or against a wall still emits at the contact point; the hearing Linecast from guards still gates audibility.

4. **One raw event shape, broadcast, per-guard single consumption, `fact_id` transport.** Every emission publishes a raw `Noise heard { kind: movement|Burst, source: player|burst-landing, position: feet|landing, radius: R_walk|R_run|R_burst, fact_id, timestamp, publisher }`. **`fact_id` is allocated by the emitter at publish ingress** — one id per published fact, and that same id reaches every guard that hears it (a single Burst heard by three guards carries one `fact_id`). Perception adds `guard`, the Perception-owned per-guard `entry_id`, and `consumption` on each relay. **`entry_id` is allocated by Perception at first-noise-heard (R5) and relayed** — the emitter never allocates it; this GDD holds no Laden ledger. Perception R5 **broadcasts** to **every** in-range guard — one guard's consumption never invalidates another's relay; each guard's FSM consumes its relay at most once per episode; stale relays after `Chase-end` are dropped, never re-heard. *(Cross-doc amendment completed: perception.md and `entities.yaml` distinguish the raw publication from the per-guard relay; `entry_id` remains Perception-owned and the relay additionally retains emitter-owned `fact_id`.)*

5. **Hearing geometry is Perception-owned; the emitter declares the origin.** The emitter publishes the noise position in world space (movement: `step_event.position`; Burst: contact point). Perception F12 performs the hearing evaluation: ray origin **feet + `f12_noise_origin_offset` 0.25 m** (clears a 0.20 m stair riser), ray endpoint **guard eye** (no ear bone — eye-anchor is the canonical perception operand), **binary Linecast, no partial attenuation** (B9 — a portal/gap either passes sound at full radius or blocks at zero; there is no in-between), cast on the **shared E20 mask** (`World/solid` minus {Player, Guard, trigger} — resolved via `LayerMask.GetMask`, **Player layer excluded**: the raised origin lies inside the player capsule `r_player 0.35`, so a self-inclusive mask occludes the player's own footfalls; the same mask serves vision rays, hearing, and the Burst projectile), `QueryTriggerInteraction.Ignore` per-call. **Hearing distance is planar (B10):** `d_noise` is the **XZ-plane** distance from guard to noise origin, with a **soft Y-delta falloff** that hits 0% at `|ΔY| = hearing_y_hard_cutoff 4.0 m [3, 5]` (renamed from `hearing_y_tolerance` to reflect the soft-leg semantics). The 0.25 m origin offset, mask pin, endpoint convention, and `hearing_y_hard_cutoff` remain single-sourced in `entities.yaml`.

6. **Noise never charges the accumulator, only Investigate.** Hearing triggers **Investigate/orientation** (FSM consumes the relay), never `dA/dt` and never `R` charge. Sustained LOS after arrival may then charge `A`/`R`, but noise is not a backdoor to Chase (concept: "noise never feeds the accumulator — only LOS does"). `Burst` vs `movement` attribution is carried for the Suspicion Attribution trace (R12) and for the `R_noise_share` consequence below.

7. **Fruitless noise has a bounded in-world consequence.** A noise-triggered Investigate that resolves with the player unfound (`Investigate-resolution cause: fruitless`) accrues a bounded share of residual wariness via Perception F6: `per-event add = min(R_noise_share 0.15, c_noise 0.20)` (effective 0.15 at starters, clamped `R + add ≤ R_max 1.00`). Camera trips mirror this; LOS exposure still charges `R` in parallel at `r_res 0.10`. This is the anti-puppeting lever — repeated tool use or fruitless luring tightens `T_entry` visibly (sinking marker) without ever directly grade-penalizing clean tool use (residual is excluded from the grade integral). **Corroboration rule (Blocker 6 Pillar-1 fix):** a guard that receives a *second* `Noise heard` (any `fact_id`) within `t_noise_corroborate_window 6.0 s` of a prior noise in the **same investigative area** (`d_investigate ≤ r_investigate_error 1.5 m` from the prior commit) **escalates** to the FSM's **peer-recruit** leg instead of debouncing — the guard recruits a peer (if available, no LOS, no Investigate active for the peer) and the original Investigate is held open until the peer's arrival or `t_noise_corroborate_window` expiry. A *third* `Noise heard` in the same area within the window escalates to **locked zone** (zone-wide alert, no Investigate commit — the entire patrol area is suspect). The corroboration rule is the *positive* leg of Pillar 1: a thinking guard should *escalate* with more noise, not debounce. The 2.0 s `t_noise_recommit_cooldown` (B11) applies to the *same-guard* re-commit to the same fact — the corroboration rule is orthogonal. **Cooldown start (Blocker 6 pin):** the `t_noise_recommit_cooldown 2.0 s` starts from the **Investigate resolution timestamp** (not from chase-end resolution, which is not a noise event), and applies to the same guard re-committing to any noise at the same area. The cooldown is the *single-guard* anti-spam; the corroboration rule is the *multi-guard* anti-puppeting.

8. **Composes with hiding, patrol, and alert.** Inside a `HideSpot` (`Occupied`), vision is disabled but hearing, spot-front verification, and every other channel are unaffected (Perception E3 / HideSpot Core Rule 6) — a hidden player who walks/runs inside can be heard and pull a **hunch** that **never breaks spots** (hunches resolve fruitless; the hunch is telegraphed — a head-cock/lean, never a grab). A guard holding a live `HideSpotFront` fix is **noise/alert-immune** (Perception R14) — Burst does not interrupt spot-front verification. Alerts (`Alert propagation` #4, Target tier) are area-imprecise and never chase on rumor; a guard who gains personal sustained LOS always promotes to chaser regardless of alert/hearing state.

9. **Determinism and budget.** The emitter publishes **aperiodically, synchronously with its input**: one `Noise heard` per `step_event` (movement) and one per Burst landing — never on a ticked cadence of its own, never polled by the hearing system. Perception's hearing reaction latency is **owned by Perception and bounded by `T_hearing = 0.2 s` (B8)**: the shared tick authority (2–5 Hz, `T_sample 0.5 s` worst-case) runs a **5 Hz hearing sub-evaluation** — radius check + one Linecast per in-range guard, ~1–10 µs — so worst-case reaction latency collapses from `T_sample_max 0.5` to the staircase `t_audible = t_commit + n·T_hearing`, `n ∈ {0, 1}`, bounded by `T_hearing`, with exact timestamp equality at commit (no phantom emitter tick). Vision (LOS) evaluation keeps the 2–5 Hz cadence; the hearing sub-eval is additive to it, never consuming the same slot. The relay publishes at most one event per `step_event` and one per throw, independent of guard count. **Hearing sub-eval tick mechanism (Blocker 5 pin):** the 5 Hz sub-eval runs on a **`MonoBehaviour.Update()` + accumulator driven by `Time.unscaledTime`** (NOT `WaitForSeconds`, which respects `Time.timeScale` and breaks E17; NOT `FixedUpdate`, which couples to physics step and breaks determinism). The accumulator is initialized at `Awake()`, ticks only when `Time.timeScale > 0` (E17: pause suspends the sub-eval), and is reset on `OnEnable()` to prevent resume phantom commits. **Staircase distribution (Blocker 5 fix):** the hearing reaction is **NOT** a uniform distribution — `Update()` + accumulator with 5 Hz granularity produces a *staircase* of `{0, T_hearing}` quantized latencies at the starter bound, not `U(0, T_hearing)`. The F2 formula `t_audible = t_commit + U(0, T_hearing)` is **struck** and replaced with: `t_audible = t_commit + n·T_hearing, n ∈ {0, 1}` (the starter staircase is bounded by `T_hearing`); AC5's bound `t_audible ∈ [1.000, 1.200]` (with `t_commit = 1.000`) is still achievable but is asserted as a *bound*, not a distribution. **Performance + budget pin (Blocker 2):** the noise GDD asserts a **frame budget ≤ 2.0 ms total** for noise + perception + Burst simulator on the worst-case scene (30 guards in range, 1 Burst in flight). At ~1–10 µs per Linecast, hearing sub-eval alone is ~0.9 ms at 30 guards; Burst simulator at 120 Hz fixed substep is ~0.2 ms; aggregate (vision + hearing + Burst + NavMeshAgent) is budgeted to ≤ 12 ms total (60 FPS at 16.6 ms — the 4.6 ms slack is rendering + UI + audio). On WebGL's 33 ms frame, the budget must be re-validated by a perf bench before implementation sign-off (Blocker 2 standing obligation: create `tests/performance/noise-frame-budget.test` during the implementation/test pass; no test file is claimed by this design-only revision). **`LayerMask.GetMask` is called ONCE during initialization** and cached in a static mask field — never per-tick, never per-Linecast; the initialization path may be a static constructor or an idempotent `Awake()` guard. **`fact_id` is `ulong`**, allocated by the emitter at publish ingress, session-scoped, and wraps only at `fact_id_max = 2^64 - 1` (effectively never); allocator reset is a session-boundary obligation for the save/session system, not a player-respawn reset. **Subscription lifetime:** the emitter's MonoBehaviour subscribes to the controller's `step_event` at `OnEnable` and **unsubscribes at `OnDisable`** — no leak across scene unloads. **FSM input priority (Blocker 10 pin):** Perception **pushes** `Noise heard` to the FSM (event-driven, not a pull-polled query); the FSM subscribes via the event bus at FSM `OnEnable`, unsubscribes at `OnDisable`; the FSM never queries Perception directly. This makes E18 + the corroboration rule (CR7) testable in isolation — the FSM is fed a scripted `Noise heard` stream and asserts its own branch outcomes, not the Perception-side branch.

### States and Transitions

**Movement relay (stateless — the observable state is the controller's ledger, F5):**

| Emitter observes | Emitter publishes | Notes |
|------------------|-------------------|-------|
| `step_event {state=Walk}` | One `Noise heard` `R_walk 4.0`, at `step_event.position` | Ledger commit already materialized by Controller F5; remainder retained there |
| `step_event {state=Run}` | One `Noise heard` `R_run 6.0`, at `step_event.position` | Same — relay maps, never computes |
| No `step_event` (Idle/Crouch/no displacement) | **Nothing** | F5 freezes the ledger; no event exists to relay. The emitter never synthesizes one |

**Burst lifecycle (limited resource, MVP):**

| State | Event | Next | Notes |
|-------|-------|------|-------|
| `Placed` | player enters pickup reach (`pickup_reach_radius` 1.6 m) | `Carried` | `pickup-reached` telemetry event; one carry max |
| `Carried` | `Throw` input edge (#19 **Button** action — edge trigger, no Tap interaction; controller C0 defines no burst action) | `InFlight` | **Instant launch in camera azimuth** (no wind-up), throw-while-moving allowed; `BurstThrowConfig` raised-origin arc (F4); visible arc + landing ghost shown (transient, not HUD); input handles next throw only after `Consumed` |
| `Carried` | death / segment reset | `Carried` (retained) | Checkpoint snapshot — you had it, you keep it; **Placed** state is per-segment (respawns with the segment); **Carried** state is per-episode (survives via checkpoint); **InFlight** is killed and does not emit (Blocker 10 pin: an in-flight burst that "stops" on player death is *not* a reward — it does not emit) |
| `InFlight` | collision with `World/solid` | `Landed` | Position = contact point (+`normal × (projectile_radius + ε)` push-out to avoid inside-wall oscillation; **projectile_radius = 0.05 m** — if radius ≥ 0.05 the sphere re-inserts on next step; if < 0.05 it overlaps floor; push-out must place sphere center **above** surface); E20 mask excludes Player/Guard/trigger — burst passes through bystander guards (documented gameplay consequence) |
| `InFlight` | out-of-bounds / no collision in 3 s | `Landed` | Fallback: last simulated point at `t=3 s`; **despawn with red-fade arc** at `t=2.5 s` to warn the player; still emits once |
| `InFlight` | player death | **Killed — does not emit** (Blocker 10 pin: death kills the in-flight burst, no `Landed`, no `Consumed`, no refund) | A burst the player threw but did not see land is **lost** — a planning mistake, not a reward. Anti-puppeting: the player cannot throw-and-die to "free" a Burst landing. (NB: this is a CHANGE from the prior reading of the lifecycle table, which was ambiguous; the new rule is symmetric with the `Consumed` no-refund rule) |
| `Landed` | emit `Burst` noise (once) | `Consumed` | `R_burst 10.5`, single broadcast, one emitter-allocated `fact_id` (per-guard `entry_id` stays Perception-owned) |
| `Consumed` | death / segment reset | **stays `Consumed`** | A spend is never refunded — **no death-restore path** |
| `Consumed` | player reaches an **unconsumed** `Placed` pickup | `Placed` pickup → `Carried` | The only re-arm; pickup geometry is the resource gate |

*No other transitions exist. No "loud crouch" or "sprint-finisher" burst — crouch is silent (no `step_event`), Walk/Run commits are the only movement triggers, and Burst is exactly-one-per-pickup.*

### Interactions with Other Systems

| System | Dir | Interface |
|--------|-----|-----------|
| **Player Controller** (#11, Approved-terminal) | upstream | Consumes `step_event` stream `{state, position, stride_length_m, ledger_remainder_m, timestamp, publisher}` (C7) — the **sole** hearing + audio feed channel (F5g pin). The controller owns `V_walk 3.6 / V_run 6.25`, stride lengths, the global ledger `L_audible`, freeze-without-reset, `audible_armed`, and first-commit timing (F5c ≈0.551 / ≈0.456 s). The emitter consumes `r_noise` **never** (debug/telemetry only). |
| **Input** (#19, provisional) | upstream | `Throw` (**Button action edge** — instant fire on the edge, no Tap interaction, when Burst `Carried`) → `InFlight`; `Interact` (edge, pickup reach) → `Carried`. Camera azimuth directs the throw (B7). Controller C0 defines no burst action — the verb is emitter-owned, wired through #19. |
| **Perception** (#2, Approved) | consumed-by | Consumes the raw `Noise heard` publication `{kind, source, position, radius, fact_id, timestamp, publisher}` and emits a per-guard relay that adds `{guard, entry_id, consumption}`. Perception R5/F12 does radius + planar `d_noise` + soft Y-delta falloff (end `hearing_y_hard_cutoff 4.0 m [3, 5]`) + **binary Linecast** (0.25 m origin to guard eye, shared E20 mask — World/solid minus {Player, Guard, trigger}, `Ignore` triggers), broadcasts per-guard, tracks per-guard consumption, **and owns `entry_id` allocation (first-noise-heard)**. **`fact_id` is emitter-allocated and retained on every relay** — the R12 schema amendment is complete. Perception never calls back into the emitter. |
| **Guard AI FSM** (#1, Approved rev 4.1) | downstream via Perception | FSM consumes Perception's `Noise heard` relay; noise seeds **Investigate** (never Chase) with `r_investigate_error 1.5 m` area error on commit target; `HideSpotFront` holds are **noise-immune** (R14). FSM publishes `Investigate-resolution cause=fruitless` which feeds F6 residual share. **Authored (B11)**: the FSM drops noise during a live episode and within `t_noise_recommit_cooldown 2.0 s` post-resolution — prevents Investigate-spam chains; the FSM never opens Chase on noise alone. |
| **HideSpot** (#5, In Review) | composed | Inside `Occupied`, movement noise still emits (its `step_event`s relay normally); hearing does **not** defeat LOS-invisibility. Hunch Investigates never capture (FSM C1.0/C1.3) and are telegraphed (head-cock/lean). Burst never interrupts a live spot-front verification. |
| **Level / Content** (#8, undesigned, provisional) | authored | Authors Burst pickup positions per room (**≥1–2 per room; ≥1 Burst-only route per major area is concept-gated**; pickup reachability is the re-arm resource). Authors **acoustic gaps/portals as real geometry** that shape the hearing Linecast graph (B9): the cast is **binary** — a green-lit gap passes sound at full radius, a solid surface blocks at zero; there is no authoring category between (`AcousticOccluder`-attenuate semantics struck). Certifies throw-range reachability against F4 and keeps throw volumes clear of ceiling-clamp (E19); multi-story atriums and mezzanines are feasible (soft Y-delta falloff, B10 — no horizontal-floor investigative-corridor authoring rule; the prior hard-cliff is removed). |
| **Event bus** (#15, undesigned, provisional) | transport | Topic-based bus (provisional). `Noise heard` is the hearing contract; `pickup-reached` is telemetry-only for claim-3. Guarantees (at-least-once, ordered per publisher) finalized by bus ADR. |
| **Physics** (#18, undesigned, provisional) | upstream | `f12_noise_origin_offset 0.25`, `World/solid` mask via `LayerMask.GetMask` (**Player excluded**), `QueryTriggerInteraction.Ignore`, `Physics.Linecast`, Burst projectile collision (`SphereCastNonAlloc`, kinematic) — pinned by Perception F12/this GDD; finalized by Physics GDD/ADR. |
| **Suspicion/Grade** (#7, undesigned, provisional) | downstream | Fruitless noise investigations accrue `R_noise_share` residual (F6) — in-world consequence, **never** a direct grade input. Noise never appears in the grade integral (forgiveness floor); `cap-forced Investigates` are non-grading; any later LOS that reaches Chase emits the one Chase escalation event (Chase-wins, R13). Residual visibility (the sinking marker) lands with this GDD — until then Burst's *felt* cost is the pick-up route + the orient tell, not the meter. |
| **Audio & UI feedback** (#13, undesigned) | emits | ONE merged footstep voice on `step_event` (see Visual/Audio): a single gated voice with internal body+accent auto-blend, fired frame-exact with the ledger commit — **not** an at-animation-contact Foley and no second at-contact layer (B4). The emitter's controller-side **`rustle_event`** fires once per stride at `rustle_threshold 0.70 [0.60, 0.80]` of the current stride (B5) — player-local, audio-only, never a hearing event. Burst throw/land cues; guard-proxy scuff cue (B6); guard-orient tell via guard motion. |
| **Alert propagation** (#4, Target) | orthogonal | Alerts are area-imprecise investigations from peer guards, not noise events. An alert during a `HideSpotFront` hold is ignored; an alert plus a later `Noise heard` targeting the same area still yields one Investigate (tier-scoped suppression, R12). |

*Provisional flags: Event bus + Physics + Level + Grade are undesigned — contracts defined here, finalized by those GDDs. The 2–5 Hz staggered tick, `T_sample 0.5` (vision), the 5 Hz hearing sub-eval (`T_hearing 0.2 s`, B8), and `stopping_distance 0` are consumed, never re-tuned here.*

## Formulas

This GDD owns **no movement-ledger arithmetic** — the ledger is Controller F5, and this GDD re-derives nothing. It owns the **relay mapping**, the **Burst throw formula**, and the **joint-validator legs**.

### F1 — Movement publication mapping (relay contract)

```
On consuming {step_event}: publish exactly one
  Noise heard {
    kind        = movement,
    source      = player,
    position    = step_event.position        // feet world XZ+Y
    radius      = N(state): Walk → R_walk 4.0, Run → R_run 6.0
    fact_id     = emitter-allocated at publish ingress (one per fact, shared by all hearing guards)
    consumption = pending (per-guard, Perception-owned)
    timestamp   = step_event.timestamp       // commit time, not hearing time
    publisher   = NoiseEmitter }
    // note: `entry_id` is NOT in this payload — Perception allocates it per guard at first-noise-heard (R5) and relays it downstream
```

| Variable | Meaning | Source / Range | Notes |
|----------|---------|----------------|-------|
| `step_event` | Controller stride-commit event | C7: `{state, position, stride_length_m, ledger_remainder_m, timestamp, publisher}` | F5g: sole hearing/audio channel. At most one per Tick |
| `N(state)` | Audible radius for the committed state | Walk `4.0` / Run `6.0` (registry `noise_radii`) | Crouch/Idle never commit → no event exists to map |
| `position` | World feet XZ+Y | `step_event.position` | Perception lifts by `f12_noise_origin_offset 0.25` for the ray |
| `fact_id` | Transport/dedup token | Emitter-allocated per published fact (B2) | One value per fact, identical to every hearing guard; per-guard `consumption` is separate |
| `entry_id` | Episode id (per guard) | **Perception-allocated** at first-noise-heard (R5), relayed downstream — never carried in this payload | One value per guard per episode; the emitter never allocates or reads it |
| `timestamp` | Commit wall-time | `step_event.timestamp` | Hearing time = later (Perception tick) |

**No ledger, no thresholding, no freeze/reset, no invariant.** Every stride-commit is already materialized by Controller F5; the relay maps committed facts to `Noise heard` facts and nothing more. The false invariant `L ∈ [0, 2.6)` is **retired** — it belonged to a ledger this system does not own.

### F2 — Commit-to-heard latency (consumed, not derived)

```
t_commit   = controller F5c: ≈0.551 s (Walk, 1.9 m @ 3.6 m·s⁻¹), ≈0.456 s (Run, 2.6 m @ 6.25 m·s⁻¹)
t_audible  = t_commit + n·T_hearing, n ∈ {0, 1}   // staircase (5 Hz sub-eval, Blocker 5 pin) — was U(0, T_hearing), struck 2026-08-27 re-review (misleading uniform; implementation is a quantized staircase)
```

The emitter publishes at `t_commit` (synchronous with `step_event`); **hearing** cannot occur earlier than publish and is witnessed on Perception's dedicated 5 Hz hearing sub-evaluation — bounded by `T_hearing 0.2 s` (B8), **not** the 2–5 Hz vision tick (`T_sample` stays a vision-cadence bound only). Exact timestamp equality at commit: the hearing timestamp equals `t_commit` (unit-testable), never a phantom emitter tick. This GDD does not recompute `t_commit`; the controller's F5c derivation is the single source (the noise GDD's prior `0.528/0.416 s` values contradicted the Approved controller and are struck).

### F3 — Validity constants (single-sourced in `entities.yaml`)

All values and safe ranges are **read from `design/registry/entities.yaml`**, never restated here (registry-dedup policy). The GDD keeps only each constant's *role* and the joint-validator legs that bind them at load.

| Constant | Role |
|----------|------|
| `R_walk` / `R_run` / `R_burst` | Movement walk / run / Burst-landing broadcast radii (`noise_radii`; co-signed) |
| `R_vis` | Vision radius — the noise radius-ordering cap references its band |
| `f12_noise_origin_offset` | Linecast origin `feet + 0.25` (clears the 0.20 m stair riser; locked) |
| `stride_length_walk` / `stride_length_run` | Controller-owned commit thresholds; co-signed here |
| `navmesh_sample_maxdistance` | Investigate-target sample (FSM-owned), **not** a hearing veto |

**Joint validator legs (H.0.7 noise legs — every loaded config, asserted at load like the `V_crouch` precedent):**
1. **Radius ordering (HARD — load rejection):** `R_walk < R_run < R_burst ≤ R_vis` in the loaded config. Band corners that violate (e.g. `R_burst 14 > R_vis 10`) are **rejected at load**, not silent — the independent safe ranges admit illegal corners by design and the validator is the gate, exactly as `V_crouch < V_patrol` is asserted.
2. **Stride ordering (HARD):** `stride_length_run > stride_length_walk` (H.0.7 already encodes this; this GDD co-signs).
3. **Cadence monotonicity (HARD — load rejection, F3(3) adjudication 2026-08-27):** `V_walk / stride_length_walk ≤ V_run / stride_length_run` is asserted at load. Starters `3.6/1.9 = 1.89 ≤ 6.25/2.6 = 2.40` ✓; the inversion corner Fast Walk `4.4/1.5 = 2.93` vs Slow Run `5.0/3.2 = 1.56` is **rejected at load** because a "safer" state emitting pips more often than a "louder" state breaks Pillar 2 (Fair Mind-Challenge) readability — the player cannot predict commit cadence from a slower-and-quicker state. The ratio is also **ratio-logged** on the load report for the Pillar-2 audit trail (deleting the soft log is reserved for the load-rejection future, not the current pass). The prior ADVISORY downgrade (B3) is **hereby struck** — the hard-reject is the right gate for the 2026-08-27 GDD state.

### F4 — Burst throw range (locked, `BurstThrowConfig`)

```
R = v₀·cosθ·(v₀·sinθ + √((v₀·sinθ)² + 2·g·h_release)) / g   # raised origin, same height
R      ∈ [8, 12] m               # safe band (readability: short enough to demand positioning, long enough to pull across a room)
v0     = 10.0 m/s                # starter (BurstThrowConfig; per-segment tunable)
θ      = 38°                     # starter (BurstThrowConfig)
h_release = 1.5 m                # throw release height above feet — stance-independent (B1)
g      = 9.81 m/s²               # down
dt_max = 1/120 s                  # arc integration fixed substep (no async, no adaptive step — locked)
projectile_radius = 0.05 m         # SphereCastNonAlloc projectile radius (pinned, Blocker 3)
```

Starter expected range: `10.0·cos38°·(10.0·sin38° + √((10.0·sin38°)² + 2·9.81·1.5))/9.81 ≈ 11.5 m` — the release height adds ≈1.6 m over the flat-ground form (`v0²·sin(2θ)/g ≈ 9.9 m`, **struck: the emitter throws from chest height, so the flat form underestimates**). Band ⇒ `v0 ∈ [8.1, 10.2] m/s` at the locked `θ = 38°`, `h_release 1.5 m`. Arc apex ≈ 1.93 m at v0=10.0 (ceiling-clamp edge case, see E19). **Three-tier tolerance (B1):** (1) **pure-math** — landing range vs formula at 1e-6 relative; (2) **exactly-once heard** at contact (AC6); (3) simulated landing vs formula within `δ_sim = v0·cosθ·dt_max + 1e-3 ≈ 0.134 m` — the discrete-step bound (`dt_max = 1/120 s` arc integration step; the +1e-3 covers vertical drift `g·dt²/2` over the hit-box). **Simulator contract (Blocker 1 pin):** `dt_max = 1/120 s` is a **fixed substep** (NOT adaptive, NOT frame-coupled); the Burst simulator is **NOT** on the Unity main physics loop — it is a dedicated, fixed-step integrator that calls `Physics.SphereCastNonAlloc` per substep. **Determinism mode:** `Physics.simulationMode = FixedUpdate` (not `Update`), single-threaded, no async. The simulator therefore uses `Time.fixedDeltaTime = 1/120 s` and runs at a deterministic 120 Hz regardless of frame rate — on WebGL's 33 ms frames, the simulator still produces 4 substeps per frame, the same as on PC's 16.6 ms frames, and `δ_sim` does **not** double. **Flat-ground assumption (Blocker 1 disclosure):** F4's raised-origin form assumes same-level launch and landing surfaces — a 0.20 m stair riser at the landing site deviates the simulated landing from the formula by ≈0.19 m (above `δ_sim ≈ 0.134 m`). The Band [8, 12] is **certified only on flat ground**. Level authors certify throw volumes: either (a) throw volume is on a single horizontal plane, **or** (b) the level certifies the actual landing distance against a per-segment `v0`/`θ` adjustment for that landing height; no additional runtime landing-height knob is implied. The default is (a) — a stair riser at the landing is a level-author bug, not a GDD violation. Level tunes `v0/θ` per segment within the band; the burst reachable-guard count and the ≥1 Burst-only route per area are certified against this formula. Flight is kinematic `SphereCastNonAlloc` (no Rigidbody), `projectile_radius = 0.05 m`; collision stops flight and `position = contact + normal × (projectile_radius + ε)` (Blocker 3 fix).

### Referenced Perception formulas (not owned)

- Residual decay `R(t)=R(t0)·exp(−(t−t0)/τ_res)` and fruitless add `min(R_noise_share 0.15, c_noise 0.20)` with clamp `R ≤ R_max 1.00` (Perception F4/F6) — noise contributes only via `Investigate-resolution cause=fruitless`.
- Entry threshold `T_entry(R)=max(T_base−k_res·R, T_floor)` (Perception F5) — residual tightens timing, never grades noise directly.

## Edge Cases

Compact matrix — each row is `if <condition>` → `then <outcome>`, never "handle gracefully".

| # | Edge / Trigger | Condition | Outcome | Why / Cross-ref |
|---|----------------|-----------|---------|-----------------|
| E1 | **Crouch/Idle silent** | `state ∈ {Idle, Crouch}` for any duration | No `step_event` → nothing to relay → no `Noise heard`, no rest of the ledger | Controller F5 freezes not resets; displacement honesty. Crouch silent by contract |
| E2 | **Walk/Run inside `Occupied` HideSpot** | Player walks/runs while `HideSpot = Occupied` | `step_event`s relay normally → emit (`R_walk`/`R_run` at feet) → guard may pull a **telegraphed hunch** that never breaks the spot | Hide disables vision only (Perception E3 / HideSpot CR6); hunches resolve fruitless (FSM C1.0/C1.3) |
| E3 | **Tap-inching / key segmentation** | Walk 0.6 m → idle 5 s → walk 0.6 m → walk 0.7 m | Ledger (controller) accumulates `0.6+0.6+0.7=1.9` across pauses; commits once on the third walk segment; idle **never subtracts** | Freeze-without-reset (F5) closes the segmentation exploit; the emitter just relays the eventual commit |
| E4 | **Rapid stance spam Walk↔Crouch** | Oscillation at any frequency | Ledger advances only during Walk ticks, freezes during Crouch; emission cadence is displacement-driven | No timer to game; threshold is distance, not cadence |
| E5 | **Movement against a wall / zero displacement** | Walk/Run input, `|Δposition_XZ| = 0` | No `step_event`, no `Noise heard` | Displacement honesty (F5(a)) — held keys never bill distance |
| E6 | **Backpedal / diagonal movement** | `dot < −eps_d` or diagonal input while `Walk/Run` | Ledger bills **actual XZ displacement** (already scaled by `backpedal_factor 0.70` / diagonal normalization in Controller F1/F5) | Distance is distance — backpedal is quieter because it moves less, never because it's exempt |
| E7 | **Burst lands off-NavMesh / in chasm** | No NavMesh hit within `navmesh_sample_maxdistance 0.4` at contact | **Still emits once** at the contact point (`R_burst 10.5`); each guard's hearing Linecast + radius gates audibility | Emit-at-contact (CR3); NavMesh is not a hearing occluder, geometry is |
| E8 | **Burst inside-wall false occlusion** | Flight collides inside a thin solid lip | Emit at `contact + normal × 0.05`; wall then occludes most guards via the Linecast | Guards at the contact point still hear (raised origin, player layer excluded) |
| E9 | **Burst flight exceeds 3 s with no collision** | Thrown into sky / void | Falls back to **last simulated point**, emits once there, then `Consumed` | Exactly-one emission per throw; no "lost Burst" stalemate |
| E10 | **Carried-unthrown through death/reset** | Player dies while `Carried` | Burst **retained** (`Carried` → `Carried`, checkpoint snapshot) | You had it, you keep it — a spend is the only loss. `Placed` pickups respawn with the segment; the resource gate is the pickup's authored position |
| E11 | **Spent Burst through death/reset** | Player dies/segment-resets while `Consumed` | **Stays `Consumed`** — death never refunds a spend | Limited-resource re-arm (CR3); anti-farm, concept:145 |
| E11b | **InFlight burst on player death (Blocker 10)** | Player dies while `InFlight` | **Killed** — the in-flight burst does not emit, does not land, does not refund | Death kills the burst (no `Landed`, no `Consumed`). The player threw it; they do not get it back. Anti-puppeting: cannot throw-and-die to "earn" a landing. (Changed from prior ambiguous table entry — the InFlight→Landed→Consumed reading would reward death throws) |
| E12 | **Single-carry enforcement** | Player attempts to pick up a second Burst while `Carried` | Second `Placed` pickup stays `Placed`; `Throw` addresses only the carried instance | One carry max (CR3) |
| E13 | **Multiple guards in radius** | Single `Noise heard` with `R = 4.0/6.0/10.5` | **Broadcast to every in-range guard** on one shared `fact_id`; each guard owns its own Perception-allocated `entry_id` (first-noise-heard) and consumes its relay at most once per episode; one guard's consumption never invalidates another's | R5/R12 contract; `fact_id` emitter-allocated, `entry_id` Perception-allocated (B2) |
| E14 | **Guard holding `HideSpotFront` fix** | `HideSpotFront` verification dwell active | Guard **noise/alert-immune** — relay ignored until hold resolves | Perception R14 + FSM R14; Burst never interrupts spot-front verification |
| E15 | **Noise while guard is in Chase** | `Noise heard` published while any guard is in `Chase` for that episode | Relay **stale after `Chase-end` dropped, never re-heard**; Chase never extends on noise alone | FSM Edge Cases "Stale noise relay after Chase-end" + Perception R12 single-consumption — a chasing guard listens to LOS/reachability, not hearing |
| E16 | **Y-delta / ledge hearing** | Noise source and guard on different vertical surfaces, large `|ΔY|` | Hearing evaluates `d_noise` (XZ-plane) + binary Linecast, with a **soft attenuation leg** on the Y-delta: full `R` at `|ΔY| = 0`, linearly falling to 0% at `|ΔY| = hearing_y_hard_cutoff 4.0 m [3, 5]`. There is **no hard discontinuity** at the cutoff — the prior hard vertical cliff is removed: 3.9 m ≈ 2.5% R, 4.1 m = 0% R. The horizontal-floor investigative-corridor authoring rule is struck; multi-story atriums and mezzanines are now feasible. Catch give-up `|ΔY|` gating (`delta_y_tolerance 1.0`) is separate: it applies to catch, never hearing | Hearing is planar-distance (`d_noise`) + soft Y-delta falloff; `hearing_y_hard_cutoff` (renamed from `hearing_y_tolerance`) is the falloff end-point, not a step |
| E17 | **Game pause / timeScale 0** | `Time.timeScale = 0` or window cancel / menu | No displacement → no `step_event` → nothing to relay; Perception tick authority (vision + 5 Hz hearing sub-eval) suspended; no phantom commits on resume | Ledger is displacement-driven, not wall-clock-driven |
| E18 | **Noise during a live noise-Investigate** | Guard is already investigating a prior `Noise heard`; a new relay arrives for the same area | FSM drops it **until the current resolution completes**, and within `t_noise_recommit_cooldown 2.0 s [1.0, 3.0]` post-resolution — prevents Investigate-spam chains (B11). The cooldown starts from the **Investigate resolution timestamp** (Blocker 6 pin), not from chase-end (chase is not a noise event). **Corroboration rule (CR7, Blocker 6):** a *second* `Noise heard` (any `fact_id`) within `t_noise_corroborate_window 6.0 s` of a prior noise in the **same investigative area** (`d_investigate ≤ r_investigate_error 1.5 m`) **escalates** to peer-recruit (a peer is recruited if available and not in Investigate); a *third* in the same area within the window escalates to **locked zone** (zone-wide alert). The corroboration rule is the *positive* leg of Pillar 1 — a thinking guard escalates with more noise, not debounces | **Authored in guard-ai-fsm.md** (noise-during-live-episode single-consumption + post-resolution cooldown + corroboration peer-recruit / locked-zone); the emitter stays colorblind — it publishes the fact; the FSM owns the branch |
| E19 | **Ceiling-clamp throw** | Interior ceiling below the arc apex (≈1.93 m at v0=10.0) | Flight terminates at ceiling contact; lands at `contact + normal × 0.05`, emits once | B1 — Level authoring note: keep throw volumes clear of ceilings near apex, or accept the shorter audible placement |

## Dependencies

### Upstream (this system consumes)

| System | What we consume | Provisional | Handshake |
|--------|----------------|-------------|-----------|
| **Player Controller #11** (Approved-terminal) | `step_event [state, position, stride_length_m, ledger_remainder_m, timestamp, publisher]` — the sole hearing/audio channel (F5g); own the stride/radius constants, the global ledger, freeze-without-reset, first-commit timing (F5c) | No | Controller GDD C7/F5 documents `NoiseEmitter` as the hearing consumer of `step_event`; this GDD consumes it. This GDD adds **no** new controller event — the stride-commit IS `step_event` |
| **Input #19** | `Throw` edge (when Burst Carried), `Interact` edge (pickup reach) | **Yes — provisional** | Controller C0 defines no burst action; #19 delivers the verb to the emitter |
| **Physics #18** | `f12_noise_origin_offset 0.25 m`, `World/solid` mask via `LayerMask.GetMask` (Player excluded), `QueryTriggerInteraction.Ignore`, `Physics.Linecast`, Burst `SphereCastNonAlloc` projectile channel | **Yes — provisional contract** | Physics GDD/ADR finalizes the pins owned here + Perception F12; this GDD declares the intended channel but does not execute the cast |
| **Event bus #15** | Topic `Noise heard` (event-driven bus), topic `pickup-reached` (telemetry) | **Yes — provisional contract** | Bus ADR finalizes topics, delivery guarantees, and `publisher = NoiseEmitter` attribution envelope |

### Downstream (systems that consume this one)

| System | What they consume from us | Handshake |
|--------|---------------------------|-----------|
| **Perception #2** (Approved) | Raw `Noise heard` publication `{kind, source, position, radius, fact_id, timestamp, publisher}`; Perception's per-guard relay adds `{guard, entry_id, consumption}`; `entry_id` stays Perception-owned and is allocated at first-noise-heard. Radii `R_walk 4.0 / R_run 6.0 / R_burst 10.5`; origin `feet+0.25`, endpoint guard eye; planar `d_noise` + soft `hearing_y_hard_cutoff` falloff; 5 Hz hearing sub-eval (`T_hearing 0.2 s`) | Perception R5/F12 owns radius + occlusion evaluation, soft vertical attenuation, broadcast per-guard, per-guard consumption, and `entry_id` allocation (unchanged). **`fact_id` is emitter-allocated and is retained on the per-guard relay.** The prior "entry_id moves to publish ingress" obligation is **countermanded** (wrong-direction; entry_id stays Perception-owned) |
| **Guard AI FSM #1** (Approved rev 4.1) | Perception's `Noise heard` relay → **Investigate/orientation** (never `dA/dt`, never Chase); publishes `Investigate-resolution cause=fruitless` → F6 residual; `HideSpotFront` noise-immunity (R14) | FSM GDD R5 consumes the relay; this GDD never calls the FSM. **E18 now authored in the FSM GDD** (noise-during-live-episode single-consumption + `t_noise_recommit_cooldown 2.0 s` post-resolution; FSM never opens Chase on noise alone) |
| **Suspicion/Grade #7** | Fruitless noise Investigates accrue `R_noise_share 0.15` bounded share (Perception F6, `c_noise 0.20`, clamp `≤ R_max 1.00`); noise never enters the grade integral | Grade GDD owns the witnessed-entry counting rule and forgiveness floor; this GDD only notes the residual side-effect |
| **HideSpot #5** (In Review) | Hearing is **not silenced** inside `Occupied`; `step_event`s relay normally; hunch Investigates never break spots, are telegraphed | HideSpot GDD CR6 documents the composition; this GDD re-asserts it |
| **Level / Content #8** | Burst pickup authoring per room (≥1–2; ≥1 Burst-only route per major area concept-gated); **acoustic gap/portal authoring as real geometry — the hearing Linecast is binary pass/block, no partial attenuation (B9)**; vertical separation authoring bounded by `hearing_y_hard_cutoff` (B10); reachability + ceiling-clamp clearance certification against F4 | Level GDD will verify AC geometry and certify pickup reachability as the re-arm resource |

### Bidirectionality check

Every row is mirrored in its counterpart's **Interactions** matrix where that counterpart exists: Controller F5/C7 → NoiseEmitter (Approved, verified); Perception R5/F12 ← NoiseEmitter (Approved — raw publication plus per-guard relay carry `fact_id`; `entry_id` attribution unchanged); FSM R5/R14 ← Perception ← NoiseEmitter (Approved — E18 and CR7 authored). Provisional rows (#15, #18, #7, #8, #19) are flagged as **contracts** until those GDDs/ADRs land — no silent re-tuning.

### Cross-doc amendments (status)

1. **perception.md R12 schema — COMPLETED 2026-08-28**: the raw NoiseEmitter publication and Perception per-guard relay now carry the emitter-allocated `fact_id`; the prior rev 3.4 obligation ("entry_id allocation moves to publish ingress") is **countermanded**: `entry_id` stays Perception-allocated at first-noise-heard.
2. **guard-ai-fsm.md — COMPLETED 2026-08-28**: E18 and CR7 are **authored** — noise-during-live-episode handling includes positive same-area corroboration (second noise → peer-recruit, third → locked-zone), ordinary unrelated-fact drop-until-resolution, post-resolution `t_noise_recommit_cooldown 2.0 s [1.0, 3.0]` (cooldown starts from Investigate resolution, not chase-end), and the negative leg (no Chase on noise alone); the stale-drop AC is in the FSM battery (B11).
3. **guard-ai-fsm.md — corroboration rule COMPLETED 2026-08-28**: FSM authors CR7: 2nd `Noise heard` in the same investigative area within `t_noise_corroborate_window 6.0 s` → peer-recruit; 3rd → locked zone. The constant is registered in `entities.yaml`. This is a **Pillar 1 fix**: a thinking guard escalates with corroborating noise, not debounces.
4. **guard-ai-fsm.md — cadence HARD-reject leg COMPLETED 2026-08-28**: the FSM adopts the H.0.7 cadence-monotonicity hard-reject leg (was ADVISORY, now HARD load rejection). No FSM code changes were required — validation runs at config load before the FSM boots.
5. **Registry — COMPLETED 2026-08-28**: cadence-monotonicity validator leg (F3) is **HARD load rejection** (Blocker 9 adjudication); the ratio remains logged on the load report for the Pillar-2 audit trail.
6. **Registry — COMPLETED 2026-08-28**: new constants registered — `T_hearing 0.2 s`, `hearing_y_hard_cutoff 4.0 [3, 5]` (renamed from `hearing_y_tolerance` — soft attenuation leg, Blocker 8), `pickup_reach_radius 1.6 [1.0, 2.5]`, `throw_release_height 1.5`, `rustle_threshold 0.70 [0.60, 0.80]`, `t_noise_recommit_cooldown 2.0 [1.0, 3.0]`, `t_noise_corroborate_window 6.0 s` (Blocker 6), `fact_id` in the Noise-heard trace schema, `burst_throw` raised-origin band note `v0 ∈ [8.1, 10.2]`, `projectile_radius 0.05 m` (Blocker 3), `dt_max 1/120 s` (Blocker 1).

## Tuning Knobs

Grouped by the feel axis they move. **Every knob value and safe range below is single-sourced in `design/registry/entities.yaml`** (registry-dedup policy) — this section records only *what each knob moves* and its retune guardrails. Retuning must still pass the joint validation legs (F3) on the loaded config.

### 1 — Stealth ladder (silence has a cost)

| Knob | What it moves | If too high | If too low |
|------|---------------|-------------|------------|
| `V_crouch` | Crouch silence speed | Crouch approaches patrol speed — silence becomes free | Crouch feels glued; player never chooses silence voluntarily |

Joint guardrail (registry, asserted at load): `V_crouch < V_patrol` in every config — a crouching player can never outrun even a patrolling guard on open ground.

### 2 — Ledger pacing (co-signed; controller-owned)

| Knob | What it moves | If too high | If too low |
|------|---------------|-------------|------------|
| `stride_length_walk` | Walk distance before one `R_walk` pip | Walk nearly silent; risk ladder collapses | Walk pips immediately; indistinguishable from running |
| `stride_length_run` | Run distance before one `R_run` pip | Run forgiving; sprint-spam dominates | Run punishes any urgency |

Both are Controller F5 knobs co-signed here (registry `1.9 m [1.5, 2.4]` / `2.6 m [2.0, 3.2]`); the freeze-without-reset semantics are controller-owned. Joint legs: stride ordering + cadence monotonicity (F3) bind every loaded config. The `t_commit ≈ 0.40–0.55 s` grace windows are F5c-derived (registry stride + speed), never re-tuned here.

### 3 — Radii (how far sound carries)

| Knob | What it moves | If too high | If too low |
|------|---------------|-------------|------------|
| `R_walk` | Walk hearing radius | Hear-through-walls; Level occlusion authoring fights the radius | Walk never reaches a guard |
| `R_run` | Run hearing radius | Run pulls the whole room; no local luring | Run barely louder than walk |
| `R_burst` | Burst landing radius | One Burst clears a wing | Burst feels like a loud footstep |
| `f12_noise_origin_offset` | Ray origin height over feet (locked) | Unrealistic riser clearance | 0.20 m riser graze-blocks adjacent guards |

Joint leg: `R_walk < R_run < R_burst ≤ R_vis` asserted at load — band corners that violate are rejected, never silently tuned around. The starter config asserts `R_vis ≥ 10.5` (the Burst radius); if `R_vis` is below 10.5 the F3(1) load-rejection leg rejects the config (Blocker Recommended #3) — there is no path where a Burst can be heard further than the vision radius, so `R_vis ≥ R_burst` is a hard floor on the vision GDD.

### 4 — Burst feel (tool placement + economy)

| Knob | What it moves | Guidance |
|------|---------------|----------|
| `BurstThrowConfig.v0 / θ` | Placement authorship — short enough to demand positioning, long enough to pull across a room | F4 raised-origin `R = v₀·cosθ·(v₀·sinθ + √((v₀·sinθ)² + 2·g·h_release))/g`; starter (10.0 @ 38°) + safe band `[8, 12] m` single-sourced in the registry `burst_throw` block; band ⇒ `v0 ∈ [8.1, 10.2]` |
| `throw_release_height` | Release height above feet (`h_release 1.5 m`, stance-independent) — stiffness of the raised-origin arc | Raising it shortens range at fixed v0; the F4 band constrains it — keep the starter landing in-band |
| `pickup_reach_radius` | How close the player must get to a `Placed` pickup to arm it | Registry `1.6 m [1.0, 2.5]` — floor stays above `r_player 0.35` so the prompt reads before contact |
| Pickup count per room | A missed throw loses the tool for that life — 2 pickups/room (one deep) softens it | Level-authored; **the re-arm resource** |

### 5 — Residual consequence (fruitless noise is not free)

| Knob | What it moves | If too high | If too low |
|------|---------------|-------------|------------|
| `R_noise_share` | How much each fruitless Investigate tightens `T_entry` (effective add `min(R_noise_share, c_noise)`) | Repeated lures brick the meter | Puppeting free; Burst spam with no visible wariness |

Values (`R_noise_share 0.15 [0.10, 0.25]`, `c_noise 0.20`, `R_max 1.00`, `τ_res 16 s [12, 20]`) are registry-sourced; shares sum and clamp `R + add ≤ R_max`, decay `R(t)=R(t0)·exp(−(t−t0)/τ_res)` — all owned by Perception, consumed here.

### 6 — Budget (hearing sub-eval, not a noise knob but Level should know)

| Knob | What it moves | Note |
|------|---------------|------|
| `T_hearing` | Hearing-reaction bound (registry `0.2 s`; the 5 Hz hearing sub-eval cadence, B8) | Perception runs a **dedicated 5 Hz hearing sub-evaluation** (radius + one Linecast per in-range guard, ~1–10 µs) on the shared tick authority; hearing latency is the staircase `t_audible = t_commit + n·T_hearing`, `n ∈ {0, 1}`, bounded by `T_hearing` — the emitter publishes aperiodically (at commits/landings); `T_hearing` bounds **hearing latency**, never emission cadence. `T_sample 0.5` remains the vision-cadence bound. |

## Visual/Audio Requirements

This system **owns** the merged footstep voice contract, Burst throw/land cues, the `rustle_event` pre-pip tell, and the guard-proxy scuff contract; it **delegates** meter/residual visuals to Suspicion/Grade and guard orient/Investigate motion to Guard AI/Perception. Tone is **Cold Watch** — hearing is the building exhaling, never a horror sting.

### Audio (owned — merged voice + event cues)

| Cue | Layer | When | Description | Matters to game |
|-----|-------|------|-------------|-----------------|
| Footstep — single voice | **player-local** | Frame-exact with each `step_event`; **Walk/Run only** | ONE gated voice with internal **body + accent auto-blend** (timbre-separated, **with LUFS targets**): body layer `−18 LUFS` non-directional contact texture, accent layer `−12 LUFS` directional footfall (the audible "what guards hear"); the merge is a runtime gain-weight, not a second SFX resource. **Walk ↔ Run is differentiated by the auto-blend gain (Run pushes accent layer to 100%, body to 70%; Walk inverts)** — not by separate voices. | **Merged model (B4).** No separate at-animation-contact Foley, no second at-contact layer: the voice fires exactly when the pipeline emits `Noise heard`, never at an animation contact event, never during Crouch/Idle (no `step_event` exists to gate it) |
| Ledger-tension rustle | **player-local** | Once per stride when the ledger reaches `rustle_threshold 0.70 [0.60, 0.80]` of the current stride (controller `rustle_event`, B5) | A faint fabric/weight-shift rustle **before** the commit | Pre-commit tell answering Pillar-2 readability — the player hears the timer arming; audio-only, never a hearing event, fire-once-per-stride |
| Burst — Throw | player-local | Input `Throw` while `Carried` | Short, authored throw exertion + projectile whirr | One-shot; no loop |
| Burst — Land | hearing event | `Landed → Consumed` transition | Single, placed "thud/bloom" at the contact point with falloff to `R_burst` edge | Attenuation audio-only; hearing radius is the gameplay radius. Must not imply a radius larger than `R_burst` |
| Guard proxy (off-screen) | player-local (first-class, B6) | **First-class event (B6):** a guard commits to an investigate on a relayed noise the player cannot see, and the player has no LOS to the guard | Soft, distant footstep/orient scuff from that guard's announced direction | Payload `{fact_id, guard_eid, guard_world_pos@consumption, player_listener_pos}`; azimuth + occlusion-derived placement; **dedupe = 1 scuff per `fact_id` per guard** (one proxy cue per guard per fact, **no octant dedupe** — multi-guard investigation is a feature, not a bug, and octant dedupe undercounts it); through-wall dedupe is **not** performed (a guard who heard through a wall via the binary Linecast still emits proxy, and that is correct); answers "did anyone hear?" without a HUD; audio-only, delegated to #13 |
| Pickup — Reached | player-local | `Placed → Carried` | Subtle pickup confirm (UI-adjacent) | Telemetry-anchored `pickup-reached`, not a gameplay warning |

No persistent "you are loud" ambience, no hearing-cone hum — noise feedback is **event-based**, matching the discrete relay.

### Visual — Guard tell (delegated, but specified as the readability contract)

| Beat | Who owns it | What the player sees |
|------|-------------|----------------------|
| **Hear → Orient** | Perception R5 + FSM (consumes relay) | Guard head snaps / turns toward the **noise position**, not toward the player; if occluded, no turn — the player learns the wall blocked it |
| **Investigate commit** | FSM Investigate | Guard walks to the **area with error** (`r_investigate_error 1.5 m` around the noise point), short look-around sweep, gives up `fruitless` if not reacquired |
| **Hunch telegraph** | FSM + #21 | A hidden-player noise inside a HideSpot pulls a **head-cock/lean**, never a grab — the player reads "he suspects, he doesn't know" |
| **Residual consequence** | Suspicion/Grade (Perception F6) | Meter residual marker sinks slightly on `fruitless`; next `T_entry` visibly tighter — the in-world cost of puppeting, not a grade popup |

The orient tell must be **synchronous with the hearing sub-eval** (`T_hearing 0.2 s` worst-case, B8) — a hearing that fires with no visible turn reads as a bug.

### Visual — Not owned (explicitly out of scope)

- Emission pulse ring at the noise position — **not in MVP**. Guard orientation is the tell; a ring would be noisy UI and undercuts light-as-sound ("sound is light's twin").
- Residual pips, grade popups, or suspicion meter chrome — owned by Suspicion/Grade GDD.
- Guard vision cone / detection meter fill — owned by Perception GDD.

### Authoring notes

- The merged footstep **voice** must be locked **frame-exact to `step_event`**, not to animation step events — the body+accent blend fires only on the ledger commit; if any footstep ever sounds off-commit, the player hears a non-pip that wasn't a hearing event (the exact class F5(f)/F5g pins). The `rustle_event` tells remain audio-only and never trigger a hearing event.
- Burst land SFX position must be the **hearing position** (`+ normal × (projectile_radius + ε)`, identical to the position carried in `Noise heard.position`), not the pre-collision arc sample — SFX and hearing share the same point so the audio engine and the gameplay radius are co-located (push-out `normal × 0.05` would put SFX 5 cm outside the wall).

## UI Requirements

Player Noise owns **no persistent HUD**. Hearing readability is delivered through guard behavior, the rustle, and the Suspicion meter (all delegated). The only UI this system owns is the **world-space Burst pickup/throw prompt**.

| Element | Trigger | Content | Behavior |
|---------|---------|---------|----------|
| **Burst pickup prompt** | Player enters `Placed` pickup reach radius | World-space prompt: key hint (e.g., `[E] Pick up Burst`) + subtle highlight on the placed pickup | Shown only while `Placed` and in reach; hides on `Carried`; reappears on the *next* unconsumed pickup reached. Telemetry fires `pickup-reached` on entry. **Pickup emit rule:** pickup is **silent** — no `Noise heard` publishes on `Placed → Carried` transition; the only audio is the player-local pickup-confirm SFX. Authors must keep pickup-reach radius (`pickup_reach_radius 1.6 m [1.0, 2.5]`) clear of patrol routes so the player is not forced to break a patrol line to grab |
| **Throw hint** | While `Carried` | Minimal key hint (e.g., `[Q] Throw`) near the reticle or as a control bar item | Available only while exactly one Burst is carried; disabled during `InFlight`/`Consumed` |
| **No HUD element** | Always | No noise meter, no "you are loud" glow, no emission ring | Silence is a choice the player feels through consequence, not a bar they watch (anti-puppeting + Pillar 2) |

**Out of scope / delegated:** Suspicion meter / residual sink marker (Suspicion/Grade); guard vision cone rendering (Perception / Guard AI); inventory chrome — there is no inventory; one carry max is a world-state, not a bag.

**Accessibility / input notes:** prompts readable at the authored reach radius under URP; contrast not reliant on the pickup's emissive alone; all prompts mouse/keyboard navigable; no hold-to-pickup in MVP (reach + instant `Carried` keeps the loop tight). **Audio accessibility (Blocker Recommended #1):** the system **does not** rely exclusively on audio for critical feedback. A **visual noise-pip flash** (low-key, off by default, on in accessibility mode) accompanies each `Noise heard` publication — a transient ring at the noise position with color = `R_walk/R_run/R_burst`. A **haptic pre-commit** on gamepad (controller rumble at the `rustle_threshold 0.70` of the current stride) warns the player the commit is imminent, mirroring the audio rustle. A **subtitle-style "you are loud" ambient** (toggleable in accessibility mode) is rendered as a low-key text overlay when the player is in `R_walk` or `R_run` for an extended period. None of these are gameplay channels — they are alternative feedback paths so deaf / hard-of-hearing players can read the system's feedback.

## Acceptance Criteria

**Harness policy (mirrors Controller H.0, trimmed to the relay):** each AC below runs against the emitter's own test oracle — a `NoiseEmitter` fed a **scripted `step_event` stream / Burst input timeline** with a **`busSpy`** (records each raw NoiseEmitter publication with fields: `{timestamp, kind, source, position, radius, fact_id, publisher}`) and an optional **`relaySpy`/`fsmSpy`** cut in at Perception's per-guard consumption edge (records `{fact_id, entry_id, guard_eid, consumption, timestamp}` — each guard's relay and consumption). The relay harness **does not** embed Perception's or the FSM's behavior — delegated outcomes (occlusion, Investigate, metering) are asserted in those systems' own integration-test suites, cross-referenced below. **Comparisons use `CompareTolerance` ≤ 1e-5 m** (single-precision `Vector3` achieves `Mathf.Epsilon ≈ 1e-5 m`; the prior 1e-9 m requirement was impossible in Unity; a deterministic quantizer rounding the output to 1e-6 m achieves the tighter bound in pure-math contexts). **Tick counter (H.0.9):** the injected tick counter is **deterministic, monotonically increasing, resolution = fixed substep (1/120 s)**, initialized at `Awake()`, pause-resumed with `Time.unscaledTime` accumulation. **Every `Then` is observable by a QA tester; no `~`, no implied nondeterminism** — hearing latency is the {0, 0.2, 0.4, ...} staircase, asserted as a bound (staircase bounded by `T_hearing`). **Registry binding:** every expected value in the `Then` columns (radii, strides, origin offset, throw config) is read from `design/registry/entities.yaml` at suite setup — a registry retune re-binds each assertion without editing this file.

| ID | Covers | Given | When | Then |
|----|--------|-------|------|------|
| AC1 | CR1/F1 — Walk relay | `busSpy` armed; controller feeds one `step_event {state=Walk, position=p, stride_length_m=1.9}` | The event is consumed | Exactly one `Noise heard` publishes with `kind=movement, source=player, radius=R_walk 4.0, position=p, publisher=NoiseEmitter` and an emitter-allocated `fact_id` (no `intensity` field — struck) |
| AC2 | CR1/F1 — Run relay | Controller feeds one `step_event {state=Run, position=p, stride_length_m=2.6}` | The event is consumed | Exactly one `Noise heard` publishes with `radius=R_run 6.0, position=p` — and **no** other `Noise heard` in this tick |
| AC3 | CR2 — No phantom | Controller feeds no `step_event` (Idle/Crouch/no displacement for 10 s) | 10s sim | Zero `Noise heard`; the emitter never synthesizes from state — the sole trigger is a consumed `step_event` |
| AC4 | CR2/F1 — Shared fact_id at ingress | One `R_burst 10.5` landing in range of 2 guard taps | The landing publishes | The single `Noise heard` carries **one** emitter-allocated `fact_id`; both guard tap records reference that same id; per-guard `consumption` fields are distinct; no `entry_id` appears in the emitter payload (Perception-owned, B2) |
| AC5 | CR9 — Aperiodic emit + hearing bound | `step_event` commits at `t=1.00 s`, `T_hearing=0.2` | 2 s sim | The emitter publishes at `t=1.000` exactly (the event's timestamp; no phantom emitter tick); Perception witnesses the fact no later than `t=1.200 s` — `t_audible ∈ [1.000, 1.200]` |
| AC6 | CR3/F4 — Burst single landing emit | Throw from `h_release 1.5 m`, v0/θ at starters | Landing resolves | Exactly one `Noise heard` publishes with `kind=Burst, source=burst-landing, position=p, radius=R_burst 10.5`; **three-tier range check (B1):** (1) pure-math `R ≈ v₀·cosθ·(v₀·sinθ + √((v₀·sinθ)² + 2·g·h_release))/g` at 1e-6 relative; (2) exactly-once heard at contact; (3) simulated landing within `δ_sim ≈ 0.134 m` |
| AC7 | CR3 — Limited-resource re-arm | `Carried` throw consumes; then death, then segment reset | Both resets | The spent Burst stays `Consumed` across both — no publish, no `Placed` re-arm; the emitter publishes no Burst event from a reset alone |
| AC8 | CR3 — Carried survives death; re-arm at pickup | Throw *not* yet cast; death; then reach unconsumed `Placed` pickup | Each step | Carried-unthrown survives death (checkpoint snapshot); reaching the pickup emits `pickup-reached` once and arms `Carried`; a second pickup while `Carried` stays `Placed` |
| AC9 | CR3 — Contact point incl. push-out (Blocker 3 fix) | Throw collides with a wall at contact `c` (normal `n`) | Landing resolves | `Noise heard.position == c + n×(projectile_radius + ε)` within `CompareTolerance ≤ 1e-5 m`; `projectile_radius = 0.05 m`, `ε = 1e-3 m`; 3 s no-collision fallback emits once at last simulated point (E9), and the arc fades red at `t = 2.5 s` (E9 lost-Burst despawn); E20 mask excludes Player/Guard/trigger — burst passes through bystander guards (documented gameplay consequence) |
| AC10 | F3(1) — Radius ordering validator (HARD) | Loaded config with `R_walk ≥ R_run` OR `R_burst > R_vis` (band-corner probes, e.g. `R_burst 14 / R_vis 10`) | Config load | **Rejected** at load by the H.0.7 noise leg with a logged failure; starter config loads clean |
| AC11 | F3(3) — Cadence ordering HARD (load rejection, adjudication 2026-08-27) | Loaded config where `V_walk/stride_walk > V_run/stride_run` (e.g. Fast Walk 4.4/1.5 vs Slow Run 5.0/3.2) | Config load | **Rejected** at load by the H.0.7 noise leg with a logged failure; the ratio is also **ratio-logged** on the load report for the Pillar-2 audit trail; starter config (`3.6/1.9 ≤ 6.25/2.6`, ratio ≈ 0.79) loads clean |
| AC11b | F3(3) — Cadence log entry exists | Any config load (clean or rejected) | Config load | The Pillar-2 audit log entry is present in the load report, regardless of accept/reject outcome (ensures the audit trail is not silently dropped on a rejected load) |
| AC12 | CR6 — Noise never metering/Chase (delegated → integration test reference) | Guard hears a Burst at 3 m, no LOS | Hearing fires | **Relay-asserts:** the emitter published the Burst fact once (counts = 1, kind = Burst, radius = R_burst, fact_id matches). **Integration-asserts (not this suite):** Perception R5 + FSM `AC-FSM-21` — Investigate commits; `dA/dt=0`; no Chase on noise alone; E18 noise-during-live-episode drop + `t_noise_recommit_cooldown` post-resolution cooldown. The relay harness is **not** the place to assert FSM behavior. |
| AC13 | CR5 — Hearing geometry (delegated → integration test reference) | Wall-blocked guard vs riser-cleared guard | Same `Noise heard` | **Relay-asserts:** the emitter declared `position` and the `f12_noise_origin_offset 0.25 m` contract (one `Noise heard`, position = `step_event.position`). **Integration-asserts (not this suite):** Perception F12/AC-F12 — origin 0.25, endpoint eye, `World/solid` mask with Player excluded, `QueryTriggerInteraction.Ignore`, planar `d_noise` + soft Y-delta falloff (`hearing_y_hard_cutoff 4.0 m`). The relay harness is **not** the place to assert Perception Linecast. |
| AC14 | CR8/E2 — HideSpot composition | Player `Occupied`, walks/runs inside | Per `step_event` | Relay publishes normally at `R_walk`/`R_run` from feet; hunch-never-break is **asserted in HideSpot + FSM integration suites** (CR6/C1.0/C1.3); emissive silent-while-crouch re-confirmed by AC3 |
| AC15 | E13/E15 — Broadcast + stale drop | 3 guards in radius, one then enters Chase | After Chase-end | **Relay-asserts:** the Burst publication has `fact_id` and one consumption per (fact_id, guard) pair across all three guards' tap records; the relay publication is the same single fact (counts = 1, fact_id shared). **Integration-asserts (not this suite):** Perception R12 stale-drop-after-Chase-end + FSM E18 (Blocker 6 corroboration rule). The relay harness is **not** the place to assert stale-drop or corroboration. |
| AC6b | B7 — Throw-while-moving + camera azimuth (Blocker B7 + Blocker 9 Recommended #2) | Player carries Burst, moves at V_run, throws | Throw edge | Throw launches **instantly on edge input** (no wind-up, frame-1 launch); direction = camera azimuth at the throw frame; landing ghost renders at the predicted landing point; **all asserted by the relay harness, not delegated** |

**AC count check:** CR1→AC1-2, CR2→AC3, CR3→AC6-9, CR4→AC4/E13, CR5→AC13, CR6→AC12, CR7→(Delegated F6), CR8→AC14, CR9→AC5; F1→AC1-4, F2→AC5, F3→AC10-11, F4→AC6 — every Core Rule and every Formula is hit at least once. Delegated rows (AC12/AC13/AC15 borders, and residual ACC of knowing F6/F7) name the owning suite rather than duplicating an oracle this relay must not own.

## Open Questions

| # | Question | Owner | Target | Notes |
|---|----------|-------|--------|-------|
| OQ1 | **Event bus transport guarantee:** at-least-once vs exactly-once, ordering per publisher, and the relay-facts atomicity for the `Noise heard` topic — what delivery contract keeps broadcast per-guard lossless across `N` guards at one shared `fact_id`? | Tech / Event-bus #15 ADR | Technical Setup | Provisional now: `publisher=NoiseEmitter`, one emission per `step_event`/throw, no synchronous polling |
| OQ2 | **Physics pins finalization:** `f12_noise_origin_offset 0.25` + `World/solid` mask (`LayerMask.GetMask`, **Player excluded**) + `QueryTriggerInteraction.Ignore` + Burst `SphereCastNonAlloc` channel + transform-sync policy for the Linecast endpoint | Tech Director / Physics #18 ADR | Pre-milestone-0 (standing obligation #3) | Same umbrella as controller; the self-occlusion fix (mask excluding Player) is indexed here |
| OQ3 | **AudioSource pooling / mixer routing:** pool size and mixer routing for the merged footstep voice (body vs accent layers, B4) and the `rustle_event` tell (B5) — whether the rustle layers under the voice cleanly at `rustle_threshold 0.70` | Tech / Audio | Technical Setup ADR | The voice must stay frame-exact with `step_event` (F5(f)); the rustle tell stays audio-only and never gates a hearing event |
| OQ4 | **RESOLVED 2026-08-28 — Perception `fact_id` schema amendment (countermand of the prior `entry_id` direction, B2):** perception.md R12 now distinguishes the raw emitter publication from the per-guard relay and carries the emitter-allocated `fact_id`; `entry_id` **stays** Perception-allocated at first-noise-heard | Perception owner + TD | Completed in cross-doc sync | Two ids, two owners: `fact_id` = transport/dedup (one per fact, shared across guards), `entry_id` = episode (one per guard). The earlier "move `entry_id` allocation to publish ingress" trace was wrong-direction — recorded in Dependencies |
| OQ5 | **Grade consumption of witnessed-entry counting rule:** `Capture carve_out` slot consumption and "one Chase-COUNT escalation per spot-verification incident" (CR-CONCEPT-01/02) — when (which system) finalizes the counting rule so cap-forced vs noise/alert Investigates grade correctly | Grade #7 | Suspicion/Grade design pass | Carried from standing obligations — not a Noise decision; noise is **never** a direct grade input |

No other open questions are carried — safe-range retunes, content tuning (pickup counts, acoustic gap/portal placement), and per-segment `BurstThrowConfig` impulses are normal iteration, not OQs.

**Standing provisional flags in this GDD:** Event bus #15, Physics #18, Level #8, Suspicion/Grade #7, Input #19 — contracts defined here, finalized by those GDDs. The 2–5 Hz staggered tick, `T_sample 0.5` (vision cadence), the dedicated 5 Hz hearing sub-eval (`T_hearing 0.2 s`, B8), and `stopping_distance 0` are consumed, never re-tuned here.