# Input / Camera / Recovery Traceability Report — 2026-08-29

## Scope

This report traces the cross-system contracts required by Player Noise for input
capture, camera aim sampling, player facing fallback, Burst lifecycle recovery and
session identity. It records ownership and verification gaps only. It does not amend
any GDD or make a new gameplay decision.

## Sources

- `design/gdd/player-noise.md`
- `design/gdd/player-third-person-controller.md`
- `design/gdd/player-movement-hide.md`
- `design/registry/entities.yaml`
- `docs/architecture/adr-0001-event-messaging-bus.md`
- `docs/architecture/adr-0002-physics-collision-contract.md`
- `src/AI/Core/EventBus.cs`
- `src/AI/Core/VirtualTickClock.cs`

## Ownership Matrix

| Contract | Owning system | Player Noise relationship | Current evidence |
|----------|---------------|---------------------------|------------------|
| Movement state and stride commit | Player Controller `#11` | Publishes `step_event`; Noise consumes it without recomputing the ledger | GDD specified; no dedicated Player Controller implementation surface found under `src/` |
| `Throw` action edge | Input `#19` | Delivers an edge to Noise while Burst is `Carried` | Provisional GDD contract; no Input implementation surface found under `src/` |
| `Interact` action edge | Input `#19` | Arms a `Placed` pickup after reach/prompt eligibility | Provisional GDD contract; no Input implementation surface found under `src/` |
| Camera azimuth at throw edge | Camera/Aim `#20` | Supplies the authoritative horizontal launch direction | Provisional GDD contract; no Camera/Cinemachine implementation surface found under `src/` |
| Near-zero aim fallback | Input/Camera integration | Uses resolved player facing, never stale render-frame camera data | GDD specified; no executable verification found |
| Crouch-to-stand throw gate | Player Controller + Input | Rejects the edge before spend when stand clearance fails | Controller GDD specifies stand probe; no integrated test found |
| `Carried` / `Placed` / `Consumed` lifecycle | Player Noise + Level | Noise consumes the state transitions; pickup owns instance state | GDD and registry specified; no lifecycle fixture found |
| Death and segment reset | Save/Session + gameplay lifecycle | Preserves or invalidates states according to the permanent-spend rule | GDD specified; no Save/Session implementation surface found under `src/` |
| Full room restart | Save/Session / scene boundary | Restores authored pickup instances to `Placed` | GDD specified; no recovery test found |
| `attempt_epoch` | Session lifecycle / Event Bus boundary | Invalidates stale queued facts after death, respawn or segment reset | Registry and ADR contracts specified; no executable test found |
| `fact_id` seed and lifetime | Noise + session boundary | Noise allocates session-scoped IDs; session reset is the only counter reset | Registry and Noise GDD specified; no implementation surface found |
| Pause and resume | Virtual clock / gameplay session | Paused time does not advance flight, hearing, timers or input | `VirtualTickClock` exists; no integrated pause test found |

## Required Event Ordering

The implementation must preserve this logical order for a throw:

```text
Input edge
  → resolve stance and stand gate
  → sample camera azimuth once
  → fallback to player facing if aim is near zero
  → perform launch preflight
  → spend Burst only after accepted preflight
  → start fixed-step flight
  → publish one landing fact through Event Bus
```

The following must not happen:

- sampling camera direction after the input edge;
- using a stale render-frame camera value;
- spending a Burst before initial-overlap rejection;
- restoring a consumed Burst on death or segment reset;
- buffering an input edge across pause/resume;
- resetting `fact_id` during pooling, disable/enable, death or segment reset;
- allowing a stale prior-epoch fact to reach Perception or the FSM.

## Current Gaps

### Implementation gaps

- No dedicated Input action-map implementation was found under `src/`.
- No dedicated Camera/Cinemachine aim implementation was found under `src/`.
- No Save/Session or checkpoint implementation was found under `src/`.
- No Player Noise runtime implementation was found under `src/`.

### Verification gaps

No automated evidence was found for:

- one-shot `Throw` edge capture;
- duplicate edge suppression;
- camera azimuth capture at the exact edge;
- near-zero aim fallback to facing;
- blocked crouch-to-stand rejection before spend;
- moving throw position/velocity snapshot;
- carried Burst survival through death;
- consumed Burst persistence through death and segment reset;
- full-room restart restoration;
- epoch increment and stale-fact discard;
- pause/resume input and timer behavior;
- preview/runtime launch-direction parity.

## Proposed Verification Matrix

| Test group | Minimum cases | Owner to implement |
|------------|---------------|---------------------|
| Input edge | one edge, held button, duplicate edge, edge during pause | Input / gameplay test harness |
| Aim capture | forward azimuth, wrap at 0/360, near-zero planar aim, moving camera after edge | Camera/Aim integration |
| Stance gate | standing throw, crouch with clear stand, crouch under ceiling, same-tick rejection | Player Controller + Noise integration |
| Resource lifecycle | carried death, in-flight death, landed consume, segment reset, full restart | Save/Session + Noise integration |
| Epoch safety | queued old fact, reset barrier, post-reset fact, repeated reset | Event Bus + session integration |
| Virtual time | pause during input, pause during flight, resume, scene/session boundary | Virtual clock integration |
| Parity | preview and runtime use identical launch direction and resolved contact | Noise/Physics integration |

## Readiness Assessment

**Design traceability: INCOMPLETE** — the Player Noise GDD names the required
contracts and boundaries, but the owning systems are still provisional or undesigned.

**Implementation readiness: BLOCKED** — Input, Camera/Aim and Save/Session seams do
not yet exist as implementation surfaces, and the acceptance boundaries have no
automated evidence.

**Review safety: SAFE TO KEEP SEPARATE** — this report does not change Player Noise,
its dependencies, its registry values or its review records. It can be updated later
without reopening the active design review.

## Next Low-Risk Actions

1. Confirm the Unity Input System package and action names without wiring gameplay.
2. Define a Camera/Aim seam that returns one azimuth sample per accepted edge.
3. Define a Save/Session state snapshot schema for pickup instances, carried state
   and `attempt_epoch`.
4. Add isolated contract tests before integrating Player Noise runtime code.
5. Re-run cross-system consistency checks only after Player Noise is Approved.

## Safety Boundary

This report intentionally does not modify:

- `design/gdd/player-noise.md`;
- `design/gdd/player-third-person-controller.md`;
- `design/gdd/perception.md`;
- `design/gdd/guard-ai-fsm.md`;
- `design/gdd/systems-index.md`;
- `design/registry/entities.yaml`;
- `design/levels/`;
- source code, review logs or session state.
