# Guard AI FSM Implementation

## Overview
This directory contains the complete implementation of the Guard AI Finite State Machine (FSM) and its session runtime (noise emission, hearing scheduling, Burst lifecycle, event transport) for Whisper Ward, based on the GDD in `design/gdd/guard-ai-fsm.md` and the entity registry in `design/registry/entities.yaml`.

## Architecture Overview
The implementation follows a strict event-driven (event-not-command) architecture with:
1. **Shared Canonical Virtual Tick Clock** - 1/120 s fixed substep; hearing (5 Hz) and FSM (0.5 s) are derived cadences — ensures determinism
2. **Session Event Bus** - Transactional pub/sub transport with bounded retry and epoch/session barriers; handles communication between Perception and FSM
3. **3-State FSM** - Patrol → Investigate → Chase (strict cap)
4. **Decision Record Publishing** - FSM never queries Perception directly; it consumes published relays and publishes immutable LivenessFacts that Perception reads at the previous boundary

## Core Components

### Infrastructure (`src/AI/Core/`)
- `VirtualTickClock.cs` - Shared canonical tick authority (1/120 s fixed substep; hearing 5 Hz and FSM 0.5 s cadences are derived boundaries)
- `EventBus.cs` - Session-scoped pub/sub transport with transactional handoff (`SubscribeHandoff<T>`), bounded retry, and epoch/session barriers
- `IEvent.cs` - Base interfaces and envelope contracts for all events
- `SessionBoundaryService.cs` - Session/attempt-epoch barrier owner; notifies lifecycle listeners before bus generation advance
- `SessionPhaseCoordinator.cs` - Deterministic phase order: GameplayIngress drain -> ingress sources -> GameplayIngress drain -> Hearing drain -> hearing participants -> FsmDecision drain -> FSM participants -> Presentation drain
- `NoiseSourceRecord.cs` - Typed Movement/Burst source records with envelope-aware Burst identity (`ulong flight_handle_id`)
- `NoiseRuntimeConfiguration.cs` - Immutable registry-backed hearing/FSM runtime contract (registered 5 Hz hearing, 0.5 s FSM cadence)
- `BurstRuntimeConfiguration.cs` - Immutable registry-backed Burst contract (`1/120 s` canonical fixed substep)
- `PhysicsQueryProfile.cs` - Validated cached E20 mask (`LayerMask.NameToLayer`, forbidden layers, `QueryTriggerInteraction.Ignore`)

### Perception (`src/AI/Perception/`)
- `R12Schema.cs` - Immutable sensing facts and decision records (NoisePublished, NoiseHeardRelay, LivenessFact, RelayConsumption, NoiseConsumptionOutcome)
- `NoiseEmitter.cs` - Deterministic source-aware emitter: source dedup, `(source_timestamp, source_event_class_rank, source_event_id)` flush order, monotonic `ulong fact_id`
- `PerceptionHearingService.cs` - Independent 5 Hz hearing scheduler: bounded queues, deadline from source timestamp, stable guard snapshots, immutable relays, Perception-owned `entry_id`
- `BurstSimulationService.cs` - Burst lifecycle: service-owned monotonic `ulong flight_handle_id`, fixed-step simulation, initial-overlap before spend, explicit terminal publication states

### FSM System (`src/AI/FSM/`)
- `GuardFSM.cs` - Central FSM controller with registry cadence gating, relay dedup `(session_id, attempt_epoch, guard_eid, fact_id)`, and FSM-owned LivenessFact publication
- `IGuardState.cs` / `GuardStateBase.cs` - State interface and base class
- `PatrolState.cs` - Waypoint consumption with dwell/scan arcs; the only state opening noise-led Investigate
- `InvestigateState.cs` - Target search, thoroughness-scaled look-around, registered difficulty-scaled re-anchor formula
- `ChaseState.cs` - Live pursuit, spot-front hold, and the state-owned Catch contract (injected PhysicsQueryProfile)
- `GuardGoalMode.cs` - Parallel goal mode for catch-gate
- `PatrolRoute.cs` - Authored route data structure

### Navigation (`src/AI/Navigation/`)
- `GuardNavigator.cs` - NavMeshAgent wrapper with authored patrol, investigate, and chase speeds
- `CatchEvaluator.cs` - **[Obsolete] compatibility shell**; catch authority belongs to ChaseState/InvestigateState and the injected PhysicsQueryProfile

### Testing & Verification (`src/AI/Testing/`)
- `PerceptionDriver.cs` - Fixture adapter for scripted sensing facts (NOT the production hearing path)
- `DecisionTap.cs` - Captures decision records for assertion
- `FSMVerificationSuite.cs` - Test suite verifying the FSM ACs
- `GuardAISystem.cs` (root: `src/AI/`) - Production composition root: canonical clock check, profile validation, phase-coordinator wiring (`Burst.Advance -> FlushNoiseSources -> ResolvePendingBurstPublication -> ProcessHearingTick`)

## Key Formulas Implemented
- **D1 Give-up Clock**: `t_giveup = t_giveup_base × s_diff × (1 + k_thorough × R/R_max)` where `s_diff` is the registered difficulty scalar
- **D1b Re-anchor Budget**: `t_reanchor = t_giveup_base × s_diff × (1 + k_thorough × R_reanchor/R_max) + t_noise_reanchor_extend`
- **D2 Thoroughness Tau**: `tau = 1 + k_thorough × (R/R_max)`
- **D3 Sweep Duration**: `t_sweep = t_sweep_base × tau`
- **D4 Chase Speed**: `V_chase = rho_chase × V_run(runtime-max)`
- **D5 Catch Range Invariant**: `catch_range > V_run × T_sample_max + hyst + r_guard + r_player + stopping_distance`
- **D6 Patrol Period**: `T_patrol_loop = Σ_i (edge_i/V_patrol + t_dwell_i + t_scan_i)`

## Design Constraints Honored
- Strict 3-state cap (Patrol/Investigate/Chase)
- Event-not-command boundary (no Perception queries; FSM reads only published facts)
- Canonical virtual tick clock determinism (1/120 s; hearing 5 Hz and FSM 0.5 s derived cadences)
- Per-tier suppression rule (investigate-commit/chase-entry)
- Catch authority in ChaseState/InvestigateState with the injected PhysicsQueryProfile (CatchEvaluator is an obsolete shell)
- GuardGoalMode parallel state for catch-gate
- All tuning knobs consumed from registry (never re-declared)
- Liveness facts are FSM-owned, immutable, and read by Perception at the previous boundary

## Acceptance Criteria Covered (H.0)
1. Hard 3-state cap verification
2. Per-tier suppression rule enforcement
3. Chase-end record publication
4. Give-up clock formula (D1)
5. Chase speed coupling (D4)
6. Catch-range invariant (D5)
7. Constraint chain (D7)
8. GuardGoalMode enum usage
9. Post-chase sweep timing (D3)
10. Catch timer duration verification

## Build Instructions
This system requires:
- Unity 6 LTS (6000.3.17f1)
- Navigation components (NavMeshAgent)
- No external dependencies

Place all components on a GameObject with:
- GuardAISystem (production composition root; injects bus, clock, profiles, and phase coordinator)
- GuardFSM
- VirtualTickClock (session-owned gameplay clock; driven via `GuardAISystem.AdvanceGameplayTime`)
- DecisionTap (for testing/observation)
- GuardNavigator
- PatrolRoute (authored by Level design)

PerceptionDriver is a fixture adapter only; production hearing uses
`PerceptionHearingService` wired by `GuardAISystem`.