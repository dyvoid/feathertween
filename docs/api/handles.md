# Handles and Control Surface

`.Start()` returns an immutable struct handle: `Tween` or `Sequence`. Handles carry an `id` and a `generation`. Operations on a stale handle (generation mismatch) no-op safely, so a forgotten handle never mutates a recycled tween.

## `Tween` handle

```csharp
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
```

For the full callback model, including builder-side callbacks and capture discipline, see [callbacks.md](callbacks.md).

## `Sequence` handle

```csharp
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

    // Mid-play modification
    public void Insert<T>(float time, TweenBuilder<T> child);
    public void Insert<T>(Position position, TweenBuilder<T> child);
    public void Insert(float time, SequenceBuilder child);
    public void AddLabel(string name, float time);

    public Sequence OnComplete(Action cb);       // multicast
    public Sequence OnKill(Action cb);           // multicast

    public TweenAwaiter GetAwaiter();
}
```

## Status enum

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

## Control surface notes

- `Play()` on a completed tween restarts from 0.
- `Seek` preserves the current `Status`; a paused tween stays paused.
- `SetTimeScale` rejects negative values: throws in safe mode, clamps to 0 in release. Direction is owned exclusively by `Reverse()`.
- Engine-wide time control lives on the static class:

```csharp
PATween.SetGlobalTimeScale(float scale);              // all phases
PATween.SetTimeScale(UpdatePhase phase, float scale); // one phase (e.g. slow gameplay, keep UI)
```

Root scale is engine-side and distinct from Unity `Time.timeScale`; `ignoreTimeScale` opts a tween out of Unity's scale only.

## Lifecycle and firing matrix

Valid status transitions:

| From → To  | Trigger                                          |
|------------|--------------------------------------------------|
| Created → Delayed   | `.Start()` with non-zero delay         |
| Created → Playing   | `.Start()` with no delay               |
| Delayed → Playing   | delay elapses                          |
| Playing → Paused    | `Pause()`                              |
| Paused  → Playing   | `Resume()` / `Play()`                  |
| Playing → Completed | natural end (forward) reached, no remaining loops |
| Completed → Playing | `Restart()` / `Play()` after complete (resets `_localTime` to 0, resumes playing) |
| Paused → Playing    | `Restart()` — resets `_localTime` to 0 and begins playing |
| Delayed → Playing   | `Restart()` — reapplies the full delay, then plays |
| any non-terminal → Cancelled | `Kill(false)`                 |
| any non-terminal → Completed | `Kill(true)` / `Complete()`   |
| Completed → Disposed | `Kill(any)` — tween is at its terminal value; `complete` arg is ignored |
| Completed or Cancelled → Disposed | `autoKill` triggers, or pool recycles |

`Reverse`, `Seek`, and `SetTimeScale` do not transition status; they mutate `_localTime` / direction / scale within the current status.

A zero-duration tween completes on the first tick after `.Start()`. `OnStart`, `OnUpdate(1f)`, `OnComplete` fire in order. A negative duration throws at `.Start()`.

`Kill(complete: true)` on a sequence walks the playhead to the end, ticking children with callbacks firing normally, then disposes. `Kill(false)` disposes immediately; only `OnKill` fires (per child, in registration order).

`Kill(complete: true)` on an infinite loop completes the current iteration (advances to the next loop boundary) then disposes. Callbacks fire as if the loop had ended naturally at that boundary.

`Kill` on an already-`Completed` tween (autoKill = false) transitions to `Disposed` regardless of the `complete` argument. The tween is already at its terminal value; no callbacks fire.

### OnComplete vs OnKill firing matrix

| Event                                  | OnStepComplete | OnComplete | OnKill |
|----------------------------------------|:--------------:|:----------:|:------:|
| Natural completion, autoKill on        | per loop end   | yes        | no     |
| Natural completion, autoKill off       | per loop end   | yes        | no     |
| `Complete()`                           | remaining loops| yes        | no     |
| `Kill(true)`                           | remaining loops| yes        | no     |
| `Kill(false)`                          | no             | no         | yes    |
| Auto-kill (`UnityEngine.Object` dies)  | no             | no         | yes    |
| Setter exception + `CancelOnError`     | no             | no         | yes    |
| Setter exception, safe mode, no `CancelOnError` (logged) | no | no    | no     |

### Reentrancy

Structural mutation invoked from inside a callback (`Kill`, `Complete`, sequence `Insert`, `AddLabel`, `Restart`, `Reverse` on another tween or self) is **deferred** to a per-tick command buffer drained at the end of the tick. Setter calls remain synchronous so values written in callbacks land in the same frame. Reading state (`Status`, `Progress`, etc.) inside a callback is allowed and reflects current state.

## Seek

`Seek(seconds, fireCallbacks = false)`:

- **`fireCallbacks = false` (default)**: jumps the playhead to the target time and renders a single sample at that position. Intermediate loops are not simulated. `AppendCallback` and `AddPause` between current and target time do not fire.
- **`fireCallbacks = true`**: walks loop boundaries between current and target time, in temporal order (forward seek issues `OnStepComplete` per crossed boundary; backward seek issues `OnRewind`). `AppendCallback` and `AddPause` are crossed in order; `AddPause` halts the seek at the pause and leaves the playhead there.
- **From-snap**: backward seek past `child._start` re-arms `_snapPending`, so a subsequent forward crossing snaps again.
- **Loop math**: target time is reduced modulo total duration when finite; clamped to `0` and `Duration` outside `[0, Duration]` for finite tweens, or wrapped for infinite loops.
