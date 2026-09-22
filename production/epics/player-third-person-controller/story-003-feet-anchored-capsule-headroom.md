# Story 003: Feet-Anchored Capsule Scaling, Headroom Clearance Probe & HideSpot State

> **Epic**: Player Third-Person Controller
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-21

## Context

**GDD**: `design/gdd/player-third-person-controller.md`, `design/gdd/player-movement-hide.md`
**Requirement**: `TR-CORE-004`, `TR-FEAT-015`, `TR-FEAT-016`, `TR-FEAT-017`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0006: Kinematic Player Controller and Stance State Machine (Secondary: ADR-0002: Shared Physics and Collision Contract)
**ADR Decision Summary**: Manage feet-anchored capsule resizing between Stand ($1.80\text{ m}$) and Crouch ($0.95\text{ m}$) with $\text{center.y} = \text{height}/2$. Evaluate upward headroom clearance via `Physics.OverlapCapsuleNonAlloc` against the E20 LayerMask with lower hemisphere ground insetting ($\ge \text{skin\_width}$) and top margin $\epsilon_{\text{probe}} = 0.03\text{ m}$. Integrate `HideSpot` containment with pure-pivot translational lock ($\Delta \vec{p} = \vec{0}$), view cone clamping, standardized $1.20\text{ m}$ standoff exit positioning, and capture/respawn freeze/reset semantics.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW
**Engine Notes**: CharacterController capsule geometry manipulation and PhysX `OverlapCapsuleNonAlloc` spatial queries.

**Control Manifest Rules (this layer)**:
- Required: Keep player capsule radius locked to $R = 0.35\text{ m}$; scale height ($1.80\text{ m}$ Stand, $0.95\text{ m}$ Crouch) with $\text{center.y} = \text{height} / 2$ to anchor feet at local $y = 0$.
- Required: Probe stand headroom using `Physics.OverlapCapsuleNonAlloc` against E20 LayerMask with lower hemisphere inset by $\ge \text{skin\_width}$ above the feet plane and top margin $\epsilon_{\text{probe}} = 0.03\text{ m}$. Fail safe (deny stand) if blocked or buffer saturates.
- Required: Set `CharacterController.stepOffset` dynamically: $0.30\text{ m}$ while Standing; $0.15\text{ m}$ while Crouched.
- Required: Call `Physics.SyncTransforms()` once prior to capsule query resize operations.
- Required: During HideSpot dwell, enforce pure-pivot translational lock ($\Delta \vec{p} = \vec{0}$) and standardized standoff distance ($1.20\text{ m}$) from spot entrance node upon exit.
- Forbidden: Never query triggers (`QueryTriggerInteraction.Collide`) during stand clearance probes. Always pass `QueryTriggerInteraction.Ignore`.
- Guardrail: Maximum 1.20 ms per frame budget for all spatial physics queries combined; exactly 0 B heap allocation on clearance probe calls.

---

## Acceptance Criteria

*From GDD `design/gdd/player-third-person-controller.md` and `design/gdd/player-movement-hide.md`, scoped to this story:*

- [x] **AC-P13 — Feet-Anchored Capsule Height Scaling**: Capsule height scales continuously: $H_{\text{cap}} = H_{\text{crouch}} + (H_{\text{stand}} - H_{\text{crouch}}) \times \gamma$. `CharacterController.center.y` is co-moved each tick to exactly $H_{\text{cap}} / 2$, ensuring feet remain anchored at $y = 0$ without sinking or lifting.
- [x] **AC-P12 — Monotonic Gamma Blend & Stand Clearance Gate**: Transitioning Crouch $\to$ Stand increases $\gamma$ monotonically ($0 \to 1$); Stand $\to$ Crouch decreases $\gamma$ ($1 \to 0$). If headroom clearance probe fails, $\gamma$ freezes, `stand_blocked` publishes `true`, movement speed remains clamped to $V_{\text{crouch}}$, and stand auto-completes on the first clear tick.
- [x] **Headroom Clearance Query Geometry**: Stand probe uses `Physics.OverlapCapsuleNonAlloc` with $R = 0.35\text{ m}$, lower sphere $P_{\text{bottom}} = \text{feet} + \text{Vector3.up} \times (R + \text{skin\_width})$, upper sphere $P_{\text{top}} = \text{feet} + \text{Vector3.up} \times (H_{\text{stand}} - R + 0.03\text{ m})$, against LayerMask E20 with `QueryTriggerInteraction.Ignore`. If `hitCount >= bufferCapacity`, fail safe and report blocked.
- [x] **Dynamic Step Offset Adjustment**: `CharacterController.stepOffset` is set to $0.30\text{ m}$ while Standing and $0.15\text{ m}$ while Crouched.
- [x] **AC-P15 — Capture / Sever Ingress**: When capture flag is raised, control severs at `Tick` entry; last pre-tick snapshot is republished as frozen baseline across all subsequent ticks without trailing transition events.
- [x] **AC-P16 — Authoritative Respawn Reset**: Respawn sets state to Idle (Standing latch, standing pose), $\gamma = 1$, $v_{\text{eff}} = 0$, $\text{facing} = \text{spawnForward}$, $\text{stand\_blocked} = \text{false}$, and emits zero spurious transition events before player input.
- [x] **TR-FEAT-015 / TR-FEAT-016 — HideSpot Containment & Translational Lock**: Entering a HideSpot transitions state to `InHideSpot`, locks translation ($\Delta \vec{p} = \vec{0}$), clamps viewing yaw to $[-60^\circ, +60^\circ]$ relative to spot entrance normal, and suppresses perception visibility.
- [x] **TR-FEAT-017 — Standoff Exit Positioning**: Exiting a HideSpot positions the player at standardized standoff distance $1.20\text{ m}$ along the spot's entrance portal forward vector to prevent geometry entrapment.

---

## Implementation Notes

*Derived from ADR-0006 and ADR-0002 Implementation Guidelines:*

1. **Feet-Anchored Resizing**:
   - `_characterController.height = capsuleHeight;`
   - `_characterController.center = new Vector3(0f, capsuleHeight * 0.5f, 0f);`
   - Before resizing, call `Physics.SyncTransforms()` if transform was moved in the same tick.
2. **Stand Clearance Query Geometry**:
   - $R = 0.35\text{ m}$, $\text{skin\_width} = 0.025\text{ m}$, $\epsilon_{\text{probe}} = 0.03\text{ m}$.
   - $P_{\text{bottom}} = \text{feet} + \text{Vector3.up} \times (0.35\text{ m} + 0.025\text{ m}) = \text{feet} + \text{Vector3.up} \times 0.375\text{ m}$.
   - $P_{\text{top}} = \text{feet} + \text{Vector3.up} \times (1.80\text{ m} - 0.35\text{ m} + 0.03\text{ m}) = \text{feet} + \text{Vector3.up} \times 1.48\text{ m}$.
   - Query: `Physics.OverlapCapsuleNonAlloc(P_bottom, P_top, R, _overlapHitBuffer, e20LayerMask, QueryTriggerInteraction.Ignore)`.
   - If `hitCount == 0`, clearance granted. If `hitCount > 0`, blocked.
3. **HideSpot Dwell Contract**:
   - `EnterHideSpot(Vector3 interiorAnchor, Vector3 portalForward)` moves character to `interiorAnchor`, disables `CharacterController`, sets state to `LocomotionState.InHideSpot`, and stores `portalForward`.
   - `ExitHideSpot(Vector3 exitPosition)` restores `CharacterController`, sets position to `exitPosition` (or `interiorAnchor + portalForward * 1.20f`), and transitions to `LocomotionState.Idle` (Crouched stance).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: Kinematic linear speed slewing, backpedal speed factor, and state transition machine.
- [Story 002]: Camera-relative yaw basis transformation, rear-damped angular rotation curves, and stationary camera orbit invariant (`AC-P25`).

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**[For Logic / Integration stories — automated test specs]:**

- **AC-P13**: Feet-Anchored Capsule Height Scaling
  - Given: Character standing on flat floor at $Y = 0$.
  - When: Stance transitions between Stand ($1.80\text{ m}$) and Crouch ($0.95\text{ m}$).
  - Then: `center.y` is verified to equal $\text{height} / 2$ on every tick; bottom of the capsule remains at exact $Y = 0 \pm 10^{-5}\text{ m}$.
  - Edge cases: Continuous mid-blend values ($\gamma = 0.5 \implies H = 1.375\text{ m}, \text{center.y} = 0.6875\text{ m}$).
- **AC-P12**: Monotonic Blend & Stand Gate Obstruction
  - Given: Crouched character beneath an obstacle with ceiling height $1.20\text{ m}$ (lower than $H_{\text{stand}} = 1.80\text{ m}$).
  - When: Player requests uncrouch (Standing latch set).
  - Then: Overlap probe detects ceiling collider; $\gamma$ freezes; `isStandBlocked` reports `true`; character speed remains clamped to $V_{\text{crouch}} = 1.80\text{ m/s}$.
  - Edge cases: Character walks out from under obstacle; probe clears on next tick; stand auto-completes to $1.80\text{ m}$ without requiring a second button press.
- **Headroom Probe Fail-Safe**:
  - Given: Buffer capacity is 16 colliders.
  - When: Query returns `hitCount >= 16`.
  - Then: Controller treats the result as obstructed (`stand_blocked = true`).
- **Ground Inset Isolation**:
  - Given: Character standing on a solid `World` floor.
  - When: Stand clearance probe is executed.
  - Then: Because lower hemisphere is inset by $\text{skin\_width}$, the floor collider is NOT detected in the query results (`hitCount == 0`).
- **AC-P15**: Capture / Sever Ingress
  - Given: Character running at $6.25\text{ m/s}$.
  - When: Capture flag is injected at start of tick.
  - Then: Velocity, stance, and snapshot freeze at pre-tick values; zero locomotion or transition events emitted.
- **AC-P16**: Authoritative Respawn Reset
  - Given: Character captured in a crouched state.
  - When: `Teleport(spawnPos, spawnRot)` and `RestoreControl()` are called.
  - Then: State resets to Idle Standing ($\gamma = 1$), facing matches spawn rotation, and no transition events are recorded prior to player input.
- **TR-FEAT-015 / TR-FEAT-016 / TR-FEAT-017**: HideSpot Dwell & Standoff Exit
  - Given: Character enters HideSpot with interior anchor $(10, 0, 10)$ and portal forward $(0, 0, 1)$.
  - When: Move inputs are injected during dwell, followed by exit request.
  - Then: During dwell, $\Delta \vec{p} = \vec{0}$; upon exit, player position is set to $(10, 0, 10) + (0, 0, 1) \times 1.20 = (10, 0, 11.20)$.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/playercontroller/playercontroller_capsule_test.cs` — must exist and pass

**Status**: [x] Verified and Passing (11 EditMode Unit Tests)

---

## Dependencies

- Depends on: Story 002 (`story-002-camera-relative-basis-facing.md`)
- Unlocks: Sprint 1 Core Locomotion Implementation

---

## Completion Notes
**Completed**: 2026-09-21  
**Criteria**: 8/8 passing  
**Deviations**: None  
**Test Evidence**: Logic: `tests/unit/playercontroller/playercontroller_capsule_test.cs` (11 passing tests)  
**Code Review**: Complete — APPROVED  
