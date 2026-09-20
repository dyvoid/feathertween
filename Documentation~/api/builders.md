# Builders

`FT.X(...)` returns a builder, not a running tween. Builders are short-lived configuration structs with a pooled backing record. They are **single-use**: after `.Start()` or being claimed by a `SequenceBuilder.Append/Insert/Join`, every copy of the builder is invalidated.

## `TweenBuilder<T>`

```csharp
public struct TweenBuilder<T>
{
    // Terminator
    public Tween Start();
    // M2 (planned): TweenAwaiter GetAwaiter() — implicit Start on await
}
```

`TweenBuilder<T>` copies **alias** the same backing record: mutating one copy mutates all copies. After `.Start()`, every alias is invalid; further calls throw in Editor / safe mode and no-op in release. This surfaces a forgotten copy as a programming error early.

## `SequenceBuilder`

```csharp
public struct SequenceBuilder
{
    public SequenceBuilder SetDefaults(/* ease, loops, delay — no duration */);
    public SequenceBuilder SetTarget(object target);   // bulk-kill scope
    public SequenceBuilder SetLink(GameObject go, LinkBehavior b);  // lifetime link
    public SequenceBuilder SetCancelBehavior(SequenceCancelBehavior b);
    public SequenceBuilder SetLoops(int count, LoopType loopType);

    // Composition methods take (position, child); Append/Join/Prepend derive
    // the position from the previous child. Every child form accepts both
    // TweenBuilder<T> and SequenceBuilder.
    public SequenceBuilder Append(TweenBuilder<T> child);
    public SequenceBuilder Append(SequenceBuilder child);
    public SequenceBuilder AppendInterval(float seconds);
    public SequenceBuilder AppendCallback(Action callback);
    public SequenceBuilder Insert(float time, TweenBuilder<T> child);
    public SequenceBuilder Insert(Position position, TweenBuilder<T> child);
    public SequenceBuilder Insert(float time, SequenceBuilder child);
    public SequenceBuilder Insert(Position position, SequenceBuilder child);
    public SequenceBuilder Join(TweenBuilder<T> child);   // parallel with previous
    public SequenceBuilder Join(SequenceBuilder child);
    public SequenceBuilder Prepend(TweenBuilder<T> child);
    public SequenceBuilder Prepend(SequenceBuilder child);
    public SequenceBuilder PrependInterval(float seconds);
    public SequenceBuilder PrependCallback(Action callback);
    public SequenceBuilder AddLabel(string name, float time);
    public SequenceBuilder AddLabel(string name, Position position);
    public SequenceBuilder AddPause(float time, Action? onPause = null);
    public SequenceBuilder AddPause(Position position, Action? onPause = null);
    public SequenceBuilder Clear(bool labels = false);

    public Sequence Start();
}
```

`Append`/`Insert`/`Join`/`Prepend` take an **unstarted** `TweenBuilder<T>` or `SequenceBuilder`. Passing the same builder to two calls throws, because the first call consumes it.

**Known limitation — abandoned sequence builders.** Child tweens reserve their store slots the moment they're appended, not at `Start()`. If you build a sequence and never start it, those slots are never released — they stay occupied until the next `TweenStore.Reset()` (domain reload / play-mode change), reducing available tween capacity for the rest of the session. There's no immediate error; the leak detector logs a warning later, when the abandoned builder is garbage-collected. Rule: every composed sequence must end in `Start()` (to play it) or `Clear()` (to discard it and free its children).

## Chainable builder settings

All of these return the builder so they can be chained.

```csharp
.SetEase(EaseRef)                            // produced by Easing.X(...) factories
.SetEase(AnimationCurve)                     // convenience: wraps Easing.Curve(c)
.SetLoops(count, LoopType)                   // Restart | Yoyo | Incremental | Rewind; SetLoops(-1) with duration 0 throws at Start()
.SetDelay(seconds, DelayType.FirstLoop | DelayType.EveryLoop)  // negative throws
.SetUpdate(UpdatePhase, ignoreTimeScale)    // Update | Late | Fixed | Manual (drive with FT.ManualTick(dt))
.SetAutoKill(bool)
.SetRelative(bool)
.SetTarget(object)                          // kill-filter tag; not the animation target for generic tweens
.SetLink(GameObject, LinkBehavior)          // ties lifetime to a GameObject's active state
.SetSafeMode(bool)                          // try/catch around setter + callbacks; default on in Editor
.SetCancelOnError(bool)                     // setter exception: kill silently + OnKill; callback exception: log + cancel
```

## `SetLink` — surviving object pooling

`SetTarget` auto-kill only covers a *destroyed* `UnityEngine.Object`. Pooled objects are never
destroyed: they are deactivated, parked, and reactivated. `SetLink` ties the tween's lifetime to a
`GameObject`'s **active state** instead.

```csharp
FT.Move(enemy.transform, dest, 1f)
  .SetLink(enemy, LinkBehavior.PauseOnDisableResumeOnEnable)
  .Start();
```

| `LinkBehavior` | On disable | On re-enable |
| -------------- | ---------- | ------------ |
| `KillOnDestroy` (default) | — | — |
| `KillOnDisable` | kill (fires `OnKill`) | — |
| `PauseOnDisable` | pause (fires `OnPause`) | — |
| `PauseOnDisableResumeOnEnable` | pause | resume where it stopped (fires `OnPlay`) |
| `RestartOnEnable` | pause | replay from the beginning |

- **Every behavior kills on destruction.** A destroyed object leaves nothing to pause or restart.
- **Link state is seeded active**, so a tween started on an already-inactive object sees a disable
  edge on its first tick — `SetLink(go, KillOnDisable)` on a parked pooled object kills it.
- **The link only resumes what the link paused.** If you call `Pause()` yourself while the object is
  inactive, `PauseOnDisableResumeOnEnable` leaves it paused. `RestartOnEnable` is the exception: it
  replays on every enable edge, including for a completed tween kept alive with `SetAutoKill(false)`
  — that is the pooled-object case it exists for.
- **Links are root-level.** In the parent-sequence model a child is a pure function of the parent
  playhead, so it has no status of its own to pause or restart. Appending a linked builder into a
  sequence throws; link the sequence itself and the whole timeline moves as one.
- **Implementation**: the runner reads `activeInHierarchy` once per tick for each linked record. No
  component is attached to your objects; see [ADR 0012](../adr/0012-setlink-polling.md).

`SequenceBuilder.SetDefaults` cascades `ease`, `loops`, and `delay` into **subsequently** appended child builders that have not explicitly overridden them. Duration is not cascaded because every creation method requires an explicit duration.

## From / FromTo

```csharp
FT.Move(transform, dest, 1f).From().Start();          // swap: dest is the start, plays to current
FT.Move(transform, dest, 1f).From(spawnPos).Start();  // explicit start value on any builder
FT.From(() => x, v => x = v, from, 1f).Start();
FT.FromTo(v => x = v, from, to, 1f).Start();          // both endpoints explicit: no getter
```

- **Root tween**: the snap (capture start + invoke `setter(start)`) fires at `.Start()`, regardless of `SetDelay`. The delay only defers interpolation, not the snap.
- **Sequenced child** with parent-imposed offset `> 0`: snap is deferred until the parent playhead crosses `child._start`. The child's own `SetDelay` further offsets interpolation but not the snap.
- For `From()`, the creation method's end value becomes the start, and the tween plays to the value the getter reads at snap time. For `From(value)` and `FT.FromTo`, both endpoints are explicit — they are the same semantic in two spellings (one for typed shortcuts, one for raw setters), and the getter is never read.
- Explicit endpoints are captured when the creation call runs, not at snap time. `SetRelative` applies only to lazily-sampled tweens (`SnapMode.None`) and is ignored by `From`/`From(value)`/`FromTo`.
