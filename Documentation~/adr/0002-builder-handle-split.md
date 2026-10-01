# ADR 0002: Builder/handle split with explicit `.Start()`

## Status

Accepted

## Context

Tween libraries typically return a handle immediately. We needed to decide when mutation stops and control begins, and how to surface misuse.

## Decision

Split the lifecycle into two value types:

1. **`TweenBuilder<T>`** (struct): Returned by `FT.X(...)`. Thin `(bufferRef, generation)` value; configuration record is a pooled class held by reference. Copies of the struct alias the same backing record. Mutating one mutates all. After `.Start()` or sequence consumption, every alias is invalid; further calls throw in Editor / safe mode and no-op in release.
2. **`Tween`** (struct): Returned by `.Start()`. Immutable `(id, generation)` handle. Exposes only control methods (`Pause`, `Resume`, `Kill`, etc.) plus a small set of late-subscription callbacks (`OnComplete`, `OnKill`, `OnStepComplete`, multicast).

Same split applies to `SequenceBuilder` and `Sequence`.

The pooled backing class has a finalizer that enqueues a leak-detection id onto a lock-free queue; the runner drains the queue on the main thread and emits an Editor `Debug.LogWarning`.

## Consequences

- **Positive**: Builders are short-lived; a forgotten copy is a programming error worth surfacing loudly. Handles are long-lived; a stale handle is a normal lifecycle event worth handling silently.
- **Positive**: Public handles insulate users from internal storage changes.
- **Negative**: Two concepts to learn instead of one.
- **Mitigation**: The API surface is small; the split maps naturally to "configure" vs. "control" phases.

## Amendment (2026-10-01): use-after-consume throws in every build

The decision above says a consumed builder throws "in Editor / safe mode and no-op in release".
The no-op half was never built: since v0.1.0, `TweenBuilder<T>` and `SequenceBuilder` throw
`InvalidOperationException` in every build, `FEATHERTWEEN_RELEASE` included, and the tests pin
it. That is kept as the decision rather than implemented away, for three reasons:

- Builder validation is not part of the safe-mode debug layer that `FEATHERTWEEN_RELEASE`
  compiles out (setter/callback try/catch, off-thread asserts). It is one generation compare per
  builder call, off the per-frame path.
- A silent no-op hides exactly the forgotten-copy bug the Consequences section wants surfaced
  loudly, in the build where it is hardest to diagnose.
- ADR 0011 set throw-on-invalid as the API's direction; a release-only no-op would be the one
  exception.
