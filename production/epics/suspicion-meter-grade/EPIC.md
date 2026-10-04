# Epic: Suspicion Meter HUD & Grade Operator

> **Layer**: UI & Presentation / Gameplay Feedback  
> **GDD Reference**: `design/gdd/suspicion-meter-grade.md` (System #6 Suspicion Meter / Grade)  
> **Governing ADRs**: `ADR-0004` (Suspicion Meter & Grade Operator), `ADR-0001` (Event Bus)  
> **Status**: In Progress  
> **Stories**: 1 story (`UI-SUSP-01` in Sprint 04)  

## Overview

The Suspicion Meter & Grade Operator epic provides real-time situational awareness and tension feedback to the player. The Suspicion Meter HUD translates internal guard AI perception data (suspicion accumulator $A$ and residual wariness $R$) into readable UI telemetry:
- **Dominant Threat Selection**: Identifies the most dangerous guard based on normalized threat ratio $r_{\text{threat}} = A / T_{\text{entry}}$.
- **Zero-Threat Quiet Region**: Hides HUD completely when all guards are unalerted ($A = 0.0$).
- **Planar Azimuth Threat Chevron**: Projects a 360-degree directional arrow on screen indicating guard direction without camera inversion singularities.
- **Dynamic Threshold Notch**: Shows the sinking investigate trigger threshold as residual wariness accumulates.

## Stories

| # | Story | Type | Status | Owner |
|---|-------|------|--------|-------|
| `UI-SUSP-01` | Suspicion Meter & Threat Chevron HUD | UI / Logic | Complete | ui-programmer |

## Definition of Done

This epic is complete when:
- HUD displays the dominant guard threat ratio accurately.
- Quiet region is enforced with complete HUD suppression when $A = 0.0$.
- Directional chevron computes continuous 360° azimuth without flipping behind camera.
- Threshold marker visually updates with residual wariness $R$.
- Automated unit tests verify threat ranking, tie-breaking, azimuth projection, and quiet state suppression.
