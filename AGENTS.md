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

```text
Tests/
  Editor/          -- EditMode tests ( builder validation, lifecycle, loops, easing, leak detection )
  Runtime/         -- PlayMode tests ( PlayerLoop ticking, update order, auto-kill timing )
  Performance/     -- EditMode tests ( allocation guards + throughput benchmarks )
```

Run via Unity Test Runner or `Unity -runTests`.

### Performance & allocation testing

Two distinct concerns, deliberately separated by intent:

- **Allocation guards (hard fail).** Steady-state ticking must allocate zero managed bytes. Measured deterministically with `System.GC.GetAllocatedBytesForCurrentThread()` around a synchronous `PATweenRunner.ManualTick` loop (no frame/thread noise). These protect the core pooled, zero-alloc design promise and are CI-safe hard failures.
- **Throughput benchmarks (report only).** Tick cost at 1k/10k tweens and `Start()` cost, measured with `Unity.PerformanceTesting`'s `Measure.Method`. Numbers are noisy (machine/thermal dependent) and must never gate a build. Run locally when investigating a perf question.

The suite lives in an isolated `Tests/Performance` EditMode asmdef so the slow/noisy benchmarks do not run alongside the fast correctness tests and so the `Unity.PerformanceTesting` dependency is contained.

Why EditMode + `ManualTick` instead of PlayMode: `ManualTick` advances the runner synchronously, giving deterministic, frame-independent allocation measurements.

### Required test dependency

The performance asmdef references `Unity.PerformanceTesting`. The **consuming project** must have the package installed (it is test-only). Add to the consuming project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unity.test-framework.performance": "3.0.3"
  }
}
```

Use the version that resolves for your Unity 6000.3 install if 3.0.3 is unavailable.

### Tests not showing in Test Runner

When PATween is consumed as a UPM package (linked via `file:`), Unity hides its tests by default. The test asmdefs use the `UNITY_INCLUDE_TESTS` define constraint, which is only active for packages listed as `testables`. To see the tests, add the package to the **consuming project's** `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.patween.patween": "file:../path/to/Tween"
  },
  "testables": [
    "com.patween.patween"
  ]
}
```

The name must match `package.json` (`com.patween.patween`). Embedding the source under `Assets/` instead would surface tests automatically, but the `testables` entry is the correct mechanism for the package workflow.

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
