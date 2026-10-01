# Architecture Overview

## What this is

FeatherTween is a Unity tween engine with a static-method API, struct handles, pooled internal storage, and a PlayerLoop-based runner. No MonoBehaviour.

## Shape

See the [design document](design.md) for goals, non-goals, and locked anchors.

---

## Architecture

### Layers

```
┌────────────────────────────────────────────────────────────┐
│ Public API (struct handles, builders, static shortcuts)    │
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

Every animation is a pure function of its own playhead, and every playhead is driven by a parent
([ADR 0006](../adr/0006-parent-sequence-model.md); its 2026-07-16 addendum describes the mechanism
as built, which is what this section summarizes):

- **Ownership is top-down.** A `SequenceData` owns an array of `SequenceChildEntry` records sorted by
  start time. The child's window (`Start`, `Length`, open-ended for infinite loops) lives on the
  entry, not on the child, and children carry no parent pointer.
- **Children are detached.** A child record occupies a store slot (so `FT.Kill(target)` reaches it)
  but is never on a phase's active list; only its parent's walk steps or seeks it.
- **The hidden root is per phase.** The runner owns one `RootSequenceData` per `UpdatePhase` and
  iterates a flat list of active root ids. Global and per-phase time scales are applied at the root,
  so a parent scales the delta it feeds its children instead of children consulting a parent.

Recursive `timeScale`, `pause`, `reverse` and `seek` fall out of that: a paused parent stops feeding
time, a reversed parent walks its children backward, and a seek repositions every child the walk
crosses.

### Storage and handles

`TweenData` is the base record: status, update phase, time scale, direction, target and link state, safe-mode flags, and the callback lists. The typed subclass `TweenData<T>` adds the getter/setter pair, start/end values, duration, the double-precision `localTime` playhead, ease, loop and delay settings, and the `IInterpolator<T>` used to lerp; `SequenceData` adds the child entry array and its own playhead. The runner iterates a `List<int>` of active ids per phase and calls a virtual `Step(scaledDelta, unscaledDelta)` on each record. This means one vtable dispatch per tween per frame in v1; an acceptable cost (~1-2 ns on modern CPUs). M5 SoA replaces this with per-`(TValue, TInterpolator)` storage and a `[BurstCompile]` job; see [performance.md](performance.md) and [phases.md](../planning/phases.md).

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

**Pool exhaustion**: when `_free` is empty and the pool is at capacity, `TweenStore` grows by doubling the backing arrays (same strategy as `List<T>`). This is an allocation, but it is bounded to startup / burst-creation periods. A `Debug.LogWarning` is emitted in Editor when growth occurs, so the developer can pre-size via `FT.SetCapacity` instead. There is no eviction and no hard ceiling in v1; refusing to create would be a silent correctness failure worse than the alloc.

The hot per-tween state is plain fields behind trivial accessors, which keeps the M5 SoA split a mechanical extraction. The fields are not yet grouped into one contiguous region; that is M5 work.

### Runner (PlayerLoop)

```csharp
// FeatherTweenRunner (TweenStore resets itself from its own SubsystemRegistration hook)
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void RuntimeBootstrap()
{
    Reset();     // fresh roots, scales and command queue for Fast Enter Play Mode
    Install();   // once per domain:
    // InsertAfter<Update.ScriptRunBehaviourUpdate>(..., TickUpdate)
    // InsertAfter<PreLateUpdate.ScriptRunBehaviourLateUpdate>(..., TickLate)
    // InsertAfter<FixedUpdate.ScriptRunBehaviourFixedUpdate>(..., TickFixed)
    // PlayerLoop.SetPlayerLoop(loop), warning if an anchor system is missing
}
```

- **Insertion position**: after the standard script update for each phase, so MonoBehaviour code sees pre-tween state in its own update and tweens drive end-of-phase values.
- **Time source** per phase: `Update` uses `Time.deltaTime` for time-scaled tweens, `Time.unscaledDeltaTime` for `ignoreTimeScale = true`; `LateUpdate` same; `FixedUpdate` uses `Time.fixedDeltaTime` (and `Time.fixedUnscaledDeltaTime` for unscaled). `Manual` uses caller-provided delta. Unity's built-in `maximumDeltaTime` clamp applies to `Time.deltaTime` automatically.
- **Thread safety**: everything is main-thread only. The store and the runner assert it in safe mode (compiled out under `FEATHERTWEEN_RELEASE`). Handle reads are not synchronized either: a read from another thread can observe a record mid-step. An awaiter continuation runs synchronously inside the tick that ends the animation, on the main thread ([awaiters.md](../api/awaiters.md)); awaiting from a background thread is unsupported, because registering the continuation mutates the record's callback lists.
- **Reentrancy**: a tick is not reentrant. Starting one while another is in progress (`FT.ManualTick` from a callback, setter or getter) throws rather than overwrite the shared snapshot and drain the outer tick's deferred commands early.
- **Editor**: a second hookup via `EditorApplication.update` ticks an editor-only runner.
- **Manual**: `FeatherTweenRunner.ManualTick(deltaTime)` advances only the `Manual` root. Destroyed-target cleanup is part of the tick: keep calling `ManualTick` or kill explicitly — tweens on destroyed targets in a stopped manual phase are not auto-killed.
- **Domain reload / Fast Enter Play Mode**: `TweenStore.Reset()` runs at `SubsystemRegistration` time. Editor uses `[InitializeOnLoad]` to also reset on assembly reload. Both cases drop all tweens cleanly so generation ids stay coherent.
- **Debug visibility**: the M3 editor preview window reads active tweens directly from `TweenStore`. No scene-side proxy needed.

### Update step (high level)

For each active root:

```text
1. dt = Unity delta (or caller-provided) * global scale * phase scale
2. root.localTime += dt                              // double accumulator, diagnostics only
3. snapshot the phase's active ids + generations; for each still-alive record:
   3a. if its UnityEngine.Object target was destroyed: OnKill (unless already
       Completed), queue free, continue
   3b. if it has a SetLink: poll activeInHierarchy (may pause, resume, restart,
       or queue a kill); runs before the status gate so a link-paused record resumes
   3c. skip unless Playing or Delayed
   3d. record.Step(dt): advance localTime * own timeScale * direction, find the
       cycle, ease, setter(Lerp(...)) (safe-mode wrapped), OnUpdate,
       OnStepComplete/OnComplete as crossed; a sequence walks its children
   3e. if Completed && autoKill: queue free
4. free queued records (generation-checked)
5. drain deferred commands (callbacks' Kill/Complete/Restart/Reverse/Insert)
6. recycle freed records into their pools
```

**Time precision**: internal time accumulators (`localTime`, root playhead) are `double` to bound drift over long sessions, repeated seeks, and nested timescales. Interpolation output is `float`. The eased `t` is cast to `float` immediately before the setter call.

**Auto-kill flag**: `TweenData` caches `IsUnityObject` (bool) at creation time so the hot loop is a flag check + cached `targetRef == null` (Unity's overloaded `==`), not a runtime type test per tween per frame.

**Burst note (M5)**: Unity's overloaded `==` requires the main thread, so the auto-kill scan stays in the managed sidecar even when M5 moves the math to a Burst job. The Burst job operates on `TweenDataHot<T>` only and writes outputs; the managed pass that runs immediately after handles auto-kill, callback dispatch, and deferred-mutation drain.

### Ease system

The ease system is documented in the API reference: [api/easings.md](../api/easings.md). Internally, `EaseEval.Evaluate` is one `switch` over `EaseType`. Every `Easing.X()` factory constructs an `EaseRef` struct on the stack (there are no cached instances, and none are needed: construction allocates nothing). `Curve` and `Custom` read their `AnimationCurve` / delegate slots.

### From and FromTo

Snap timing matches the design anchor:

- **Root tween**: snap fires synchronously inside `.Start()`. The tween's own `SetDelay(...)` defers interpolation but not the snap.
- **Sequenced child** with parent-imposed offset `> 0`: snap is deferred. The child carries a `snapPending` flag set at build time. The flag is consumed on the first parent tick where the playhead crosses the child entry's `Start` in the forward direction. A backward walk or seek past that start re-arms the flag.
- `From`: at snap time, read current value via getter; that becomes `end`. The supplied argument is `start`. Invoke `setter(start)`.
- `FromTo` (and builder `From(value)`): both endpoints are supplied directly, so there is no getter; `FT.FromTo` takes only the setter (ADR 0011). Invoke `setter(start)` at snap time.

`invalidate()` (M4) re-arms the snap on a live handle.

### Auto-kill and SetLink

- Per-frame, if `target is UnityEngine.Object o && o == null` → kill, firing `OnKill`. A record that already completed (`SetAutoKill(false)`) disposes without callbacks instead, per the firing matrix in [handles.md](../api/handles.md).
- `SetLink(GameObject, LinkBehavior)`: `KillOnDestroy` (default), `KillOnDisable`, `PauseOnDisable`, `PauseOnDisableResumeOnEnable`, `RestartOnEnable`. The runner reads `activeInHierarchy` once per tick for each linked record — no component is attached to the user's objects (ADR 0012). The poll runs *before* the status gate, so a link-paused record is still evaluated and can resume. Links are root-level: appending a linked builder into a sequence throws.

### Disposal hook

`TweenStore.Free` fires a per-record disposal callback list after `OnFree()`, as the last thing it
does. `Free` is all but the only route by which a record can die — `Kill`, `Complete`,
auto-kill, destroyed target, `SetLink` kill, safe-mode error cancel, sequence cascade — which makes
it the one place a subscriber can be sure of hearing about a death however it happened. Two
exceptions, neither of which fires the hook: a safe-mode snap failure in `TweenBuilder.Start`
returns its record to the pool before it ever reaches a slot (so no handle exists to subscribe),
and `TweenStore.Reset` clears and bumps every slot wholesale without running `OnFree` or the hook —
which is why an `await` outstanding across a domain-reload-free play-mode transition parks. Anything
building on this hook needs to treat `Reset` as its own case. `await` is built on it
(ADR 0013): the user-facing `OnKill` does **not** fire on every death, notably not on a safe-mode
setter exception without `CancelOnError`, so an awaiter hung off `OnKill` parks forever. Invoked
inside a `TweenCommandQueue` callback scope like every other callback list, so structural calls made
from a resumed `await` defer to end of tick. This is also the machinery the deferred `WaitForKill` /
`WaitForPosition` work is expected to build on.

### Safe mode

A try/catch wrapper around `setter(...)` and each callback invocation, compiled out entirely under the `FEATHERTWEEN_RELEASE` define (`#if`-style; verified by the release CI leg). Costs one try/catch per tween per frame when enabled. Default on in Editor, off in player builds. Toggleable per tween via `.SetSafeMode(bool)`; `SetCancelOnError(bool)` refines what happens on error.

Semantics (phase 1.13):

- **Setter exception** — the value write failed mid-step, so the animation contract is broken: the tween is killed and its slot freed. With `CancelOnError(true)` the kill is silent and fires `OnKill`; without it the exception is logged and the tween is disposed without `OnKill` (see the firing matrix in `Documentation~/api/handles.md`). Other tweens in the same tick are unaffected. A `From`/`FromTo` snap that throws inside `.Start()` returns a dead handle.
- **Callback exception** — a user-code side effect: always logged, remaining callbacks in the same list still run. With `CancelOnError(true)` the tween is additionally cancelled (deferred — the error surfaces inside a callback scope) and `OnKill` fires. A sequence entry callback (`AppendCallback`/`AddPause`) with `CancelOnError(true)` kills the sequence mid-walk using the same unwind path as child auto-kill.
- **Off-thread assertions** (`TweenStore` access, runner ticks, `.Start()`) are part of the same debug layer and are compiled out under `FEATHERTWEEN_RELEASE`.

A sequence's flags cover its own callbacks and entry callbacks; children carry their own flags (set on the child builder before appending).

### Reverse and Yoyo

Every record carries a `Direction` (+1/-1). `Reverse()` flips it on the record it is called on, nothing else:

- **Leaf tween** (direct child of the hidden root): `Reverse()` flips its own `Direction`. The hidden root never reverses, so leaf reversal is local. The tween rewinds its own `localTime` toward `0`. For an **infinite-loop** leaf tween whose reversed `localTime` would go below `0`: wrap by adding one cycle slot so `localTime` lands inside the previous iteration. The tween continues playing backwards from there, preserving the oscillation.
- **Sequence**: `Reverse()` flips the sequence's `Direction`. Children continue to interpolate `start → end` in their own local time; what changes is the order in which the parent playhead reaches them. A reversed parent at local time `t` exposes its children at their normal forward progress within their own entry windows.
- **Yoyo** is not a direction flip. A tween maps odd cycles to `end → start` with the same ease applied forward; a sequence maps odd cycles to a backward walk of its local timeline. Both are functions of the cycle index, independent from `Reverse()`, so a yoyo child inside a yoyo parent composes per record, not by direction multiplication.

`Direction` is the only reversal state, and it lives on the record being reversed.
