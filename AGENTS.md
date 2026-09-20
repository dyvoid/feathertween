# FeatherTween — Agent Guide

Single source of truth for AI agents working on this codebase.

> **Session state lives in `Documentation~/PICKUP.md`**. Read it at the start of every session for current position, what's done, and what's next. Update it at the end of every session. This `AGENTS.md` holds stable conventions; `PICKUP.md` holds volatile state. (Kept under `Documentation~` so Unity does not import it as a package asset.)

## Stack

Unity 6000.3 UPM package, C# 9+, Unity Test Framework (EditMode + PlayMode). The repo root is the
package; layout and per-folder purpose: [`README.md`](README.md#project-structure).

## Architecture

Static-method API (`FT.Move(...)`) over a builder/handle split, pooled internal storage, a
`MonoBehaviour`-free PlayerLoop runner, and a parent-sequence timeline model. Locked anchors and
the reasoning behind each: [`Documentation~/architecture/design.md`](Documentation~/architecture/design.md).
The invariants below are the parts an agent must not break without an ADR.

## Invariants (Do Not Break)

1. `TweenBuilder<T>` and `Tween` remain structs. Never convert to classes.
2. Static-method API only. Do not add DOTween-style extension methods on `Transform` / `CanvasGroup` / etc. in core.
3. The runner must remain `MonoBehaviour`-free.
4. Handles must carry a generation id for safe use-after-free detection.
5. Pooled backing classes must have a finalizer that enqueues a leak-detection id; runner drains on main thread.
6. Safe mode (try/catch around step and callbacks) stays in core, default `true` in Editor, `false` in release.
7. The repo root IS the UPM package — Unity imports every file and DLL in it. Anything Unity must not see (dev tooling, .NET projects, build output) lives in a `~`-suffixed folder (like `Samples~`, `tools~`) or a dot-folder (like `.github`). Never generate or commit DLLs/`bin`/`obj` in a Unity-visible path.
8. Samples and docs call the static API as `FT.To(...)`, `FT.Sequence(...)`. Never `using static dyvoid.FeatherTween.FT;` (see `Documentation~/guides/conventions.md`).

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
- Commit directly to `main` or `develop`
- Delete or rename files without being asked
- Change architecture without recording an ADR in `Documentation~/adr/`
- Add third-party dependencies without explicit instruction
- Break any invariant listed in the section below

## Code Style

Apply the **unity dev skill** when writing Unity C# here. Canonical written conventions, including
the public API shape rules from ADR 0011: [`Documentation~/guides/conventions.md`](Documentation~/guides/conventions.md).

## Adding New Features

- **New `IInterpolator<T>`**: implement `IInterpolator<T>`, register in core bootstrap. Must be blittable-friendly.
- **New ease**: add factory to `Easing`, return `EaseRef`. No plumbing changes.
- **New builder method**: extend `TweenBuilder<T>`, return `this`. Keep alias semantics (mutate shared backing record).
- **New shortcut**: add static method on `FT`. Follow existing overload pattern with zero-alloc target-capture variants.

## Testing

Principle: **allocation guards hard-fail** (zero managed bytes in steady-state ticking),
**throughput benchmarks are report-only** (noisy, never gate a build). New code needs tests.

Structure, run instructions, and consumer-project setup: [`Documentation~/guides/testing.md`](Documentation~/guides/testing.md).

## Documentation Discipline

Keep state and design docs in sync with the code. Update as part of the same change, not later.

- **Every session**: update `Documentation~/PICKUP.md` (current position, done, next up, test status) as the closing step.
- **Finishing a phase or milestone**: update `Documentation~/PICKUP.md` and reconcile the affected docs (`Documentation~/planning/phases.md` phase status, `Documentation~/api/` if the public surface changed, `Documentation~/architecture/overview.md` or `Documentation~/architecture/sequence.md` if internals changed). Move the milestone tag only on explicit user go-ahead.
- **Any architectural decision or deviation from a doc**: add or update an ADR in `Documentation~/adr/` and its `README.md` index. Do not let code silently contradict a doc.
- **New public API**: document it in `Documentation~/api/` in the same change that adds it.
- **New test category or required dependency**: document it in `Documentation~/guides/testing.md` and in `Documentation~/PICKUP.md` consumer reminders.

If a change touches behavior described in a doc and the doc is not updated, the change is incomplete.

## Git Workflow

Full rules: [`Documentation~/git-strategy.md`](Documentation~/git-strategy.md). In brief:

- `main` equals the last tagged release. **`develop` is the integration branch** — branch from it,
  merge back into it.
- Short-lived work branches (`task/2.x-name`, `fix/...`), rebased onto `develop`, fast-forward merge.
- No squashing — atomic commits are the audit trail.

## Key Documents

| Document | Purpose |
| -------- | ------- |
| `Documentation~/PICKUP.md` | Where the last session left off — active work only |
| `Documentation~/ROADMAP.md` | Feature status and what is planned next |
| `Documentation~/planning/phases.md` | Milestone/phase plan and exit criteria |
| `Documentation~/architecture/design.md` | Goals, non-goals, locked anchors |
| `Documentation~/architecture/overview.md` | Internal design (storage, runner, tick) |
| `Documentation~/api/index.md` | Public API quickstart and map to the rest of `api/` |
| `Documentation~/guides/conventions.md` | Code style and API shape conventions |
| `Documentation~/adr/README.md` | Index of architectural decision records |

The full tree — sequence internals, performance plan, testing, editor, engine comparison,
influences, risks — is indexed from [`README.md`](README.md#documentation).
