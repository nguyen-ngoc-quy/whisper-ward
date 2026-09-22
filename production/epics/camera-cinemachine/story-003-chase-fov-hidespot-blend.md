# Story 003: Dynamic Chase FOV & HideSpot Viewport Blend

> **Epic**: Camera (Cinemachine Rig)
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-22

## Context

**GDD**: `design/gdd/camera-cinemachine.md`, `design/gdd/player-movement-hide.md`
**Requirement**: `TR-CORE-010`, `TR-FEAT-015`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0008: Cinemachine 3rd-Person Follow Rig and Deocclusion Recovery (Secondary: ADR-0001, ADR-0006)
**ADR Decision Summary**: Implement dynamic chase FOV scaling and HideSpot virtual camera priority blending. In exploration mode, FOV is $60.0^\circ$. When `GuardStateChangedEvent` or `SetChaseFovActive(true)` signals pursuit, smoothly expand FOV to $68.0^\circ$ ($\tau_{\text{fov}} = 0.18\text{ s}$, reaching target in $0.60\text{ s}$); smoothly contract back to $60.0^\circ$ over $1.50\text{ s}$ ($\tau_{\text{fov}} = 0.45\text{ s}$) upon evasion. Orchestrate `CM_HideSpot` (Priority 20) vs `CM_FreeOrbit` (Priority 10) transitions via Cinemachine $0.35\text{ s}$ `EaseInOut` blend curve, constraining interior look to $\pm 30^\circ$ yaw cone, and immediately cutting ($0\text{ ms}$) to `CaptureFocus` if `PlayerCapturedEvent` occurs during blend.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW
**Engine Notes**: `Unity.Cinemachine.CinemachineCamera`, `CinemachineBrain`, `CinemachineBlenderSettings`.

**Control Manifest Rules (this layer)**:
- Required: Dynamic Chase FOV Scaling: Smoothly interpolate horizontal/vertical FOV from $60.0^\circ$ to $68.0^\circ$ over $0.6\text{ s}$ during alert/chase, returning over $1.5\text{ s}$.
- Required: HideSpot Portal Blending: Transition to fixed portal camera inside hide spots over $0.35\text{ s}$ EaseInOut blend.
- Required: Dedicated interior aperture targeting and $\pm 30^\circ$ yaw cone constraint.
- Forbidden: Never update camera rig in `Update` or `FixedUpdate`.
- Guardrail: Maximum 0.30 ms per frame budget for blend orchestration; WebGL draw calls $< 1000$.

---

## Acceptance Criteria

*From GDD `design/gdd/camera-cinemachine.md` and `design/gdd/player-movement-hide.md`, scoped to this story:*

- [x] **AC-CAM-10 / AC6 — Dynamic Chase FOV Scaling & Smoothing**: When pursuit begins, FOV expands from $60.0^\circ \to 68.0^\circ$ with $\tau_{\text{fov}} = 0.18\text{ s}$ (reaching $\ge 67.6^\circ$ within $0.54\text{ s}$). When pursuit ends, FOV contracts back to $60.0^\circ$ with $\tau_{\text{fov}} = 0.45\text{ s}$ (reaching $\le 60.4^\circ$ within $1.35\text{ s}$).
- [x] **AC-CAM-11 / AC7 — HideSpot Viewport Blend & Aperture Constraint**: On `SwitchToHideSpotView`, `CM_HideSpot` priority elevates to 20, triggering $0.35\text{ s}$ `EaseInOut` blend to aperture portal target ($h_{\text{portal}} = \min(0.8\text{ m}, \text{aperture\_height}/2)$), and player look is constrained to a $\pm 30^\circ$ yaw cone facing outward.
- [x] **AC-CAM-12 / AC7 — Capture Event Blend Cancellation & Instant Cut**: If `PlayerCapturedEvent` fires during a transition into/out of a HideSpot, any active blend is aborted (`CancelActiveBlend`), and camera cuts instantly ($0\text{ ms}$) to `CaptureFocus` looking at the capturing guard.
- [x] **AC-CAM-13 / AC12 — WebGL Performance and CPU Budget Verification**: CPU execution time across `CinemachineBrain`, deocclusion, and FOV interpolation does not exceed $1.0\text{ ms}$/frame ($0.30\text{ ms}$ typical), and allocations remain 0 B GC.

---

## Implementation Notes

*Derived from ADR-0008 Implementation Guidelines:*

1. **Dynamic FOV Smoother**:
   ```csharp
   public float UpdateFov(float currentFov, bool isChaseActive, float deltaTime)
   {
       float targetFov = isChaseActive ? 68.0f : 60.0f;
       float tau = isChaseActive ? 0.18f : 0.45f;
       float alpha = 1.0f - Mathf.Exp(-deltaTime / tau);
       return currentFov + (targetFov - currentFov) * alpha;
   }
   ```
2. **HideSpot Priority and Aperture Setup**:
   ```csharp
   public void SwitchToHideSpotView(Vector3 apertureTarget, Vector3 outwardFacing)
   {
       _hideSpotCamera.transform.position = apertureTarget;
       _hideSpotCamera.transform.forward = outwardFacing;
       _hideSpotCamera.Priority = 20; // Triggers 0.35s EaseInOut blend via CinemachineBlenderSettings
       _baseOutwardFacing = outwardFacing;
       _isInHideSpot = true;
   }

   public void SwitchToOrbitView()
   {
       _hideSpotCamera.Priority = 5; // Falls back to CM_FreeOrbit (Priority 10)
       _isInHideSpot = false;
   }
   ```
3. **HideSpot Interior Yaw Clamp ($\pm 30^\circ$)**:
   ```csharp
   if (_isInHideSpot)
   {
       float baseYaw = Mathf.Atan2(_baseOutwardFacing.x, _baseOutwardFacing.z) * Mathf.Rad2Deg;
       _currentYaw = Mathf.Clamp(_currentYaw, baseYaw - 30.0f, baseYaw + 30.0f);
   }
   ```
4. **Capture Interruption**:
   ```csharp
   public void OnPlayerCaptured(PlayerCapturedEvent evt)
   {
       _cinemachineBrain.ActiveBlend?.Reset();
       _captureCamera.transform.position = evt.CaptureLocation;
       _captureCamera.transform.LookAt(evt.GuardLocation);
       _captureCamera.Priority = 100; // Immediate override
   }
   ```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: Planar basis calculation, pitch clamping, angular velocity limiter, and `AC-P25` idle decoupling.
- [Story 002]: Spherecast obstacle deocclusion, exponential recovery pull-out damping, and dither transparency.

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**[For Logic / Integration stories — automated test specs]:**

- **AC-CAM-10**: Dynamic Chase FOV Scaling
  - Given: `CameraRigService` with initial FOV $60.0^\circ$.
  - When: `SetChaseFovActive(true)` is set and simulation advances for $0.54\text{ s}$ ($3\tau$).
  - Then: Current FOV expands monotonically to $\ge 67.6^\circ$.
  - When: `SetChaseFovActive(false)` is set and simulation advances for $1.35\text{ s}$ ($3\tau$).
  - Then: Current FOV contracts monotonically to $\le 60.4^\circ$.
- **AC-CAM-11**: HideSpot Viewport Blend & Yaw Cone Clamp
  - Given: Player enters HideSpot facing north $(0, 0, 1)$ ($baseYaw = 0^\circ$).
  - When: `SwitchToHideSpotView` is called and mouse input attempts to look east ($+90^\circ$).
  - Then: `CM_HideSpot.Priority` is set to 20, and applied camera yaw is clamped to $+30.0^\circ$.
- **AC-CAM-12**: Capture Event Blend Interruption
  - Given: Active blend between `CM_FreeOrbit` and `CM_HideSpot` in progress ($t = 0.15\text{ s}$ into $0.35\text{ s}$ blend).
  - When: `PlayerCapturedEvent` is published.
  - Then: Active blend is cancelled and `_captureCamera.Priority` elevates to 100 on the same frame tick.
- **AC-CAM-13**: Frame Timing & Allocation Budget
  - Given: Camera rig running full orbit, deocclusion, and FOV interpolation loop.
  - When: Evaluated over 100 frames.
  - Then: Main thread CPU execution is $\le 1.0\text{ ms}$/frame ($< 0.30\text{ ms}$ mean), and 0 B GC memory is allocated.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/camera/camera_blend_integration_test.cs` — must exist and pass

**Status**: [x] Verified
- Automated integration test suite: `tests/integration/camera/camera_blend_integration_test.cs` (6 tests, 100% passing).
- Validates dynamic chase FOV scaling and exponential smoothing (AC-CAM-10), HideSpot viewport blend and aperture yaw cone clamp (AC-CAM-11), capture event blend cancellation and instant cut (AC-CAM-12), and WebGL 0 B GC allocation and frame timing budget (AC-CAM-13).

---

## Dependencies

- Depends on: Story 002 (`story-002-spherecast-deocclusion-damping.md`)
- Unlocks: Sprint 1 Core Implementation Completion & Sprint QA Close-Out

---

## Completion Notes
**Completed**: 2026-09-22
**Criteria**: 4/4 passing
**Deviations**: None
**Test Evidence**: Integration: test suite at `tests/integration/camera/camera_blend_integration_test.cs` (6 integration tests)
**Code Review**: Complete (Approved)
