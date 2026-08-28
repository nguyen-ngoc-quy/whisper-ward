# Consistency Check Report
Date: 2026-08-27
Registry entries checked: 5 formulas, 3 noise_radii (items), 68 constants, 14 trace event types (76 constant-like entries total)
GDDs scanned: 5 (guard-ai-fsm.md, perception.md, player-third-person-controller.md, player-movement-hide.md, player-noise.md)
Scope: full

---

### Conflicts Found (must resolve before architecture)

*None.*

All cross-system numeric pins consumed by Player Noise match the registry LOCK:

- `R_walk 4.0` (safe [3.5, 5.0]) — player-noise Core Rule 2 / F3 vs perception F12/F13 / registry noise_radii — PASS
- `R_run 6.0` (safe [4.5, 7.5]) — same — PASS
- `R_burst 10.5` (safe [9, 14]) — same — PASS
- `stride_length_walk 1.9` (safe [1.5, 2.4]) — player-noise F1 ledger threshold vs controller F5 / registry — PASS
- `stride_length_run 2.6` (safe [2.0, 3.2], `> stride_length_walk`) — same — PASS
- `f12_noise_origin_offset 0.25` (locked) — player-noise CR5/F3 vs perception F12 — PASS
- `navmesh_sample_maxdistance 0.4` (locked) — consumed but not re-declared — PASS
- `R_noise_share 0.15` (safe [0.10, 0.25], effective `min(R_noise_share, c_noise)`) — player-noise CR7 / perception F6 — PASS
- `c_noise 0.20` (safe [0.15, 0.30]) — same — PASS
- `R_max 1.00` — clamp `R + add <= R_max` — PASS
- `T_sample 0.5` (safe [0.2, 0.5], `T_sample_max 0.5`) — budget tick consumed, not re-tuned — PASS
- `r_investigate_error 1.5` (safe [1.0, 2.5]) — noise commit target error — PASS
- Speed ladder `V_crouch 1.8 < V_patrol 2.3`, `V_walk 3.6`, `V_run 6.25`, `V_chase 7.50` pinned absolute — all PASS (product-form historical language excluded from numeric check)

Invariant checks:
- `R_walk < R_run < R_burst <= R_vis (12.0)` — 4.0 < 6.0 < 10.5 <= 12.0 — PASS
- `stride_length_run > stride_length_walk` — 2.6 > 1.9 — PASS
- `f12 0.25 > stair riser 0.20` margin 0.05 — PASS

---

### Stale Registry Entries (registry behind the GDD)

*None.*

Registry is current as of 2026-08-26 rev-notes (V_chase pinned absolute, chase_speed_ratio historical). No source GDD has drifted beyond the registry since the last check (2026-08-27).

---

### Unverifiable References (no conflict, informational)

- `guard-ai-fsm.md` mentions `navmesh_sample_maxdistance` in a `SamplePosition` call but states no numeric — informational (registry holds 0.4).
- `perception.md` mentions `stride_length_*` only as a consumed player-noise/controller knob, not re-declared — no conflict.
- `player-noise.md` references `R_vis 12.0`, `T_base 0.30`, `catch_range 5.5`, `V_chase 7.50` as consumed invariants, not re-declared — no conflict.

---

### Clean Entries (no issues found)

68 constants + 3 radii + 5 formulas + 14 event types verified across 5 GDDs with no conflicts.
Heuristic false-positives (e.g., `R_walk / R_run / R_burst | 4.0 / 6.0 / 10.5` grouping, safe-range bands) were manually triaged and excluded.

---

Verdict: PASS

No blocking conflicts. Registry and GDDs agree. Safe to proceed to `/design-review` or `/create-architecture`.
