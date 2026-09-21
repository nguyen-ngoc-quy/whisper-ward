# Control Manifest

> **Engine**: Unity 6 LTS (6000.3.17f1)  
> **Last Updated**: 2026-09-21  
> **Manifest Version**: 2026-09-21  
> **ADRs Covered**: ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006  
> **Status**: Active — regenerate with `/create-control-manifest update` when ADRs change  

`Manifest Version` is the date this manifest was generated. Story files embed this date when created. `/story-readiness` compares a story's embedded version to this field to detect stories written against stale rules. Always matches `Last Updated` — they are the same date, serving different consumers.

This manifest is a programmer's quick-reference extracted from all Accepted ADRs, technical preferences, and engine reference docs. For the reasoning behind each rule, see the referenced ADR.

---

## Foundation Layer Rules

*Applies to: scene management, event architecture, save/load, physics contracts, input systems, and engine initialisation*

### Required Patterns
- **Immutable Struct Event Envelopes**: All event envelopes must be readonly C# structs containing `session_id`, `attempt_epoch`, `timestamp`, and `publisher`. No reference-type envelope allocations on the event bus. — source: `ADR-0001`
- **Owner-Defined Identity & Ingress Deduplication**: Every event must carry an owner-defined immutable identity (`(session_id, attempt_epoch, fact_id)` for `NoisePublished`; `(session_id, attempt_epoch, hide_spot_id, transition_id)` for `HideSpot occupied/empty`). Bus performs deduplication against `(session_id, attempt_epoch, owner namespace, identity)` before listener dispatch. — source: `ADR-0001`
- **Atomic Attempt Epoch Invalidation**: Checkpoint reload, capture, respawn, or room restart must execute `BeginEpoch` or `BeginSession` barriers, atomically purging queued envelopes, invalidating stale identity state, and tagging leftover items with `event-bus-stale-epoch` or `event-bus-stale-session`. — source: `ADR-0001`
- **Two-Phase Transactional Perception Handoff**: `NoisePublished` dispatch must invoke the typed contract `Perception.AcceptNoise(envelope) -> Accepted | Duplicate | Retry | Rejected(code)`. Envelopes stay in `handoff_pending` during retry and remove only when all active listeners acknowledge or terminals serialize. — source: `ADR-0001`
- **E20 Layer Mask Resolution at Startup**: Exact layer names (`World`, etc.) must resolve and cache once at initialization via `LayerMask.NameToLayer`. Mask initialization must fail-closed if any layer name returns `-1`. — source: `ADR-0002`
- **Contentful Scene Validation**: The Physics service must assert that the active scene contains at least one non-trigger collider on the assembled E20 mask upon initialization; an empty/contentless mask fails closed. — source: `ADR-0002`
- **Geometric Wall Thickness Invariant**: All solid obstacle, wall, and occluder geometry must adhere to a minimum thickness invariant of $\ge 0.10\text{ m}$ to guarantee zero tunneling. — source: `ADR-0002`
- **Strongly Typed C# Input Actions Wrapper**: Compile `WhisperWardInputActions.inputactions` to strongly typed C# class `WhisperWardInputActions`. Cache all `InputAction` references once during initialization. — source: `ADR-0005`
- **Exclusive Action Map Switching**: Switch cleanly between `Player` and `UI` action maps. When switching to `UI` (or Pausing), flush all movement and look state to zero (`Vector2.zero`) to prevent stuck inputs. — source: `ADR-0005`
- **Stance Transition Clearance SphereCast**: Before executing an uncrouch/stand transition, cast an upward SphereCast ($R = 0.25\text{ m}, H = 1.80\text{ m}$) against the E20 LayerMask. Deny uncrouch if obstructed. — source: `ADR-0005`

### Forbidden Approaches
- **Never Query Actions by String at Runtime**: Never call `InputActionAsset.FindAction("Move")` or string indexers in game loops (`Update`/`FixedUpdate`). — source: `ADR-0005`
- **Never Allocate on Input Polling**: Never box values or allocate heap memory inside input polling methods (`ReadValue<Vector2>()` must use direct typed reads into readonly structs). — source: `ADR-0005`
- **Never Allow queriesHitTriggers = true for Perception**: Never enable trigger collision (`queriesHitTriggers = true` or `QueryTriggerInteraction.Collide`) for hearing, vision, pickup reach, or Burst collision queries. Use `QueryTriggerInteraction.Ignore` universally across sensing raycasts. — source: `ADR-0002`
- **Never Allow queriesHitBackfaces = true**: Backface hits must remain disabled (`Physics.queriesHitBackfaces = false`) for the entire gameplay session. — source: `ADR-0002`
- **Never Include Player/Guard/Trigger in E20 Mask**: Reject any mask containing bits for `Player`, `Guard`, or trigger volumes during E20 assembly. — source: `ADR-0002`
- **Never Synthesize or Reassign fact_id / entry_id in the Bus**: The Event Bus must never allocate, synthesize, or reinterpret `fact_id` (owned by Emitter) or `entry_id` (owned by Perception). — source: `ADR-0001`
- **Never Dispatch Events Across Session/Epoch Barriers**: Older queued events must never cross session or epoch barriers; unevaluated work must be explicitly serialized and rejected, never silently dropped or executed late. — source: `ADR-0001`

### Performance Guardrails
- **Event Bus Ring Buffer Capacity**: Ingress pending-envelope ring buffer fixed capacity of 4096 entries (`event_bus_pending_envelope_capacity`). Overflow must reject with `event-bus-queue-overflow-rejected`. — source: `ADR-0001`
- **Event Bus Dispatch Budget**: Maximum 0.50 ms per frame budget for event draining. Dispatch operates on Unity's main thread with 0 heap allocation on hot paths. — source: `ADR-0001`
- **Physics Query Frame Budget**: Maximum 1.20 ms per frame total for all spatial physics queries (combined raycasts, linecasts, spherecasts). — source: `ADR-0002`
- **Input System GC Budget**: Exactly 0 B (Zero GC) per frame during gameplay input polling. — source: `ADR-0005`

---

## Core Layer Rules

*Applies to: core locomotion, player kinematic controller, navigation, camera follow, and physics interaction*

### Required Patterns
- **Standardized Noise Origin & Anchors**: Movement hearing linecasts must originate from `player_feet + f12_noise_origin_offset` ($Y = 0.10\text{ m}$); Burst hearing originates from post-collision contact; both terminate at `guard_eye` anchor ($Y = 1.65\text{ m}$). — source: `ADR-0002`
- **Closest-Hit SphereCast for Kinematic Ballistics**: Burst trajectory simulation must perform one authoritative closest-hit `SphereCastNonAlloc` per fixed substep, plus an initial overlap check at launch. Resolve contact at the sphere surface with the registered push-out margin. — source: `ADR-0002`
- **Solid Transform Synchronization**: Explicitly call `Physics.SyncTransforms()` once prior to executing Burst trajectory simulation batches or capsule query resize operations. — source: `ADR-0002`, `ADR-0006`
- **Dedicated HideSpot Containment Profile**: Use `HideSpotContainmentProfile` with `QueryTriggerInteraction.Collide` strictly and exclusively for player capsule containment inside authored trigger volumes; never repurpose as an E20 sensing mask. — source: `ADR-0002`
- **Stance & Throw Input Buffering**: Buffer stance change requests (Crouch toggle/hold, Sprint hold) and throw triggers for $150\text{ ms} - 200\text{ ms}$ to eliminate dropped inputs during kinematic deceleration or headroom clearing. — source: `ADR-0005`
- **Dual Deadzone Processing**: Apply radial deadzones to analog movement sticks: inner deadzone 0.10, outer deadzone 0.95. Normalize output vector magnitude to $[0.0, 1.0]$. — source: `ADR-0005`
- **Kinematic Speed Hierarchy & Linear Slew**: Speeds strictly governed by $V_{\text{crouch}} = 1.80\text{ m/s} < V_{\text{walk}} = 3.60\text{ m/s} < V_{\text{run}} = 6.25\text{ m/s}$ with linear acceleration approach ($a_{\text{max}} = 78.125\text{ m/s}^2$, $\text{accel\_time} \le 0.08\text{ s}$). — source: `ADR-0006`
- **Anti-Kiting Invariant**: Enforce guard chase speed $V_{\text{chase}} = 7.50\text{ m/s}$ such that $V_{\text{chase}} / V_{\text{run}} = 7.50 / 6.25 = 1.20 \ge 1.20$. — source: `ADR-0006`
- **Backpedal Speed Penalty**: Apply a $0.70$ multiplier to target speed when $\vec{d}_{\text{move}} \cdot \text{facing} < -10^{-4}$. — source: `ADR-0006`
- **Camera-Relative Planar Basis with Pitch Degeneracy Fallback**: Transform movement input relative to Cinemachine forward yaw vector projected on the horizontal plane. Fall back to projected camera up-vector when camera pitch approaches $\pm 90^\circ$ ($\|\vec{d}_{\text{proj}}\| < 10^{-4}$). — source: `ADR-0006`
- **Feet-Anchored Capsule Height Scaling**: Keep player capsule radius locked to $R = 0.35\text{ m}$; scale height ($1.80\text{ m}$ Stand, $0.95\text{ m}$ Crouch) with $\text{center.y} = \text{height} / 2$ to anchor feet at local $y = 0$. — source: `ADR-0006`
- **Stand Clearance Query with Ground Insetting**: Probe stand headroom using `Physics.OverlapCapsuleNonAlloc` against E20 LayerMask with lower hemisphere inset by $\ge \text{skin\_width}$ above the feet plane and top margin $\epsilon_{\text{probe}} = 0.03\text{ m}$. Fail safe (deny stand) if blocked or buffer saturates. — source: `ADR-0006`
- **Downward Ground-Snap Velocity**: Apply continuous downward bias velocity ($v_{\text{down}} = -3.0\text{ m/s}$) during grounded locomotion in `CharacterController.Move()` to eliminate `isGrounded` micro-flicker. — source: `ADR-0006`
- **Dynamic Step Offset**: Set `CharacterController.stepOffset` dynamically: $0.30\text{ m}$ while Standing; $0.15\text{ m}$ while Crouched. — source: `ADR-0006`

### Forbidden Approaches
- **Never Rely on Rigidbody Dynamics for Burst**: Never attach a dynamic Rigidbody or PhysX physics simulation to the Burst projectile; simulation must remain kinematic and deterministic across platforms. — source: `ADR-0002`
- **Never Use Dynamic Rigidbody for Player Locomotion**: Dynamic Rigidbodies and physics forces are strictly prohibited for player locomotion; character movement must remain kinematic and deterministic via `CharacterController`. — source: `ADR-0006`
- **Never Couple Locomotion Stance to Render Framerate**: Stance transitions and input buffer draining must use virtual clock timing ($150\text{ ms}$ window), never variable `Time.deltaTime`. — source: `ADR-0005`
- **Never Query NavMesh or Physics with Allocating Methods**: Never use allocating calls (e.g. `Physics.RaycastAll`, `NavMesh.CalculatePath` without pre-allocated path buffer); all queries must use pre-allocated non-allocating buffers. — source: `ADR-0002`, `ADR-0005`, `ADR-0006`
- **Never Resize Capsule Center Independently from Height**: Never set `CharacterController.center.y` to a fixed value; it must strictly equal `height * 0.5f` to prevent feet detachment from the ground plane. — source: `ADR-0006`

### Performance Guardrails
- **Burst Substep Fixed Interval**: Fixed simulation substep $\Delta t = 0.02\text{ s}$ ($50\text{ Hz}$) with maximum 10 simulation iterations per frame backlog ceiling. — source: `ADR-0002`
- **Controller Movement Budget**: Maximum 0.80 ms per frame budget for character locomotion, stance evaluation, and collision resolution. — source: `ADR-0005`, `ADR-0006`

---

## Feature Layer Rules

*Applies to: guard AI, perception pipeline, suspicion grading, room outcome calculation, and hide mechanics*

### Required Patterns
- **Pure Projections Over Injected State**: Suspicion Meter and Room Grade systems must operate as pure mathematical projections over injected immutable state snapshots. They must never maintain an independent update loop or mutate source values. — source: `ADR-0004`
- **Incident Deduplication Tuple**: Room grading incident deduplication must strictly use the 5-element tuple:  
  `(session_id, attempt_epoch, entry_id, guard_eid, event_type)`. — source: `ADR-0004`
- **Idempotent Room Finalization**: A room finalization call must be idempotent for a given `(session_id, attempt_epoch, room_id)` boundary. — source: `ADR-0004`
- **Fail-Closed Outcome on Missing Trace**: Missing accumulator or threshold input produces an unavailable meter; missing mandatory trace or residual evidence produces an unresolved grade. Never substitute fabricated default scores. — source: `ADR-0004`
- **Capture Trumps All Ranks**: Any capture event or incomplete room status immediately sets the grade outcome to `Failed`, never S/A/B/C. — source: `ADR-0004`
- **Two-Component Threat Input Separation**: Meter calculations must receive both sustained-LOS accumulator $A_i$ and residual wariness $R_i$, calculating the sinking threshold $T_{\text{entry}}(R_i) = \max(T_{\text{floor}}, T_{\text{base}} - \kappa \cdot R_i)$. — source: `ADR-0004`
- **HideSpot Pure-Pivot Translational Lock**: When in `InHideSpot` state, lock translation ($\Delta \vec{p} = \vec{0}$) while permitting yaw rotation within authored aperture cone ($[-60^\circ, +60^\circ]$). — source: `ADR-0006`
- **HideSpot Standoff Exit**: On exiting a `HideSpot`, position the player at `standoff_distance = 1.20 m` along the entrance normal vector to avoid collision entrapment. — source: `ADR-0006`

### Forbidden Approaches
- **Never Poll Gameplay Singletons for Suspicion State**: Never create a frame-polled singleton that inspects Guard FSM or Perception internals directly. State must arrive via immutable snapshot or event ledger. — source: `ADR-0004`
- **Never Recompute AI or Perception State in Grade Operator**: The grading operator must never evaluate raycasts, linecasts, or re-run perception equations; it acts solely as a trace reducer over accepted FSM records. — source: `ADR-0004`
- **Never Mutate Perception Thresholds from Presentation**: The Suspicion Meter presenter or UI components must never send commands that alter $T_{\text{entry}}$, $R_i$, or FSM states. — source: `ADR-0004`

### Performance Guardrails
- **Grade Trace Ring Buffer Capacity**: In-memory incident ledger ring buffer fixed capacity of 2048 entries. — source: `ADR-0004`
- **Grade Evaluation Budget**: Maximum 0.20 ms execution budget for room grade calculation and trace reduction upon room exit. — source: `ADR-0004`

---

## Presentation Layer Rules

*Applies to: audio engine integration, UI/HUD rendering, camera framing, and visual effects*

### Required Patterns
- **Explicit One-Way Virtual Timestamp Audio Boundary**: Gameplay requests audio via virtual timestamp (`virtual_cue_request`); the audio system reports the actual DSP start sample (`dsp_start_sample`). Gameplay timing must never depend on wall-clock audio callbacks. — source: `ADR-0003`
- **Canonical DSP Onset Conversion**: Convert reported DSP start samples to the virtual domain strictly via:  
  `virtual_dsp_onset = (dsp_start_sample / sample_rate) + epoch_offset`. — source: `ADR-0003`
- **Immutable Onset Outcome Lifecycle**: Every triggered sound event must record an immutable `onset_outcome` (`confirmed`, `estimated`, `missing`, `rejected`) and append to `onset_trace_history[]`. — source: `ADR-0003`
- **Nullable Estimation Separation**: When exact hardware sample timing is unavailable, `dsp_start_sample_estimate` must be used; never write an estimate into `dsp_start_sample`. — source: `ADR-0003`
- **Azimuth Threat Chevron Projection**: Threat chevrons on the HUD must project from the 3D guard position onto the screen-space azimuth boundary ($R_x = 240\text{ px}, R_y = 160\text{ px}$). — source: `ADR-0004`

### Forbidden Approaches
- **Never Derive Gameplay Timing from Audio Callbacks**: Gameplay evaluation (movement speed, guard hearing, detection) must never wait on or synchronize with audio thread callbacks or DSP hardware clocks. — source: `ADR-0003`
- **Never Fabricate Zero Onset Evidence**: If the audio integration fails to report an onset, record `onset_outcome = missing`. Never invent zero or assume instantaneous playback. — source: `ADR-0003`
- **Never Allocate Audio Voice Instances Dynamically**: Never instantiate audio sources dynamically in gameplay; allocate from a pre-warmed 24-voice hardware voice pool. — source: `ADR-0003`

### Performance Guardrails
- **Audio Voice Limit**: Maximum 24 concurrent active audio voices in the hardware pool. — source: `ADR-0003`
- **Audio DSP Latency Window**: Target onset confirmation within $\le 30\text{ ms}$ on PC Windows and $\le 60\text{ ms}$ on WebGL. — source: `ADR-0003`

---

## Global Rules (All Layers)

### Naming Conventions
| Element | Convention | Example | Source |
| :--- | :--- | :--- | :--- |
| **Classes & Structs** | PascalCase | `PlayerController`, `NoisePublishedEvent` | `.claude/docs/technical-preferences.md` |
| **Public Fields & Properties** | PascalCase | `MoveSpeed`, `CurrentStance` | `.claude/docs/technical-preferences.md` |
| **Private & Internal Fields** | `_camelCase` | `_moveSpeed`, `_eventBus` | `.claude/docs/technical-preferences.md` |
| **Signals & Events** | PascalCase + `Event` suffix | `OnPlayerSpottedEvent`, `NoisePublishedEvent` | `.claude/docs/technical-preferences.md` |
| **Source Files** | PascalCase matching primary type | `PlayerController.cs`, `IInputService.cs` | `.claude/docs/technical-preferences.md` |
| **Constants & Statics** | PascalCase | `MaxSuspicion`, `DefaultMoveSpeed` | `.claude/docs/technical-preferences.md` |
| **Scenes & Prefabs** | PascalCase | `FacilityLevel.unity`, `GuardNPC.prefab` | `.claude/docs/technical-preferences.md` |

### Performance Budgets
| Target Metric | PC Target (Primary) | WebGL Target (Portfolio) | Source |
| :--- | :--- | :--- | :--- |
| **Framerate** | 60 fps (16.6 ms frame budget) | 30 fps (33.3 ms frame budget) | `.claude/docs/technical-preferences.md` |
| **Draw Calls** | $\le 1000$ draw calls (URP Forward+) | $\le 400$ draw calls | `.claude/docs/technical-preferences.md` |
| **Heap Allocations** | 0 B per frame on main gameplay loop | 0 B per frame on main gameplay loop | `ADR-0001`, `ADR-0005` |
| **Physics Simulation** | Fixed $50\text{ Hz}$ ($0.02\text{ s}$ substep) | Fixed $50\text{ Hz}$ ($0.02\text{ s}$ substep) | `ADR-0002` |

### Approved Libraries & Addons
- **URP (Universal Render Pipeline)**: Render pipeline and shader graph.
- **Unity New Input System (`com.unity.inputsystem`, v1.7.0+)**: Input action assets, C# wrapper code generation, input rebinding.
- **Cinemachine (v3.x)**: Third-person follow camera, target group framing, occlusion damping.
- **AI Navigation (NavMesh)**: NavMesh surface baking and non-allocating path queries.
- **ProBuilder**: Rapid level blockout and geometric prototyping.
- **Unity Test Framework (NUnit)**: EditMode and PlayMode automated verification suites.

### Forbidden APIs & Anti-Patterns (Unity 6 LTS)
These APIs and patterns are strictly forbidden across all subsystems:
- **`FindObjectOfType<T>()` / `FindObjectsOfType<T>()`**: Deprecated in Unity 6 and unacceptable for performance. Inject dependencies via constructors or serialized fields.
- **`GameObject.SendMessage()` / `BroadcastMessage()`**: Untyped reflection dispatch. Use the strongly typed `IEventBus` service.
- **`Resources.Load()`**: Legacy synchronous loader bypassing memory governance. Use direct inspector references or Addressables.
- **Legacy Input Manager (`UnityEngine.Input`)**: Deprecated. All input must route through `IInputService` and the Unity New Input System.
- **`Instantiate()` / `Destroy()` on Hot Paths**: Prohibited during active gameplay. Pre-warm pools for audio voices, projectiles, and visual markers.
- **Singletons with Global Mutable State**: Banned for core gameplay logic. Use decoupled interfaces and explicit dependency injection.

### Cross-Cutting Architectural Constraints
1. **Virtual Clock Authority**: All gameplay state, timers, detection accumulation, buffer expirations, and projectile flight are governed by the injected virtual clock, never variable render frame `Time.deltaTime` or wall-clock timestamps.
2. **Deterministic Replay & Save/Load Invariance**: Systems must separate deterministic logic from presentation so state can be reconstructed identically given an input snapshot ledger.
3. **Fail-Closed Diagnostic Principle**: Incomplete data, missing raycast layers, or queue overflows must trigger explicit error handling and diagnostics; systems must never fabricate fallback values or fail silently.
