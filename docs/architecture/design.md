# PATween — Design Proposal

A robust, minimal C# tween engine for Unity. Compositional sequences, static-method API, struct handles, PlayerLoop runner.

---

## Goals and non-goals

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

## Design principles (locked anchors)

1. **Static-method API**: typed shortcuts live as static methods on `PATween` (`PATween.Move(transform, ...)`), not as extension methods on Unity types. Avoids namespace pollution on `Transform`/`CanvasGroup`/etc. and gives one discoverable entry point. A small optional `PATween.Extensions` asmdef in a later milestone can re-add DOTween-style extension wrappers for users who prefer them.
2. **Builder/handle split with explicit `.Start()`**.
   - `PATween.X(...)` returns a `TweenBuilder<T>` struct; copies alias the same pooled backing record.
   - After `.Start()` or sequence consumption, every builder alias is invalid.
   - `.Start()` returns an immutable `Tween` handle (`id, generation`). Stale handles no-op safely.
   - Same split for `SequenceBuilder` and `Sequence`.
   - The pooled backing record has a finalizer that enqueues a leak-detection id; the runner drains it on the main thread and logs a warning in Editor. This is a best-effort diagnostic; the optional Roslyn analyzer (M2) is the hard guarantee.
3. **Pooled-class internal storage** in v1, swappable later. Public handle insulates users.
4. **PlayerLoop injection runner**, no MonoBehaviour. Edit-mode capable via `EditorApplication.update`.
5. **Three update phases from M1**: `Update`, `LateUpdate`, `FixedUpdate`. Plus a `Manual` mode that takes an explicit `deltaTime`.
6. **Ease as `EaseRef` value type** produced by `Easing.X(...)` factories. The ease and its parameters travel together; the tween stores one `EaseRef`. Plug new eases in without API change.
7. **`[Serializable] TweenSettings` and `TweenSettings<T>`** structs for designer-facing inspector workflows. The generic form bundles `startValue`/`endValue` plus a `WithDirection(bool toEndValue)` helper for show/hide style toggles.
8. **Custom awaiter** (`await tween;`, zero managed alloc per await). Deferred to M2; core callback surface covers the same use cases. UniTask asmdef remains optional for cancellation ergonomics.
9. **Per-frame auto-kill** for `UnityEngine.Object` targets (`obj == null` check).
10. **SoA-friendly internal layout** to keep a future Burst path cheap; not a public concern.
11. **Parent-sequence model from M1**: every animation has `_start`, `_end`, `_timeScale`, `_parent`. A hidden root sequence owned by the runner contains all top-level tweens. M2 nested sequences slot in for free. The public type is named `Sequence` to avoid clashing with Unity's `Timeline` package.
12. **`Position` value type from M1**: typed `End`, `AtTime`, `AtLabel`, `AfterPrevious`, `WithPrevious`. The GSAP string DSL (`"+=0.3"`, `"<"`, `">"`) is added later as a pure `Position.Parse` sugar, no plumbing changes.
13. **`From` / `FromTo` are core builder methods**, not extensions. A **root tween** snaps at `.Start()` regardless of its own `SetDelay` (delay only defers interpolation, not the snap). A **sequenced child** with a parent-imposed offset `> 0` snaps when the parent playhead first crosses `child._start`; the child's own `SetDelay` further offsets interpolation but not the snap.
14. **Safe mode** (try/catch around tween step and callbacks) included from M1. Default `true` in Editor, `false` in release builds.
15. **Zero-alloc target-capture overloads** for every callback and every generic creation method. A `<TTarget>(TTarget target, Action<TTarget, ...> action)` overload sits next to each plain delegate overload, so users can write static lambdas that receive the target as a parameter and eliminate closure allocations.
16. **Extensible value-type support via `IInterpolator<T>`**: users can register interpolators for custom blittable types without forking the core. M1 ships built-ins for `float`, `Vector2/3/4`, `Color`, `Quaternion`, `int`.

---

## Detailed documentation

| Topic | Document |
|-------|----------|
| Public API quickstart | [../api/index.md](../api/index.md) |
| Architecture overview | [overview.md](overview.md) |
| Sequence design | [sequence.md](sequence.md) |
| Performance plan | [../architecture/performance.md](../architecture/performance.md) |
| Code conventions | [../guides/conventions.md](../guides/conventions.md) |
| Testing | [../guides/testing.md](../guides/testing.md) |
| Editor & integration | [../guides/editor.md](../guides/editor.md) |
| Milestones and phases | [../planning/phases.md](../planning/phases.md) |
| Risks and open questions | [../planning/risks.md](../planning/risks.md) |
| Engine comparison | [../design/comparison.md](../design/comparison.md) |
| Influences | [../design/influences.md](../design/influences.md) |
