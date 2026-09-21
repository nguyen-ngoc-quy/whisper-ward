# Epic: Event / Messaging Bus

> **Layer**: Foundation  
> **GDD**: `design/gdd/event-messaging-bus.md`  
> **Architecture Module**: `EventMessagingBus` (`WhisperWard.Foundation.EventBus`)  
> **Status**: Ready  
> **Stories**: Not yet created — run `/create-stories event-messaging-bus`  

## Overview

The Event / Messaging Bus (System #15) provides the decoupled, deterministic communication backbone for Whisper Ward. It routes, normalizes, orders, and dispatches gameplay events across all game subsystems without introducing circular dependencies between components. Operating on an injected virtual clock, it encapsulates all gameplay notifications in readonly struct envelopes, enforces ingress deduplication to eliminate double-dispatch anomalies, maintains a bounded ring buffer with fail-closed rejection policies, provides atomic epoch invalidation on respawn/reload to banish ghost events, and manages transactional two-phase handoff to AI perception pipelines.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|---|---|---|
| `ADR-0001: Deterministic Event/Messaging Bus` | Immutable struct event envelopes, ingress deduplication ring buffer (4096 capacity), atomic epoch invalidation barriers, and transactional perception handoff contract (`AcceptNoise`). | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|---|---|---|
| `TR-FOUND-001` | Struct-based zero-allocation event dispatch with type-safe subscription keys and snapshot lifecycle. | `ADR-0001` ✅ |
| `TR-FOUND-002` | Deterministic ordering tuple: `(virtual_time, priority, sequence_id)`. | `ADR-0001` ✅ |
| `TR-FOUND-003` | Ingress deduplication ring buffer (capacity 4096) with `fact_id` tracking. | `ADR-0001` ✅ |
| `TR-FOUND-004` | Atomic `attempt_epoch` invalidation: purge all queued/inflight events upon checkpoint reload or player respawn. | `ADR-0001` ✅ |

## Definition of Done

This epic is complete when:
- All stories under this epic are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/event-messaging-bus.md` (AC1 through AC12) are verified
- Deterministic ordering and ingress deduplication are validated by automated NUnit EditMode tests
- Zero heap allocation (0 B GC) is confirmed on event publishing and draining paths
- Atomic epoch flush guarantees zero event bleed-through across respawn/reload boundaries

## Next Step

Run `/create-stories event-messaging-bus` to break this epic into implementable stories.
