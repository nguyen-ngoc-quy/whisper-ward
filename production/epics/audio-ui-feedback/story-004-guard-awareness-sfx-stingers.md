# Story 004: Guard Awareness SFX & Alert Stingers

> **Epic**: Audio & Visual Feedback (`production/epics/audio-ui-feedback/EPIC.md`)  
> **Story ID**: `AUDIO-03`  
> **Status**: Complete  
> **Layer**: AI & Feedback / Presentation  
> **Type**: Logic / Audio  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-27  
> **Owner**: `gameplay-programmer`  

---

## Context

**GDD**: `design/gdd/audio-ui-feedback.md` (System #13 Guard Audio & Feedback lines 45..50), `design/gdd/guard-ai-fsm.md`  
**Governing ADRs**: `ADR-0003` (Audio Virtual Timestamp Boundary), `ADR-0001` (Event Bus)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Awareness Audio Cues**:
1. **Investigate State Sound**:
   - Guard curiosity murmur / low synth chime triggered on transition to `GuardState.Investigate`.
2. **Radio Bark Alert SFX**:
   - Diegetic radio squelch / vocal bark when `GuardRadioAlertTransceiver` broadcasts alert ($25\text{ m}$ radius).
3. **Chase Alert Stinger**:
   - Sharp non-diegetic metal stinger / percussion sting upon escalation to `GuardState.Chase`.
   - Dynamic Ducking: Lowers ambient background audio by $-6\text{ dB}$ while chase is active.
4. **Capture Defeat Sound**:
   - Heavy low brass / arrest sting when mission transitions to `MissionLifecycleState.Captured`.

---

## Acceptance Criteria

- [x] **AC-AUD-07 — State Escalation SFX**: FSM transition to Investigate triggers curiosity audio cue; transition to Chase triggers sharp stinger.
- [x] **AC-AUD-08 — Radio Alert Audio Broadcast**: Radio bark alert transmission plays 3D spatialized radio static / voice bark at guard position.
- [x] **AC-AUD-09 — Ambient Ducking During Chase**: Active chase reduces ambient audio volume to heighten psychological tension.

---

## QA Test Cases

- `test_guard_investigate_state_triggers_curiosity_sfx`
- `test_guard_chase_state_triggers_alert_stinger_and_ducking`
- `test_radio_bark_alert_plays_spatial_audio_cue`
- `test_capture_state_triggers_defeat_audio_cue`

---

## Test Evidence
- Unit Tests: `tests/unit/audio/guard_awareness_audio_test.cs`
