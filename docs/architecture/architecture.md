# Whisper Ward — Master Technical Architecture

## Document Status
- **Version**: 1.0.0
- **Last Updated**: 2026-09-21
- **Target Engine**: Unity 6 LTS (6000.3.17f1)
- **Target Platforms**: PC Windows (Primary), WebGL Demo (Secondary / Portfolio)
- **Status**: APPROVED — Master Technical Architecture Blueprint
- **Technical Director Sign-Off**: 2026-09-21 — APPROVED
- **Lead Programmer Feasibility**: FEASIBLE (Lean Mode Evaluation)
- **GDDs Covered**:
  - System #0: Game Concept (`design/gdd/game-concept.md`)
  - System #1: Guard AI FSM (`design/gdd/guard-ai-fsm.md`)
  - System #2: Perception Systems (`design/gdd/perception.md`)
  - System #3: Player Noise (`design/gdd/player-noise.md`)
  - System #5: Player Movement & Hide (`design/gdd/player-movement-hide.md`)
  - System #7: Suspicion Meter & Grade Operator (`design/gdd/suspicion-meter-grade.md`)
  - System #10: Suspicion Attribution Telemetry (`design/gdd/suspicion-attribution-telemetry.md`)
  - System #11: Player Third-Person Controller (`design/gdd/player-third-person-controller.md`)
  - System #12: NavMesh / Pathfinding (`design/gdd/navmesh-pathfinding.md`)
  - System #13: Audio & UI Feedback (`design/gdd/audio-ui-feedback.md`)
  - System #15: Event / Messaging Bus (`design/gdd/event-messaging-bus.md`)
  - System #18: Physics & Collision Config (`design/gdd/physics-collision-config.md`)
  - System #19: Input System (`design/gdd/input-system.md`)
  - System #20: Camera Cinemachine Rig (`design/gdd/camera-cinemachine.md`)
- **ADRs Referenced**:
  - `ADR-0001`: Deterministic Event/Messaging Bus (Accepted)
  - `ADR-0002`: Shared Physics and Collision Contract (Accepted)
  - `ADR-0003`: Audio Virtual Timestamp Boundary (Accepted)
  - `ADR-0004`: Suspicion Meter and Room Grade (Accepted)
  - `ADR-0005`: Input Action Asset & Stance Buffering Contract (Accepted)

---

## 1. Engine Knowledge Gap Summary

- **Engine Target**: Unity 6 LTS (`6000.3.17f1`)
- **LLM Training Knowledge Cutoff**: January 2026 (All Unity 6 LTS core APIs are within LLM training data)
- **Risk Level**: **LOW** across all technical domains

| Engine Domain | Package / API Reference | Risk Level | Architectural Invariant & Guardrails |
| :--- | :--- | :--- | :--- |
| **Physics** | PhysX 4.x / Unity Physics (`Linecast`, `SphereCast`) | **LOW** | `queriesHitTriggers = false` enforced project-wide for perception. Wall thickness $\ge 0.10\text{ m}$. |
| **Input** | Unity Input System (v1.8+) | **LOW** | Action map asset (`WhisperWardInputActions`), C# callbacks, zero-allocation input polling. |
| **Navigation** | Unity AI Navigation (`NavMesh.CalculatePath`) | **LOW** | Pre-allocated `NavMeshPath` buffer, zero GC in update loop. Planar 2.5D navigation only, off-mesh links disabled. |
| **Rendering** | Universal Render Pipeline (URP 17) | **LOW** | Forward+ renderer, draw call budget $\le 1000$ (PC), simplified shaders for WebGL target. |
| **Camera** | Cinemachine v3 (`CinemachineCamera`) | **LOW** | Shoulder follow framing ($X=0.45, Y=1.65, Z=-3.20\text{ m}$), asymmetric de-occlusion damping ($\tau_{\text{in}}=0.05\text{s}, \tau_{\text{out}}=0.40\text{s}$). |
| **Audio** | `AudioSource`, `AudioMixer`, DSP LPF | **LOW** | Low-Pass Filter dynamic cutoff ($800\text{ Hz}$ in HideSpot, $1200\text{ Hz}$ occluded). 24 voice pool. |
| **Core / Scripting**| C# 9.0 / .NET Standard 2.1 | **LOW** | Virtual Clock independent of render framerate. Zero-GC struct ring buffers for events and telemetry. |

---

## 2. Technical Requirements Baseline

| Req ID | System | Layer | Technical Requirement Specification |
| :--- | :--- | :--- | :--- |
| **TR-FOUND-001** | #15 Event Bus | Foundation | Struct-based zero-allocation event dispatch with type-safe subscription keys and snapshot lifecycle. |
| **TR-FOUND-002** | #15 Event Bus | Foundation | Deterministic ordering tuple: `(virtual_time, priority, sequence_id)`. |
| **TR-FOUND-003** | #15 Event Bus | Foundation | Ingress deduplication ring buffer (capacity 4096) with `fact_id` tracking. |
| **TR-FOUND-004** | #15 Event Bus | Foundation | Atomic `attempt_epoch` invalidation: purge all queued/inflight events upon checkpoint reload or player respawn. |
| **TR-FOUND-005** | #18 Physics | Foundation | Strict 7-layer physics matrix: Player, Guard, World, VisionOccluder, SoundOccluder, HideSpotTrigger, PickupTrigger. |
| **TR-FOUND-006** | #18 Physics | Foundation | Global `queriesHitTriggers = false` for perception raycasts to prevent trigger volume occlusion anomalies. |
| **TR-FOUND-007** | #18 Physics | Foundation | Geometric minimum wall thickness invariant of $0.10\text{ m}$ across all collision geometry. |
| **TR-FOUND-008** | #19 Input | Foundation | Unified C# Action Asset: WASD locomotion, Crouch toggle/hold, Run hold, Burst throw charge/release. |
| **TR-FOUND-009** | #19 Input | Foundation | Analog stick response curve with inner deadzone $0.10$ and outer deadzone $0.95$. |
| **TR-CORE-001** | #11 Controller | Core | Kinematic speed hierarchy: $V_{\text{crouch}} = 1.80\text{ m/s} < V_{\text{walk}} = 3.60\text{ m/s} < V_{\text{run}} = 6.25\text{ m/s}$. |
| **TR-CORE-002** | #11 Controller | Core | Anti-kiting kinematic invariant: $V_{\text{chase}} / V_{\text{run}} \ge 1.20$ ($7.50 / 6.25 = 1.20$). |
| **TR-CORE-003** | #11 Controller | Core | Camera-relative planar motion basis transformation from Cinemachine forward vector. |
| **TR-CORE-004** | #11 Controller | Core | Stance transition headroom validation: Uncrouch clearance SphereCast ($R=0.25\text{ m}, H=1.80\text{ m}$). |
| **TR-CORE-005** | #12 NavMesh | Core | Agent-parameterized `NavMesh.CalculatePath` invocation using pre-allocated `NavMeshPath` buffers (0 GC). |
| **TR-CORE-006** | #12 NavMesh | Core | Spatial corridor clearance enforcement: minimum navigable width $\ge 1.50\text{ m}$ for all patrol paths. |
| **TR-CORE-007** | #12 NavMesh | Core | Off-mesh links strictly disabled (planar 2.5D surface navigation only). |
| **TR-CORE-008** | #20 Camera | Core | Cinemachine 3rd-person follow rig ($X=0.45\text{ m}, Y=1.65\text{ m}, Z=-3.20\text{ m}$). |
| **TR-CORE-009** | #20 Camera | Core | Asymmetric occlusion recovery damping: fast push-in ($\tau_{\text{in}}=0.05\text{s}$), smooth pull-out ($\tau_{\text{out}}=0.40\text{s}$). |
| **TR-CORE-010** | #20 Camera | Core | Dynamic FOV expansion during active chase ($60^\circ \to 68^\circ, \tau=0.20\text{s}$). |
| **TR-FEAT-001** | #1 Guard FSM | Feature | Deterministic 3-state FSM: Patrol, Investigate, Chase. Zero intermediate hunting/alert states. |
| **TR-FEAT-002** | #1 Guard FSM | Feature | Monotonic investigation re-anchor budget formula: $t_{\text{reanchor}} = \min(t_{\text{investigate\_max}}, \max(t_{\text{remaining}}, t_{\text{reanchor\_floor}}))$. |
| **TR-FEAT-003** | #1 Guard FSM | Feature | 2.5D Catch Gate cylinder check: $|\Delta Y| \le 1.0\text{ m}$, distance $\le 5.50\text{ m}$, clear Linecast at path arrival. |
| **TR-FEAT-004** | #1 Guard FSM | Feature | Witnessed HideSpot entry authority: suspends chase give-up timer, switches goal mode to `HideSpotFront`. |
| **TR-FEAT-005** | #1 Guard FSM | Feature | Multi-guard chase duration pooling and sequential give-up timers (Target tier). |
| **TR-FEAT-006** | #2 Perception | Feature | Two-component threat tracking per guard: sustained-LOS accumulator ($A_i$) and residual wariness ($R_i$). |
| **TR-FEAT-007** | #2 Perception | Feature | Distance-scaled accumulator charge rate: $dA/dt = r_{\text{charge\_base}} \cdot \max(0, 1 - d/R_{\text{vision}})^p$. |
| **TR-FEAT-008** | #2 Perception | Feature | Dynamic entry threshold marker: $T_{\text{entry}}(R_i) = \max(T_{\text{floor}}, T_{\text{base}} - \kappa R_i)$. |
| **TR-FEAT-009** | #2 Perception | Feature | Hearing Linecast occlusion check using SoundOccluder layer mask. |
| **TR-FEAT-010** | #2 Perception | Feature | Fixed virtual-time tick evaluation independent of render frame fluctuations. |
| **TR-FEAT-011** | #3 Noise | Feature | Ballistic projectile simulation ($v_0=10.0\text{ m/s} @ 30^\circ$) with SphereCast ground snap. |
| **TR-FEAT-012** | #3 Noise | Feature | Acoustic hearing emission radii: Walk ($4.0\text{ m}$), Run ($6.0\text{ m}$), Burst ($10.5\text{ m}$). |
| **TR-FEAT-013** | #3 Noise | Feature | Audible stride ledger with fresh-session commit window ($0.55\text{ s}$) to debounce stutter steps. |
| **TR-FEAT-014** | #3 Noise | Feature | Clean distraction scorecard exemption: $0.0\text{ pts}$ direct penalty for un-witnessed Burst throws. |
| **TR-FEAT-015** | #5 Movement | Feature | HideSpot trigger volume state machine with full player visibility suppression mask. |
| **TR-FEAT-016** | #5 Movement | Feature | Pure-pivot translational lock ($\Delta \vec{p} = \vec{0}$) during spot dwell; yaw rotation allowed within clamp. |
| **TR-FEAT-017** | #5 Movement | Feature | Standoff positioning contract: `standoff_distance = 1.20 m` from spot entry node. |
| **TR-FEAT-018** | #7 Grade | Feature | Deterministic score integral: $S = 100 - \sum \text{Penalties} - \int \text{Exposure}$. Letter ranks S/A/B/C/F. |
| **TR-FEAT-019** | #7 Grade | Feature | Immutable incident ledger recording detection events, chase escalations, and retry deductions. |
| **TR-PRES-001** | #7 Meter UI | Presentation | Planar azimuth directional chevron projection ($R_x=240, R_y=160$) on screen HUD. |
| **TR-PRES-002** | #7 Meter UI | Presentation | Dominant threat selection ratio: $r_{\text{threat}}^* = \arg\max_i (A_i / T_{\text{entry}, i})$. |
| **TR-PRES-003** | #10 Telemetry | Presentation | Zero-GC struct ring buffer of capacity 2048 entries recording all FSM transitions and causality keys. |
| **TR-PRES-004** | #10 Telemetry | Presentation | Async JSON exporter and WebGL LocalStorage sync under key `"ww_fsm_trace_latest"`. |
| **TR-PRES-005** | #10 Telemetry | Presentation | Forensic verification scores evaluating Claims 1, 2, and 3 during automated playtests. |
| **TR-PRES-006** | #13 Audio | Presentation | Dynamic DSP Low-Pass Filter: $800\text{ Hz}$ interior HideSpot cutoff, $1200\text{ Hz}$ occluded wall cutoff. |
| **TR-PRES-007** | #13 Audio | Presentation | Dual-voice footstep audio pool (capacity 24 voices) with surface material acoustics. |
| **TR-PRES-008** | #13 Audio | Presentation | Continuous threat drone with dynamic pitch and volume modulation tied to dominant threat ratio. |

---

## 3. System Layer Map

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                           PRESENTATION LAYER                            │
│  - #7  Suspicion Meter Presenter & Directional Chevron HUD              │
│  - #10 Suspicion Attribution Telemetry Overlay & Export Presenter       │
│  - #13 Audio & UI Feedback (Spatial DSP, Footsteps Pool, Drone Mixer)   │
│  - #20 Camera Cinemachine Rig (Shoulder framing, De-occlusion)          │
├─────────────────────────────────────────────────────────────────────────┤
│                             FEATURE LAYER                               │
│  - #1  Guard AI FSM (Patrol, Investigate, Chase, Catch Gate)            │
│  - #2  Perception Pipeline (LOS Raycasts, Acoustic Occlusion, Wariness) │
│  - #3  Player Noise Emitter & Ballistic Projectile                      │
│  - #5  Player Movement & HideSpot State Machine                         │
│  - #7  Suspicion Grade Backend Engine (Score integral, Incident ledger) │
│  - #10 Telemetry Ring Buffer (Zero-GC FSM State Tracer)                 │
├─────────────────────────────────────────────────────────────────────────┤
│                              CORE LAYER                                 │
│  - #11 Player Third-Person Controller (Kinematic state machine)         │
│  - #12 NavMesh Pathfinding Query Service (Cached path buffer)           │
├─────────────────────────────────────────────────────────────────────────┤
│                           FOUNDATION LAYER                              │
│  - #15 Deterministic Event / Messaging Bus (Epoch barrier, dedup ring)  │
│  - #18 Physics Collision & Layer Mask Configuration                     │
│  - #19 Unity Input System Service (Action map asset & buffer)           │
├─────────────────────────────────────────────────────────────────────────┤
│                           PLATFORM LAYER                                │
│  - Unity 6 LTS (6000.3.17f1) Runtime, URP Forward+, PhysX, Standalone/WebGL│
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Module Ownership Map

| Module | Layer | Solely Owns (Data & State) | Exposes (Public API) | Consumes (Dependencies) | Engine APIs Used (Risk) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`EventMessagingBus`** | Foundation | Ingress dedup ring buffer (capacity 4096), subscriber registry, current `attempt_epoch`, monotonic sequence counter. | `Publish<TEvent>(in TEvent evt)`, `Subscribe<TEvent>(Action<TEvent> handler)`, `AdvanceEpoch(uint newEpoch)`, `FlushQueue()` | Virtual Clock | Pure C# structs, delegates, zero Unity engine dependency (**LOW**) |
| **`PhysicsCollisionConfig`** | Foundation | 7-layer physics separation matrix, `queriesHitTriggers = false` rule, geometric wall thickness invariant ($0.10\text{ m}$). | `WorldMask`, `VisionOcclusionMask`, `SoundOcclusionMask`, `RaycastVision()`, `LinecastAcoustic()` | None | `UnityEngine.Physics`, `LayerMask` (**LOW**) |
| **`InputSystemService`** | Foundation | `WhisperWardInputActions` instance, analog input smoothing buffer, deadzone filters ($0.10 / 0.95$), button edge buffers. | `MoveInput`, `LookInput`, `IsRunHeld`, `IsCrouchToggled`, `IsThrowPressed/Released` | Hardware via Unity Input System | `UnityEngine.InputSystem` v1.8+ (**LOW**) |
| **`PlayerController`** | Core | Kinematic position, velocity, stance FSM (`Upright`, `Crouched`), uncrouch clearance SphereCast state. | `Position`, `Velocity`, `CurrentStance`, `IsGrounded`, `PlayerTransform` | `InputSystemService`, `CameraRig` (planar basis), `PhysicsConfig` | `CharacterController`, `Transform` (**LOW**) |
| **`NavMeshPathfinding`** | Core | Reusable pre-allocated `NavMeshPath` buffers (0 GC), path arrival tolerance checks, corridor clearance ($\ge 1.50\text{ m}$). | `TryCalculatePath()`, `GetRemainingDistance()`, `IsOnNavMesh()` | `PhysicsConfig` (geometry) | `UnityEngine.AI.NavMesh`, `NavMeshPath` (**LOW**) |
| **`CameraCinemachineRig`** | Core / Pres | Cinemachine Camera rig, de-occlusion SphereCast ray, dynamic FOV tweener ($60^\circ \to 68^\circ$ on chase). | `MainCamera`, `PlanarForward`, `PlanarRight` | `PlayerController` (follow transform), `EventBus` (chase FOV event) | `Unity.Cinemachine.CinemachineCamera` (**LOW**) |
| **`GuardAIFSM`** | Feature | Per-guard FSM state (`Patrol`, `Investigate`, `Chase`), `GoalMode`, re-anchor budget $t_{\text{reanchor}}$, 2.5D Catch Gate. | `CurrentState`, `GuardId`, `EyePosition`, `ForwardDirection` | `EventBus` (`NoiseHeardRelay`, `VisionEvent`), `NavMeshService` | `NavMeshAgent` driver, `Transform` (**LOW**) |
| **`PerceptionPipeline`** | Feature | Threat Accumulators ($A_i$), Residual Wariness ($R_i$), Dynamic Thresholds ($T_{\text{entry}, i}$), Confirm timers ($0.25\text{ s}$). | `GetThreatSnapshot(guardId)`, `AllThreats` | `PlayerController` (eye/chest nodes, hide mask), `PhysicsConfig` (raycasts), `EventBus` | `Physics.RaycastNonAlloc`, `Physics.Linecast` (**LOW**) |
| **`PlayerNoiseEmitter`** | Feature | Audible stride ledger, commit timer ($0.55\text{ s}$), Burst inventory ($0/1$), ballistic trajectory simulation. | `HasBurst`, `ThrowBurst()`, `EmitLocomotionNoise()` | `PlayerController` (speed, stance), `PhysicsConfig`, `EventBus` | `Physics.SphereCast` (**LOW**) |
| **`PlayerMovementHide`** | Feature | HideSpot occupancy state, active spot ID, pure-pivot translational lock ($\Delta \vec{p} = \vec{0}$), visibility mask. | `IsHidden`, `CurrentHideSpotId`, `TryEnterSpot()`, `ExitSpot()` | `PlayerController` (locks motion), `EventBus` (`HideSpotEntered/Exited`) | `Collider` trigger, `Transform` (**LOW**) |
| **`SuspicionGradeBackend`** | Feature | Cumulative score integral $S \in [0, 100]$, detection incident ledger, attempt counter, letter grade (S/A/B/C/F). | `CurrentScore`, `CurrentGrade`, `IncidentLog` | `EventBus` (`ChaseEscalated`, `ExposureTick`, `AttemptEpochIncrement`) | Pure C# logic (**LOW**) |
| **`TelemetryRingBuffer`** | Feature / Pres | 2048-entry fixed struct ring buffer, write head pointer, JSON serialization buffer, WebGL storage sync. | `RecordTransition()`, `RecordCausality()`, `ExportJson()`, `Flush()` | `EventBus` (listens to all FSM state transitions) | C# `Span<T>`, `PlayerPrefs`/JS Interop (**LOW**) |
| **`SuspicionPresenter`** | Presentation | Screen HUD canvas widgets, dominant threat chevron azimuth angle ($R_x=240, R_y=160$), needle interpolation. | Screen HUD Canvas elements | `PerceptionPipeline` (dominant threat ratio $r_{\text{threat}}^*$), `CameraRig` | `UnityEngine.UI` / UI Toolkit (**LOW**) |
| **`AudioFeedback`** | Presentation | 24-voice AudioSource pool, AudioMixer parameters, DSP Low-Pass Filter ($800\text{ Hz} / 1200\text{ Hz}$), threat drone loop. | Audio playback and mixer modulation | `EventBus` (footsteps, Burst impact, stingers), `PerceptionPipeline` | `AudioSource`, `AudioMixer`, `AudioLowPassFilter` (**LOW**) |

---

## 5. Data Flow & Scenarios

### 5.1 Frame Update Path (Kinematic Locomotion & Camera)
```text
Hardware Input (Keyboard/Mouse/Gamepad)
  └─► [InputSystemService] Poll & Buffer (WASD, Stance, Throw)
        └─► [PlayerController] Reads Input + Camera Planar Basis -> Kinematic Move & Stance
              └─► [Physics Scene] CharacterController Move + Uncrouch SphereCast
                    ├─► [PlayerNoiseEmitter] Checks stride ledger -> Emits Locomotion Noise if threshold exceeded
                    └─► [CameraCinemachineRig] Smooth Follow Target + SphereCast De-occlusion
```

### 5.2 Perception & AI Sensing Loop (Fixed Virtual Tick @ 20 Hz / 50ms)
```text
[Virtual Clock Tick (t_virtual)]
  └─► [PerceptionPipeline]
        ├─► Linecast checks acoustic occlusion (SoundOccluder mask)
        └─► Raycast checks sightline (VisionOccluder mask)
              ├─► If LOS Clear: Accumulator Charges: dA/dt = r_charge_base * (1 - d/R_vision)^p
              │     └─► A_i >= T_entry(R_i) -> Enters Confirm Window (0.25s)
              │           └─► Expiry with sustained LOS -> Publish ChaseEscalatedEvent
              └─► If LOS Broken: A_i resets to 0.0 immediately; residual wariness R_i retains charge
  └─► [GuardAIFSM]
        ├─► Consumes Event Bus (NoiseHeardRelay / ChaseEscalatedEvent)
        ├─► Updates GoalMode (Patrol -> Investigate -> Chase)
        └─► [NavMeshPathfinding] Evaluates route using pre-allocated NavMeshPath
              └─► Path arrival check: Evaluates 2.5D Catch Gate (|ΔY| <= 1.0m, d <= 5.50m)
```

### 5.3 Epoch Barrier & Checkpoint Respawn Path
```text
Player Capture / Checkpoint Reload Trigger
  └─► [Lifecycle Authority] Advances attempt_epoch: N -> N + 1
        ├─► [EventMessagingBus] Purges all queued events matching old epoch (Epoch Barrier)
        ├─► [GuardAIFSM] Closes active tracking keys with cause="stale" -> Resets Guard to Patrol
        ├─► [PerceptionPipeline] Resets all A_i = 0.0, R_i = 0.0 for all guards in scene
        ├─► [PlayerController] Teleports to checkpoint spawn, clears velocity and stride ledger
        └─► [SuspicionGradeBackend] Preserves historical incident ledger (score deductions hold), increments attempt count
```

---

## 6. API Boundaries & Contracts

The public interfaces decouple subsystems cleanly, allowing deterministic NUnit testing without requiring live scene singletons or heavy MonoBehaviour lifecycles.

```csharp
namespace WhisperWard.Core.Contracts
{
    // =========================================================================
    // FOUNDATION CONTRACTS
    // =========================================================================

    /// <summary>
    /// Deterministic Event Bus contract supporting zero-GC struct dispatch,
    /// ingress deduplication, and atomic epoch barrier invalidation.
    /// </summary>
    public interface IEventBus
    {
        uint CurrentEpoch { get; }
        void Publish<TEvent>(in TEvent evt) where TEvent : struct, IGameEvent;
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameEvent;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameEvent;
        void AdvanceEpoch(uint newEpoch);
        void FlushQueue();
    }

    public interface IGameEvent
    {
        uint Epoch { get; }
        float VirtualTimestamp { get; }
        long FactId { get; }
    }

    /// <summary>
    /// Physics query authority enforcing queriesHitTriggers = false and layer isolation.
    /// </summary>
    public interface IPhysicsQueryService
    {
        bool RaycastVision(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit);
        bool LinecastAcoustic(Vector3 origin, Vector3 target, out RaycastHit hit);
        bool CheckSphereClearance(Vector3 center, float radius, LayerMask mask);
        bool SphereCastBallistic(Ray ray, float radius, float maxDistance, out RaycastHit hit);
    }

    /// <summary>
    /// Input System service abstracting Unity Input System action asset.
    /// </summary>
    public interface IInputService
    {
        Vector2 MoveInput { get; }
        Vector2 LookInput { get; }
        bool IsRunHeld { get; }
        bool IsCrouchToggled { get; }
        bool IsThrowPressed { get; }
        bool IsThrowReleased { get; }
    }

    // =========================================================================
    // CORE CONTRACTS
    // =========================================================================

    public enum PlayerStance { Upright, Crouched, Sliding }

    /// <summary>
    /// Kinematic player controller interface exposing spatial and stance state.
    /// </summary>
    public interface IPlayerController
    {
        Vector3 Position { get; }
        Vector3 Velocity { get; }
        PlayerStance Stance { get; }
        bool IsHidden { get; }
        Transform PlayerTransform { get; }
        void SetHidden(bool hidden, int? hideSpotId);
        void Teleport(Vector3 position, Quaternion rotation);
    }

    /// <summary>
    /// Navigation query interface with zero runtime allocations.
    /// </summary>
    public interface INavMeshService
    {
        bool CalculatePathNonAlloc(Vector3 start, Vector3 end, NavMeshPath pathBuffer);
        float CalculatePathLength(NavMeshPath path);
        bool IsPointNavigable(Vector3 point, float tolerance = 0.5f);
    }

    // =========================================================================
    // FEATURE CONTRACTS
    // =========================================================================

    public readonly struct ThreatSnapshot
    {
        public readonly int GuardId;
        public readonly float Accumulator;
        public readonly float Residual;
        public readonly float SinkingThreshold;
        public readonly float NormalizedThreatRatio; // Accumulator / SinkingThreshold
        public readonly bool HasDirectLOS;
        public readonly Vector3 ThreatPosition;

        public ThreatSnapshot(int guardId, float acc, float res, float threshold, bool los, Vector3 pos)
        {
            GuardId = guardId;
            Accumulator = acc;
            Residual = res;
            SinkingThreshold = threshold;
            NormalizedThreatRatio = threshold > 0.001f ? Mathf.Clamp01(acc / threshold) : 1.0f;
            HasDirectLOS = los;
            ThreatPosition = pos;
        }
    }

    /// <summary>
    /// Perception service exposing threat telemetry and guard awareness.
    /// </summary>
    public interface IPerceptionService
    {
        ThreatSnapshot GetThreat(int guardId);
        IReadOnlyList<ThreatSnapshot> GetAllThreats();
        ThreatSnapshot GetDominantThreat();
    }

    public enum GuardState { Patrol, Investigate, Chase }

    /// <summary>
    /// Guard AI FSM interface.
    /// </summary>
    public interface IGuardAI
    {
        int GuardId { get; }
        GuardState State { get; }
        Vector3 EyePosition { get; }
        Vector3 Forward { get; }
        float ReanchorBudgetRemaining { get; }
    }

    /// <summary>
    /// Suspicion Grade computation interface.
    /// </summary>
    public interface IGradeService
    {
        float CurrentScore { get; }
        char LetterRank { get; }
        IReadOnlyList<GradeIncidentRecord> Incidents { get; }
    }

    public readonly struct GradeIncidentRecord
    {
        public readonly uint Epoch;
        public readonly float VirtualTime;
        public readonly string IncidentType;
        public readonly float ScorePenalty;
        public readonly Vector3 Location;
    }
}
```

---

## 7. ADR Audit & Traceability Matrix

### 7.1 Existing ADR Audit
| ADR ID | Title | Engine Compat | GDD Linkage | Current Status | Architecture Audit Verdict |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **ADR-0001** | Deterministic Event/Messaging Bus | Unity 6 LTS (LOW) | #15, #1, #2, #3, #5, #7, #10 | **Accepted** | **ACCEPTED (Confirmed)**. Covers TR-FOUND-001..004. Zero architectural conflicts. |
| **ADR-0002** | Shared Physics and Collision Contract | Unity 6 LTS (LOW) | #18, #11, #12, #2, #3, #5 | **Accepted** | **ACCEPTED (Confirmed)**. Covers TR-FOUND-005..007. Sets queriesHitTriggers standard. |
| **ADR-0003** | Audio Virtual Timestamp Boundary | Unity 6 LTS (LOW) | #13, #3 | **Accepted** | **ACCEPTED (Confirmed)**. Covers TR-PRES-006..008. Isolates DSP reporting from sim time. |
| **ADR-0004** | Suspicion Meter and Room Grade | Unity 6 LTS (LOW) | #7, #10 | **Accepted** | **ACCEPTED (Confirmed)**. Covers TR-FEAT-018..019 and TR-PRES-001..002. |
| **ADR-0005** | Input Action Asset & Stance Buffering | Unity 6 LTS (LOW) | #19, #11, #5, #3 | **Accepted** | **ACCEPTED (Confirmed)**. Covers TR-FOUND-008..009. Zero-GC hot path & 150ms buffer. |

### 7.2 Requirements Traceability Matrix
- **Foundation Layer (TR-FOUND-001 to 009)**:
  - `TR-FOUND-001..004` (Event Bus): Covered by `ADR-0001` (Accepted).
  - `TR-FOUND-005..007` (Physics): Covered by `ADR-0002` (Accepted).
  - `TR-FOUND-008..009` (Input): Covered by `ADR-0005` (Accepted). **Zero Foundation Gaps Remaining**.
- **Core Layer (TR-CORE-001 to 010)**:
  - `TR-CORE-001..004` (Locomotion & Stance): Targeted for `ADR-0006`.
  - `TR-CORE-005..007` (NavMesh & Catch Gate): Targeted for `ADR-0007`.
  - `TR-CORE-008..010` (Cinemachine Camera Rig): Targeted for `ADR-0008`.
- **Feature Layer (TR-FEAT-001 to 019)**:
  - `TR-FEAT-001..005` (Guard AI FSM): Targeted for `ADR-0010`.
  - `TR-FEAT-006..010` (Perception Dual-Threat): Targeted for `ADR-0009`.
  - `TR-FEAT-011..014` (Player Noise & Burst): Targeted for `ADR-0011`.
  - `TR-FEAT-015..017` (HideSpot Mechanics): Targeted for `ADR-0006` / `ADR-0010`.
  - `TR-FEAT-018..019` (Suspicion Grade Engine): Covered by `ADR-0004` (Accepted).
- **Presentation Layer (TR-PRES-001 to 008)**:
  - `TR-PRES-001..002` (Meter Presenter & Chevron): Covered by `ADR-0004` (Accepted).
  - `TR-PRES-003..005` (Telemetry Struct Ring Buffer): Targeted for `ADR-0012`.
  - `TR-PRES-006..008` (Audio DSP & Drone): Covered by `ADR-0003` (Accepted).

---

## 8. Required & Prioritized ADRs

Following the Producer's phased implementation plan, ADRs are scheduled in 4 strict tiers:

### Tier 1: Foundation Layer (Blocking before any feature code) — 100% ACCEPTED
1. **`ADR-0001`**: Deterministic Event/Messaging Bus → **Accepted** (2026-09-21)
2. **`ADR-0002`**: Shared Physics and Collision Contract → **Accepted** (2026-09-21)
3. **`ADR-0003`**: Audio Virtual Timestamp Boundary → **Accepted** (2026-09-21)
4. **`ADR-0005`**: Input Action Asset & Stance Buffering Contract → **Accepted** (2026-09-21)

### Tier 2: Core Layer (Locomotion & Spatial Systems)
5. **Author `ADR-0006`**: *Kinematic Player Controller, Stance FSM & Headroom SphereCast*
6. **Author `ADR-0007`**: *NavMesh NonAlloc Query Service & 2.5D Catch Gate Verification*
7. **Author `ADR-0008`**: *Cinemachine 3rd-Person Camera Rig & Occlusion SphereCast Damping*

### Tier 3: AI & Sensing Core (Thinking Enemies & Stealth Verbs)
8. **Author `ADR-0009`**: *Perception Dual-Threat Pipeline, Raycast Budget & Dynamic Thresholds*
9. **Author `ADR-0010`**: *Guard AI 3-State FSM, Re-anchor Budget & Witnessed HideSpot Authority*
10. **Author `ADR-0011`**: *Player Noise Stride Ledger, Burst Ballistics & Zero-Deduction Distraction*

### Tier 4: Presentation & Telemetry
11. **`ADR-0004`**: *Suspicion Meter and Room Grade* → Already **Accepted**
12. **Author `ADR-0012`**: *Zero-GC Struct Telemetry Ring Buffer & WebGL LocalStorage Sync*

---

## 9. Architecture Principles

1. **Deterministic Virtual Clock Authority**: All gameplay state transitions, cooldowns, threat charging ($dA/dt$), and re-anchor budgets are driven by an injected virtual clock `t_virtual`, never raw `Time.deltaTime` or unscaled frame durations.
2. **Zero-Allocation Gameplay Loop**: Mainframe execution paths (Perception Raycasts, NavMesh Path calculation, Event Dispatch, Telemetry Logging) must produce 0 GC allocations on the managed heap. Reusable pre-allocated arrays, struct ring buffers, and `Span<T>` are mandatory.
3. **Strict Downward & Event-Driven Coupling**: Modules may call directly downward into lower layers (`Presentation` $\to$ `Feature` $\to$ `Core` $\to$ `Foundation`), but cross-module and upward notifications must travel strictly via typed structs through the `EventMessagingBus`.
4. **Fairness Over Reflexes**: Invariants defined in GDDs (anti-kiting speed ratio $\ge 1.20$, confirm window $0.25\text{ s}$, 2.5D Catch Gate cylinder check, clean Burst distraction zero-penalty) take precedence over generic simulation shortcuts.
5. **Epoch-Gated Lifecycle Cleanliness**: Any game reset (Capture, Death, Checkpoint Load) is an atomic transaction incrementing `attempt_epoch`. Dangling asynchronous tasks or stale queued events must be discarded at the epoch barrier.

---

## 10. Open Questions & Risk Mitigation

| ID | Summary | Risk Level | Mitigation & Resolution Path |
| :--- | :--- | :--- | :--- |
| **QQ-01** | *WebGL Build Audio DSP Latency* | Low | Verify DSP Low-Pass Filter performance on WebGL build during Tier 4; fallback to simple 2-track crossfade if WebGL AudioWorklet issues emerge. |
| **QQ-02** | *NavMesh Corridor Clearance at Doorways* | Low | Enforce level geometry check: all navigable openings must measure $\ge 1.50\text{ m}$ (satisfies $2 \times r_{\text{guard}} + 0.5\text{ m}$ margin). Verified in `ADR-0007`. |
| **QQ-03** | *C-5 Prototype Playtest Plan Sign-Off* | Medium | Producer flagged C-5 playtest plan awaiting QA-lead sign-off prior to Milestone-0 build. Tracked in `production/gate-checks/2026-09-21-systems-design-to-technical-setup.md`. |

