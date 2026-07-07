# PATween — Agent Guide

Single source of truth for AI agents working on this codebase.

> **Session state lives in `PICKUP.md`** (repo root). Read it at the start of every session for current position, what's done, and what's next. Update it at the end of every session. This `AGENTS.md` holds stable conventions; `PICKUP.md` holds volatile state.

## Stack

- **Unity**: 6000.3.8f1
- **Type**: UPM package (embedded or registry)
- **Language**: C# 9+ (Unity)
- **Test framework**: Unity Test Framework (EditMode + PlayMode)
- **Target platforms**: All Unity supports ( Burst path kept open for future )

## Package Structure

```text
Runtime/              -- PATween.asmdef (core, Editor references allowed for EditMode)
Editor/               -- PATween.Editor.asmdef (drawers, debugger)
Tests/
  Editor/             -- EditMode tests asmdef
  Runtime/            -- PlayMode tests asmdef
  Performance/        -- EditMode allocation guards + throughput asmdef
Samples~/             -- importable package samples (BasicUsage demo)
```

## AI Skill Reference

When writing Unity C# for this project, apply the **unity dev skill**.

## Architecture Summary

- **Static-method API**: `PATween.Move(transform, ...)` — not extension methods on Unity types. One discoverable entry point; avoids namespace pollution.
- **Builder/handle split**: `PATween.X(...)` returns a mutable `TweenBuilder<T>` struct (aliases share a pooled backing record). `.Start()` returns an immutable `Tween` handle (`id, generation`). After `.Start()`, builder aliases are invalid.
- **Pooled internal storage**: backing classes are pooled in v1; public handles insulate users from the swap.
- **PlayerLoop runner**: no `MonoBehaviour`. Injection into `Update`, `LateUpdate`, `FixedUpdate`. Edit-mode via `EditorApplication.update`.
- **Parent-sequence model**: every animation has `_start`, `_end`, `_timeScale`, `_parent`. A hidden root sequence owns top-level tweens. `Sequence` is the public type.
- **Ease as value type**: `EaseRef` produced by `Easing.X(...)` factories. Parameters travel with the ease; tween stores one `EaseRef`.
- **SoA-friendly internal layout**: keeps a future Burst/Jobs path cheap. Not a public concern.

Full design and locked anchors: `docs/architecture/design.md` and `docs/architecture/overview.md`.

## Invariants (Do Not Break)

1. `TweenBuilder<T>` and `Tween` remain structs. Never convert to classes.
2. Static-method API only. Do not add DOTween-style extension methods on `Transform` / `CanvasGroup` / etc. in core.
3. The runner must remain `MonoBehaviour`-free.
4. Handles must carry a generation id for safe use-after-free detection.
5. Pooled backing classes must have a finalizer that enqueues a leak-detection id; runner drains on main thread.
6. Safe mode (try/catch around step and callbacks) stays in core, default `true` in Editor, `false` in release.
7. The repo root IS the UPM package — Unity imports every file and DLL in it. Anything Unity must not see (dev tooling, .NET projects, build output) lives in a `~`-suffixed folder (like `Samples~`, `tools~`) or a dot-folder (like `.github`). Never generate or commit DLLs/`bin`/`obj` in a Unity-visible path.
8. Samples and docs must call the static API as `PATween.To(...)`, `PATween.Sequence(...)`, etc. Never use `using static PATween.PATween;`; it shadows Unity built-in types such as `Color` and `Image` and produces `CS0119` errors.

## AI Instructions

### You can do these freely
- Write, edit, and refactor code that follows the patterns already in the codebase
- Create new files consistent with existing conventions
- Update documentation to match code changes
- Add tests for new or existing functionality

### These need human review before they land
- `.gitignore` and `.gitattributes`
- Authentication, authorization, or anything touching secrets
- Dependency changes (`package.json`, lockfiles, package manifests)
- Refactors that cut across multiple modules

### Do not do these
- Commit directly to `main`
- Delete or rename files without being asked
- Change architecture without recording an ADR in `docs/adr/`
- Add third-party dependencies without explicit instruction
- Break any invariant listed in the section below

## Code Style

Apply the unity dev skill (see above). Canonical written conventions: `docs/guides/conventions.md`.

## Adding New Features

- **New `IInterpolator<T>`**: implement `IInterpolator<T>`, register in core bootstrap. Must be blittable-friendly.
- **New ease**: add factory to `Easing`, return `EaseRef`. No plumbing changes.
- **New builder method**: extend `TweenBuilder<T>`, return `this`. Keep alias semantics (mutate shared backing record).
- **New shortcut**: add static method on `PATween`. Follow existing overload pattern with zero-alloc target-capture variants.

## Testing

Three asmdefs: `Tests/Editor` (EditMode correctness), `Tests/Runtime` (PlayMode), `Tests/Performance` (EditMode allocation guards + throughput).

Principle: **allocation guards hard-fail** (zero managed bytes in steady-state ticking, CI-safe), **throughput benchmarks are report-only** (noisy, never gate a build).

Full run instructions, consumer-project setup (`testables` + perf package), and Test Runner troubleshooting: see `docs/guides/testing.md`.

## Documentation Discipline

Keep state and design docs in sync with the code. Update as part of the same change, not later.

- **Every session**: update `PICKUP.md` (current position, done, next up, test status) as the closing step.
- **Finishing a phase or milestone**: update `PICKUP.md` and reconcile the affected docs (`docs/planning/phases.md` phase status, `docs/api/` if the public surface changed, `docs/architecture/overview.md` or `docs/architecture/sequence.md` if internals changed). Move the milestone tag only on explicit user go-ahead.
- **Any architectural decision or deviation from a doc**: add or update an ADR in `docs/adr/` and its `README.md` index. Do not let code silently contradict a doc.
- **New public API**: document it in `docs/api/` in the same change that adds it.
- **New test category or required dependency**: document it in `docs/guides/testing.md` and in `PICKUP.md` consumer reminders.

If a change touches behavior described in a doc and the doc is not updated, the change is incomplete.

## Git Workflow

See [`docs/git-strategy.md`](docs/git-strategy.md) for full branching, merging, and commit rules. In brief:

- Trunk-based: single `main` branch, short-lived task/fix branches (`task/1.x-phase-name`, `fix/...`).
- Rebase onto `main`, fast-forward merge only — no merge commits.
- No squashing — atomic commits are the audit trail.

## Key Documents

| Document | Purpose |
| -------- | ------- |
| `docs/architecture/design.md` | Goals, non-goals, locked anchors |
| `docs/api/index.md` | Public API quickstart and map |
| `docs/architecture/overview.md` | Internal design |
| `docs/architecture/sequence.md` | Sequence internals |
| `docs/architecture/performance.md` | Allocation budget and benchmark methodology |
| `docs/planning/phases.md` | Milestone/phase plan |
| `docs/planning/risks.md` | Risks and open questions |
| `docs/guides/conventions.md` | Code style conventions |
| `docs/guides/testing.md` | Test structure, running, consumer setup |
| `docs/guides/editor.md` | Editor & integration |
| `docs/design/comparison.md` | Engine comparison |
| `docs/design/influences.md` | Design influences |
| `docs/git-strategy.md` | Branching, merging, commit rules |
| `docs/ROADMAP.md` | Feature candidates, planned work, and status |
| `PICKUP.md` | Where the last session left off — active work only, not the backlog |
| `docs/adr/` | Architectural decision records |
