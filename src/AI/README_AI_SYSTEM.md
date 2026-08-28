# Guard AI FSM Implementation

## Overview
This directory contains the complete implementation of the Guard AI Finite State Machine (FSM) for Whisper Ward, based on the GDD in `design/gdd/guard-ai-fsm.md` and the entity registry in `design/registry/entities.yaml`.

## Architecture Overview
The implementation follows a strict event-driven (event-not-command) architecture with:
1. **Shared Virtual Tick Clock** - Ensures determinism
2. **R12 Event Bus** - Handles communication between Perception and FSM
3. **3-State FSM** - Patrol → Investigate → Chase (strict cap)
4. **Decision Record Publishing** - FSM never queries Perception directly

## Core Components

### Infrastructure
- `VirtualTickClock.cs` - Shared tick authority (0.5s default)
- `EventBus.cs` - Pub/sub system for R12 events
- `IEvent.cs` - Base interface for all events

### Perception Schema (`src/AI/Perception/`)
- `R12Schema.cs` - Complete implementation of all R12 sensing facts and decision records
  - SensingFacts: LOSGain, LOSBreak, NoiseHeard, ThresholdCrossing, ConfirmWindowElapsed, CapReached, Reachability, ChaseReached
  - DecisionRecords: InvestigateCommit, ChaseEntry, ChaseEnd, InvestigateResolution, Capture

### FSM System (`src/AI/FSM/`)
- `GuardFSM.cs` - Central FSM controller
- `IGuardState.cs` / `GuardStateBase.cs` - State interface and base class
- `PatrolState.cs` - Waypoint consumption with dwell/scan arcs
- `InvestigateState.cs` - Target search, thoroughness-scaled look-around, give-up clock (D1)
- `ChaseState.cs` - Live pursuit, spot-front hold, and Catch contract
- `GuardGoalMode.cs` - Parallel goal mode for catch-gate
- `PatrolRoute.cs` - Authored route data structure

### Navigation & Catch Contract (`src/AI/Navigation/`)
- `GuardNavigator.cs` - NavMeshAgent wrapper with design speeds
- `CatchEvaluator.cs` - Implements the full Catch contract (C1.4):
  - Nav-goal gate (LivePursuit/HideSpotFront only)
  - Path-arrival metric (NavMeshAgent.CalculatePath)
  - Partial path validation (Physics.Linecast backstop)
  - Catch timer with hysteresis and DeltaY tolerance

### Testing & Verification (`src/AI/Testing/`)
- `PerceptionDriver.cs` - Simulated Perception for H.0 automated tests
- `DecisionTap.cs` - Captures decision records for assertion
- `FSMVerificationSuite.cs` - Complete test suite verifying all ACs
- `GuardAISystem.cs` - Unity entry point component

## Key Formulas Implemented
- **D1 Give-up Clock**: `t_giveup = t_giveup_base × s_diff × (1 + k_thorough × R/R_max)`
- **D2 Thoroughness Tau**: `tau = 1 + k_thorough × (R/R_max)`
- **D3 Sweep Duration**: `t_sweep = t_sweep_base × tau`
- **D4 Chase Speed**: `V_chase = rho_chase × V_run(runtime-max)`
- **D5 Catch Range Invariant**: `catch_range > V_run × T_sample_max + hyst + r_guard + r_player + stopping_distance`
- **D6 Patrol Period**: `T_patrol_loop = Σ_i (edge_i/V_patrol + t_dwell_i + t_scan_i)`

## Design Constraints Honored
- Strict 3-state cap (Patrol/Investigate/Chase)
- Event-not-command boundary (no Perception queries)
- Virtual tick clock determinism
- Per-tier suppression rule (investigate-commit/chase-entry)
- Catch contract backstop (Physics.Linecast + NavMeshAgent)
- GuardGoalMode parallel state for catch-gate
- All tuning knobs consumed from registry (never re-declared)

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
- GuardAISystem (entry point)
- GuardFSM
- PerceptionDriver (for testing) or real Perception integration
- DecisionTap (for testing/observation)
- GuardNavigator
- CatchEvaluator
- PatrolRoute (authored by Level design)