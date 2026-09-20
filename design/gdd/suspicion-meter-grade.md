# Suspicion Meter / Grade Operator

> **Status**: Approved (2026-09-20)
> **Author**: Systems Designer (Agent) & Creative Director (co-sign)
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Pillar 2 (Fair Mind-Challenge), Pillar 3 (You Create the Situation), Pillar 4 (Visible Intelligence & Mastery)

## Overview

The **Suspicion Meter & Grade Operator** is the dual feedback and evaluation backbone of *Whisper Ward*, bridging the AI's internal cognition and the player's mastery loop. The system operates as two strictly decoupled components consuming authoritative data from the Perception System (GDD #2) and Guard AI FSM (GDD #1):

1. **Suspicion Meter (Early-Warning Telemetry)**: Translates the guard's continuous-LOS accumulator $A \in [0, 1]$ and residual wariness $R \in [0, R_{\max}]$ into immediate, player-readable visual and auditory tension. Charge rate scales inversely with distance up to a hard cap ($dA/dt = \min(k/d, rate_{\max})$). As residual wariness builds from previous close sightings or distractions, the dynamic Investigate threshold marker $T_{\text{entry}}(R) = \max(T_{\text{base}} - k_{\text{res}} \cdot R, T_{\text{floor}})$ visibly sinks on the meter. This provides fair, readable early warnings that allow players to retreat within the confirm-window grace period before Investigate commits, and freezes deterministically when Chase engages.
2. **Grade Operator (Mastery Scoring Engine)**: An idempotent, deterministic evaluator that computes segment performance (Ranks S, A, B, C/Fail) upon room clear or capture. The Grade Operator listens to typed, immutable lifecycle and decision facts (`Chase-entry`, `Capture`, and room-exit residual snapshots) via the event bus, applying bounded penalties ($grade\_penalty\_cap = 90$) from a base score of 100. Clean manipulation tool usage (Burst noise-makers) is never directly penalized on the scorecard; distraction searches generate in-world residual wariness ($R$) rather than score deductions, preserving player agency, strategic diversion, and core pillar integrity.

Architecturally governed by `docs/architecture/adr-0004-suspicion-meter-grade.md`, the system functions purely as an event consumer and presentation pipeline, maintaining zero simulation authority and remaining robust against network/transport jitter and lifecycle resets. It delivers the core stealth fantasy of a calculating infiltrator who reads guard attention like a chessboard and executes flawless, zero-detection clean runs.

## Player Fantasy

The Suspicion Meter and Grade Operator serve the fantasy of the **Calculating Infiltrator** — a phantom operative who does not rely on twitch reflexes or violent force, but on cold observation, spatial awareness, and immaculate execution.

### 1. The Thrill of the Calculated Gamble (Pillar 2: Fair Mind-Challenge)
- **Tension Through Transparency, Never Ambiguity**: The player never asks *"How did they see me?"*. The meter makes risk tangible and readable in real-time. As the player darts across a moonlit corridor, watching the amber gauge climb while gauging the guard's distance, they experience a sharp spike of deliberate, calculated adrenaline.
- **The Sweet Taste of a Narrow Escape**: Slipping behind cover just as the meter kisses the Investigate threshold, the player feels the breathless tension of the confirm-window grace period. When line-of-sight breaks and the meter begins its clean visual reset, the relief is earned — not an arbitrary dice roll, but the reward of decisive positioning.
- **Fairness in Defeat**: When an alarm sounds or a guard charges into Chase, the player feels accountability rather than frustration. They saw the threshold marker sink lower due to previous close calls; they knew the guard was wary; they pushed their luck too far.

### 2. You Create the Situation (Pillar 3: Unpenalized Tool Agency)
- **Meaningful Tools Without Punitive Taxes**: Players feel empowered to reshape the room using Burst noise-makers. A clean, well-placed distraction that draws a guard away from a door and allows a silent bypass is celebrated as masterclass stealth. The game never taxes clean tool usage on the scorecard; the consequence of distraction lives inside the living simulation (heightened residual wariness $R$), not as an arbitrary penalty deduction.

### 3. The Pride of Flawless Mastery (Pillar 4: Visible Intelligence & Clean Runs)
- **The "Silent Assassin" Benchmark**: The end-of-segment Grade Card is the ultimate badge of honor. Earning an **S-Rank** validates that the player outsmarted the facility's thinking AI entirely — zero alarms, zero chases, and no sloppy detections. An active Chase in any room mathematically disqualifies the run from Rank S.
- **The "One More Try" Drive**: A tainted grade (Rank A or B) exposes exact telemetry breakdowns: *"1 Chase Escalation (-25.0)", "Residual Wariness Snapshot (-4.5)"*. Players immediately understand where their execution fractured and are driven by the classic stealth compulsion: *"I know what I did wrong. Let me do it clean."*

## Detailed Design

### Core Rules

#### 1. Meter Threat Composition & Dominant Guard Selection
- **Per-Guard Tracking**: For every active guard $g_i$ in the active segment, the Suspicion Meter tracks:
  - Accumulator state: $A_i \in [0.0, 1.0]$.
  - Residual wariness: $R_i \in [0.0, R_{\max}]$.
  - Dynamic Investigate threshold: $T_{\text{entry}, i} = \max(T_{\text{base}} - k_{\text{res}} \cdot R_i, T_{\text{floor}})$.
- **Dominant Threat Selection**: The HUD presents a single **Dominant Guard Indicator** to preserve screen legibility and ensure WebGL draw-call efficiency:
  $$r_{\text{threat}, i} = \begin{cases} \dfrac{A_i}{T_{\text{entry}, i}} & \text{if } T_{\text{entry}, i} > 0 \\ 0.0 & \text{otherwise} \end{cases}$$
  - **Zero-Threat Invariant**: If all active guards have zero suspicion ($\max_{g_i}(A_i) == 0.0$), the dominant guard is $g^* = \text{null}$, the meter displays `SuspicionMeterRegion.Quiet`, and the directional chevron is completely hidden (preventing chevron indicators from tracking unalerted guards through walls).
  - If $\max_{g_i}(A_i) > 0.0$, the dominant guard is:
    $$g^* = \arg\max_{g_i \in G_{\text{active}}} \left( r_{\text{threat}, i} \right)$$
- **Tie-Breaking Cascade**:
  1. $\arg\max (A_i)$ (higher absolute accumulator priority).
  2. If tied: $\arg\min (d_{\text{Euclid}}(g_i, \text{player}))$ (closer physical proximity).
  3. If still tied: Ordinal string comparison of entity ID: $\min(\text{guard\_eid}_i, \text{guard\_eid}_j)$.
- **Planar Azimuth Chevron Projection**:
  - To prevent screen-edge flipping and coordinate inversion when guards cross behind the camera plane ($Z_{\text{cam}} < 0$), the 2D directional chevron azimuth is computed in camera-local space:
    $$\vec{p}_{\text{cam}} = \mathbf{V}_{\text{view}} \cdot (\vec{p}_{\text{guard}} - \vec{p}_{\text{camera}})$$
    $$\theta_{\text{azimuth}} = \text{atan2}(x_{\text{cam}},\ z_{\text{cam}})$$
  - Angle $\theta_{\text{azimuth}}$ maps directly to the screen-space ellipse ($R_x = 240\text{ px}, R_y = 160\text{ px}$), providing smooth, continuous 360-degree tracking without singularities.
- **Room Portal Boundary Filtering**:
  - Upstream Perception filters active sensing candidates $G_{\text{active}}$ strictly to guards assigned to the player's current authoritative room ID (`RoomId`). Guards in adjacent, unentered segments are excluded from snapshot publication.

#### 2. Confirm-Window Grace & Clean Break Resets
- **Sub-Threshold Clears**: While $A^* < T_{\text{entry}}^*$, breaking line-of-sight (LOS) immediately resets authoritative $A^*$ to $0.0$ at the virtual tick boundary in Perception. No investigation is triggered.
- **Confirm Window Grace**: When $A^*$ crosses $T_{\text{entry}}^*$, a confirm window timer $t_{\text{confirm}}$ ($0.25\text{ s}$) engages:
  - If LOS breaks before $t_{\text{confirm}}$ expires: The escalation cancels, the guard remains in Patrol, and $A^*$ resets to $0.0$.
  - If LOS persists past $t_{\text{confirm}}$: The guard's FSM formally commits to `Investigate` (`sight-committed`).
- **Segment Transition Reset**: Upon crossing a certified segment boundary, all active accumulators $A_i$ and residual values $R_i$ reset strictly to $0.0$ (carried-in exposure never penalizes a new segment).

#### 3. Chase-Tier Lock & Meter Inactivity
- **Pursuit Transition**: When any guard emits `Chase-entry` (via accumulator reaching $T_{\text{chase}} = 1.0$, or via witnessed hide-spot entry):
  - Suspicion Meter transitions into **Locked Pursuit State**: HUD gauge pulses warm red-orange (`CHASE ACTIVE`), directional chevron locks on the pursuing guard, and a subtle screen-edge amber vignette pulses.
  - While in Chase: Accumulator $A$ and residual $R$ are **strictly inert** (no accumulation, no decay).
- **Chase Resolution**: Upon receiving `Chase-end`:
  - Meter exits Locked Pursuit State.
  - Accumulator $A$ resets to $0.0$.
  - Residual wariness $R$ unfreezes and resumes exponential decay toward $0.0$.

#### 4. Witnessed Hide-Spot Incident Accounting
- **Incident Slot Counting**: When a player dives into a hide spot under direct guard sight (`witnessed hide-spot dive` with `carve_out = true`):
  - If the player subsequently escapes or breaks pursuit: The incident bills as exactly **ONE Chase escalation** ($N_{\text{chase}} += 1, -25.0\text{ pts}$).
  - If the guard reaches the spot, verifies presence, and executes capture: The episode terminates the room as `GradeCompletionStatus.Failed` (Score $= 0$, Rank **F**). The telemetry record correlates the witnessed dive with the terminal capture on the shared `entry_id` to eliminate redundant double-deduction reporting in the diagnostic breakdown.

#### 5. Grade Operator Evaluation Lifecycle & Scoring Rules
- **Base Score**: Every segment attempt begins with $S_{\text{base}} = 100.0$ points.
- **Global Incident Penalty Pooling**:
  - To prevent multi-guard penalty dilution exploits, Chase escalations represent a systemic compromise of room security and are **pooled globally** at the room level, never diluted by unalerted guards:
    $$P_{\text{incident, room}} = \min\left( \text{grade\_penalty\_cap},\ \sum_{i=1}^M \left( N_{\text{chase}, i} \cdot W_{\text{chase}} \right) \right)$$
    Where $W_{\text{chase}} = 25.0\text{ pts}$ (`grade_weight_chase`) and $\text{grade\_penalty\_cap} = 90.0\text{ pts}$.
- **Clean Distraction Exemption (Zero Score Deduction)**:
  - Fruitless noise investigations ($N_{\text{fruitless}}$) resulting from Burst noise-maker deployments incur **$0.0\text{ pts}$ direct scorecard penalty**. Distractions feed authoritative in-world residual wariness ($R$) within the simulation, preserving player agency while naturally sharpening AI senses against sloppy follow-up maneuvers.
- **Residual Wariness Penalty ($P_{\text{residual, room}}$)**:
  - Exposure weighting across guards applies strictly to residual wariness snapshots at room exit:
    $$P_{\text{residual, room}} = \sum_{i=1}^M \left( \frac{w_i}{W_{\text{total}}} \cdot P_{\text{residual}, i} \right), \quad P_{\text{residual}, i} = W_{\text{residual}} \times \text{clamp}\left( \frac{R_{\text{final}, i}}{R_{\max}},\ 0.0,\ 1.0 \right)$$
    Where $W_{\text{residual}} = 10.0\text{ pts}$ (`grade_weight_residual`).
- **Total Penalty Cap & Completion Floor**:
  - Total room deductions are capped globally to guarantee a completion floor of $10.0\text{ pts}$ for completed runs:
    $$P_{\text{total}} = \min\left( \text{grade\_penalty\_cap},\ P_{\text{incident, room}} + P_{\text{residual, room}} \right)$$
- **Terminal Outcomes**:
  - **Segment Clear**: Final Score $S_{\text{room}} = \max(10.0,\ 100.0 - P_{\text{total}})$.
  - **Capture**: Final Score $= 0.0$, awarded Rank **F** (Fail).

### States and Transitions

#### Table 1: Suspicion Meter Presenter State Machine
| State | Entry Condition | Active Behaviors | Exit Condition |
|---|---|---|---|
| **Idle** | $A^* = 0.0 \land \text{FSM} == \text{Patrol}$ | Gauge empty ($0\%$), dim; directional chevron hidden. | $A^* > 0.0$ (LOS contact established) $\to$ **Accumulating**. |
| **Accumulating** | $A^* > 0.0 \land A^* < T_{\text{entry}}^*$ | Cold blue-grey fill; directional chevron indicates $g^*$; audio low hum. | LOS break $\to$ **Resetting**; $A^* \ge T_{\text{entry}}^*$ $\to$ **Confirming**. |
| **Confirming** | $A^* \ge T_{\text{entry}}^* \land t_{\text{elapsed}} < t_{\text{confirm}}$ | Amber gauge pulsing; audio heartbeat cue; confirm-timer ticking. | LOS break before $t_{\text{confirm}}$ $\to$ **Resetting**; $t_{\text{confirm}}$ expired $\to$ **Investigating**. |
| **Investigating** | $t_{\text{confirm}}$ expired $\land \text{FSM} == \text{Investigate}$ | Amber fill climbing toward $1.0$; guard investigating; directional chevron active. | LOS break $\to$ **Resetting**; $A^* \ge 1.0$ or Witnessed Dive $\to$ **PursuitLocked**. |
| **PursuitLocked** | Receipt of `Chase-entry` | Gauge locked at $100\%$ glowing red-orange; border vignette pulses; alarm stinger. | Receipt of `Chase-end` $\to$ **Resetting**; `Capture` $\to$ **TerminalCaptured**. |
| **Resetting** | LOS broken while $A^* > 0.0$ outside Chase | Authoritative $A^*$ is $0.0$; visual UI bar executes rapid cosmetic drain ($1.0\text{ s}$ linear decay); chevron fades. | Gauge hits $0\% \to$ **Idle**; New LOS contact ($A^* > 0$) $\to$ gauge immediately resynchronizes without delay $\to$ **Accumulating**. |
| **TerminalCaptured** | Receipt of `Capture` | Screen freeze/fade; red alert lock; telemetry flush. | Segment restart $\to$ **Idle**. |

*Note on Presenter Decoupling*: The $1.0\text{ s}$ gauge drain in `Resetting` is a pure cosmetic smoothing filter over an underlying $A^* = 0.0$. If the player re-establishes LOS during `Resetting`, the presenter immediately cancels the decay animation and snaps its logical tracking to the authoritative snapshot value.

#### Table 2: Grade Operator State Lifecycle
| Lifecycle State | Trigger Event | Internal Operations | Output |
|---|---|---|---|
| **Unarmed** | Scene load / Application start | Clear session caches; allocate initial data buffers. | None. |
| **Armed** | `Segment-start` (new `session_id`, `attempt_epoch`) | Initialize $S = 100.0$; reset $N_{\text{chase}} = 0, P_{\text{incident, room}} = 0.0, P_{\text{total}} = 0.0$. | Telemetry log `GradeOperatorArmed`. |
| **Recording** | Active gameplay in segment | Ingest deduplicated typed event facts: `Chase-entry`, `Capture`. | Update internal incident counters. |
| **Evaluated** | `Segment-clear` OR `Capture` | Ingest exit residual snapshot, apply global pooled scoring, evaluate Rank S zero-chase gate. | Emit `Grade-record` to Event Bus; pass to HUD & Save. |

### Interactions with Other Systems

| System | Direction | Interface / Contract | Description |
|---|---|---|---|
| **Perception (#2)** | Upstream $\to$ Meter | Per-tick sensing snapshot `GuardSuspicionSnapshot`: `(guard_eid, a_current, r_current, t_chase, investigation_entry_threshold, guard_state, guard_position, distance_to_player, last_causal_event, source_entry_id)` | Supplies live telemetry for accumulator fill, sinking threshold marker, distance tie-breaking, and azimuth chevron projection. Filters $G_{\text{active}}$ to player's current `RoomId`. |
| **Guard AI FSM (#1)** | Upstream $\to$ Grade | Deduplicated event records: `Chase-entry`, `Capture`, `Chase-end`. Idempotency key: `(session_id, attempt_epoch, entry_id, guard_eid, event_type)`. | Supplies immutable decision facts for Chase locking and global room incident penalty pooling. |
| **Event Bus (#15)** | Downstream $\leftarrow$ Grade | `Grade-record` published event (`RoomGradeFinalized`) | Delivers immutable score payload `{session_id, attempt_epoch, segment_id, score, rank, breakdown}`. |
| **HUD / UI (#14)** | Downstream $\leftarrow$ Meter/Grade | Presenter data bindings | Drives Suspicion Gauge, Directional Chevron (via planar azimuth), Vignette, and end-of-segment Grade Card. |
| **Save / Session (#16)** | Downstream $\leftarrow$ Grade | Persistent grade record save | Records best rank achieved per segment across playable campaign attempts. |
| **Win/Lose & Respawn (#9)** | Bidirectional | `Segment-clear` / `Capture` trigger $\leftrightarrow$ Grade evaluation | Signals grade finalization and coordinates retry de-escalation on capture. |

## Formulas

### D1: Dominant Threat Selection Ratio ($g^*$)
- **Output**: $g^* \in G_{\text{active}} \cup \{\text{null}\}$, $r_{\text{threat}}^* \in [0.0, 25.0]$.
- **Inputs**: 
  - $A_i \in [0.0, 1.0]$: Suspicion accumulator of guard $i$ (Perception GDD #2).
  - $T_{\text{entry}, i} \in [0.04, 0.40]$: Dynamic Investigate entry threshold of guard $i$ (D3).
  - $G_{\text{active}}$: Set of active, alertable guards registered in the room/segment.
- **Formula**:
  $$r_{\text{threat}, i} = \begin{cases} \dfrac{A_i}{T_{\text{entry}, i}} & \text{if } T_{\text{entry}, i} > 0 \\ 0.0 & \text{otherwise} \end{cases}$$
  $$g^* = \begin{cases} \text{null} & \text{if } |G_{\text{active}}| == 0 \lor \max_{g_i \in G_{\text{active}}}(A_i) == 0.0 \\ \arg\max_{g_i \in G_{\text{active}}} \left( r_{\text{threat}, i} \right) & \text{otherwise} \end{cases}$$
- **Tie-Breaking Cascade**:
  1. $\arg\max (A_i)$ (higher absolute accumulator priority).
  2. If tied: $\arg\min (d_{\text{Euclid}}(g_i, \text{player}))$ (closer physical proximity).
  3. If still tied: Ordinal string comparison of entity ID: $\min(\text{guard\_eid}_i, \text{guard\_eid}_j)$.
- **Boundary Behavior**:
  - When $g^* == \text{null}$: HUD switches to `SuspicionMeterRegion.Quiet`, gauge fill $0\%$, directional chevron hidden.

---

### D2: Normalized Meter Ratio & Display Percentage
- **Output**: $\text{MeterRatio} \in [0.0, 1.0]$, $\text{MeterPercent} \in [0, 100]$, $\text{Region} \in \{\text{Unavailable, Quiet, Investigate, Chase}\}$.
- **Inputs**:
  - $A^* \in [0.0, 1.0]$: Suspicion accumulator of dominant guard $g^*$.
  - $T_{\text{chase}} \in [0.85, 1.00]$ (starter: $1.00$): Authoritative Chase commitment threshold.
  - $T_{\text{entry}}^* \in [0.04, 0.40]$: Dynamic Investigate threshold of $g^*$.
  - $\text{meter\_display\_scale} = 100$ (from registry).
- **Formula**:
  $$\text{MeterRatio} = \begin{cases} 0.0 & \text{if } g^* == \text{null} \\ \text{clamp}\left( \dfrac{A^*}{T_{\text{chase}}}, 0.0, 1.0 \right) & \text{otherwise} \end{cases}$$
  $$\text{MeterPercent} = \text{round}\left( \text{MeterRatio} \times \text{meter\_display\_scale} \right)$$
  $$\text{Region} = \begin{cases}
  \text{Unavailable} & \text{if snapshot is corrupt, missing, or non-finite} \\
  \text{Chase} & \text{if } g^* \neq \text{null} \land (A^* \ge T_{\text{chase}} \lor \text{GuardState} == \text{"Chase"}) \\
  \text{Investigate} & \text{if } g^* \neq \text{null} \land A^* \ge T_{\text{entry}}^* \land A^* < T_{\text{chase}} \\
  \text{Quiet} & \text{otherwise}
  \end{cases}$$

---

### D3: Dynamic Investigate Entry Threshold & Forgiveness Floor Chain (F15)
- **Output**: $T_{\text{entry}}(R) \in [0.04, 0.40]$ (starter slice: $[0.06, 0.30]$).
- **Inputs**:
  - $T_{\text{base}} = 0.30$ ($[0.20, 0.40]$): Baseline Investigate entry threshold.
  - $k_{\text{res}} = 0.24$ ($[0.15, 0.30]$): Residual wariness sensitivity factor.
  - $R \in [0.0, R_{\max}]$ ($[0.0, 1.0]$): Guard's authoritative residual wariness.
  - $T_{\text{floor}} = 0.20 \times T_{\text{base}}$ ($[0.04, 0.08]$): Floor depression limit ($0.060$).
  - $\text{forgiveness\_floor} = 0.030$ ($[0.05 \times T_{\text{base}}, 0.20 \times T_{\text{base}})$): Sub-threshold LOS accumulator reset floor.
- **Formula**:
  $$T_{\text{entry}}(R) = \max\left( T_{\text{base}} - k_{\text{res}} \cdot R,\ T_{\text{floor}} \right)$$
- **F15 Joint Constraint Chain**:
  $$0.0 \le \text{forgiveness\_floor} < T_{\text{floor}} = 0.060$$
  $$\text{margin}_{\text{forgive}} = T_{\text{floor}} - \text{forgiveness\_floor} = 0.060 - 0.030 = 0.030 > 0.0$$

---

### D4: Global Room Incident Penalty Pooling ($P_{\text{incident, room}}$)
- **Output**: $P_{\text{incident, room}} \in [0.0, 90.0]$ (pts).
- **Inputs**:
  - $N_{\text{chase}, i} \ge 0$: Number of Chase escalations and verified witnessed dive entries for guard $i$.
  - $W_{\text{chase}} = 25.0$ (`grade_weight_chase`).
  - $\text{grade\_penalty\_cap} = 90.0$ (`grade_penalty_cap`).
  - $M$: Number of evaluated guards in the room.
- **Formula**:
  $$P_{\text{incident, room}} = \min\left( \text{grade\_penalty\_cap},\ \sum_{i=1}^M \left( N_{\text{chase}, i} \cdot W_{\text{chase}} \right) \right)$$
- **Tool Exemption Invariant**: Fruitless noise searches ($N_{\text{fruitless}}$) are excluded from direct score deductions ($W_{\text{fruitless}} \equiv 0.0$).

---

### D5: Exposure-Weighted Room Residual Penalty ($P_{\text{residual, room}}$)
- **Output**: $P_{\text{residual, room}} \in [0.0, 10.0]$ (pts).
- **Inputs**:
  - $R_{\text{final}, i} \in [0.0, R_{\max}]$: Authoritative final residual wariness snapshot for guard $i$ at room exit.
  - $R_{\max} = 1.00$: Maximum residual capacity (`r_max`).
  - $W_{\text{residual}} = 10.0$: Residual penalty weight (`grade_weight_residual`).
  - $w_i > 0$ (default $1.0$): Exposure weight for guard $i$.
  - $W_{\text{total}} = \sum_{i=1}^M w_i$: Total exposure weight.
- **Formula**:
  $$P_{\text{residual}, i} = W_{\text{residual}} \times \text{clamp}\left( \frac{R_{\text{final}, i}}{R_{\max}},\ 0.0,\ 1.0 \right)$$
  $$P_{\text{residual, room}} = \begin{cases} 0.0 & \text{if } M == 0 \lor W_{\text{total}} \le 0 \\ \sum_{i=1}^M \left( \dfrac{w_i}{W_{\text{total}}} \cdot P_{\text{residual}, i} \right) & \text{otherwise} \end{cases}$$

---

### D6: Room Quality Score ($S_{\text{room}}$) & Total Penalty Cap
- **Output**: $S_{\text{room}} \in [0.0, 100.0]$ (pts), $P_{\text{total}} \in [0.0, 90.0]$ (pts).
- **Inputs**:
  - $S_{\text{base}} = 100.0$: Base score.
  - $P_{\text{incident, room}} \in [0.0, 90.0]$: Global pooled incident penalty (D4).
  - $P_{\text{residual, room}} \in [0.0, 10.0]$: Exposure-weighted residual penalty (D5).
  - $\text{grade\_penalty\_cap} = 90.0$: Total deduction ceiling.
  - $\text{Status} \in \{\text{Completed, Failed, Unresolved}\}$.
- **Formula**:
  $$P_{\text{total}} = \min\left( \text{grade\_penalty\_cap},\ P_{\text{incident, room}} + P_{\text{residual, room}} \right)$$
  $$S_{\text{room}} = \begin{cases}
  0.0 & \text{if } \text{Status} == \text{Failed} \\
  0.0 & \text{if } \text{Status} == \text{Unresolved} \\
  100.0 & \text{if } \text{Status} == \text{Completed} \land M == 0 \\
  \max\left( 10.0,\ S_{\text{base}} - P_{\text{total}} \right) & \text{if } \text{Status} == \text{Completed} \land M > 0
  \end{cases}$$

---

### D7: Sanctuary Room Evaluation ($M = 0$)
- **Output**: $S_{\text{room}} = 100.0$, $\text{Grade} = \text{RoomGrade.S}$, $\text{Status} = \text{GradeCompletionStatus.Completed}$.
- **Rule**: When a room segment contains zero guards ($M = 0$) and the player reaches `RoomCompletionBoundary` without triggering environment death, the room completes cleanly with full points.

---

### D8: Grade Letter Band Mapping & Rank S Zero-Chase Gate
- **Output**: $\text{Grade} \in \{\mathbf{None/F},\ \mathbf{S},\ \mathbf{A},\ \mathbf{B},\ \mathbf{NeedsImprovement/C}\}$.
- **Inputs**:
  - $S_{\text{room}} \in [0.0, 100.0]$: Room quality score (D6).
  - $\text{Status} \in \{\text{Completed, Failed, Unresolved}\}$.
  - $\sum N_{\text{chase}}$: Total Chase escalations in the room.
  - Thresholds: $\Theta_S = 90.0, \Theta_A = 75.0, \Theta_B = 60.0$.
- **Formula**:
  $$\text{Grade} = \begin{cases}
  \text{RoomGrade.None (Rank F)} & \text{if } \text{Status} \in \{\text{Failed},\ \text{Unresolved}\} \\
  \text{RoomGrade.S} & \text{if } \text{Status} == \text{Completed} \land S_{\text{room}} \ge \Theta_S \land \sum_{i=1}^M N_{\text{chase}, i} == 0 \\
  \text{RoomGrade.A} & \text{if } \text{Status} == \text{Completed} \land S_{\text{room}} \ge \Theta_A \\
  \text{RoomGrade.B} & \text{if } \text{Status} == \text{Completed} \land S_{\text{room}} \ge \Theta_B \\
  \text{RoomGrade.NeedsImprovement (Rank C)} & \text{if } \text{Status} == \text{Completed} \land S_{\text{room}} < \Theta_B
  \end{cases}$$
- **Mastery Invariant**: Triggering even a single Chase escalation ($N_{\text{chase}} \ge 1$) strictly disqualifies the attempt from Rank S, regardless of room score or guard count.

## Edge Cases

| # | Condition | Resolution |
|---|---|---|
| **E1** | **Chập chờn mối đe doạ thống trị (Jittery Threat Swap)**: Hai lính $g_1, g_2$ có tỷ lệ đe doạ xấp xỉ nhau ($r_1 \approx r_2$) dao động qua lại từng frame do khoảng cách hoặc raycast. | Áp dụng ngưỡng trễ chuyển đổi (hysteresis margin) $\Delta r_{\text{swap}} = 0.05$ và khoảng thời gian giữ tối thiểu $\tau_{\text{dwell}} = 0.10\text{ s}$. Dominant Guard $g^*$ chỉ bị hoán đổi khi mối đe doạ mới vượt trội hơn ít nhất $0.05$ hoặc lính mới chính thức bước vào trạng thái `Investigate` / `Chase`. |
| **E2** | **Cắt tầm nhìn đúng tick kết thúc Confirm Window ($t = t_{\text{confirm}}$)**: Người chơi kịp khuất bóng sau vật cản đúng tại frame/tick đồng hồ $0.25\text{ s}$ chạm mốc. | Đánh giá tại virtual tick boundary: Nếu trạng thái LOS là `false` tại tick kết thúc confirm window, hành vi huỷ bỏ leo thang (`cancellation`) luôn được ưu tiên. Guard duy trì Patrol, accumulator $A$ reset về $0.0$. `Investigate` chỉ commit khi LOS duy trì liên tục qua toàn bộ cửa sổ bao gồm cả tick hết hạn. |
| **E3** | **Bị bắt hoặc chết khi Meter đang ở trạng thái Confirming / Investigating**: Người chơi bị bắt bởi một lính khác hoặc chết do bẫy môi trường trong khi dominant meter đang tăng. | Sự kiện `Capture` hoặc `PlayerDeath` ngay lập tức đóng băng toàn bộ Presenter, vô hiệu hoá mọi bộ đếm. Room Grade Operator nhận sự kiện `Capture` / `RoomFailed`, chuyển trạng thái thành `GradeCompletionStatus.Failed`, điểm số trả về $0.0$ (Rank **F**) và đóng epoch hiện tại. |
| **E4** | **Thiếu hoặc hỏng dữ liệu Telemetry (Snapshot Corruption / Disconnect)**: Dữ liệu snapshot từ Perception hoặc FSM bị mất gói, trễ nhịp hoặc chứa giá trị phi hữu hạn (`NaN`, `Infinity`). | Tuân thủ nghiêm ngặt ADR-0004: Suspicion Meter chuyển sang `SuspicionMeterRegion.Unavailable` (tuyệt đối không bịa số $0$ giả lập). Đối với Grade Operator, nếu biên kết thúc thiếu trace bắt buộc hoặc snapshot residual không hợp lệ, phát sinh kết quả `GradeCompletionStatus.Unresolved`, không chấm điểm bừa. |
| **E5** | **Tạm dừng game (Pause Menu / Escape) giữa lúc Chase hoặc Confirm Window**: Người chơi mở menu Pause khi thanh đo đang cảnh báo nguy cấp. | Mọi bộ đếm thời gian ($t_{\text{confirm}}$, thời gian trễ hiển thị, nhịp đập audio) đều neo theo thời gian mô phỏng ảo của game ($t_{\text{sim}}$ / `Time.time`), không neo theo wall-clock time (`Time.unscaledTime`). Khi Pause, toàn bộ trạng thái đóng băng chính xác, không thất thoát hoặc trôi dạt dữ liệu khi Resume. |
| **E6** | **Phòng không có lính tuần tra (Sanctuary Room / Transit Corridor)**: Phân đoạn chơi có $|G_{\text{active}}| == 0$ (phòng an toàn, hành lang chuyển tiếp). | Suspicion Meter duy trì trạng thái nghỉ (`Quiet` / gauge fill 0%, ẩn chevron). Khi chạm ranh giới kết thúc phòng (`RoomCompletionBoundary`), Grade Operator xác định $M = 0$, không có sự cố, tự động đánh giá điểm $S_{\text{room}} = 100.0$, trạng thái `Completed`, và trao Rank **S**. |
| **E7** | **Nhiều lính cùng kích hoạt Chase trong cùng một tick**: Người chơi bước vào vùng giao nhau giữa tầm nhìn của 2-3 lính và cả 3 đều đạt ngưỡng Chase cùng lúc. | Trace Reducer tiếp nhận và ghi nhận từng bản ghi `Chase-entry` độc lập cho từng lính có `guard_eid` riêng biệt. Mỗi sự kiện cộng $25\text{ pts}$ vào tổng phạt sự cố phòng $P_{\text{incident, room}}$ (bị chặn trần tối đa $90\text{ pts}$). HUD Suspicion Meter khoá pursuit vào lính ở cự ly gần nhất ($d_{\text{Euclid}}$ nhỏ nhất). |
| **E8** | **Lặp lại hành vi nhảy trốn vào HideSpot dưới tầm mắt lính (Witnessed Dive Loop)**: Người chơi liên tục nhảy vào, nhảy ra khỏi HideSpot khi đang bị truy đuổi. | Mỗi lần nhảy vào HideSpot có người chứng kiến (`witnessed hide-spot dive`) được FSM phát sinh một sự kiện `Chase-entry` mới (mỗi lần phạt $25\text{ pts}$) cho đến khi chạm trần phạt $90\text{ pts}$. Nếu bị bắt tại chỗ trốn, phát sinh sự kiện `Capture` đơn nhất kết thúc phòng ở điểm 0 (Rank F), không ghi nhận lặp kép trên telemetry. |
| **E9** | **Lính di chuyển ra phía sau mặt phẳng camera ($Z_{\text{cam}} < 0$)**: Lính áp sát từ phía sau hoặc người chơi xoay camera nhanh. | Thuật toán Planar Azimuth Projection tính góc $\theta = \text{atan2}(x_{\text{cam}}, z_{\text{cam}})$ trong không gian camera. Mũi tên chevron di chuyển mượt mà xuống cung dưới của hình elip ($Y_{\text{screen}} < 0$), không xảy ra hiện tượng đảo ngược toạ độ hoặc nhảy góc 180 độ. |

## Dependencies

### Upstream Dependencies (Hệ thống cung cấp dữ liệu đầu vào)

| System | Path | Data / Events Exchanged | Direction | Failure Behavior |
|---|---|---|---|---|
| **Perception (#2)** | `design/gdd/perception.md` | `GuardSuspicionSnapshot` per-tick: `(guard_eid, a_current, r_current, t_chase, investigation_entry_threshold, guard_state, guard_position, distance_to_player, last_causal_event, source_entry_id)`. Đồng sở hữu chuỗi ràng buộc F15 `forgiveness_floor` ($0.030$). | In ($\leftarrow$) | Nếu snapshot thiếu hoặc chứa `NaN`/`Infinity`, Presenter chuyển về `SuspicionMeterRegion.Unavailable`, không bịa số $0$. Lọc ứng viên $G_{\text{active}}$ theo `RoomId`. |
| **Guard AI FSM (#1)** | `design/gdd/guard-ai-fsm.md` | Các bản ghi quyết định bất biến: `Chase-entry` (nguyên nhân `threshold` hoặc `hide-entry`), `Capture`, và `Chase-end`. Bộ nhận diện khử trùng lặp `(session_id, attempt_epoch, entry_id, guard_eid, event_type)`. | In ($\leftarrow$) | Dữ liệu trùng lặp hoặc stale epoch bị bỏ qua; nếu thiếu trace bắt buộc, Grade Operator từ chối chấm điểm hợp lệ (`Unresolved`). |
| **Player Movement & Hide (#5)** | `design/gdd/player-movement-hide.md` | Sự kiện lặn trốn bị chứng kiến (`witnessed hide-spot dive` với `carve_out = true`). | In ($\leftarrow$) | Tính như đúng 1 lần leo thang Chase ($N_{\text{chase}} += 1, -25\text{ pts}$). Nếu lặn trốn không bị nhìn thấy, hoàn toàn không tính phạt. |
| **Player Noise (#3)** | `design/gdd/player-noise.md` | Các sự kiện tiếng động dẫn đến FSM Investigate (gián tiếp qua FSM resolution). | In ($\leftarrow$) | Sử dụng công cụ nghi binh (Burst) sạch sẽ hoàn toàn không bị trừ điểm trực tiếp ($0.0\text{ pts}$); việc điều tra chỉ tích luỹ cảnh giác tàn dư $R$ trong mô phỏng thế giới. |
| **Win/Lose & Respawn (#9)** | `design/gdd/game-concept.md` | Sự kiện ranh giới hoàn tất phòng `RoomCompletionBoundary` và sự kiện `PlayerDeath` / `Capture`. | In ($\leftarrow$) | Kích hoạt bộ chấm điểm RoomGradeOperator đóng epoch và tính điểm hoặc đánh dấu `Failed`. |

### Downstream Dependents (Hệ thống tiêu thụ dữ liệu đầu ra)

| System | Path | Data / Events Exchanged | Direction | Failure Behavior |
|---|---|---|---|---|
| **HUD & UI (#14)** | `design/gdd/hud-ui.md` | `SuspicionMeterReadModel` (`MeterRatio`, `MeterPercent`, `Region`, dominant chevron azimuth) và dữ liệu Grade Card cuối phòng (`score`, `grade`, `breakdown`). | Out ($\rightarrow$) | Nếu dữ liệu `Unavailable`, HUD ẩn chevron, gauge fill $0\%$. Grade Card hiển thị `UNRESOLVED` nếu thiếu chứng cứ. |
| **Event Messaging Bus (#15)** | `docs/architecture/adr-0001-event-messaging-bus.md` | Đăng ký nhận sự kiện FSM và phát hành sự kiện cuối cùng `RoomGradeFinalized` (`Grade-record`). | In/Out ($\leftrightarrow$) | Vận chuyển bất biến, khử trùng lặp chính xác theo ADR-0004. |
| **Save / Session System (#16)** | `design/gdd/save-session.md` | Lưu trữ kỷ lục thành tích tốt nhất theo phòng: `{session_id, segment_id, grade, score, attempt_epoch, timestamp}`. | Out ($\rightarrow$) | Chỉ các phòng có `GradeCompletionStatus.Completed` mới được ghi nhận kỷ lục rank (Rank F và Unresolved bị loại trừ). |

## Tuning Knobs

### 1. Scoring & Penalty Balance Knobs (Hệ số Chấm điểm & Xử phạt)

| Knob Name | Default | Safe Range | Unit | Category | Gameplay Effect | What Breaks Outside Range |
|---|---|---|---|---|---|---|
| `grade_weight_chase` | 25.0 *(from registry)* | [10.0, 50.0] | pts/event | Balance | Điểm phạt cho mỗi lần guard leo thang Chase hoặc người chơi lặn trốn có chứng kiến. | <10.0: Bị rượt đuổi không còn tính răn đe; >50.0: Chỉ 2 lần Chase là tụt hạng thảm hại, phá vỡ đường cong học tập. |
| `grade_weight_fruitless` | 0.0 *(obsolete)* | [0.0, 0.0] | pts/event | Balance | Đã bãi bỏ theo phán quyết Creative Director và Pillar 3. Sử dụng công cụ nghi binh không bị trừ điểm trực tiếp. | >0.0: Vi phạm cam kết Pillar 3 và game-concept.md, trừng phạt người chơi vì sử dụng công cụ ném Burst. |
| `grade_weight_capture` | 50.0 *(from registry)* | [25.0, 100.0] | pts/event | Balance | Trọng số phạt chẩn đoán khi người chơi bị bắt (phục vụ phân tích breakdown). | Giá trị chẩn đoán thuần tuý; khi bị bắt thì Room Status luôn là `Failed` (Score = 0.0). |
| `grade_weight_residual` | 10.0 *(from registry)* | [5.0, 15.0] | pts | Balance | Trọng số phạt tối đa cho mức độ cảnh giác tàn dư ($R_{\text{final}}$) khi rời khỏi phòng. | >15.0: Phạt tàn dư lấn át cả lỗi bị rượt đuổi; <5.0: Người chơi thoải mái để lính trong trạng thái bán nghi ngờ. |
| `grade_penalty_cap` | 90.0 *(from registry)* | [70.0, 95.0] | pts | Core | Trần khấu trừ điểm sự cố tối đa cho toàn phòng (đảm bảo sàn điểm 10.0 cho màn hoàn thành). | <70.0: Người chơi phạm vô số sai lầm vẫn nhận Rank B; >95.0: Không còn sàn điểm an toàn $10.0$ cho người hoàn thành phòng. |
| `grade_threshold_s` | 90.0 *(from registry)* | [85.0, 95.0] | pts | Balance | Ngưỡng điểm tối thiểu đạt Rank **S** (Silent Assassin). Bắt buộc kèm điều kiện $\sum N_{\text{chase}} == 0$. | <85.0: Cho phép đạt Rank S quá dễ dàng; >95.0: Bất khả thi nếu chỉ cần một thoáng tàn dư nhỏ. |
| `grade_threshold_a` | 75.0 *(from registry)* | [70.0, 85.0] | pts | Balance | Ngưỡng điểm tối thiểu đạt Rank **A** (Chuyên nghiệp). | Bắt buộc duy trì thứ tự đơn điệu: $\Theta_B < \Theta_A < \Theta_S$. |
| `grade_threshold_b` | 60.0 *(from registry)* | [50.0, 70.0] | pts | Balance | Ngưỡng điểm tối thiểu đạt Rank **B** (Hoàn thành khá). | <50.0: Rank B bị trao cho những màn chơi quá cẩu thả. |

### 2. Suspicion Meter Presentation & Feel Knobs (Tham số Cảm giác & Giao diện)

| Knob Name | Default | Safe Range | Unit | Category | Gameplay Effect | What Breaks Outside Range |
|---|---|---|---|---|---|---|
| `t_confirm` | 0.25 | [0.15, 0.40] | s | Feel | Thời gian ân hạn (grace window) khi accumulator chạm ngưỡng $T_{\text{entry}}$ trước khi Investigate cam kết. | <0.15 s: Vượt quá phản xạ người chơi, tạo cảm giác "bị nhìn thấy bất công"; >0.40 s: Người chơi lạm dụng nhấp nhô (peek exploit). |
| `t_meter_drain` | 1.0 | [0.5, 2.0] | s | Feel | Thời gian xả mượt hình ảnh trên UI (từ $100\%$ về $0\%$) khi mất tầm nhìn ngoài Chase (không trễ logic). | <0.5 s: Thanh đo biến mất quá giật cục; >2.0 s: Người chơi lầm tưởng mối đe doạ vẫn đang duy trì. |
| `r_threat_hysteresis` | 0.05 | [0.02, 0.10] | ratio | Feel | Chênh lệch tỷ lệ đe doạ tối thiểu cần thiết để hoán đổi dominant guard $g^*$. | <0.02: Mũi tên chevron và thanh đo nhấp nháy liên tục khi 2 lính ở gần nhau; >0.10: Chậm phản ứng với mối đe doạ mới nguy hiểm hơn. |
| `tau_threat_dwell` | 0.10 | [0.05, 0.25] | s | Feel | Thời gian hiển thị giữ tối thiểu cho một dominant threat trước khi cho phép hoán đổi. | Bảo đảm độ ổn định thị giác trên HUD WebGL/PC, chống rung lắc camera UI. |
| `forgiveness_floor` | 0.030 *(from registry)* | $[0.05 T_{\text{base}}, 0.20 T_{\text{base}})$ | ratio | Core | Sàn tha thứ LOS theo ràng buộc chuỗi F15 (đồng sở hữu với Perception #2). | $\ge T_{\text{floor}}$: Vi phạm bình đẳng F15, gây kẹt tích luỹ vĩnh viễn không thể reset sau vật cản. |
| `meter_display_scale` | 100.0 *(from registry)* | [100.0, 100.0] | scale | Core | Hệ số tỷ lệ hiển thị giá trị thanh đo. | Bắt buộc cố định để đồng bộ hiển thị UI phần trăm ($0–100\%$). |
| `grade_operator_schema_version` | 1 *(from registry)* | [1, 5] | version | Core | Phiên bản hợp đồng dữ liệu xuất bản của Room Grade Operator. | Phải khớp với bộ giải mã của Event Bus và Save System. |

## Visual/Audio Requirements

### 1. Visual Feedback & Shaders (Phản hồi Thị giác)

- **Thanh đo Suspicion Gauge (HUD Center-Bottom)**:
  - **Dải màu chuyển động (Color Morphing)**:
    - Vùng $A^* < T_{\text{entry}}^*$: Xanh lam khói mờ lạnh (Cold Blue-Grey, `#4A6984`), biểu thị sự chú ý mơ hồ.
    - Vùng $A^* \ge T_{\text{entry}}^*$ (Confirm Window): Nhấp nháy vàng hổ phách cảnh báo (Warning Amber, `#E5A93C`), chu kỳ xung nhịp $0.25\text{ s}$.
    - Vùng Chase Active: Đỏ cam rực lửa (Fiery Red-Orange, `#D94E34`), khoá đầy $100\%$ kèm hiệu ứng toả sáng viền (edge bloom).
  - **Điểm đánh dấu ngưỡng động (Sinking Threshold Pip)**:
    - Một vạch phân định trực quan (pip) trên thước đo tại vị trí $T_{\text{entry}}^*$.
    - Khi lính tích luỹ cảnh giác tàn dư $R$, vạch pip từ từ chìm dần từ $30\%$ xuống sát đáy sàn $6\%$ ($T_{\text{floor}}$), giải thích trực quan cho người chơi lý do lính nhạy cảm hơn.
  - **Mũi tên chỉ hướng đe doạ phẳng (Planar Azimuth Directional Chevron)**:
    - Mũi tên 2D xoay quanh bán kính elip tâm màn hình ($R_x = 240\text{ px}, R_y = 160\text{ px}$), tính toán theo góc phương vị cục bộ camera $\theta = \text{atan2}(x_{\text{cam}}, z_{\text{cam}})$.
    - Khi lính ở phía sau camera ($Z_{\text{cam}} < 0$), chevron trỏ chính xác về cạnh đáy màn hình tương ứng với góc tiếp cận thực tế, triệt tiêu hoàn toàn lỗi đảo góc.
    - Độ trong suốt (Alpha) tăng dần từ $0.2 \to 1.0$ đồng bộ theo tỷ lệ $A^* / T_{\text{chase}}$. Ẩn hoàn toàn khi $g^* == \text{null}$.
  - **Hiệu ứng viền màn hình (Chase Vignette)**:
    - Khi bước vào trạng thái Chase, một lớp viền mờ màu hổ phách đậm ở 4 góc màn hình nhấp nháy nhẹ theo nhịp rượt đuổi, báo hiệu không gian an toàn bị xâm nhập.
  - **Hỗ trợ tiếp cận (Accessibility & Colorblind Support)**:
    - Không dựa đơn thuần vào màu sắc: Kèm ký hiệu hình học phụ trợ (biểu tượng mắt mở hờ $\to$ mắt mở to $\to$ dấu chấm than rực sáng) và tỷ lệ phần trăm số học.
    - Cung cấp bảng màu Colorblind tuỳ chọn (Protanopia / Deuteranopia chuyển sang Vàng / Xanh dương tương phản cao).

### 2. Audio Design & Cues (Thiết kế Âm thanh)

- **Phân tách Diegetic vs Non-Diegetic**:
  - Âm thanh Suspicion Meter là âm thanh phi thế giới (non-diegetic, vang lên trong nhận thức người chơi), không làm lính chú ý và không xung đột với âm thanh bước chân.
- **Bảng âm thanh theo trạng thái**:
  - **Accumulating ($0 < A^* < T_{\text{entry}}^*$)**: Tiếng vo ve điện từ trầm (low-frequency drone, 60–90 Hz), cao độ tăng dần tịnh tiến theo $A^*$.
  - **Confirming Window ($A^* \ge T_{\text{entry}}^*$)**: Tiếng nhịp tim đập dồn dập (Heartbeat thump, 120 BPM) kết hợp tiếng kim loại rít nhẹ tạo cảm giác nghẹt thở.
  - **Chase Stinger**: Tiếng chuông báo động kim loại sắc nhọn (brass stinger) đánh dấu chuyển trạng thái FSM, chuyển sang lớp nhạc rượt đuổi dồn dập.
  - **LOS Break / Gauge Drain**: Tiếng xả hơi nhẹ (soft air release / descending filter sweep), báo hiệu nguy hiểm đã qua và người chơi đã cắt đuôi tầm nhìn thành công.

## UI Requirements

### 1. In-Game HUD Suspicion Widget (Giao diện Trong Trận)
- **Vị trí & Bố cục**:
  - Đặt cố định ở vùng dưới tâm màn hình (Center-Bottom, offset $Y = -180\text{ px}$ so với tâm màn hình 1080p), bảo đảm tầm nhìn người chơi tập trung vào môi trường mà không bị che khuất.
  - Kích thước: Rộng $260\text{ px}$, cao $14\text{ px}$, bo góc nhẹ $4\text{ px}$.
- **Các thành phần giao diện**:
  - `MeterBarFill`: Thanh trượt tiến độ shader đổi màu theo 3 vùng (Lam khói $\to$ Hổ phách $\to$ Đỏ cam).
  - `ThresholdPipMarker`: Con trỏ hình tam giác nhỏ phía trên thanh đo, chỉ chính xác vị trí $T_{\text{entry}}^*$ đang dao động.
  - `PercentageLabel`: Nhãn phần trăm số học ($0–100\%$) đặt phía bên phải thanh đo với font số monospace dễ đọc.
  - `DirectionalChevron`: Mũi tên 2D cách tâm màn hình theo elip ($240 \times 160\text{ px}$), xoay theo góc azimuth của dominant guard $g^*$.
- **Tối ưu hoá Kỹ thuật (WebGL & Zero-Allocation Game Loop)**:
  - Trình phát sự kiện trong `SuspicionMeterPresenter.cs` sử dụng event delegate tĩnh hoặc interface implementation, tuyệt đối loại bỏ lambda closures để đạt mức phân bổ rác $0\text{ bytes/frame}$ (0 allocs/sec) trong luồng 60 FPS WebGL.
  - Tất cả các thành phần UI thuộc 1 Canvas duy nhất, sử dụng chung 1 texture atlas và shader UI tuỳ chỉnh để giữ số lượng draw call $\le 2$.

### 2. End-of-Segment Grade Card (Thẻ Điểm Tổng Kết Phân Đoạn)
- **Thời điểm xuất hiện**:
  - Kích hoạt ngay khi người chơi tương tác thành công với cửa thoát của phân đoạn (`Segment-clear`), làm mờ nhẹ nền game phía sau ($60\%$ dark blur).
- **Cấu trúc Thẻ điểm (Modal Dialog)**:
  - **Huy hiệu Phân hạng (Grade Stamp)**: Chữ cái lớn ở vị trí trung tâm trên cùng (**S**, **A**, **B**, **C**, hoặc **FAIL**) với hiệu ứng đóng dấu kim loại (stamp animation) kèm âm thanh gõ búa nặng.
  - **Điểm số Tổng hợp**: Hiển thị điểm số chất lượng lớn: ví dụ `75.0 / 100.0`.
  - **Bảng phân tích khấu trừ (Deduction Breakdown Table)**:
    - *Điểm cơ sở ban đầu (Base Score)*: `+100.0`
    - *Leo thang rượt đuổi toàn phòng (Chase Escalations)*: `-25.0` (Số lần: $\sum N_{\text{chase}}$)
    - *Cảnh giác tàn dư phòng (Residual Wariness)*: `-4.5` (Dựa trên snapshot $R_{\text{final}}$)
    - *Tổng điểm phạt thực tế*: `-29.5` (Áp dụng trần cap: tối đa $-90.0$)
  - **So sánh Kỷ lục (Historical Comparison)**: Hiển thị dòng trạng thái: `Kỷ lục trước đó: B (65.0) -> Kỷ lục mới: A (75.0)!`.
  - **Điều hướng & Nút bấm**:
    - Nút *Tiếp tục (Continue)*: Phím tắt `Space` / `Enter` hoặc click chuột.
    - Nút *Chơi lại (Retry Segment)*: Phím tắt `R` hoặc click chuột (dành cho người chơi muốn chinh phục Rank S sạch).

## Acceptance Criteria

#### AC-1: Dominant Threat Ratio & Multi-Tier Tie-Breaking (Formula D1)
- **Given** an active segment with two alertable guards $g_1$ and $g_2$, where $g_1$ has $A_1 = 0.18, T_{\text{entry}, 1} = 0.30$ ($r_1 = 0.60$) and $g_2$ has $A_2 = 0.12, T_{\text{entry}, 2} = 0.18$ ($r_2 = 0.667$).
- **When** the Suspicion Meter updates dominant threat selection.
- **Then** $g_2$ is selected as dominant guard $g^*$ ($0.667 > 0.60$). If $r_1 == r_2$, the tie is broken deterministically by: (1) higher $A_i$, (2) smaller $d_{\text{Euclid}}(g_i, \text{player})$, (3) ordinal string comparison $\min(\text{guard\_eid}_1, \text{guard\_eid}_2)$. If $\max(A_i) == 0.0$, $g^*$ is null and the chevron is hidden.

#### AC-2: Meter Normalized Ratio, Percentage Readout & Region Enum (Formula D2)
- **Given** dominant guard $g^*$ with $T_{\text{chase}} = 1.00, T_{\text{entry}}^* = 0.18, \text{MeterDisplayScale} = 100.0$.
- **When** $A^*$ is evaluated at values $0.00, 0.09, 0.245$, and $1.00$ (or guard state transitions to `"Chase"`).
- **Then**:
  - At $A^* = 0.00$: $\text{MeterRatio} = 0.0$, $\text{MeterPercent} = 0$, $\text{Region} = \text{SuspicionMeterRegion.Quiet}$.
  - At $A^* = 0.09$: $\text{MeterRatio} = 0.09$, $\text{MeterPercent} = 9$, $\text{Region} = \text{SuspicionMeterRegion.Quiet}$.
  - At $A^* = 0.245$: $\text{MeterRatio} = 0.245$, $\text{MeterPercent} = 25$ (rounded away from zero), $\text{Region} = \text{SuspicionMeterRegion.Investigate}$.
  - At $A^* = 1.00$ or state `"Chase"`: $\text{MeterRatio} = 1.0$, $\text{MeterPercent} = 100$, $\text{Region} = \text{SuspicionMeterRegion.Chase}$.

#### AC-3: Dynamic Investigate Entry Threshold & F15 Floor Chain (Formula D3)
- **Given** $T_{\text{base}} = 0.30, k_{\text{res}} = 0.24, T_{\text{floor}} = 0.060$, and $\text{forgiveness\_floor} = 0.030$.
- **When** residual wariness $R$ spans $[0.0, 1.0]$.
- **Then**:
  - At $R = 0.0$: $T_{\text{entry}} = 0.30$.
  - At $R = 0.75$: $T_{\text{entry}} = \max(0.30 - 0.24 \times 0.75, 0.060) = 0.120$.
  - At $R = 1.0$: $T_{\text{entry}} = \max(0.30 - 0.24 \times 1.0, 0.060) = 0.060 \equiv T_{\text{floor}}$.
  - For all $R \in [0.0, 1.0]$, the forgiveness margin $\text{margin}_{\text{forgive}} = T_{\text{entry}}(R) - \text{forgiveness\_floor} \ge 0.030 > 0.0$ holds strictly.

#### AC-4: Confirm Window Grace & Virtual Tick Boundary Race (Section C2, Edge Case E2)
- **Given** dominant guard $g^*$ reaches $A^* \ge T_{\text{entry}}^*$ entering the `Confirming` state with window $t_{\text{confirm}} = 0.25\text{ s}$.
- **When** line-of-sight (LOS) breaks at simulation time $t \le t_{\text{confirm}}$ (including the exact boundary tick at $0.25\text{ s}$).
- **Then** the escalation to `Investigate` is cancelled; guard $g^*$ remains in `Patrol`, $A^*$ resets to $0.0$, and the presenter transitions to `Resetting` without logging an incident. Escalation commits if and only if LOS is maintained continuously for $t > t_{\text{confirm}}$.

#### AC-5: Chase Lock & Telemetry Inertia (Section C3, State Machine)
- **Given** any guard emits a verified `Chase-entry` event.
- **When** the Suspicion Meter enters `PursuitLocked` state.
- **Then** the presenter locks $\text{MeterRatio} = 1.0$ ($100\%$), directional chevron locks to the chaser, and accumulator $A$ and residual $R$ freeze (strictly inert). Upon receiving `Chase-end`, the meter exits pursuit lock, $A$ resets to $0.0$, and $R$ resumes decay.

#### AC-6: Global Incident Penalty Pooling & Room Deduction Cap (Formulas D4, D6)
- **Given** a room completion boundary with no capture ($N_{\text{capture}} = 0$), $W_{\text{chase}} = 25.0$, and $\text{grade\_penalty\_cap} = 90.0$.
- **When** the room trace records $N_{\text{chase}} = 4$ across guards ($4 \times 25.0 = 100.0$) and $P_{\text{residual, room}} = 5.0$.
- **Then** $P_{\text{incident, room}} = \min(90.0, 100.0) = 90.0\text{ pts}$, total penalty is clamped to $P_{\text{total}} = \min(90.0, 90.0 + 5.0) = 90.0\text{ pts}$, and the resulting room quality score is $S_{\text{room}} = \max(10.0, 100.0 - 90.0) = 10.0\text{ pts}$, mapping to `RoomGrade.NeedsImprovement`.

#### AC-7: Terminal Capture Failure Override (Formula D8, Edge Case E3)
- **Given** a session where the player is captured (`isCapture = true` or aggregate `CaptureCount > 0`), regardless of whether prior score was $\ge 90.0$.
- **When** `RoomGradeOperator.FinalizeRoom` is executed.
- **Then** `CompletionStatus` is strictly `GradeCompletionStatus.Failed`, `QualityScore` is $0.0$, and `Grade` is strictly `RoomGrade.None` (Rank F).

#### AC-8: Normalized Final Residual Penalty (Formula D5)
- **Given** a successfully cleared room with $R_{\text{final}} = 0.45, R_{\max} = 1.00$, and $W_{\text{residual}} = 10.0$.
- **When** the residual penalty is evaluated.
- **Then** $R_{\text{norm}} = \text{clamp}(0.45 / 1.00, 0.0, 1.0) = 0.45$, yielding $P_{\text{residual}} = 10.0 \times 0.45 = 4.5\text{ pts}$. If $R_{\text{final}} = 0.0$, $P_{\text{residual}} = 0.0\text{ pts}$.

#### AC-9: Multi-Guard Exposure-Weighted Residual Aggregation (Formula D5, D7)
- **Given** a completed room with Guard 1 ($w_1 = 3.0, R_{\text{final}, 1} = 0.20 \implies P_{\text{residual}, 1} = 2.0$) and Guard 2 ($w_2 = 1.0, R_{\text{final}, 2} = 0.0 \implies P_{\text{residual}, 2} = 0.0$), with zero Chase escalations.
- **When** `RoomGradeOperator.FinalizeRoom` aggregates the room score.
- **Then** $W_{\text{total}} = 4.0$, room residual penalty is $P_{\text{residual, room}} = \frac{3 \times 2.0 + 1 \times 0.0}{4.0} = 1.50\text{ pts}$, and final score is $S_{\text{room}} = 100.0 - 1.50 = 98.5\text{ pts}$ (mapping to `RoomGrade.S`).

#### AC-10: Witnessed HideSpot Dive Incident Accounting (Section C4, Edge Case E8)
- **Given** a player executes a dive into a HideSpot witnessed by a guard (`witnessed hide-spot dive` with `carve_out = true`).
- **When** the episode is recorded into the grade aggregate trace.
- **Then** if the player successfully evades, the dive bills as exactly one Chase escalation ($N_{\text{chase}} += 1, -25.0\text{ pts}$). If caught at the spot, the room evaluates as `Failed` with Score $0.0$ and Rank F, correlating the dive to the terminal capture under the same `entry_id`.

#### AC-11: Clean Distraction Exemption (Section C5, Formula D4)
- **Given** a player deploys a Burst noise distraction tool, drawing a guard into an investigation that resolves with no LOS contact.
- **When** the room is finalized.
- **Then** the investigation incurs $0.0\text{ pts}$ direct incident penalty on the Grade Card ($W_{\text{fruitless}} \equiv 0.0$).

#### AC-12: Distraction Residual Seeding Invariant
- **Given** a guard conducts a noise investigation triggered by a Burst distraction.
- **When** the guard investigates without detecting the player.
- **Then** the guard accumulates authoritative residual wariness $R$ in Perception, lowering their $T_{\text{entry}}$ threshold, but contributing $0.0$ direct score deductions to $P_{\text{incident, room}}$.

#### AC-13: Missing/Corrupt Telemetry Resilience (Edge Case E4, ADR-0004)
- **Given** a snapshot or completion boundary where required values (`ACurrent`, `TChase`, or `finalResidualByGuard`) are missing, null, `float.NaN`, or `float.PositiveInfinity`.
- **When** processed by `SuspicionMeterCalculator` or `RoomGradeOperator`.
- **Then** the meter returns `SuspicionMeterRegion.Unavailable` (never fabricating $0\%$), and `RoomGradeOperator` produces `GradeCompletionStatus.Unresolved` with `RoomGrade.None` and zero score.

#### AC-14: Zero-Guard Sanctuary Room Clear (Edge Case E6, Formula D7)
- **Given** a completed room segment containing zero active guards ($|G_{\text{active}}| == 0, M = 0$).
- **When** the player crosses `RoomCompletionBoundary` with valid completion flags.
- **Then** the meter presents `Quiet` with gauge fill $0\%$ and hidden chevron, and the Grade Operator awards $S_{\text{room}} = 100.0\text{ pts}$ with `RoomGrade.S` and status `Completed`.

#### AC-15: Threat Hysteresis Margin & Dwell Window (Edge Case E1, Tuning Knobs)
- **Given** guard $g_1$ is currently dominant $g^*$, and guard $g_2$ has $r_2 > r_1$ by margin $\delta r = r_2 - r_1$.
- **When** the frame updates within dwell window $\tau_{\text{dwell}} = 0.10\text{ s}$ or with $\delta r < 0.05$.
- **Then** dominant selection retains $g_1$; $g^*$ switches to $g_2$ if and only if $\delta r \ge 0.05$ and dwell time has elapsed, or if $g_2$ commits to `Investigate` or `Chase`.

#### AC-16: Simulation-Time Clock Invariance on Game Pause (Edge Case E5)
- **Given** the Suspicion Meter is in `Confirming` or `Resetting` state with active timers.
- **When** the game is paused ($Time.timeScale == 0.0f$).
- **Then** all internal elapsed timers ($t_{\text{elapsed}}$, $t_{\text{confirm}}$, $t_{\text{meter\_drain}}$) freeze completely, resuming without time drift or state skipping when $Time.timeScale$ returns to $1.0f$.

#### AC-17: Concurrent Multi-Guard Chase Escalations (Edge Case E7)
- **Given** two guards $g_1$ and $g_2$ simultaneously reach Chase threshold in the exact same tick.
- **When** the trace reducer processes the tick events.
- **Then** both $g_1$ and $g_2$ receive independent $N_{\text{chase}} = 1$ ($25.0\text{ pts}$ each, totaling $50.0\text{ pts}$ before cap), while the HUD Suspicion Meter locks its directional chevron to the guard with the smaller Euclidean distance $d_{\text{Euclid}}(g_i, \text{player})$.

#### AC-18: Trace Event Idempotency & Deduplication with guard_eid (ADR-0004)
- **Given** an event trace stream that re-delivers duplicate records with identical `(session_id, attempt_epoch, entry_id, guard_eid, event_type)` or records from a stale `attempt_epoch`.
- **When** ingested by `GradeTraceAggregate`.
- **Then** duplicate records and stale-epoch records are discarded; records with matching `entry_id` but distinct `guard_eid` are both retained as distinct incidents.

#### AC-19: Planar Azimuth Projection Continuity (Section C1, Edge Case E9)
- **Given** a dominant threat guard $g^*$ positioned behind the camera plane ($Z_{\text{cam}} = -5.0\text{ m}, X_{\text{cam}} = -2.0\text{ m}$).
- **When** the presenter calculates the directional chevron screen coordinate.
- **Then** azimuth angle evaluates to $\theta = \text{atan2}(-2.0, -5.0) \approx -1.976\text{ rad}$, placing the chevron along the lower-left perimeter of the ellipse ($Y_{\text{screen}} < 0, X_{\text{screen}} < 0$) without coordinate inversion or screen flipping.

#### AC-20: Rank S Zero-Chase Gate Invariant (Formula D8)
- **Given** a completed 3-guard room where Guard 1 incurs $N_{\text{chase}} = 1$ ($25.0\text{ pts}$) and zero residual, while Guards 2 and 3 incur zero penalties, yielding $S_{\text{room}} = 100.0 - 25.0 = 75.0\text{ pts}$ (or hypothetically $S_{\text{room}} \ge 90.0$).
- **When** `RoomGradeOperator.CalculateGrade` evaluates letter rank.
- **Then** because $\sum N_{\text{chase}} = 1 > 0$, Rank S is strictly disqualified; the attempt is awarded `RoomGrade.A`.

## Open Questions

### OQ-1: Nhận thức Không gian Đa Lính Ngoài Màn hình (Multi-Guard Off-Screen Awareness)
- **Vấn đề**: HUD Suspicion Meter chỉ hiển thị 1 lính thống trị ($g^*$) để bảo vệ ngân sách draw-call WebGL và giữ màn hình thông thoáng. Liệu người chơi có cảm thấy bất công nếu một lính thứ hai bất ngờ tiếp cận từ phía sau trong khi đang theo dõi lính thứ nhất ở phía trước?
- **Định hướng Giải quyết**: Trong phạm vi MVP, hệ thống âm thanh không gian đa chiều (3D spatial audio: tiếng bước chân, tiếng rè đèn pin) đóng vai trò cảnh báo lính phụ ngoài màn hình. Sau đợt playtest MVP đầu tiên, nếu người chơi cảm thấy khó phán đoán, UI có thể thử nghiệm thêm các chấm nhỏ phụ (secondary mini-pips) mờ nhạt trên đường tròn chevron.

### OQ-2: Thời gian Vượt màn vs Tính điểm Lén lút Thuần tuý (Speedrun Time Bonus)
- **Vấn đề**: Thẻ điểm Grade Card có nên thưởng điểm hoặc cộng thời gian hoàn thành (Speedrun Time) vào điểm tổng $S_{\text{room}}$ không?
- **Định hướng Giải quyết**: Không đưa thời gian vào công thức tính điểm của MVP để giữ vững Pillar 2 (Fair Mind-Challenge) và tránh thúc ép người chơi lao vội vã. Thời gian hoàn thành sẽ hiển thị như một chỉ số thống kê phụ trợ (secondary vanity stat) trên Grade Card mà không ảnh hưởng đến Rank S/A/B/C.
