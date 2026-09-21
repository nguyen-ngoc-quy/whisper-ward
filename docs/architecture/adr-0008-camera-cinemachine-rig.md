# ADR-0008: Cinemachine 3rd-Person Follow Rig and Deocclusion Recovery

## Status
Accepted

## Date
2026-09-21

## Engine Compatibility

| Field | Value |
|---|---|
| **Engine** | Unity 6 LTS (6000.3.17f1) |
| **Package** | Cinemachine 3.x (`com.unity.cinemachine`), New Input System (`com.unity.inputsystem`), PhysX |
| **Domain** | Camera / Core / Input |
| **Knowledge Risk** | LOW |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `.claude/docs/technical-preferences.md`, `design/gdd/camera-cinemachine.md`, `design/gdd/player-third-person-controller.md`, `docs/architecture/adr-0005-input-action-asset-stance-buffering.md`, `docs/architecture/adr-0006-player-controller-stance-fsm.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | NUnit EditMode tests for yaw/pitch clamping, `AC-P25` idle-facing decoupling invariant, deocclusion distance spherecast logic, and exponential damping recovery |

## ADR Dependencies

| Field | Value |
|---|---|
| **Depends On** | `ADR-0001: Deterministic Event/Messaging Bus`, `ADR-0002: Shared Physics and Collision Contract`, `ADR-0005: Input Action Asset & Stance Buffering Contract`, `ADR-0006: Kinematic Player Controller and Stance State Machine` |
| **Enables** | `ADR-0010: Guard AI 3-State FSM`, Sprint 1 Camera Integration |
| **Blocks** | Production implementation of `CameraRigService.cs` in Sprint 1 |
| **Ordering Note** | This ADR defines the core camera rig and planar orientation basis consumed by the Player Controller. |

---

## Context

### Problem Statement
In *Whisper Ward*, player agency, spatial navigation, and stealth tension depend on a dependable third-person camera (Pillar 2: Fair Mind-Challenge, Pillar 3: Cat-and-Mouse Suspense). The camera acts as the sole source of truth for the character's movement coordinate frame (`camera_yaw`).

Previous prototype implementations suffered from:
1. **Camera-Player Rotational Entanglement**: When players stopped moving and swung the camera to inspect an intersection, the character mesh automatically rotated with the camera, breaking stealth cover and exposing the player to patrolling guards.
2. **Wall Geometry Clipping & Void Leaks**: In tight industrial corridors, camera collisions against solid walls either pushed the near clipping plane inside geometry (exposing the scene void) or snapped abruptly between distances, inducing motion sickness.
3. **Gimbal Lock & Inversion**: Extreme vertical mouse movement allowed the camera to pitch past $\pm 90^\circ$, inverting look controls or driving the camera beneath the floor.
4. **Static Visual Tension**: Escaping pursuit lacked cinematic intensity because camera framing remained identical between cautious creeping and desperate running.

### Constraints
- **Unity 6 LTS**, C# 9+, Cinemachine 3.x, supporting PC Windows (60 fps) and WebGL (30 fps).
- **Scope Discipline (Anti-Pillar Enforcement)**: Third-person orbit and hide-spot portal inspection only. No free-cam debug modes, no first-person transitions, no cover snap cameras.
- **Zero Heap Allocations on Hot Update**: 0 B GC per frame during continuous camera look and follow tracking.
- **Strict `AC-P25` Invariant**: When WASD movement input is zero, rotating the camera must not alter the player character's facing orientation (`facing` vector must remain bit-identical).
- **Right-Shoulder Cinematography**: Nominal distance $D_{\text{nom}} = 2.80\text{ m}$, chest target height $Y_{\text{target}} = 1.35\text{ m}$, right-shoulder offset $X_{\text{offset}} = +0.35\text{ m}$.
- **Tight Pitch Clamping**: Pitch bounded strictly to $[-35.0^\circ, +65.0^\circ]$.

### Requirements
- **TR-CORE-008**: Cinemachine 3rd-person follow rig (right-shoulder offset $X=+0.35\text{ m}, Y=1.35\text{ m}, D_{\text{nom}}=2.80\text{ m}$, pitch clamped $[-35^\circ, +65^\circ]$).
- **TR-CORE-009**: Asymmetric occlusion recovery damping: fast push-in ($D_{\text{actual}} = \max(D_{\text{min}}, d_{\text{hit}} - R)$), smooth exponential pull-out ($\tau_{\text{recover}} = 0.25\text{ s}$).
- **TR-CORE-010**: Dynamic FOV expansion during active chase ($60.0^\circ \to 68.0^\circ$ over $0.6\text{ s}$; contraction over $1.5\text{ s}$).

---

## Decision

Implement a **Cinemachine 3.x Dual-Camera Rig Service** (`CameraRigService`), exposing the `ICameraRigService` contract.

```text
               ┌───────────────────────────────┐
               │    IInputService (ADR-0005)   │
               │         Mouse Look Delta      │
               └───────────────┬───────────────┘
                               │
                               ▼
               ┌───────────────────────────────┐
               │       CameraRigService        │
               │   LateUpdate Orchestration    │
               └───────────────┬───────────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
        ▼                      ▼                      ▼
┌────────────────┐   ┌───────────────────┐   ┌─────────────────┐
│ Pitch Clamp    │   │ Spherecast        │   │ Dynamic FOV     │
│ [-35°, +65°]   │   │ Deocclusion (E20) │   │ Scaling         │
│ Yaw Free 360°  │   │ Damped Recovery   │   │ Chase 60°->68°  │
└───────┬────────┘   └─────────┬─────────┘   └────────┬────────┘
        │                      │                      │
        └──────────────────────┼──────────────────────┘
                               │
                               ▼
               ┌───────────────────────────────┐
               │       CinemachineBrain        │
               │  - CM_FreeOrbit (P=10)        │
               │  - CM_HideSpot (P=20)         │
               └───────────────┬───────────────┘
                               │
                 ┌─────────────┴─────────────┐
                 ▼                           ▼
        Player Controller            Main Camera Render
      (Camera Yaw Basis)           (Render Output / WebGL)
```

### 1. Dual Virtual Camera Architecture (Cinemachine 3.x)
- The physical `MainCamera` hosts a `CinemachineBrain` executing in Unity's `LateUpdate` lifecycle phase to eliminate physics tracking jitter.
- Two virtual cameras are authored:
  1. `CM_FreeOrbit` (Priority 10): Primary third-person over-the-shoulder orbital camera.
  2. `CM_HideSpot` (Priority 20): Fixed interior portal camera mounted at `aperture_portal_target` of occupied hide spots.
- Blending between cameras uses `CinemachineBlenderSettings` with an `EaseInOut` curve over $T_{\text{blend}} = 0.35\text{ s}$.

### 2. Coordinate Mapping & Invariant `AC-P25` Compliance
- Input look delta $(\Delta x, \Delta y)$ is mapped to camera rotation:
  $$\omega_{\text{yaw}} = \Delta x \cdot S_{\text{mouse}}$$
  $$\theta_{\text{pitch}} = \text{Clamp}(\theta_{\text{pitch}} - \Delta y \cdot S_{\text{mouse}}, -35.0^\circ, +65.0^\circ)$$
- **Planar Basis Exposure**: The service exposes `PlanarForward` and `PlanarRight` projected onto the horizontal XZ plane for consumption by `IPlayerController`:
  $$\vec{d}_{\text{cam\_fwd}} = \text{Vector3.ProjectOnPlane}(\text{Camera.forward}, \text{Vector3.up}).\text{normalized}$$
- **Invariant `AC-P25`**: While the player character's movement velocity is zero, rotating the camera updates `CameraYaw` and planar vectors, but **strictly zero** calls or notifications are sent to rotate the character model. The character's `facing` vector remains bit-identical.

### 3. Spherecast Wall Deocclusion & Exponential Recovery Damping
To prevent camera geometry clipping and void exposure:
- A continuous `Physics.SphereCast` is evaluated from the chest target ($Y=1.35\text{ m}$) toward the nominal camera eye:
  - Radius: $R_{\text{cam\_col}} = 0.20\text{ m}$.
  - LayerMask: `LayerMask.GetMask("World")` (E20 static solid geometry only).
- **Instant In-Snap**: When a hit is detected at distance $d_{\text{hit}}$, actual camera distance snaps instantly:
  $$D_{\text{actual}} = \max(D_{\text{min}}, d_{\text{hit}} - R_{\text{cam\_col}})$$
  where $D_{\text{min}} = 0.40\text{ m}$.
- **Exponential Recovery Pull-Out**: When occlusion clears, the distance smoothly expands toward $D_{\text{nom}} = 2.80\text{ m}$:
  $$D(t + \Delta t) = D(t) + (D_{\text{nom}} - D(t)) \cdot \left(1 - e^{-\frac{\Delta t}{\tau_{\text{recover}}}}\right)$$
  where $\tau_{\text{recover}} = 0.25\text{ s}$.
- WebGL optimization: Max 2 deocclusion iterations per frame.

### 4. HideSpot Interior Framing
- Upon `IPlayerController.EnterHideSpot()`, `CM_HideSpot` priority elevates to 20.
- Camera targets the hide spot aperture ($h_{\text{portal}} = \min(0.8\text{ m}, \text{aperture\_height}/2)$).
- Rotation is constrained to a narrow $\pm 30^\circ$ yaw cone outward through the door peep slats, fully preventing camera clipping against the wardrobe back wall.

### 5. Dynamic Chase FOV Scaling
- Normal exploration FOV is $\text{FOV}_{\text{base}} = 60.0^\circ$.
- When the Event Bus signals Guard Chase state:
  - FOV expands smoothly to $\text{FOV}_{\text{chase}} = 68.0^\circ$ over $0.60\text{ s}$.
- When chase ceases:
  - FOV contracts smoothly back to $60.0^\circ$ over $1.50\text{ s}$.

---

## Key Interfaces

```csharp
namespace WhisperWard.Core.Contracts
{
    public interface ICameraRigService
    {
        float CameraYaw { get; }
        Vector3 PlanarForward { get; }
        Vector3 PlanarRight { get; }
        float CurrentFov { get; }
        bool IsInHideSpotView { get; }

        void UpdateLookRotation(Vector2 lookDelta, float deltaTime);
        void SetChaseFovActive(bool isChaseActive);
        void SwitchToHideSpotView(Vector3 apertureTarget, Vector3 outwardFacing);
        void SwitchToOrbitView();
    }
}
```

---

## Alternatives Considered

### Alternative 1: Custom Math-Only Camera (No Cinemachine)
- **Description**: Hand-code a MonoBehaviour that positions and rotates `Camera.main` directly via matrix math.
- **Pros**: Zero third-party or package dependencies.
- **Cons**: High engineering cost to replicate Cinemachine's robust blending, damping, and multi-camera priority systems; significant bug risk during hide-spot camera transitions.
- **Rejection Reason**: Unnecessary reinventing of the wheel for an 8-week production schedule.

### Alternative 2: Raycast Single-Point Occlusion Test
- **Description**: Use a simple `Physics.Raycast` from player to camera center.
- **Pros**: Marginally cheaper than SphereCast ($~0.01\text{ ms}$ difference).
- **Cons**: Raycast detects only mathematical lines; camera near clipping plane ($0.10\text{ m}$) still clips geometry when camera passes near wall edges, exposing the level void.
- **Rejection Reason**: Violates visual quality requirements and produces geometry artifacts.

---

## Consequences

### Positive
- **Guaranteed Stealth Precision (`AC-P25`)**: Players can inspect corners from concealment without accidental character movement.
- **Artifact-Free Geometry Collision**: Spherecast + exponential damping prevents both void clipping and camera jitter.
- **Seamless HideSpot Immersion**: Clean transitions into peep-hole interior perspective heighten tension without disorientation.

### Negative
- **Package Dependency**: Bound to Unity's Cinemachine 3.x package API conventions.
- **LateUpdate Requirement**: Must strictly execute in `LateUpdate` after character physics updates.

### Risks & Mitigations
- **Extreme Mouse Sensitivity Spike**: High DPI mice could cause jitter; mitigated by clamping input deltas and scaling with configurable sensitivity ($S_{\text{mouse}}$).
- **Corner Trapping in Corners**: Tight $90^\circ$ wall crevices could clamp distance to $0.40\text{ m}$; mitigated by player chest offset $1.35\text{ m}$ keeping ray origin clear of floor.

---

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|---|---|---|
| `camera-cinemachine.md` | TR-CORE-008: 3rd-person follow rig ($X=+0.35\text{ m}, Y=1.35\text{ m}, D_{\text{nom}}=2.80\text{ m}$) | Encapsulated in `CM_FreeOrbit` framing configuration and pitch clamping. |
| `camera-cinemachine.md` | TR-CORE-009: Asymmetric deocclusion ($R=0.20\text{ m}, \tau_{\text{recover}}=0.25\text{ s}$) | Implemented via `Physics.SphereCast` against E20 `World` layer with exponential damping recovery. |
| `camera-cinemachine.md` | TR-CORE-010: Dynamic Chase FOV scaling ($60^\circ \to 68^\circ$) | Driven by `SetChaseFovActive` smoothly interpolating camera lens FOV. |

---

## Performance Implications
- **CPU**: $< 0.15\text{ ms}$ per frame in `LateUpdate` on PC; $< 0.35\text{ ms}$ on WebGL. Max 2 spherecast iterations per frame.
- **Memory**: 0 B managed GC per frame.
- **Load Time**: Negligible. Virtual Camera prefabs instantiate with scene load.

---

## Validation Criteria
- NUnit EditMode tests confirm:
  1. Pitch angle is clamped strictly between $-35.0^\circ$ and $+65.0^\circ$.
  2. Planar basis calculation returns unit-length orthogonal vectors on the XZ plane.
  3. Pitch $\pm 90^\circ$ degeneracy fallbacks do not return zero-length vectors.
  4. Deocclusion distance formula matches $D_{\text{actual}} = \max(D_{\text{min}}, d_{\text{hit}} - R)$.
