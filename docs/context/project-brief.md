# Whisper Ward — Project Context Brief

> Derived navigation document. Canonical rules remain in the linked GDDs and registry.
> Snapshot: 2026-08-28.

## Current state

- Stage: **Systems Design** (`production/stage.txt`).
- Review mode: **lean** (`production/review-mode.txt`).
- Engine target: Unity 6 LTS `6000.3.17f1`; language C#.
- Core loop: observe → route → act → slip past.
- Core AI: Patrol / Investigate / Chase FSM, perception, suspicion/residual model, and attributable trace events.
- MVP focus: one playable room, one guard, and the smallest complete stealth loop.

## Design pipeline

- Approved: Game Concept, Perception, Guard AI FSM, Player Third-Person Controller.
- In Review: Player Noise and Player Movement & Hide.
- Next active gate: re-review `design/gdd/player-noise.md` after the 2026-08-28 behavioral revision; then review Player Movement & Hide.
- Current canonical system status is in `design/gdd/systems-index.md`.

## Context entry points

1. Read this brief and `docs/context/review-index.md`.
2. Read the relevant row and dependency map in `design/gdd/systems-index.md`.
3. Read the target GDD in full for target review or implementation planning.
4. Read only directly linked dependency sections and matching entries in `design/registry/entities.yaml`.
5. Use raw review/session history only for an explicitly scoped historical question.

## Do not treat as default context

`production/session-logs/`, `production/session-state/`, `.claude/agent-memory/`,
and local settings are recovery or machine-state data, not project design input.
