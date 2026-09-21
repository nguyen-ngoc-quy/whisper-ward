# Story 001: Unified C# Input Actions Wrapper & Action Map Switching

> **Epic**: Input System Service  
> **Status**: Ready  
> **Layer**: Foundation  
> **Type**: Logic  
> **Estimate**: 3 hours  
> **Manifest Version**: 2026-09-21  
> **Last Updated**: 2026-09-21  

## Context

**GDD**: `design/gdd/input-system.md`  
**Requirement**: `TR-FOUND-008`  
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: `ADR-0005: Input Action Asset & Stance Buffering Contract`  
**ADR Decision Summary**: Strongly typed C# class wrapper for `WhisperWardInputActions.inputactions`, exclusive action map switching with state flush, and zero GC polling.

**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  
**Engine Notes**: Uses Unity New Input System (`com.unity.inputsystem` v1.7.0+). Actions cached once at startup; no string-based searches in gameplay loops.

**Control Manifest Rules (this layer)**:
- Required: **Strongly Typed C# Input Actions Wrapper** — Compile `WhisperWardInputActions.inputactions` to strongly typed C# class `WhisperWardInputActions`. Cache all `InputAction` references once during initialization. — source: `ADR-0005`
- Required: **Exclusive Action Map Switching** — Switch cleanly between `Player` and `UI` action maps. When switching to `UI` (or Pausing), flush all movement and look state to zero (`Vector2.zero`) to prevent stuck inputs. — source: `ADR-0005`
- Forbidden: **Never Query Actions by String at Runtime** — Never call `InputActionAsset.FindAction("Move")` or string indexers in game loops (`Update`/`FixedUpdate`). — source: `ADR-0005`
- Forbidden: **Never Allocate on Input Polling** — Never box values or allocate heap memory inside input polling methods (`ReadValue<Vector2>()` must use direct typed reads into readonly structs). — source: `ADR-0005`
- Guardrail: **Input System GC Budget** — Exactly 0 B (Zero GC) per frame during gameplay input polling. — source: `ADR-0005`

---

## Acceptance Criteria

*From GDD `design/gdd/input-system.md`, scoped to this story:*

- [ ] **AC1 (Typed Wrapper & Cache)**: `WhisperWardInputActions` is wrapped as a strongly-typed C# service. All actions (`Move`, `Look`, `Crouch`, `Sprint`, `Throw`, `Interact`, `Pause`) are cached once upon initialization without runtime string lookups.
- [ ] **AC2 (Exclusive Map Switching)**: The service exposes `SwitchToPlayerMap()` and `SwitchToUIMap()`. Enabling one automatically disables the other. Switching to UI or Pausing flushes active movement and look vectors to `Vector2.zero` immediately.
- [ ] **AC3 (Zero GC Allocation)**: Polling input vectors and action states generates 0 B heap allocations per frame.

---

## Implementation Notes

*Derived from ADR-0005 Implementation Guidelines:*

1. Generate / implement `WhisperWardInputActions` wrapper.
2. Implement `InputSystemService` which manages instance lifecycle and cached `InputAction` references.
3. On map transition:
   ```csharp
   public void SwitchToUIMap()
   {
       _playerActions.Disable();
       _cachedMoveInput = Vector2.zero;
       _cachedLookDelta = Vector2.zero;
       _uiActions.Enable();
   }
   ```
4. Expose `IPlayerInputProvider` interface for gameplay consumption.

---

## Out of Scope

- [Story 002]: Radial deadzones and stance/throw input buffer windows.

---

## QA Test Cases

*Defined by QA Lead for automated testing:*

- **AC-1 (No String Lookups in Hot Paths)**:
  - Given: `InputSystemService` is initialized.
  - When: `MoveInput` or `LookDelta` is polled 10,000 times.
  - Then: Assert 0 string comparison calls and 0 GC allocations.

- **AC-2 (Exclusive Action Map Switching & Flush)**:
  - Given: `Player` map is active with non-zero movement input `(0.8, 0.6)`.
  - When: `SwitchToUIMap()` is called.
  - Then: Assert `Player` map is disabled, `UI` map is enabled, and `MoveInput` immediately returns `(0, 0)`.

- **AC-3 (Zero Allocation Polling)**:
  - Given: Continuous polling of movement, look, crouch, and sprint state over 1,000 frames.
  - When: GC allocation is tracked.
  - Then: Assert total GC memory allocated is 0 bytes.

---

## Test Evidence

**Story Type**: Logic  
**Required evidence**: `tests/EditMode/Foundation/InputActionsWrapperTests.cs` — must exist and pass  
**Status**: [ ] Not yet created  

---

## Dependencies

- Depends on: None (Foundational input asset)
- Unlocks: Story 002 (Deadzones process polled values)
