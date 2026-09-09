# ADR-0001: Deterministic Event/Messaging Bus for Gameplay Contracts

## Status
Proposed

## Date
2026-08-29

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6 LTS (6000.3.17f1) |
| **Domain** | Core / Scripting |
| **Knowledge Risk** | LOW |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `.claude/docs/technical-preferences.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | Unity Test Framework coverage for ordering, deduplication, lifecycle, epoch invalidation, and virtual-clock phase delivery |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | Player Noise, Perception, Guard AI FSM, Player Movement & Hide integration contracts |
| **Blocks** | Production implementation of cross-system sensing and decision delivery until this contract is accepted and tested |
| **Ordering Note** | The bus contract must be accepted before implementing the Player Noise `NoisePublished` delivery path. It does not change the active Player Noise design review. |

## Context

### Problem Statement

Whisper Ward's approved and in-review gameplay documents use the Event Bus as the
boundary between Player Noise, Perception, Guard AI FSM, and HideSpot. The former
implementation in `src/AI/Core/EventBus.cs` was a minimal static pub/sub helper:
its `Unsubscribe` method was not implemented, delivery was immediate, and it had
no contract for `fact_id` deduplication, `attempt_epoch` invalidation, retry
handling, or deterministic ordering. The current source contains a provisional
`SessionEventBus` adapter for those seams, but the ADR remains Proposed and its
transactional Perception handoff is not yet implemented or tested.

### Constraints

- Unity 6 LTS, C#, PC and WebGL targets.
- Gameplay timing is governed by the shared injected virtual clock, not render-frame
  duration or wall-clock time.
- Player Noise, Perception, and Guard AI FSM must remain event-driven; the FSM must
  not synchronously query Perception.
- The bus must preserve the observable contracts already declared by the GDDs.
- The MVP must remain simple enough for a one-room, one-guard scenario.

### Requirements

- Carry `session_id`, `attempt_epoch`, `timestamp`, and `publisher` on every event
  envelope.
- Deliver each accepted sensing fact exactly once to each active eligible listener.
- Deduplicate a raw sensing fact by `(session_id, attempt_epoch, fact_id)`.
- Deduplicate typed HideSpot occupancy transitions by
  `(session_id, attempt_epoch, hide_spot_id, transition_id)` while keeping occupied and
  empty as separate event types.
- Preserve per-publisher order; for equal source timestamps, preserve the publisher's
  monotonic fact/transition order.
- Atomically invalidate older queued facts and occupancy transitions at an epoch
  transition.
- Support reliable subscription and unsubscription across Unity component lifecycles.
- Avoid allocating or reassigning `fact_id` or Perception-owned `entry_id` in the bus.
- Keep dispatch deterministic and compatible with WebGL's main-thread model.

## Decision

Use a deterministic, phase-driven Event Bus with an explicit immutable envelope and
session/epoch-aware ingress deduplication. The bus is a runtime service whose lifetime
is controlled by the gameplay session, rather than an unbounded process-global event
registry.

> **Cross-document authority note (lifecycle / attempt_epoch).** The canonical lifecycle
> transition table — covering death, capture, respawn, segment reset, full-room restart,
> scene reload, new playable attempt, pause/resume, and pool/unpool — is authoritative in
> `design/registry/entities.yaml` and reproduced verbatim in `player-noise.md §Ownership`.
> The bus's `BeginEpoch` / `BeginSession` barriers consume this table; no file owns a
> distinct version. ADR status remains **Proposed**; this note is additive only.

**Implementation status (not acceptance):** `SessionEventBus` currently supplies a
bounded queued service, typed token subscriptions, phase-tagged events, ingress and
listener deduplication, and explicit session/epoch clearing. It does not yet provide
the transactional `Perception.AcceptNoise(envelope)` handoff or retained
`handoff_pending` obligations specified below, and no Unity Test Framework execution
has verified the adapter. The static `EventBus` facade is compatibility-only and is
not an additional authoritative runtime path.

Publishers submit immutable events to the bus ingress. The bus validates the envelope,
records publisher order, and queues accepted events for dispatch at the owning
virtual-clock phase. Dispatch occurs on Unity's main thread. The bus does not create
gameplay facts, allocate `fact_id`, allocate `entry_id`, or reinterpret system-owned
payloads.

### Identity, deduplication, and ordering semantics

The envelope identity is immutable and owner-defined. The bus never synthesizes a
replacement identity when a publisher retries, and an identity is never reused within
its `(session_id, attempt_epoch)` generation. The mandatory identity keys are:

- `NoisePublished`: `(session_id, attempt_epoch, fact_id)`; `fact_id` remains emitter-owned. The bus retains the Player Noise source metadata without redefining hearing time: Movement uses `t_publish = source_timestamp`, while Burst uses `t_publish = terminal_publication_time`; Burst `source_timestamp` remains allocation/order metadata only.
- `HideSpot occupied` and `HideSpot empty`: `(session_id, attempt_epoch, hide_spot_id,
  transition_id)` within the `HideSpot` namespace. The two event types remain separate
  typed events, and `occupied` must agree with the event type.

`transition_id` is an opaque, HideSpot-owned monotonic identity allocated exactly once
when the player capsule causes an actual Empty ↔ Occupied transition. Duplicate trigger
callbacks do not allocate another transition; a retry carries the same immutable
transition id. The raw occupancy event records the current occupancy state only and never
carries, allocates, or grants a Perception-owned `entry_id` or
`witnessed_entry_authority`. Perception derives those values from the raw occupancy
stream plus its own sensing evidence.

Ingress deduplication is performed against `(session_id, attempt_epoch, owner namespace,
identity)`. An event already `admitted`, `handoff_pending`, or `delivered` is reported as a
duplicate and is not enqueued again. A retry after a session or epoch barrier is stale,
not a new identity. Deduplication applies before listener delivery, so every active
eligible listener sees at most one delivery for one accepted identity.

The bus records an internal monotonic publisher sequence when an envelope is admitted;
listener arrival order, thread arrival order, subscription order, and incidental queue
insertion order are never identity or ordering sources. Events from one publisher retain
that publisher sequence, including equal timestamps. Across independent publishers, the
virtual-clock phase fence is authoritative first; equal-phase ties use the registered
source-allocation, fact-admission, and pair-dispatch tuples owned by the consuming gameplay
contract. Publisher sequence, listener order, subscription order, and queue insertion order
must never replace those tuples. HideSpot's transition sequence remains the authoritative
order for that publisher. Consequently, a `HideSpot empty` transition flushed in the HideSpot
phase is visible to Perception before the FSM phase can evaluate capture, while a same-tick
re-entry remains a later typed transition and cannot inherit the earlier transition id.

### Queue, retry, and barrier contract

The Event Bus owns one bounded pending-envelope queue, with capacity supplied by the
registry as `event_bus_pending_envelope_capacity`. An envelope remains in that queue
with state `admitted` or `handoff_pending` until its active-listener delivery
obligations are complete. Perception owns its separate bounded hearing queue after a
transactional handoff; the Event Bus does not count or silently replace records in
that downstream queue.

Ingress is ordered as follows:

1. Validate session, epoch, publisher, timestamp, payload, and owner-defined identity.
2. If the identity is already `admitted`, `handoff_pending`, or `delivered` for the
   active `(session_id, attempt_epoch)`, report `event-bus-duplicate-identity` and do
   not create a second envelope. A pending identity continues its existing handoff.
3. If the pending-envelope queue has no capacity, apply deterministic backpressure
   and retry up to the registry's `event_bus_retry_attempts`. A retry reuses the
   immutable envelope and does not advance publisher order or mark the identity as
   admitted.
4. If capacity remains unavailable, reject with
   `event-bus-queue-overflow-rejected` and serialize session, epoch, publisher,
   identity, retry count, and queue depth. No unevaluated work is silently dropped.
5. Only an envelope admitted to the pending queue is recorded in the ingress
   identity table and publisher-order stream.

A phase drain takes one active typed-listener snapshot and dispatches admitted
envelopes in deterministic queue order. The snapshot is retained while a delivery
is pending, so a downstream retry cannot accidentally select a different recipient
set. For every `NoisePublished` listener, the bus calls the typed handoff contract:

```text
Perception.AcceptNoise(envelope) -> Accepted | Duplicate | Retry | Rejected(code)
```

`Accepted` means Perception atomically admitted the immutable fact to its raw-fact
queue; `Duplicate` means that exact `(session_id, attempt_epoch, fact_id)` is already
admitted there. Both complete that listener's obligation exactly once. `Retry` keeps
the envelope in `handoff_pending`, reuses the same identity, and consumes the
registered retry budget. `Rejected(code)` completes with explicit failure, records
`event-bus-downstream-handoff-retry-exhausted` (or the returned stable code), and
never reports a successful relay. The envelope is removed only when every active
snapshot listener has acknowledged `Accepted`/`Duplicate`, or when a terminal
rejection has been serialized. An unsubscribed snapshot listener is marked inactive
and removed from the obligation set; newly subscribed listeners join only future
snapshots.

This transaction resolves the Bus/Perception capacity boundary: the Bus does not
mark a fact delivered merely because ingress succeeded, and Perception does not need
to accept a fact that its queue cannot hold. Invalid envelopes use
`event-bus-invalid-envelope`; duplicate identities use `event-bus-duplicate-identity`.

A full-room restart uses an atomic session barrier: stop ingress, invalidate the old
generation, purge pending envelopes and ingress identity state, record stale queued
envelopes as `event-bus-stale-session`, then enable the new `session_id` and initial
epoch. `BeginEpoch` performs the same barrier for a new epoch within a live session
and records older queued envelopes as `event-bus-stale-epoch`. Neither stale nor
rejected envelopes reach a later FSM phase. A retry after a barrier is accepted only
if its session and epoch match the active generation.

### Architecture Diagram

```text
Player Controller / NoiseEmitter / Perception / FSM
                 │
                 ▼
        EventBus ingress envelope
        - session_id
        - attempt_epoch
        - timestamp
        - publisher
        - owner-defined identity
                 │
       validate + deduplicate + queue
                 │
        virtual-clock phase drain
                 │
                 ▼
       active typed subscribers
                 │
       Perception relays / FSM decisions
```

### Key Interfaces

The concrete API may be adapted to the project's Unity composition pattern, but the
following observable contract is mandatory:

```text
EventEnvelope
  session_id: SessionId
  attempt_epoch: AttemptEpoch
  timestamp: VirtualTimestamp
  publisher: PublisherId
  phase: VirtualClockPhase
  payload: IEvent
  identity: owner-defined immutable identity

EventBus.Publish(envelope)
  admits once per owner-defined identity and epoch

Perception.AcceptNoise(envelope)
  -> Accepted | Duplicate | Retry | Rejected(stable_code)
  atomically admits the raw fact or asks the Bus to retain handoff_pending

EventBus.Subscribe<T>(handler) -> SubscriptionToken
EventBus.Unsubscribe(token)
EventBus.BeginSession(session_id, attempt_epoch)
EventBus.BeginEpoch(session_id, attempt_epoch)
EventBus.Drain(virtual_clock_phase)
```

For `NoisePublished`, the owner-defined identity is the emitter-allocated
`fact_id`; the deduplication key is `(session_id, attempt_epoch, fact_id)`. The bus
must preserve the same `fact_id` for every Perception listener. `entry_id` remains
Perception-owned and is never generated by the bus.

For `HideSpot occupied` and `HideSpot empty`, the owner-defined identity is the
HideSpot-allocated immutable `transition_id`; the deduplication key is
`(session_id, attempt_epoch, hide_spot_id, transition_id)` in the HideSpot namespace.
The typed event names remain distinct, and `occupied` must agree with the event type.
HideSpot allocates one new transition identity only when capsule containment causes an
actual Empty ↔ Occupied transition. Duplicate trigger callbacks and delivery retries
reuse the same identity; they cannot create a second raw occupancy transition or a
second Perception state update. A same-tick exit is flushed as `HideSpot empty` before
the FSM capture phase, and a later re-entry receives a new transition identity.

Raw HideSpot occupancy carries the current occupancy state and the authored
`interior_position` only. It does not carry, allocate, or grant `entry_id` or
`witnessed_entry_authority`; Perception derives those values from the raw occupancy
stream and its own sensing/liveness evidence. Session and epoch barriers invalidate
stale transition envelopes exactly as they invalidate stale sensing facts.

For events that do not carry `fact_id`, the owning system must define an immutable
identity before publication. The bus must reject or report an event with no valid
identity when exactly-once delivery is required; it must not invent a substitute ID.
`LivenessFact` uses owner namespace `GuardAISystem` and the immutable key
`(session_id, attempt_epoch, guard_eid, entry_id, op, source_timestamp)`. The
session/lifecycle owner initiates one `StaleLivenessClose` handoff for each live
prior-epoch key before queue invalidation; `GuardAISystem` is the delegated publisher
of the single `op=close,cause=stale` fact. The epoch barrier does not commit until
those handoffs are retained or a fail-closed barrier record is written. Duplicate
handoffs are deduplicated before retry, and a late callback is stale rather than a
new identity.

The bus may transport `LivenessFact` on a dedicated topic or the registered trace
transport, but that implementation choice cannot change the observable liveness
contract: the FSM is the sole writer, the source timestamp and `(guard_eid, entry_id)`
identity are retained, `op=open|promote|close` remains typed, and the latest fact
published before a Perception hearing drain is the snapshot visible at that drain.
Publisher sequence, subscription order, listener order, and queue insertion order
never replace the registered source-allocation, fact-admission, or pair-dispatch keys.

At `BeginEpoch`, queued events from older epochs are invalidated before the next FSM
phase. Events already delivered remain part of the prior epoch's trace and are not
replayed.

Subscription callbacks are snapshot-dispatched. A callback may subscribe or
unsubscribe without modifying the collection currently being drained. Destroyed or
disabled Unity components must unsubscribe during their lifecycle teardown.

## Alternatives Considered

### Alternative 1: Keep the static immediate pub/sub bus

- **Description**: Retain the current static dictionary and add metadata checks to
  `Publish`.
- **Pros**: Smallest code change; easy to call from existing code.
- **Cons**: Global lifetime, difficult reset semantics, no natural virtual-clock
  phase boundary, and the current unsubscribe design leaks handlers.
- **Rejection Reason**: Cannot reliably satisfy lifecycle, epoch, and deterministic
  delivery requirements without recreating a service inside the static helper.

### Alternative 2: UnityEvent or ScriptableObject channel assets

- **Description**: Represent each event topic as a Unity asset or UnityEvent channel.
- **Pros**: Inspector-friendly and convenient for authoring.
- **Cons**: Runtime deduplication, session scoping, epoch invalidation, and exact
  virtual-clock ordering would still require a separate coordination layer.
- **Rejection Reason**: Authoring convenience does not solve the authoritative
  gameplay delivery contract and adds asset lifecycle complexity for the MVP.

### Alternative 3: Deterministic session-scoped bus with explicit queue and envelope

- **Description**: Use a typed runtime service with immutable envelopes, ingress
  deduplication, publisher-order tracking, lifecycle tokens, and virtual-clock phase
  drains.
- **Pros**: Directly expresses the GDD contract, supports reset boundaries, is
  testable without a running render loop, and works on WebGL's main thread.
- **Cons**: Requires replacing the current minimal helper and adding explicit test
  seams and lifecycle handling.
- **Rejection Reason**: Not rejected; this is the selected approach.

## Consequences

### Positive

- Duplicate raw facts and retry deliveries become observable and rejectable.
- Death, respawn, and segment reset can invalidate stale queued facts deterministically.
- Perception and FSM remain decoupled through explicit event contracts.
- Tests can drive delivery using the shared virtual clock without relying on frame
  timing or wall-clock sleeps.
- The design remains compatible with PC and WebGL main-thread execution.

### Negative

- Publishers must provide valid session/epoch and owner-defined identity fields.
- Queue and subscription lifecycle require more code than the current helper.
- The service must be reset or disposed between gameplay sessions to avoid stale
  subscribers and retained event history.

### Risks

- **Incorrect global ordering**: keep the required guarantee limited to per-publisher
  order and define deterministic tie-breaking in the implementation tests.
- **Lifecycle leaks**: require token-based unsubscribe and lifecycle tests that create,
  disable, re-enable, and destroy subscribers.
- **Hidden synchronous assumptions**: prohibit direct FSM queries from callbacks and
  test that phase delivery is the only authoritative consumption boundary.
- **Unbounded queue growth**: expose queue depth diagnostics and add a bounded,
  measured policy for backlog handling in the implementation/performance gate.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| `player-noise.md` | Raw `NoisePublished` is delivered exactly once to each active Perception listener and deduplicated by `(session_id, attempt_epoch, fact_id)` | Defines immutable envelope, ingress deduplication, and per-listener exactly-once delivery |
| `player-noise.md` | Epoch transitions discard stale queued facts; `fact_id` is emitter-owned | Defines `BeginEpoch` invalidation and explicitly forbids bus ID allocation |
| `perception.md` | Perception relays retain the emitter `fact_id` while owning `entry_id` | Preserves the two-ID ownership boundary |
| `guard-ai-fsm.md` | FSM consumes event facts and publishes decisions without synchronous Perception queries | Defines phase-driven typed subscription and event-only integration |
| `player-movement-hide.md` | HideSpot occupancy remains two typed events with HideSpot-owned immutable `transition_id`, deterministic deduplication/order, and no raw `entry_id` or witnessed authority | Preserves the occupancy-state ownership boundary while making retry, same-tick exit, and epoch invalidation explicit |

## Performance Implications

- **CPU**: One ingress validation/deduplication check per event plus subscriber
  dispatch. Measure queue drain and callback cost in the existing AC21 performance
  harness.
- **Memory**: Bounded queued envelopes and subscription records; no unbounded event
  history is required for runtime correctness.
- **Load Time**: No material load-time cost; session service construction is expected
  during gameplay initialization.
- **Network**: None; this ADR is for local gameplay delivery only.

## Migration Plan

1. Introduce the envelope and subscription-token abstractions behind the existing
   Event Bus call sites.
2. Implement real unsubscribe and session reset behavior.
3. Add sensing-fact deduplication and epoch invalidation.
4. Move Perception and FSM delivery to the virtual-clock phase drain.
5. Add deterministic tests before wiring the Player Noise runtime publisher.
6. Remove the old static callback-wrapper behavior after all consumers migrate.

No migration step authorizes changes to the active Player Noise design document or
its review records.

## Validation Criteria

- Two active Perception listeners receive one `NoisePublished` each for one valid
  `fact_id`.
- Replaying the same fact or retrying the same source event produces no duplicate
  delivery.
- Replaying a `HideSpot occupied` or `HideSpot empty` envelope with the same
  `(session_id, attempt_epoch, hide_spot_id, transition_id)` produces no duplicate
  transition or Perception state update; occupied and empty remain separate typed
  events.
- Equal-timestamp facts from one publisher arrive in monotonic publisher order.
- HideSpot transition order is stable under equal timestamps and queue/subscriber
  timing; a same-tick `HideSpot empty` is visible before the FSM capture phase and a
  fresh re-entry cannot reuse the prior transition identity.
- Raw HideSpot events contain no `entry_id` or `witnessed_entry_authority`; those are
  derived by Perception from its own sensing/liveness evidence.
- A new `attempt_epoch` drops older queued facts before the next FSM phase.
- Disable/destroy/re-enable lifecycle sequences do not duplicate callbacks or retain
  stale handlers.
- No bus-generated `fact_id` or `entry_id` appears in any event payload.
- Virtual-clock pause/resume does not advance delivery or alter event order.
- A full Event Bus queue retries deterministically before rejection; a rejected envelope
  has the stable overflow diagnostic and serialized identity/queue accounting.
- A retry that eventually succeeds is deduplicated exactly once; a retry after a
  session/epoch barrier is recorded stale and never reaches a new phase.
- `BeginSession` atomically purges the old generation, while `BeginEpoch` invalidates
  only older queued epochs in the same session.
- The Event Bus pending queue and Perception hearing queue expose separate depths,
  capacities, and overflow counters.
- Queue depth and dispatch cost remain within the implementation performance budget.

## Related Decisions

- `design/gdd/player-noise.md` — Player Noise (`NoiseEmitter`) contract
- `design/gdd/perception.md` — Perception sensing and hearing relay contract
- `design/gdd/guard-ai-fsm.md` — FSM consumption and decision contract
- `design/gdd/player-movement-hide.md` — HideSpot event dependency
- `src/AI/Core/EventBus.cs` — current implementation being superseded
