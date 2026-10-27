# Story 005: World Interaction & Ambient Audio

> **Epic**: Audio & Visual Feedback (`production/epics/audio-ui-feedback/EPIC.md`)  
> **Story ID**: `AUDIO-04`  
> **Status**: Complete  
> **Layer**: Level & Audio Presentation  
> **Type**: Logic / Audio  
> **Estimate**: 3h (0.4d)  
> **Manifest Version**: 2026-10-27  
> **Owner**: `level-designer` / `sound-designer`  

---

## Context

**GDD**: `design/gdd/audio-ui-feedback.md` (System #13 Ambience lines 49..50), `design/gdd/player-noise.md` (Burst impact)  
**Governing ADRs**: `ADR-0003` (Audio Virtual Timestamp Boundary)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Audio Interaction Cues**:
1. **Keycard Pickup SFX**:
   - High-tech electronic chime / confirm tone upon collecting objective keycard.
2. **Locked Security Gate Opening**:
   - Heavy mechanical latch unseal + motor sliding door sound when gate is unlocked.
3. **Burst Distraction Impact**:
   - Ceramic / metallic landing impact clang upon projectile landing and emitting noise pulse.
4. **Cold Watch Ambience Tone**:
   - Continuous subtle low drone / ventilation wind loop establishing cold atmospheric tension.

---

## Acceptance Criteria

- [x] **AC-AUD-10 — Keycard & Objective Interaction SFX**: Picking up the keycard plays crisp positive confirmation sound.
- [x] **AC-AUD-11 — Security Gate Unlocking Audio**: Gate transition from locked to open plays mechanical door opening sound.
- [x] **AC-AUD-12 — Burst Noise Clang & Continuous Ambience**: Projectile landing emits impact sound; ambient loop plays continuously in background.

---

## QA Test Cases

- `test_keycard_pickup_plays_objective_sound`
- `test_security_gate_unlock_plays_sliding_door_sound`
- `test_burst_projectile_landing_plays_impact_sound`
- `test_ambient_sound_loops_seamlessly`

---

## Test Evidence
- Unit Tests: `tests/unit/audio/world_interaction_audio_test.cs`
