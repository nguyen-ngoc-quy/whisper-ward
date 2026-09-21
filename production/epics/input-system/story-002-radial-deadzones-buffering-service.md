# Story 002: Radial Deadzones & Stance/Throw Input Buffering Service

> **Epic**: Input System Service  
> **Status**: Ready  
> **Layer**: Foundation  
> **Type**: Logic  
> **Estimate**: 3 hours  
> **Manifest Version**: 2026-09-21  
> **Last Updated**: 2026-09-21  

## Context

**GDD**: `design/gdd/input-system.md`  
**Requirement**: `TR-FOUND-009`  
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: `ADR-0005: Input Action Asset & Stance Buffering Contract`  
**ADR Decision Summary**: Dual radial deadzones ($0.10$ inner, $0.95$ outer), stance and throw input request buffering ($150\text{ ms} - 200\text{ ms}$ window) with consumption timestamp validation.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  
**Engine Notes**: Software radial deadzone filtering and time-stamped struct queue for buffer requests without heap allocations.

**Control Manifest Rules (this layer)**:
- Required: **Radial Deadzone Filtering** — Clamp analog stick input magnitude: $< 0.10 \implies 0.0$, $> 0.95 \implies 1.0$, linearly scale $[0.10, 0.95] \to [0.0, 1.0]$ preserving direction vector. — source: `ADR-0005`
- Required: **Stance & Throw Input Buffering** — Buffer stance transition requests (Crouch, Sprint) and Burst throw triggers for $150\text{ ms} - 200\text{ ms}$ ($0.15\text{ s} - 0.20\text{ s}$) before discarding as stale. — source: `ADR-0005`
- Forbidden: **Never Allocate on Input Buffering** — Use fixed-size pre-allocated structs or ring buffer for buffered input events; no `List<T>` expansions or heap allocations. — source: `ADR-0005`
- Guardrail: **Input System GC Budget** — Exactly 0 B (Zero GC) per frame during gameplay input polling and buffer evaluation. — source: `ADR-0005`

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [ ] **AC1 (Radial Deadzone Math)**: Analog stick input magnitude below $0.10$ evaluates to `Vector2.zero`. Magnitude above $0.95$ clamps to $1.0$ along the same heading. Magnitude between $[0.10, 0.95]$ scales linearly to $[0.0, 1.0]$.
- [ ] **AC2 (Stance & Throw Buffering Window)**: Stance change requests (Crouch toggle/hold, Sprint hold) and Burst throw triggers are buffered for $0.15\text{ s}$ ($150\text{ ms}$). Requests polled after $> 0.15\text{ s}$ expire and are automatically discarded.
- [ ] **AC3 (Single Consumption Semantics)**: Polling and consuming a buffered action invalidates it immediately so it cannot trigger twice.
- [ ] **AC4 (Zero Heap Allocation)**: Deadzone filtering and buffer checks generate 0 B heap allocations.

---

## Implementation Notes

*Derived from ADR-0005 Implementation Guidelines:*

1. Implement `RadialDeadzoneProcessor`:
   ```csharp
   public static Vector2 ProcessRadialDeadzone(Vector2 rawInput, float inner = 0.10f, float outer = 0.95f)
   {
       float magnitude = rawInput.magnitude;
       if (magnitude <= inner) return Vector2.zero;
       if (magnitude >= outer) return rawInput / magnitude;
       float normalizedMagnitude = (magnitude - inner) / (outer - inner);
       return (rawInput / magnitude) * normalizedMagnitude;
   }
   ```
2. Implement `InputBufferService`:
   - Store buffered actions as value types `BufferedInputRequest` with `double Timestamp` and `InputActionType Action`.
   - Expose `bool TryConsumeBufferedAction(InputActionType action, double currentTime, double maxAge = 0.15)`.

---

## Out of Scope

- [Story 001]: Action map lifecycle and input action wrapper caching.

---

## QA Test Cases

*Defined by QA Lead for automated testing:*

- **AC-1 (Radial Deadzone Boundary Checks)**:
  - Given: Raw stick input vector with magnitude $0.08$ ($< 0.10$).
  - When: `ProcessRadialDeadzone` is called.
  - Then: Assert result is `Vector2.zero`.
  - Given: Raw stick input vector with magnitude $0.98$ ($> 0.95$).
  - When: `ProcessRadialDeadzone` is called.
  - Then: Assert magnitude is strictly $1.0$ and direction matches original.
  - Given: Raw stick input vector with magnitude $0.525$ (midpoint of $[0.10, 0.95]$).
  - When: `ProcessRadialDeadzone` is called.
  - Then: Assert magnitude is approximately $0.50 \pm 0.001$.

- **AC-2 (Buffering Expiration & Single Consumption)**:
  - Given: A Crouch request is recorded at time $t = 1.00\text{ s}$.
  - When: Queried at $t = 1.10\text{ s}$ ($\Delta t = 0.10\text{ s} \le 0.15\text{ s}$).
  - Then: Assert `TryConsumeBufferedAction` returns `true`.
  - When: Queried a second time at $t = 1.11\text{ s}$.
  - Then: Assert `TryConsumeBufferedAction` returns `false` (already consumed).
  - Given: A Throw request is recorded at time $t = 2.00\text{ s}$.
  - When: Queried at $t = 2.16\text{ s}$ ($\Delta t = 0.16\text{ s} > 0.15\text{ s}$).
  - Then: Assert `TryConsumeBufferedAction` returns `false` (expired).

- **AC-3 (Zero GC Allocation)**:
  - Given: 1,000 deadzone calculations and buffer push/consume calls.
  - When: GC allocations are measured.
  - Then: Assert total GC memory allocated is 0 bytes.

---

## Test Evidence

**Story Type**: Logic  
**Required evidence**: `tests/EditMode/Foundation/InputBufferAndDeadzoneTests.cs` — must exist and pass  
**Status**: [ ] Not yet created  

---

## Dependencies

- Depends on: Story 001
- Unlocks: Input System Epic completion (`/story-done`)
