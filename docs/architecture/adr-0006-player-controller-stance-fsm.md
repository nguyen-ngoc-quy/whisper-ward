# ADR-0006: Kinematic Player Controller and Stance State Machine

## Status
Accepted

## Date
2026-09-21

## Engine Compatibility

| Field | Value |
|---|---|
| **Engine** | Unity 6 LTS (6000.3.17f1) |
| **Package** | PhysX, Unity New Input System (`com.unity.inputsystem`), Cinemachine (v3.x) |
| **Domain** | Core / Locomotion / Physics / Character Controller |
| **Knowledge Risk** | LOW |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `.claude/docs/technical-preferences.md`, `design/gdd/player-third-person-controller.md`, `design/gdd/player-movement-hide.md`, `docs/architecture/adr-0002-physics-collision-contract.md`, `docs/architecture/adr-0005-input-action-asset-stance-buffering.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | NUnit EditMode tests for speed ratios ($V_{\text{chase}} / V_{\text{run}} \ge 1.20$), stance transitions, stand clearance probe insetting, and zero-allocation snapshot generation |

## ADR Dependencies

| Field | Value |
|---|---|
| **Depends On** | `ADR-0001: Deterministic Event/Messaging Bus`, `ADR-0002: Shared Physics and Collision Contract`, `ADR-0005: Input Action Asset & Stance Buffering Contract` |
| **Enables** | `ADR-0008: Cinemachine 3rd-Person Camera Rig`, `ADR-0010: Guard AI 3-State FSM`, `ADR-0011: Player Noise Stride Ledger` |
| **Blocks** | Production implementation of `PlayerThirdPersonController.cs` in Sprint 1 |
| **Ordering Note** | This ADR defines the core locomotion contract. It unblocks Sprint 1 player movement stories. |

---

## Context

### Problem Statement
Whisper Ward's stealth gameplay requires precision movement where detection failure is strictly fair (Pillar 2: Fair Mind-Challenge). The third-person player controller acts as the character's physical embodiment and the authoritative source for speed, stance, capsule bounds, and noise emission. 

Previous prototype implementations suffered from:
1. **Locomotion Snagging & Wall Glitching**: Custom swept-sphere controllers snagged on geometry seams and doorways, while default dynamic Rigidbodies introduced unpredictable sliding and non-deterministic physics acceleration.
2. **Ceiling Overlap Anomalies**: Transitioning from crouch to stand under low crawlspaces or furniture frequently clipped into ceiling geometry or falsely reported blockage when the query capsule collided with the floor beneath the player's feet.
3. **Framerate-Dependent Decoupling**: Variable render framerates caused inconsistent linear speed slewing, leading to erratic footstep cadence and unpredictable distance attenuation.

### Constraints
- **Unity 6 LTS**, C# 9+, supporting PC Windows (60 fps) and WebGL (30 fps).
- **Scope Discipline (Anti-Pillar Enforcement)**: Ground-plane 2.5D movement only. Strictly NO jumping, climbing, vaulting, leaning, or cover magnet systems.
- **Zero-GC on Hot Paths**: 0 managed heap allocations during active locomotion and snapshot polling.
- **Strict Kinematic Speeds**: $V_{\text{crouch}} = 1.80\text{ m/s}$, $V_{\text{walk}} = 3.60\text{ m/s}$, $V_{\text{run}} = 6.25\text{ m/s}$.
- **Anti-Kiting Invariant**: Guard chase speed $V_{\text{chase}} = 7.50\text{ m/s}$ must satisfy $V_{\text{chase}} / V_{\text{run}} \ge 1.20$ ($7.50 / 6.25 = 1.20$) to guarantee players cannot kite guards indefinitely.
- **Feet-Anchored Capsule Scaling**: Modifying character height must never lift the feet off the floor or sink the capsule into ground colliders.

### Requirements
- **TR-CORE-001**: Implement kinematic speed hierarchy: $V_{\text{crouch}} = 1.80\text{ m/s} < V_{\text{walk}} = 3.60\text{ m/s} < V_{\text{run}} = 6.25\text{ m/s}$.
- **TR-CORE-002**: Enforce anti-kiting kinematic invariant: $V_{\text{chase}} / V_{\text{run}} \ge 1.20$.
- **TR-CORE-003**: Camera-relative planar motion basis transformation via Cinemachine forward yaw vector with pitch degeneracy guards.
- **TR-CORE-004**: Stance transition clearance validation via `Physics.OverlapCapsuleNonAlloc` against the E20 LayerMask.
- **TR-FEAT-015**: Support `HideSpot` trigger containment state with full perception LOS suppression.
- **TR-FEAT-016**: Enforce pure-pivot translational lock ($\Delta \vec{p} = \vec{0}$) during `HideSpot` dwell.
- **TR-FEAT-017**: Standardized standoff distance ($1.20\text{ m}$) from spot entrance node upon exit.

---

## Decision

Implement a **Kinematic Player Controller** backed by the Unity `CharacterController` component, governed by a pure C# deterministic state machine (`PlayerLocomotionState`) and exposed via the `IPlayerController` interface.

```text
               ┌───────────────────────────────┐
               │    IInputService (ADR-0005)   │
               │   WASD Vector2, Crouch, Run   │
               └───────────────┬───────────────┘
                               │
                               ▼
               ┌───────────────────────────────┐
               │  PlayerThirdPersonController  │
               │  Camera-Relative Planar Basis │
               └───────────────┬───────────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
        ▼                      ▼                      ▼
┌────────────────┐   ┌───────────────────┐   ┌─────────────────┐
│ Stance FSM     │   │ Stand Gate Probe  │   │ Character-      │
│ Idle / Crouch  │   │ OverlapCapsule-   │   │ Controller      │
│ Walk / Run     │   │ NonAlloc (E20)    │   │ Kinematic Move  │
│ InHideSpot     │   │ Ground Inset      │   │ Feet Anchoring  │
└───────┬────────┘   └───────────────────┘   └────────┬────────┘
        │                                             │
        └──────────────────────┬──────────────────────┘
                               │
                               ▼
               ┌───────────────────────────────┐
               │   PlayerLocomotionSnapshot    │
               │   (Readonly Struct, Zero-GC)  │
               └───────────────┬───────────────┘
                               │
                 ┌─────────────┴─────────────┐
                 ▼                           ▼
        Perception System            Player Noise System
     (Eye Offsets Selection)       (Audible Stride Ledger)
```

### 1. Kinematic Movement & Linear Slew Model
The controller computes effective horizontal speed $v_{\text{eff}}$ using a deterministic linear slew approach toward target speed $v_{\text{target}}$:

$$v_{\text{target}} = S(\text{state}) \times f_{\text{back}}$$

$$f_{\text{back}} = \begin{cases} 0.70 & \text{if } \vec{d}_{\text{move}} \cdot \text{facing} < -10^{-4} \\ 1.00 & \text{otherwise} \end{cases}$$

$$a_{\text{max}} = \frac{V_{\text{run}}}{\text{accel\_time}} = \frac{6.25\text{ m/s}}{0.08\text{ s}} = 78.125\text{ m/s}^2$$

$$v_{\text{eff}}(t + \Delta t) = \text{Approach}(v_{\text{eff}}, v_{\text{target}}, a_{\text{max}} \cdot \Delta t)$$

- **Acceleration Time (`accel_time`)**: Bounded at $0.08\text{ s}$ ($\le 0.10\text{ s}$ per GDD #11), giving crisp, immediate response without visual animation popping.
- **Facing Rotation**: The character mesh rotates toward $\vec{d}_{\text{move}}$ using a rear-damped angular velocity curve $W(\vec{d}_{\text{move}} \cdot \text{facing})$ that preserves readability during backpedaling. Idle holds the last facing direction.

### 2. Camera-Relative Planar Basis & Degeneracy Protection
Movement direction is transformed relative to the active camera yaw:

$$\vec{d}_{\text{proj}} = \text{Vector3.ProjectOnPlane}(\text{Camera.main.transform.forward}, \text{Vector3.up})$$

$$\vec{d}_{\text{cam\_fwd}} = \begin{cases} \vec{d}_{\text{proj}}.\text{normalized} & \text{if } \|\vec{d}_{\text{proj}}\| \ge 10^{-4} \\ \text{Vector3.ProjectOnPlane}(\text{Camera.main.transform.up}, \text{Vector3.up}).\text{normalized} & \text{otherwise (fallback for pitch } \pm 90^\circ\text{)} \end{cases}$$

$$\vec{d}_{\text{cam\_right}} = \text{Vector3.Cross}(\text{Vector3.up}, \vec{d}_{\text{cam\_fwd}})$$

$$\vec{d}_{\text{world}} = (\vec{d}_{\text{cam\_right}} \times \text{input.x} + \vec{d}_{\text{cam\_fwd}} \times \text{input.y}).\text{normalized}$$

### 3. Feet-Anchored Capsule Geometry & Stand-Gate Probe
The player capsule radius is locked to $R = 0.35\text{ m}$. Height transitions between Stand ($H_{\text{stand}} = 1.80\text{ m}$) and Crouch ($H_{\text{crouch}} = 0.95\text{ m}$).

- **Feet-Anchored Center Rule**: In Unity, resizing `CharacterController.height` expands symmetrically from center. To keep feet anchored at local $y = 0$:
  $$\text{center.y} = \frac{\text{height}}{2}$$
- **Stand Clearance Query (`OverlapCapsuleNonAlloc`)**:
  Before transitioning from Crouched to Standing, an upward clearance query is executed against the E20 LayerMask:
  - Lower sphere center: $P_{\text{bottom}} = \text{feet} + \text{Vector3.up} \times (R + \text{skin\_width})$.
    *Note: Insetting by $\text{skin\_width}$ ensures the query never detects the floor collider the character is standing on.*
  - Upper sphere center: $P_{\text{top}} = \text{feet} + \text{Vector3.up} \times (H_{\text{stand}} - R + \epsilon_{\text{probe}})$, where $\epsilon_{\text{probe}} = 0.03\text{ m}$.
  - Query radius: $R = 0.35\text{ m}$.
  - Interaction: `QueryTriggerInteraction.Ignore`.
  - **Fail-Safe Gate**: If `hitCount == 0`, clearance is granted. If `hitCount >= bufferCapacity`, the query fails safe and reports `stand_blocked = true`.
  - While `stand_blocked = true`, the latch remains Standing, but the movement speed is clamped to $V_{\text{crouch}}$ and the visual pose remains crouched.

### 4. CharacterController Tunings & Downward Ground-Snap
To prevent micro-jitter and slope separation:
- `skinWidth = 0.025m` ($7.1\%$ of $R = 0.35\text{ m}$).
- `minMoveDistance = 0.0m` (ensures linear slew reaches exact zero without stalling).
- `stepOffset`: Dynamically adjusted by stance — $0.30\text{ m}$ while Standing; $0.15\text{ m}$ while Crouched (prevents stepping over waist-high obstacles while crawling).
- `slopeLimit = 45.0°` (mirrors NavMesh agent maximum slope).
- **Downward Ground-Snap**: Apply a continuous downward bias velocity ($v_{\text{down}} = -3.0\text{ m/s}$) during grounded locomotion inside `CharacterController.Move()` to prevent intermittent `isGrounded` false-negatives across uneven floor seams.

### 5. HideSpot Integration (TR-FEAT-015, TR-FEAT-016, TR-FEAT-017)
- **`InHideSpot` State**: Triggered upon confirmed entry into a `HideSpot` volume (`ADR-0002`).
- **Translational Lock**: Enforce $\Delta \vec{p} = \vec{0}$ on the CharacterController. Position is clamped to the spot's interior datum.
- **Pivot Yaw Allowance**: Free yaw rotation is permitted within the authored aperture viewing cone ($[-60^\circ, +60^\circ]$ relative to spot entrance normal).
- **Standoff Exit**: On exit, character is positioned at `standoff_distance = 1.20 m` along the spot's entrance vector to prevent collision entrapment against the prop doorway.

---

## Key Interfaces

```csharp
namespace WhisperWard.Core.Contracts
{
    public enum MovementStance
    {
        Crouched = 0,
        Standing = 1
    }

    public enum LocomotionState
    {
        Idle = 0,
        Crouch = 1,
        Walk = 2,
        Run = 3,
        InHideSpot = 4
    }

    public readonly struct PlayerLocomotionSnapshot
    {
        public readonly Vector3 Position;
        public readonly Vector3 Velocity;
        public readonly Vector3 Facing;
        public readonly LocomotionState State;
        public readonly MovementStance Stance;
        public readonly float EffectiveSpeed;
        public readonly float CapsuleHeight;
        public readonly bool IsStandBlocked;
        public readonly bool IsGrounded;

        public PlayerLocomotionSnapshot(
            Vector3 position,
            Vector3 velocity,
            Vector3 facing,
            LocomotionState state,
            MovementStance stance,
            float effectiveSpeed,
            float capsuleHeight,
            bool isStandBlocked,
            bool isGrounded)
        {
            Position = position;
            Velocity = velocity;
            Facing = facing;
            State = state;
            Stance = stance;
            EffectiveSpeed = effectiveSpeed;
            CapsuleHeight = capsuleHeight;
            IsStandBlocked = isStandBlocked;
            IsGrounded = isGrounded;
        }
    }

    public interface IPlayerController
    {
        PlayerLocomotionSnapshot CurrentSnapshot { get; }
        void Teleport(Vector3 worldPosition, Quaternion worldRotation);
        void EnterHideSpot(Vector3 interiorAnchor, Vector3 portalForward);
        void ExitHideSpot(Vector3 exitPosition);
        void SeverControl();
        void RestoreControl();
    }
}
```

---

## Alternatives Considered

### Alternative 1: Kinematic Rigidbody with MovePosition in FixedUpdate
- **Description**: Attach a kinematic `Rigidbody` to the player and update position via `Rigidbody.MovePosition()` inside `FixedUpdate`.
- **Pros**: Direct integration with PhysX velocity solver; unified with dynamic obstacle collision.
- **Cons**: Severe stair-stepping and snagging on low curbs; interpolating camera between `FixedUpdate` and render frames introduces visual jitter on high-refresh WebGL displays; lacks built-in slope/step limits.
- **Rejection Reason**: Excessive custom code required to match `CharacterController`'s step-handling; camera smoothing jitter degrades portfolio demo quality.

### Alternative 2: Pure Custom Raycast / Capsule Sweeper
- **Description**: Fully hand-rolled swept-volume collision resolver using `Physics.CapsuleCastNonAlloc` and penetration push-outs.
- **Pros**: Complete mathematical transparency; no reliance on Unity internal component lifecycles.
- **Cons**: High development time (estimated 2–3 weeks to resolve all corner-wedging and acute-angle tunneling bugs); unacceptable schedule risk for an 8-week solo timeline.
- **Rejection Reason**: Violates the 8-week production timeline and anti-pillar scope discipline.

---

## Consequences

### Positive
- **Guaranteed Anti-Kiting Fairness**: The $1.20$ speed ratio ($V_{\text{chase}} = 7.50\text{ m/s}$ vs $V_{\text{run}} = 6.25\text{ m/s}$) guarantees guards catch running players over open ground, preserving chase tension.
- **Zero Ceiling Snagging**: Inset lower hemisphere on `OverlapCapsuleNonAlloc` prevents false floor collisions while top-margin probe reliably blocks uncrouching under low geometry.
- **Zero GC on Hot Paths**: Snapshot structs and non-allocating overlap buffers prevent garbage collection stutter on WebGL.

### Negative
- **Manual Physics Synchronization**: When changing capsule height immediately prior to queries, `Physics.SyncTransforms()` must be managed carefully to avoid redundant overhead.
- **Dual Step-Offset Maintenance**: Dynamic `stepOffset` tuning must be updated in tandem with stance transitions.

### Risks & Mitigations
- **Slope Separation / Ground Flicker**: `CharacterController.isGrounded` micro-flicker mitigated by applying continuous $-3.0\text{ m/s}$ downward snap bias.
- **Camera Pitch Degeneracy**: Looking straight down could cause zero-length horizontal projections; mitigated by fallback to projected camera up-vector.

---

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|---|---|---|
| `player-third-person-controller.md` | `TR-CORE-001`: Speed hierarchy ($1.80 < 3.60 < 6.25\text{ m/s}$) | Implements fixed speed constants and linear slew approach ($a_{\text{max}} = 78.125\text{ m/s}^2$). |
| `player-third-person-controller.md` | `TR-CORE-002`: Anti-kiting ratio $\ge 1.20$ | Locks $V_{\text{run}} = 6.25\text{ m/s}$ against guard $V_{\text{chase}} = 7.50\text{ m/s}$ ($7.50 / 6.25 = 1.20$). |
| `player-third-person-controller.md` | `TR-CORE-003`: Camera-relative planar motion basis | Projects Cinemachine forward yaw onto horizontal plane with pitch degeneracy fallback. |
| `player-third-person-controller.md` | `TR-CORE-004`: Stand clearance probe ($H=1.80\text{ m}, R=0.35\text{ m}$) | `OverlapCapsuleNonAlloc` with floor inset $\ge \text{skin\_width}$ and top inflation $\epsilon_{\text{probe}} = 0.03\text{ m}$. |
| `player-movement-hide.md` | `TR-FEAT-015`: HideSpot visibility suppression | `InHideSpot` locomotion state masks player visibility to guard perception LOS. |
| `player-movement-hide.md` | `TR-FEAT-016`: Pure-pivot translational lock | Enforces $\Delta \vec{p} = \vec{0}$ while permitting yaw rotation inside authored view cone. |
| `player-movement-hide.md` | `TR-FEAT-017`: Standoff exit distance ($1.20\text{ m}$) | Exits spot with $1.20\text{ m}$ forward offset along portal normal to prevent door-jam clipping. |

---

## Performance Implications
- **CPU**: Maximum $0.80\text{ ms}$ per frame for character movement, stance evaluation, and collision resolution.
- **Memory**: Exactly 0 B GC per frame. Pre-allocated static collider buffer (`capacity = 8`) for overlap tests.
- **Physics**: 1 `CharacterController.Move()` call per frame; 1 `OverlapCapsuleNonAlloc` query only when Stand is requested while crouched.

---

## Validation Criteria
1. **NUnit EditMode Tests**:
   - `test_locomotion_speeds_satisfy_anti_kiting_ratio`: Asserts $V_{\text{chase}} / V_{\text{run}} \ge 1.20$.
   - `test_linear_slew_approaches_target_speed_within_accel_time`: Validates approach within $\le 0.08\text{ s}$.
   - `test_backpedal_multiplier_applies_when_opposing_facing`: Confirms $0.70$ factor when dot $< -10^{-4}$.
   - `test_stand_clearance_probe_insets_floor_and_inflates_ceiling`: Mathematical verification of query coordinates.
2. **PlayMode Tests**:
   - Walking under low ceiling ($1.20\text{ m}$) keeps character in Crouch speed despite Stand toggle.
   - Stepping out from low ceiling auto-completes Stand transition once clearance is detected.
