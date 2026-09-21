# ADR 0013 — Awaitables depend on neither UniTask nor `Awaitable`

**Status**: Accepted
**Date**: 2026-09-21
**Milestone**: M2

## Context

`await tween` is the second half of M2's production-stickiness pair. Three ways to get it:

1. **Depend on UniTask.** The richest option: `WhenAll`/`WhenAny`/`WhenEach`, `DelayFrame`, fine
   `PlayerLoopTiming` control, a tracker window. Actively maintained.
2. **Return Unity 6's `Awaitable`.** Native, pooled, no third-party dependency. A deliberate subset
   of UniTask — no composition operators.
3. **Expose our own awaiter type.** `await` binds to any type with a `GetAwaiter()` returning
   `IsCompleted` / `OnCompleted` / `GetResult`, so a package can satisfy `await` with a struct of
   its own and depend on nothing.

The project owner's prior preference was UniTask, formed before `Awaitable` existed (Unity 2023.1).

## Decision

Option 3 for `await`, option 2 for composition.

- `Tween`, `Sequence`, `TweenBuilder<T>` and `SequenceBuilder` expose `GetAwaiter()` returning our
  own `TweenAwaiter` struct. `await tween` therefore works with no dependency at all.
- `WaitForCompletion()` returns `UnityEngine.Awaitable`, which composes with Unity's async APIs and
  converts to UniTask via `AsUniTask()` for consumers who want `WhenAll`.
- `ToYieldInstruction()` covers coroutines.
- Core references neither UniTask nor a UniTask asmdef. That stays an M3 candidate, only if a
  consumer actually asks.

## Rationale

- **A library dependency is a tax every consumer pays.** Someone who has never heard of UniTask
  should not have to install it to await a tween. This is the decisive argument, and it is about
  the app/library split rather than which library is better — a game can and should keep using
  UniTask regardless of what this package references.
- **UniTask's own maintainers say the same.** Cysharp recommend UniTask for application
  development, but for libraries minimizing dependencies they recommend `Awaitable` as a return
  type, noting it "can be converted to UniTask using `AsUniTask`, so there's no issue in handling
  Awaitable-based functionality within the UniTask library."
  ([discussion #627](https://github.com/Cysharp/UniTask/discussions/627))
- **The missing `WhenAll` matters less here than elsewhere.** FeatherTween's answer to "wait for
  several animations" is a `Sequence`: compose them on one timeline and await that, which is
  strictly better because the result stays seekable, reversible and killable as one unit.
  `WhenAll` is what you reach for when your async primitives do not compose; ours do.
- **AGENTS.md forbids adding third-party dependencies without explicit instruction**, and nothing
  here required one.

## Consequences

- `await tween` compiles identically in a UniTask project and a vanilla one.
- **The awaiter struct allocates nothing; the `await` does.** The C# compiler allocates an async
  state machine per call, as it does for every `await` in the language, plus one `OneShotSignal`
  and two delegates to register the continuation. Claiming "zero-alloc await" would require pooling
  the state machine through a custom method builder, which is what UniTask does and this does not.
  The zero-allocation guarantee covers steady-state ticking, not the act of awaiting.
- The awaiter resumes on **any** terminal status — completed, cancelled, auto-killed. Callers
  distinguish outcomes by reading `tween.Status` after the await. It does not throw on cancel.
- Registration is on `OnComplete` **plus an internal disposal hook** fired from `TweenStore.Free`,
  not on `OnKill`. `Free` is the one chokepoint every death route passes through, which is what
  makes the resume total. Hanging the awaiter off `OnKill` instead left `await` parked forever when
  a safe-mode setter threw without `CancelOnError` — that path cancels and frees without firing
  `OnKill` (the no/no/no row of the firing matrix), and it is the *default* Editor configuration.
  The disposal hook is the same machinery ADR-wise that `WaitForKill` was deferred for; building it
  here is what makes `await` correct, and it lowers the cost of the deferred three.
- `OneShotSignal` collapses the two paths that legitimately fire twice: auto-kill (`OnComplete`,
  then the free), and complete → `Restart()` → complete again on a non-auto-kill tween. Resuming a
  state machine twice throws.
- Awaiting a *builder* starts it. That is the ergonomic point of `await FT.Move(...)`, but it means
  an awaited builder cannot also be appended to a sequence.
- Continuations run inside the existing `OnComplete`/`OnKill` callback lists, so they fire in
  registration order among a tween's other callbacks rather than strictly after all of them.
- Not implemented here: `WaitForKill`, `WaitForPosition`, `WaitForElapsedLoops`. Each needs engine
  machinery that does not exist yet — a disposal hook that fires however a record dies, and a
  per-tick registry of pending playhead waits. Shipping them on top of the current callback set
  would produce awaits that hang (`WaitForKill` on an auto-killing tween never sees `OnKill`).
