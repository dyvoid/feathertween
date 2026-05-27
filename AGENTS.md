# PATween — Agent Guide

Single source of truth for AI agents working on this codebase.

## Stack

- **Unity**: 6000.3.8f1
- **Type**: UPM package (embedded or registry)
- **Language**: C# 9+ (Unity)
- **Test framework**: Unity Test Framework (EditMode + PlayMode)
- **Target platforms**: All Unity supports ( Burst path kept open for future )

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

Intended structure (created when implementation starts):

```text
Tests/
  Editor/          -- EditMode tests ( builder validation, leak detection )
  Runtime/         -- PlayMode tests ( playback, sequences, callbacks, await )
```

Run via Unity Test Runner or `Unity -runTests`.

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
| `docs/adrs/` | Architectural decision records |
