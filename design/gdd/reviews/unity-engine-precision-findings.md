# Unity-Engine Precision Findings — Player Noise (`NoiseEmitter`)

> **Author**: unity-specialist agent
> **Date**: 2026-09-01
> **Scope**: `design/gdd/player-noise.md` + `src/AI/Core/*.cs` (Burst runtime, VirtualTickClock, NoiseRuntimeConfiguration)
> **Purpose**: Unity-engine-specific implementation pitfalls in the physics/Burst/trajectory contract. Not a design review — the GDD is assumed correct; this audits whether Unity 6 LTS (6000.3.17f1) + PhysX can honor the contract as written.

---

## [unity-specialist] F1 — `Physics.Simulate` is not a "service"; the GDD's fixed-step language collides with Unity's fixed-tick model

**File**: `design/gdd/player-noise.md:58-62` (F4 contract)  
**File**: `src/AI/Core/BurstRuntimeConfiguration.cs:14-15`  
**File**: `src/AI/Core/VirtualTickClockService.cs:72-97`

The GDD repeatedly describes Burst as "a main-thread custom fixed-step service with semi-implicit Euler integration at `dt_max = 1/120 s`, **separate from Rigidbody simulation but sharing the physics scene**." The `BurstRuntimeConfiguration.CanonicalFixedSubstepSeconds = 1f / 120f` constant and the `VirtualTickClockService.Advance` loop confirm the intent. But Unity's `Physics.Simulate(float step)` is the only way to advance the PhysX scene outside FixedUpdate, and it has strict requirements:

1. **`Physics.Simulate` must be called from the main thread** — the GDD's "main-thread custom service" phrasing is correct, but the GDD never states this explicitly, and a sub-agent could easily schedule it on a job thread.
2. **`Physics.Simulate` is re-entrant only if called from the same thread** — calling it again while a previous `Simulate` is still processing (e.g., if a listener callback triggers another `Simulate`) causes a crash. The GDD's "shared physics scene" language invites the assumption that Unity's own `FixedUpdate` and the custom service can both call `Physics.Simulate` on the same frame.
3. **Unity's `FixedUpdate` itself calls `Physics.Simulate` internally** (at the project's fixed timestep, typically `0.02 s` / 50 Hz in Unity 6). If the project's `Time.fixedDeltaTime` is 0.02 and the GDD's custom service runs `Physics.Simulate(1/120)` *in addition to* Unity's built-in FixedUpdate, **the scene is simulated twice per frame**: once at 50 Hz by Unity, once at 120 Hz by the custom service. This is not "separate from Rigidbody simulation" — it *is* Rigidbody simulation, and the two runs will desync.

**Why it matters**: The contract's "separate from Rigidbody simulation" clause is internally contradictory with "sharing the physics scene." If Burst is kinematic (no Rigidbody), it doesn't need `Physics.Simulate` at all — it can use `Physics.OverlapSphere` / `Physics.SphereCast` against the scene directly without advancing the physics simulation. If it *does* call `Physics.Simulate`, it must be the *only* physics advance on that thread, which means Unity's `FixedUpdate` must be disabled or its `fixedDeltaTime` set to `1/120`.

**Recommendation**: Pick one model and document it:
- **Model A (kinematic-only, no PhysX advance)**: Burst is a pure kinematic integrator; it uses `Physics.SphereCast` / `Physics.Linecast` as query-only calls against the static scene. No `Physics.Simulate`. This is the cleanest and matches the GDD's "kinematic via the single-hit SphereCast (no Rigidbody)" statement.
- **Model B (full PhysX advance)**: The custom service *is* the physics simulator. Disable Unity's `FixedUpdate` physics (set `Time.fixedDeltaTime = 1/120` and call `Physics.autoSimulation = false`, then call `Physics.Simulate(1/120)` from the service). Document that this replaces Unity's built-in FixedUpdate for the Burst scene, not supplements it.

The current wording supports neither model unambiguously.

---

## [unity-specialist] F2 — `Physics.autoSyncTransforms` is a global setting; the GDD's per-batch scope is unimplementable as written

**File**: `design/gdd/player-noise.md:521-522`, `62`  
**File**: `design/gdd/player-noise.md:11` (AC21)

The GDD states: "**one explicit `Physics.SyncTransforms()` before each due batch**" and "Player Noise never toggles the global `Physics.autoSyncTransforms` setting per query." Both statements are problematic:

1. **`Physics.autoSyncTransforms` is a global `bool`** (`UnityEngine.Physics.autoSyncTransforms`). There is no per-system, per-batch, or per-query scope. Setting it `true` affects *all* physics transforms in the scene, including every `Rigidbody`, `CharacterController`, `TrailRenderer`, and `LineRenderer` that Unity syncs each frame. Toggling it around a `SyncTransforms` call is a global side effect.
2. **`Physics.SyncTransforms()` is redundant and dangerous** when `autoSyncTransforms = true`. Unity already syncs transforms once per physics step (in `FixedUpdate`). Calling `SyncTransforms()` manually after each physics step is a no-op unless transforms were moved outside the physics API. If `autoSyncTransforms = false`, then *nothing* syncs — including Unity's own animation-driven transforms — unless you call `SyncTransforms()` yourself. The GDD's "one explicit sync before each due batch" only makes sense if `autoSyncTransforms = false`, which means the *entire project* must adopt manual transform sync.
3. **The `max_backlog_ticks = 8` catch-up path makes this worse.** If the backlog fires 8 ticks in a row, the GDD expects 8 `SyncTransforms()` calls in 8 `Physics.Simulate(1/120)` calls. But if a `Rigidbody`-based system (NavMeshAgent, player controller) also runs in the same frame, its transform is out of sync for the *entire* backlog window unless it also gets a `SyncTransforms()` after each of its own steps.

**Why it matters**: AC21's `PENDING_OQ6` caveat explicitly flags this as not-yet-passing evidence. The GDD itself acknowledges the measurement boundary is incomplete. But the deeper issue is that `autoSyncTransforms` cannot be scoped — it's a project-wide toggle. A per-measurement contract on a global is a design lie.

**Recommendation**: Replace the `autoSyncTransforms` / `SyncTransforms` requirement with a concrete, scoped mechanism:
- Either move Burst's transform updates off the physics sync path entirely (use `Transform.position` directly for the kinematic sphere, and rely on `Physics.SphereCast`'s `queryStartInColliders = false` + the E20 mask for collision — no transform sync needed).
- Or document that the project sets `Physics.autoSyncTransforms = false` globally and that Burst's `SyncTransforms()` call is the *sole* transform-sync point for the entire physics scene (a project-wide architectural decision, not a per-batch GDD clause).

---

## [unity-specialist] F3 — `Physics.SphereCast` + initial-overlap check: the GDD's sequence is not what Unity's API does

**File**: `design/gdd/player-noise.md:497-522`, `599-616`  
**File**: `design/gdd/player-noise.md:194`

The GDD specifies this sequence for each Burst substep:
1. Initial-overlap query at `origin = p_current` (the projectile center).
2. If overlap → `burst-launch-blocked`.
3. Otherwise, `Physics.SphereCast(origin, radius, direction, maxDistance, mask, QueryTriggerInteraction.Ignore)` with Unity closest-hit semantics.

The problem is the **initial-overlap query**. The GDD says "an initial-overlap result is a blocked launch handled before spend." But `Physics.SphereCast` *itself* can report an overlap at `distance = 0` if the sphere starts inside a collider — it returns the closest hit, and for a starting-overlap the hit `distance` is 0 and `normal` is undefined (or point-inward). The GDD treats "initial overlap" and "SphereCast hit" as two distinct outcomes, but **Unity's `SphereCast` collapses them**: a spherecast starting inside a collider *is* a hit, with `distance = 0`.

Specifically, the GDD's line 500-503 shows:
```text
origin      = p_current
direction   = normalize(p_next − p_current)
maxDistance = |p_next − p_current|
radius      = projectile_radius
```

If `p_current` is inside a solid, `SphereCast` with `maxDistance = |p_next − p_current|` will return a hit with `distance = 0` (the sphere is already overlapping). The GDD wants a *separate* overlap check that returns "blocked" without producing a `collider_surface_contact` and `hit.normal` (which it then uses for push-out). But Unity gives you one or the other — you cannot get a clean "yes, overlapping, but no contact normal" from `SphereCast`.

**Unity API reality**:
- `Physics.SphereCast` returns `true` if the sphere *starts* overlapping or *sweeps* into something. There is no separate "overlap-only" mode on `SphereCast`.
- To get a clean initial-overlap check *without* a sweep, you must call `Physics.OverlapSphere(p_current, projectile_radius, mask, QueryTriggerInteraction.Ignore)` and inspect the results yourself.
- `Physics.CheckSphere` or `Physics.OverlapSphere` is the correct primitive for the initial-overlap check; `SphereCast` is the correct primitive for the swept contact.

**Why it matters**: If you use `SphereCast` for both, the initial-overlap "hit" will have `distance = 0` and an undefined `normal`, which will corrupt the `collider_surface_contact + normal × (projectile_radius + epsilon_contact)` push-out formula. The push-out will either be zero (if normal is `(0,0,0)`) or point inward (if Unity returns an inward normal), pushing the projectile *into* the wall instead of out of it.

**Recommendation**: Split the two queries explicitly in the implementation contract:
- Initial overlap: `Physics.OverlapSphere(p_current, projectile_radius, E20mask, QueryTriggerInteraction.Ignore)` — if any result, reject.
- Swept contact: `Physics.SphereCast(p_current, direction, hit, maxDistance, projectile_radius, E20mask, QueryTriggerInteraction.Ignore)` — use `hit.point` and `hit.normal`.
- Document that `SphereCast` is NOT used for the initial-overlap check; the overlap check is a separate `OverlapSphere` call.

---

## [unity-specialist] F4 — Floating-point drift on `fixed_time_accumulator` over a 3s flight: the integer-tick comparison is fragile

**File**: `design/gdd/player-noise.md:60-62`, `644`  
**File**: `src/AI/Core/VirtualTickClockService.cs:88-97`

The GDD specifies: "every simulation batch runs `floor(accumulator / dt_max)` integer ticks" and "the contact-versus-timeout comparison is performed on **integer fixed-tick counts** — `tick_index ≤ timeout_tick`, where `timeout_tick = ⌈3.0 s / dt_max⌉` (exactly 360 at `dt_max = 1/120 s`) — never on the accumulated float `flight_elapsed`."

The `VirtualTickClockService.Advance` loop confirms the pattern:
```csharp
_accumulator += gameplayDelta;
while (_accumulator >= TickInterval)
{
    _accumulator -= TickInterval;
    CurrentTick++;
    ...
}
```

The problem is the **accumulator itself**. `_accumulator` is a `float` (`VirtualTickClockService` line 23). The GDD never specifies the accumulator type, but the code uses `float`. Over a 3s flight at `dt_max = 1/120 ≈ 0.0083333333 s`, the accumulator accumulates `3.0 / 0.0083333333 ≈ 360` subtractions of `0.0083333333` from a running sum. IEEE 754 `float` (24-bit mantissa, ~7 decimal digits) cannot represent `0.0083333333` exactly, and after 360 subtractions the residual can drift by up to ~`360 × 2^-14 ≈ 0.022` seconds — enough to make `floor(accumulator / dt_max)` off by one tick in edge cases, and potentially corrupt the `contact_event_time = flight_elapsed + α × dt_max` comparison.

The GDD's own line 644 says the comparison must use integer tick counts, never the float `flight_elapsed`. But `flight_elapsed` is *derived from the float accumulator* — `flight_elapsed = CurrentTick × dt_max` or similar — so the integer tick count itself is derived from a float accumulator. If the accumulator drifts, the integer tick count can diverge from the "true" tick count by 1.

**Specific risk**: If the accumulator's residual fraction grows slightly above `1.0` due to rounding (e.g., `0.0083333333 × 120 = 0.99999994` in float, which rounds *down*, not up), the `while (_accumulator >= TickInterval)` condition can under-count by one tick per 120 advances. Over 3s this is 360 advances × (120 per second) = 43,200 float operations. The cumulative error is bounded but non-zero.

**Recommendation**: Use `double` for `_accumulator` and for `flight_elapsed`. The `VirtualTickClockService` currently uses `float` for the accumulator and `float` for `TickInterval`. The `double` accumulator eliminates drift for a 3s flight (a `double` has 52-bit mantissa; `0.0083333333` × 360 is representable to many more digits than needed). If `float` is required for memory/ALC reasons, the accumulator must be periodically re-based (e.g., `if (_accumulator > 1.0f) { _accumulator -= TickInterval; CurrentTick++; }` with a clamp) rather than a `while` loop that subtracts repeatedly. The GDD should explicitly require `double` for the accumulator and `flight_elapsed`.

---

## [unity-specialist] F5 — The `max_frame_delta_s = 0.067s` clamp is unmeasurable on WebGL; the 33ms hitch gate has no defined measurement primitive

**File**: `design/gdd/player-noise.md:60-62`, `11` (AC21)  
**File**: `src/AI/Core/VirtualTickClockService.cs:81`

The GDD states: "a render stall (e.g. the WebGL 33 ms hitch) is classified separately" and "the virtual clock's per-call gameplay-time advance is clamped to `max_frame_delta_s` (registered, starter `0.067 s` = 8 ticks at `dt_max`)". The `VirtualTickClockService` hardcodes `const float maxFrameDelta = 0.0667f` (line 81).

The problems:

1. **`0.0667f` is not `0.067s`**. The clamp value in code (`0.0667f`) differs from the GDD's stated `0.067s`. This is a 0.5% discrepancy that matters for the boundary: `0.0667 / (1/120) = 8.004` ticks (clamped to 8), while `0.067 / (1/120) = 8.04` ticks (also clamped to 8). The clamp produces the same integer result here, but the *diagnostic label* `clock-delta-clamped` will fire at different thresholds. The GDD and the code disagree on the constant.

2. **WebGL has no reliable frame-time measurement API that matches the GDD's "33 ms hitch" definition.** Unity's `Time.unscaledDeltaTime` on WebGL reports the *render-frame* time, but the browser's event-loop jitter means a "hitch" can be 100ms+ and the exact boundary between a "hitch" and "normal frame" is browser-defined. The GDD's "33 ms" comes from `1000ms / 30fps`, but WebGL frames are not scheduled at 33ms — they're scheduled by the browser's requestAnimationFrame, which can skip frames entirely. There is no `Physics.autoSyncTransforms`-style API to detect "this frame was a hitch."

3. **The "hitch frames are EXCLUDED from the steady-state gates" rule requires the measurement system to *classify* each frame as hitch or non-hitch.** But the `VirtualTickClockService` does not classify frames — it clamps the delta and increments the clock. The classification (hitch vs. non-hitch) happens somewhere else, and the GDD never specifies *where* or *how*.

**Recommendation**: Define the hitch measurement explicitly:
- Specify the measurement primitive (e.g., `System.Diagnostics.Stopwatch` on the main thread, or `UnityEngine.ProfilerDriver.GetFrameTime()` if available, or a custom `FrameTiming` query).
- Define the classification threshold (e.g., a frame where `deltaTime > 2 × T_hearing = 0.4s` is a hitch, or use the `max_frame_delta_s` clamp itself as the threshold).
- Define where the classification happens (the `VirtualTickClockService.Advance` caller, or a separate `FrameHitchDetector` class).
- Remove the hardcoded `0.0667f` from `VirtualTickClockService` and inject `max_frame_delta_s` from the registry (`entities.yaml`).

---

## [unity-specialist] F6 — `LayerMask.NameToLayer(name) ≥ 0` validation and "content-ful at scene load": the enumeration is unimplementable as written

**File**: `design/gdd/player-noise.md:52` (E20), `62`  
**File**: `design/gdd/player-noise.md:230` (Physics dependency)

The GDD states: "The mask is also asserted **content-ful at scene load**: the loaded scene must contain at least one non-trigger collider on the mask, or E20 queries are meaningless." And the `LevelFixture` validation leg `navmesh_layer_separation_check` "enumerates every collider in the level scene and fails closed if any NavMesh collider's layer intersects the E20 World mask."

The problems:

1. **"Enumerate every collider in the level scene" is not a runtime operation you can do once and cache.** `Physics.FindColliders` does not exist in Unity's API. The only way to enumerate all colliders is `FindObjectsOfType<Collider>()` (or `FindObjectsOfType<Collider2D>()`), which is expensive, returns all colliders including disabled ones, and does not include physics-only colliders that are part of a `MeshCollider` on a disabled `GameObject`. More importantly, `FindObjectsOfType` returns *active* scene objects only — it does not include prefab instances that haven't been instantiated yet, or colliders added after scene load via addressables.

2. **The "content-ful at scene load" assertion cannot be done once at load.** Scenes can be loaded additively, and colliders can be added/removed at runtime (e.g., procedural level generation, destructible geometry). The GDD says "validated once at scene load, not per query" — but if a collider is added later, the assertion is stale.

3. **`LayerMask.NameToLayer(name) ≥ 0` is necessary but not sufficient.** `LayerMask.NameToLayer` returns −1 if the layer name doesn't exist, but it also returns −1 if the layer name is empty or null. The GDD says "fail load if any returns −1," but it doesn't say what happens if the *same* layer name resolves to different layer indices across additive scenes (Unity's layer list is global, so this can't happen — but the GDD doesn't state this, leaving ambiguity).

4. **The "at least one non-trigger collider on the mask" check requires iterating all colliders and testing `IsTrigger` and `(collider.gameObject.layer & mask) != 0`.** This is `O(n)` in the number of colliders in the scene. For a typical stealth game with hundreds of colliders, this is fine at load, but the GDD doesn't specify whether the check runs synchronously (blocks load) or asynchronously.

**Recommendation**: Replace "enumerate every collider" with a concrete, cacheable operation:
- At scene load, call `FindObjectsOfType<Collider>()`, filter to `!isTrigger && (layer & mask) != 0`, and assert `count > 0`. Cache the count (or a boolean) in the `Physics` ADR.
- If additive scenes are used, either re-run the check per additive scene load, or assert that all additive scenes use the same layer setup (a project-level constraint, not a runtime check).
- Document that `Physics.FindColliders` does not exist and that `FindObjectsOfType` is the closest available primitive.

---

## [unity-specialist] F7 — `Physics.queriesHitBackfaces = false` + mesh winding: interaction with GPU skinning and accelerated physics is unspecified

**File**: `design/gdd/player-noise.md:52` (E20), `62`

The GDD pins `Physics.queriesHitBackfaces = false` and a mesh-winding policy: "a backface hit would leak hearing through single-sided authored geometry." The `NoisePerformanceAudit` and `Physics ADR` are referenced but not included in the GDD's `docs/engine-reference/` snapshot.

The problem:

1. **`Physics.queriesHitBackfaces` only affects `Physics.Raycast`, `Physics.SphereCast`, `Physics.Linecast`, and their `NonAlloc` variants.** It does *not* affect `Physics.OverlapSphere`, `Physics.CheckSphere`, or `Physics.OverlapBox`. If the initial-overlap check uses `OverlapSphere` (as F3 recommends), `queriesHitBackfaces` is irrelevant for that check.

2. **GPU skinning and accelerated physics.** Unity's PhysX can use the GPU for physics queries on some platforms (WebGL via WebAssembly, console). When GPU-accelerated physics is enabled, `Physics.queriesHitBackfaces` may be ignored or behave differently because the GPU ray-tracing path may not honor the CPU-side `queriesHitBackfaces` setting. The GDD does not specify whether GPU-accelerated physics is enabled or disabled for this project.

3. **Mesh winding order.** Unity's mesh import settings include "Read/Write Enabled" and "Optimize Mesh" flags that can reorder triangles. If a mesh is imported with `Read/Write Enabled = false`, Unity may not be able to read backface normals at query time, and `queriesHitBackfaces = false` may behave as if backfaces don't exist (i.e., the query only hits frontfaces, but the winding order is unknown). The GDD's "mesh-winding policy" requires authored meshes to have consistent winding, but it doesn't enforce this at import time (e.g., via a pre-build check on `Mesh.importFlags`).

**Recommendation**: In the Physics ADR, document:
- Whether GPU-accelerated physics is enabled (if yes, `queriesHitBackfaces` may not apply to all queries — test on target platform).
- That `queriesHitBackfaces = false` only applies to cast/raycast queries, not overlap queries.
- The mesh import validation: authored level geometry must have `Read/Write Enabled = true` and consistent clockwise winding for front faces.
- A pre-build check that verifies all `MeshFilter.mesh` in the level scene has the expected winding convention.

---

## [unity-specialist] F8 — `Physics.SyncTransforms()` before each due batch: thread-safety and conflict with Unity's internal sync

**File**: `design/gdd/player-noise.md:521-522`, `62`  
**File**: `src/AI/Core/VirtualTickClockService.cs:72-97`

The GDD requires "one explicit `Physics.SyncTransforms()` before each due batch" in a "main-thread custom service." The `VirtualTickClockService.Advance` loop (line 88-97) calls listeners synchronously inside the tick loop, and the Burst simulation presumably runs inside one of those listener callbacks.

The problems:

1. **`Physics.SyncTransforms()` is a main-thread-only API.** If the custom service's tick loop runs inside a `MonoBehaviour.Update()` callback, it's on the main thread and `SyncTransforms()` is safe. But if the service is ever moved to a coroutine, a job, or a separate thread (e.g., for the `burst-backlog-clamped` diagnostic path), `SyncTransforms()` will crash or silently fail.

2. **Conflict with Unity's internal sync.** Unity calls `Physics.SyncTransforms()` internally at the end of `FixedUpdate` if `autoSyncTransforms = true`. If the custom service calls `SyncTransforms()` *before* Unity's `FixedUpdate` (which it would, if the custom service runs in `Update` and Unity's `FixedUpdate` comes later), the transforms are synced twice — once by the service, once by Unity. If the service calls `SyncTransforms()` *after* Unity's `FixedUpdate`, the transforms are already synced and the call is a no-op. The GDD's "one explicit sync before each due batch" has no effect if `autoSyncTransforms = true` (the no-op case) or is dangerous if `autoSyncTransforms = false` (the sync-is-required case, because Unity's internal sync won't happen).

3. **The `max_backlog_ticks = 8` catch-up path means 8 consecutive `Physics.Simulate` calls, each preceded by a `SyncTransforms`.** If a `Rigidbody`-based system (e.g., the player controller) also runs `Physics.Simulate` or `FixedUpdate` between the Burst service's ticks, the `SyncTransforms` calls will desync from the Rigidbody's transform updates. The Burst simulation's kinematic sphere will be at a different position than what the Rigidbody-based systems see, causing visual tearing and physics glitches.

**Recommendation**: Either:
- Set `Physics.autoSyncTransforms = false` globally and make the Burst service the *sole* transform-sync point (document this as a project-wide architectural decision).
- Or eliminate the `SyncTransforms` requirement entirely by moving Burst's kinematic sphere off the physics transform path (set `Transform.position` directly, don't rely on physics sync). The kinematic sphere doesn't need to be synced to anything — it's only used for collision queries against the static scene.

---

## [unity-specialist] F9 — Audio main-thread cost measurement is not well-defined in Unity's profiling model

**File**: `design/gdd/player-noise.md:11` (AC21), `sound_performance_audit.md` (referenced)

The GDD's performance envelope requires "noise-path subsystem aggregate ≤12.0 ms p95/p99" and "the **whole-frame WebGL gate ≤33.0 ms p95 at the 30 fps floor**." The `sound_performance_audit.md` is referenced for "audio main-thread cost," but the GDD doesn't specify how this cost is measured.

The problems:

1. **Unity's audio system is split between main thread and audio thread.** In Unity's audio pipeline, sound generation (DSP callbacks) runs on a **separate audio thread**, not the main thread. The main thread's cost is primarily: (a) submitting audio commands to the audio thread (`AudioSource.Play()`, `SetParameter` on mixer snapshots), and (b) the audio mixer's update pass (which runs on the main thread during `AudioController.Update`). The actual DSP processing (sample generation, effects, filtering) runs on the audio thread.

2. **The "audio main-thread cost" is not a single measurable number.** It depends on what you include: just the `AudioSource.Play()` calls? The mixer update? The `AudioListener` update? The `AudioMixer` parameter setcalls? The GDD doesn't specify which of these constitutes the "audio main-thread cost" for the subsystem slice.

3. **WebGL's audio architecture is different.** On WebGL, Unity's audio uses the browser's Web Audio API, which runs on a separate thread. The main-thread cost of submitting audio commands is minimal (a few `AudioBufferSourceNode` creations), but the *latency* of the audio thread is browser-dependent and not directly measurable from Unity's profiler. The "audio main-thread cost" measurement that works on PC may not work on WebGL, and vice versa.

4. **Unity's Profiler API does not provide a clean "audio main-thread cost" counter.** You can use `Profiler.BeginSample("Audio")` / `Profiler.EndSample()` around the audio submission code, but this only captures the main-thread submission cost, not the audio-thread DSP cost. The GDD's "audio main-thread cost" is ambiguous between these two.

**Recommendation**: In the `sound_performance_audit.md` (or the Physics ADR), define exactly what "audio main-thread cost" measures:
- Specify the measurement primitive (e.g., `Profiler.GetTotalAllocatedMemoryLong()` for audio, or custom `Stopwatch` around `AudioMixer.SetFloat` calls, or `Unity.Profiling.ProfilerMarker`).
- Specify which audio subsystem operations are included (mixer updates, `AudioSource.Play()`, DSP callback submission).
- Specify whether the measurement includes the audio-thread DSP cost or only the main-thread submission cost.
- Provide a WebGL-specific measurement path (e.g., browser's `performance.now()` around the audio context initialization, or Unity's `AudioSettings.dspBufferSize` as a proxy).

---

## Summary of findings

| # | Finding | Severity | GDD line(s) | Source file(s) |
|---|---------|----------|-------------|----------------|
| F1 | `Physics.Simulate` collision with Unity's built-in FixedUpdate; "separate from Rigidbody but sharing the scene" is contradictory | **BLOCKER** | 58-62, 493 | `BurstRuntimeConfiguration.cs:14`, `VirtualTickClockService.cs:72` |
| F2 | `Physics.autoSyncTransforms` is global; per-batch scope is unimplementable | **BLOCKER** | 521-522, 62, 11 | `VirtualTickClockService.cs:81` |
| F3 | `SphereCast` for initial-overlap collapses overlap + hit into one result; `OverlapSphere` is the correct primitive | **BLOCKER** | 497-522, 194 | — |
| F4 | `float` accumulator drifts over 3s / 360 ticks; integer tick comparison is fragile | **IMPORTANT** | 60-62, 644 | `VirtualTickClockService.cs:23,88-97` |
| F5 | `max_frame_delta_s` constant disagrees between GDD and code; 33ms hitch is unmeasurable on WebGL | **IMPORTANT** | 60-62 | `VirtualTickClockService.cs:81` |
| F6 | "Enumerate every collider" is unimplementable; `FindObjectsOfType` is not cacheable; additive scenes break it | **IMPORTANT** | 52, 230 | — |
| F7 | `queriesHitBackfaces = false` doesn't apply to overlap queries; GPU-accelerated physics may ignore it | **IMPORTANT** | 52, 62 | — |
| F8 | `SyncTransforms()` main-thread-only; conflicts with Unity's internal sync and Rigidbody systems | **IMPORTANT** | 521-522, 62 | `VirtualTickClockService.cs:72` |
| F9 | "Audio main-thread cost" is not well-defined in Unity's profiling model; WebGL differs from PC | **ADVISORY** | 11, sound_performance_audit.md | — |

**Net assessment**: 3 of 9 findings are **blockers** — the Burst simulation model (F1), the transform-sync scope (F2), and the initial-overlap query primitive (F3). These three must be resolved before implementation can begin. The remaining 6 are important or advisory but do not block coding.
