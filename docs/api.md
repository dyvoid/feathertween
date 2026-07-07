## 3. Public API overview

### 3.1 Namespaces

- `PATween` — public API
- `PATween.Internal` — runtime guts
- `PATween.Editor` — drawers, debugger
- `PATween.UniTask` — optional integration (separate asmdef)

### 3.2 Builder and handle types

```csharp
// Builders (mutable structs, pooled backing buffer, single-use)
public struct TweenBuilder<T>
{
    // Configuration (chainable, return self)
    public TweenBuilder<T> SetEase(EaseRef e);
    public TweenBuilder<T> SetLoops(int count, LoopType type);
    public TweenBuilder<T> SetDelay(float seconds, DelayType type = DelayType.FirstLoop);
    public TweenBuilder<T> SetUpdate(UpdatePhase phase, bool ignoreTimeScale = false);
    public TweenBuilder<T> SetTarget(object target);
    public TweenBuilder<T> SetId(int id);
    public TweenBuilder<T> SetAutoKill(bool value);
    public TweenBuilder<T> SetRelative(bool value);
    public TweenBuilder<T> SetSafeMode(bool value);
    public TweenBuilder<T> SetCancelOnError(bool value);
    public TweenBuilder<T> From();
    public TweenBuilder<T> OnComplete(Action cb);
    public TweenBuilder<T> OnComplete<TS>(TS state, Action<TS> cb);
    // ... full callback set with plain and target-capture overloads

    // Terminators
    public Tween Start();
    public TweenAwaiter GetAwaiter();   // implicit Start on await
}

// Handle (immutable struct, generation-checked)
public readonly struct Tween : IEquatable<Tween>
{
    public TweenStatus Status { get; }
    public bool IsAlive { get; }
    public float Progress { get; }
    public float TotalProgress { get; }

    public void Play();    // unpause if paused; restart from 0 if completed; no-op if playing
    public void Pause();
    public void Resume();
    public void Reverse();
    public void Restart();
    public void Seek(float seconds, bool fireCallbacks = false);  // preserves play/pause state
    public void SetTimeScale(float scale);
    public void SetRemainingCycles(int cycles);
    public void SetRemainingCycles(bool stopAtEndValue);
    public void Kill(bool complete = false);
    public void Complete();

    // Late-subscription callbacks (completion-shaped only)
    public Tween OnComplete(Action cb);          // multicast (appends)
    public Tween OnKill(Action cb);              // multicast
    public Tween OnStepComplete(Action cb);      // multicast

    public TweenAwaiter GetAwaiter();
}

// Sequence handle parallels Tween
public readonly struct Sequence : IEquatable<Sequence>
{
    public TweenStatus Status { get; }
    public bool IsAlive { get; }
    public float Progress { get; }
    public float TotalProgress { get; }
    public float Duration { get; }

    public void Play();
    public void Pause();
    public void Resume();
    public void Reverse();
    public void Restart();
    public void Seek(float seconds, bool fireCallbacks = false);
    public void SetTimeScale(float scale);
    public void Kill(bool complete = false);
    public void Complete();

    // Mid-play modification (phase 1.10)
    public void Insert<T>(float time, TweenBuilder<T> child);
    public void Insert<T>(Position position, TweenBuilder<T> child);
    public void Insert(float time, SequenceBuilder child);
    public void AddLabel(string name, float time);

    public Sequence OnComplete(Action cb);       // multicast
    public Sequence OnKill(Action cb);           // multicast

    public TweenAwaiter GetAwaiter();
}

public struct SequenceBuilder
{
    public SequenceBuilder SetDefaults(/* ease, loops, delay — no duration, ADR 0010 */);
    public SequenceBuilder SetTarget(object target);   // bulk-kill scope: PATween.Kill(target) reaches the sequence
    public SequenceBuilder SetCancelBehavior(SequenceCancelBehavior b);
    public SequenceBuilder SetLoops(int count, LoopType type);  // 1.10: sequence-level looping
    // Append / Insert / Join / AddLabel / AddPause / Prepend* / Clear ...
    public Sequence Start();
}
```

Builders are single-use: after `.Start()` (or being claimed by a `SequenceBuilder.Append/Insert/Join`), the builder is invalidated and its pooled buffer returned. Handles are safe to copy, store, compare; operations on a stale handle no-op (or return defaults) instead of mutating a recycled tween.

### 3.3 Generic tween creation (lambda core)

```csharp
Tween t = PATween.To(
    () => obj.value,
    v  => obj.value = v,
    endValue: 10f,
    duration: 1f
)
.SetEase(Easing.OutCubic)
.SetLoops(2, LoopType.Yoyo)
.OnComplete(() => Debug.Log("done"))
.Start();

// Zero-alloc target-capture variant (anchor 15):
PATween.To(this, () => x.value, (s, v) => s.x.value = v, 10f, 1f)
    .OnComplete(this, s => s.HandleDone())
    .Start();

// Fire and forget:
PATween.Move(transform, target, 1f).SetEase(Easing.OutCubic).Start();
```

Supported value types in M1: `float`, `Vector2`, `Vector3`, `Vector4`, `Color`, `Quaternion`, `int` (snapping). Custom blittable types via `IInterpolator<T>` (anchor 16).

```csharp
public interface IInterpolator<T>
{
    T Lerp(T from, T to, float t);
    T Add(T a, T b);
    T Subtract(T a, T b);
}

// Register at startup:
PATween.RegisterInterpolator<Rect, RectInterpolator>();
```

### 3.4 Typed shortcuts (static methods on `PATween`)

```csharp
PATween.Move(transform, new Vector3(2,3,4), 1f).Start();
PATween.Rotate(transform, new Vector3(0,180,0), 1f).Start();
PATween.Fade(canvasGroup, 0f, 0.5f).Start();
PATween.Color(image, UnityEngine.Color.red, 0.3f).Start();
```

Each shortcut: builds a lambda pair internally, sets `target` automatically, returns a `TweenBuilder<T>`. `.Start()` produces the `Tween` handle. The optional `PATween.Extensions` asmdef (later milestone) provides `transform.PAMove(...)` wrappers.

### 3.5 From and FromTo

```csharp
PATween.Move(transform, target.position, 1f).From().Start();
PATween.From(() => x, v => x = v, startValue, 1f).Start();
PATween.FromTo(() => x, v => x = v, from, to, 1f).Start();
```

Snap timing (see anchor 13):

- **Root tween**: snap fires at `.Start()`, regardless of its own `SetDelay(...)`. The delay defers interpolation, not the snap. No first-frame pop. Matches DOTween.
- **Sequenced child** with parent-imposed offset `> 0`: snap is deferred until the parent playhead crosses `child._start`. The child's own `SetDelay` further offsets interpolation but not the snap. Matches GSAP.
- For `From`, the start value is read via the getter at snap time; the supplied argument is `end`. For `FromTo`, the supplied `from` is `start`. Both invoke `setter(start)` at snap time.

### 3.6 Sequences (M1 minimum, M2 expansion)

```csharp
SequenceBuilder sb = PATween.Sequence()
    .SetDefaults(ease: Easing.OutQuad);

sb.Append(PATween.Move(transform, p1, 0.5f));            // builder consumed by sequence
sb.Append(PATween.Rotate(transform, r1, 0.5f));
sb.AppendInterval(0.25f);
sb.Join(PATween.Fade(canvasGroup, 0f, 0.5f));            // parallel with previous
sb.Insert(0f, PATween.Color(image, UnityEngine.Color.red, 1f));
sb.AppendCallback(() => Debug.Log("step"));
sb.AddLabel("intro", 1.2f);
sb.Insert(Position.AtLabel("intro", +0.3f), nextTweenBuilder);

Sequence seq = sb.Start();
```

`SetDefaults` cascades ease/loops/delay to child tweens (no duration: creation methods require it explicitly, ADR 0010). `Append`/`Insert`/`Join` take a `TweenBuilder<T>` or `SequenceBuilder`, never a started `Tween`/`Sequence`.

### 3.7 Position type

```csharp
public readonly struct Position
{
    public static Position End { get; }
    public static Position AtTime(float seconds);
    public static Position AtLabel(string label, float offset = 0f);
    public static Position AfterPrevious(float offset = 0f);
    public static Position WithPrevious(float offset = 0f);
    // M4: public static Position Parse(string s);
}
```

`seq.Append(...)` is sugar for `seq.Insert(Position.End, ...)`.

### 3.8 Control surface (shared by Tween and Sequence handles)

See the `Tween` declaration in §3.2. All control methods are valid on a live handle; on a stale handle they no-op safely.

```csharp
public enum TweenStatus
{
    Delayed,      // in delay phase, no value updates yet
    Playing,      // actively interpolating
    Paused,       // mid-flight, paused
    Completed,    // reached end (if !autoKill, stays here)
    Cancelled,    // killed before completion
    Disposed,     // recycled, handle is stale
}
```

There is no `Scheduled` state. A tween only exists as a `Tween` handle after `.Start()`, at which point it is either `Delayed` or `Playing` on the next tick.

### 3.9 Builder settings (chainable, all return the builder)

```csharp
.SetEase(EaseRef)                            // produced by Easing.X(...) factories
.SetEase(AnimationCurve)                     // convenience: wraps Easing.Curve(c)
.SetLoops(count, LoopType)                   // Restart | Yoyo | Incremental | Rewind
.SetDelay(seconds, DelayType.FirstLoop | DelayType.EveryLoop)
.SetUpdate(UpdatePhase, ignoreTimeScale)    // Update | Late | Fixed | Manual
.SetAutoKill(bool)
.SetRelative(bool)
.SetTarget(object)                          // for typed shortcuts: overrides the kill-filter tag only, not the animation target
.SetId(int)                                 // small filter id
.SetLink(GameObject, LinkBehavior)          // M2: KillOnDestroy etc.
.SetSafeMode(bool)
.SetCancelOnError(bool)                     // kill silently on setter exception
```

The `Tween` handle has a parallel narrower setter surface for the small set of values that make sense post-start:

```csharp
t.SetTimeScale(float);
t.SetRemainingCycles(int cycles);             // gracefully stop infinite loops
t.SetRemainingCycles(bool stopAtEndValue);    // stop at next forward/backward boundary
```

Engine-wide time control (1.10) lives on the static class, applied at the hidden root sequences so it composes with per-tween `SetTimeScale`:

```csharp
PATween.SetGlobalTimeScale(float scale);              // all phases
PATween.SetTimeScale(UpdatePhase phase, float scale); // one phase (e.g. slow gameplay, keep UI)
```

Root scale is engine-side and distinct from Unity `Time.timeScale`; `ignoreTimeScale` opts a tween out of Unity's scale only (exact interaction finalized in 1.10).

### 3.10 Callbacks

Set on the builder: `OnStart`, `OnPlay`, `OnPause`, `OnUpdate`, `OnStepComplete`, `OnComplete`, `OnKill`, `OnRewind`. Each has a plain and a target-capture overload (anchor 15). Implemented as direct delegates, no params array (no allocation per call).

Late subscription on the `Tween`/`Sequence` handle is restricted to completion-shaped events: `OnComplete`, `OnKill`, `OnStepComplete`. Other callbacks (`OnUpdate`, `OnStart`, `OnPlay`, `OnPause`, `OnRewind`) must be set during build. Both builder-side and handle-side subscriptions for the completion-shaped events are **multicast**: each call appends to one invocation list per slot. Multiple builder-side `OnComplete` calls on the same builder append in call order; the second does not overwrite the first. Handlers fire in registration order. The list is allocated lazily on second subscription and pooled on tween recycle.

**Capture discipline**: target-capture overloads only avoid allocation when the lambda body does not capture any outer variables. If you reference `this`, a local, or any field outside the supplied state parameter, the C# compiler emits a closure-allocating delegate and the anchor-15 benefit is lost. Use `static` lambdas where possible; an optional Roslyn analyzer (M2) can enforce this.

**Timing details (as implemented in 1.9)**: `OnStart` and the initial `OnPlay` fire on the first tick that actually renders (after any delay, matching the no-first-frame-pop snap timing), once per playhead lifecycle; `Restart` re-arms them. Subsequent `Resume`/`Play` fire `OnPlay` each time. `OnUpdate(float)` receives the eased in-cycle progress on a tween (1f at every cycle end regardless of ease shape) and the normalized playhead (0..1) on a sequence. Target-capture overloads exist on `OnComplete`/`OnKill` only; all other slots take plain delegates (`OnUpdate` takes `Action<float>`).

### 3.11 Filters and bulk ops

```csharp
PATween.Kill(target);                    // by target (object)
PATween.Kill(id: 42);                    // by int id
PATween.KillAll();
PATween.PauseAll(); PATween.ResumeAll();
PATween.IsTweening(target);
```

### 3.12 Awaiter

```csharp
await PATween.Move(transform, p, 1f);                  // builder.GetAwaiter() starts implicitly
Tween t = PATween.Move(transform, p, 1f).Start();
await t;                                                // await an already-running handle
```

Both `TweenBuilder<T>` and `Tween` expose `GetAwaiter()` returning a `TweenAwaiter` struct (implements `INotifyCompletion`). The builder's awaiter calls `.Start()` internally before returning the handle's awaiter.

The awaiter resolves on **any** terminal status: `Completed`, `Cancelled`, or auto-killed (target destroyed). The continuation fires **exactly once** at the terminal transition. Within a single tick the firing order is fixed: `OnStepComplete` (per-loop boundary if applicable) → `OnComplete` (if completed normally) → `OnKill` (if killed, including auto-kill) → awaiter continuation. Users differentiate outcomes by checking `tween.Status` after `await`. The bare core awaiter does not throw on cancel; the UniTask asmdef adds opt-in `OperationCanceledException` semantics.

Allocation: the `TweenAwaiter` struct is alloc-free on the await side. Registering the continuation allocates one delegate per await (standard C# state machine behavior). No `TaskCompletionSource`.

### 3.13 `[Serializable] TweenSettings` (designer-facing)

```csharp
[Serializable]
public struct TweenSettings
{
    public float duration;
    public float delay;
    public EaseType ease;          // "Curve" entry selects the curve field
    public AnimationCurve curve;
    public float easeParamA;       // overshoot / strength
    public float easeParamB;       // period
    public int loops;
    public LoopType loopType;
    public UpdatePhase updatePhase;
    public bool ignoreTimeScale;
}

[Serializable]
public struct TweenSettings<T>
{
    public T startValue;
    public T endValue;
    public bool useStartValue;     // if false, animate from current
    public TweenSettings settings;

    public TweenSettings<T> WithDirection(bool toEndValue);
}

// Usage:
[SerializeField] TweenSettings<float> windowAnim;
public void SetOpen(bool open) =>
    PATween.AnchoredPosY(rect, windowAnim.WithDirection(toEndValue: open)).Start();
```

Custom drawer collapses common fields into a single line with a foldout for advanced. AnimationCurve field hidden unless `ease == Curve`. No reflection at runtime.

### 3.14 Lifecycle, mutation, and firing matrix

**State machine** (`TweenStatus` transitions; methods marked `*` are valid only in the listed source states, all other invocations no-op):

| From → To  | Trigger                                          |
|------------|--------------------------------------------------|
| Created → Delayed   | `.Start()` with non-zero delay         |
| Created → Playing   | `.Start()` with no delay               |
| Delayed → Playing   | delay elapses                          |
| Playing → Paused    | `Pause()`*                             |
| Paused  → Playing   | `Resume()` / `Play()`*                 |
| Playing → Completed | natural end (forward) reached, no remaining loops |
| Completed → Playing | `Restart()`* / `Play()` after complete (resets `_localTime` to 0, resumes playing) |
| Paused → Playing    | `Restart()`* — resets `_localTime` to 0 and begins playing (does not stay paused) |
| Delayed → Playing   | `Restart()`* — reapplies the full delay, then plays (delay is part of the tween) |
| any non-terminal → Cancelled | `Kill(false)`                 |
| any non-terminal → Completed | `Kill(true)` / `Complete()`   |
| Completed → Disposed | `Kill(any)` — tween is at its terminal value; `complete` arg is ignored |
| Completed or Cancelled → Disposed | `autoKill` triggers, or pool recycles |

Reverse, Seek, and SetTimeScale do not transition status; they mutate `_localTime` / direction / scale within the current status.

**Negative `SetTimeScale`**: rejected. Throws in safe mode, clamps to 0 in release. Direction is owned exclusively by `Reverse()`.

**Zero-duration tween**: completes on the first tick after `.Start()`. `OnStart`, `OnUpdate(1f)`, `OnComplete` fire in order. **Negative duration**: throws at `.Start()`.

**`Kill(complete: true)` on a sequence**: walks the playhead to the end, ticking children with callbacks firing normally, then disposes. `Kill(false)` disposes immediately; only `OnKill` fires (per child, in registration order).

**`Kill(complete: true)` on an infinite loop**: completes the current iteration (advances to the next loop boundary) then disposes. Callbacks fire as if the loop had ended naturally at that boundary.

**`Kill` on an already-`Completed` tween** (autoKill = false): transitions to `Disposed` regardless of the `complete` argument. The tween is already at its terminal value; no callbacks fire.

**OnComplete vs OnKill firing matrix**:

| Event                                  | OnStepComplete | OnComplete | OnKill |
|----------------------------------------|:--------------:|:----------:|:------:|
| Natural completion, autoKill on        | per loop end   | yes        | no     |
| Natural completion, autoKill off       | per loop end   | yes        | no     |
| `Complete()`                           | remaining loops| yes        | no     |
| `Kill(true)`                           | remaining loops| yes        | no     |
| `Kill(false)`                          | no             | no         | yes    |
| Auto-kill (`UnityEngine.Object` dies)  | no             | no         | yes    |
| Setter exception + `CancelOnError`     | no             | no         | yes    |

**Reentrancy / mutation during callbacks**: structural mutation invoked from inside a callback (`Kill`, `Complete`, sequence `Insert`, `AddLabel`, `Restart`, `Reverse` on another tween or self) is **deferred** to a per-tick command buffer drained at the end of the tick. Setter calls remain synchronous so values written in callbacks land in the same frame. Reading state (`Status`, `Progress`, etc.) inside a callback is allowed and reflects current state.

### 3.15 Seek traversal

`Seek(seconds, fireCallbacks = false)`:

- **`fireCallbacks = false` (default)**: jumps the playhead to the target time and renders a single sample at that position. Intermediate loops are not simulated. `AppendCallback` and `AddPause` between current and target time do not fire.
- **`fireCallbacks = true`**: walks loop boundaries between current and target time, in temporal order (forward seek issues `OnStepComplete` per crossed boundary; backward seek issues `OnRewind`). `AppendCallback` and `AddPause` are crossed in order; `AddPause` halts the seek at the pause and leaves the playhead there.
- **From-snap**: backward seek past `child._start` re-arms `_snapPending`, so a subsequent forward crossing snaps again.
- **Play/pause state**: `Seek` does not change `Status`. A paused tween stays paused after seeking.
- **Loop math**: target time is reduced modulo total duration when finite; clamped to `0` and `Duration` outside `[0, Duration]` for finite tweens, or wrapped for infinite loops.

### 3.16 Interpolator registration

```csharp
PATween.RegisterInterpolator<T, TInterp>() where TInterp : struct, IInterpolator<T>;
```

- Main-thread only; throws if called off-thread.
- Idempotent if the same `TInterp` is re-registered for the same `T`. Re-registering with a different `TInterp` while any tween of `T` is live throws.
- `TweenStore.Reset()` clears registrations; built-ins are re-registered automatically by the runner installer.
