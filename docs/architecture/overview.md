# Architecture Overview

## What this is

PATween is a Unity tween engine with a static-method API, struct handles, pooled internal storage, and a PlayerLoop-based runner. No MonoBehaviour.

## Shape

See the [design document](design.md) for goals, non-goals, and locked anchors.

---

## Architecture

### Layers

```
┌────────────────────────────────────────────────────────────┐
│ Public API (struct handles, static shortcuts, TweenSettings)│
├────────────────────────────────────────────────────────────┤
│ Internal core: TweenData, SequenceData, ease eval, loop    │
│ math, position math, callback dispatch, safe mode wrapper  │
├────────────────────────────────────────────────────────────┤
│ Storage: pooled class instances behind id+generation table │
├────────────────────────────────────────────────────────────┤
│ Runner: PlayerLoop subsystems for Update/Late/Fixed/Manual │
└────────────────────────────────────────────────────────────┘
```

### The parent-sequence model

Every animation (`Tween` or `Sequence`) has:

- `_parent` — owning sequence (or root)
- `_start`, `_end` — position in parent-local seconds
- `_timeScale` — local time multiplier
- `_paused`, `_reversed`
- `localTime` — current playhead in own duration (double precision)

The runner owns a hidden `_rootSequence` for each `UpdatePhase`. Top-level tweens are children of the root. When the runner ticks a root with `deltaTime`, the root advances its playhead and recursively renders children at their parent-local times. Recursive `timeScale`, `pause`, `reverse`, `seek` fall out for free.

Implementation note: the root is iterated by index over a flat list of active child ids for cache locality. Nested `Sequence` children with ordering use sorted child arrays.

### Storage and handles

`TweenData` is the abstract base (holds `_parent`, `_start`, `_end`, `_timeScale`, `localTime`, `_paused`, `_reversed`, status, callbacks, target ref). The typed subclass `TweenData<T>` adds `start: T`, `end: T`, `getter`, `setter`, and the `IInterpolator<T>` used to lerp. The runner iterates `List<TweenData>` and calls a virtual `Step(double dt)` per child. This means one vtable dispatch per tween per frame in v1; an acceptable cost (~1-2 ns on modern CPUs). M5 SoA replaces this with per-`(TValue, TInterpolator)` storage and a `[BurstCompile]` job; see [performance.md](performance.md) and [phases.md](../../planning/phases.md).

```csharp
internal static class TweenStore
{
    static TweenData[]    _data;        // pooled, indexed by handle.id
    static uint[]         _generations; // bumped on recycle
    static Stack<int>     _free;
    static List<int>      _activeUpdate, _activeLate, _activeFixed, _activeManual;
}
```

`Tween` handle = `(int id, uint generation)`. All public ops do a generation check first; mismatched generations no-op safely.

**Pool exhaustion**: when `_free` is empty and the pool is at capacity, `TweenStore` grows by doubling the backing arrays (same strategy as `List<T>`). This is an allocation, but it is bounded to startup / burst-creation periods. A `Debug.LogWarning` is emitted in Editor when growth occurs, so the developer can pre-size via `PATween.SetCapacity` instead. There is no eviction and no hard ceiling in v1; refusing to create would be a silent correctness failure worse than the alloc.

`TweenData<T>` layout groups blittable scalars (`start, end, duration, localTime, timeScale, easeParamA, easeParamB`) at the top of the base class so the M5 SoA split is mechanical.

### Runner (PlayerLoop)

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void InstallRunner()
{
    TweenStore.Reset();   // explicit clear for Fast Enter Play Mode (no domain reload)

    var loop = PlayerLoop.GetCurrentPlayerLoop();
    InsertAfter<Update.ScriptRunBehaviourUpdate>(ref loop, TickUpdate);
    InsertAfter<PreLateUpdate.ScriptRunBehaviourLateUpdate>(ref loop, TickLate);
    InsertAfter<FixedUpdate.ScriptRunBehaviourFixedUpdate>(ref loop, TickFixed);
    PlayerLoop.SetPlayerLoop(loop);
}
```

- **Insertion position**: after the standard script update for each phase, so MonoBehaviour code sees pre-tween state in its own update and tweens drive end-of-phase values.
- **Time source** per phase: `Update` uses `Time.deltaTime` for time-scaled tweens, `Time.unscaledDeltaTime` for `ignoreTimeScale = true`; `LateUpdate` same; `FixedUpdate` uses `Time.fixedDeltaTime` (and `Time.fixedUnscaledDeltaTime` for unscaled). `Manual` uses caller-provided delta. Unity's built-in `maximumDeltaTime` clamp applies to `Time.deltaTime` automatically.
- **Thread safety**: handles are read-safe across threads (struct value, generation check on read), but every mutating control method is main-thread only and asserts in safe mode. `PATween.X(...)` builders and registration calls are likewise main-thread only. Awaiter continuations are posted to the next PlayerLoop tick on the main thread regardless of capture context, so `await tween;` from any thread always resumes on the main thread.
- **Editor**: a second hookup via `EditorApplication.update` ticks an editor-only runner.
- **Manual**: `PATweenRunner.ManualTick(deltaTime)` advances only the `Manual` root.
- **Domain reload / Fast Enter Play Mode**: `TweenStore.Reset()` runs at `SubsystemRegistration` time. Editor uses `[InitializeOnLoad]` to also reset on assembly reload. Both cases drop all tweens cleanly so generation ids stay coherent.
- **Debug visibility**: the M4 EditorWindow reads active tweens directly from `TweenStore`. No scene-side proxy needed.

### Update step (high level)

For each active root:

```text
1. dt = caller-provided                             // double precision
2. root.localTime += dt * root._timeScale           // double accumulator
3. for each child in root.activeChildren (snapshot):
   3a. if child._isUnityObject && targetRef == null: queue auto-kill, continue
   3b. if root.localTime < child._start: continue
   3c. childLocal = (root.localTime - child._start) * child._timeScale
   3d. compute loop iteration + eased t (eased t cast to float for setter)
   3e. setter(Lerp(start, end, easedT))             // safe-mode wrapped
   3f. fire OnUpdate; fire OnStepComplete/OnComplete as needed (multicast)
   3g. if completed && autoKill: queue kill
4. drain deferred mutation buffer (callbacks may have queued Kill/Insert/etc.)
5. apply queued kills (no list mutation mid-iter)
```

**Time precision**: internal time accumulators (`localTime`, root playhead) are `double` to bound drift over long sessions, repeated seeks, and nested timescales. Interpolation output is `float`. The eased `t` is cast to `float` immediately before the setter call.

**Auto-kill flag**: `TweenData` caches `_isUnityObject` (bool) at creation time so the hot loop is a flag check + cached `targetRef == null` (Unity's overloaded `==`), not a runtime type test per tween per frame.

**Burst note (M5)**: Unity's overloaded `==` requires the main thread, so the auto-kill scan stays in the managed sidecar even when M5 moves the math to a Burst job. The Burst job operates on `TweenDataHot<T>` only and writes outputs; the managed pass that runs immediately after handles auto-kill, callback dispatch, and deferred-mutation drain.

### Ease system

The ease system is documented in the API reference: [api/easings.md](../../api/easings.md). Internally, evaluation uses a static function table indexed by `EaseType`. Standard eases are cached as static `EaseRef` instances; parametric variants construct a struct on the stack. `Curve` and `Custom` read their `AnimationCurve` / delegate slots.

### From and FromTo

Snap timing matches the design anchor:

- **Root tween**: snap fires synchronously inside `.Start()`. The tween's own `SetDelay(...)` defers interpolation but not the snap.
- **Sequenced child** with parent-imposed offset `> 0`: snap is deferred. The child carries a `_snapPending` flag set at append time. The flag is consumed on the first parent tick where the playhead crosses `child._start` in the forward direction. Backward seek past `child._start` re-arms the flag.
- `From`: at snap time, read current value via getter; that becomes `end`. The supplied argument is `start`. Invoke `setter(start)`.
- `FromTo`: arguments are `start` and `end` directly. Invoke `setter(start)` at snap time.

`invalidate()` (M4) re-arms the snap on a live handle.

### Auto-kill and SetLink

- Per-frame, if `target is UnityEngine.Object o && o == null` → kill silently.
- `SetLink(GameObject, LinkBehavior)` (M2): `KillOnDestroy`, `KillOnDisable`, `PauseOnDisable`, `PauseOnDisableRestartOnEnable`. Implemented by a tiny component the system adds on demand; users do not see it.

### Safe mode

A try/catch wrapper around `setter(...)` and each callback invocation, compiled out entirely under the `PATWEEN_RELEASE` define (`#if`-style; verified by the release CI leg). Costs one try/catch per tween per frame when enabled. Default on in Editor, off in player builds. Toggleable per tween via `.SetSafeMode(bool)`; `SetCancelOnError(bool)` refines what happens on error.

Semantics (phase 1.13):

- **Setter exception** — the value write failed mid-step, so the animation contract is broken: the tween is killed and its slot freed. With `CancelOnError(true)` the kill is silent and fires `OnKill`; without it the exception is logged and the tween is disposed without `OnKill` (see the firing matrix in `docs/api/handles.md`). Other tweens in the same tick are unaffected. A `From`/`FromTo` snap that throws inside `.Start()` returns a dead handle.
- **Callback exception** — a user-code side effect: always logged, remaining callbacks in the same list still run. With `CancelOnError(true)` the tween is additionally cancelled (deferred — the error surfaces inside a callback scope) and `OnKill` fires. A sequence entry callback (`AppendCallback`/`AddPause`) with `CancelOnError(true)` kills the sequence mid-walk using the same unwind path as child auto-kill.
- **Off-thread assertions** (`TweenStore` access, runner ticks, `.Start()`) are part of the same debug layer and are compiled out under `PATWEEN_RELEASE`.

A sequence's flags cover its own callbacks and entry callbacks; children carry their own flags (set on the child builder before appending).

### Reverse and Yoyo

Every node carries a `_direction` flag. `Reverse()` flips the flag on the node it's called on, nothing else. The runner ticks each node by composing parent direction with own direction at evaluation time:

- **Leaf tween** (direct child of the hidden root): `Reverse()` flips its own `_direction`. The hidden root never reverses, so leaf reversal is local. The tween rewinds its own `localTime` toward `0`. For an **infinite-loop** leaf tween whose reversed `localTime` would go below `0`: wrap by adding one cycle slot so `localTime` lands inside the previous iteration. The tween continues playing backwards from there, preserving the oscillation.
- **Sequence**: `Reverse()` flips the sequence's `_direction`. Children continue to interpolate `start → end` in their own local time; what changes is the order in which the parent playhead reaches them. A reversed parent at local time `t` exposes its children at their normal forward progress within their own `[_start, _end]` windows.
- **Yoyo**: implemented as automatic `_direction` flip at each loop boundary on the node carrying the yoyo loop type. Independent from `Reverse()`. A yoyo-looped child inside a yoyo-looped parent composes per-node, not by direction multiplication.

One mechanism (`_direction` per node), applied at the node being reversed.
