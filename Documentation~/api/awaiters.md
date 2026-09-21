# Awaiters and `TweenSettings`

Awaiting an animation ships as of `0.2.0-dev`. `TweenSettings` (the second half of this page) is
still **planned** M2 surface and will not compile yet.

## `await` — no dependency on anything

```csharp
await FT.Move(transform, p, 1f);         // awaiting a builder starts it
await FT.Move(transform, p, 1f).Start(); // or await a running handle
await sequence;                          // sequences too
```

`await` binds to any type exposing `GetAwaiter()`, so FeatherTween satisfies it with its own
`TweenAwaiter` struct and references **neither UniTask nor `UnityEngine.Awaitable`**. The same code
compiles in a project that uses UniTask and one that does not ([ADR 0013](../adr/0013-awaitables-without-dependencies.md)).

| Member | On |
| ------ | -- |
| `GetAwaiter()` | `Tween`, `Sequence`, `TweenBuilder<T>`, `SequenceBuilder` |
| `WaitForCompletion()` → `Awaitable` | `Tween`, `Sequence` |
| `ToYieldInstruction()` → `CustomYieldInstruction` | `Tween`, `Sequence` |

### Semantics

- Resumes on **any** terminal status — `Completed`, `Cancelled`, or auto-killed. Read
  `tween.Status` after the await to tell them apart; the awaiter never throws on cancel.
- Resumes **exactly once**, which takes a guard: auto-kill fires `OnComplete` and then frees the
  record, and a `SetAutoKill(false)` tween that completes, is `Restart()`ed and completes again
  fires `OnComplete` twice. Resuming a state machine twice throws.
- Registration is on `OnComplete` plus an internal **disposal hook** fired by the store whenever a
  record is freed — not on `OnKill`. That matters: a safe-mode setter exception without
  `CancelOnError` (the Editor default pair) cancels and frees the tween *without* firing `OnKill`
  at all, and an awaiter hung off `OnKill` would park forever.
- A **dead handle resumes immediately** rather than parking forever.
- A **paused** tween does not resume — pausing is not finishing.
- The continuation runs **inside the tick**, with FeatherTween's callback scope open. Structural
  calls the resumed code makes — `Kill`, `Complete`, `Restart`, `Reverse` — are therefore deferred
  to the end of that tick, which surprises people who read `await` as "back on my own stack". For
  the same reason, `Restart(); await tween;` written inside a callback falls straight through: the
  restart has not been applied yet when the compiler tests `IsCompleted`.
- **Repeated awaits on a reusable tween accumulate.** There is no way to unregister a callback, so
  each `await` on a `SetAutoKill(false)` tween you restart in a loop leaves two spent entries behind
  forever. The continuation itself is released as soon as it fires, but the list is not. Awaiting a
  long-lived tween thousands of times will grow memory; awaiting one that auto-kills will not,
  because the record's lists are cleared on pool return.
- `TweenStore.Reset()` bumps every generation without firing anything, so an `await` outstanding
  across it parks. Only reachable with *Enter Play Mode Options → no domain reload*.
- On a kill path the continuation runs **strictly last**: `TweenStore.Free` fires `OnKill` (where
  that path fires it at all) and only then the disposal hook. On a natural completion it runs inside
  the `OnComplete` list, so it interleaves with other `OnComplete` callbacks in registration order.
- Awaiting a **builder starts it**, which also consumes it — an awaited builder cannot then be
  appended to a sequence.

### Allocation, honestly

The `TweenAwaiter` struct allocates nothing. The `await` around it does: the C# compiler emits an
async state machine per call, plus this package adds one `OneShotSignal` and two delegates to
register the continuation. Genuinely allocation-free `await` needs a pooled state machine via a
custom method builder — that is what UniTask does and what this does not. FeatherTween's
zero-allocation guarantee covers **steady-state ticking**, not the act of awaiting.

## Composing several animations

There is no `WhenAll` here, and it is mostly not missed: compose the animations into a `Sequence`
and await that. The result stays seekable, reversible and killable as one unit, which `WhenAll`
cannot give you.

```csharp
await FT.Sequence()
    .Append(FT.Move(a, p1, 1f))
    .Join(FT.Move(b, p2, 1f))
    .Start();
```

When you genuinely need task composition, `WaitForCompletion()` hands back a `UnityEngine.Awaitable`
that converts:

```csharp
await UniTask.WhenAll(
    a.WaitForCompletion().AsUniTask(),
    b.WaitForCompletion().AsUniTask());
```

Each call returns a fresh `Awaitable`. **Never await the same instance twice** — Unity pools them.

## Coroutines

```csharp
yield return tween.ToYieldInstruction();
```

Poll-based (`keepWaiting`), so it cannot strand a coroutine if the tween dies in a way no callback
covers. Not pooled: Unity holds a yield instruction across frames with no signal for when it is
done with it, so recycling one risks handing a live coroutine an instruction that now belongs to a
different tween. One allocation per coroutine wait; `await` is the lighter path.

## Not implemented yet

`WaitForKill`, `WaitForPosition` and `WaitForElapsedLoops` are still planned. Each needs engine
machinery that does not exist: a disposal hook that fires however a record dies, and a per-tick
registry of pending playhead waits. Built on the current callback set they would produce awaits that
hang — `WaitForKill` on an auto-killing tween never sees `OnKill`.

## UniTask integration (M3 candidate)

A separate `FeatherTween.UniTask` asmdef behind a `FEATHERTWEEN_UNITASK` define would add
cancellation-aware await semantics (`TweenCancelBehavior`: `Kill`, `Complete`, `Pause`,
`KillAndThrow`, `CompleteAndCancelAwait`). It ships only if a consumer needs more than `AsUniTask()`
on `WaitForCompletion()` already gives them. Core will not reference UniTask either way.

## `TweenSettings` (planned)

Designer-facing serializable structs for inspector workflows.

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
```

Usage:

```csharp
[SerializeField] TweenSettings<float> fadeAnim;
public void SetOpen(bool open) =>
    FT.Fade(canvasGroup, fadeAnim.WithDirection(toEndValue: open)).Start();
```

The custom drawer collapses common fields into a single line with a foldout for advanced options. The AnimationCurve field is hidden unless `ease == Curve`. No reflection at runtime.
