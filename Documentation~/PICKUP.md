# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-09-20 (documentation audit; branch model moved to `main`/`develop`)

## Current position

- **Milestone**: M1 **closed**. v0.1.0 tagged on `main` 2026-07-18; public API declared stable (semver from here).
- Unity manual test protocol for the Showcase (1.17) passed by user, including the zero-alloc profiler check (FeatherTween PlayerLoop rows at 0 B GC Alloc across chapters 1–7).
- **Branching**: `main` tracks the last release and stays the default/landing branch; `develop` is the integration branch and the base for task branches. See `Documentation~/git-strategy.md`.
- **Next**: M2 begins — `SetLink` + Awaitables first (production stickiness), then zero-alloc fast paths. See `Documentation~/ROADMAP.md`.

## This session (2026-09-20, documentation audit)

Audited the doc tree against the code; 21 findings fixed. The ones that change how work is done:

- **Branch model** (user decision): `main` = last release, `develop` = integration, task branches off
  `develop`, release = fast-forward `main` + tag. `git-strategy.md` rewritten; CI now builds `develop` too.
- **`api/awaiters.md` and `guides/editor.md` documented unshipped API as shipped** — both now marked as
  planned surface; `editor.md` describes what `FeatherTween.Editor` actually contains.
- Editor preview is **M3** everywhere (was M4 in three docs). Roslyn analyzer is an **M2 candidate**
  (was promised as a guarantee in three docs, planned nowhere). `.prompts/` + `ai-assisted:` commit
  trailer dropped — zero commits ever used them.
- `phases.md` preamble corrected (17 phases, closes at 1.17, no `m1.x` tags); the M2 fast-path design
  decision moved into the M2 section.
- Added `CLAUDE.md` (`@AGENTS.md`) so the agent guide auto-loads in Claude Code. AGENTS.md 133 → 105 lines.

## Test status

- Compile-check harness: **216 green + 204 in the FEATHERTWEEN_RELEASE leg** (2026-07-18, includes the four review-fix tests).
- Doc-check leg (CS1591 as error on Runtime): green; wired into CI.
- Unity: Showcase manual protocol + zero-alloc profiler check passed by user 2026-07-18; edit-mode tick guard sanity-checked in the editor (enter/exit play, edit-mode tweens advance at normal speed).

## Known issues / tech debt

- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- Abandoned (never-started) `SequenceBuilder` pins child store slots until `TweenStore.Reset()` — documented behavior (builders.md, CHANGELOG known limitations), LeakDetector warning is the mitigation.
- Two micro-opts folded into the M2 fast-paths phase entry (`planning/phases.md`): the `TransferCallbacks` duplication between `TweenBuilderBuffer`/`SequenceBuilderBuffer`, and the `Interpolators.Get<T>()` dictionary lookup per `Build()`.
- **Branch protection not set**: `develop` exists (created from `main` 2026-09-20). `main` stays the default branch by design — it is the consumer-facing landing page. Still needs, via GitHub settings: no direct push to either branch, PRs targeted at `develop`, CI required to merge. Until then the model is convention, not enforcement.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `Documentation~/guides/testing.md`.
