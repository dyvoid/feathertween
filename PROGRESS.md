# PATween — Progress

Living status file. Read this first at session start. Update it at session end.
Git history is the archive; keep this file short and current, prune stale detail.

Last updated: 2026-06-01

## Current position

- **Milestone**: M1 (Core), 16 phases. See `docs/implementation.md` §10.
- **Done through**: Phase 1.7 (loops, delays, reverse). Plus post-1.7 hardening (code-review fixes), a sample, and a performance suite.
- **Next phase**: 1.8 — Sequence builder.
- **Branch**: work on `develop`; feature branches `feature/1.x-phase-name`.

## Done

- **Phases 1.1–1.7** (merged to `develop`): storage/handle scaffold, PlayerLoop runner, builder/handle split, generic tween core, full ease system, From/FromTo, loops/delays/direction/reverse.
- **Docs reconciliation**: renamed `localTime`, `IInterpolator` Add/Subtract, infinite-loop reverse wrap, ADRs 0008/0009.
- **Code-review lifecycle fixes**: playhead reset on Restart/Play, ForceComplete snap on Complete/Kill(true), TickActive reentrancy hardening (snapshot + generation guard), LeakDetector.Drain in TickLate/TickFixed. Regression tests in `Tests/Editor/LifecycleTests.cs`.
- **Namespace fix**: `PATween` class moved into `namespace PATween`; test refs updated to `global::PATween.PATween`.
- **Edit-mode fix**: `PATweenRunner.EnsureInitialized()` captures main thread id for editor ticking.
- **Sample**: `Samples~/BasicUsage/PATweenDemo.cs` (move/scale/rotate/color/bounce), registered in `package.json`.
- **Performance suite**: `Tests/Performance` EditMode asmdef. Hard-fail zero-alloc guards + report-only throughput benchmarks.
- **Docs/process infra**: `PROGRESS.md` (this file) + AGENTS.md session-state pointer; AGENTS.md Documentation Discipline section; extracted `docs/testing.md`; extracted `docs/conventions.md`; lean-AGENTS.md audit (gist + pointers, no info living only in AGENTS.md).

## In flight

- Nothing half-built. `develop` is clean and ahead of `origin/develop`.

## Next up

1. Push `develop` to origin when ready (currently a few commits ahead).
2. Begin Phase 1.8 — Sequence builder (`SequenceBuilder`, Append/Insert/Join, Position values, labels). See `docs/implementation.md` §10 phase 1.8 for deliverable + test list.

## Open questions / decisions pending

- None.

## Known issues / tech debt

- `Kill(target)` is an O(n) linear scan of the active list (acceptable for now; target-indexed map deferred to M2). See `docs/implementation.md` §8.1.
- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).

## Test status

- Editor + Runtime suites: 72 tests green (last full run 2026-06-01).
- Performance suite added but not yet run in a consumer that has the perf package installed.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `docs/testing.md`.
