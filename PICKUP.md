# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-07

## Current position

- **Milestone**: M1 (Core), production cut. See `docs/implementation.md` §10.
- **Done through**: Phase 1.10 (Seek/control) implemented and green in the compile-check harness (148 tests); **needs an in-Unity verification pass** (Editor + Runtime + Performance) before it counts as done-done. Phase 1.9 and earlier verified in Unity 2026-07-03.
- **Remaining M1 phases** (renumbered 2026-07-02, production cut; M1 is now 14 linear phases): 1.9 (callbacks) → 1.10 (seek/control) → 1.11 (typed shortcuts) → 1.12 (filters/bulk ops) → 1.13 (safe mode) → 1.14 (acceptance, v0.1 tag). Fast paths, TweenSettings, and the cross-engine benchmark moved to M2.
- **Next phase**: 1.11 — Typed shortcuts (Move/Rotate/Scale/LocalMove/LocalRotate, Fade, Color, FillAmount), after 1.10 is verified in Unity.
- **Branch**: trunk-based on `main`; short-lived branches `task/1.x-phase-name` / `fix/...`, fast-forward merge.

## Done

- **Phase 1.10 (2026-07-07, branch `task/1.10-seek-control`)**: `Tween.Seek`/`Sequence.Seek` per §3.15 (silent single-sample vs boundary-walking; pause halts a firing seek); `SequenceData` rewritten around a bidirectional `AdvanceTo(from, to)` cycle walk shared by tick/Seek/ForceComplete (Yoyo cycles are backward local walks); `SequenceBuilder.SetLoops` (Restart/Yoyo; Incremental degrades to Restart); `Sequence.Reverse`; `Sequence.TotalProgress`; mid-play `Sequence.Insert` (deferred from callbacks via new `SequenceInsert` queue op); per-tween `SetTimeScale` + `PATween.SetGlobalTimeScale` + per-phase `PATween.SetTimeScale` (engine-side, governs `ignoreTimeScale` tweens too — decided + documented). 21 tests in `Tests/Editor/SeekControlTests.cs`. **Semantics notes**: sequence completion now fires the final loop-end `OnStepComplete` (matches tween §3.14); `To` children re-snap from current values on loop wrap (deferred-snap design), so exact per-cycle replay needs `FromTo`/`From` children. `SetCancelOnError` moved to 1.13 (no-op until safe mode exists).
- **Review-fix pass (2026-07-07)**: incremental-loop O(1) cycle cache; parametric elastic (amplitude/period) + amplitude-scaled `BounceExact`; §8.1 doc reconciliation. See "From code review" below.
- **Phases 1.1–1.9** (merged to `main`): storage/handle scaffold, PlayerLoop runner, builder/handle split, generic tween core, full ease system, From/FromTo, loops/delays/direction/reverse, sequence builder, callbacks.
- **Phase 1.9 highlights**: full callback set on both builders, target-capture OnComplete/OnKill (CallbackEntry + cached per-type invoker, zero-alloc dispatch), handle-side OnStepComplete, TweenCommandQueue deferred-mutation buffer (Kill/Complete/Restart/Reverse from inside callbacks defer to end of tick), TweenOps dedupe of handle logic, firing-matrix compliance fix (no OnKill on completion paths, §3.14). 21 tests in `Tests/Editor/CallbackTests.cs` + callback-dispatch zero-alloc perf guard.
- **Phase 1.8 highlights**: `SequenceBuilder` (Append/Insert/Join/Prepend*/AppendInterval/AppendCallback/AddLabel/AddPause/Clear), `Position` type, `SequenceCancelBehavior`, `SetDefaults` cascade frozen at append, label resolution at `Start()`, typed child storage (ids only, no boxing), sequenced-From snap deferred to parent-window entry, nested sequences, `Sequence` handle control surface. 23 tests in `Tests/Editor/SequenceTests.cs` + zero-alloc sequence tick guard in the perf suite.
- **Deferred-snap refactor**: start-value capture moved to `TweenData<T>.ResolveStartValues()`; `FromValue` holds the pristine user value so Restart re-arm is safe.
- **SequenceDemo sample** (`Samples~/SequenceDemo`): choreographed loop covering all 1.8 features, registered in `package.json`.
- **Test de-flake**: `RunnerTicks_AfterScriptUpdate` probe now pairs its readings within a single frame (was racy across frames).
- Earlier history (docs reconciliation, lifecycle fixes, namespace fix, sample, perf suite, docs/process infra): see git log.

## In flight

- **Phase 1.10 on `task/1.10-seek-control`**, harness-green, awaiting in-Unity verification + fast-forward merge to `main`.

## Next up

1. Verify 1.10 in Unity (Editor + Runtime + Performance suites), then fast-forward merge `task/1.10-seek-control` into `main`.
2. Begin Phase 1.11 — Typed shortcuts: `PATween.Move/Rotate/Scale/LocalMove/LocalRotate` (Transform), `Fade` (CanvasGroup), `Color`/`FillAmount` (Image), all on the lambda core. See `docs/implementation.md` §10 phase 1.11.

## Infra (2026-07-02)

- **.NET stub harness** (`tools~/compile-check/`): compiles Runtime + Samples + EditMode tests against UnityEngine stubs, runs the suite via NUnitLite in <1s. Unity-only tests carry `[Category("RequiresUnity")]`.
- **CI**: `.github/workflows/ci.yml` runs the harness on push to `main` and PRs. Branch protection (require CI, no direct push) still to be enabled on GitHub by the user.
- **ADR 0010**: `SetDefaults` duration parameter removed (dead code — creation methods require explicit duration).
- **Roadmap refinement (2026-07-03)**: sequence `SetLoops` + global/per-phase time scale added to 1.10; LICENSE/CHANGELOG/XML-docs added to the 1.14 gate; `SetLink` and Awaitables (Unity 6 native `Awaitable`) promoted to Planned in M2; Editor preview window promoted M4→M3; blendable tweens flagged needs-ADR; cross-timeline `globalTime` marked drop-unless-needed.

## Open questions / decisions pending

- `AddLabel(name, Position)` resolves at definition time (only `Insert`/`AddPause` defer label resolution to `Start()`); duplicate label names throw.

## Known issues / tech debt

- `Kill(target)` is an O(n) linear scan of the active list (acceptable for now; the target-indexed multimap lands in phase 1.12). See `docs/implementation.md` §8.1.
- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- An abandoned (never-started) `SequenceBuilder` leaks its already-allocated child store slots until the next `TweenStore.Reset()`; the LeakDetector warns via finalizer.

### From code review (2026-07-06, verified + triaged 2026-07-07)

Resolved 2026-07-07:

- ~~Incremental loop `GetCycleEnds` O(cycleIndex)~~ — fixed with a cached cycle base (O(1) amortized; backward jumps recompute once). No interface change was needed, contrary to the review's suggestion.
- ~~Elastic/BounceExact parameters ignored~~ — implemented parametric Penner elastic (amplitude/period) and amplitude-scaled BounceExact; defaults reduce exactly to the former hardcoded constants.
- ~~Docs claimed `TweenData<T>` pooling that doesn't exist~~ — §8.1 amended to state reality; pooling itself is scheduled (below).
- ~~`Easing.Linear()` "allocates a struct per call"~~ — **struck**: `EaseRef` is a readonly struct; `new` on it is stack construction, zero heap alloc. The finding misread C# struct semantics.

Scheduled in phase 1.12 (storage surgery, now in `docs/implementation.md` §10):

- `TweenData<T>` pooling so `Start()` is alloc-free after warmup; the pooling design also collapses the 8 per-instance callback-list fields into a lazily allocated slot structure.
- Swap-remove + index map for active lists (`RemoveFromActiveList` is O(n) per `Free()`; batch kills O(n²)).
- Target-indexed multimap for `Kill(target)` (was already scheduled).

Remaining tracked debt:

- **Safe mode absent** — scheduled 1.13, confirmed real gap (a throwing setter corrupts tick iteration until then).
- **Builder buffer callback duplication** (~120 lines shared between `TweenBuilderBuffer`/`SequenceBuilderBuffer`) — extract a shared `CallbackBuffer` when either next changes (refactor-on-touch).
- **`Interpolators.Get<T>()` dictionary lookup per `Build()`** — valid micro-opt (generic static cache), but the cache must handle `Interpolators.Reset()` re-registration (version stamp) or interpolator-swapping tests break. Low urgency; fold into 1.12 or M2 fast paths.

## Test status

- Compile-check harness: 148 tests green (2026-07-07, includes phase 1.10 + review fixes).
- Unity (Editor + Runtime + Performance): last verified 2026-07-03 (through 1.9). 1.10 changes not yet run in Unity.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `docs/guides/testing.md`.
