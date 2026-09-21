# Edit Mode Tests

Unit tests that run without entering Play Mode.
Use for pure logic: mathematical formulas, finite state machines, data contracts, and serialization validation.

## Guidelines
- Assembly Definition: Requires `tests/EditMode/EditModeTests.asmdef` referencing `UnityEngine.TestRunner` and `UnityEditor.TestRunner`.
- Fast execution: Runs headlessly in seconds.
- Zero scene dependencies: Mock or inject all required services (e.g. `IEventBus`, `IPhysicsQueryService`).
