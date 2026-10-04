# Story 001: Hide Spot & Sanctuary Interaction Volume

> **Epic**: Player Movement & Hide (`production/epics/player-movement-hide/EPIC.md`)  
> **Story ID**: `HIDE-01`  
> **Status**: Complete  
> **Layer**: Core Gameplay  
> **Type**: Logic / Integration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-04  
> **Owner**: `gameplay-programmer`  

## Context

**GDD**: `design/gdd/player-movement-hide.md` (System #5 Player Movement & Hide)  
**Governing ADRs**: `ADR-0002` (Physics Collision), `ADR-0006` (Player Locomotion FSM), `ADR-0008` (Camera Orbit Rig)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Constraints**:
- `interior_position`: authored reference coordinate occupied by the player capsule while hidden.
- `spot_front_anchor` + `hold_vector`: position in front of aperture facing outward.
- `guard_hold`: `spot_front_anchor + hold_vector * r_guard` ($r_{\text{guard}} = 0.40\text{ m}$).
- Stand-off distance: $\text{EuclidXZ}(\text{guard\_hold}, \text{interior\_position}) \le 5.10\text{ m}$ (within certified catch range $5.50\text{ m}$).
- Pure pivot: while hidden, translation is locked ($\Delta \vec{p} == 0$), orientation/rotation is permitted at $0\text{ dB}$ noise.
- Sanctuary vs Witnessed Trap:
  - If entered unwitnessed (`IsWitnessedEntry == false`), player is hidden from vision, guard cannot initiate capture.
  - If entered witnessed during Chase (`IsWitnessedEntry == true`), chasing guard moves to `guard_hold`, stays for $t_{\text{spotfront\_verify}} = 1.5\text{ s}$, and captures the player if still inside.

---

## Acceptance Criteria

- [x] **AC-HIDE-01 — Entry & Translation Lock**: Entering a hide spot teleports/moves the player capsule to `interior_position`, sets state to `Occupied`, locks translation $\Delta \vec{p} == 0$, and maintains $0\text{ dB}$ noise for pure-pivot rotation.
- [x] **AC-HIDE-02 — Sanctuary Protection**: When entered unwitnessed, guard vision raycasts and detection logic ignore the player capsule, providing complete immunity from detection.
- [x] **AC-HIDE-03 — Witnessed Trap & Dwell Capture**: When entered witnessed under active Chase, the pursuing guard navigates to `guard_hold`, dwells for $1.5\text{ s}$, and triggers `OnPlayerCaptured` if the player remains inside.
- [x] **AC-HIDE-04 — Exit & Boundary Hysteresis**: Exiting restores normal player locomotion and noise emission, sets state to `Empty`, and aborts guard dwell capture. Trigger boundary oscillations within $0.06\text{ s}$ emit at most 1 state transition pair.

---

## QA Test Cases

- `test_hidespot_entry_sets_occupied_and_locks_translation`
- `test_hidespot_unwitnessed_entry_provides_sanctuary`
- `test_hidespot_witnessed_chase_entry_triggers_capture`
- `test_hidespot_exit_cleans_up_to_empty`
- `test_hidespot_boundary_hysteresis_prevents_flapping`

---

## Test Evidence
- Unit Tests: `tests/unit/gameplay/hidespot_interaction_test.cs`
- Integration Tests: `tests/integration/gameplay/hidespot_guard_sanctuary_test.cs`
