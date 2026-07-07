# Performance Plan

How PATween stays allocation-free in the hot path and how performance is verified.

## Allocation budget

- **Tween creation**: 1 `TweenData<T>` from pool (no alloc after warmup) + 1 delegate pair (getter, setter) for generic form. Typed shortcuts may amortize delegates via cached statics where possible. `SequenceData` is not pooled (its entry array is unique per build).
- **Per-frame step**: 0 managed alloc.
- **Callback dispatch**: 0 alloc; single delegates, not delegate lists, not params arrays.
- **Awaiter**: `TweenAwaiter` struct is alloc-free on the await side. The continuation registration allocates one delegate per await (standard C# state machine behavior). No `TaskCompletionSource`.
- **`Kill(target)`** resolves through the target-indexed multimap: O(k) in the target's own tween count. `Free()` is an O(1) swap-remove from its active list via a slot→index map.

## SoA-readiness

Hot fields (`_start, _end, _localTime, _duration, _timeScale, _easeParamA, _easeParamB`) sit at the top of `TweenData` in a struct-of-floats region. The M5 SoA split into `NativeArray<TweenHot>` + managed sidecar does not break public API.

## Benchmark methodology

Implemented in the `Tests/Performance` EditMode asmdef. Two separate concerns:

### Allocation guards (hard fail)

Deterministic zero-managed-alloc assertions using `System.GC.GetAllocatedBytesForCurrentThread()` around a synchronous `PATweenRunner.ManualTick` loop. `ManualTick` avoids frame/thread noise, so the delta is exact and CI-safe.

- Steady-state tick of 1k tweens after warmup: delta must be exactly 0 bytes.
- Tick after a kill/realloc cycle: free-list reuse must not allocate.

### Throughput benchmarks (report only)

`Unity.PerformanceTesting` `Measure.Method` cases; numbers are machine-dependent and must never gate a build.

- Tick cost at 1k and 10k concurrent float tweens.
- `Start()` cost per tween.

Cross-engine comparison (DOTween, PrimeTween, LitMotion) is deferred until there is a competitive claim to make; the harness structure mirrors LitMotion's `Tests.Benchmark` so it can be added apples-to-apples later.

The `Unity.PerformanceTesting` package (`com.unity.test-framework.performance`) is a test-only dependency the consuming project must install.
