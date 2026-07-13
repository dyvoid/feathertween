# Influences

FeatherTween is a synthesis of ideas from several existing tween engines.

- **DOTween**: pooled storage, three update phases + manual, safe-mode try/catch around plugin steps, `SetLink`/`LinkBehaviour`, filter-by-target/id, sequence insertion quirks (autoKill=false, infinite-loop clamp).
- **GSAP**: universal parent-timeline data model with `_start`/`_end`/`_parent`/`_timeScale`, recursive composition of timeScale/pause/reverse/seek, position parameter semantics, `defaults` cascade, labels, `addPause`, `tweenTo(label)`, `recent()`, `invalidate()`.
- **PrimeTween**: struct handle with generation, target-capture zero-alloc overload pattern, `Easing.X(...)` factory pattern, `BounceExact(amplitude)` parametric ease, `TweenSettings<T>` + `WithDirection`, explicit pool sizing, `CycleMode.Rewind`, `SetRemainingCycles` graceful infinite-loop stop, static-method shortcuts.
- **LitMotion**: builder/handle split with explicit terminator, two-array SoA storage layout (unmanaged + managed sidecar), `[BurstCompile] IJobParallelFor` hot loop, `IMotionAdapter`-style extension hook, editor-mode dispatcher mirror, `DelayType.FirstLoop` vs `EveryLoop`, granular status enum, `CancelOnError` per-tween flag, N-state target-capture overloads, sequence-as-tween realization, opt-in debugger tracking.
