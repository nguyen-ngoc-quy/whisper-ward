# Suspicion Meter / Grade — Design Specification

> **Status:** Design approved by user, 2026-09-01
> **System:** Suspicion Meter / Grade (#7)
> **Scope:** MVP
> **Approach:** Trace-weighted operator with per-guard meter and room grade
> **Engine:** Unity 6 LTS `6000.3.17f1`

## 1. Overview

Suspicion Meter / Grade is a deterministic presentation and measurement system for
Whisper Ward's stealth loop. It exposes each relevant guard's current suspicion as
an early-warning meter and produces a final S/A/B room grade from authoritative
Perception/FSM trace records. The system consumes existing sensing facts, FSM
decisions, residual snapshots, and session boundaries; it does not recalculate
Perception, alter FSM thresholds, or send commands to gameplay systems.

The MVP uses a continuous 0–100 per-guard meter with Quiet, Investigate, and Chase
regions. Residual wariness is shown as a separate afterglow/background signal. The
room grade is trace-weighted: Chase entries, fruitless investigations, and final
residual contribute to quality penalty. Clean noise use, near misses, noise relay
receipt, and clean line-of-sight breaks do not create direct grade penalties.

## 2. Player Fantasy

*You are a phantom who reads the machine — never surprised, never guessing.*

The meter must make every visible change answerable. A player should understand
whether a guard is calm, entering an investigation, or approaching Chase without
needing to infer hidden state from an unexplained bar. The room grade then turns
that readable play into a mastery loop: a clean run earns S, while consequences
remain attributable to concrete incidents rather than arbitrary time pressure.

The system supports the project's Visible Intelligence and Fair Mind-Challenge
pillars. It must not punish the player merely for creating a clean diversion. A
fruitless investigation can leave residual wariness in the world, but it is scored
through the authoritative trace and residual snapshot rather than by treating the
noise emission itself as a failure.

## 3. Detailed Rules

### 3.1 Per-guard meter

Each guard has a read model keyed by:

```text
(session_id, attempt_epoch, guard_eid)
```

The read model contains:

- `A_current`: the authoritative current suspicion accumulator supplied by
  Perception's existing contract;
- `T_chase`: the registry-owned Chase threshold;
- `meter_ratio = clamp(A_current / T_chase, 0, 1)`;
- `meter_percent = round(meter_ratio × 100)`;
- `guard_state`: Patrol, Investigate, or Chase from the FSM state/decision feed;
- `R_current`: residual wariness, displayed separately as afterglow/background;
- `last_causal_event`: the most recent event that explains a read-model change;
- `source_entry_id`: the episode identity where one exists.

The meter does not own or mutate `A_current`, `R_current`, FSM state, Perception
thresholds, or hearing/vision outcomes. It is a projection of authoritative
state. `T_chase` is consumed from the registry and is never independently tuned by
this system.

The UI shows three semantic regions:

| Region | Condition | Meaning |
|---|---:|---|
| Quiet | below the Investigate entry region | No active escalation is represented by the meter |
| Investigate | at or above the applicable entry region and below Chase | The guard is investigating or suspicion is accumulating |
| Chase | `A_current >= T_chase` / FSM Chase state | The guard has escalated to Chase |

The exact entry boundary is read from the existing Perception formula
`T_entry(R) = max(T_base - k_res × R, T_floor)` and its registry-owned aliases;
Suspicion/Grade does not redefine that formula.

### 3.2 Residual presentation

`R_current` is a separate visual channel. It may be represented as a dim fill,
afterglow, or hardware-like background signal behind the live accumulator. It is
never added directly to `A_current`, never moves the meter independently of the
Perception contract, and never creates a second incident penalty merely because it
is visible.

### 3.3 Room-grade eligibility

A room grade is finalized only when the session owner supplies a valid completed
room boundary. A run that ends in Capture or an incomplete room reset has:

```text
completion_status = Failed
```

and does not receive S/A/B. A room with incomplete mandatory trace or snapshot
data has:

```text
completion_status = Unresolved
```

rather than silently treating missing evidence as a clean score.

### 3.4 Incident accounting

The reducer consumes immutable FSM decision records. The following records are
eligible for incident accounting:

- `Chase-entry`;
- `investigate-resolution(cause=fruitless)`;
- `Capture`.

An event is counted at most once per `(session_id, attempt_epoch, entry_id,
event_type)`. Duplicate delivery and transport retry therefore cannot increase a
penalty. Events from an older `attempt_epoch` are stale and are ignored.

The following are explicitly not direct incident penalties:

- movement or Burst emission;
- noise relay receipt;
- `investigate-commit(cause=noise)`;
- near miss;
- LOS gain or clean LOS break;
- suppression micro-tell;
- residual decay;
- a normal investigation resolution whose cause is not `fruitless`.

### 3.5 Deterministic finalization

`RoomGradeOperator` finalizes one immutable output per valid completed room. It
records the event IDs and component values that contributed to the result. The
same trace, regardless of event delivery order or retry pattern, must produce the
same score, grade, and breakdown.

## 4. Formulas

### 4.1 Per-guard meter

```text
meter_ratio  = clamp(A_current / T_chase, 0, 1)
meter_percent = round(meter_ratio × 100)
```

`A_current` and `T_chase` are authoritative inputs. The meter does not integrate
or decay them itself.

### 4.2 Incident penalty

```text
P_incident = min(
    grade_penalty_cap,
    grade_weight_chase × N_chase_entry
      + grade_weight_fruitless × N_fruitless_resolution
      + grade_weight_capture × N_capture
)
```

`N_*` counts distinct, valid, deduplicated events for the current room and epoch.

Starter values:

```text
grade_weight_chase      = 25
grade_weight_fruitless  = 10
grade_weight_capture    = 50
grade_penalty_cap       = 90
```

Capture has a diagnostic weight for the breakdown, but a Capture still produces
`completion_status = Failed` and does not receive S/A/B.

### 4.3 Residual penalty

```text
P_residual = grade_weight_residual × clamp(R_final / R_max, 0, 1)
```

Starter value:

```text
grade_weight_residual = 10
```

`R_final` is the authoritative residual snapshot at room finalization. This is not
an additional count of fruitless investigations.

### 4.4 Quality score

```text
Q = clamp(100 - P_incident - P_residual, 0, 100)
```

### 4.5 Grade mapping

| Result | Condition |
|---|---:|
| S | `Q >= grade_threshold_s` |
| A | `grade_threshold_a <= Q < grade_threshold_s` |
| B | `grade_threshold_b <= Q < grade_threshold_a` |
| Needs Improvement | `Q < grade_threshold_b` |
| Failed | capture or incomplete failed room completion |
| Unresolved | mandatory trace/snapshot data missing |

Starter thresholds:

```text
grade_threshold_s = 90
grade_threshold_a = 75
grade_threshold_b = 60
```

### 4.6 Multiple guards

For a room with multiple valid guard aggregates:

```text
Q_room = weighted_mean(Q_guard_i, exposure_weight_i)
```

In the one-guard MVP, `Q_room = Q_guard`. Future `exposure_weight_i` values must
come from valid episode/trace exposure, not render-frame time or raw guard count.
The multi-guard rule is included for forward compatibility and is not a MVP tuning
surface.

## 5. Architecture and Data Flow

```text
Perception snapshot ──────────────┐
                                  ├─> SuspicionMeterReadModel ─> HUD meter
FSM decision trace ─> TraceReducer┤
                                  └─> RoomGradeOperator ─> final S/A/B
Session boundary ─────────────────┘
```

### 5.1 `SuspicionMeterReadModel`

A read-only projection keyed by session, epoch, and guard. It exposes current
accumulator ratio, semantic state region, residual presentation value, and the
latest explanation event. It never sends gameplay commands.

### 5.2 `GradeTraceReducer`

A deterministic reducer over immutable trace records. It validates identity,
filters stale epochs, deduplicates event keys, and maintains per-guard incident
aggregates. It does not recalculate accumulator charge, hearing, vision, or FSM
transitions.

### 5.3 `RoomGradeOperator`

The finalizer validates completion state, obtains the authoritative final residual
snapshot, computes penalties and score, maps the score to a grade, and emits one
immutable `RoomGradeFinalized` record.

The output contains:

```text
session_id
attempt_epoch
grade_version
completion_status
per_guard_breakdown[]
incident_penalty
residual_penalty
quality_score
grade
contributing_event_ids[]
```

### 5.4 `SuspicionMeterPresenter`

The presentation adapter renders the read model and explanation state. It may
choose the final visual language, but it cannot infer hidden causes, rewrite score,
or change gameplay thresholds. A missing read-model input is displayed as
`unavailable`, not as zero.

## 6. Error Handling and Lifecycle

- Missing `A_current` or `T_chase` makes the meter `unavailable`; it does not show
  0%.
- Missing mandatory trace or final snapshot makes the grade `Unresolved`; it does
  not imply no incidents.
- Stale epoch records are ignored and produce a stable diagnostic.
- Duplicate records are ignored after identity validation.
- Capture before successful completion produces `Failed` and no S/A/B.
- A restart closes the old aggregate and begins a new aggregate under the new
  `attempt_epoch`.
- An invalid record does not crash gameplay or erase valid prior aggregate state.
- If a required event is still awaiting transport at the final boundary, the grade
  remains pending/unresolved according to Session State policy; it is never finalized
  using a fabricated zero.
- Finalization is idempotent: a repeated final-boundary callback cannot create a
  second grade for the same session and epoch.

## 7. Dependencies

| Dependency | Contract consumed |
|---|---|
| Perception | `A_current`, residual snapshot, sensing facts, threshold contract |
| Guard AI FSM | Patrol/Investigate/Chase state and immutable decision records |
| Event Bus | typed delivery, retry behavior, session and epoch identity |
| Session/Save State | room completion, restart, and finalization boundary |
| HUD/UI | read-only meter and explanation presentation |
| Telemetry | trace export, score breakdown, contributing event IDs |
| Entity Registry | thresholds, weights, grade schema version, tuning authority |

## 8. Tuning Knobs

The following values are owned by Suspicion/Grade and must be registry/config
values, not constants embedded in UI, FSM, or Perception code:

| Registry name | Starter value | Meaning |
|---|---:|---|
| `grade_weight_chase` | `25` | Penalty per distinct `Chase-entry` |
| `grade_weight_fruitless` | `10` | Penalty per distinct fruitless resolution |
| `grade_weight_capture` | `50` | Diagnostic Capture weight; Capture remains Failed |
| `grade_weight_residual` | `10` | Weight of normalized final residual |
| `grade_penalty_cap` | `90` | Maximum incident penalty |
| `grade_threshold_s` | `90` | Minimum S score |
| `grade_threshold_a` | `75` | Minimum A score |
| `grade_threshold_b` | `60` | Minimum B score |
| `meter_display_scale` | `100` | Display scale for normalized accumulator |
| `grade_operator_schema_version` | `1` | Output contract version |

Existing registry values `T_base`, `T_floor`, `T_chase`, `forgiveness_floor`,
`R_max`, and `residual_decay` remain authoritative dependencies and are referenced,
not redefined here.

## 9. Edge Cases

1. **Zero accumulator:** meter is exactly 0%.
2. **Accumulator at Chase threshold:** meter is exactly 100% and the Chase region
   is active.
3. **Accumulator above threshold due to a numerical overshoot:** clamp to 100%; do
   not alter the authoritative accumulator.
4. **Residual at zero:** no residual penalty and no afterglow.
5. **Residual above `R_max`:** normalized residual is clamped to 1.
6. **Repeated `Chase-entry` for one episode:** count once by `entry_id`.
7. **Fruitless resolution after a noise investigation:** count the fruitless
   resolution, not the original noise commit.
8. **Clean noise diversion:** no incident penalty if it does not yield a fruitless
   resolution or escalation.
9. **Capture:** output Failed regardless of numeric `Q`.
10. **Room restart:** old epoch cannot contribute to the new room grade.
11. **Out-of-order trace delivery:** reducer output remains identical.
12. **Duplicate transport retry:** no duplicate penalty.
13. **Missing accumulator snapshot:** display unavailable; do not infer 0%.
14. **Missing final residual:** output Unresolved; do not infer `R_final = 0`.
15. **No valid incident events:** incident penalty is zero, subject to valid final
    snapshot and completion state.

## 10. Acceptance Criteria

- **SG-AC1:** With `A_current = 0`, the meter is 0%; with `A_current = T_chase`,
  the meter is 100%; output never leaves `[0,100]`.
- **SG-AC2:** The meter consumes `T_chase` from the registry and never changes the
  FSM threshold.
- **SG-AC3:** A `Chase-entry` is counted at most once per `entry_id`.
- **SG-AC4:** `investigate-resolution(cause=fruitless)` is counted exactly once;
  other resolution causes are not misclassified.
- **SG-AC5:** Noise emission, noise relay, near miss, and clean LOS break do not
  create direct incident penalty.
- **SG-AC6:** Capture or incomplete failed reset returns `Failed`, not S/A/B.
- **SG-AC7:** Residual penalty uses `clamp(R_final / R_max, 0, 1)` and does not
  recount the fruitless event.
- **SG-AC8:** Identical trace content with different delivery order produces the
  same score, grade, and breakdown.
- **SG-AC9:** A stale epoch event cannot alter the current epoch aggregate.
- **SG-AC10:** Duplicate delivery cannot increase penalty.
- **SG-AC11:** Missing snapshot or mandatory trace produces `unavailable` or
  `Unresolved`, never a fabricated zero.
- **SG-AC12:** Final output contains score, grade, version, completion status, and
  contributing event IDs.
- **SG-AC13:** Weights and thresholds are read from registry/config and are not
  hardcoded in HUD, FSM, or Perception.
- **SG-AC14:** Unit tests cover meter boundaries, S/A/B thresholds, idempotency,
  stale epochs, failed completion, missing data, and order invariance.

## 11. Test Evidence Plan

Required logic evidence is a deterministic Unity Test Framework suite under:

```text
tests/unit/suspicion-grade/
```

Minimum test scenarios:

1. meter 0%, entry boundary, and 100% boundary;
2. exact S/A/B threshold mapping;
3. Chase, fruitless, and clean-noise accounting;
4. Capture/Failed precedence;
5. residual normalization and clamping;
6. duplicate and retry idempotency;
7. stale epoch rejection;
8. delivery-order invariance;
9. missing-data `unavailable`/`Unresolved` behavior;
10. one-guard and forward-compatible multi-guard aggregation.

No runtime, Unity, or target-hardware evidence is claimed by this design document.
Those results require the Unity scaffold, test runner, and later integration capture.

## 12. Out of Scope

- Grade based on completion time.
- Leaderboards or cross-room comparison.
- Cross-session/profile progression.
- Multiplayer aggregation.
- Difficulty modifiers that change incident weights.
- Gameplay commands from the meter or grade operator.
- Reimplementation of Perception or FSM formulas.
- Complex VFX/audio polish beyond the read-model presentation contract.
