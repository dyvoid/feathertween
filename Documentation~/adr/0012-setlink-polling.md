# ADR 0012 — `SetLink` polls, it does not attach a component

**Status**: Accepted
**Date**: 2026-09-20
**Milestone**: M2

## Context

`SetTarget` auto-kill only fires when a `UnityEngine.Object` is *destroyed*. Object pooling never
destroys anything: a pooled object is deactivated, parked, and reactivated later. A tween that
survives that round trip keeps writing to an object the game considers retired, and on reactivation
the object shows a half-finished animation. This is the most-reported footgun class in every tween
library that lacks a lifetime link, which is why `SetLink(GameObject, LinkBehavior)` was planned as
the first M2 item.

The behaviors need to observe *activation edges*, and Unity surfaces those through `OnEnable` /
`OnDisable` on a component. Two ways to get them:

1. **Attach a helper `MonoBehaviour`** to the linked `GameObject` on first link (DOTween's approach).
   Edges arrive as callbacks; no per-frame work.
2. **Poll `activeInHierarchy`** once per tick for records that carry a link.

## Decision

Poll. A linked record stores the `GameObject`, the `LinkBehavior`, the last observed
`activeInHierarchy`, and whether the link is what paused it. `FeatherTweenRunner.TickActiveCore`
evaluates the link before the status gate, so a link-paused tween is still polled and can resume.

## Rationale

- **Attaching a component mutates the user's scene.** It changes what a prefab instance looks like
  at runtime, what `GetComponents` returns, and what a pooling system copies or resets. For a package
  whose pitch is that it never touches the scene graph — invariant 3, the `MonoBehaviour`-free
  runner, ADR 0004 — silently adding components to arbitrary objects is the wrong trade.
- **The cost is bounded and pay-per-use — measured, not assumed.** One `activeInHierarchy` read per
  tick per *linked* record. Unlinked tweens, the overwhelming majority, pay nothing but a bool test.
  The read allocates nothing, so the zero-alloc steady-state budget holds.

  `Throughput_Tick1k/10kLinkedTweens` against their unlinked baselines, measured 2026-09-20 in Unity
  on the author's machine:

  | Tick | Unlinked | Linked | Per poll |
  | ---- | -------- | ------ | -------- |
  | 1k records | 0.08 ms | 0.11 ms | ~30 ns |
  | 10k records | 0.81 ms | 1.07 ms | ~26 ns |

  Two scales agreeing puts the poll at **~26-30 ns**. A thousand linked tweens cost 0.03 ms/frame, or
  0.2% of a 60fps budget. The figure also settles a question the Unity docs do not answer outright:
  at 26 ns this is a cached flag read, not a parent-chain walk — Unity pays the walk inside
  `SetActive`, which is what propagates the state downward. Caveat: the benchmark objects are
  parentless, so a deep hierarchy is untested; if a profile ever shows link polling scaling with
  hierarchy depth, that assumption is the thing to re-check.
- **One code path, no lifetime puzzle.** A helper component has to answer: what if two tweens link
  the same object, what if the object is destroyed while the component still holds handles, what if
  the user deletes the component. Polling has no state on the object at all.
- **Edges are detected on the tick, not the frame — and this is a real, not a theoretical, loss.**
  A disable-then-enable between two ticks is invisible to polling, where `OnDisable`/`OnEnable` would
  see both. `PauseOnDisable` and `PauseOnDisableResumeOnEnable` survive it, because a net-unchanged
  active state means a net-unchanged pause state. `KillOnDisable` and `RestartOnEnable` do not: they
  are genuinely edge-triggered, and the pattern that defeats them is the ordinary one —

  ```csharp
  pool.Release(enemy);   // SetActive(false)
  pool.Get();            // same instance, SetActive(true) — same frame
  ```

  the poll sees `true` both times, so the retired entity's tween keeps running on the reused one.
  Confirmed in the editor 2026-09-20: a scripted same-frame `SetActive(false)`/`SetActive(true)` on a
  linked object produces no observable effect at all — the tween does not pause, flicker or restart.
  That is the exact bug `SetLink` exists to prevent. It is worse on `UpdatePhase.Fixed` (a frame may
  contain no FixedUpdate) and on `Manual` (the poll only happens when the user calls `ManualTick`).
  A pool that recycles an instance within a single frame must still `FT.Kill(target)` on release.
  A helper component would catch this case; that is the strongest argument against this ADR, and it
  is the reason to revisit it if the explicit-kill workaround turns out not to be good enough.

## Consequences

- Link state is seeded as *active*, so a tween started on an already-inactive object sees a disable
  edge on its first tick. `SetLink(go, KillOnDisable)` on a parked pooled object therefore kills it,
  which is what the caller meant.
- Every behavior kills on destruction; a destroyed object leaves nothing to pause or restart.
- Links are a **root-level** concern. In the parent-sequence model (ADR 0006) a child is a pure
  function of the parent playhead and has no independent status to pause, resume or restart, so
  appending a linked builder into a sequence throws rather than silently dropping the link. Link the
  sequence instead.
- A `MonoBehaviour`-based fast path stays available later if profiling ever shows the poll matters at
  a scale we have not hit — it would be an internal change behind the same public API.
