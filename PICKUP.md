# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-02

## Current position

- **Milestone**: M1 (Core), 16 phases. See `docs/implementation.md` §10.
- **Done through**: Phase 1.7. Phase 1.8 (Sequence builder) implemented on `task/1.8-sequence-builder`, pending in-Unity test verification.
- **Next phase**: verify 1.8 in Unity, then 1.9 — Callbacks.
- **Branch**: trunk-based on `main`; short-lived branches `task/1.x-phase-name` / `fix/...`, fast-forward merge.

## Done

- **Phases 1.1–1.7** (merged to `main`): storage/handle scaffold, PlayerLoop runner, builder/handle split, generic tween core, full ease system, From/FromTo, loops/delays/direction/reverse.
- **Phase 1.8 — Sequence builder** (on `task/1.8-sequence-builder`): `SequenceBuilder` (Append/Insert/Join/Prepend*/AppendInterval/AppendCallback/AddLabel/AddPause/Clear), `Position` type, `SequenceCancelBehavior`, `SetDefaults` cascade frozen at append, label resolution at `Start()`, typed child storage (ids only, no boxing), sequenced-From snap deferred to parent-window entry, nested sequences, `Sequence` handle control surface (Play/Pause/Resume/Restart/Complete/Kill/Duration). 22 new tests in `Tests/Editor/SequenceTests.cs` + zero-alloc sequence tick guard in perf suite.
- **Deferred-snap refactor**: start-value capture moved from `TweenBuilder.Start()` into `TweenData<T>.ResolveStartValues()`; `FromValue` now holds the pristine user value so re-resolving after `Restart` is safe (fixes a latent From-restart corruption).
- Earlier history (docs reconciliation, lifecycle fixes, namespace fix, sample, perf suite, docs/process infra): see git log.

## In flight

- **Phase 1.8 verification**: all suites pass in a stub-based NUnitLite run (95/96; the one failure is the PlayerLoop-injection test, which requires real Unity). Needs a real Unity Editor test run before merging to `main` and marking the roadmap Done.

## Next up

1. Run Editor + Runtime + Performance suites in Unity on `task/1.8-sequence-builder`; fix anything the stubs missed.
2. Merge to `main` (rebase + ff), mark Sequence builder Done in `docs/ROADMAP.md`.
3. Begin Phase 1.9 — Callbacks (full set + multicast + reentrancy). See `docs/implementation.md` §10 phase 1.9.

## Open questions / decisions pending

- Sequence `Reverse()`/direction support is deferred to 1.10 (Seek/control surface) — `SequenceData.Step` is forward-only for now.
- `AddLabel(name, Position)` resolves at definition time (only `Insert`/`AddPause` defer label resolution to `Start()`); duplicate label names throw.

## Known issues / tech debt

- `Kill(target)` is an O(n) linear scan of the active list (acceptable for now; target-indexed map deferred to M2). See `docs/implementation.md` §8.1.
- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- An abandoned (never-started) `SequenceBuilder` leaks its already-allocated child store slots until the next `TweenStore.Reset()`; the LeakDetector warns via finalizer.

## Test status

- Stub-based NUnitLite run (2026-07-02): 95/96 green; the failure is `Install_InjectsThreePlayerLoopSubsystems`, which needs real Unity PlayerLoop. In-Unity verification pending.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `docs/guides/testing.md`.
