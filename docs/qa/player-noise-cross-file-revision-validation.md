# Player Noise Cross-File Revision Validation

- Review source: 2026-09-07 fresh full design review; fresh follow-up panel run 2026-09-08
- Validation run: 2026-09-08; `design/fixtures/noise-fixture-spec.md` SHA-256 `9e5bf6cd6bf45419f2f93b5a25e61ce966a10a13208b2ce0a287aace8864f4c6`
- Scope: documentation-only; Player Noise remains In Review
- Runtime/platform evidence: not captured unless explicitly listed below

## Blocker Matrix

The matrix is the handoff checklist for Tasks 2–7. `PASS (static)` means only that the documentation-slice check ran and passed; it is not a semantic design-review approval. Fresh-review blockers and the readiness disposition are recorded in **Fresh Review Handoff** below.

| cluster | canonical authority | affected files | check | status | notes |
|---|---|---|---|---|---|
| A — Throw admission and initial overlap | `design/gdd/player-noise.md` behavior contract; `design/registry/entities.yaml` rejection/schema contract | `player-noise.md`, `entities.yaml`, `noise-fixture-spec.md`, `mvp-burst-route-fixture.md`, `adr-0002-physics-collision-contract.md` | Walk/Run labels reject at zero speed before allocation/spend; Crouch-Idle stand resolution; positive-speed rejection; initial overlap is pre-spend/no terminal; exact injected PhysicsScene E20 overlap query with ignored triggers | PASS (static) | Exact admission/rejection wording, registered stable codes, overlap operation, and fixture parity scan passed. Runtime execution remains unavailable. |
| B — Burst physics, timing, and identity | `player-noise.md` Burst contract; registry owns registered values/schema | `player-noise.md`, `entities.yaml`, `perception.md`, `noise-fixture-spec.md`, `mvp-burst-route-fixture.md`, `sound_performance_audit.md` | `terminal_publication_time` anchors hearing; one-time `theta_rad`; magnitude `d_step`; immutable `ThrowSnapshot` producer/parity; `stride_id` producer/scope; integer tick timeout; quantization, batch, backlog, pause, and 22 ms interpretation | PASS (static) | Timing, snapshot, identity, fixed-tick, quantization, and performance-decomposition wording is aligned. Runtime/platform capture remains unavailable. |
| C — Corroboration, re-anchor, and liveness | `player-noise.md` and `guard-ai-fsm.md` ownership contracts; registry profile values | `player-noise.md`, `perception.md`, `guard-ai-fsm.md`, `entities.yaml`, `noise-fixture-spec.md`, `mvp-burst-route-fixture.md` | MVP re-anchor retained; Target-only peer/locked-zone; cause-specific anchors; cooldown only after resolution; shared windows/saturation; `promote` retains episode and `entry_id`; FSM owns semantic suppressed receipt | PASS (static) | Cross-file ownership and liveness scan passed; no runtime corroboration capture was performed. |
| D — Event identity and ordering | `player-noise.md` identity/order contract; registry schema | `player-noise.md`, `perception.md`, `guard-ai-fsm.md`, `entities.yaml`, `noise-fixture-spec.md`, `mvp-burst-route-fixture.md` | Separate guard (`guard_eid` ascending), fact, and pair-dispatch orders; `step:`/`flight:` namespaces; no arrival/queue/listener ordering leakage; diagnostic position never a sort key | PASS (static) | Namespace ownership, tuple parity, and diagnostic-position non-ordering checks passed. |
| E — Hearing geometry and Physics authority | `player-noise.md` hearing contract; `adr-0002-physics-collision-contract.md` Physics authority | `player-noise.md`, `perception.md`, `entities.yaml`, `noise-fixture-spec.md`, `mvp-burst-route-fixture.md`, `adr-0002-physics-collision-contract.md` | Exact project layer names; separate overlap/sweep/Linecast operations; feet falloff and eye occlusion endpoint; 3D pickup sphere plus E20 Linecast; static MVP and one sync per batch; backface/winding/NavMesh/contentful-scene parity | PASS (static) | Physics and geometry authority scan passed, including pickup boundary arithmetic and static sync policy. No runtime/scene capture was performed. |
| F — Audio and performance evidence | `sound_performance_audit.md` evidence contract; registry evidence states | `player-noise.md`, `entities.yaml`, `noise-fixture-spec.md`, `sound_performance_audit.md` | DSP onset has `voice_instance_id` + `dsp_start_sample` from injected clock/adapter; limiter provenance/evidence state; 22 ms default/floor classification; whole-frame vs subsystem gates; fail-closed states stay non-passing | PASS (static) | Evidence schema and authoritative 33.0 ms whole-frame versus diagnostic 2.0/12.0 ms wording passed. OQ3/OQ6 and actual captures remain unresolved/non-passing. |
| G — Acceptance and route fixture | `noise-fixture-spec.md` schema first, then registry and route fixture | `player-noise.md`, `entities.yaml`, `noise-fixture-spec.md`, `mvp-burst-route-fixture.md`, `sound_performance_audit.md`, `adr-0002-physics-collision-contract.md` | AC6/10/12/14/15/18/19/20 complete Given/When/Then legs and actual/evidence fields; schema-before-route; route records cover all required positives/negatives/lifecycle/gate/pickup checks; negative records are complete and fail-closed | PASS (static) | Schema/identity parity, F4 arithmetic, route timing, required actual fields, and fence/whitespace checks passed. Fixture execution remains uncaptured. |

## Locked Decisions

The following decisions are authoritative for the sweep and must be propagated without local exceptions:

1. Pickup reach is a 3D sphere followed by E20 Linecast: `distance_squared(player_feet, pickup_anchor) <= pickup_reach_radius^2`.
2. F4 parity uses authored landing surface height as `h_landing`; projectile-center height is separately derived from `surface_contact + normal * (projectile_radius + epsilon_contact)`.
3. Audio onset evidence requires an instrumented DSP contract carrying `voice_instance_id` and `dsp_start_sample` from an injected audio clock or middleware adapter; Unity `AudioSource.timeSamples` is clip-relative and not absolute DSP evidence.
4. MVP E20 geometry is static. Burst performs one explicit `Physics.SyncTransforms()` before each simulation batch after authored solids are finalized; hearing does not sync transforms per query.
5. FSM owns the semantic suppressed-receipt event; presentation/audio renders the micro-tell and does not infer gameplay behavior.
6. Investigate-to-Chase emits `LivenessFact op=promote`, retaining the episode and `entry_id`; it does not close Investigate and open a second episode.
7. The episode-open corroboration anchor decision remains locked: raw-noise episodes use the first accepted raw noise origin/publication time; hide-entry uses the authored hide-spot position; cap-forced uses the cap-reached position.
8. Guard selection order is `guard_eid` ascending only; `stable_guard_snapshot_position` remains diagnostic/geometry data and never a sort key.

## Static Checks

A follow-up documentation-slice gate was run on 2026-09-08 after the Unity/Physics blocker corrections and whitespace-only cleanup. Results:

- Required Player Noise headings (all eight): **PASS**.
- Markdown fence balance across the eight canonical files: **PASS**.
- Active contradiction scan: **PASS**. Explicit fail-closed wording such as “never a sort key,” “not a terminal,” “not absolute DSP evidence,” and “not the deadline anchor” was classified as compliant rather than as a contradiction.
- Trailing-whitespace scan across the eight canonical files: **PASS**.
- `git diff --check` across the eight canonical files: **PASS**. Git emitted only normal LF-to-CRLF working-tree warnings for pre-existing modified files; no whitespace errors were reported.

## Numeric/Schema Checks

- Registry/fixture/route field inventory: **PASS (lexical/static)** for the exact PickupInteraction LevelFixture fields, trace-event Pickup fields, Queue serialized-field boundary, deterministic order tuple presence, namespaced source identities, ThrowSnapshot velocity semantics, and audio evidence fields.
- Queue ordering parity: **PASS (static)**. Guard selection is `[guard_eid]`; fact admission is `[source_timestamp, source_event_class_rank, source_event_id, fact_id]`; pair dispatch is `[source_timestamp, source_event_class_rank, source_event_id, fact_id, guard_eid]`. `stable_guard_snapshot_position` remains diagnostic data and `admission_order_key` remains a validator concept, not a serialized Queue field.
- Semantic cross-file schema parity: **PASS (static revision)** for `step:<step_id>` / `flight:<flight_handle_id>` serialized identities, finite authored `h_landing` including zero/negative/positive boundary legs, AC10 rejection-leg/code coverage, `AUTHORED_THETA_OUTSIDE_BAND_DOMAIN`, field-for-field authoritative `resolved_velocity`, unique canonical lower-landing route binding, and adapter-owned audio provenance. Runtime execution remains unavailable.
- F4 deterministic arithmetic: **PASS** for `throw_flat_starter_01` and `throw_lower_landing_01` at `pure_math_relative <= 1e-6`; discriminant, selected positive root, and range recomputed from authored inputs.
- Pickup sphere boundary arithmetic: **PASS** (`1.80 m² <= 2.56 m²` for the documented example).
- Route phase/timing arithmetic: **PASS** for the authored `min_validity_s = 1.5`, fixed `dt_max = 1/120 s`, and registered `timeout_boundary_s = 360 × dt_max = 3.0 s`.
- Duplicate-key-aware YAML validation: **NOT PERFORMED — parser unavailable**. No duplicate-key safety is inferred from a normal YAML load.

## Evidence Limitations

- This remains a documentation-only validation slice. No Unity, NUnit, WebGL, target-hardware, middleware, DSP, NavMesh, or runtime fixture capture was executed.
- `UNCAPTURED`, `PENDING_OQ6`, `pending`, `estimated`, `unsupported`, and `limiter_unsupported` remain non-passing states.
- Static parity, formula recomputation, marker presence, and prose alignment cannot substitute for runtime/platform evidence.
- The Task 5 fixture-spec corrections have passed local schema/static checks, but the requested fresh independent Task 5 re-review was unavailable because the agent launch was blocked; no independent PASS/APPROVED verdict is claimed for that sub-review.
- Player Noise approval/tracking records are outside this task and remain unchanged.

## Fresh Review Handoff

A fresh follow-up review panel was launched against the pre-normalization canonical set on 2026-09-08. The panel was **PARTIAL** because the systems-design reviewer terminated from context exhaustion and the remaining full-panel synthesis could not be completed. The completed scoped reviewers returned **NEEDS REVISION**; that verdict applies to the prior revision and no APPROVED verdict is claimed for this normalization pass.

The seven documented blockers were addressed in the current documentation revision as follows:

1. Serialized source identities are now namespaced as `step:<step_id>` and `flight:<flight_handle_id>`; bare IDs are explicitly internal allocation/dedup fields.
2. AC6a now defines finite authored world-space `h_landing`, including zero, negative, and positive boundary legs; projectile-center push-out remains separate.
3. AC10 now enumerates zero, negative, non-finite, angle, `dt_max`, radius-order, cadence-order, and cadence-margin rejection legs with stable `CFG_*` mappings.
4. `AUTHORED_THETA_OUTSIDE_BAND_DOMAIN` is present in the stable Level result-code vocabulary and remains distinct from `CFG_INVALID_ANGLE`.
5. `throw_lower_landing_01` is the canonical causal MVP volume bound to `geometry_throw_lower_open_01` and `world_throw_lower_landing_01`; the flat volume is supplemental formula coverage only. The discriminant-negative case remains `geometry_validation_result: NOT_EVALUATED`.
6. `ThrowSnapshot.resolved_velocity` is the authoritative projectile initial velocity; player velocity is excluded or separately diagnostic.
7. ADR-0003 and the sound audit now require adapter-owned absolute DSP provenance (`voice_instance_id`, `dsp_start_sample`, `sample_rate`, `audio_clock_source`, `integration_source`, `epoch_offset`); Unity clip-relative `AudioSource.timeSamples` and unqualified Wwise readback cannot produce `confirmed`; limiter `not_applicable` is restricted and `limiter_unsupported` remains fail-closed.

These are static documentation results only. The Unity/Physics review's implementation prerequisites remain open, and no reviewer claimed Unity, NUnit, NavMesh, DSP, middleware, WebGL, or target-hardware evidence.

## Post-Normalization Review Panel (2026-09-08)

A fresh independent review panel was run against the normalized set. Results:

| Reviewer | Verdict | Notes |
|---|---|---|
| audio-director | NEEDS REVISION (5 findings) | Findings 2–4 (field vocabulary `requested_virtual_time`/`reported_virtual_time` vs `virtual_cue_request`/`virtual_dsp_onset`; `estimated`-state estimate field; limiter-matrix wording) were fixed in this revision pass after the review. Findings 1 and 5 (OQ3 middleware ADR pending; no audio/performance capture passable) are standing fail-closed constraints, not documentation defects. |
| qa-lead | **PASS** (0 blockers) | All 15 previously documented blocker families verified closed, including identity ownership, ordering keys, two-timestamp contract, namespaces, latch, re-anchor formula, validator split, AC6a/AC10 completeness, gate causality, failed-spend/epoch, and fail-closed evidence behavior. Non-blocking observations: registry umbrella entry hygiene, ADRs remain Proposed (expected at Systems Design stage), intentionally non-passing runtime gates preserved. |
| systems-designer | terminated (context exhaustion) | No report; re-launch pending. |
| game-designer | no report returned | Launched against the pre-normalization set; no verdict returned. |
| level-designer | blocked at launch | Classifier blocked the launch; no report. |
| performance-analyst | blocked at launch | Classifier blocked the launch; no report. |
| unity-specialist | **NEEDS REVISION** | Three documentation blockers were identified: float-based timeout wording in ADR-0002, flat geometry in the canonical teaching/receiver records, and max-frame-delta precision wording. All three were fixed in the follow-up correction pass below; no runtime evidence was claimed. |

The panel is therefore **PARTIAL**: one PASS, one NEEDS REVISION with its actionable findings fixed, one NEEDS REVISION with its three documentation findings fixed in the follow-up pass, and four missing scopes. No APPROVED verdict is claimed. Player Noise remains **In Review**.

## Unity/Physics Correction Pass (2026-09-08)

The Unity-specialist findings were corrected without changing runtime code or claiming engine evidence:

1. ADR-0002 now makes the integer fixed-tick predicate authoritative: `timeout_tick = 360` for `dt_max = 1/120 s`; accumulated `flight_elapsed` is diagnostic/replay data only.
2. `teach_certified_throw_01` and `receiver_from_captured_landing_01` now bind to `geometry_throw_lower_open_01` and `world_throw_lower_landing_01`, matching the canonical causal route. The flat volume remains supplemental formula coverage only.
3. `max_frame_delta_s` now uses the registry's rounded `0.0667 s` serialization and explicitly identifies the exact value as `8 × (1/120 s) = 0.066666… s`.

Static recheck: ADR timeout marker, route bindings, max-frame-delta parity, Markdown fence balance, and `git diff --check` all **PASS**. Unity/runtime/target-hardware evidence remains unavailable.

## Final Gate

**NOT READY — normalization and Unity/Physics correction static checks pass; the fresh panel remains partial (one PASS, two scoped NEEDS REVISION results with actionable findings fixed, four missing scopes).** Player Noise remains **In Review**; unavailable runtime/platform evidence remains fail-closed; no approval/tracking update, commit, or push was performed.

## Fixture Schema Correction Recheck (2026-09-08)

The latest fixture/registry correction was rechecked after adding the complete-key fields:

- `ValidThrowVolumeRecord` parity: **PASS (static)** for `route_role`, `causal_route_binding`, and `throw_snapshot_parity` across the fixture specification, route fixture, and registry.
- Route-causality parity: **PASS (static)** for `causal_volume_id`, `causal_geometry_variant_id`, `causal_landing_surface_id`, and `liveness_continuity` across the fixture specification, route fixture, and registry.
- `GateTransitionRecord.receiver_binding` parity: **PASS (static)** across the fixture specification, route fixture, and registry.
- `ReceiverProbeRecord.landing_surface_id` parity: **PASS (static)** in Player Noise AC20, the fixture specification, registry, and all eight `receiver_probes` records. Non-landing probes explicitly use `null`; the captured landing probe resolves to `world_throw_lower_landing_01`.
- Canonical causal route remains unique: `throw_lower_landing_01` → `geometry_throw_lower_open_01` → `world_throw_lower_landing_01`; flat-ground coverage remains supplemental formula evidence, and the discriminant-negative case remains formula-only with `geometry_validation_result: NOT_EVALUATED`.
- Corrected fast preflight: target eight-heading check, canonical Markdown-fence balance, trailing whitespace, `git diff --check`, namespaced identity parity, authoritative `resolved_velocity`, audio evidence fields, PhysicsScene authority, fail-closed vocabulary, schema parity, and receiver complete-key coverage: **PASS**.
- Intentional registry tombstone mentions of `S_DIFF`/`reanchor_speed_ratio` remain historical `STRUCK` notes and are not active contract references; no active obsolete-term use was found.
- Duplicate-key-aware YAML validation: **NOT PERFORMED — parser unavailable**. No duplicate-key safety is inferred from a normal YAML load.

This correction closes the fixture-schema blocker. A follow-up fixture/registry review then found one additional vocabulary omission: the route fixture's stable result-code set did not list the registry-defined `AUTHORED_THETA_OUTSIDE_BAND_DOMAIN` and `THROW_WHILE_MOVING_REJECTED` codes already required by its own records and AC6b. Both codes are now included in the route fixture list; the post-edit static preflight now passes.

Lifecycle/ownership and identity/ordering seam rechecks returned **PASS** with no new blockers or regressions. The physics/audio/performance recheck found one documentation contradiction in the registry's timeout-policy entry; it is now corrected to integer fixed-tick authority, with `contact_event_time` and `flight_elapsed` diagnostic-only. Its separate runtime observations (static Physics queries and float/event-time winner participation) remain implementation debt outside this documentation-only scope and are not claimed as runtime evidence or approval blockers for the design text.

The fixture seam recheck then found one additional schema omission: `tolerance_class` was present in every `route_evidence` record but absent from the fixture-spec and registry field contracts. It is now declared in both authoritative field lists. The final independent fixture recheck returned **PASS** with no new blocker or regression, and the post-edit static preflight returned **PASS**.

All four seam dispositions are now clear for the final review: lifecycle/ownership **PASS**, identity/ordering **PASS**, physics/audio/performance **PASS for documentation authority** with runtime implementation debt deferred, and fixture/registry/acceptance **PASS**. No runtime/platform evidence or approval state is claimed.
