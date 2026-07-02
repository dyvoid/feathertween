# PATween

<!-- Dynamic badges (CI status, package version, last commit, issues) do not
     work on private repos: GitHub proxies README images anonymously, so the
     Actions badge always reads "no status" and shields.io gets 404s.
     Restore them if this repo ever goes public:
     [![CI](https://github.com/dyvoid/patween/actions/workflows/ci.yml/badge.svg)](https://github.com/dyvoid/patween/actions/workflows/ci.yml)
-->
[![Unity](https://img.shields.io/badge/Unity-6000.3%2B-black?logo=unity)](https://unity.com)

A robust, minimal C# tween engine for Unity. Compositional sequences, static-method API, struct handles, PlayerLoop runner.

## Status

Early development — M1 (Core) is in progress. Phases 1.1–1.7 are complete; 1.8 onward is planned. Expect breaking changes until M1 ships.

## Getting Started

PATween is distributed as a UPM package.

1. Add the repository as an embedded or scoped-registry package in your Unity project.
2. Install the `com.unity.test-framework.performance` package if you want to run the performance benchmarks (test-only dependency).
3. Open `Window > Package Manager > PATween > Samples` and import **BasicUsage** for a quick demo.

See [`docs/guides/testing.md`](docs/guides/testing.md) for consumer-project setup details.

## Project Structure

```
Runtime/              Core engine (PATween.asmdef)
Editor/               Inspector drawers and debugger (PATween.Editor.asmdef)
Tests/
  Editor/             EditMode correctness tests
  Runtime/            PlayMode tests
  Performance/        Allocation guards + throughput benchmarks
Samples~/             Importable package samples (BasicUsage demo)
docs/                 Architecture, decisions, and guides
AGENTS.md             AI agent instructions and conventions
PICKUP.md             Where the last session left off — active work only, not the backlog
```

## Documentation

- [Design](docs/architecture/design.md) — Goals, non-goals, and locked anchors
- [Public API](docs/api.md) — API reference
- [Architecture](docs/architecture/overview.md) — Internal design
- [Editor & integration](docs/guides/editor.md) — Inspector and editor workflows
- [Implementation plan](docs/implementation.md) — Milestones and performance plan
- [Roadmap](docs/ROADMAP.md) — Feature candidates and status
- [Reference & comparison](docs/reference.md) — Comparison with DOTween, GSAP, LitMotion
- [ADRs](docs/adr/) — Architectural decision records

## Agent guide

See [AGENTS.md](AGENTS.md) for AI agent instructions.
