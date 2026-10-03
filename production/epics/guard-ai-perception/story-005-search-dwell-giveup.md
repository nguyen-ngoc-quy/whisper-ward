# Story 005: Search Dwell & Give-up Return to Patrol

> **Epic**: Guard AI & Perception (`production/epics/guard-ai-perception/EPIC.md`)  
> **Story ID**: `GUARD-05`  
> **Status**: Complete  
> **Layer**: AI & Navigation  
> **Type**: Logic  
> **Estimate**: 3h (0.4d)  
> **Manifest Version**: 2026-10-03  
> **Owner**: `ai-programmer`  

## Context

**GDD**: `design/gdd/guard-ai-fsm.md` (Investigate give-up, Dwell scan), `design/gdd/perception.md` (R15 Investigate Self-Resolve)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Give-up Rules**:
- Base give-up timeout: $t_{giveup} = 4.0\text{ s}$.
- Upon reaching Last Known Position (LKP) during Investigate without sighting the player, guard initiates search dwell.
- Guard performs local look-around scan ($\pm 45^\circ$) for $4.0\text{ s}$.
- When $t_{giveup}$ expires without renewed perception input, guard state transitions from `Investigate` back to `Patrol`, resuming navigation to the nearest waypoint.

---

## Acceptance Criteria

- [x] **AC-GUARD-15 — LKP Arrival & Dwell Search**: When guard arrives within stopping distance of the investigation point, switches to dwell search, scanning left and right.
- [x] **AC-GUARD-16 — Give-up Expiry & Patrol Resumption**: If no LOS or noise is detected for $4.0\text{ s}$ during dwell search, state returns to `Patrol` and guard resumes original waypoint patrol route.
