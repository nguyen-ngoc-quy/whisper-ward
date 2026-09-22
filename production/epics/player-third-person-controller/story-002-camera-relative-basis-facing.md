# Story 002: Camera-Relative Planar Basis & Facing Orientation

> **Epic**: Player Third-Person Controller
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-21

## Context

**GDD**: `design/gdd/player-third-person-controller.md`
**Requirement**: `TR-CORE-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Kinematic Player Controller and Stance State Machine (Secondary: ADR-0008: Cinemachine 3rd-Person Follow Rig)
**ADR Decision Summary**: Transform raw 2D input vectors into 3D world movement using Cinemachine forward yaw projected onto the horizontal plane with pitch degeneracy fallback ($\pm 90^\circ$ pitch uses projected camera up-vector). Apply proportional rearward damping $w(\text{dot})$ to rotation rate ($360^\circ/\text{s}$, floor $0.25$), strictly enforce invariant AC-P25 (zero autonomous rotation when idle), and compute animator variant speed ratios.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW
**Engine Notes**: Pure planar vector projections (`Vector3.ProjectOnPlane`) and quaternion slerp / move-towards rotations.

**Control Manifest Rules (this layer)**:
- Required: Transform movement input relative to Cinemachine forward yaw vector projected on the horizontal plane. Fall back to projected camera up-vector when camera pitch approaches $\pm 90^\circ$ ($\|\vec{d}_{\text{proj}}\| < 10^{-4}$).
- Required: Rotating the camera while movement input is zero must strictly maintain bit-identical character facing orientation (`AC-P25`).
- Required: Mesh rotation toward movement heading applies rear-damped angular velocity curve $W(\vec{d}_{\text{move}} \cdot \text{facing})$ with floor $0.25$ and base rate $360^\circ/\text{s}$.
- Guardrail: Exactly 0 B (Zero GC) during camera basis calculation and snapshot generation.

---

## Acceptance Criteria

*From GDD `design/gdd/player-third-person-controller.md`, scoped to this story:*

- [x] **AC-P19 — Yaw-Frame Direction Correctness**: With camera yaw rotated in $90^\circ$ increments, sustained W input produces world-space planar velocity matching the expected basis quadrant direction in both magnitude and sign.
- [x] **Pitch Degeneracy Protection**: When camera forward is perpendicular to the ground plane ($\text{pitch} = \pm 90^\circ$, $\|\vec{d}_{\text{proj}}\| < 10^{-4}$), planar basis falls back cleanly to projected camera up-vector without generating NaN or zero vectors.
- [x] **AC-P3 — Proportional Rear Damping**: Rotation magnitude per tick equals $\min(\text{turn\_rate} \cdot w(\text{dot}) \cdot \Delta t, |\Delta \theta|)$. Full $180^\circ$ reversal settles monotonically without stall or flicker in $\sim 0.83\text{ s}$ (within $[0.52, 1.24]\text{ s}$ band) at starters.
- [x] **AC-P4 — Turn-Rate Bounded Rotation**: A forced $90^\circ$ forward yaw error resolves within $\lceil (90^\circ / \text{turn\_rate}) / \Delta t \rceil \cdot \Delta t + \text{one tick}$; per-tick rotation never exceeds $\text{turn\_rate} \times \Delta t$.
- [x] **AC-P25 — Idle Facing Hold Invariant**: With zero move input held while camera yaw changes continuously across N ticks, character `facing` remains bit-identical throughout — zero autonomous turn.
- [x] **AC-P21 — Determinism Replay Canary**: A recorded 60 s scripted input trace replayed twice yields bit-identical telemetry transcripts (timestamps and floating-point vector coordinates).
- [x] **AC-P22 — Readout Completeness & Copy-Safety**: `CurrentSnapshot` exposes an immutable readonly `PlayerLocomotionSnapshot` with position, velocity, facing, state, stance, effectiveSpeed, capsuleHeight, isStandBlocked, and isGrounded. Mutating copies has zero effect on controller state.
- [x] **AC-P24 — Animator Variant & Cruise Speed Calculator**: `AnimState` enum flips between forward and backward variants based on $\vec{d}_{\text{move}} \cdot \text{facing} < -10^{-4}$; `SpeedRatio` divides $v_{\text{eff}}$ by the active variant's registry cruise speed ($V_{\text{state}}$ forward, $V_{\text{state}} \times f_{\text{back}}$ backward).

---

## Implementation Notes

*Derived from ADR-0006 Implementation Guidelines:*

1. **Camera-Relative Basis Calculation**:
   $$\vec{d}_{\text{proj}} = \text{Vector3.ProjectOnPlane}(\vec{f}_{\text{cam}}, \text{Vector3.up})$$
   $$\vec{d}_{\text{cam\_fwd}} = \begin{cases} \vec{d}_{\text{proj}}.\text{normalized} & \text{if } \|\vec{d}_{\text{proj}}\| \ge 10^{-4} \\ \text{Vector3.ProjectOnPlane}(\vec{u}_{\text{cam}}, \text{Vector3.up}).\text{normalized} & \text{otherwise} \end{cases}$$
   $$\vec{d}_{\text{cam\_right}} = \text{Vector3.Cross}(\text{Vector3.up}, \vec{d}_{\text{cam\_fwd}})$$
   $$\vec{d}_{\text{world}} = (\vec{d}_{\text{cam\_right}} \times \text{input.x} + \vec{d}_{\text{cam\_fwd}} \times \text{input.y}).\text{normalized}$$
2. **Proportional Rear Damping Function $w(\text{dot})$**:
   $$w(\text{dot}) = \begin{cases} 1.0 & \text{if } \text{dot} \ge 0 \\ \text{rear\_damp\_floor} + (1.0 - \text{rear\_damp\_floor}) \times (1.0 + \text{dot}) & \text{if } \text{dot} < 0 \end{cases}$$
   where $\text{rear\_damp\_floor} = 0.25$ and $\text{turn\_rate} = 360^\circ/\text{s}$.
3. **Decoupling Idle Facing (AC-P25)**:
   - If $\|\text{input}\| \le 10^{-5}$, update logic skips facing updates entirely, retaining the exact previous facing vector regardless of camera yaw changes.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: Kinematic linear speed slewing, backpedal speed factor, and state transition machine.
- [Story 003]: Stand headroom clearance probe (`OverlapCapsuleNonAlloc`), capsule height resizing, and HideSpot containment states.

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**[For Logic / Integration stories — automated test specs]:**

- **AC-P19**: Yaw-Frame Direction Correctness
  - Given: Camera yaw injected at $0^\circ$ (North), $90^\circ$ (East), $180^\circ$ (South), $270^\circ$ (West).
  - When: Forward input $(0, 1)$ is applied.
  - Then: World-space movement vectors align with $(0, 0, 1)$, $(1, 0, 0)$, $(0, 0, -1)$, and $(-1, 0, 0)$ within $10^{-4}$ tolerance.
  - Edge cases: Diagonal input combinations in each rotated quadrant.
- **Pitch Degeneracy**:
  - Given: Camera looking straight down ($\text{pitch} = -90^\circ$, forward $=(0, -1, 0)$, up $=(0, 0, 1)$).
  - When: Forward input $(0, 1)$ is applied.
  - Then: Controller falls back to camera up-vector, producing movement along $(0, 0, 1)$ without generating NaN or zero vectors.
- **AC-P3**: Proportional Rear Damping
  - Given: Controller facing $(0, 0, 1)$.
  - When: Commanded heading is full rearward $(0, 0, -1)$ ($\text{dot} = -1.0$).
  - Then: Per-tick turn rate is clamped to $\text{turn\_rate} \times \text{rear\_damp\_floor} = 360^\circ/\text{s} \times 0.25 = 90^\circ/\text{s}$; total reversal completes in $0.83\text{ s} \pm 0.10\text{ s}$.
  - Edge cases: Oblique rearward heading ($\text{dot} = -0.5$).
- **AC-P4**: Turn-Rate Bounded Rotation
  - Given: Controller facing $(0, 0, 1)$.
  - When: Commanded heading is $90^\circ$ East $(1, 0, 0)$.
  - Then: Per-tick rotation equals $360^\circ/\text{s} \times \Delta t$; target heading reached in exactly $\lceil 0.25\text{ s} / \Delta t \rceil$ steps.
- **AC-P25**: Idle Facing Hold Invariant
  - Given: Character stationary at $(0, 0, 0)$ facing $(0, 0, 1)$ with zero input.
  - When: Camera rotates $360^\circ$ horizontally over 100 simulated ticks.
  - Then: Character facing vector remains bit-identical $(0, 0, 1)$ across all 100 ticks.
- **AC-P21**: Determinism Replay Canary
  - Given: A pre-recorded 60 s script of camera yaw changes and WASD inputs.
  - When: Script is run across two independent test instances with fixed $\Delta t = 0.02\text{ s}$.
  - Then: All resulting snapshot positions, velocities, and facing vectors match bit-for-bit.
- **AC-P22**: Snapshot Completeness & Copy-Safety
  - Given: Generated `PlayerLocomotionSnapshot`.
  - When: Fields are read and local copy modified.
  - Then: Controller's internal state remains completely unaltered (struct copy semantics).
- **AC-P24**: Animator Variant Calculator
  - Given: Controller moving backward ($\vec{d}_{\text{move}} \cdot \text{facing} < -10^{-4}$) at $v_{\text{eff}} = 2.1875\text{ m/s}$.
  - When: `SpeedRatio` is calculated for Run state.
  - Then: Variant resolves to `RunBack`, cruise speed evaluates to $6.25 \times 0.70 = 4.375\text{ m/s}$, and `SpeedRatio` outputs $2.1875 / 4.375 = 0.50$.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/playercontroller/playercontroller_orientation_test.cs` — must exist and pass

**Status**: [x] Verified by `tests/unit/playercontroller/playercontroller_orientation_test.cs` (8 NUnit tests, 100% pass)

---

## Dependencies

- Depends on: Story 001 (`story-001-kinematic-locomotion-slew.md`)
- Unlocks: Story 003 (`story-003-feet-anchored-capsule-headroom.md`)

---

## Completion Notes
**Completed**: 2026-09-21
**Criteria**: 8/8 passing (100%)
**Deviations**: None
**Test Evidence**: Logic: test file at `tests/unit/playercontroller/playercontroller_orientation_test.cs` (8 NUnit tests, 100% pass)
**Code Review**: Complete (APPROVED — zero allocations on hot paths, pitch degeneracy fallback verified, proportional rear damping validated)
