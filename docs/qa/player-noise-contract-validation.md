# Player Noise Contract Validation

**Date:** 2026-09-09  
**Stream:** B — Documentation and design-contract verification  
**Scope:** Canonical teaching contracts, identity/lifecycle vocabulary, acceptance schemas, and static documentation checks.  
**Status:** Documentation contract **PASS with evidence limitations**; Player Noise remains **In Review**.

## Scope and ownership

This note covers only the Stream B paths:

- `design/gdd/player-noise.md`
- `design/registry/entities.yaml`
- `design/fixtures/noise-fixture-spec.md`
- `design/levels/mvp-burst-route-fixture.md`
- `design/gdd/perception.md`
- `design/gdd/guard-ai-fsm.md`
- `design/gdd/player-third-person-controller.md`
- `design/gdd/player-movement-hide.md`

The audit did not modify `src/**`, runtime tests, `design/gdd/systems-index.md`, review logs, or approval/tracking records. Runtime, Unity, WebGL, audio/DSP, NavMesh, route-capture, profiling, and target-hardware evidence was not inferred or promoted.

## 1. Teaching-contract matrix

| Contract | Documentation result | Evidence | Runtime status |
|---|---|---|---|
| Crouch movement produces no movement fact | **PASS** | `player-noise.md:191-197` defines no `step_event` for Idle/Crouch/no displacement; `noise-fixture-spec.md:438-446` and `mvp-burst-route-fixture.md:601-615` require empty step events, no movement `NoisePublished`, and `expected_crouch_control_no_fact=true`. | `actual: UNCAPTURED` in `mvp-burst-route-fixture.md:618`; not a runtime pass. This is movement-channel-only: Burst thrown from crouch is a separate thrown-object channel and is explicitly allowed at full radius by `perception.md:451`. |
| Walk/Run teaching beats are raw-emission-only | **PASS, qualified** | `player-noise.md:284-288`, `noise-fixture-spec.md:438-446`, and `mvp-burst-route-fixture.md:619-651` bind the teaching beats to raw `NoisePublished` emission outside `R_eff`, with explicit no-response values and no relay/FSM response requirement. | `actual: UNCAPTURED` in the teaching records. This qualifier must not be generalized: in-range movement probes may relay and cause Investigate (`mvp-burst-route-fixture.md:1319-1337`; `perception.md:584`). |
| Explicit no-response values | **PASS** | `mvp-burst-route-fixture.md:613`, `631`, `649`, `667`, and `721` use distinct values for Crouch silence, raw-only Walk/Run, pickup silence, and route-outside-`R_eff`; `noise-fixture-spec.md:438-446` requires explicit no-response outcomes. | All teaching records remain `UNCAPTURED`; values are expected contracts, not captured results. |
| Certified Burst has visible orient then Investigate response | **PASS as normative contract; runtime certification unavailable** | `player-noise.md:284-290`; `mvp-burst-route-fixture.md:672-688` requires `orient_tell_toward_captured_landing_then_investigate_commit`; `mvp-burst-route-fixture.md:748-786` binds the causal chain `burst_landed → noise_heard_relay → investigate_commit → orient_tell`. | `actual: UNCAPTURED` for `teach_certified_throw_01` at `mvp-burst-route-fixture.md:690`; the manifest also records `runtime_capture: not_available` and `certification_status: UNCAPTURED` at `mvp-burst-route-fixture.md:40-43`, with the non-passing rule at `:61-82`. |

**Teaching conclusion:** The four documents agree when “raw-emission-only” is read as the teaching-beat contract outside `R_eff`, not as a global prohibition on movement hearing. Crouch silence is also scoped to the movement signature; the thrown Burst channel is independent.

## 2. Identity and lifecycle vocabulary

| Check | Result | Evidence |
|---|---|---|
| `fact_id` versus `entry_id` | **PASS** | `player-noise.md:30-39` assigns `fact_id` at accepted raw publication and shares it across hearing guards; `perception.md:38-41` assigns per-guard `entry_id`; `noise-fixture-spec.md:77-128` and `:232-245` require the same ownership split. FSM consumes identities and does not allocate or re-derive them (`guard-ai-fsm.md:25-56`, `:214-227`). |
| `open` / `promote` / `close` | **PASS** | `player-noise.md:78-134`, `perception.md:133-160`, `guard-ai-fsm.md:45-56`, and `noise-fixture-spec.md:232-245` agree that `open` starts an episode, `promote` preserves the live episode and `entry_id` for Investigate→Chase continuity, and `close` terminates it. The fixture requires exactly one close per open and no duplicate open/close. |
| Authoritative position and `position_source` | **PASS** | Position-source ownership is explicit across `player-noise.md:78-134`, `perception.md:133-160`, `guard-ai-fsm.md:45-56`, and registry lifecycle fields around `entities.yaml:1570-1619`: opener uses `opener_decision`, promote uses `episode_position`, normal close uses `terminal_decision`, and stale close uses `last_authoritative_episode_position`. Missing/non-finite positions fail closed. |
| Burst locomotion/stance admission | **PASS** | `player-noise.md:199-225` and `noise-fixture-spec.md:397-407` require stationary Idle/standing or successfully resolved Crouch-Idle; Walk/Run labels and positive resolved planar speed reject before allocation/spend. Rejections preserve `Carried`, allocate no flight/terminal identity, and publish no noise. The registry rejection codes and AC6b bind the same legs (`entities.yaml:1483-1484`; `player-noise.md:986`). |
| Surface contact versus published center | **PASS** | `player-noise.md:204-207` and `:498-590`, `noise-fixture-spec.md:182-203`, and `mvp-burst-route-fixture.md:1510-1680` separate authored physical surface contact from the pushed-out projectile center: `published_center = surface_contact + normal × (projectile_radius + epsilon_contact)`. `epsilon_contact` is not the contact ambiguity tolerance. |

## 3. Fail-closed acceptance and evidence-state checks

The acceptance schemas remain fail-closed:

- `noise-fixture-spec.md:397-407` rejects missing, malformed, duplicate, or stale identity records and requires immutable rejection outcomes.
- `noise-fixture-spec.md:530-576` and `:645-658` require expected/actual separation, provenance, capture state, tolerance, comparison result, and stable validator codes.
- `mvp-burst-route-fixture.md:2178-2205` requires complete route, geometry, trace, and evidence records; nominal-radius-only, ambiguous, contradictory, or uncaptured evidence fails closed.
- `player-noise.md:962-1000` keeps missing or `UNCAPTURED` actual/evidence fields non-passing, including identity/liveness, audio, route, and performance acceptance criteria.

The following evidence values were preserved as non-passing wherever used:

- `UNCAPTURED`
- `PENDING_OQ6`
- `pending`
- `estimated`
- `unsupported`
- `limiter_unsupported`
- `REQUIRED_RUNTIME_*` placeholders

No placeholder or expected value was converted into actual runtime evidence.

## 4. Static checks

| Check | Result | Exact result |
|---|---|---|
| Target-file existence and line inventory | **PASS** | All 8 target documents exist: `player-noise.md` 1010 lines; `entities.yaml` 1683; `noise-fixture-spec.md` 676; `mvp-burst-route-fixture.md` 2221; `perception.md` 672; `guard-ai-fsm.md` 688; `player-third-person-controller.md` 471; `player-movement-hide.md` 649. |
| Markdown heading/fence balance | **PASS** | All Markdown target files have balanced fence delimiters: Player Noise 24, fixture spec 2, route fixture 46, Perception 2, Guard FSM 24, controller 10, movement-hide 6. The YAML registry has no Markdown fences. |
| Stale contradiction scan | **PASS with intentional historical markers** | No active `N(state)`, `World/solid`, or `max_backlog_batches` remains. `S_DIFF`, `reanchor_speed_ratio`, and the former `peakSum` gain expression occur only in explicitly struck/deleted/tombstone or migration notes (for example `entities.yaml:54-58`, `guard-ai-fsm.md:73`, `player-noise.md:253`, `:863`), not as active authority. |
| `git diff --check` | **PASS** | Exit code 0; the current run emitted only five normal working-copy LF→CRLF warnings and no whitespace-error diagnostics. |
| Duplicate-key-aware YAML validation | **UNAVAILABLE** | Neither `yaml` nor `ruamel.yaml` is installed in the available Python environment. No duplicate-key pass is claimed. |

## 5. Findings and unresolved boundaries

1. **No unresolved documentation contradiction was found within the requested Stream B contract.** The only apparent Walk/Run conflict disappears when the teaching sequence’s explicit outside-`R_eff` qualifier is retained; in-range movement hearing is a separate receiver/Perception contract.
2. **Crouch wording must remain channel-scoped.** The route teaching case proves no movement fact, while a Burst thrown from Crouch-Idle can be admitted after same-tick stand resolution and emits as a thrown-object fact. The current documents state both contracts; this note records the distinction so future edits do not collapse them.
3. **Runtime certification remains open.** All route teaching and certified Burst actuals are `UNCAPTURED`; this Stream B result is documentation readiness only and is not a Player Noise approval.
4. **YAML duplicate-key validation remains open.** A parser with duplicate-key detection is still required before claiming that validation leg.

## Handoff

- Documentation QA note created: `docs/qa/player-noise-contract-validation.md`.
- Canonical files in this worktree reflect the pre-existing current documentation revision copied from the user’s working tree; the audit did not change their contract content.
- No runtime/platform evidence, approval status, systems-index row, review log, commit, or push was changed.
- Stream B exit status: **PASS with unavailable runtime/YAML evidence explicitly recorded**.
