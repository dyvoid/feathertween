# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-06

## Current position

- **Milestone**: M1 (Core), production cut. See `docs/implementation.md` §10.
- **Done through**: Phase 1.9 (Callbacks), verified green in Unity (Editor + Runtime + Performance).
- **Remaining M1 phases** (renumbered 2026-07-02, production cut; M1 is now 14 linear phases): 1.9 (callbacks) → 1.10 (seek/control) → 1.11 (typed shortcuts) → 1.12 (filters/bulk ops) → 1.13 (safe mode) → 1.14 (acceptance, v0.1 tag). Fast paths, TweenSettings, and the cross-engine benchmark moved to M2.
- **Next phase**: 1.10 — Seek and control surface (bidirectional AdvanceTo rework, sequence SetLoops, global/per-phase time scale).
- **Branch**: trunk-based on `main`; short-lived branches `task/1.x-phase-name` / `fix/...`, fast-forward merge.

## Done

- **Phases 1.1–1.9** (merged to `main`): storage/handle scaffold, PlayerLoop runner, builder/handle split, generic tween core, full ease system, From/FromTo, loops/delays/direction/reverse, sequence builder, callbacks.
- **Phase 1.9 highlights**: full callback set on both builders, target-capture OnComplete/OnKill (CallbackEntry + cached per-type invoker, zero-alloc dispatch), handle-side OnStepComplete, TweenCommandQueue deferred-mutation buffer (Kill/Complete/Restart/Reverse from inside callbacks defer to end of tick), TweenOps dedupe of handle logic, firing-matrix compliance fix (no OnKill on completion paths, §3.14). 21 tests in `Tests/Editor/CallbackTests.cs` + callback-dispatch zero-alloc perf guard.
- **Phase 1.8 highlights**: `SequenceBuilder` (Append/Insert/Join/Prepend*/AppendInterval/AppendCallback/AddLabel/AddPause/Clear), `Position` type, `SequenceCancelBehavior`, `SetDefaults` cascade frozen at append, label resolution at `Start()`, typed child storage (ids only, no boxing), sequenced-From snap deferred to parent-window entry, nested sequences, `Sequence` handle control surface. 23 tests in `Tests/Editor/SequenceTests.cs` + zero-alloc sequence tick guard in the perf suite.
- **Deferred-snap refactor**: start-value capture moved to `TweenData<T>.ResolveStartValues()`; `FromValue` holds the pristine user value so Restart re-arm is safe.
- **SequenceDemo sample** (`Samples~/SequenceDemo`): choreographed loop covering all 1.8 features, registered in `package.json`.
- **Test de-flake**: `RunnerTicks_AfterScriptUpdate` probe now pairs its readings within a single frame (was racy across frames).
- Earlier history (docs reconciliation, lifecycle fixes, namespace fix, sample, perf suite, docs/process infra): see git log.

## In flight

- Nothing half-built. All suites green in Unity (2026-07-03).

## Next up

1. Begin Phase 1.10 — Seek and control surface: rework `SequenceData.Step` into a shared bidirectional `AdvanceTo(from, to, fireCallbacks)` boundary walk; `Seek(seconds, fireCallbacks)` per §3.15; sequence `SetLoops`; `PATween.SetGlobalTimeScale` + per-phase scale; sequence `Reverse`; mid-play `Sequence.Insert`. See `docs/implementation.md` §10 phase 1.10.

## Infra (2026-07-02)

- **.NET stub harness** (`tools~/compile-check/`): compiles Runtime + Samples + EditMode tests against UnityEngine stubs, runs the suite via NUnitLite in <1s. Unity-only tests carry `[Category("RequiresUnity")]`.
- **CI**: `.github/workflows/ci.yml` runs the harness on push to `main` and PRs. Branch protection (require CI, no direct push) still to be enabled on GitHub by the user.
- **ADR 0010**: `SetDefaults` duration parameter removed (dead code — creation methods require explicit duration).
- **Roadmap refinement (2026-07-03)**: sequence `SetLoops` + global/per-phase time scale added to 1.10; LICENSE/CHANGELOG/XML-docs added to the 1.14 gate; `SetLink` and Awaitables (Unity 6 native `Awaitable`) promoted to Planned in M2; Editor preview window promoted M4→M3; blendable tweens flagged needs-ADR; cross-timeline `globalTime` marked drop-unless-needed.

## Open questions / decisions pending

- Sequence `Reverse()`/direction support is deferred to 1.10 (Seek/control surface) — `SequenceData.Step` is forward-only for now.
- `AddLabel(name, Position)` resolves at definition time (only `Insert`/`AddPause` defer label resolution to `Start()`); duplicate label names throw.

## Known issues / tech debt

- `Kill(target)` is an O(n) linear scan of the active list (acceptable for now; the target-indexed multimap lands in phase 1.12). See `docs/implementation.md` §8.1.
- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- An abandoned (never-started) `SequenceBuilder` leaks its already-allocated child store slots until the next `TweenStore.Reset()`; the LeakDetector warns via finalizer.

### From code review (2026-07-06)

**High**

- **`TweenData<T>` not pooled**: `TweenBuilderBuffer.Build()` does `new TweenData<T>()` every time. Builder buffers are pooled, but the runtime data they produce is not. Every `Start()` allocates. The zero-alloc guarantee covers the tick loop, not creation — clarify docs or pool the data records.
- **Incremental loop `GetCycleEnds` is O(cycleIndex)**: The `Add` loop in `TweenDataT.cs:404-414` recomputes the offset from scratch every frame. At high cycle counts (infinite incremental loops) this becomes a per-frame linear cost. Needs `IInterpolator<T>.Scale(T, int)` or cached cycle base — public interface change, needs ADR.
- **Elastic ease ignores `amplitude`/`period` parameters**: `EaseEval` receives `paramA`/`paramB` but the elastic formulas use hardcoded constants. `Easing.InElastic(amplitude, period)` is cosmetic-only. Same for `BounceExact` amplitude.

**Medium**

- **`RemoveFromActiveList` is O(n) per `Free()`**: Applies to all kills, not just `Kill(target)`. Batch kills on scene transition → O(n²). Broader than the existing bullet above.
- **Safe mode not implemented**: Invariant 6 specifies try/catch around step and callbacks; currently absent. A throwing getter/setter will corrupt tick iteration. Scheduled for phase 1.13, just confirming it's a real gap.
- **Builder buffer callback duplication**: `TweenBuilderBuffer` and `SequenceBuilderBuffer` duplicate ~120 lines of identical callback list infrastructure (`Add`/`Transfer`/`Clear`). Maintenance hazard — any callback change needs two edits.
- **`Interpolators.Get<T>()` dictionary lookup per `Build()`**: Could use a generic static class pattern (`InterpolatorCache<T>.Value`) for O(1) JIT-inlined access instead of `Dictionary<Type, object>` + unbox.
- **`Easing` parameterless factories allocate a new struct per call**: `Easing.Linear()` etc. could be `static readonly` fields for the parameterless variants. Called on every tween creation via `ResetConfig()`.
- **8 callback list fields on every `TweenData`**: Most tweens use 0-2 callbacks but carry 8 `List<>` reference slots (64 bytes). Since `TweenData` isn't pooled, this is pure per-instance waste.

## Test status

- All suites green in Unity (Editor + Runtime + Performance), verified 2026-07-03 (phase 1.9 included).

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `docs/guides/testing.md`.
