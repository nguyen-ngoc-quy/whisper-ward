# Story 002: Core Audio Infrastructure & Sound Manager

> **Epic**: Audio & Visual Feedback (`production/epics/audio-ui-feedback/EPIC.md`)  
> **Story ID**: `AUDIO-01`  
> **Status**: Complete  
> **Layer**: Foundation / Audio Architecture  
> **Type**: Logic / Audio  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-27  
> **Owner**: `engine-programmer`  

---

## Context

**GDD**: `design/gdd/audio-ui-feedback.md` (System #13 Core Rules lines 39..75)  
**Governing ADRs**: `ADR-0003` (Audio Virtual Timestamp Boundary), `ADR-0001` (Event Bus)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Audio Architecture Contracts**:
1. **4-Bus Audio Hierarchy**:
   - `Master`: Hardware output limiter ceiling $\le 0\text{ dBFS}$.
   - `SFX_Diegetic`: 3D spatialized sound sources within game world (footsteps, projectiles, doors, guard vocalizations).
   - `Feedback_NonDiegetic`: 2D psychological and interface feedback cues (stingers, heartbeat, UI clicks).
   - `Ambience`: Background room tone with automatic ducking ($-6\text{ dB}$) during active chase sequences.
2. **AudioSource Voice Pooling & Zero GC**:
   - Pre-allocated voice pool (capacity 16 voices) with priority-based voice stealing. Zero managed heap allocations during sound playback.
3. **Epoch & Scene Reset Cleanup**:
   - `CancelAllVoices()` instantly stops lingering active audio voices upon player capture or scene restart to prevent ghost audio cues across runs.

---

## Acceptance Criteria

- [x] **AC-AUD-01 — Bus Routing & Volume Hierarchy**: `AudioManager` initializes audio buses (Master, SFX, Feedback, Ambience) and routes playback requests to the correct bus.
- [x] **AC-AUD-02 — Voice Pooling & Zero Allocations**: Sound requests draw from a pre-allocated `AudioSource` pool without `Instantiate` or garbage collection allocations.
- [x] **AC-AUD-03 — Life-cycle Reset & Voice Cancellation**: Calling `StopAll()` or resetting the mission epoch immediately terminates all active playing voices.

---

## QA Test Cases

- `test_audio_manager_routes_cues_to_specified_bus`
- `test_audio_manager_reuses_pooled_audio_sources_without_allocation`
- `test_audio_manager_voice_stealing_respects_playback_priority`
- `test_audio_manager_stop_all_clears_active_voices_cleanly`

---

## Test Evidence
- Unit Tests: `tests/unit/audio/audio_manager_test.cs`
