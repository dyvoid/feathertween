# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-02

## Current position

- **Milestone**: M1 (Core), 16 phases. See `docs/implementation.md` §10.
- **Done through**: Phase 1.8 (Sequence builder), verified green in Unity (Editor + Runtime + Performance) and visually via the SequenceDemo sample.
- **Next phase**: 1.9 — Callbacks (full set + multicast + reentrancy).
- **Branch**: trunk-based on `main`; short-lived branches `task/1.x-phase-name` / `fix/...`, fast-forward merge.

## Done

- **Phases 1.1–1.8** (merged to `main`): storage/handle scaffold, PlayerLoop runner, builder/handle split, generic tween core, full ease system, From/FromTo, loops/delays/direction/reverse, sequence builder.
- **Phase 1.8 highlights**: `SequenceBuilder` (Append/Insert/Join/Prepend*/AppendInterval/AppendCallback/AddLabel/AddPause/Clear), `Position` type, `SequenceCancelBehavior`, `SetDefaults` cascade frozen at append, label resolution at `Start()`, typed child storage (ids only, no boxing), sequenced-From snap deferred to parent-window entry, nested sequences, `Sequence` handle control surface. 23 tests in `Tests/Editor/SequenceTests.cs` + zero-alloc sequence tick guard in the perf suite.
- **Deferred-snap refactor**: start-value capture moved to `TweenData<T>.ResolveStartValues()`; `FromValue` holds the pristine user value so Restart re-arm is safe.
- **SequenceDemo sample** (`Samples~/SequenceDemo`): choreographed loop covering all 1.8 features, registered in `package.json`.
- **Test de-flake**: `RunnerTicks_AfterScriptUpdate` probe now pairs its readings within a single frame (was racy across frames).
- Earlier history (docs reconciliation, lifecycle fixes, namespace fix, sample, perf suite, docs/process infra): see git log.

## In flight

- Nothing half-built. All suites green in Unity (2026-07-02).

## Next up

1. Begin Phase 1.9 — Callbacks: builder-side `OnStart`/`OnPlay`/`OnPause`/`OnUpdate`/`OnStepComplete`/`OnComplete`/`OnKill`/`OnRewind`, zero-alloc target-capture overloads on `OnComplete`/`OnKill`, handle-side multicast, deferred-mutation command buffer drained at end of tick. See `docs/implementation.md` §10 phase 1.9.

## Open questions / decisions pending

- Sequence `Reverse()`/direction support is deferred to 1.10 (Seek/control surface) — `SequenceData.Step` is forward-only for now.
- `AddLabel(name, Position)` resolves at definition time (only `Insert`/`AddPause` defer label resolution to `Start()`); duplicate label names throw.

## Known issues / tech debt

- `Kill(target)` is an O(n) linear scan of the active list (acceptable for now; target-indexed map deferred to M2). See `docs/implementation.md` §8.1.
- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- An abandoned (never-started) `SequenceBuilder` leaks its already-allocated child store slots until the next `TweenStore.Reset()`; the LeakDetector warns via finalizer.

## Test status

- All suites green in Unity (Editor + Runtime + Performance), verified 2026-07-02. SequenceDemo sample verified visually.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `docs/guides/testing.md`.
