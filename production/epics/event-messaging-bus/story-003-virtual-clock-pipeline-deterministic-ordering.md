# Story 003: 5-Phase Virtual-Clock Pipeline & Deterministic Ordering

> **Epic**: Event / Messaging Bus  
> **Status**: Ready  
> **Layer**: Foundation  
> **Type**: Logic  
> **Estimate**: 3 hours  
> **Manifest Version**: 2026-09-21  
> **Last Updated**: 2026-09-21  

## Context

**GDD**: `design/gdd/event-messaging-bus.md`  
**Requirement**: `TR-FOUND-002`  
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: `ADR-0001: Deterministic Event/Messaging Bus for Gameplay Contracts`  
**ADR Decision Summary**: Deterministic 5-phase sequential pipeline driven by an injected virtual clock and strict lexicographical ordering tuple `(Phase, virtual_time, Rank_class, PublisherSeq)`.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  
**Engine Notes**: Decoupled from Unity `Time.time`, completely deterministic in headless and EditMode testing.

**Control Manifest Rules (this layer)**:
- Required: **Immutable Struct Event Envelopes** — envelopes must carry phase and timestamp. — source: `ADR-0001`
- Guardrail: **Event Bus Dispatch Budget** — Maximum 0.50 ms per frame budget for event draining. Dispatch operates on Unity's main thread with 0 heap allocation on hot paths. — source: `ADR-0001`

---

## Acceptance Criteria

*From GDD `design/gdd/event-messaging-bus.md`, scoped to this story:*

- [ ] **AC3 (5-Phase Sequential Execution)**: Events in `Phase 1` (Props/HideSpots) drain and complete all listener callbacks before any events in `Phase 2` (Sensing/Noise) or subsequent phases begin, regardless of the physical order in which `Publish` was called.
- [ ] **AC4 (Lexicographical Tie-Breaking)**: When two envelopes share the same phase and virtual timestamp, they are drained in strict order of `Rank_class` (priority) followed by monotonic `PublisherSeq`.

---

## Implementation Notes

*Derived from ADR-0001 Implementation Guidelines:*

1. Inject `IVirtualClock` interface to provide monotonic virtual time and active phase.
2. Maintain separate phase buckets or a prioritized ring buffer indexed by `(Phase, virtual_time, Rank_class, PublisherSeq)`.
3. Implement `DrainPhase(VirtualClockPhase phase)` to execute dispatches sequentially without cross-phase interleaving.
4. Assign monotonic `PublisherSeq` atomically upon ingress admission.

---

## Out of Scope

- [Story 001]: Token-based subscriptions and snapshot isolation.
- [Story 002]: Ingress deduplication and ring buffer backpressure.
- [Story 004]: Epoch barrier flushes and transactional perception handoffs.

---

## QA Test Cases

*Defined by QA Lead for automated testing:*

- **AC-3 (Phase Order Invariance)**:
  - Given: Envelope A (Phase 4: Guard AI) is published before Envelope B (Phase 1: HideSpot).
  - When: `Drain()` is executed for the tick.
  - Then: Assert Envelope B's listeners execute and finish before Envelope A's listeners start.

- **AC-4 (Lexicographical Sorting)**:
  - Given: Envelope 1 (`HideSpotEmpty`, Rank 1) and Envelope 2 (`HideSpotOccupied`, Rank 2) share Phase 1 and timestamp `12.40s`.
  - When: Phase 1 is drained.
  - Then: Assert Envelope 1 is delivered first.

---

## Test Evidence

**Story Type**: Logic  
**Required evidence**: `tests/EditMode/Foundation/EventBusOrderingPipelineTests.cs` — must exist and pass  
**Status**: [ ] Not yet created  

---

## Dependencies

- Depends on: Story 001, Story 002
- Unlocks: Story 004 (Needs ordering before handoff)
