# Player Noise Cross-File Revision Design

**Date:** 2026-09-07  
**Status:** Approved for implementation planning  
**Scope:** Documentation-only revision; Player Noise remains **In Review**.

## 1. Context and problem

The fresh full Player Noise review on 2026-09-07 returned **NEEDS REVISION** with 49 deduplicated blockers and 19 advisories. The dominant failure mode was incomplete propagation: local GDD corrections were not consistently reflected in the registry, FSM/Perception contracts, route fixture, fixture schema, performance audit, and Physics ADR.

This revision therefore uses one cross-file contract matrix and one dependency-ordered sweep. It does not add runtime behavior, claim unavailable evidence, or change approval/tracking records.

## 2. Locked design decisions

The following decisions were approved before implementation:

- Pickup reach is a 3D sphere: `distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2`, followed by the E20 Linecast. The earlier `distance³` text was a typographical error; the intended operation is squared distance.
- F4 parity uses the authored landing **surface height** as `h_landing`. Projectile-center height after collision is a separate result derived from `surface_contact + normal * (projectile_radius + epsilon_contact)`.
- Audio onset evidence uses an instrumented DSP contract with `voice_instance_id` and `dsp_start_sample` from an injected audio clock or middleware adapter. Unity `AudioSource.timeSamples` is clip-relative and is not absolute DSP evidence.
- MVP E20 geometry is static. Hearing does not sync transforms per query; Burst performs one explicit `Physics.SyncTransforms()` before each simulation batch after authored solids are finalized.
- FSM owns the semantic suppressed-receipt event; presentation/audio renders the micro-tell and does not infer gameplay behavior.
- Investigate-to-Chase emits `LivenessFact op=promote`, retaining the episode and `entry_id`; it does not close Investigate and open a second episode.
- MVP corroboration/re-anchor remains valid. Peer recruitment and locked-zone escalation remain Target-only.
- Guard selection order is `guard_eid` ascending only. `stable_guard_snapshot_position` remains diagnostic/geometry data, never a sort key.

## 3. Canonical scope and authority

The sweep covers exactly these eight canonical files:

1. `design/gdd/player-noise.md` — NoiseEmitter behavior, source and terminal contracts.
2. `design/gdd/perception.md` — hearing geometry, admission, deadline, relay, and entry reservation.
3. `design/gdd/guard-ai-fsm.md` — response precedence, corroboration, liveness, and semantic response events.
4. `design/registry/entities.yaml` — values, schemas, identities, validators, and stable rejection codes.
5. `design/fixtures/noise-fixture-spec.md` — replay/fixture record schemas, parity, and evidence rules.
6. `design/levels/mvp-burst-route-fixture.md` — concrete room geometry, route, patrol windows, and records.
7. `design/gdd/sound_performance_audit.md` — performance, limiter, DSP provenance, and evidence-state gates.
8. `docs/architecture/adr-0002-physics-collision-contract.md` — PhysicsScene, E20, overlap, cast, sync, backface, and winding authority.

Authority is resolved in this order: Player Noise behavior contract, registry value/schema contract, Perception/FSM ownership contracts, fixture schema, concrete route fixture, audit and Physics ADR pins. A dependent file must not introduce a local exception to make a fixture pass.

## 4. Contract matrix

Every review blocker is tracked with these fields:

`blocker_id -> canonical rule -> authority owner -> affected files -> schema/record fields -> stable failure code -> acceptance/static check`

The matrix is the completion checklist. A blocker is not closed when one prose paragraph is edited; it is closed only when every affected authority and evidence consumer agrees.

## 5. Revision clusters

### Cluster A — Throw admission and initial overlap

- Walk/Run-labelled Throw edges reject before allocation/spend even when resolved planar speed is zero.
- Idle/standing at zero speed may accept after launch-clear checks.
- Crouch-Idle must resolve a same-tick stand before acceptance.
- Any positive planar speed rejects before allocation/spend.
- Initial overlap is a pre-spend rejection, not a flight terminal.
- The initial-overlap query has an exact operation, injected `PhysicsScene`, E20 mask, and `QueryTriggerInteraction.Ignore`.

### Cluster B — Burst physics, timing, and identity

- Burst hearing anchors to `terminal_publication_time`; `source_timestamp` remains allocation/order metadata.
- `theta_rad` conversion is explicit and occurs once before trigonometric evaluation.
- `d_step` means center-segment magnitude; valid backpedal is not rejected by sign.
- `ThrowSnapshot` has an explicit producer and immutable field-for-field parity across runtime, preview, and fixture.
- `stride_id` has a producer and uniqueness scope.
- Integer fixed ticks are timeout authority; float elapsed values are diagnostics only.
- Timestamp quantization, batch definition, backlog cap, pause behavior, and the 22 ms tolerance have one registered interpretation.

### Cluster C — Corroboration, re-anchor, and liveness

- MVP single-guard re-anchor remains in scope; peer-recruit and locked-zone are Target-only.
- Anchors are selected by episode-open cause: raw noise, authored hide spot, or cap-reached position.
- `t_noise_recommit_cooldown` applies after resolution, never to live corroboration.
- Same-area/window boundaries and third-fact saturation are shared across GDD, registry, and fixtures.
- Investigate-to-Chase is `promote` with the existing `entry_id`.
- FSM emits the semantic suppressed-receipt event; presentation only renders it.

### Cluster D — Event identity and ordering

The three ordering domains remain distinct:

1. Guard selection: `guard_eid` ascending.
2. Fact order: `(source_timestamp, source_event_class_rank, source_event_id, fact_id)`.
3. Pair dispatch: fact order plus `guard_eid`.

Movement and Burst source identities use separate `step:<id>` and `flight:<id>` namespaces. Arrival order, queue insertion order, and listener churn never become identity or gameplay ordering.

### Cluster E — Hearing geometry and Physics authority

- E20 uses exact project layer names, not an abstract “World layer family”.
- Initial overlap, Burst swept collision, and hearing Linecast retain separate operations.
- Guard feet are the falloff datum; guard eye is the occlusion endpoint.
- Pickup reach is 3D sphere plus E20 Linecast.
- MVP E20 geometry is static; Burst syncs once per batch.
- Backface/winding, NavMesh-layer separation, and contentful-scene validation are aligned with ADR-0002.

### Cluster F — Audio and performance evidence

- DSP evidence contains `voice_instance_id` and `dsp_start_sample` from an instrumented clock/adapter.
- Limiter measurements include provenance and explicit evidence state.
- The 22 ms value is classified consistently as a registered default/floor and is not silently reused as every pass threshold.
- Whole-frame WebGL gates and subsystem decomposition remain separate.
- `UNCAPTURED`, `PENDING_OQ6`, `pending`, `estimated`, `unsupported`, and `limiter_unsupported` remain non-passing.

### Cluster G — Acceptance and route fixture

- AC6, AC10, AC12, AC14, AC15, AC18, AC19, and AC20 receive complete Given/When/Then legs, identities, and actual/evidence fields.
- Fixture schema is updated before the concrete route fixture.
- Route records cover variant isolation, patrol schedule, phase-window validity, landing probes, void/ceiling/wall negatives, segment-reset lifecycle, gate physical contract, and pickup sphere checks.
- Positive actuals require real capture provenance; negative records require complete inputs, stable rejection code, no-spend/no-launch result, and validator outcome.

## 6. Dependency-ordered execution

1. Freeze status and populate the contract matrix.
2. Update `player-noise.md` as the behavioral source, including all locked choices.
3. Update `entities.yaml` values, schemas, identity/order tuples, validators, and rejection codes.
4. Propagate to `perception.md` and `guard-ai-fsm.md`.
5. Update `noise-fixture-spec.md` as the schema authority.
6. Update `mvp-burst-route-fixture.md` against the revised schema and registry.
7. Align `sound_performance_audit.md` and `adr-0002-physics-collision-contract.md`.
8. Run the full cross-file verification suite and fresh design review.

## 7. Verification gates

### Gate 0 — Matrix completeness

Every blocker has an owner, affected files, and a validation method.

### Gate 1 — Authority/registry parity

Every Player Noise rule has either a matching registry field or an explicit prose-only classification. No registry value contradicts the GDD.

### Gate 2 — Perception/FSM parity

The same input envelope yields the same hearing, relay, response, and liveness interpretation across Player Noise, Perception, and FSM.

### Gate 3 — Fixture parity

The route fixture validates against the fixture schema and registry. No route-only field lacks a schema or registry owner.

### Gate 4 — Audit/ADR parity

Audit and Physics ADR statements do not become a second gameplay authority; gameplay contracts point back to Player Noise or registry.

### Gate 5 — Static and evidence checks

Run required-heading scans, stale-term/value scans, cross-file markers, duplicate-key-aware YAML validation when available, schema/field parity, negative-record completeness, F4 numerical checks, ordering/identity checks, fail-closed evidence checks, Markdown fence checks, and `git diff --check`.

## 8. Non-goals and evidence policy

- No runtime source implementation is added in this revision.
- No Unity, NUnit, WebGL, target-hardware, middleware, NavMesh, or DSP capture is claimed unless actually executed.
- No evidence state is upgraded to make a gate pass.
- No Player Noise approval/tracking record is changed before a genuine fresh full review returns **APPROVED**.
- No commit or push is performed without explicit user instruction.

## 9. Final acceptance

The revision is complete only when all 49 blockers are closed or have a documented technical disposition, the six direct cross-file contradictions are gone, all eight files agree on the locked decisions, no actual evidence is fabricated, and Player Noise remains **In Review** pending the fresh full design review.
