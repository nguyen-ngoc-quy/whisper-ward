# Story 001: Third-Person Follow Rig & Planar Basis Service

> **Epic**: Camera (Cinemachine Rig)
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-22

## Context

**GDD**: `design/gdd/camera-cinemachine.md`
**Requirement**: `TR-CORE-008`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0008: Cinemachine 3rd-Person Follow Rig and Deocclusion Recovery (Secondary: ADR-0006, ADR-0005)
**ADR Decision Summary**: Establish the third-person follow rig using Cinemachine 3.x (`CM_FreeOrbit`), executing strictly in `LateUpdate`. Frame player at right-shoulder offset ($X=+0.35\text{ m}, Y=1.35\text{ m}, D_{\text{nom}}=2.80\text{ m}$), clamp pitch angle to $[-35.0^\circ, +65.0^\circ]$, provide authoritative XZ planar forward and right basis vectors, enforce invariant `AC-P25` (zero autonomous character turning when idle), clamp angular look velocity ($\omega_{\text{max}} = 720.0^\circ/\text{s}$), and freeze camera orientation when `Time.timeScale == 0`.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW
**Engine Notes**: Cinemachine 3.x package (`Unity.Cinemachine`). `CinemachineBrain` executes in `LateUpdate`. New Input System provides mouse `Look` delta.

**Control Manifest Rules (this layer)**:
- Required: Camera follow logic and `CinemachineBrain` execution must occur exclusively in `LateUpdate` to eliminate physics tracking jitter.
- Required: Independent camera orbit & pitch clamping $[-35^\circ, +65^\circ]$; camera rotation while stationary must never rotate the character model (`AC-P25`).
- Required: Camera-relative planar basis with pitch degeneracy fallback ($\|\vec{d}_{\text{proj}}\| < 10^{-4}$).
- Required: Right-shoulder offset ($X=+0.35\text{ m}, Y=1.35\text{ m}, D_{\text{nom}}=2.80\text{ m}$).
- Forbidden: Never update camera rig in `Update` or `FixedUpdate`.
- Forbidden: Never rotate player model on stationary camera orbit (`AC-P25`).
- Guardrail: Maximum 0.30 ms per frame budget for camera orbit and basis transformations. Zero heap allocations (0 B GC) per frame.

---

## Acceptance Criteria

*From GDD `design/gdd/camera-cinemachine.md`, scoped to this story:*

- [x] **AC-CAM-01 / AC1 — Camera-Relative Planar Basis & Normalization**: Given azimuth `camera_yaw` $\theta$, forward vector maps to $(\sin \theta, \cos \theta)$ and right vector maps to $(\cos \theta, -\sin \theta)$ on the XZ plane. Input $(W+D)$ results in strictly normalized $\|\vec{d}_{\text{move}}\| = 1.0000$, ensuring maximum speed $6.25\text{ m/s}$ with zero $\sqrt{2}\times$ diagonal speed glitch.
- [x] **AC-CAM-02 / AC2 — Stationary Idle Facing Invariant (`AC-P25`)**: Rotating the camera a full $360^\circ$ around an idle player (WASD input magnitude $< 10^{-4}$) mutates `camera_yaw` but leaves character facing orientation bit-identical ($0.0^\circ$ delta).
- [x] **AC-CAM-03 / AC5 — Pitch Extrema Clamping & Gimbal Lock Prevention**: Pitch input is clamped strictly between $[-35.0^\circ, +65.0^\circ]$. Orbit rotation uses world up $(0, 1, 0)$ as fixed reference, preventing ground penetration or gimbal lock.
- [x] **AC-CAM-04 / AC10 — Angular Velocity Mouse Delta Spike Limiter**: Extreme mouse deltas are clamped to $\omega_{\text{max}} = 720.0^\circ/\text{s}$ ($\le 12.0^\circ$ per frame at $60\text{ fps}$), preventing disorienting flick spikes.
- [x] **AC-CAM-05 / AC11 — Pause Menu TimeScale Freeze & Delta Flusher**: When `Time.timeScale == 0`, look input is ignored and position/rotation remain frozen. On resume, mouse delta buffer is flushed to prevent jerk.

---

## Implementation Notes

*Derived from ADR-0008 Implementation Guidelines:*

1. **ICameraRigService Interface Contract**:
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
2. **Planar Basis Calculation with Degeneracy Fallback**:
   ```csharp
   Vector3 forward = cameraTransform.forward;
   Vector3 planarForward = new Vector3(forward.x, 0f, forward.z);
   if (planarForward.sqrMagnitude < 1e-8f)
   {
       // Pitch degeneracy fallback (+/- 90 degrees)
       Vector3 up = cameraTransform.up;
       planarForward = forward.y > 0f ? new Vector3(-up.x, 0f, -up.z) : new Vector3(up.x, 0f, up.z);
   }
   planarForward.Normalize();
   Vector3 planarRight = new Vector3(planarForward.z, 0f, -planarForward.x);
   ```
3. **Angular Look Limiter**:
   ```csharp
   float maxAngleThisFrame = maxAngularVelocity * deltaTime; // 720 deg/s
   float appliedYawDelta = Mathf.Clamp(lookDelta.x * mouseSensitivity, -maxAngleThisFrame, maxAngleThisFrame);
   float appliedPitchDelta = Mathf.Clamp(lookDelta.y * mouseSensitivity * (invertY ? -1f : 1f), -maxAngleThisFrame, maxAngleThisFrame);
   _currentPitch = Mathf.Clamp(_currentPitch - appliedPitchDelta, -35.0f, 65.0f);
   ```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: Spherecast obstacle deocclusion, exponential recovery pull-out damping, and dither transparency.
- [Story 003]: Dynamic chase FOV scaling and HideSpot virtual camera blend orchestration.

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**[For Logic / Integration stories — automated test specs]:**

- **AC-CAM-01**: Planar Basis Vector & Diagonal Normalization
  - Given: `CameraRigService` with `camera_yaw = 45.0f` ($\sin 45^\circ = \cos 45^\circ \approx 0.7071$).
  - When: `PlanarForward` and `PlanarRight` are queried, and combined with diagonal input $(I_x = 1, I_y = 1)$.
  - Then: `PlanarForward` equals $(0.7071, 0, 0.7071) \pm 10^{-4}$, and normalized movement vector magnitude equals exactly $1.0000 \pm 10^{-4}$ with world direction $(1.0, 0, 0)$.
- **AC-CAM-02**: Idle Facing Decoupling Invariant (`AC-P25`)
  - Given: Stationary character with initial facing $(0, 0, 1)$ and zero movement input.
  - When: Camera yaw rotates continuously from $0^\circ \to 360^\circ$ over 60 simulated frames.
  - Then: Character rotation quaternion remains bit-identical across all 60 frames.
- **AC-CAM-03**: Pitch Clamping & Degeneracy Fallback
  - Given: Look delta attempting to drive pitch to $+90^\circ$ or $-80^\circ$.
  - When: `UpdateLookRotation` processes the deltas.
  - Then: Internal pitch value clamps strictly to $[-35.0^\circ, +65.0^\circ]$, and planar basis vectors remain unit-length without NaN or zero-vectors.
- **AC-CAM-04**: Angular Velocity Mouse Spike Clamp
  - Given: Delta input spike $\Delta x = 5000\text{ px}$ at $\Delta t = 0.0166\text{ s}$ ($60\text{ fps}$).
  - When: `UpdateLookRotation` is executed.
  - Then: Applied yaw change is clamped to $720.0^\circ \times 0.0166\text{ s} \approx 12.0^\circ$.
- **AC-CAM-05**: TimeScale Freeze & Delta Flusher
  - Given: `Time.timeScale = 0.0f` with active mouse look deltas.
  - When: Update cycles run during pause.
  - Then: Camera yaw and pitch remain constant; on unpause, residual delta is cleared.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/camera/camera_follow_rig_test.cs` — must exist and pass

**Status**: [x] Verified
- Automated unit test suite: `tests/unit/camera/camera_follow_rig_test.cs` (6 tests, 100% passing).
- Validates camera-relative planar basis normalization, stationary idle facing decoupling invariant AC-P25, pitch clamping [-35 deg, +65 deg] and pitch degeneracy fallbacks, angular look spike clamp at 720 deg/s, and TimeScale = 0 freeze.

---

## Dependencies

- Depends on: None (Foundational Core Camera story)
- Unlocks: Story 002 (`story-002-spherecast-deocclusion-damping.md`)

---

## Completion Notes
**Completed**: 2026-09-22
**Criteria**: 5/5 passing
**Deviations**: None
**Test Evidence**: Logic: test suite at `tests/unit/camera/camera_follow_rig_test.cs` (6 unit tests)
**Code Review**: Complete (Approved)
