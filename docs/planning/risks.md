# Risks and Open Questions

## Risks

- **Parent-timeline-everywhere overhead at the root**: every top-level tween pays for being a "child" of a hidden root. Mitigation: the root iterates a plain flat `List<int>` of active child ids, no per-child parent traversal at the root. Confidence 8/10 this is essentially free.
- **Generation-id check on every public op**: one extra branch per call. Negligible. Confidence 9/10.
- **Vtable dispatch per tween per frame**: each `TweenData<T>.Step()` is virtual in v1. ~1-2 ns per tween on modern CPUs, dominated by setter delegate invocation. Acceptable for M1–M4. M5 SoA path eliminates it.
- **Delegate alloc per generic tween**: unavoidable in v1 without source gen. One getter + one setter per tween. Acceptable for M1–M4.
- **Edit-mode runner**: PlayerLoop is play-mode only. Edit mode uses a parallel `EditorApplication.update` driver over the same store. Assembly reload triggers `TweenStore.Reset()`.
- **Safe mode in release**: people will leave it on and complain about cost, or turn it off and complain about silent failures. Default split (on in Editor, off in Release) is a reasonable compromise; document loudly.
- **Builder GC without consumption**: forgetting `.Start()` is a silent no-op at runtime. The pooled backing record's finalizer enqueues the leak id onto a lock-free `ConcurrentQueue<int>`; the next PlayerLoop tick drains the queue on the main thread and emits `Debug.LogWarning`. No Unity API calls from the finalizer thread. Best-effort Editor diagnostic only; the Roslyn analyzer (optional, M2) is the hard guarantee. Release builds skip both.

## Open questions (not blocking M1)

1. `globalTimeScale` knob on `FeatherTween` (slow-mo everything)? Trivial to add; deferred until requested.
2. Quaternion tweens: shortest-path vs. euler-additive? DOTween offers `RotateMode.Fast`, `FastBeyond360`, etc. Pick a sane default and a single alternative for M2.
3. Should `Tween.SetUpdate(Manual)` allow per-tween manual ticking (`tween.Tick(dt)`) or only via the global `Manual` root? Lean toward the latter for API minimalism.
4. Should `Append(Action)` exist as sugar for `AppendCallback(Action)`? Cheap convenience, possible ambiguity with `Append(Tween)`. Defer.
