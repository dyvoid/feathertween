# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-07 (end of session)

## Current position

- **Milestone**: M1 (Core), production cut. See `docs/implementation.md` §10.
- **Done through**: Phase 1.12 (filters/bulk ops + storage surgery) implemented and green in the compile-check harness (173 tests); **1.10–1.12 need an in-Unity verification pass** (Editor + Runtime + Performance) before they count as done-done. Phase 1.9 and earlier verified in Unity 2026-07-03.
- **Remaining M1 phases** (renumbered 2026-07-02, production cut; M1 is now 14 linear phases): 1.9 (callbacks) → 1.10 (seek/control) → 1.11 (typed shortcuts) → 1.12 (filters/bulk ops) → 1.13 (safe mode) → 1.14 (acceptance, v0.1 tag). Fast paths, TweenSettings, and the cross-engine benchmark moved to M2.
- **Next phase**: 1.13 — Safe mode and assertions (try/catch around setter and callback invocations, `SetSafeMode`, `SetCancelOnError`, off-thread assertion).
- **Branch**: trunk-based on `main`; short-lived branches `task/1.x-phase-name` / `fix/...`, fast-forward merge.

## Done

- **Phase 1.12 (2026-07-07, branch `task/1.12-filters-storage`)**: target-indexed multimap (`Dictionary<object, List<int>>` incl. detached sequence children) behind `PATween.Kill(target, complete)` / `IsTweening(target)`; `KillAll`/`PauseAll`/`ResumeAll` (roots only; children follow parents); O(1) swap-remove active lists via slot→index map; per-type `TweenData<T>` pooling (`TweenDataPool<T>`, returns deferred to end-of-tick via `TweenStore.FlushPoolReturns`). **Found + fixed en route**: `TransferCallbacks` passed `data.AddOnX` method groups to a helper — five 64-byte delegate allocations per `Start()` even with zero callbacks; replaced with null-guarded direct loops in both builder buffers. Create/kill cycle with cached delegates asserted zero-alloc after warmup (guards in both `FilterTests` and the Unity perf suite). 12 tests in `Tests/Editor/FilterTests.cs`.
- **Phase 1.11 (2026-07-07, branch `task/1.11-typed-shortcuts`)**: `PATween.Move/LocalMove/Scale/Rotate/LocalRotate` (Transform, Euler + Quaternion overloads), `Fade` (CanvasGroup), `Color`/`Fade`/`FillAmount` (Image) — all lambda-core builders with auto-set target. **Package now depends on `com.unity.ugui`** (Runtime asmdef references `UnityEngine.UI`). Compile-check stubs got real vector/quaternion math so shortcut tests assert values in the harness. 13 tests in `Tests/Editor/ShortcutTests.cs`.
- **Phase 1.10 (2026-07-07, branch `task/1.10-seek-control`)**: `Tween.Seek`/`Sequence.Seek` per §3.15 (silent single-sample vs boundary-walking; pause halts a firing seek); `SequenceData` rewritten around a bidirectional `AdvanceTo(from, to)` cycle walk shared by tick/Seek/ForceComplete (Yoyo cycles are backward local walks); `SequenceBuilder.SetLoops` (Restart/Yoyo; Incremental degrades to Restart); `Sequence.Reverse`; `Sequence.TotalProgress`; mid-play `Sequence.Insert` (deferred from callbacks via new `SequenceInsert` queue op); per-tween `SetTimeScale` + `PATween.SetGlobalTimeScale` + per-phase `PATween.SetTimeScale` (engine-side, governs `ignoreTimeScale` tweens too — decided + documented). 21 tests in `Tests/Editor/SeekControlTests.cs`. **Semantics notes**: sequence completion now fires the final loop-end `OnStepComplete` (matches tween §3.14); `To` children re-snap from current values on loop wrap (deferred-snap design), so exact per-cycle replay needs `FromTo`/`From` children. `SetCancelOnError` moved to 1.13 (no-op until safe mode exists).
- **Review-fix pass (2026-07-07)**: incremental-loop O(1) cycle cache; parametric elastic (amplitude/period) + amplitude-scaled `BounceExact`; §8.1 doc reconciliation. See "From code review" below.
- **Phases 1.1–1.9** (merged to `main`): storage/handle scaffold, PlayerLoop runner, builder/handle split, generic tween core, full ease system, From/FromTo, loops/delays/direction/reverse, sequence builder, callbacks.
- **Documentation reconciliation (2026-07-07, end of session)**: `docs/ROADMAP.md` marked phases 1.10–1.12 as `Done`; `docs/api.md` and `docs/architecture/sequence.md` corrected `Sequence.Insert` milestone from M2 to phase 1.10.
- **Phase 1.9 highlights**: full callback set on both builders, target-capture OnComplete/OnKill (CallbackEntry + cached per-type invoker, zero-alloc dispatch), handle-side OnStepComplete, TweenCommandQueue deferred-mutation buffer (Kill/Complete/Restart/Reverse from inside callbacks defer to end of tick), TweenOps dedupe of handle logic, firing-matrix compliance fix (no OnKill on completion paths, §3.14). 21 tests in `Tests/Editor/CallbackTests.cs` + callback-dispatch zero-alloc perf guard.
- **Phase 1.8 highlights**: `SequenceBuilder` (Append/Insert/Join/Prepend*/AppendInterval/AppendCallback/AddLabel/AddPause/Clear), `Position` type, `SequenceCancelBehavior`, `SetDefaults` cascade frozen at append, label resolution at `Start()`, typed child storage (ids only, no boxing), sequenced-From snap deferred to parent-window entry, nested sequences, `Sequence` handle control surface. 23 tests in `Tests/Editor/SequenceTests.cs` + zero-alloc sequence tick guard in the perf suite.
- **Deferred-snap refactor**: start-value capture moved to `TweenData<T>.ResolveStartValues()`; `FromValue` holds the pristine user value so Restart re-arm is safe.
- **SequenceDemo sample** (`Samples~/SequenceDemo`): choreographed loop covering all 1.8 features, registered in `package.json`.
- **Test de-flake**: `RunnerTicks_AfterScriptUpdate` probe now pairs its readings within a single frame (was racy across frames).
- Earlier history (docs reconciliation, lifecycle fixes, namespace fix, sample, perf suite, docs/process infra): see git log.

## In flight

- Nothing half-built; 1.10–1.12 merged to `main`, harness-green, awaiting in-Unity verification.

## Next up

1. Verify 1.10–1.12 in Unity (Editor + Runtime + Performance suites). Consumer project must resolve the new `com.unity.ugui` dependency.
2. Begin Phase 1.13 — Safe mode and assertions: try/catch wrapper around setter and callback invocations, `SetSafeMode` per tween, `SetCancelOnError`, off-thread assertion, `PATWEEN_RELEASE` define. See `docs/implementation.md` §10 phase 1.13.

## Infra (2026-07-02)

- **.NET stub harness** (`tools~/compile-check/`): compiles Runtime + Samples + EditMode tests against UnityEngine stubs, runs the suite via NUnitLite in <1s. Unity-only tests carry `[Category("RequiresUnity")]`.
- **CI**: `.github/workflows/ci.yml` runs the harness on push to `main` and PRs. Branch protection (require CI, no direct push) still to be enabled on GitHub by the user.
- **ADR 0010**: `SetDefaults` duration parameter removed (dead code — creation methods require explicit duration).
- **Roadmap refinement (2026-07-03)**: sequence `SetLoops` + global/per-phase time scale added to 1.10; LICENSE/CHANGELOG/XML-docs added to the 1.14 gate; `SetLink` and Awaitables (Unity 6 native `Awaitable`) promoted to Planned in M2; Editor preview window promoted M4→M3; blendable tweens flagged needs-ADR; cross-timeline `globalTime` marked drop-unless-needed.

## Open questions / decisions pending

- `AddLabel(name, Position)` resolves at definition time (only `Insert`/`AddPause` defer label resolution to `Start()`); duplicate label names throw.

## Known issues / tech debt

- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- An abandoned (never-started) `SequenceBuilder` leaks its already-allocated child store slots until the next `TweenStore.Reset()`; the LeakDetector warns via finalizer.

### From code review (2026-07-06, verified + triaged 2026-07-07)

Resolved 2026-07-07:

- ~~Incremental loop `GetCycleEnds` O(cycleIndex)~~ — fixed with a cached cycle base (O(1) amortized; backward jumps recompute once). No interface change was needed, contrary to the review's suggestion.
- ~~Elastic/BounceExact parameters ignored~~ — implemented parametric Penner elastic (amplitude/period) and amplitude-scaled BounceExact; defaults reduce exactly to the former hardcoded constants.
- ~~Docs claimed `TweenData<T>` pooling that doesn't exist~~ — §8.1 amended to state reality; pooling itself is scheduled (below).
- ~~`Easing.Linear()` "allocates a struct per call"~~ — **struck**: `EaseRef` is a readonly struct; `new` on it is stack construction, zero heap alloc. The finding misread C# struct semantics.

Resolved in phase 1.12 (2026-07-07): `TweenData<T>` pooling, swap-remove active lists, target multimap — all landed (see Done). The 8-callback-list-field collapse was **deliberately dropped**: pooled reuse amortizes the per-instance cost, so the collapse would save memory, not allocations; revisit only if record memory shows up in profiling (M2+).

Remaining tracked debt:

- **Safe mode absent** — scheduled 1.13, confirmed real gap (a throwing setter corrupts tick iteration until then).
- **Builder buffer callback duplication** (`TransferCallbacks` + callback lists duplicated between `TweenBuilderBuffer`/`SequenceBuilderBuffer`) — both copies were touched in 1.12 (method-group alloc fix) but the extraction is still pending; do it next time either changes.
- **`Interpolators.Get<T>()` dictionary lookup per `Build()`** — valid micro-opt (generic static cache), but the cache must handle `Interpolators.Reset()` re-registration (version stamp) or interpolator-swapping tests break. Low urgency; fold into M2 fast paths.

## Test status

- Compile-check harness: 173 tests green (2026-07-07, includes phases 1.10–1.12 + review fixes).
- Unity (Editor + Runtime + Performance): last verified 2026-07-03 (through 1.9). 1.10–1.12 changes not yet run in Unity.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `docs/guides/testing.md`.
