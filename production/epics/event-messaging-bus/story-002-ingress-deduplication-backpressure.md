# Story 002: Ingress Deduplication & Bounded Queue Backpressure

> **Epic**: Event / Messaging Bus  
> **Status**: Ready  
> **Layer**: Foundation  
> **Type**: Logic  
> **Estimate**: 3 hours  
> **Manifest Version**: 2026-09-21  
> **Last Updated**: 2026-09-21  

## Context

**GDD**: `design/gdd/event-messaging-bus.md`  
**Requirement**: `TR-FOUND-003`  
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: `ADR-0001: Deterministic Event/Messaging Bus for Gameplay Contracts`  
**ADR Decision Summary**: 4-tuple `DedupKey` table, bounded queue of 256/4096 capacity, 3-attempt backpressure retry, and strict `reject-newest` overflow policy with zero silent drops.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  
**Engine Notes**: Pre-allocated fixed ring buffer, no dynamic heap allocations on push/pop.

**Control Manifest Rules (this layer)**:
- Required: **Owner-Defined Identity & Ingress Deduplication** — Every event must carry an owner-defined immutable identity (`(session_id, attempt_epoch, fact_id)` for `NoisePublished`; `(session_id, attempt_epoch, hide_spot_id, transition_id)` for `HideSpot occupied/empty`). Bus performs deduplication against `(session_id, attempt_epoch, owner namespace, identity)` before listener dispatch. — source: `ADR-0001`
- Forbidden: **Never Synthesize or Reassign fact_id / entry_id in the Bus** — The Event Bus must never allocate, synthesize, or reinterpret `fact_id` (owned by Emitter) or `entry_id` (owned by Perception). — source: `ADR-0001`
- Guardrail: **Event Bus Ring Buffer Capacity** — Ingress pending-envelope ring buffer fixed capacity of 4096 entries (`event_bus_pending_envelope_capacity`). Overflow must reject with `event-bus-queue-overflow-rejected`. — source: `ADR-0001`

---

## Acceptance Criteria

*From GDD `design/gdd/event-messaging-bus.md`, scoped to this story:*

- [ ] **AC2 (Ingress Deduplication)**: When an envelope with duplicate `DedupKey = (SessionId, AttemptEpoch, OwnerNamespace, EventIdentity)` is submitted within the same epoch, the bus rejects it with `event-bus-duplicate-identity` and does not enqueue or dispatch it a second time.
- [ ] **AC5 (Backpressure Retry)**: When the pending queue is at capacity ($|\mathcal{Q}_{\text{pending}}| = 256$), incoming unique envelopes enter `BACKPRESSURE_RETRY` for up to $N_{\text{retry\_max}} = 3$ sub-ticks without exceeding queue bounds.
- [ ] **AC6 (Reject-Newest Overflow Policy)**: If the queue remains full after 3 retries ($r \ge 3$), the newest envelope is rejected with diagnostic code `event-bus-queue-overflow-rejected`. All existing envelopes in the queue are preserved 100% (zero silent drops).

---

## Implementation Notes

*Derived from ADR-0001 Implementation Guidelines:*

1. Maintain an epoch-scoped lookup table `HashSet<DedupKey>` with pre-allocated capacity `ingress_dedup_capacity = 4096`.
2. Implement fixed-capacity ring buffer for pending envelopes with `event_bus_pending_envelope_capacity = 256` (scalable to 4096).
3. If an incoming envelope key exists in the dedup table, return `PublishResult.DuplicateRejected` immediately.
4. If the queue is saturated, track retry count per envelope up to `event_bus_retry_attempts = 3`.
5. On the 4th attempt, reject with `PublishResult.OverflowRejected` and log full diagnostic envelope metadata.

---

## Out of Scope

- [Story 001]: EventEnvelope definition and subscription token lifecycle.
- [Story 003]: 5-phase virtual clock ordering and dispatch execution.
- [Story 004]: Epoch/session reset barriers and downstream AI handoffs.

---

## QA Test Cases

*Defined by QA Lead for automated testing:*

- **AC-2 (Duplicate Ingress Rejection)**:
  - Given: Envelope E with key `(1, 0, "Noise", 101)` has been admitted.
  - When: Another envelope with identical key `(1, 0, "Noise", 101)` is published in epoch 0.
  - Then: Assert `Publish` returns `event-bus-duplicate-identity` and queue depth remains unchanged.

- **AC-5 (Queue Capacity & Backpressure)**:
  - Given: Pending queue is loaded to exactly 256 envelopes.
  - When: A new unique envelope E_257 is published with retry count 0.
  - Then: Assert E_257 is flagged for backpressure retry and queue depth does not exceed 256.

- **AC-6 (Reject-Newest on Saturation)**:
  - Given: Pending queue remains full and E_257 reaches retry count 3.
  - When: A 4th publish attempt occurs.
  - Then: Assert E_257 is rejected with `event-bus-queue-overflow-rejected`, while original 256 items remain intact.

---

## Test Evidence

**Story Type**: Logic  
**Required evidence**: `tests/EditMode/Foundation/EventBusIngressDedupTests.cs` — must exist and pass  
**Status**: [ ] Not yet created  

---

## Dependencies

- Depends on: Story 001 (Needs `EventEnvelope`)
- Unlocks: Story 003 (Needs admitted queue for ordering)
