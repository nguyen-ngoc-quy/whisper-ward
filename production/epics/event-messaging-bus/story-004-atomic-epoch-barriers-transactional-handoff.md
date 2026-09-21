# Story 004: Atomic Epoch Barriers & Transactional Perception Handoff

> **Epic**: Event / Messaging Bus  
> **Status**: Ready  
> **Layer**: Foundation  
> **Type**: Integration  
> **Estimate**: 4 hours  
> **Manifest Version**: 2026-09-21  
> **Last Updated**: 2026-09-21  

## Context

**GDD**: `design/gdd/event-messaging-bus.md`  
**Requirement**: `TR-FOUND-004`  
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: `ADR-0001: Deterministic Event/Messaging Bus for Gameplay Contracts`  
**ADR Decision Summary**: Atomic `BeginEpoch` / `BeginSession` barriers, stale event purging, and two-phase transactional perception handoff (`AcceptNoise`).

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  
**Engine Notes**: Barrier flushes must be atomic on Unity main thread; zero leftover state across respawn/scene reload.

**Control Manifest Rules (this layer)**:
- Required: **Atomic Attempt Epoch Invalidation** — Checkpoint reload, capture, respawn, or room restart must execute `BeginEpoch` or `BeginSession` barriers, atomically purging queued envelopes, invalidating stale identity state, and tagging leftover items with `event-bus-stale-epoch` or `event-bus-stale-session`. — source: `ADR-0001`
- Required: **Two-Phase Transactional Perception Handoff** — `NoisePublished` dispatch must invoke the typed contract `Perception.AcceptNoise(envelope) -> Accepted | Duplicate | Retry | Rejected(code)`. Envelopes stay in `handoff_pending` during retry and remove only when all active listeners acknowledge or terminals serialize. — source: `ADR-0001`
- Forbidden: **Never Dispatch Events Across Session/Epoch Barriers** — Older queued events must never cross session or epoch barriers; unevaluated work must be explicitly serialized and rejected, never silently dropped or executed late. — source: `ADR-0001`

---

## Acceptance Criteria

*From GDD `design/gdd/event-messaging-bus.md`, scoped to this story:*

- [ ] **AC7 (Epoch Barrier Purge)**: Calling `BeginEpoch(sessionId, newEpoch)` immediately purges all queued envelopes with `AttemptEpoch < newEpoch`, marks them `event-bus-stale-epoch`, and resets the dedup table. Stale envelopes never invoke listener callbacks in the new epoch.
- [ ] **AC8 (Session Barrier Reset)**: Calling `BeginSession(newSessionId, initialEpoch)` resets all internal queues, dedup keys, publisher sequences, and active subscriptions cleanly (`event-bus-stale-session`).
- [ ] **AC11 (Transactional Handoff & Retry)**: When a downstream perception listener returns `HandoffResult.Retry`, the envelope remains in `HandoffPending` in the queue and is retried on subsequent ticks without re-invoking listeners that already returned `Accepted`.
- [ ] **AC12 (Handoff Retry Exhaustion)**: If a listener repeatedly returns `Retry` past $N_{\text{retry\_max}} = 3$ attempts, the envelope terminates with `event-bus-downstream-handoff-retry-exhausted`, logs a diagnostic failure, and is removed from the queue.

---

## Implementation Notes

*Derived from ADR-0001 Implementation Guidelines:*

1. Implement `BeginEpoch(SessionId session, AttemptEpoch newEpoch)` and `BeginSession(SessionId newSession, AttemptEpoch initialEpoch)`.
2. Define `IHandoffConsumer` with method `HandoffResult AcceptNoise(in EventEnvelope envelope)`.
3. Support per-listener ack tracking within envelopes: `Accepted`, `Duplicate`, `Retry`, `Rejected(code)`.
4. Stale envelopes purged during barrier triggers must emit structured telemetry/diagnostic events.

---

## Out of Scope

- [Story 001]: Event envelope memory layout and basic subscriptions.
- [Story 002]: Ingress deduplication table.
- [Story 003]: Virtual clock ordering.

---

## QA Test Cases

*Defined by QA Lead for automated testing:*

- **AC-7 (Epoch Flush)**:
  - Given: 10 envelopes from Epoch 1 are waiting in the pending queue.
  - When: `BeginEpoch(session, 2)` is called.
  - Then: Assert pending queue count drops to 0, all 10 are tagged stale, and no listener receives callbacks in Epoch 2.

- **AC-8 (Session Reset)**:
  - Given: Active session has 5 registered listeners and 3 pending envelopes.
  - When: `BeginSession(newSession, 0)` is called.
  - Then: Assert all queues, sequence counters, and dedup tables are completely reset.

- **AC-11 (Handoff Pending Retention)**:
  - Given: Listener 1 accepts an envelope; Listener 2 returns `Retry`.
  - When: Tick 1 completes.
  - Then: Assert envelope is retained in `HandoffPending` and only Listener 2 is invoked on Tick 2.

- **AC-12 (Handoff Retry Exhaustion)**:
  - Given: Listener 2 returns `Retry` across 3 successive ticks.
  - When: Tick 4 runs.
  - Then: Assert envelope terminates with `event-bus-downstream-handoff-retry-exhausted` and is purged.

---

## Test Evidence

**Story Type**: Integration  
**Required evidence**: `tests/EditMode/Foundation/EventBusEpochHandoffTests.cs` — must exist and pass  
**Status**: [ ] Not yet created  

---

## Dependencies

- Depends on: Story 001, Story 002, Story 003
- Unlocks: Event Bus Epic completion (`/story-done`)
