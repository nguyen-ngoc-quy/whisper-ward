# Story 001: Vision Cone Sensor & Line-of-Sight Sensing Engine

> **Epic**: Guard AI & Perception (`production/epics/guard-ai-perception/EPIC.md`)  
> **Story ID**: `GUARD-01`  
> **Status**: Complete  
> **Layer**: AI & Perception  
> **Type**: Logic / Integration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-03  
> **Owner**: `ai-programmer`  

## Context

**GDD**: `design/gdd/perception.md` (R1 Sensing Cadence, R2 Vision Cone, R3 Perceived Position, E20 Sensing Mask)  
**Governing ADRs**: `ADR-0002` (Physics Query Service)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Constraints**:
- Vision Range $R_{vis} = 12.0\text{ m}$, Horizontal FOV $= 100^\circ$ ($\pm 50^\circ$ from forward vector).
- Sensing tick cadence: 5 Hz ($T_{sample} = 0.2\text{ s} = 200\text{ ms}$). No per-frame raycasting in `Update()`.
- Physics Linecast uses E20 LayerMask (Layer 20 `World`), `QueryTriggerInteraction.Ignore`.
- Eye height: Guard eye positioned at $Y = 1.60\text{ m}$ relative to feet.
- Target height: Evaluates to Player stance center ($Y = 1.35\text{ m}$ for Stand, $Y = 0.80\text{ m}$ for Crouch).
- Zero GC allocations on tick hot-path.

---

## Acceptance Criteria

- [x] **AC-GUARD-01 — Vision Cone Field of View & Distance**: Guard detects target within straight-line distance $d \le 12.0\text{ m}$ and horizontal angle $|\theta| \le 50.0^\circ$ relative to guard facing. Rejects targets at $d > 12.0\text{ m}$ or angle $> 50.0^\circ$.
- [x] **AC-GUARD-02 — E20 Occlusion Mask**: Linecast from guard eye to player target point checks against Layer 20 `World`. If an obstacle intersects the segment, `hasLOS` is false. Ignores triggers (Layer 10 HideSpot, etc.).
- [x] **AC-GUARD-03 — Stance-Adaptive Occlusion**: When player is crouched behind low cover ($H = 1.0\text{ m}$), linecast to player crouch height ($0.80\text{ m}$) hits cover and reports no LOS, whereas standing player ($1.35\text{ m}$) is detected over low cover.
- [x] **AC-GUARD-04 — 5 Hz Discrete Cadence & Events**: Sensing evaluates every $0.20\text{ s}$ ($5\text{ Hz}$). Emits `OnLOSGained` and `OnLOSLost` events on state transitions. Maintains zero managed heap allocations during continuous evaluation.

---

## Implementation Notes

1. Component: `VisionConeSensor.cs` in `src/AI/Perception/` (or `src/Core/AI/`):
   - Configurable: `VisionRange` (default 12.0m), `VisionFOV` (default 100.0f), `TickInterval` (0.2s).
   - Method: `EvaluateVisibility(Vector3 targetPosition, bool isCrouching, out float distance, out float angle)`.
   - Method: `Tick(float deltaTime)`.
2. Unit and Integration tests in `tests/unit/ai/` and `tests/integration/ai/`.
