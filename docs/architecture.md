## 4. Architecture

### 4.1 Layers

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

### 4.2 The parent-sequence model

Every animation (`Tween` or `Sequence`) has:

- `_parent` — owning sequence (or root)
- `_start`, `_end` — position in parent-local seconds
- `_timeScale` — local time multiplier
- `_paused`, `_reversed`
- `localTime` — current playhead in own duration (double precision)

The runner owns a hidden `_rootSequence` for each `UpdatePhase`. Top-level tweens are children of the root. When the runner ticks a root with `deltaTime`, the root advances its playhead and recursively renders children at their parent-local times. Recursive `timeScale`, `pause`, `reverse`, `seek` fall out for free.

Implementation note: the root is iterated by index over a flat list of active child ids for cache locality. Nested `Sequence` children with ordering use sorted child arrays.

### 4.3 Storage and handles

`TweenData` is the abstract base (holds `_parent`, `_start`, `_end`, `_timeScale`, `localTime`, `_paused`, `_reversed`, status, callbacks, target ref). The typed subclass `TweenData<T>` adds `start: T`, `end: T`, `getter`, `setter`, and the `IInterpolator<T>` used to lerp. The runner iterates `List<TweenData>` and calls a virtual `Step(double dt)` per child. This means one vtable dispatch per tween per frame in v1; an acceptable cost (~1-2 ns on modern CPUs). M5 SoA replaces this with per-`(TValue, TInterpolator)` storage and a `[BurstCompile]` job, see §10 M5.

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

### 4.4 Runner (PlayerLoop)

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

### 4.5 Update step (high level)

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

### 4.6 Ease system

```csharp
public enum EaseType
{
    Linear,
    InSine,    OutSine,    InOutSine,
    InQuad,    OutQuad,    InOutQuad,
    InCubic,   OutCubic,   InOutCubic,
    InQuart,   OutQuart,   InOutQuart,
    InQuint,   OutQuint,   InOutQuint,
    InExpo,    OutExpo,    InOutExpo,
    InCirc,    OutCirc,    InOutCirc,
    InBack,    OutBack,    InOutBack,
    InElastic, OutElastic, InOutElastic,
    InBounce,  OutBounce,  InOutBounce,
    BounceExact,   // amplitude in user units (meters/degrees)
    Curve,         // delegates to AnimationCurve slot
    Custom,        // delegates to EaseFunction slot
}

public readonly struct EaseRef
{
    public readonly EaseType type;
    public readonly float    paramA;   // overshoot, strength, amplitude
    public readonly float    paramB;   // period
    public readonly AnimationCurve curve;   // only when type == Curve
    public readonly EaseFunction   func;    // only when type == Custom
}

public static class Easing
{
    public static EaseRef Linear { get; }
    public static EaseRef OutCubic { get; }
    // ... all standard eases as cached EaseRef values

    public static EaseRef OutBack(float overshoot);
    public static EaseRef Bounce(float strength);
    public static EaseRef BounceExact(float amplitude);
    public static EaseRef Elastic(float strength, float period = 0.3f);
    public static EaseRef Curve(AnimationCurve curve);
    public static EaseRef Custom(EaseFunction func);
}

internal static readonly Func<float, float, float, float>[] _easeTable;
// signature: (t01, paramA, paramB) -> eased01
```

A static function table indexed by enum value. No virtual dispatch. `Curve` and `Custom` look at the `EaseRef.curve` / `EaseRef.func` slots. Standard eases are cached as static `EaseRef` instances; parametric variants allocate a struct on the stack.

### 4.7 From and FromTo

Snap timing matches anchor 13 / §3.5:

- **Root tween**: snap fires synchronously inside `.Start()`. The tween's own `SetDelay(...)` defers interpolation but not the snap.
- **Sequenced child** with parent-imposed offset `> 0`: snap is deferred. The child carries a `_snapPending` flag set at append time. The flag is consumed on the first parent tick where the playhead crosses `child._start` in the forward direction. Backward seek past `child._start` re-arms the flag (§3.15).
- `From`: at snap time, read current value via getter; that becomes `end`. The supplied argument is `start`. Invoke `setter(start)`.
- `FromTo`: arguments are `start` and `end` directly. Invoke `setter(start)` at snap time.

`invalidate()` (M4) re-arms the snap on a live handle.

### 4.8 Auto-kill and SetLink

- Per-frame, if `target is UnityEngine.Object o && o == null` → kill silently.
- `SetLink(GameObject, LinkBehavior)` (M2): `KillOnDestroy`, `KillOnDisable`, `PauseOnDisable`, `PauseOnDisableRestartOnEnable`. Implemented by a tiny component the system adds on demand; users do not see it.

### 4.9 Safe mode

A `[Conditional]`-style wrapper around `setter(...)` and each callback invocation. On exception: log, mark tween as dead, continue. Costs one try/catch per tween per frame when enabled. Default on in Editor, off in release. Toggleable per tween via `.SetSafeMode(bool)`.

### 4.10 Reverse and Yoyo

Every node carries a `_direction` flag. `Reverse()` flips the flag on the node it's called on, nothing else. The runner ticks each node by composing parent direction with own direction at evaluation time:

- **Leaf tween** (direct child of the hidden root): `Reverse()` flips its own `_direction`. The hidden root never reverses, so leaf reversal is local. The tween rewinds its own `localTime` toward `0`. For an **infinite-loop** leaf tween whose reversed `localTime` would go below `0`: wrap by adding one cycle slot so `localTime` lands inside the previous iteration. The tween continues playing backwards from there, preserving the oscillation.
- **Sequence**: `Reverse()` flips the sequence's `_direction`. Children continue to interpolate `start → end` in their own local time; what changes is the order in which the parent playhead reaches them. A reversed parent at local time `t` exposes its children at their normal forward progress within their own `[_start, _end]` windows.
- **Yoyo**: implemented as automatic `_direction` flip at each loop boundary on the node carrying the yoyo loop type. Independent from `Reverse()`. A yoyo-looped child inside a yoyo-looped parent composes per-node, not by direction multiplication.

One mechanism (`_direction` per node), applied at the node being reversed.

---

## 5. Sequence design

### 5.1 Primitives (M1 ships all of these)

All signatures accept builders, never started handles.

- `Append(TweenBuilder<T>)` — at end
- `Append(SequenceBuilder)` — nested
- `AppendInterval(seconds)`
- `AppendCallback(Action)`
- `Insert(float time, TweenBuilder<T>)` / `Insert(Position, TweenBuilder<T>)`
- `Join(TweenBuilder<T>)` / `Group(TweenBuilder<T>)` — parallel with most-recently-appended child: child `_start` = previous child's `_start` (same start time, not same end time). If the previous child was itself a `Join`'d child, both share the original appended child's `_start`. (aliases)
- `Chain(TweenBuilder<T>)` — alias of `Append`
- `Prepend(TweenBuilder<T>)` / `PrependInterval` / `PrependCallback`
- `AddLabel(string, float)` / `AddLabel(string, Position)`
- `AddPause(Position, Action? onPause = null)` — zero-duration child with `isPause` flag, halts playhead when reached
- `Clear(bool labels = false)`

### 5.2 Sequence invariants

- A `SequenceBuilder` accepts only unstarted builders. Passing the same builder to two `Append/Insert/Join` calls throws (builder was already consumed by the first).
- `Insert(Position.AtLabel("x"))` where `"x"` is not yet defined: deferred until `Start()`; if still undefined at start, throws.
- `Insert(time: t)` with `t < 0`: throws. The sequence's own delay is the only way to defer.
- `Insert(time: t)` with `t > current duration`: extends the sequence's duration to `t + child.duration`.
- `Start()` on an empty sequence: produces a zero-duration `Sequence` that completes on its first tick (callbacks fire normally).
- Mid-play insertion: a `Sequence` handle's `Insert(...)` (M2) accepts new children at any position. If the position is at or before the playhead, the child will not tick until `Restart` or `Seek` revisits that range.
- **Cascading auto-kill policy** (`SequenceCancelBehavior`, set via `SequenceBuilder.SetCancelBehavior`):
  - `ContinueOnChildAutoKill` (default): if a child auto-kills, the parent treats it as completed at the current parent-local time and continues. Other children are unaffected.
  - `KillSequenceOnChildAutoKill`: any child auto-kill propagates `Kill(false)` to the parent. The parent's `OnKill` fires; remaining children are killed in turn.
  - The parent does not auto-kill from its own target unless `SetTarget` was called and that target is destroyed.
- **Frozen child duration**: a child's duration (and ease, loops, delay) is captured at append time and frozen. Subsequent changes to the source builder (already invalidated) or to `SetDefaults` cascade do not retroactively update the sequence. Live duration recomputation is out of scope for v1.
- **Storage of heterogeneous children**: `Append/Insert/Join` consume the builder's backing record into a typed `TweenData<T>` slot in `TweenStore` and the sequence stores only the resulting `int` id. No boxing of generic structs; the sequence's child list is `int[]`.
- Sequence reverse and yoyo follow the rules in §4.10.

### 5.3 Nesting rules (M1)

- A sequence can contain tweens, sequences, callbacks, intervals, labels, pauses.
- Inserted children get `autoKill = false`, `delay` absorbed into insertion offset.
- Infinite-loop children (`SetLoops(-1)`) keep the `-1` sentinel internally; sequence duration math treats them as `∞`. The sequence's own duration is the max of finite children's `[_start, _end]` ranges; an infinite child does not extend the sequence's reported `Duration`. Inspector / serialization paths clamp to a UI-visible cap.
- A child can only have one parent (enforced by single-use builder).
- **Manual phase**: children inherit `UpdatePhase` from their parent sequence. A `Manual` sequence cannot contain `Update`-phase children, and vice versa. Phase mismatch at `Append/Insert` throws.

### 5.4 Defaults cascade

`SetDefaults(duration, ease, loops, ...)` on a `SequenceBuilder` pushes those values into every **subsequently** appended builder that has not explicitly overridden them. Captured at append time, then frozen on the child (see §5.2). Calling `SetDefaults` after `Append/Insert/Join` does **not** retroactively affect already-appended children.

### 5.4a Label resolution timing

- `SequenceBuilder.Insert(Position.AtLabel("x"))`: resolved at `.Start()`. Unresolved labels at start throw.
- `Sequence.AddLabel(name, time)` on the handle (post-start): defines a label visible only to **subsequent** handle-side `Insert` calls. Existing inserts are not retroactively rebound.

### 5.5 String position DSL (deferred to M4)

`Position.Parse("<+0.3")` etc. A single static method, ~30 lines, hooked into existing `Position` shape. Adding it later does not change any other code.
