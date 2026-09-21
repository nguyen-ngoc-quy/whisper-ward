# Architecture Traceability Index: Whisper Ward

> **Last Updated**: 2026-09-21  
> **Target Engine**: Unity 6 LTS (`6000.3.17f1`)  
> **Status**: Verified by `/architecture-review` (Verdict: PASS)  
> **Coverage Goal**: 100% Foundation Layer Coverage for Technical Setup Gate (ACHIEVED)  

---

## 1. Coverage Summary

| Metric | Count | Percentage |
| :--- | :--- | :--- |
| **Total Technical Requirements** | **46** | **100.0%** |
| **Covered by Accepted ADRs** | **22** | **47.8%** |
| **Foundation Layer Requirements** | **9 / 9** | **100.0% (Zero Foundation Gaps)** |
| **Core Layer Requirements** | **4 / 10** | **40.0%** (6 planned in ADR-0007..008) |
| **Feature Layer Requirements** | **7 / 19** | **36.8%** (12 planned in ADR-0009..011) |
| **Presentation Layer Requirements**| **5 / 8** | **62.5%** (3 planned in ADR-0012) |
| **Unplanned Coverage Gaps** | **0** | **0.0%** |

---

## 2. Full Requirements Traceability Matrix

### Foundation Layer (Tier 1 — Blocking for Pre-Production)

| Requirement ID | GDD Source | System | Technical Requirement Specification | ADR Coverage | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TR-FOUND-001** | `event-messaging-bus.md` | #15 Event Bus | Struct-based zero-allocation event dispatch with type-safe subscription keys. | `ADR-0001` | ✅ **Covered (Accepted)** |
| **TR-FOUND-002** | `event-messaging-bus.md` | #15 Event Bus | Deterministic ordering tuple: `(virtual_time, priority, sequence_id)`. | `ADR-0001` | ✅ **Covered (Accepted)** |
| **TR-FOUND-003** | `event-messaging-bus.md` | #15 Event Bus | Ingress deduplication ring buffer (capacity 4096) with `fact_id` tracking. | `ADR-0001` | ✅ **Covered (Accepted)** |
| **TR-FOUND-004** | `event-messaging-bus.md` | #15 Event Bus | Atomic `attempt_epoch` invalidation: purge queued/inflight events on reset. | `ADR-0001` | ✅ **Covered (Accepted)** |
| **TR-FOUND-005** | `physics-collision-config.md` | #18 Physics | Strict 7-layer physics collision and raycast matrix. | `ADR-0002` | ✅ **Covered (Accepted)** |
| **TR-FOUND-006** | `physics-collision-config.md` | #18 Physics | Global `queriesHitTriggers = false` for perception raycasts. | `ADR-0002` | ✅ **Covered (Accepted)** |
| **TR-FOUND-007** | `physics-collision-config.md` | #18 Physics | Geometric minimum wall thickness invariant of $0.10\text{ m}$. | `ADR-0002` | ✅ **Covered (Accepted)** |
| **TR-FOUND-008** | `input-system.md` | #19 Input | Unified C# Action Asset: WASD, Crouch, Run, Burst throw charge/release. | `ADR-0005` | ✅ **Covered (Accepted)** |
| **TR-FOUND-009** | `input-system.md` | #19 Input | Analog stick response curve with inner deadzone $0.10$, outer deadzone $0.95$. | `ADR-0005` | ✅ **Covered (Accepted)** |

### Core Layer (Tier 2 — Locomotion, Navigation, Camera)

| Requirement ID | GDD Source | System | Technical Requirement Specification | ADR Coverage | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TR-CORE-001** | `player-third-person-controller.md` | #11 Controller | Kinematic speed hierarchy: $V_{\text{crouch}}=1.80 < V_{\text{walk}}=3.60 < V_{\text{run}}=6.25\text{ m/s}$. | `ADR-0006` | ✅ **Covered (Accepted)** |
| **TR-CORE-002** | `player-third-person-controller.md` | #11 Controller | Anti-kiting kinematic invariant: $V_{\text{chase}} / V_{\text{run}} \ge 1.20$. | `ADR-0006` | ✅ **Covered (Accepted)** |
| **TR-CORE-003** | `player-third-person-controller.md` | #11 Controller | Camera-relative planar motion basis transformation. | `ADR-0006` | ✅ **Covered (Accepted)** |
| **TR-CORE-004** | `player-third-person-controller.md` | #11 Controller | Stance transition headroom clearance SphereCast ($R=0.25\text{ m}, H=1.80\text{ m}$). | `ADR-0005` | ✅ **Covered (Accepted)** |
| **TR-CORE-005** | `navmesh-pathfinding.md` | #12 NavMesh | `NavMesh.CalculatePath` non-alloc buffer queries. | `ADR-0007` | ⚠️ Planned (Pre-Prod) |
| **TR-CORE-006** | `navmesh-pathfinding.md` | #12 NavMesh | Spatial corridor clearance enforcement ($\ge 1.50\text{ m}$) for patrol paths. | `ADR-0007` | ⚠️ Planned (Pre-Prod) |
| **TR-CORE-007** | `navmesh-pathfinding.md` | #12 NavMesh | Off-mesh links strictly disabled (planar 2.5D surface navigation only). | `ADR-0007` | ⚠️ Planned (Pre-Prod) |
| **TR-CORE-008** | `camera-cinemachine.md` | #20 Camera | Cinemachine 3rd-person follow rig ($X=0.45\text{ m}, Y=1.65\text{ m}, Z=-3.20\text{ m}$). | `ADR-0008` | ⚠️ Planned (Pre-Prod) |
| **TR-CORE-009** | `camera-cinemachine.md` | #20 Camera | Asymmetric occlusion recovery damping ($\tau_{\text{in}}=0.05\text{s}, \tau_{\text{out}}=0.40\text{s}$). | `ADR-0008` | ⚠️ Planned (Pre-Prod) |
| **TR-CORE-010** | `camera-cinemachine.md` | #20 Camera | Dynamic FOV expansion during active chase ($60^\circ \to 68^\circ, \tau=0.20\text{s}$). | `ADR-0008` | ⚠️ Planned (Pre-Prod) |

### Feature Layer (Tier 3 — AI, Sensing, Mechanics, Grading)

| Requirement ID | GDD Source | System | Technical Requirement Specification | ADR Coverage | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TR-FEAT-001** | `guard-ai-fsm.md` | #1 Guard FSM | Deterministic 3-state FSM: Patrol, Investigate, Chase. | `ADR-0010` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-002** | `guard-ai-fsm.md` | #1 Guard FSM | Monotonic re-anchor budget formula. | `ADR-0010` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-003** | `guard-ai-fsm.md` | #1 Guard FSM | 2.5D Catch Gate cylinder check ($|\Delta Y| \le 1.0\text{ m}, d \le 5.50\text{ m}$). | `ADR-0010` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-004** | `guard-ai-fsm.md` | #1 Guard FSM | Witnessed HideSpot entry authority switches goal mode to `HideSpotFront`. | `ADR-0010` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-005** | `guard-ai-fsm.md` | #1 Guard FSM | Multi-guard chase duration pooling and sequential give-up timers. | `ADR-0010` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-006** | `perception.md` | #2 Perception | Two-component threat tracking per guard: Accumulator $A_i$ and Residual $R_i$. | `ADR-0009` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-007** | `perception.md` | #2 Perception | Distance-scaled accumulator charge rate $dA/dt$. | `ADR-0009` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-008** | `perception.md` | #2 Perception | Dynamic entry threshold marker: $T_{\text{entry}}(R_i) = \max(T_{\text{floor}}, T_{\text{base}} - \kappa R_i)$. | `ADR-0009` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-009** | `perception.md` | #2 Perception | Hearing Linecast occlusion check using SoundOccluder layer mask. | `ADR-0002` | ✅ **Covered (Accepted)** |
| **TR-FEAT-010** | `perception.md` | #2 Perception | Fixed virtual-time tick evaluation independent of render frame fluctuations. | `ADR-0001` | ✅ **Covered (Accepted)** |
| **TR-FEAT-011** | `player-noise.md` | #3 Noise | Ballistic projectile simulation ($v_0=10.0\text{ m/s} @ 30^\circ$) with ground snap. | `ADR-0011` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-012** | `player-noise.md` | #3 Noise | Acoustic hearing emission radii: Walk ($4.0\text{ m}$), Run ($6.0\text{ m}$), Burst ($10.5\text{ m}$). | `ADR-0011` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-013** | `player-noise.md` | #3 Noise | Audible stride ledger with fresh-session commit window ($0.55\text{ s}$). | `ADR-0011` | ⚠️ Planned (Pre-Prod) |
| **TR-FEAT-014** | `player-noise.md` | #3 Noise | Clean distraction scorecard exemption ($0.0\text{ pts}$ direct penalty). | `ADR-0004` | ✅ **Covered (Accepted)** |
| **TR-FEAT-015** | `player-movement-hide.md` | #5 Movement | HideSpot trigger volume state machine with visibility suppression mask. | `ADR-0006` / `ADR-0010` | ✅ **Covered (Accepted)** |
| **TR-FEAT-016** | `player-movement-hide.md` | #5 Movement | Pure-pivot translational lock ($\Delta \vec{p} = \vec{0}$) during spot dwell. | `ADR-0006` | ✅ **Covered (Accepted)** |
| **TR-FEAT-017** | `player-movement-hide.md` | #5 Movement | Standoff positioning contract: `standoff_distance = 1.20 m` from entry node. | `ADR-0006` / `ADR-0010` | ✅ **Covered (Accepted)** |
| **TR-FEAT-018** | `suspicion-meter-grade.md` | #7 Grade | Deterministic score integral: $S = 100 - \sum \text{Penalties} - \int \text{Exposure}$. | `ADR-0004` | ✅ **Covered (Accepted)** |
| **TR-FEAT-019** | `suspicion-meter-grade.md` | #7 Grade | Immutable incident ledger recording detection events, escalations, retries. | `ADR-0004` | ✅ **Covered (Accepted)** |

### Presentation Layer (Tier 4 — UI, Telemetry, Audio Feedback)

| Requirement ID | GDD Source | System | Technical Requirement Specification | ADR Coverage | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TR-PRES-001** | `suspicion-meter-grade.md` | #7 Meter UI | Planar azimuth directional chevron projection ($R_x=240, R_y=160$). | `ADR-0004` | ✅ **Covered (Accepted)** |
| **TR-PRES-002** | `suspicion-meter-grade.md` | #7 Meter UI | Dominant threat selection ratio: $r_{\text{threat}}^* = \arg\max_i (A_i / T_{\text{entry}, i})$. | `ADR-0004` | ✅ **Covered (Accepted)** |
| **TR-PRES-003** | `suspicion-attribution-telemetry.md` | #10 Telemetry | Zero-GC struct ring buffer (capacity 2048) recording FSM transitions. | `ADR-0012` | ⚠️ Planned (Pre-Prod) |
| **TR-PRES-004** | `suspicion-attribution-telemetry.md` | #10 Telemetry | Async JSON exporter and WebGL LocalStorage sync under key. | `ADR-0012` | ⚠️ Planned (Pre-Prod) |
| **TR-PRES-005** | `suspicion-attribution-telemetry.md` | #10 Telemetry | Forensic verification scores evaluating Claims 1, 2, and 3. | `ADR-0012` | ⚠️ Planned (Pre-Prod) |
| **TR-PRES-006** | `audio-ui-feedback.md` | #13 Audio | Dynamic DSP Low-Pass Filter ($800\text{ Hz}$ interior, $1200\text{ Hz}$ occluded). | `ADR-0003` | ✅ **Covered (Accepted)** |
| **TR-PRES-007** | `audio-ui-feedback.md` | #13 Audio | Dual-voice footstep audio pool (capacity 24 voices) with acoustics. | `ADR-0003` | ✅ **Covered (Accepted)** |
| **TR-PRES-008** | `audio-ui-feedback.md` | #13 Audio | Continuous threat drone tied to dominant threat ratio. | `ADR-0003` | ✅ **Covered (Accepted)** |

---

## 3. Scheduled Pre-Production ADR Roadmap

1. **`ADR-0006`**: *Kinematic Player Controller, Stance FSM & Headroom SphereCast* (Covers `TR-CORE-001..003`, `TR-FEAT-015..017`) — ✅ **Accepted**
2. **`ADR-0007`**: *NavMesh NonAlloc Query Service & 2.5D Catch Gate Verification* (Covers `TR-CORE-005..007`)
3. **`ADR-0008`**: *Cinemachine 3rd-Person Camera Rig & Occlusion SphereCast Damping* (Covers `TR-CORE-008..010`)
4. **`ADR-0009`**: *Perception Dual-Threat Pipeline, Raycast Budget & Dynamic Thresholds* (Covers `TR-FEAT-006..008`)
5. **`ADR-0010`**: *Guard AI 3-State FSM, Re-anchor Budget & Witnessed HideSpot Authority* (Covers `TR-FEAT-001..005`)
6. **`ADR-0011`**: *Player Noise Stride Ledger, Burst Ballistics & Zero-Deduction Distraction* (Covers `TR-FEAT-011..013`)
7. **`ADR-0012`**: *Zero-GC Struct Telemetry Ring Buffer & WebGL LocalStorage Sync* (Covers `TR-PRES-003..005`)
