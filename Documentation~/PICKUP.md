# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-09-21 (`SetLink` merged; awaitables on `task/2.1-awaitables`)

## Current position

- **Milestone**: M1 **closed**. v0.1.0 tagged on `main` 2026-07-18.
- **Versioning** (decided 2026-09-20, policy in `git-strategy.md`): pre-1.0, **minor versions may
  break**; the 0.1.0 stability promise is withdrawn. `develop` carries `0.2.0-dev`; the release
  commit drops the suffix. Release per coherent chunk — `v0.2.0` is `SetLink` + awaitables together,
  so **do not tag until awaitables land**.
- **Branching**: `main` tracks the last release and stays the default/landing branch; `develop` is the integration branch and the base for task branches. See `Documentation~/git-strategy.md`.
- **M2 in flight**. `SetLink` merged to `develop`; awaitables on `task/2.1-awaitables`. After the
  v0.2.0 release: zero-alloc fast paths. See `Documentation~/ROADMAP.md`.

## This session (2026-09-21, M2.2 awaitables)

`await tween` / `await sequence` / `await FT.Move(...)`, plus `WaitForCompletion()` → `Awaitable` and
`ToYieldInstruction()` for coroutines. Semantics: `api/awaiters.md`. Rationale: ADR 0013.

Three things a future session should not silently reverse:

- **No UniTask and no `Awaitable` dependency.** `await` binds to any `GetAwaiter()`, so `TweenAwaiter`
  is ours. `WaitForCompletion()` returns `Awaitable` only so consumers can `AsUniTask()` it for `WhenAll`.
- **`await` is not zero-alloc and cannot be** — the async state machine allocates per call; only the
  struct is free. Do not re-add the "zero-alloc await" claim `phases.md` used to carry.
- **`OneShotSignal` is load-bearing**: a `SetAutoKill(false)` tween that completes then gets killed
  fires both `OnComplete` and `OnKill`, and resuming a state machine twice throws.

**Deliberately not built**: `WaitForKill`, `WaitForPosition`, `WaitForElapsedLoops` — each needs a
disposal hook or a per-tick pending-wait registry the engine lacks; on today's callbacks they hang.

## Test status

- Compile-check harness at the `SetLink` merge: **230 green + 218 in the FEATHERTWEEN_RELEASE leg**
  (2026-09-20, PR #4). The 20 new `AwaiterTests` have **not run anywhere yet** — CI on the awaitables
  PR is their first execution.
- Note for future sessions in this container: there is no .NET SDK here and the egress policy blocks
  the installer, so the harness cannot be run locally. CI on a PR is the only way to execute it.
- Doc-check leg (CS1591 as error on Runtime): green; wired into CI.
- Unity: Showcase manual protocol + zero-alloc profiler check passed 2026-07-18. `SetLink` verified
  2026-09-20 — 244 EditMode green, play-mode pause/resume confirmed, and the ADR 0012 same-frame
  blind spot reproduced. **The 5 PlayMode tests have not been run in either pass.**

## Known issues / tech debt

- **Open API question — `Seek` past the end leaves `Status == Completed`.** `Seek` is deliberately
  status-neutral, so a timeline scrubbed to its end stays `Completed`; scrubbing back renders the
  frame but never resumes, and `Play()` from there replays from 0 (`LifecycleTests
  .Play_AfterCompletion_ReplaysFromZero` pins that). Found via the Showcase scrubber 2026-09-20; the
  sample's transport now makes it legible rather than hiding it. Fixing it properly means deciding
  whether `Completed` is a state or a position — if seeking backwards cleared it, `OnComplete`
  re-firing needs an answer. Wants an ADR; do not patch it in the sample.
- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- Abandoned (never-started) `SequenceBuilder` pins child store slots until `TweenStore.Reset()` — documented behavior (builders.md, CHANGELOG known limitations), LeakDetector warning is the mitigation.
- **Branch protection not set**: `develop` exists (created from `main` 2026-09-20). `main` stays the default branch by design — it is the consumer-facing landing page. Still needs, via GitHub settings: no direct push to either branch, PRs targeted at `develop`, CI required to merge. Until then the model is convention, not enforcement.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `Documentation~/guides/testing.md`.
