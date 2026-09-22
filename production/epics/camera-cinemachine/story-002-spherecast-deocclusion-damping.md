# Story 002: Spherecast Deocclusion & Exponential Recovery Damping

> **Epic**: Camera (Cinemachine Rig)
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-22

## Context

**GDD**: `design/gdd/camera-cinemachine.md`
**Requirement**: `TR-CORE-009`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0008: Cinemachine 3rd-Person Follow Rig and Deocclusion Recovery (Secondary: ADR-0002)
**ADR Decision Summary**: Implement real-time spherecast deocclusion against the E20 `World` layer ($R_{\text{cam\_col}} = 0.20\text{ m}$) with maximum 2 iterations per frame for WebGL efficiency. Enforce asymmetric distance damping: instantaneous collapse on collision ($D_{\text{actual}} = \max(D_{\text{min}}, d_{\text{hit}} - R)$ where $D_{\text{min}} = 0.40\text{ m}$), and smooth exponential pull-out ($\tau_{\text{recover}} = 0.25\text{ s}$) when clearance opens. Manage character dither fade threshold ($D \le 0.60\text{ m}$) and vertical leash threshold ($\Delta Y > 2.50\text{ m}$).

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW
**Engine Notes**: `Physics.SphereCastNonAlloc`, Layer `World`. Zero heap allocation (0 B GC).

**Control Manifest Rules (this layer)**:
- Required: SphereCast wall deocclusion with damped recovery: Camera occlusion evaluation uses $R=0.20\text{ m}$ SphereCast against World geometry; snaps inward instantly on hit, recovers outward smoothly with exponential damping ($\tau = 0.25\text{ s}$).
- Required: Bounded deocclusion iterations: WebGL performance limit of max 2 iterations per frame.
- Required: Distance clamp floor $D_{\text{min}} = 0.40\text{ m}$ and nominal clearance $D_{\text{nom}} = 2.80\text{ m}$.
- Forbidden: Never query physics with allocating methods (e.g. `Physics.SphereCastAll` without non-alloc buffer).
- Guardrail: Maximum 0.15 ms CPU budget for deocclusion calculations per frame.

---

## Acceptance Criteria

*From GDD `design/gdd/camera-cinemachine.md`, scoped to this story:*

- [x] **AC-CAM-06 / AC3 — Instantaneous Deocclusion Collapse**: When a static E20 obstacle on `World` layer blocks the line of sight at distance $d_{\text{hit}} < D_{\text{nom}} + R_{\text{cam\_col}}$, camera distance instantly collapses to $D_{\text{actual}} = \max(D_{\text{min}}, d_{\text{hit}} - R_{\text{cam\_col}})$ within $0\text{ ms}$ (same frame).
- [x] **AC-CAM-07 / AC4 — Exponential Distance Recovery Damping**: When occlusion clears ($D_{\text{target}} > D(t)$), distance recovers according to $D(t + \Delta t) = D(t) + (D_{\text{target}} - D(t)) \cdot (1 - e^{-\Delta t / \tau_{\text{recover}}})$ with $\tau_{\text{recover}} = 0.25\text{ s}$, reaching $\ge 95\%$ nominal distance after $0.75\text{ s}$ without frame stutter.
- [x] **AC-CAM-08 / AC8 — Dither Transparency Signal on Tight Crevice**: When camera distance is compressed to $D_{\text{actual}} \le 0.60\text{ m}$, the rig calculates and exposes normalized dither opacity $\text{Opacity}_{\text{dither}} = \text{clamp}((D_{\text{actual}} - 0.40) / 0.20, 0.15, 1.0)$ to prevent character geometry from obstructing the view.
- [x] **AC-CAM-09 / AC9 — Vertical Leash Hard Override on Rapid Descent**: When player vertical displacement relative to camera exceeds leash threshold $\Delta Y_{\text{leash}} = 2.50\text{ m}$ ($V_y < -8.0\text{ m/s}$), vertical position damping is bypassed and camera altitude snaps directly to maintain framing.

---

## Implementation Notes

*Derived from ADR-0008 Implementation Guidelines:*

1. **SphereCast NonAlloc Solver Algorithm**:
   ```csharp
   private readonly RaycastHit[] _hitBuffer = new RaycastHit[2];

   public float EvaluateCameraDistance(Vector3 targetPosition, Vector3 cameraDirection, float currentDistance, float deltaTime)
   {
       float targetDistance = NominalDistance; // 2.80m
       int hits = Physics.SphereCastNonAlloc(
           targetPosition,
           SpherecastRadius, // 0.20m
           cameraDirection,
           _hitBuffer,
           NominalDistance,
           _worldLayerMask,
           QueryTriggerInteraction.Ignore);

       if (hits > 0)
       {
           float closestHit = _hitBuffer[0].distance;
           if (hits > 1 && _hitBuffer[1].distance < closestHit)
           {
               closestHit = _hitBuffer[1].distance;
           }
           targetDistance = Mathf.Max(MinDistance, closestHit - SpherecastRadius); // MinDistance = 0.40m
       }

       if (targetDistance < currentDistance)
       {
           // Instantaneous collapse on collision
           return targetDistance;
       }
       else
       {
           // Exponential damped recovery pull-out
           float alpha = 1.0f - Mathf.Exp(-deltaTime / RecoveryTau); // RecoveryTau = 0.25s
           return currentDistance + (targetDistance - currentDistance) * alpha;
       }
   }
   ```
2. **Dither Opacity Function**:
   ```csharp
   public float CalculateDitherOpacity(float actualDistance)
   {
       if (actualDistance > 0.60f) return 1.0f;
       return Mathf.Clamp((actualDistance - 0.40f) / 0.20f, 0.15f, 1.0f);
   }
   ```
3. **Vertical Leash Enforcement**:
   ```csharp
   if (cameraPosition.y - targetPosition.y > MaxVerticalLeash) // 2.50m
   {
       cameraPosition.y = targetPosition.y + MaxVerticalLeash;
   }
   ```

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: Planar basis calculation, pitch clamping, angular velocity limiter, and `AC-P25` idle decoupling.
- [Story 003]: Dynamic chase FOV scaling and HideSpot virtual camera blend orchestration.

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**[For Logic / Integration stories — automated test specs]:**

- **AC-CAM-06**: Instantaneous Deocclusion Collapse
  - Given: Camera at nominal distance $2.80\text{ m}$, obstacle hit detected at $d_{\text{hit}} = 1.50\text{ m}$.
  - When: `EvaluateCameraDistance` is called with $\Delta t = 0.0166\text{ s}$.
  - Then: Returned distance collapses immediately to $\max(0.40, 1.50 - 0.20) = 1.30\text{ m}$ in 0 frames.
  - Edge cases: Hit at $d_{\text{hit}} = 0.30\text{ m}$ clamps to floor $0.40\text{ m}$.
- **AC-CAM-07**: Exponential Damped Pull-Out
  - Given: Current distance compressed to $1.30\text{ m}$, obstacle removed ($D_{\text{target}} = 2.80\text{ m}$).
  - When: Simulation advances at $60\text{ fps}$ for $t = 0.75\text{ s}$ ($3\tau_{\text{recover}}$).
  - Then: Distance increases monotonically and reaches $\ge 2.72\text{ m}$ ($\ge 95\%$ of nominal).
- **AC-CAM-08**: Dither Opacity Response
  - Given: Actual distance evaluates to $0.40\text{ m}$, $0.50\text{ m}$, and $0.70\text{ m}$.
  - When: `CalculateDitherOpacity` is executed.
  - Then: Returns $0.15$ ($15\%$ opacity / $85\%$ transparent), $0.50$, and $1.00$ ($100\%$ opaque) respectively.
- **AC-CAM-09**: Vertical Leash Snap
  - Given: Target drops with $\Delta Y = 3.20\text{ m}$ ($> 2.50\text{ m}$ leash).
  - When: Camera vertical position constraint is evaluated.
  - Then: Camera vertical offset is snapped to exactly $\Delta Y = 2.50\text{ m}$.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/camera/camera_deocclusion_test.cs` — must exist and pass

**Status**: [x] Verified
- Automated unit test suite: `tests/unit/camera/camera_deocclusion_test.cs` (7 tests, 100% passing).
- Validates instantaneous deocclusion collapse on collision (AC-CAM-06), exponential recovery damping (AC-CAM-07), character dither opacity curve (AC-CAM-08), and vertical leash hard override constraint (AC-CAM-09).

---

## Dependencies

- Depends on: Story 001 (`story-001-follow-rig-planar-basis.md`)
- Unlocks: Story 003 (`story-003-chase-fov-hidespot-blend.md`)

---

## Completion Notes
**Completed**: 2026-09-22
**Criteria**: 4/4 passing
**Deviations**: None
**Test Evidence**: Logic: test suite at `tests/unit/camera/camera_deocclusion_test.cs` (7 unit tests)
**Code Review**: Complete (Approved)
