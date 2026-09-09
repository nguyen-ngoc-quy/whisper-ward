# Consistency Failure Log

<!-- Auto-maintained by /consistency-check. Do not edit manually. -->
<!-- One entry per detected conflict, in chronological order. -->

| Date | GDD A | GDD B | Conflict Type | Status |
|------|-------|-------|---------------|--------|
| 2026-08-26 | entities.yaml (perception F11 / guard-ai-fsm D4) | player-third-person-controller.md | Model-semantics (V_chase auto-scale vs pinned-absolute) | Resolved |
| 2026-08-26 | player-movement-hide.md §Core Rule 5 | player-movement-hide.md §D1 (same doc) | Internal consistency — stale Euclidean-depth pin vs retired D1 OR-invariant | Resolved |

### [2026-08-26] — /consistency-check — 🔴 CONFLICT
**Domain**: Cross-system chase/movement constants (Guard AI FSM D4 · Perception F11 · Player Controller F2/G3)
**Documents involved**: `design/registry/entities.yaml` (`chase_speed_ratio`, `rho_chase` — product-form auto-scale language) vs `design/gdd/player-third-person-controller.md` :321 (V_chase PINNED ABSOLUTE, no auto-scale)
**What happened**: Registry presented `V_chase = rho_chase * V_run(runtime max)` as the operative definition while the Round-7-approved controller declares the starter 7.50 PINNED ABSOLUTE with derivation historical only. Zero numeric divergence anywhere (all five surfaces state 7.50 / bands / floors identically) — but a future tuner reading the yaml could retune V_run above the solo ceiling 6.81 expecting silent auto-scale, silently breaking the anti-kiting invariant (at V_run 7.0 the pinned 7.50 fails the 7.70 requirement).
**Resolution**: RESOLVED same session, registry-side only — rev-note added to `chase_speed_ratio` + `rho_chase` (product form = historical derivation + coordinated-retune rule), standalone `V_chase` entry added (7.50 pinned, cross-ref V_run solo-ceiling note rev 2026-08-25). Composed canonical model: pinned starter · solo ceiling V_run ≤ 6.81 · crossing governed by the ≥ 1.10× coordinated-retune invariant. No GDD edits required (guard-ai-fsm's "Consumed, never re-derived" already true under the composed model).
**Pattern**: When a review round strikes a derivation as "historical/non-operative", the consuming GDD's fix does not propagate to the registry automatically — route an explicit `/consistency-check` to annotate the REGISTRY entries, otherwise legacy definitional language keeps teaching the superseded model.

### [2026-08-26] — /consistency-check — 🔴 CONFLICT (internal, same-doc)
**Domain**: HideSpot authored geometry pins (Player Movement & Hide #5)
**Documents involved**: `design/gdd/player-movement-hide.md` — Core Rule 5 ("Interior depth — Euclidean interior depth ≤ catch_range + margin") vs Section D1 ("Pure Euclidean depth is retired as the operative pin" — replaced by the path-leg OR occlusion-clear-backstop OR-invariant)
**What happened**: A systems-designer review (H1/H2) proved the Euclidean-only interior-depth pin re-opens capture-immunity (circuitous navmesh route; enclosed blocked-sightline spot). Section D correctly retired it in favor of the D1 OR-invariant — but **Core Rule 5 (written earlier, in Section C) was never updated**, so the GDD simultaneously taught the retired pin and the replacing pin. An author following Core Rule 5 alone would size spots on Euclidean depth and re-open both holes.
**Resolution**: Resolved same session — Core Rule 5's "Interior depth" bullet reworded to reference the D1 reach-resolvability invariant (path leg OR occlusion-clear backstop), with an explicit note that pure Euclidean depth is not sufficient. GDD was In Review (not yet Approved), so no Approved doc or registry value changed.
**Pattern**: When a later section refactor RETIRES an earlier pin, the earlier section (Core Rules / States) is usually left teaching the dead model — run `/consistency-check` (or a same-GDD section-to-section sweep) after any Section C→D refactor, because the registry scan alone only catches cross-doc value conflicts, not same-doc rule-vs-formula contradictions.

### [2026-08-27] — /design-review (player-noise re-review, B2) — 🟡 WRONG-DIRECTION OBLIGATION (countermanded)
**Domain**: `entry_id` vs `fact_id` ownership (Player Noise `NoiseEmitter` · Perception)
**Documents involved**: `design/gdd/player-noise.md` (first-review OQ4 + applied cross-doc obligation) vs `design/gdd/perception.md` R5/R12 (episode id allocated at first-noise-heard) vs registry noise-heard trace schema
**What happened**: The first review's OQ4 directed moving `entry_id` allocation from Perception (per-guard episode id, allocated at first-noise-heard, R12/J3) to the emitter's publish ingress — and the first revision APPLIED it. The re-review's B2 blocker proved this wrong-direction: `entry_id` is Perception's episode identity (intrinsic to who heard and when), while an emitter mounting a shared transport needs only a dedup/presence token. Applying the OQ4 would have forced the emitter to allocate per-guard episode ids it has no knowledge of, and Perception to derive rather than allocate.
**Resolution**: RESOLVED same session — two-ids-two-owners split: emitter-allocated **`fact_id`** (one per published fact, identical across all hearing guards, transport/dedup) + Perception-allocated **`entry_id`** (relayed across the rail, never derived). Amended: player-noise.md (F1 row, CR1/CR2/CR4, OQ4 → "COUNTERMANDED", Dependencies), perception.md R5/R12, registry noise-heard schema (`{kind, source, guard, fact_id, entry_id, consumption, timestamp, publisher}`), review log countermand note.
**Pattern**: A review-directed cross-doc obligation can ship the **wrong direction** — a later re-review proved it so, and the dead obligation had to be countermanded across the GDD, perception.md, and the registry simultaneously. Record countermands prominently (review log + reflexion log + live GDD OQ text) or a future author re-applies the dead direction; a stale obligation is as dangerous as a stale pin.

### [2026-09-02] — /consistency-check — 🔴 CONFLICT
**Domain**: HideSpot reachability and catch-gate datum (Guard AI FSM C1.4 · Player Movement & Hide D1)
**Documents involved**: `design/gdd/player-movement-hide.md` / `design/registry/entities.yaml` vs `design/gdd/guard-ai-fsm.md` C1.4
**What happened**: The canonical D1 contract uses the sampled `proxy(interior_position)` from `guard_hold`; the FSM backstop still used raw `spot_position`, allowing certification and runtime to evaluate different endpoint and origin datums.
**Resolution**: Highest-priority conflict fixed same session — C1.4 now samples and evaluates `proxy(interior_position)` from `guard_hold`. `catch_range` remains the engagement threshold and `catch_range + margin` remains hysteresis only.
**Pattern**: Certification and runtime must consume the same sampled HideSpot datum and guard hold origin; a raw-zone-center fallback silently reopens endpoint divergence.

### [2026-09-02] — /consistency-check — 🔴 CONFLICT
**Domain**: Perception threshold tuning domain (Perception F5 · registry)
**Documents involved**: `design/registry/entities.yaml` vs `design/gdd/perception.md` F5
**What happened**: The F5 table presents starter-slice `T_entry` and `T_floor` values without clearly labeling their scope, while the registry defines the full legal `T_entry` domain `[0.04, 0.40]` and derived `T_floor` domain `[0.04, 0.08]`.
**Resolution**: Resolved same session — F5 now presents the full legal ranges and separately labels the starter slice and starter examples.
**Pattern**: A starter calibration table must identify its scope whenever the registry exposes a wider legal tuning domain.

### [2026-09-02] — /consistency-check — 🔴 CONFLICT
**Domain**: HideSpot event identity and performance audit (Event Bus · Player Movement & Hide · sound performance audit)
**Documents involved**: `design/registry/entities.yaml` / `design/gdd/player-movement-hide.md` vs `design/gdd/sound_performance_audit.md`
**What happened**: The audit's occupancy deduplication row omits the canonical immutable `transition_id` from `(session_id, attempt_epoch, hide_spot_id, transition_id)`.
**Resolution**: Resolved same session — the audit row now states the complete immutable occupancy identity `(session_id, attempt_epoch, hide_spot_id, transition_id)`.
**Pattern**: Exactly-once event audit rows must reproduce the full canonical identity tuple rather than rely on vague transport/source wording.
