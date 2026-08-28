# Concept Prototype Report: Whisper Ward — Core Loop

> **Date**: 2026-08-13
> **Prototype Path**: HTML
> **Concept File**: design/gdd/game-concept.md

---

## Hypothesis

*If the player moves through a one-guard room reading an early-warning suspicion
meter and uses a throwable noise-maker to reshape the guard's flow, the
thinking-AI loop reads as fair, readable, and satisfying — evidenced by the
player naming a true cause after capture, completing a clean run after one
patrol observation, and choosing to use the noise-maker.*

---

## Riskiest Assumption Tested

**Payoff half** — "the guard resolves a fruitless investigation / walks past you,
none the wiser." Proven out: the guard's investigation resolution was called out
as the best moment, and the GHOST RUN beat landed.

---

## Approach

**Path chosen:** HTML (top-down 2D browser build, single self-contained file)
**Reason for path:** the hypothesis is about loop readability and the payoff
moment, not moment-to-moment feel; the HTML file distributes to naive testers in
seconds with no install.

**Shortcuts taken (intentional):**
- Hardcoded tuning values (charge rates, radii, thresholds)
- Placeholder shapes for all entities (circles, rects, cones)
- Straight-line AI movement — no pathfinding (knowingly deferred; NavMesh in Unity)
- No menus, no music, no tutorial text

---

## Result

- Core loop **confirmed fun and readable**; verdict **PROCEED**.
- **Best moment:** the guard's investigation + throwing the noise-maker.
- **A little bit easy** — a tuning issue, not a structural one.
- **Suspicion meter reads well but fails at close range:** standing in the cone
  near the guard with a low meter and getting no reaction read as the guard
  feeling *blind/dumb* — a Pillar 1 / Pillar 4 legibility hit.
- **Bug found (logged, not fixed):** the guard gets stuck grinding straight into a
  wall when its investigate point isn't reachable in a straight line (no
  pathfinding, no investigate give-up). The walk-noise non-reaction was partly
  the same stuck bug, plus intended hearing occlusion (walls block sound).
- **Explicit fix direction from the playtester:** suspicion fill speed should
  scale inversely with distance to the guard.

---

## Metrics

| Metric | Value |
|--------|-------|
| Path used | HTML |
| Iterations to playable | 1 (single-shot) |
| Prototype duration | ~1 session |
| Playtesters | 1 internal |
| Feel assessment | Loop fun and legible; not urgent enough at close range; slightly easy |
| Hypothesis verdict | **CONFIRMED** |

---

## Recommendation: PROCEED

The loop is fun, the payoff moment lands, and both defects found are
tuning/fidelity issues with known fixes — not structural problems with the
concept. Proceed to re-review the concept doc and move toward design.

---

## If Proceeding

- **Core tuning values discovered:**
  - Suspicion charge must scale **inversely with distance** (validates **R8**,
    previously deferred from the design review). Closer = fills faster, so risk
    is readable at the exact moment it matters.
  - Investigate needs a **give-up timeout** (~4s) so unreachable points resolve
    back to patrol instead of wall-grinding.
  - Hearing **occlusion (radius + wall-blocking)** confirmed as intended and
    readable once the stuck bug is gone.
  - Noise radii read correctly: soft walk-noise ~78px, loud noise-maker ~205px.
- **Assumptions confirmed:** early-warning suspicion meter works; noise-maker is
  an appealing tool; guard investigation is the strongest moment; GHOST RUN
  payoff works.
- **Assumptions disproved:** flat-rate suspicion charge is unreadable at close
  range; straight-line AI movement without pathfinding breaks the experience
  even with a single guard.
- **Emergent mechanics worth formalizing:** wall-slide + give-up as the minimum
  AI navigation behavior; proximity-scaled suspicion charge.

> Note: HTML path — feel was not the primary hypothesis, but the distance-based
> charge interaction deserves validation in the engine prototype (milestone 0 of
> the AI GDD) before final tuning is locked.

**Next steps:**
1. `/design-review design/gdd/game-concept.md` (re-review B1–B4)
2. `/gate-check`
3. `/map-systems`
4. `/design-system [perception]` — carry learnings into Formulas and Tuning
   Knobs: distance-based charge falloff, investigate timeout, noise radii

---

## Lessons Learned

- **What assumptions were broken by actually building this?**
  A one-guard loop still needs reachable investigate points or the AI reads
  broken. Straight-line movement was fine for patrol but not for investigation.

- **What surprised us that didn't show up in the brainstorm?**
  R8 (distance-based LOS charge falloff) was speculative in review and was
  *validated by play* in the prototype — the player independently suggested the
  inverse-distance charge before seeing any design docs.

- **What would we test differently next time?**
  Test with an external (naive) player to separate first-impression signal from
  the developer's own tuning familiarity; test the "a little easy" read with a
  tighter suspicion budget to find the difficulty knob.

---

> *Prototype code location: `prototypes/whisper-ward-concept/`*
> *This code is throwaway. Never refactor into production.*
