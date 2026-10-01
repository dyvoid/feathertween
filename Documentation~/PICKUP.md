# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-10-01 (full code + doc review on `fix/review-findings`, PR against `develop`)

## Current position

- **Milestone**: M1 **closed**. v0.1.0 tagged on `main` 2026-07-18.
- **Versioning** (decided 2026-09-20, policy in `git-strategy.md`): pre-1.0, **minor versions may
  break**; the 0.1.0 stability promise is withdrawn. `develop` carries `0.2.0-dev`; the release
  commit drops the suffix. Release per coherent chunk — `v0.2.0` is `SetLink` + awaitables together.
  Both are on `develop`; tag once the review PR below lands and the Showcase has had its editor pass.
- **Branching**: `main` tracks the last release and stays the default/landing branch; `develop` is the integration branch and the base for task branches. See `Documentation~/git-strategy.md`.
- **M2 in flight**. `SetLink` (PR #4) and awaitables (PR #5) merged to `develop`. After the v0.2.0
  release: zero-alloc fast paths. See `Documentation~/ROADMAP.md`.

## This session (2026-10-01, full review)

Code + doc review of the whole package, on `fix/review-findings` (PR against `develop`). Fixed,
each with a regression test that fails on the old code: re-entrant `FT.ManualTick` corrupting the
outer tick (now throws), `KillAll` from a setter recycling the stepping record, end-of-tick frees now
generation-checked, destroyed target on a completed tween firing `OnKill`, `Complete()` on a
self-killing sequence firing `OnStepComplete` after `OnKill`, seek/reverse into a nested sequence
with its own delay rendering `delay` seconds ahead, `SetDefaults(loops: 0)` looping a child forever
and clamping a negative delay, mid-play `Insert` dropping a `SetLink`, `SetCapacity` hanging above
2^30. **`LoopType.Rewind` was never implemented** (it was `Restart`); it now replays odd cycles
backward in time, PrimeTween's meaning (user decision, ADR 0014). Docs reconciled across `api/`,
`architecture/`, planning, ADRs 0002/0005/0013 (amendment/addenda). Three behavior changes are in
CHANGELOG `### Changed` with migration notes.

**Needs a Unity pass before merge** (git-strategy CI gate): EditMode + PlayMode + Performance on the
Runtime changes, and the Showcase loops chapter (Yoyo/Rewind lanes now `OutCubic`, caption longer —
check it fits the 110 px caption box).

## Previous session (2026-09-30, Showcase catch-up)

The Showcase still advertised the v0.1.0 feature set. Its playground (chapter 8, the only
off-timeline chapter) now has P3 (`SetLink` pause/resume with a toggle and `Status` readout) and P4
(the same rise/punch/settle chain as `await` and as a coroutine). Spec and test-protocol step 7 in
`design/showcase-sample.md`. **Needs a visual pass in the editor** — layout and readability were not
checked in Unity; the GUI panel grew from 330 to 400 px tall.

## Previous session (2026-09-21, M2.2 awaitables)

`await tween` / `await sequence` / `await FT.Move(...)`, plus `WaitForCompletion()` → `Awaitable` and
`ToYieldInstruction()` for coroutines. Semantics: `api/awaiters.md`. Rationale: ADR 0013.

Three things a future session should not silently reverse:

- **No UniTask and no `Awaitable` dependency.** `await` binds to any `GetAwaiter()`, so `TweenAwaiter`
  is ours. `WaitForCompletion()` returns `Awaitable` only so consumers can `AsUniTask()` it for `WhenAll`.
- **`await` is not zero-alloc and cannot be** — the async state machine allocates per call; only the
  struct is free. Do not re-add the "zero-alloc await" claim `phases.md` used to carry.
- **The awaiter hangs off `OnComplete` + an internal disposal hook fired by `TweenStore.Free`, not
  `OnKill`.** `Free` is the one chokepoint every death route passes; `OnKill` is not — a safe-mode
  setter exception without `CancelOnError` (the Editor default) cancels and frees without firing it,
  which parked `await` forever in the first cut. Do not "simplify" this back to `OnKill`.

**Deliberately not built**: `WaitForKill`, `WaitForPosition`, `WaitForElapsedLoops`. The disposal
hook now exists, so `WaitForKill` is buildable on it; the two playhead waits still need a per-tick
pending-wait registry the engine lacks.

## Test status

- Compile-check harness, run locally 2026-10-01 on `fix/review-findings`: 268 EditMode tests green,
  release leg (`FEATHERTWEEN_RELEASE`) green, doc check and compile check green. Awaitables have
  **not** been exercised in real Unity — the stub `Awaitable` is a no-op, so `WaitForCompletion()`'s
  real completion is untested.
- The harness **can** run in this container: `apt-get update && apt-get install -y dotnet-sdk-8.0`
  works (the dotnet-install script host is blocked; apt and nuget.org are not). CI runs only on
  pushes to `main`/`develop` and on PRs.
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
- **Open API question — `Append` uses a cursor, not `Position.End`.** `Append` places a child after
  the last `Append`/`AppendInterval`; an `Insert` past that point does not move it. DOTween and GSAP
  append after everything. Now documented as-is (`api/sequences.md`); switching to `Position.End`
  semantics would be a breaking change and wants a decision, plus a Showcase check.
- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- Abandoned (never-started) `SequenceBuilder` pins child store slots until `TweenStore.Reset()` — documented behavior (builders.md, CHANGELOG known limitations), LeakDetector warning is the mitigation.
- **Branch protection not set**: `develop` exists (created from `main` 2026-09-20). `main` stays the default branch by design — it is the consumer-facing landing page. Still needs, via GitHub settings: no direct push to either branch, PRs targeted at `develop`, CI required to merge. Until then the model is convention, not enforcement.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `Documentation~/guides/testing.md`.
