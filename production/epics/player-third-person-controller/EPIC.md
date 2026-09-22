# Epic: Player Third-Person Controller

> **Layer**: Core  
> **GDD**: `design/gdd/player-third-person-controller.md`  
> **Architecture Module**: `PlayerThirdPersonController` (`WhisperWard.Core.Locomotion`)  
> **Status**: Ready  
> **Stories**: 3 stories (`story-001`, `story-002`, `story-003`) — Ready  

## Overview

The Player Third-Person Controller (System #11) implements the physical embodiment, stance state machine, and authoritative kinematic locomotion for the player character in Whisper Ward. Operating strictly on ground-plane 2.5D planar movement (enforcing Anti-Pillar scope discipline: no jumping, climbing, vaulting, or cover magnetism), it translates raw input vectors into camera-relative planar velocities, manages feet-anchored capsule resizing between Stand ($1.80\text{ m}$) and Crouch ($0.95\text{ m}$), evaluates upward headroom clearance via `Physics.OverlapCapsuleNonAlloc` against the E20 LayerMask before uncrouching, and guarantees the anti-kiting kinematic invariant ($V_{\text{chase}} / V_{\text{run}} \ge 1.20$). It encapsulates state in an immutable, zero-allocation `PlayerLocomotionSnapshot` consumed by Perception and Player Noise systems.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|---|---|---|
| `ADR-0006: Kinematic Player Controller and Stance State Machine` | Kinematic `CharacterController` with deterministic linear slew ($a_{\text{max}} = 78.125\text{ m/s}^2$), camera yaw basis projection with pitch degeneracy fallbacks, feet-anchored center scaling ($y = \text{height}/2$), inset stand headroom probe, and 0 B GC snapshots. | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|---|---|---|
| `TR-CORE-001` | Kinematic speed hierarchy: $V_{\text{crouch}} = 1.80\text{ m/s} < V_{\text{walk}} = 3.60\text{ m/s} < V_{\text{run}} = 6.25\text{ m/s}$. | `ADR-0006` ✅ |
| `TR-CORE-002` | Anti-kiting kinematic invariant: $V_{\text{chase}} / V_{\text{run}} \ge 1.20$ ($7.50 / 6.25 = 1.20$). | `ADR-0006` ✅ |
| `TR-CORE-003` | Camera-relative planar motion basis transformation from Cinemachine forward yaw vector. | `ADR-0006` ✅ |
| `TR-CORE-004` | Stance transition headroom validation: Uncrouch clearance `OverlapCapsuleNonAlloc` against E20. | `ADR-0006` ✅ |
| `TR-FEAT-015` | Support `HideSpot` trigger containment state with full perception LOS suppression. | `ADR-0006` ✅ |
| `TR-FEAT-016` | Enforce pure-pivot translational lock ($\Delta \vec{p} = \vec{0}$) during `HideSpot` dwell. | `ADR-0006` ✅ |
| `TR-FEAT-017` | Standardized standoff distance ($1.20\text{ m}$) from spot entrance node upon exit. | `ADR-0006` ✅ |

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | Kinematic Locomotion & Linear Slew FSM | Logic | Complete | ADR-0006 |
| 002 | Camera-Relative Planar Basis & Facing Orientation | Logic | Complete | ADR-0006 |
| 003 | Feet-Anchored Capsule Scaling, Headroom Clearance Probe & HideSpot State | Logic | Complete | ADR-0006 |

## Definition of Done

This epic is complete when:
- All stories under this epic are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/player-third-person-controller.md` (AC-P1 through AC-P25) are verified
- `PlayerThirdPersonController.cs` implements `IPlayerController` with zero GC allocations during active locomotion and snapshot polling
- Linear speed slewing reaches target speed within $0.08\text{ s}$ without velocity overshooting or oscillation
- Feet anchoring maintains exact ground contact ($y = 0$) across stance transitions without lifting or floor penetration
- Automated EditMode and PlayMode tests confirm speed ratios, clearance probe insetting, and hide spot transitions

## Next Step

Run `/story-readiness production/epics/player-third-person-controller/story-001-kinematic-locomotion-slew.md` then `/dev-story`.
