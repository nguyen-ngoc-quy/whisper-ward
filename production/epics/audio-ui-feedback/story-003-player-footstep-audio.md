# Story 003: Player Dual-Voice Footstep Audio System

> **Epic**: Audio & Visual Feedback (`production/epics/audio-ui-feedback/EPIC.md`)  
> **Story ID**: `AUDIO-02`  
> **Status**: Complete  
> **Layer**: Gameplay / Sensory Feedback  
> **Type**: Logic / Audio  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-27  
> **Owner**: `gameplay-programmer`  

---

## Context

**GDD**: `design/gdd/audio-ui-feedback.md` (Dual-Voice Footstep Container lines 63..75), `design/gdd/player-noise.md`  
**Governing ADRs**: `ADR-0003` (Audio Virtual Timestamp Boundary)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Footstep Audio Contracts**:
1. **Event Synchronicity**:
   - Footstep playback is directly driven by `PlayerFootstepNoiseEmitter.OnStepCommitted` rather than animation clip events, ensuring mathematical frame-accurate audio emission.
2. **Dual-Voice Blend Container**:
   - Combines `body` voice (low-frequency weight: 60–250 Hz) and `accent` voice (high-frequency surface friction: 500–4000 Hz).
3. **Stance Volume & Pitch Modulations**:
   - `Crouch`: Subdued volume ($-12\text{ dB}$ or volume factor 0.25), slower pacing, 0 acoustic detection range.
   - `Walk`: Balanced volume (0 dB reference or volume factor 0.65), medium cadence.
   - `Run`: Heavy volume ($+4\text{ dB}$ or volume factor 1.0), rapid cadence, high pitch impact.
   - Micro-pitch variance ($\pm 5\%$) to eliminate acoustic monotony.

---

## Acceptance Criteria

- [x] **AC-AUD-04 — Step Event Audio Triggering**: Listening to player step commit events triggers footstep sound playback without frame delay.
- [x] **AC-AUD-05 — Stance Volume Scaling**: Crouch stance plays at quietest volume, Walk at intermediate, and Run at highest volume.
- [x] **AC-AUD-06 — Pitch & Timbre Randomization**: Each successive step applies subtle pitch modulation ($\pm 5\%$) to prevent robotic repetition.

---

## QA Test Cases

- `test_player_footstep_audio_plays_on_step_event`
- `test_footstep_volume_scales_with_player_locomotion_stance`
- `test_footstep_pitch_randomizes_within_safety_margin`
- `test_crouch_step_volume_attenuates_appropriately`

---

## Test Evidence
- Unit Tests: `tests/unit/audio/player_footstep_audio_test.cs`
