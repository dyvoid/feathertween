# Sequences

A sequence is a parent timeline that composes tweens, callbacks, intervals, labels, and pauses. Time scale, pause, reverse, and seek compose recursively with children.

## Building a sequence

```csharp
SequenceBuilder sb = FT.Sequence()
    .SetDefaults(ease: Easing.OutQuad());

sb.Append(FT.Move(transform, p1, 0.5f));            // builder consumed by sequence
sb.Append(FT.Rotate(transform, r1, 0.5f));
sb.AppendInterval(0.25f);
sb.Join(FT.Fade(canvasGroup, 0f, 0.5f));            // parallel with previous
sb.Insert(0f, FT.Color(image, UnityEngine.Color.red, 1f));
sb.AppendCallback(() => Debug.Log("step"));
sb.AddLabel("intro", 1.2f);
sb.Insert(Position.AtLabel("intro", +0.3f), nextTweenBuilder);

Sequence seq = sb.Start();
```

`Append`/`Insert`/`Join` accept unstarted `TweenBuilder<T>` or `SequenceBuilder` values. Passing a started handle throws.

## `Position` type

```csharp
public readonly struct Position
{
    public static Position End { get; }
    public static Position AtTime(float time);
    public static Position AtLabel(string label, float offset = 0f);
    public static Position AfterPrevious(float offset = 0f);
    public static Position WithPrevious(float offset = 0f);
    // M4: public static Position Parse(string s);
}
```

`seq.Append(...)` is sugar for `seq.Insert(Position.End, ...)`.

## Sequence invariants

- A `SequenceBuilder` accepts only unstarted builders. Passing the same builder to two `Append/Insert/Join` calls throws (builder already consumed).
- `Insert(Position.AtLabel("x"))` where `"x"` is not yet defined is deferred until `Start()`; unresolved labels throw at start.
- `AddLabel` resolves its position at **definition time** (only `Insert`/`AddPause` defer to `Start()`); a duplicate label name throws.
- `Insert(time: t)` with `t < 0` throws. The sequence's own delay is the only way to defer.
- `Insert(time: t)` with `t > current duration` extends the sequence's duration to `t + child.duration`.
- `Start()` on an empty sequence produces a zero-duration `Sequence` that completes on its first tick.
- Mid-play insertion: a `Sequence` handle's `Insert(...)` accepts new children at any position. If the position is at or before the playhead, the child will not tick until `Restart` or `Seek` revisits that range.
- **Cascading auto-kill policy** (`SequenceCancelBehavior`, set via `SequenceBuilder.SetCancelBehavior`):
  - `ContinueOnChildAutoKill` (default): if a child auto-kills, the parent treats it as completed at the current parent-local time and continues. Other children are unaffected.
  - `KillSequenceOnChildAutoKill`: any child auto-kill propagates `Kill(false)` to the parent.
- **Frozen child duration**: a child's duration, ease, loops, and delay are captured at append time and frozen. Subsequent changes to the source builder (already invalidated) or to `SetDefaults` cascade do not retroactively update the sequence.
- **Phase inheritance**: children inherit `UpdatePhase` from their parent sequence. A `Manual` sequence cannot contain `Update`-phase children, and vice versa. Phase mismatch at `Append/Insert` throws.

## Nesting

A sequence can contain tweens, other sequences, callbacks, intervals, labels, and pauses. Inserted children get `autoKill = false`; their delay is absorbed into the insertion offset. Infinite-loop children keep the `-1` sentinel internally; sequence duration math treats them as infinite.

## Reverse and yoyo

Sequence reverse and yoyo follow the rules in the parent-sequence model. See [handles.md](handles.md) for the control surface and [architecture/sequence.md](../architecture/sequence.md) for the internal design.
