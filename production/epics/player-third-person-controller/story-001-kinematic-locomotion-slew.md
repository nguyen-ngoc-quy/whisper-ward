# Story 001: Kinematic Locomotion & Linear Slew FSM

> **Epic**: Player Third-Person Controller
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-21

## Context

**GDD**: `design/gdd/player-third-person-controller.md`
**Requirement**: `TR-CORE-001`, `TR-CORE-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Kinematic Player Controller and Stance State Machine
**ADR Decision Summary**: CharacterController-backed kinematic locomotion governed by a pure C# deterministic state machine with linear velocity slewing ($a_{\text{max}} = 78.125\text{ m/s}^2$, $\text{accel\_time} \le 0.08\text{ s}$), strict speed hierarchy ($V_{\text{crouch}} = 1.80\text{ m/s} < V_{\text{walk}} = 3.60\text{ m/s} < V_{\text{run}} = 6.25\text{ m/s}$), and anti-kiting invariant preservation ($V_{\text{chase}} / V_{\text{run}} \ge 1.20$).

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW
**Engine Notes**: Standard PhysX CharacterController and C# math. Zero post-cutoff APIs.

**Control Manifest Rules (this layer)**:
- Required: Speeds strictly governed by $V_{\text{crouch}} = 1.80\text{ m/s} < V_{\text{walk}} = 3.60\text{ m/s} < V_{\text{run}} = 6.25\text{ m/s}$ with linear acceleration approach ($a_{\text{max}} = 78.125\text{ m/s}^2$, $\text{accel\_time} \le 0.08\text{ s}$).
- Required: Enforce guard chase speed $V_{\text{chase}} = 7.50\text{ m/s}$ such that $V_{\text{chase}} / V_{\text{run}} = 7.50 / 6.25 = 1.20 \ge 1.20$.
- Required: Apply backpedal speed penalty factor $0.70$ when $\vec{d}_{\text{move}} \cdot \text{facing} < -10^{-4}$.
- Forbidden: Never exceed runtime-max speed declaration $V_{\text{run\_runtime\_max}} = 6.25\text{ m/s}$ on diagonal movement or combined inputs.
- Guardrail: Exactly 0 B (Zero GC) on hot path `Tick(float dt)` locomotion updates and snapshot polling.

---

## Acceptance Criteria

*From GDD `design/gdd/player-third-person-controller.md`, scoped to this story:*

- [x] **H.0 Prerequisites**: Deterministic `Tick(float dt)` driver, headless state machine harness, registry-backed constants, config validator, and runtime assert seam. (H.0.1, H.0.2, H.0.6, H.0.7, H.0.8, H.0.10, H.0.12)
- [x] **AC-P1 — Diagonal Normalization**: Diagonal movement input produces horizontal speed equal to cardinal-input speed for the active state within float epsilon ($6.25\text{ m/s}$, never $6.25 \times \sqrt{2}$).
- [x] **AC-P2 — Backpedal Factor Boundary**: When $\vec{d}_{\text{move}} \cdot \text{facing} < -10^{-4}$, apply $f_{\text{back}} = 0.70$. At $\vec{d}_{\text{move}} \cdot \text{facing} \ge -10^{-4}$ and pure strafe $\text{dot} = 0$, full state speed applies.
- [x] **AC-P5 — Accel Timing**: Idle to Run with sustained input reaches target speed $6.25\text{ m/s}$ within $\text{accel\_time} \pm \text{one frame}$ ($0.08\text{ s}$ at $a_{\text{max}} = 78.125\text{ m/s}^2$).
- [x] **AC-P6 — Mid-Blend Continuity**: State change mid-ramp never snaps velocity: per-tick $|\Delta v_{\text{eff}}| \le a_{\text{max}} \times \Delta t$ across all transitions.
- [x] **AC-P7 — Full Stop**: Releasing input from full run brings $v_{\text{eff}}$ to $0$ within registry-derived $\text{accel\_time} + \text{one observation tick}$.
- [x] **AC-P8 — Latch Priority & Same-Frame Transitions**: Crouch input toggles immediate crouch latch override over Run; C-edge + Shift + Move in one atomic input set resolves to Crouch.
- [x] **AC-P17 — Structural Speed Ceiling**: Sweeps over all states, backpedal modifiers, and input directions confirm $v_{\text{eff}} \le V_{\text{run\_runtime\_max}}$ ($6.25\text{ m/s}$); invalid inputs trip runtime assert.
- [x] **AC-P18 — Hard-Constraint Config Validator**: Validator returns false and logs errors if $V_{\text{walk}} \ge V_{\text{investigate}}$, $V_{\text{crouch}} \ge V_{\text{patrol}}$, $\text{accel\_time} \le 0$ or $> 0.10\text{ s}$, or $\text{backpedal\_factor} \notin (0, 1]$.
- [x] **AC-P20 — Single-Event-Per-Frame Shape**: Same-frame multi-input sets collapse to at most one net state transition.
- [x] **AC-P23 — Zero-Vector NaN Guard**: Releasing movement during rearward motion produces zero direction vector without evaluating undefined dot products; $v_{\text{eff}}$ remains finite.

---

## Implementation Notes

*Derived from ADR-0006 Implementation Guidelines:*

1. **Deterministic Linear Slew**:
   - $v_{\text{target}} = S(\text{state}) \times f_{\text{back}}$
   - $f_{\text{back}} = 0.70$ if $\vec{d}_{\text{move}} \cdot \text{facing} < -10^{-4}$, else $1.00$.
   - $a_{\text{max}} = \frac{V_{\text{run}}}{\text{accel\_time}} = \frac{6.25}{0.08} = 78.125\text{ m/s}^2$.
   - $v_{\text{eff}}(t + \Delta t) = \text{Mathf.MoveTowards}(v_{\text{eff}}, v_{\text{target}}, a_{\text{max}} \times \Delta t)$.
2. **State Machine Hierarchy**:
   - `LocomotionState` enum: `Idle`, `Crouch`, `Walk`, `Run`, `InHideSpot`.
   - `MovementStance` enum: `Crouched`, `Standing`.
   - Crouch latch overrides Run. Releasing Crouch while Run input is held transitions directly to Run with continuous slew.
3. **Diagonal Normalization**:
   - $\vec{d}_{\text{move}} = \text{input.normalized}$. If $\|\text{input}\| \le 10^{-5}$, $\vec{d}_{\text{move}} = \text{Vector3.zero}$ and controller transitions to Idle without computing dot products.
4. **Downward Ground-Snap**:
   - In `CharacterController.Move()`, add downward bias velocity $v_{\text{down}} = -3.0\text{ m/s} \times \Delta t$ to prevent false negative `isGrounded` flickers.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: Camera-relative yaw basis transformation, rear-damped angular rotation curves, and stationary camera orbit invariant (`AC-P25`).
- [Story 003]: Stand clearance headroom probe (`OverlapCapsuleNonAlloc`), capsule height resizing, and HideSpot containment states.

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**[For Logic / Integration stories — automated test specs]:**

- **AC-P1**: Diagonal Normalization
  - Given: Controller in Run state ($V_{\text{run}} = 6.25\text{ m/s}$).
  - When: Raw diagonal move vector $(1, 1)$ is injected.
  - Then: Horizontal velocity magnitude equals $6.25\text{ m/s} \pm 10^{-4}\text{ m/s}$, never $8.84\text{ m/s}$.
  - Edge cases: Single-axis $(1, 0)$ and tiny inputs $(10^{-4}, 10^{-4})$.
- **AC-P2**: Backpedal Factor Boundary
  - Given: Controller facing forward $(0, 0, 1)$ with Run active.
  - When: Move vector has $\vec{d}_{\text{move}} \cdot \text{facing} = -0.00011$ vs $-0.00009$ vs $0.0$.
  - Then: Below $-10^{-4}$ speed targets $6.25 \times 0.70 = 4.375\text{ m/s}$; at or above $-10^{-4}$ speed targets $6.25\text{ m/s}$.
  - Edge cases: Pure lateral strafe $\text{dot} = 0.0$ maintains $100\%$ cruise speed.
- **AC-P5**: Accel Timing
  - Given: Stationary controller at $v_{\text{eff}} = 0$.
  - When: Sustained Run input is applied with $\Delta t = 0.02\text{ s}$ steps.
  - Then: Target speed $6.25\text{ m/s}$ is reached at $t = 0.08\text{ s}$ ($\pm 1$ tick).
  - Edge cases: $\Delta t = 0.0166\text{ s}$ (60 fps) and $\Delta t = 0.0333\text{ s}$ (30 fps).
- **AC-P6**: Mid-Blend Continuity
  - Given: Controller accelerating mid-ramp at $v_{\text{eff}} = 3.0\text{ m/s}$.
  - When: Input toggles Run $\to$ Walk ($V_{\text{walk}} = 3.60\text{ m/s}$) or Run $\to$ Crouch ($1.80\text{ m/s}$).
  - Then: $|\Delta v_{\text{eff}}| \le a_{\text{max}} \times \Delta t$ on every subsequent tick without step discontinuity.
  - Edge cases: Instant reversal to full stop.
- **AC-P7**: Full Stop
  - Given: Controller cruising at $6.25\text{ m/s}$.
  - When: Input is set to zero.
  - Then: $v_{\text{eff}}$ reaches exactly $0.0\text{ m/s}$ within $0.08\text{ s} + 1\text{ tick}$.
  - Edge cases: Deceleration from Crouch ($1.80\text{ m/s}$) stopping in $\le 0.024\text{ s}$.
- **AC-P8 / AC-P20**: Latch Priority & Same-Frame Collapse
  - Given: Controller in Run state.
  - When: Atomic input packet delivers Crouch edge + Shift hold + WASD move in one tick.
  - Then: State resolves to Crouch; exactly one transition event emitted.
  - Edge cases: Un-toggle Crouch while Shift persists restores Run state without intermediate Idle.
- **AC-P17 / AC-P18**: Hard-Constraint Config Validator
  - Given: Config data registry with parameter mutations.
  - When: Validator `ValidateConfig()` is invoked on corrupted values ($V_{\text{walk}} \ge 5.0$, $V_{\text{crouch}} \ge 2.3$, $\text{accel\_time} = 0$).
  - Then: Method returns `false` and writes failure messages to the log spy.
  - Edge cases: Shipped production configuration returns `true` with zero warnings.
- **AC-P23**: Zero-Vector NaN Guard
  - Given: Controller moving backwards.
  - When: Movement input is released to $(0, 0)$.
  - Then: Direction evaluates to `Vector3.zero`, dot product calculation is skipped, state transitions cleanly to Idle with finite numbers (no NaN/Inf).

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/playercontroller/playercontroller_movement_test.cs` — must exist and pass

**Status**: [x] Created and verified (11 tests passing, 0 B GC verified)

---

## Dependencies

- Depends on: None (Foundational Core story)
- Unlocks: Story 002 (`story-002-camera-relative-basis-facing.md`)

---

## Completion Notes

- **Completed**: 2026-09-21
- **Code Review**: Approved by lead-programmer (Zero-GC hot path, deterministic linear slew, diagonal normalization, backpedal threshold handling, anti-kiting validation).
- **Test Evidence**: 11 automated NUnit EditMode unit tests in `tests/unit/playercontroller/playercontroller_movement_test.cs`.
- **Implementation Files**:
  - `src/Core/Player/PlayerConfig.cs`
  - `src/Core/Player/PlayerLocomotionFSM.cs`
  - `src/Core/Player/PlayerThirdPersonController.cs`
  - `tests/unit/UnitTests.asmdef`
  - `tests/unit/playercontroller/playercontroller_movement_test.cs`
- **Tech Debt**: None logged.

