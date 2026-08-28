# Whisper Ward — Current Review Index

> Navigation summary only. Full findings remain in the linked review logs.
> Snapshot: 2026-08-28.

| System | Current status | Latest recorded verdict | Next action | Canonical source |
|---|---|---|---|---|
| Game Concept | Approved conditional | Approved, 2026-08-17 | Preserve scope and pillars | `design/gdd/game-concept.md` |
| Perception | Approved | Approved, 2026-08-18 | Proceed under registry lock-step | `design/gdd/perception.md` |
| Guard AI FSM | Approved final | Approved, 2026-08-24 | Carry implementation obligations into architecture | `design/gdd/guard-ai-fsm.md` |
| Player Third-Person Controller | Approved terminal | Approved, 2026-08-26 | Exception-based review only after mutation | `design/gdd/player-third-person-controller.md` |
| Player Noise | In Review | Major Revision Needed, 2026-08-27 | Re-review the 2026-08-28 behavioral revision | `design/gdd/player-noise.md` |
| Player Movement & Hide | In Review | Design complete; review pending | Run target design review | `design/gdd/player-movement-hide.md` |

## Current review facts

- Player Noise's behavioral revision is recorded in `production/session-state/active.md`; it is a recovery checkpoint, not a canonical review verdict.
- The full chronological logs are under `design/gdd/reviews/` and are read only when the latest summary cannot answer the question.
- Cross-document consistency reports are supporting evidence, not replacements for the canonical GDD or registry.

## Reading rule

For a target review, load this index, the target GDD, direct dependencies, and
matching registry entries. Do not load every historical review entry by default.
