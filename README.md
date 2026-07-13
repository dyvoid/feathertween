# FeatherTween

[![CI](https://github.com/dyvoid/patween/actions/workflows/ci.yml/badge.svg)](https://github.com/dyvoid/patween/actions/workflows/ci.yml)
[![GitHub last commit](https://img.shields.io/github/last-commit/dyvoid/patween)](https://github.com/dyvoid/patween/commits/main)
[![GitHub issues](https://img.shields.io/github/issues/dyvoid/patween)](https://github.com/dyvoid/patween/issues)
[![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity)](https://unity.com)

A robust, minimal C# tween engine for Unity. Compositional sequences, static-method API, struct handles, PlayerLoop runner.

## Status

Early development — M1 (Core) is in progress. Phases 1.1–1.12 are complete (through filters, bulk ops, and storage surgery); 1.13–1.14 remain (safe mode and acceptance). Expect breaking changes until M1 ships.

## Getting Started

FeatherTween is distributed as a UPM package.

1. Add the repository as an embedded or scoped-registry package in your Unity project.
2. Install the `com.unity.test-framework.performance` package if you want to run the performance benchmarks (test-only dependency).
3. Open `Window > Package Manager > FeatherTween > Samples` and import **Basic Usage** (tween features) or **Sequence Demo** (sequence choreography) for a quick demo.

See [`docs/guides/testing.md`](docs/guides/testing.md) for consumer-project setup details.

## Usage

The public API lives in the `Dyvoid.FeatherTween` namespace, and the entry point is the
static class `FT`:

```csharp
using Dyvoid.FeatherTween;

FT.To(() => value, v => value = v, target: 10f, duration: 1f).Start();
FT.Move(transform, new Vector3(4f, 0f, 0f), 1f).Start();
```

Prefer the full product name at call sites? Add a file-scoped alias — `FT` stays the
canonical type, and `FeatherTween` becomes an equivalent handle in that file:

```csharp
using FeatherTween = Dyvoid.FeatherTween.FT;

FeatherTween.To(() => value, v => value = v, 10f, 1f).Start();
```

Avoid `using static Dyvoid.FeatherTween.FT;` — it dumps every shortcut into scope and
shadows common Unity/.NET types (`Color`, `Image`, `Text`, …), producing cryptic `CS0119`
errors.

## Project Structure

```
Runtime/              Core engine (FeatherTween.asmdef)
Editor/               Inspector drawers and debugger (FeatherTween.Editor.asmdef)
Tests/
  Editor/             EditMode correctness tests
  Runtime/            PlayMode tests
  Performance/        Allocation guards + throughput benchmarks
Samples~/             Importable package samples (BasicUsage and SequenceDemo)
docs/                 Architecture, decisions, and guides
AGENTS.md             AI agent instructions and conventions
PICKUP.md             Where the last session left off — active work only, not the backlog
```

## Documentation

- [API Guide](docs/api/index.md) — Public API quickstart and reference
- [Design](docs/architecture/design.md) — Goals, non-goals, and locked anchors
- [Architecture](docs/architecture/overview.md) — Internal design
- [Sequence Design](docs/architecture/sequence.md) — Sequence internals
- [Performance Plan](docs/architecture/performance.md) — Allocation budget and benchmarks
- [Editor & integration](docs/guides/editor.md) — Inspector and editor workflows
- [Testing](docs/guides/testing.md) — Test structure and consumer setup
- [Milestones](docs/planning/phases.md) — Phase-by-phase implementation plan
- [Roadmap](docs/ROADMAP.md) — Feature candidates and status
- [Engine Comparison](docs/design/comparison.md) — Comparison with DOTween, GSAP, LitMotion
- [ADRs](docs/adr/) — Architectural decision records

## Agent guide

See [AGENTS.md](AGENTS.md) for AI agent instructions.
