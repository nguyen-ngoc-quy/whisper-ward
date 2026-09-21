# Story 001: Immutable Event Envelope & Type-Safe Subscription Lifecycle

> **Epic**: Event / Messaging Bus  
> **Status**: Ready  
> **Layer**: Foundation  
> **Type**: Logic  
> **Estimate**: 3 hours  
> **Manifest Version**: 2026-09-21  
> **Last Updated**: 2026-09-21  

## Context

**GDD**: `design/gdd/event-messaging-bus.md`  
**Requirement**: `TR-FOUND-001`  
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: `ADR-0001: Deterministic Event/Messaging Bus for Gameplay Contracts`  
**ADR Decision Summary**: Readonly struct event envelopes, type-safe tokenized subscriptions, active listener snapshot isolation, exception containment, and zero heap allocation on dispatch hot paths.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  
**Engine Notes**: Pure C# struct memory layout, non-allocating arrays for listeners, no runtime reflection in dispatch loops.

**Control Manifest Rules (this layer)**:
- Required: **Immutable Struct Event Envelopes** — All event envelopes must be readonly C# structs containing `session_id`, `attempt_epoch`, `timestamp`, and `publisher`. No reference-type envelope allocations on the event bus. — source: `ADR-0001`
- Forbidden: **Never Synthesize or Reassign fact_id / entry_id in the Bus** — The Event Bus must never allocate, synthesize, or reinterpret `fact_id` (owned by Emitter) or `entry_id` (owned by Perception). — source: `ADR-0001`
- Guardrail: **Event Bus Dispatch Budget** — Maximum 0.50 ms per frame budget for event draining. Dispatch operates on Unity's main thread with 0 heap allocation on hot paths. — source: `ADR-0001`

---

## Acceptance Criteria

*From GDD `design/gdd/event-messaging-bus.md`, scoped to this story:*

- [ ] **AC1 (Envelope Immutability)**: `EventEnvelope` is declared as `readonly struct`. All fields (`SessionId`, `AttemptEpoch`, `Timestamp`, `Publisher`, `Phase`, `OwnerNamespace`, `EventIdentity`, `Payload`) are initialized upon construction and cannot be mutated.
- [ ] **AC9 (Token-based Subscription)**: Subscribing to an event via `Subscribe<T>(handler)` returns a unique, comparable `SubscriptionToken`. Calling `Unsubscribe(token)` detaches the handler so subsequent dispatches never trigger it.
- [ ] **AC10 (Snapshot Isolation)**: If a subscriber calls `Subscribe` or `Unsubscribe` during an active dispatch loop, the current loop executes across the pre-captured snapshot without throwing `InvalidOperationException: Collection was modified`. Changes take effect on the next cycle.
- [ ] **AC13 (Exception Containment)**: If a subscriber's callback throws an exception, the bus catches and logs it with envelope metadata, and continues dispatching to remaining listeners in the snapshot without aborting the loop.
- [ ] **AC14 (Zero Allocation)**: Dispatching and handling events generates exactly 0 B heap allocation (`GC.Alloc`) per cycle.

---

## Implementation Notes

*Derived from ADR-0001 Implementation Guidelines:*

1. Define `EventEnvelope` in `WhisperWard.Foundation.EventBus.Contracts` as a `readonly struct`.
2. Define `SubscriptionToken` as a `readonly struct` implementing `IEquatable<SubscriptionToken>`.
3. Pre-allocate static listener arrays or reusable invocation buffers sized to `max_active_subscribers_per_event = 32` to avoid runtime resizing.
4. Active listener dispatch must copy the registered count into a stackalloc/fixed buffer or swap index to guarantee snapshot isolation without heap allocations.
5. Wrap listener callback invocation in a `try-catch` block that emits structured error logs containing `(SessionId, AttemptEpoch, Timestamp, EventType, ListenerTarget)`.

---

## Out of Scope

- [Story 002]: Ingress deduplication against `DedupKey` and queue overflow policies.
- [Story 003]: 5-phase sequential draining and lexicographical ordering.
- [Story 004]: Epoch barrier flushes and transactional `AcceptNoise` handoffs.

---

## QA Test Cases

*Defined by QA Lead for automated testing:*

- **AC-1 (Envelope Immutability)**:
  - Given: A publisher creates an `EventEnvelope` with specific header and payload values.
  - When: The envelope is published and received by a subscriber.
  - Then: Assert all fields match bit-for-bit with original publisher inputs.
  - Edge cases: Verify struct fields are strictly `readonly` at compile time.

- **AC-9 (Token Unsubscribe)**:
  - Given: Subscriber A registers with `Subscribe<T>()` and receives Token A.
  - When: Subscriber A calls `Unsubscribe(Token A)` before an event of type `T` is published.
  - Then: Assert Subscriber A receives 0 invocations.
  - Edge cases: Unsubscribing an already-unsubscribed or default token is a safe no-op.

- **AC-10 (Snapshot Re-entrancy)**:
  - Given: Subscriber A unregisters itself and registers Subscriber B inside its own callback.
  - When: The bus dispatches to the active listeners.
  - Then: Assert no `InvalidOperationException` is thrown; Subscriber A runs for this cycle, and Subscriber B only runs in the subsequent cycle.

- **AC-13 (Exception Containment)**:
  - Given: Subscriber 1 throws a `NullReferenceException`, while Subscriber 2 is valid.
  - When: An event is dispatched to both subscribers.
  - Then: Assert Subscriber 2 receives the event successfully and an error is logged.

- **AC-14 (Zero GC Allocation)**:
  - Given: 1000 events published and dispatched across 10 registered subscribers.
  - When: Memory allocations are tracked.
  - Then: Assert GC allocation equals exactly 0 bytes.

---

## Test Evidence

**Story Type**: Logic  
**Required evidence**: `tests/EditMode/Foundation/EventEnvelopeSubscriptionTests.cs` — must exist and pass  
**Status**: [ ] Not yet created  

---

## Dependencies

- Depends on: None (Foundational contract)
- Unlocks: Story 002 (Ingress Deduplication)
