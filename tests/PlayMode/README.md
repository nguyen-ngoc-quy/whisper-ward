# Play Mode Tests

Integration tests that execute within a simulated Unity runtime player.
Use for cross-system interactions, physics raycasting/SphereCast, NavMesh path queries, and coroutine execution.

## Guidelines
- Assembly Definition: Requires `tests/PlayMode/PlayModeTests.asmdef` referencing `UnityEngine.TestRunner`.
- Use `[UnityTest]` attribute for tests requiring frame yields (`yield return null;` or `yield return new WaitForFixedUpdate();`).
- Ensure test fixtures clean up any spawned GameObjects during `[TearDown]`.
