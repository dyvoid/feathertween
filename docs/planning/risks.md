# Risks and Open Questions

## Risks

- **Parent-timeline-everywhere overhead at the root**: every top-level tween pays for being a "child" of a hidden root. Mitigation: the root iterates a plain flat `List<int>` of active child ids, no per-child parent traversal at the root. Confidence 8/10 this is essentially free.
- **Generation-id check on every public op**: one extra branch per call. Negligible. Confidence 9/10.
- **Vtable dispatch per tween per frame**: each `TweenData<T>.Step()` is virtual in v1. ~1-2 ns per tween on modern CPUs, dominated by setter delegate invocation. Acceptable for M1–M4. M5 SoA path eliminates it.
- **Delegate alloc per generic tween**: unavoidable in v1 without source gen. One getter + one setter per tween. Acceptable for M1–M4.
- **Edit-mode runner**: PlayerLoop is play-mode only. Edit mode uses a parallel `EditorApplication.update` driver over the same store. Assembly reload triggers `TweenStore.Reset()`.
- **Safe mode in release**: people will leave it on and complain about cost, or turn it off and complain about silent failures. Default split (on in Editor, off in Release) is a reasonable compromise; document loudly.
- **Builder GC without consumption**: forgetting `.Start()` is a silent no-op at runtime. The pooled backing record's finalizer enqueues the leak id onto a lock-free `ConcurrentQueue<int>`; the next PlayerLoop tick drains the queue on the main thread and emits `Debug.LogWarning`. No Unity API calls from the finalizer thread. Best-effort Editor diagnostic only; the Roslyn analyzer (optional, M2) is the hard guarantee. Release builds skip both.

## Open questions

_All resolved (2026-07-16, user decisions; documented in 1.16 alongside the rest of the contract):_

1. ~~`globalTimeScale` knob?~~ Shipped in 1.10: `FT.GlobalTimeScale` (property since 1.15) / `SetTimeScale(phase, scale)` / per-handle `SetTimeScale`.
2. ~~Quaternion default?~~ **Decided**: shortest-path slerp (`Quaternion.SlerpUnclamped`) is the contract; euler overloads convert via `Quaternion.Euler`. A `RotateMode` alternative (e.g. beyond-360) is additive, M2+ if requested. Doc note for 1.16: shortest-path + yoyo/`Incremental` over rotations ≥180° takes the short way round, which can surprise.
3. ~~Per-tween manual ticking?~~ **Decided**: global `Manual` root only (`FT.ManualTick`); no `tween.Tick(dt)`. Adding it later would be additive.
4. ~~`Append(Action)` sugar?~~ **Decided**: no — `AppendCallback(Action)` stays the only spelling; the overload would ambiguate with `Append(Tween)`/`Append(SequenceBuilder)` composition. Recorded in `docs/guides/conventions.md` API shape rules.
