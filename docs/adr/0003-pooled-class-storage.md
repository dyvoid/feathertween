# ADR 0003: Pooled-class internal storage in v1

## Status

Accepted

## Context

Tween engines store animation state. Options include: raw structs in a contiguous array (SoA), pooled classes, or a hybrid. We needed a v1 choice that supports the full feature set (M1-M4) without a storage refactor.

## Decision

Use pooled-class internal storage in v1. The public `Tween` / `TweenBuilder<T>` structs reference into the pool via `(bufferRef, generation)`. The backing class is pooled to reduce GC pressure.

This choice is explicitly swappable later. The public handle insulates users from the swap.

## Consequences

- **Positive**: Supports all planned features (nested sequences, labels, callbacks, late subscription) without a storage refactor.
- **Positive**: Leak detection is possible via finalizer on pooled instances.
- **Negative**: Higher per-tween memory overhead than a pure SoA array.
- **Mitigation**: SoA-friendly internal layout is preserved so a future Burst/Jobs path remains cheap to implement (M5+).
