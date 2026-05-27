# PATween — Design Proposal

A robust, minimal C# tween engine for Unity. Compositional sequences, static-method API, struct handles, PlayerLoop runner.

---

## 1. Goals and non-goals

### Goals

- **GSAP-style composition**: every animation lives on a parent timeline; timelines nest; `timeScale`, `pause`, `reverse`, `seek` compose recursively.
- **DOTween-style Unity ergonomics**: typed shortcuts for common Unity components, lambda getter/setter generic core, AnimationCurve eases, three Unity update phases.
- **Robust by default**: auto-kill on destroyed Unity targets, safe mode try/catch, no use-after-free via struct handles with a generation id.
- **Minimal surface**: a small public API, one runner, one ease system, one position model. No feature bloat without a concrete user need.
- **No tech debt path**: M1 architecture supports everything we plan up to M4 without refactor.

### Non-goals (for v1.0)

- Path tweens, Bezier/Catmull-Rom utilities (M3+).
- Burst/Jobs hot loop (keep door open via SoA layout, ship in M5 if ever).
- Source-generator zero-allocation (M5+).
- Editor preview/debugger window (M4+).
- GSAP plugins (MorphSVG, Flip, ScrollTrigger). Out of scope, web concerns.
- The full GSAP position string DSL in v1.0 (typed `Position` first, parser later).

---

## 2. Design principles (locked anchors)

1. **Static-method API**: typed shortcuts live as static methods on `PATween` (`PATween.Move(transform, ...)`), not as extension methods on Unity types. Avoids namespace pollution on `Transform`/`CanvasGroup`/etc. and gives one discoverable entry point. A small optional `PATween.Extensions` asmdef in a later milestone can re-add DOTween-style extension wrappers for users who prefer them.
2. **Builder/handle split with explicit `.Start()`.** `PATween.X(...)` returns a `TweenBuilder<T>` struct. The struct itself is a thin `(bufferRef, generation)` value; the configuration record is a pooled class held by reference. Copies of the struct **alias** the same backing record (mutating one mutates all). After `.Start()` or sequence consumption, every alias is invalid; further calls throw in Editor / safe mode and no-op in release. Rationale: builders are short-lived, and a forgotten copy is a programming error worth surfacing loudly; handles are long-lived, and a stale handle is a normal lifecycle event worth handling silently. `.Start()` returns an immutable `Tween` handle (struct, `(id, generation)`). The handle exposes only control methods plus a small set of late-subscription callbacks (`OnComplete`, `OnKill`, `OnStepComplete`, multicast). Same split for `SequenceBuilder` and `Sequence`. The pooled backing class has a finalizer that enqueues a leak-detection id onto a lock-free queue; the runner drains the queue on the main thread and emits an Editor `Debug.LogWarning`. Best-effort diagnostic; the Roslyn analyzer (optional, M2) is the hard guarantee.
3. **Pooled-class internal storage** in v1, swappable later. Public handle insulates users.
4. **PlayerLoop injection runner**, no MonoBehaviour. Edit-mode capable via `EditorApplication.update`.
5. **Three update phases from M1**: `Update`, `LateUpdate`, `FixedUpdate`. Plus a `Manual` mode that takes an explicit `deltaTime`.
6. **Ease as `EaseRef` value type** produced by `Easing.X(...)` factories (`Easing.OutBack(overshoot)`, `Easing.Elastic(strength, period)`, `Easing.BounceExact(amplitudeMeters)`, `Easing.Curve(animCurve)`, `Easing.Custom(easeFunc)`). The ease and its parameters travel together; the tween stores one `EaseRef`. Plug new eases in without API change.
7. **`[Serializable] TweenSettings` and `TweenSettings<T>`** structs for designer-facing inspector workflows. The generic form bundles `startValue`/`endValue` plus a `WithDirection(bool toEndValue)` helper for show/hide style toggles.
8. **Custom awaiter in core** (`await tween;`, zero managed alloc per await). UniTask asmdef remains optional for cancellation ergonomics.
9. **Per-frame auto-kill** for `UnityEngine.Object` targets (`obj == null` check).
10. **SoA-friendly internal layout** to keep a future Burst path cheap; not a public concern.
11. **Parent-sequence model from M1**: every animation has `_start`, `_end`, `_timeScale`, `_parent`. A hidden root sequence owned by the runner contains all top-level tweens. M2 nested sequences slot in for free. The public type is named `Sequence` to avoid clashing with Unity's `Timeline` package.
12. **`Position` value type from M1**: typed `End`, `AtTime`, `AtLabel`, `AfterPrevious`, `WithPrevious`. The GSAP string DSL (`"+=0.3"`, `"<"`, `">"`) is added later as a pure `Position.Parse` sugar, no plumbing changes.
13. **`From` / `FromTo` are core builder methods**, not extensions. The snap (capture start + invoke `setter(start)`) fires as follows: a **root tween** snaps at `.Start()` regardless of its own `SetDelay` (the delay only defers interpolation, not the snap, matching DOTween). A **sequenced child** with a parent-imposed offset `> 0` snaps when the parent playhead first crosses `child._start` (matching GSAP), so the property is not visibly forced to the From-value before the child should play; the child's own `SetDelay` further offsets interpolation but not the snap.
14. **Safe mode** (try/catch around tween step and callbacks) included from M1. Default `true` in Editor, `false` in release builds.
15. **Zero-alloc target-capture overloads** for every callback and every generic creation method. A `<TTarget>(TTarget target, Action<TTarget, ...> action)` overload sits next to each plain delegate overload, so users can write static lambdas that receive the target as a parameter and eliminate closure allocations.
16. **Extensible value-type support via `IInterpolator<T>`**: users can register interpolators for custom blittable types without forking the core. M1 ships built-ins for `float`, `Vector2/3/4`, `Color`, `Quaternion`, `int`.

---

## Detailed documentation

| Topic | Document |
|-------|----------|
| Public API | [docs/api.md](docs/api.md) |
| Architecture | [docs/architecture.md](docs/architecture.md) |
| Editor & integration | [docs/editor.md](docs/editor.md) |
| Implementation plan | [docs/implementation.md](docs/implementation.md) |
| Reference & comparison | [docs/reference.md](docs/reference.md) |
