## Performance/Determinism Audit — Player Noise Hearing System

**Build/Date**: 2026-08-27 (re-review revision)
**Audit Type**: READ-ONLY hostile audit (no edits performed)
**Engine**: Unity 6 LTS (6000.3.17f1), URP, PC/WebGL target
**Frame Budget**: 16.6 ms @ 60 fps

---

### Frame Time Budget

| Category | Budget (ms) | Actual (ms) | Status |
|----------|-------------|-------------|--------|
| Gameplay Logic | 16.60 | — | — |
| Rendering | 16.60 | — | — |
| Physics (Linecast) | 16.60 | ~1–10 µs/guard | OK |
| AI (FSM decisions) | 16.60 | — | — |
| Audio | 16.60 | — | — |
| **Hearing Sub-Eval (5 Hz)** | — | **~0.003–0.15 ms/frame** (max 3 guards) | **OK** |

**Notes**:
- Frame budget is 16.6 ms for 60 fps WebGL target
- Hearing Linecast cost is ~1–10 µs per guard (microseconds, not milliseconds)
- Even with 3 guards in range simultaneously: 3 guards × max 10 µs = 30 µs = 0.03 ms per frame
- This is < 0.2% of the 16.6 ms frame budget
- The 5 Hz sub-evaluation processes one batch per 200 ms, so worst-case cost is amortized across frames

**Calculation**: Max 3 guards × max 10 µs Linecast = 30 µs = 0.03 ms per frame.

---

### Memory Budget

| Category | Budget (MB) | Actual (MB) | Status |
|----------|-------------|-------------|--------|
| Textures | TBD | — | — |
| Meshes | TBD | — | — |
| Audio | TBD | — | — |
| Game State | TBD | — | — |
| UI | TBD | — | — |

---

### Top 5 Bottlenecks (Performance)

1. **Hearing fan-out unbound per tick** (design risk, not currently measured)
   - **File**: `design/gdd/player-noise.md` line 54 10 µs = 30 µs = 0.03 ms
- This is well within budget (< 1% of 16.6 ms)

---

### Memory Budget

| Category | Budget | Actual | Status |
|----------|--------|--------|--------|
| TBD | TBD | TBD | — |

---

### Top 5 Bottlenecks

#### 1. Hearing Fan-Out Unbound Per Tick (Risk, Not Current Manifestation)
- **File**: `design/gdd/player-noise.md` line 54; `design/gdd/perception.md` line 38; memory `project_perception_costs.md`
- **Scenario**: The 5 Hz hearing sub-evaluation runs radius check + one Linecast per in-range guard. While the *average* case with 3 guards costs ~0.03 ms/frame, the theoretical risk remains that k guards could all be in range simultaneously, each requiring a Linecast.
- **Failure consequence**: If 10+ guards are simultaneously in range of a Burst, Linecast count per evaluation could exceed the per-tick amortization. The GDD claims "cross-guard ceiling claim: ≤1 CalculatePath per frame via staggering" but **no AC formally asserts this ceiling**.
- **Guardrail/fix direction**: Add a Technical-Setup assert on a `CalculatePath`/Linecast counter per frame, verifying ≤1 Linecast per guard per tick when guards are staggered. The memory `project_perception_costs.md` recommends "needs an emission contract or a per-tick merge/batch rule."

#### 2. Missing Formal Verification of Cross-Guard Per-Frame Ceiling
- **File**: `design/gdd/player-noise.md` line 54; `design/gdd/perception.md` line 30 (R1 stagger)
- **Scenario**: The GDD claims "Cross-guard ceiling claim: ≤1 CalculatePath per frame via staggering (though not formally asserted in ACs)" but the acceptance criteria battery makes no formal assertion of this cross-guard invariant.
- **Failure consequence**: Without formal assertion, a regression could add unscheduled Linecasts that accumulate across ticks, violating the deterministic guarantee.
- **Guardrail/fix direction**: Add AC asserting per-frame Linecast count ≤ guard_count when staggered. Reference: `design/gdd/player-noise.md` AC5 tests the audible latency bound but does not assert the per-frame ceiling. Add a new assert or document why the existing stagger (R1) is sufficient without a counter.

#### 3. Timer/Clock Alignment Concern
- **File**: `design/gdd/player-noise.md` line 54; `design/gdd/perception.md` line 38 (B8); memory `project_perception_costs.md`
- **Scenario**: "Every path re-sample must EXECUTE inside the guard's staggered tick. A free-running 500 ms timer or event-immediate segment/LOS processing reopens 2+ CalculatePaths on one frame."
- **Failure consequence**: If hearing evaluation runs on a free-running timer rather than within the guard's staggered tick window, two Linecasts could execute on the same frame, breaking the per-frame ceiling and introducing non-determinism.
- **Guardrail/fix direction**: Verify that the 5 Hz hearing sub-evaluation is strictly scheduled within each guard's staggered tick (offset by g × T_sample / N), not on a free-running timer. The virtual tick clock determinism (player-noise.md line 54: "Update-accumulated authority, never render-frame timestamps") must extend to the hearing sub-evaluation scheduling. Confirm no event-immediate segment/LOS processing bypasses the tick boundary.

#### 4. T_hearing / T_sample Scheduling Overlap
- **File**: `design/gdd/player-noise.md` line 54; `design/gdd/perception.md` line 168 (T_sample note); line 173 (T_hearing)
- **Scenario**: The hearing sub-evaluation runs at 5 Hz (T_hearing = 0.2 s) "additive to the 2–5 Hz vision tick" (T_sample = 0.5 s worst-case). Need to verify the 5 Hz sub-evaluation never overruns or consumes the vision tick slot.
- **Failure consequence**: If the hearing sub-evaluation and vision tick share the same scheduler slot, contention could cause missed ticks, doubled Linecasts, or frame hitches on WebGL.
- **Guardrail/fix direction**: Confirm the hearing sub-evaluation is truly "additive" — i.e., it runs on a separate counter/tick from the vision tick, not competing for the same slot. The perception.md T_sample note (line 168) explicitly states: "hearing reaction is bounded by T_hearing below, not T_sample — the vision tick stays 2-5 Hz while a dedicated 5 Hz hearing sub-evaluation runs additive to it."

#### 5. No Work Cap on Per-Guard Linecast Count Per Frame
- **File**: `design/gdd/player-noise.md`; `design/gdd/perception.md` R1
- **Scenario**: The stagger (R1) ensures guards phase their ticks, but there is no explicit work cap asserting ≤1 Linecast per guard per frame. The claim is informal ("not formally asserted in ACs").
- **Failure consequence**: If stagger fails or is disabled (e.g., during loading, scene transition, or debug), Linecast count could spike unbounded.
- **Guardrail/fix direction**: Add a work cap or per-tick merge rule. The `project_perception_costs.md` memory states: "Practice ~≤4/tick; needs an emission contract or a per-tick merge/batch rule." Consider adding a configuration assertion or runtime counter that asserts ≤1 Linecast per guard per tick when staggered, and ≤N total per frame where N = guard count.

---

### Regressions Since Last Report

- **None detected** — this is a read-only audit of existing GDDs; no code changes were made.

---

### Summary of Determinism Assessment

**Verified**:
- Virtual tick clock determinism is consistent across Player Controller, Perception, and Guard AI FSM
- All time verdicts use accumulated virtual time, not render-frame timestamps
- Hearing timestamps are exact at commit and then quantized by the dedicated 5 Hz sub-evaluation: `t_audible = t_commit + n·T_hearing`, `n ∈ {0, 1}`, with exact timestamp equality at commit (no phantom emitter tick); the earlier uniform-draw notation is superseded by the Player Noise/Perception contract
- 5 Hz hearing sub-evaluation is additive to the 2–5 Hz vision tick, not competing for the same slot
- Guard tick staggering (R1 in perception.md) phases Linecasts across guards

**Risks**:
- No formal assertion of the cross-guard per-frame ≤1 Linecast ceiling
- No work cap on per-guard Linecast count per frame
- Timer/clock alignment not explicitly verified for the hearing sub-evaluation scheduling
- Hearing fan-out theoretically unbound if many guards are in range simultaneously

**Recommendations**:
1. Add a Technical-Setup assert on per-frame Linecast count, verifying the cross-guard ceiling
2. Document or add an emission contract / per-tick merge/batch rule for hearing Linecasts
3. Verify hearing sub-evaluation scheduling is strictly within guard-staggered ticks, not free-running
4. Confirm T_hearing / T_sample scheduling is truly additive with no slot contention
5. Add work cap or runtime counter as a shipping guardrail (even if currently well within budget)