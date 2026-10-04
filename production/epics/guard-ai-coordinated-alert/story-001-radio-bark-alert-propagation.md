# Story 001: Guard Radio Bark & Coordinated Alert

> **Epic**: Guard AI Coordinated Alert & Radio Bark Network (`production/epics/guard-ai-coordinated-alert/EPIC.md`)  
> **Story ID**: `ALERT-01`  
> **Status**: Complete  
> **Layer**: AI & Navigation  
> **Type**: Logic / Integration  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-04  
> **Owner**: `ai-programmer`  

## Context

**GDD**: `design/gdd/guard-ai-fsm.md` (Alert propagation C3, lines 307 & 495, tuning knobs lines 514..515)  
**Governing ADRs**: `ADR-0001` (Event Bus), `ADR-0007` (NavMesh Pathfinding)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Rules & Contracts**:
- **Radio Bark Broadcast**: When a guard enters `Chase` state, broadcasts `GuardAlertEvent` carrying emitter ID, emitter position, and player target/LKP coordinates.
- **Broadcast Range**: Radio communication range $R_{\text{radio}} = 25.0\text{ m}$.
- **Rate-Limit Cooldown**: Guard radio transmitter enforces $t_{\text{alert\_cooldown}} = 5.0\text{ s}$ cooldown between broadcasts to prevent alert storming.
- **Reception & Redirection**: Unalerted/patrolling guards within $25.0\text{ m}$ of the emitter receive the alert and redirect to `Investigate` the reported LKP with spatial error offset ($r_{\text{investigate\_error}} = 1.50\text{ m}$).
- **Chase-Wins Invariant (R13)**: Guards actively in `Chase` state are immune to peer alerts and will not abandon direct pursuit for rumor.

---

## Acceptance Criteria

- [x] **AC-ALERT-01 — Chase Transition Broadcast**: Guard transitioning to `Chase` fires a radio bark alert with emitter position and player LKP.
- [x] **AC-ALERT-02 — 25.0m Radio Coverage & Cooldown**: Alert reaches guards within $25.0\text{ m}$ radius. A guard cannot broadcast again within $5.0\text{ s}$ cooldown.
- [x] **AC-ALERT-03 — Peer Patrol Redirection with Spatial Error**: Patrolling guards within radio range transition to `Investigate` at target position $\vec{p}_{\text{LKP}} + \vec{\Delta}_{\text{error}}$ where $|\vec{\Delta}_{\text{error}}| \le 1.50\text{ m}$.
- [x] **AC-ALERT-04 — Chase Immunity (R13 Chase-Wins)**: Guards actively in `Chase` ignore incoming peer alerts and retain their direct chase target.

---

## QA Test Cases

- `test_guard_chase_broadcasts_radio_bark_alert`
- `test_radio_bark_rate_limit_cooldown`
- `test_nearby_patrol_guard_receives_alert_and_investigates`
- `test_investigation_target_includes_bounded_spatial_error`
- `test_chasing_guard_ignores_peer_alerts`
- `test_guard_beyond_radio_radius_ignores_alert`
- `test_guard_ignores_own_broadcast`

---

## Test Evidence
- Unit Tests: `tests/unit/ai/guard_radio_bark_alert_test.cs`
- Integration Tests: `tests/integration/ai/guard_alert_propagation_test.cs`
