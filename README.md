# FeatherTween

[![CI](https://github.com/dyvoid/feathertween/actions/workflows/ci.yml/badge.svg)](https://github.com/dyvoid/feathertween/actions/workflows/ci.yml)
[![GitHub last commit](https://img.shields.io/github/last-commit/dyvoid/feathertween)](https://github.com/dyvoid/feathertween/commits/main)
[![GitHub issues](https://img.shields.io/github/issues/dyvoid/feathertween)](https://github.com/dyvoid/feathertween/issues)
[![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity)](https://unity.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Lightweight tweening for Unity: animate transforms, UI, colors, or any value with a single line of code, then chain tweens into sequences you can loop, reverse, pause, and scrub. No components to add, no per-frame garbage.

## Getting Started

FeatherTween is distributed as a UPM package.

> **Pre-1.0**: minor versions may contain breaking changes, as `0.x` implies in semver. Every one is
> listed in [`CHANGELOG.md`](CHANGELOG.md) with a migration note. Pin a tag if you need stability.

1. Add the repository as an embedded or scoped-registry package in your Unity project.
2. Open `Window > Package Manager > FeatherTween > Samples` and import **Showcase** — a guided tour of the whole feature set as one scrubbable, captioned timeline with a player panel.

Running FeatherTween's own test suites additionally requires consumer-project setup
(`testables` plus a test-only performance package): see [`Documentation~/guides/testing.md`](Documentation~/guides/testing.md).

## Usage

The public API lives in the `dyvoid.FeatherTween` namespace, and the entry point is the
static class `FT`:

```csharp
using dyvoid.FeatherTween;

FT.To(() => value, v => value = v, to: 10f, duration: 1f).Start();
FT.FromTo(v => value = v, 0f, 10f, 1f).Start();   // both endpoints known: setter only
FT.Move(transform, new Vector3(4f, 0f, 0f), 1f).Start();
```

Prefer the full product name at call sites? Add a file-scoped alias — `FT` stays the
canonical type, and `FeatherTween` becomes an equivalent handle in that file:

```csharp
using FeatherTween = dyvoid.FeatherTween.FT;

FeatherTween.To(() => value, v => value = v, 10f, 1f).Start();
```

Avoid `using static dyvoid.FeatherTween.FT;` — it dumps every shortcut into scope and
shadows common Unity/.NET types (`Color`, `Image`, `Text`, …), producing cryptic `CS0119`
errors.

## Project Structure

```
Runtime/              Core engine (FeatherTween.asmdef)
Editor/               Edit-mode runner and store bootstrap (FeatherTween.Editor.asmdef)
Tests/
  Editor/             EditMode correctness tests
  Runtime/            PlayMode tests
  Performance/        Allocation guards + throughput benchmarks
Samples~/             Importable package sample (Showcase)
tools~/               .NET stub harness — compile check + EditMode tests without Unity
Documentation~/       Architecture, decisions, planning, and guides
AGENTS.md             AI agent instructions and conventions (CLAUDE.md imports it)
```

## Documentation

- [API Guide](Documentation~/api/index.md) — Public API quickstart and reference
- [Design](Documentation~/architecture/design.md) — Goals, non-goals, and locked anchors
- [Architecture](Documentation~/architecture/overview.md) — Internal design
- [Sequence Design](Documentation~/architecture/sequence.md) — Sequence internals
- [Performance Plan](Documentation~/architecture/performance.md) — Allocation budget and benchmarks
- [Editor & integration](Documentation~/guides/editor.md) — Inspector and editor workflows
- [Testing](Documentation~/guides/testing.md) — Test structure and consumer setup
- [Milestones](Documentation~/planning/phases.md) — Phase-by-phase implementation plan
- [Roadmap](Documentation~/ROADMAP.md) — Feature candidates and status
- [Engine Comparison](Documentation~/design/comparison.md) — Comparison with DOTween, GSAP, LitMotion
- [ADRs](Documentation~/adr/) — Architectural decision records

## Agent guide

See [AGENTS.md](AGENTS.md) for AI agent instructions.
