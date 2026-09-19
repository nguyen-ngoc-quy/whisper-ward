# Prototype Playtest Plan — Whisper Ward MVP

**Status**: Draft — awaiting QA-lead approval  
**Date**: 2026-08-29  
**Scope**: Pre-milestone-0 validation of the one-room stealth loop  
**Engine target**: Unity 6 LTS (6000.3.17f1)  
**Primary test build**: PC/Windows  
**Secondary build**: WebGL demo, with clipboard transcript in place of file flush  
**Minimum total participants**: 10 across two separate cohorts  

## Purpose

This protocol validates whether the MVP loop is fair and learnable to first-time
players. It does not attempt to validate the Vertical Slice S/A/B grade loop or
the Target-tier multi-guard alert system.

The protocol is based on the operational definitions and conditional milestone-0
gate in `design/gdd/game-concept.md`. The HTML prototype's internal N=1 result is
not evidence for this protocol.

## Hypotheses and Pass Rules

### Claim 1 — Specific detection cause

After a capture, a first-time player can state at least one specific true cause of
the detection.

- **Scored population**: the first capture from each participant in Cohort A.
- **Minimum sample**: 5 first captures.
- **Pass**: at least 80% of scored first captures produce a specific true cause.
- **Below minimum**: INCONCLUSIVE.
- **Later captures**: recorded as secondary evidence only; they are not scored
  because the trace reveal contaminates later recall.

### Claim 2 — Observation, plan and execution

After observing one complete guard patrol cycle, a first-time player can state the
correct crossing plan and execute a clean crossing.

Claim 2 has two required sub-bars:

1. **Comprehension** — the participant states the level-specific crossing plan
   before attempting it.
2. **Execution** — the participant completes the first eligible post-observation
   crossing without a committed visual detection.

Rules:

- A visual detection is an actual LOS-suspicion escalation to Investigate or Chase,
  including residual-lowered threshold entries.
- A confirm-window-cancelled peek is not a visual detection.
- A noise/alert-only Investigate without LOS suspicion is not a visual detection.
- The scored execution start must have `R ≤ epsilon_residual`.
- **Protocol value**: `epsilon_residual = 0.02` residual units, provisional until
  QA-lead approval. This is a measurement floor, not a gameplay tuning knob.
- An above-floor start is not scored and does not consume the attempt budget. The
  participant waits for residual decay or the facilitator resets the session.
- Two consecutive above-floor starts cap the participant's execution sub-bar as
  INCONCLUSIVE rather than silently scoring contaminated data.
- A no-detection attempt that fails to complete the objective is an execution miss
  and does not consume the clean-attempt budget. The participant may try again,
  up to `K = 3` eligible clean attempts; failure to complete by then fails the
  execution sub-bar.
- Any eligible attempt with a committed visual detection fails the execution
  sub-bar immediately.
- **Cohort pass**: at least 60% of participants pass both comprehension and
  execution.
- **Minimum sample**: 5 participants.
- **Below minimum**: INCONCLUSIVE.

### Claim 3 — Unprompted noise-maker adoption

Participants use the Burst noise-maker without being instructed or demonstrated,
after the build has presented the tool through a placed pickup.

- **Cohort**: separate from Cohort A to prevent trace-reveal contamination.
- **Denominator**: participants with a `pickup-reached` event.
- **Numerator**: participants whose `pickup-reached` event is followed by a Burst
  use on a later attempt. Using Burst during the pickup-reaching attempt does not
  count.
- **Minimum presented sample**: 5 participants.
- **Pass**: at least 60% of the denominator is in the numerator.
- **Below minimum**: INCONCLUSIVE.
- A participant who never reaches the pickup is excluded from the denominator, not
  counted as a failure.
- The pickup is discoverable through placement only; no HUD hint, spoken prompt,
  tester demonstration or explicit instruction is allowed.

## Required MVP Build Preconditions

The facilitator must not start scored sessions until all preconditions below are
recorded as PASS or explicitly marked as a non-scored pilot limitation.

### Core room and AI

- [ ] One room, one guard and one objective load without a crash.
- [ ] Guard runs the complete Patrol → Investigate → Chase loop.
- [ ] Player supports crouch, walk and run movement.
- [ ] Perception supports vision, hearing and the early-warning suspicion meter.
- [ ] Burst is a placed, limited-resource noise-maker with a reachable reserve.
- [ ] Win, capture and fast restart are available.

### Geometry acceptance

- [ ] **Timing-clean route**: a spawn-to-objective route exists at crouch and walk
      speed with a non-empty clean start-phase window no larger than half of one
      full patrol period, including scan sweeps.
- [ ] The timing-clean route has at least one sampled start phase outside the clean
      window that produces a committed LOS escalation at base residual.
- [ ] The clean route produces no noise-driven Investigate that re-covers the route.
- [ ] **Burst-only route**: sampled full transits without a noise event produce a
      committed LOS escalation by outcome, not merely by an intermediate sightline.
- [ ] The shipped MVP Investigate behavior is trace-verified against the Burst-only
      route. When strengthened residual look-around behavior is implemented, the
      Burst route is re-tested with Burst and must remain crossable without a
      committed LOS escalation.
- [ ] No-capture-immunity editor sweep is complete for the certified-route vicinity:
      walkable player cells, guard positions at the committed margin, path-partial
      cases, off-mesh vertical limits and hide-spot reachability are all covered.
- [ ] Safe observation vantage exists at spawn with zero LOS exposure and therefore
      zero residual accrual during the patrol observation.
- [ ] The route and patrol schedule are stable for the entire test batch.

### Trace and build integrity

- [ ] The dev-only Suspicion Attribution gizmo records the full FSM decision trace.
- [ ] Trace contains every LOS gain/break, source and position of noise events,
      escalation trigger and threshold state, Chase entry, capture trigger and
      `pickup-reached`.
- [ ] Trace is held in an in-memory ring buffer and is not visible during play.
- [ ] Trace flushes at attempt start, attempt end and after the participant states
      the cause.
- [ ] PC builds flush to an approved artifact path without using `Debug.Log` or
      `Player.log`.
- [ ] WebGL builds expose the same trace through a clipboard transcript.
- [ ] Trace includes `session_id`, `attempt_epoch`, attempt id, start/end residual,
      threshold state and event timestamps.
- [ ] Reset increments `attempt_epoch`; stale queued facts cannot appear in the
      subsequent attempt.
- [ ] The facilitator can re-arm the objective after a clean grab so the session can
      continue toward a capture when needed for Claim 1.

## Participant Cohorts

### Cohort A — Claims 1 and 2

Minimum 5 first-time players of Whisper Ward. Participants must not be developers,
designers, QA staff, or people who have seen the attribution trace or the authored
solution. The cohort may share a session because the protocol controls scenario
ordering and delays trace reveal.

### Cohort B — Claim 3

Separate minimum 5 first-time players. No participant from Cohort A may join Cohort
B. Participants must not receive a Burst demonstration or a verbal instruction to
use it. The facilitator may explain only basic controls and the objective of the
session.

## Scenario Ordering

### Cohort A sequence

1. Confirm consent, first-time status and basic controls. Do not explain stealth
   solutions, the Burst purpose or the attribution rubric.
2. Start a fresh session with residual at or below `epsilon_residual`.
3. Allow the participant to observe one complete patrol cycle from the safe vantage.
4. Ask the participant to state the crossing plan before movement begins. Score the
   comprehension rubric below; do not reveal the trace.
5. Begin the first eligible post-observation execution attempt. Record residual and
   threshold state at attempt start.
6. If the participant completes the clean route, re-arm the objective and continue
   to the remaining route scenario so the session can produce a first capture.
7. If a capture occurs, stop input, ask for the participant's cause statement before
   showing any trace, then flush and reveal the relevant trace.
8. Continue only until the participant's first capture is recorded or the structured
   post-clean continuation cap of `N = 3` later attempts is reached.
9. Record later captures and cause statements as secondary evidence only.

### Cohort B sequence

1. Confirm consent, first-time status and basic controls. Do not explain Burst's
   purpose or demonstrate it.
2. Start a fresh session and allow normal exploration.
3. Record the exact `pickup-reached` event that presents the tool.
4. Observe whether the participant chooses Burst without prompting.
5. Do not count Burst use during the pickup-reaching attempt.
6. Count the participant in the Claim 3 numerator only when a later attempt contains
   a Burst use event.
7. Do not reveal attribution trace during this cohort's scored session.

## Claim 1 — Cause Comprehension Rubric

The facilitator asks: **“What specifically caused the guard to catch you?”**

### Acceptable true-cause statements

The statement must identify at least one contributing decision input from the trace:

- guard identity or clearly identified guard position plus the LOS exposure;
- a specific crossing location or direction where the player remained visible;
- lingering past the confirm window;
- a named Burst or movement noise event that caused the guard to investigate, only
  when the participant also identifies the subsequent LOS exposure that caused the
  capture;
- entering a HideSpot while witnessed, when that event is the trace-supported cause.

Examples:

- “Guard A saw me crossing from the east window and the sight lasted past the
  confirm window.”
- “I stayed in the light too long after the guard turned.”
- “The guard heard my Burst, then saw me when I crossed the route.”

### Rejectable statements

- “The guard saw me.”
- “I got unlucky.”
- “The AI is unfair.”
- “I made a mistake.”
- A statement that names only the final capture without a contributing input.

Raters judge specificity and trace correspondence, not vocabulary or exact system
terminology. Any one trace-supported contributing input is sufficient; the player
does not need to identify the first input or the entire causal chain.

## Claim 2 — Comprehension Rubric

Before the first execution attempt, the participant describes the crossing plan in
their own words. Score PASS only when all three required elements are present:

1. **Route** — identifies the intended safe route or crossing side of the room.
2. **Timing** — identifies when in the observed patrol cycle to begin/cross, rather
   than only saying “wait until safe.”
3. **Movement/noise plan** — identifies the intended crouch/walk movement and whether
   Burst is or is not needed for this route.

The facilitator records the participant's exact wording before any trace reveal. A
generic statement without a route, timing cue and movement/noise choice is a FAIL.

## Attempt Definitions

### Attempt start

An attempt starts when the facilitator records a fresh attempt id, resets the input
capture marker, records `session_id`, `attempt_epoch`, residual and threshold state,
and releases the participant to act from the authored start position.

### Attempt end

An attempt ends on objective completion, committed visual detection, capture, timeout,
technical abort, or facilitator stop. Trace flush occurs immediately at the boundary.

### Technical abort

A crash, missing trace, input loss, build fault, frame stall outside the registered
test budget, or facilitator intervention for a non-design reason invalidates the
attempt. The attempt is repeated only after a fresh reset; it does not count toward
Claim 2's clean-attempt budget.

### Timeout

The facilitator ends an attempt when the authored route or objective timeout is
reached. The timeout value is supplied by the build/level contract before scoring and
is recorded with the attempt.

## Data Collection

### Per-participant record

- anonymized participant id;
- cohort;
- first-time status;
- build version and platform;
- session id;
- attempt ids and start/end timestamps;
- attempt start residual and threshold state;
- comprehension rubric result and verbatim response;
- objective completion result;
- visual detection result and operational cause;
- capture result and verbatim cause response;
- trace reveal timestamp;
- `pickup-reached` event and later Burst-use event, if applicable;
- technical aborts and facilitator interventions.

### Per-attempt trace record

- `session_id` and `attempt_epoch`;
- attempt id;
- attempt start/end;
- residual and threshold state at start/end;
- every LOS gain and break with source, guard and distance;
- every noise event with kind, source, position and consumption;
- Investigate/Chase escalation with trigger and threshold state;
- Chase entry and capture trigger;
- `pickup-reached` and Burst-use events;
- reset, pause, timeout and technical-abort markers.

## Scoring Sheet

### Claim 1

```text
first captures scored:                  ____ / 5 minimum
specific true causes:                   ____
true-cause rate:                        ____ %
verdict:                                PASS / FAIL / INCONCLUSIVE
```

### Claim 2

```text
participants scored:                   ____ / 5 minimum
comprehension passes:                   ____
execution passes:                       ____
participants passing both sub-bars:    ____
both-bar rate:                          ____ %
verdict:                                PASS / FAIL / INCONCLUSIVE
```

### Claim 3

```text
pickup-presented participants:         ____ / 5 minimum
later Burst users:                      ____
adoption rate:                          ____ %
verdict:                                PASS / FAIL / INCONCLUSIVE
```

## Manual QA Checklist

### Before every session

- [ ] Correct cohort and participant id recorded.
- [ ] No participant has seen the trace or authored solution.
- [ ] Correct build version and platform recorded.
- [ ] Trace visibility is disabled during play.
- [ ] Session starts with a fresh `attempt_epoch`.
- [ ] Safe observation vantage is available.

### After every attempt

- [ ] Attempt boundary and reason recorded.
- [ ] Trace flush completed.
- [ ] Residual and threshold state recorded.
- [ ] Technical abort distinguished from design failure.
- [ ] No stale prior-epoch fact appears in the trace.

### After every capture

- [ ] Participant states cause before trace reveal.
- [ ] Verbatim response recorded.
- [ ] Trace reveals only after response is captured.
- [ ] First capture is marked for Claim 1 scoring.
- [ ] Later capture is marked secondary evidence.

### After every pickup presentation

- [ ] `pickup-reached` event exists.
- [ ] Pickup was discovered without hint or demonstration.
- [ ] Burst use on the same attempt is not counted for Claim 3.
- [ ] Burst use on a later attempt is linked to the original presentation event.

## Smoke Scope Before Playtest

The developer must run a smoke check before inviting participants. The check must
cover:

1. Build launch and new-session start.
2. Player crouch, walk and run controls.
3. Patrol, vision escalation, hearing investigation and capture.
4. Clean timing route and negative start-phase route.
5. Burst pickup, accepted throw, landing publication and fast restart.
6. Objective completion and objective re-arm.
7. Trace flush at attempt boundaries and after cause response.
8. Pause/reset/epoch behavior.
9. PC trace artifact or WebGL clipboard transcript.
10. No trace leakage through live UI or `Debug.Log`/`Player.log`.

## Verdict Rules and Sign-Off

The playtest report must state one verdict per claim:

- **PASS** — minimum sample reached and pass threshold met.
- **FAIL** — minimum sample reached and pass threshold not met.
- **INCONCLUSIVE** — minimum sample not reached or protocol integrity was not
  maintained.

The overall MVP hypothesis is **SUPPORTED** only when Claims 1, 2 and 3 each have a
PASS verdict. Any INCONCLUSIVE claim leaves the overall result INCONCLUSIVE. A FAIL
requires design review and a documented revision decision before re-testing.

Required sign-off before milestone-0:

- [ ] Facilitator confirms protocol execution and raw records.
- [ ] Designer confirms scenarios and rubrics match the current concept/GDDs.
- [ ] QA lead approves the plan before scored sessions begin.
- [ ] QA lead reviews the final playtest report and verdicts.

## Known Current Limitations

At the time this plan was authored, the repository did not contain a Unity project
scaffold (`Packages/manifest.json`, `ProjectSettings/`, `Assets/` or test assembly).
The plan is therefore a precondition and measurement contract; it is not evidence
that the build or any acceptance criterion currently passes.

## Related Documents

- `design/gdd/game-concept.md`
- `design/gdd/player-noise.md`
- `design/gdd/perception.md`
- `design/gdd/guard-ai-fsm.md`
- `design/gdd/player-movement-hide.md`
- `docs/architecture/adr-0001-event-messaging-bus.md`
- `docs/architecture/adr-0002-physics-collision-contract.md`
- `docs/qa/traceability-gaps-2026-08-29.md`
- `docs/qa/input-camera-recovery-trace-2026-08-29.md`
