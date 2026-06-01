# PATween — Agent Guide

Single source of truth for AI agents working on this codebase.

> **Session state lives in `PROGRESS.md`** (repo root). Read it at the start of every session for current position, what's done, and what's next. Update it at the end of every session. This `AGENTS.md` holds stable conventions; `PROGRESS.md` holds volatile state.

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

## Invariants (Do Not Break)

1. `TweenBuilder<T>` and `Tween` remain structs. Never convert to classes.
2. Static-method API only. Do not add DOTween-style extension methods on `Transform` / `CanvasGroup` / etc. in core.
3. The runner must remain `MonoBehaviour`-free.
4. Handles must carry a generation id for safe use-after-free detection.
5. Pooled backing classes must have a finalizer that enqueues a leak-detection id; runner drains on main thread.
6. Safe mode (try/catch around step and callbacks) stays in core, default `true` in Editor, `false` in release.

## Code Style

- Tab size 4, keep tabs.
- Braces on own line; always use braces for `if`/`else`.
- PascalCase classes, structs, enums, methods, properties, events.
- camelCase private fields, parameters.
- Interfaces prefix with `I`.
- Boolean names affirmative/negative (`isAlive`, `hasAmmo`).
- Fields never public/internal; expose via `[SerializeField]` or public properties.
- Public properties have explicit `get`/`set`.
- One statement / one declaration per line.
- Attributes one per line for classes/methods.
- Dispose/clean up: instantiated objects, coroutines, event listeners, `Resources` assets, IO files.

## Adding New Features

- **New `IInterpolator<T>`**: implement `IInterpolator<T>`, register in core bootstrap. Must be blittable-friendly.
- **New ease**: add factory to `Easing`, return `EaseRef`. No plumbing changes.
- **New builder method**: extend `TweenBuilder<T>`, return `this`. Keep alias semantics (mutate shared backing record).
- **New shortcut**: add static method on `PATween`. Follow existing overload pattern with zero-alloc target-capture variants.

## Testing

Three asmdefs: `Tests/Editor` (EditMode correctness), `Tests/Runtime` (PlayMode), `Tests/Performance` (EditMode allocation guards + throughput).

Principle: **allocation guards hard-fail** (zero managed bytes in steady-state ticking, CI-safe), **throughput benchmarks are report-only** (noisy, never gate a build).

Full run instructions, consumer-project setup (`testables` + perf package), and Test Runner troubleshooting: see `docs/testing.md`.

## Documentation Discipline

Keep state and design docs in sync with the code. Update as part of the same change, not later.

- **Every session**: update `PROGRESS.md` (current position, done, next up, test status) as the closing step.
- **Finishing a phase or milestone**: update `PROGRESS.md` and reconcile the affected docs (`docs/implementation.md` phase status, `docs/api.md` if the public surface changed, `docs/architecture.md` if internals changed). Move the milestone tag only on explicit user go-ahead.
- **Any architectural decision or deviation from a doc**: add or update an ADR in `docs/adrs/` and its `README.md` index. Do not let code silently contradict a doc.
- **New public API**: document it in `docs/api.md` in the same change that adds it.
- **New test category or required dependency**: document it in `docs/testing.md` and in `PROGRESS.md` consumer reminders.

If a change touches behavior described in a doc and the doc is not updated, the change is incomplete.

## Git Workflow

- **Branching**: feature branches from `develop`; name `feature/1.x-phase-name`.
- **Commits**: frequent, natural developer commits. Do not squash.
- **Feature → develop**: merge with `--no-ff`.
- **Develop → main**: fast-forward (user handles).
- **Tags**: milestone releases only.
- **Authority**: merge to `develop` only on explicit user go-ahead.

## Key Documents

| Document | Purpose |
| -------- | ------- |
| `docs/design.md` | Goals, non-goals, locked anchors |
| `docs/api.md` | Public API reference |
| `docs/architecture.md` | Internal design |
| `docs/testing.md` | Test structure, running, consumer setup |
| `docs/adrs/` | Architectural decision records |
