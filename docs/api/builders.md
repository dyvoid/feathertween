# Builders

`PATween.X(...)` returns a builder, not a running tween. Builders are short-lived configuration structs with a pooled backing record. They are **single-use**: after `.Start()` or being claimed by a `SequenceBuilder.Append/Insert/Join`, every copy of the builder is invalidated.

## `TweenBuilder<T>`

```csharp
public struct TweenBuilder<T>
{
    // Terminators
    public Tween Start();
    public TweenAwaiter GetAwaiter();   // implicit Start on await
}
```

`TweenBuilder<T>` copies **alias** the same backing record: mutating one copy mutates all copies. After `.Start()`, every alias is invalid; further calls throw in Editor / safe mode and no-op in release. This surfaces a forgotten copy as a programming error early.

## `SequenceBuilder`

```csharp
public struct SequenceBuilder
{
    public SequenceBuilder SetDefaults(/* ease, loops, delay — no duration */);
    public SequenceBuilder SetTarget(object target);   // bulk-kill scope
    public SequenceBuilder SetCancelBehavior(SequenceCancelBehavior b);
    public SequenceBuilder SetLoops(int count, LoopType type);

    public SequenceBuilder Append(TweenBuilder<T> child);
    public SequenceBuilder Append(SequenceBuilder child);
    public SequenceBuilder AppendInterval(float seconds);
    public SequenceBuilder AppendCallback(Action callback);
    public SequenceBuilder Insert(float time, TweenBuilder<T> child);
    public SequenceBuilder Insert(Position position, TweenBuilder<T> child);
    public SequenceBuilder Join(TweenBuilder<T> child);   // alias: parallel with previous
    public SequenceBuilder Group(TweenBuilder<T> child);  // alias: parallel with previous
    public SequenceBuilder Prepend(TweenBuilder<T> child);
    public SequenceBuilder PrependInterval(float seconds);
    public SequenceBuilder PrependCallback(Action callback);
    public SequenceBuilder AddLabel(string name, float time);
    public SequenceBuilder AddLabel(string name, Position position);
    public SequenceBuilder AddPause(Position position, Action? onPause = null);
    public SequenceBuilder Clear(bool labels = false);

    public Sequence Start();
}
```

`Append`/`Insert`/`Join` take an **unstarted** `TweenBuilder<T>` or `SequenceBuilder`. Passing the same builder to two calls throws, because the first call consumes it.

## Chainable builder settings

All of these return the builder so they can be chained.

```csharp
.SetEase(EaseRef)                            // produced by Easing.X(...) factories
.SetEase(AnimationCurve)                     // convenience: wraps Easing.Curve(c)
.SetLoops(count, LoopType)                   // Restart | Yoyo | Incremental | Rewind
.SetDelay(seconds, DelayType.FirstLoop | DelayType.EveryLoop)
.SetUpdate(UpdatePhase, ignoreTimeScale)    // Update | Late | Fixed | Manual
.SetAutoKill(bool)
.SetRelative(bool)
.SetTarget(object)                          // kill-filter tag; not the animation target for generic tweens
.SetId(int)                                 // small filter id
.SetLink(GameObject, LinkBehavior)          // M2: KillOnDestroy etc.
.SetSafeMode(bool)
.SetCancelOnError(bool)                     // kill silently on setter exception
```

`SequenceBuilder.SetDefaults` cascades `ease`, `loops`, and `delay` into **subsequently** appended child builders that have not explicitly overridden them. Duration is not cascaded because every creation method requires an explicit duration.

## From / FromTo

```csharp
PATween.Move(transform, target.position, 1f).From().Start();
PATween.From(() => x, v => x = v, startValue, 1f).Start();
PATween.FromTo(() => x, v => x = v, from, to, 1f).Start();
```

- **Root tween**: the snap (capture start + invoke `setter(start)`) fires at `.Start()`, regardless of `SetDelay`. The delay only defers interpolation, not the snap.
- **Sequenced child** with parent-imposed offset `> 0`: snap is deferred until the parent playhead crosses `child._start`. The child's own `SetDelay` further offsets interpolation but not the snap.
- For `From`, the start value is read via the getter at snap time; the supplied argument is the end value. For `FromTo`, the supplied `from` is the start value.
