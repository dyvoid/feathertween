# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-09-20 (M2 started: `SetLink` shipped)

## Current position

- **Milestone**: M1 **closed**. v0.1.0 tagged on `main` 2026-07-18; public API declared stable (semver from here).
- **Branching**: `main` tracks the last release and stays the default/landing branch; `develop` is the integration branch and the base for task branches. See `Documentation~/git-strategy.md`.
- **M2 in flight**. `SetLink` done; **next up is Awaitables** (`TweenAwaiter` on Unity 6 native `Awaitable`, `WaitForCompletion`/`WaitForKill`/`WaitForPosition`), then the zero-alloc fast paths. See `Documentation~/ROADMAP.md`.

## This session (2026-09-20, M2.1 SetLink)

Shipped `SetLink(GameObject, LinkBehavior)` on both builders — the pooled-object footgun `SetTarget`
auto-kill cannot cover, since pooling disables objects instead of destroying them. Behaviors:
`KillOnDestroy` (default), `KillOnDisable`, `PauseOnDisable`, `PauseOnDisableResumeOnEnable`,
`RestartOnEnable`; all still kill on destruction. Semantics and rationale: `api/builders.md`, ADR 0012.

Three decisions a future session should not silently reverse:
- **Polling, not a helper component** (ADR 0012). The poll sits before the status gate in
  `TickActiveCore`, so a link-paused record is still evaluated and can resume.
- **Link state is seeded active**, so a tween started on an already-inactive object sees a disable edge
  on its first tick.
- **Links are root-level**: appending a linked builder or nested sequence throws, because in the
  parent-sequence model a child has no independent status to pause or restart.

`Tests/Editor/LinkTests.cs` (14 tests); the `GameObject` stub gained `SetActive`/`activeInHierarchy`.

## Test status

- Compile-check harness: **230 green + 218 in the FEATHERTWEEN_RELEASE leg** (2026-09-20, PR #4;
  216/204 before `LinkTests`). 0 skipped in both legs, so all 14 link tests really ran.
- Note for future sessions in this container: there is no .NET SDK here and the egress policy blocks
  the installer, so the harness cannot be run locally. CI on a PR is the only way to execute it.
- Doc-check leg (CS1591 as error on Runtime): green; wired into CI.
- Unity: Showcase manual protocol + zero-alloc profiler check passed by user 2026-07-18; edit-mode tick guard sanity-checked in the editor (enter/exit play, edit-mode tweens advance at normal speed).

## Known issues / tech debt

- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- Abandoned (never-started) `SequenceBuilder` pins child store slots until `TweenStore.Reset()` — documented behavior (builders.md, CHANGELOG known limitations), LeakDetector warning is the mitigation.
- Two micro-opts folded into the M2 fast-paths phase entry (`planning/phases.md`): the `TransferCallbacks` duplication between `TweenBuilderBuffer`/`SequenceBuilderBuffer`, and the `Interpolators.Get<T>()` dictionary lookup per `Build()`.
- **Branch protection not set**: `develop` exists (created from `main` 2026-09-20). `main` stays the default branch by design — it is the consumer-facing landing page. Still needs, via GitHub settings: no direct push to either branch, PRs targeted at `develop`, CI required to merge. Until then the model is convention, not enforcement.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `Documentation~/guides/testing.md`.
