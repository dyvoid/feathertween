# Sequence Design

## Primitives

M1 ships all of these.

All signatures accept builders, never started handles.

- `Append(TweenBuilder<T>)` — at end
- `Append(SequenceBuilder)` — nested
- `AppendInterval(seconds)`
- `AppendCallback(Action)`
- `Insert(float time, child)` / `Insert(Position, child)` — child is a `TweenBuilder<T>` or `SequenceBuilder`
- `Join(child)` — parallel with most-recently-appended child: child `_start` = previous child's `_start` (same start time, not same end time). If the previous child was itself a `Join`'d child, both share the original appended child's `_start`. Accepts `TweenBuilder<T>` or `SequenceBuilder`.
- `Prepend(child)` / `PrependInterval` / `PrependCallback` — `Prepend` accepts `TweenBuilder<T>` or `SequenceBuilder`
- `AddLabel(string, float)` / `AddLabel(string, Position)`
- `AddPause(float time | Position, Action? onPause = null)` — zero-duration child with `isPause` flag, halts playhead when reached
- `Clear(bool labels = false)`
- `SetLoops(int count, LoopType loopType)` — sequence-level looping (ships in phase 1.10 with the `AdvanceTo` boundary walk; loop wrap is a boundary crossing)

There are no alias names (former `Chain`/`Group` were removed in the ADR 0011 consistency pass): one operation, one name.

## Sequence invariants

- A `SequenceBuilder` accepts only unstarted builders. Passing the same builder to two `Append/Insert/Join` calls throws (builder was already consumed by the first).
- `Insert(Position.AtLabel("x"))` where `"x"` is not yet defined: deferred until `Start()`; if still undefined at start, throws.
- `Insert(time: t)` with `t < 0`: throws. The sequence's own delay is the only way to defer.
- `Insert(time: t)` with `t > current duration`: extends the sequence's duration to `t + child.duration`.
- `Start()` on an empty sequence: produces a zero-duration `Sequence` that completes on its first tick (callbacks fire normally).
- Mid-play insertion: a `Sequence` handle's `Insert(...)` accepts new children at any position. If the position is at or before the playhead, the child will not tick until `Restart` or `Seek` revisits that range.
- **Cascading auto-kill policy** (`SequenceCancelBehavior`, set via `SequenceBuilder.SetCancelBehavior`):
  - `ContinueOnChildAutoKill` (default): if a child auto-kills, the parent treats it as completed at the current parent-local time and continues. Other children are unaffected.
  - `KillSequenceOnChildAutoKill`: any child auto-kill propagates `Kill(false)` to the parent. The parent's `OnKill` fires; remaining children are killed in turn.
  - The parent does not auto-kill from its own target unless `SetTarget` was called and that target is destroyed.
- **Frozen child duration**: a child's duration (and ease, loops, delay) is captured at append time and frozen. Subsequent changes to the source builder (already invalidated) or to `SetDefaults` cascade do not retroactively update the sequence. Live duration recomputation is out of scope for v1.
- **Storage of heterogeneous children**: `Append/Insert/Join` consume the builder's backing record into a typed `TweenData<T>` slot in `TweenStore` and the sequence stores only the resulting `int` id. No boxing of generic structs; the sequence's child list is `int[]`.
- Sequence reverse and yoyo follow the rules in [overview.md](overview.md).

## Nesting rules

- A sequence can contain tweens, sequences, callbacks, intervals, labels, pauses.
- Inserted children get `autoKill = false`, `delay` absorbed into insertion offset.
- A finite looping child's window spans **all** of its cycles: `[_start, _start + delay + duration × loops]`. This holds for tween children and nested sequence children alike.
- Infinite-loop children (`SetLoops(-1)`) keep the `-1` sentinel internally; sequence duration math treats them as `∞`. The sequence's own duration is the max of finite children's `[_start, _end]` ranges; an infinite child does not extend the sequence's reported `Duration`. Inspector / serialization paths clamp to a UI-visible cap.
- A child can only have one parent (enforced by single-use builder).
- **Manual phase**: children inherit `UpdatePhase` from their parent sequence. A `Manual` sequence cannot contain `Update`-phase children, and vice versa. Phase mismatch at `Append/Insert` throws.

## Defaults cascade

`SetDefaults(ease, loops, delay)` on a `SequenceBuilder` (no duration — every creation method requires an explicit duration, see ADR 0010) pushes those values into every **subsequently** appended builder that has not explicitly overridden them. Captured at append time, then frozen on the child. Calling `SetDefaults` after `Append/Insert/Join` does **not** retroactively affect already-appended children.

## Label resolution timing

- `SequenceBuilder.Insert(Position.AtLabel("x"))`: resolved at `.Start()`. Unresolved labels at start throw.
- `Sequence.AddLabel(name, time)` on the handle (post-start): defines a label visible only to **subsequent** handle-side `Insert` calls. Existing inserts are not retroactively rebound.

## String position DSL

Deferred to M4: `Position.Parse("<+0.3")` etc. A single static method, ~30 lines, hooked into existing `Position` shape. Adding it later does not change any other code.
