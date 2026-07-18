# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-18 (v0.1.0 released; M1 closed)

## Current position

- **Milestone**: M1 **closed**. v0.1.0 tagged on `main` 2026-07-18; public API declared stable (semver from here).
- Unity manual test protocol for the Showcase (1.17) passed by user, including the zero-alloc profiler check (FeatherTween PlayerLoop rows at 0 B GC Alloc across chapters 1–7).
- **Next**: M2 begins — `SetLink` + Awaitables first (production stickiness), then zero-alloc fast paths. See `Documentation~/ROADMAP.md`.

## This session (2026-07-18, full-library code review + release)

- **Full-library code review** found and fixed four correctness bugs (all with new tests):
  - `SetLoops(0)` / `SetRemainingCycles(0)` produced `loopCount = 0`, which no completion check satisfies → clamped (0 counts the in-progress cycle as the last).
  - Bulk ops (`FT.Kill`/`KillAll`/`PauseAll`/`ResumeAll`) shared one static snapshot pair; a nested bulk op from an `OnKill`/`OnPause` callback corrupted the outer iteration → pooled per-call snapshots.
  - `CompleteAtCycleStart()` never completed a reversed tween still in cycle 0 (no lower boundary to cross) → explicit cycle-0 start path, mirroring sequences.
  - `KillAll` flushed pool returns mid-tick when called from a callback → guarded by `TweenCommandQueue.InCallback`.
- **Runner hardening**: PlayerLoop ticks no-op in edit mode (`UNITY_EDITOR`) so leftover hooks after play mode cannot double-advance edit-mode tweens alongside `EditorRunner`; `Install()` warns when a PlayerLoop anchor system is missing; `TweenData<T>.Step`/`SeekTo` compute the EveryLoop cycle slot in double, matching `ForceComplete`/`TotalProgress`.
- **Docs**: `handles.md` documents the `SetRemainingCycles(0)` clamp and cycle-0 `CompleteAtCycleStart`; `Sequence.Insert` XML doc notes builder-time `SetDefaults` are not applied mid-play.
- **Release**: `package.json` → 0.1.0, CHANGELOG dated, ROADMAP 1.17 → Done, merged to `main` (fast-forward), tagged `v0.1.0`.

## Test status

- Compile-check harness: **216 green + 204 in the FEATHERTWEEN_RELEASE leg** (2026-07-18, includes the four review-fix tests).
- Doc-check leg (CS1591 as error on Runtime): green; wired into CI.
- Unity: Showcase manual protocol + zero-alloc profiler check passed by user 2026-07-18; edit-mode tick guard sanity-checked in the editor (enter/exit play, edit-mode tweens advance at normal speed).

## Known issues / tech debt

- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- Abandoned (never-started) `SequenceBuilder` pins child store slots until `TweenStore.Reset()` — documented behavior (builders.md, CHANGELOG known limitations), LeakDetector warning is the mitigation.
- **Builder buffer callback duplication** (`TransferCallbacks` duplicated between `TweenBuilderBuffer`/`SequenceBuilderBuffer`) — extract next time either changes.
- **`Interpolators.Get<T>()` dictionary lookup per `Build()`** — valid micro-opt; fold into M2 fast paths (cache needs an `Interpolators.Reset()` version stamp).

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `Documentation~/guides/testing.md`.
