# Whisper Ward — Core Loop Prototype

## Hypothesis Being Tested
Does the one-room thinking-AI loop (early-warning two-timeframe suspicion meter +
throwable noise-maker vs one guard) read as **fair, readable, and satisfying** —
and does the payoff half land: the moment a guard passes you by or resolves a
fruitless investigation, *none the wiser*?

Falsifiable claims (from the game concept's core hypothesis, adapted to this build):
1. After a capture, the player can state at least one true cause of the detection
   (matching the logged trigger shown on the capture screen).
2. After observing the guard's patrol cycle once, the player can complete a run in
   which the guard never visually detects them (GHOST RUN).
3. The player chooses to use the noise-maker at least once.

## How to Run
Double-click `prototype.html` and open it in any modern browser. No server, no
install. Controls: `W A S D` move · `Shift` crouch (silent, slow) · `Click` throw
noise-maker (3 total) · `R` restart.

## What It Implements
- Top-down room, wall geometry blocking both vision (raycast LOS) and movement.
- One guard: FSM **Patrol → Investigate → Chase**, vision cone (FOV + range),
  investigates noise/last-known-position, gives up and returns to patrol.
- **Two-timeframe suspicion meter** (the committed B3 model): fills fast on clear
  LOS to the Investigate threshold (~0.6s), reaches Chase only after sustained LOS
  (~2.5s), residual decays slowly after LOS breaks — not an instant reset.
  Meter gains show their source (VISION / NOISE / RESIDUAL).
- Noise-maker: 3 throws, lands at the click point, spawns a hearing event the
  guard investigates. Soft footstep noise while walking un-crouched.
- Capture screen with the logged cause ("sustained line-of-sight", etc.) + the
  payoff beat: **"GHOST RUN — the guard never knew you were here"** when 0 captures.
- Auto-restart (`R`), minimal HUD (suspicion bar, noise-maker count, timer).

## Current Status
**Concluded** — playtested 2026-08-13. Verdict: **PROCEED**. See `REPORT.md`.

## Findings
- **Hypothesis CONFIRMED.** Core loop is fun and readable; the payoff half
  (guard resolves a fruitless investigation, none the wiser) is the best moment.
- **Suspicion meter** reads well but fails at close range: with a low meter and
  no reaction while inside the cone near the guard, the guard felt "blind/dumb."
  Fix direction (from playtester): charge speed should scale **inversely with
  distance** — validates R8 from the design review.
- **Bug (logged, not fixed):** guard grinds straight into a wall when its
  investigate point is unreachable (no pathfinding, no give-up). For Unity:
  NavMesh pathing + reachable investigate points + ~4s give-up timeout.
- **Tuning:** slightly easy; hearing occlusion (radius + wall-blocking) reads
  correctly; soft walk-noise ~78px / loud noise-maker ~205px.
- **Next:** `/design-review` re-review → `/gate-check` → `/map-systems` →
  `/design-system [perception]` (carry distance-charge falloff + investigate
  timeout into Formulas/Tuning Knobs).
