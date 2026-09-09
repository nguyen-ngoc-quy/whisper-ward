# Consistency Check Report

Date: 2026-09-02

## Scope

Registry entries checked: 0 entities, 0 items, 10 formula definitions, 77 scalar/config constants, plus registered noise, physics, HideSpot, lifecycle, event, and fixture contracts.

GDDs scanned: `game-concept.md`, `perception.md`, `guard-ai-fsm.md`, `player-noise.md`, `player-movement-hide.md`, `player-third-person-controller.md`, and `sound_performance_audit.md`.

## Conflicts Found

### 1. HideSpot D1 datum mismatch — resolved

The registry and `player-movement-hide.md` require reachability against the sampled `proxy(interior_position)` from `guard_hold`. `guard-ai-fsm.md` C1.4 still described the runtime backstop using raw `spot_position`, allowing certification and runtime to evaluate different endpoint and origin datums.

Resolution: C1.4 was updated on 2026-09-02 to sample and evaluate `proxy(interior_position)` from `guard_hold`. `catch_range` remains the engagement threshold and `catch_range + margin` remains retained-accrual/pause hysteresis only.

### 2. Entry-threshold domain is underspecified — resolved

The registry defines the full-knob `T_entry` output domain as `[0.04, 0.40]`, with starter slice `[0.06, 0.30]`. It also defines `T_floor = 0.2 × T_base`, giving a full derived domain of `[0.04, 0.08]`. `perception.md` F5 previously presented starter values without clearly limiting their scope.

Resolution: F5 now presents the full legal ranges and separately labels the starter slice and starter examples.

### 3. Occupancy deduplication identity is incomplete — resolved

The performance audit previously described occupancy identity with transport/source wording plus `(session_id, attempt_epoch, hide_spot_id)` but omitted the canonical immutable `transition_id`. The required identity key is `(session_id, attempt_epoch, hide_spot_id, transition_id)`.

Resolution: the audit row now states the complete immutable occupancy identity `(session_id, attempt_epoch, hide_spot_id, transition_id)`.

## Stale Registry Entries

None confirmed.

## Unverifiable References

- `sound_performance_audit.md` lists audit-only HideSpot workload controls without registry ownership. The document marks them as pending ownership and `UNCAPTURED`/`PENDING_OQ6`; they are not treated as canonical conflicts.
- `sound_performance_audit.md` references `limiter_reported` and `limiter_unsupported`, which are absent from the canonical Player Noise GDD and registry. These states remain unverifiable and are not treated as captured evidence.
- No lifecycle, hunch/StaleLKP, typed occupancy, entry-ID ownership, or `UNCAPTURED`/`PENDING_OQ6` conflict was found.

## Verdict

**PASS** — all three detected conflicts were corrected during this pass. Unverifiable audit-only controls and undefined limiter states remain explicitly non-canonical and are not treated as passing evidence.
