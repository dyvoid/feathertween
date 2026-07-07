# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-07 (1.14 tests Unity-verified and merged; hardening pass next)

## Current position

- **Milestone**: M1 (Core), production cut. See `docs/planning/phases.md`.
- **Done through**: Phase 1.14 (dev acceptance) merged to `main`. Unity run 2026-07-07: 196 tests green including the new 10k-tween and 1k×10-child-sequence zero-alloc guards. **Composed demo visual pass**: confirm it has been eyeballed in Play mode; if not, it is the one open 1.14 exit item.
- **Phase restructure (user decision, 2026-07-07)**: release hygiene (LICENSE, CHANGELOG, XML docs) split out of 1.14 into a new **1.15**, which now carries the v0.1 tag. Between 1.14 and 1.15 sits a hardening pass (full test sweep + code review) — everything must be flawless before 1.15 documents it.
- **Remaining M1**: hardening pass → 1.15 (hygiene + docs, v0.1 tag).
- **Branch**: trunk-based on `main`; short-lived branches `task/1.x-phase-name` / `fix/...`, fast-forward merge.

## Done

- **Phases 1.1–1.12** merged to `main`: storage/handle scaffold, PlayerLoop runner, builder/handle split, generic tween core, ease system, From/FromTo, loops/delays/reverse, sequence builder, callbacks, seek/control, typed shortcuts, filters/bulk ops, storage surgery.
- **Documentation pass (this session)**: reorganized docs into `design/`, `architecture/`, `api/`, `guides/`, and `planning/`; removed monolithic section numbering and production-cut reshuffle noise; split `implementation.md` and `reference.md` into focused files.
- **Sample static-import fix**: removed `using static PATween.PATween;` from `Samples~/BasicUsage/PATweenDemo.cs` and `Samples~/SequenceDemo/PATweenSequenceDemo.cs`; calls are now `PATween.To(...)`, `PATween.Sequence(...)`, and `PATween.From(...)`. This fixes the `CS0119` collision with `UnityEngine.Color` that broke the compile-check CI. Added AGENTS.md invariant (#8) and `docs/guides/conventions.md` rule.
- **README badges**: enabled CI status, last commit, and issues shields; updated Status and project structure to reflect phases 1.1–1.12 done and both samples.
- Earlier history: see git log.

## In flight

- Nothing half-built. M1 dev work (1.1–1.14) is complete and merged.

## Next up

1. **Hardening pass** (user-mandated gate before 1.15): full test sweep + deep code review of the whole M1 surface; fix everything found. Candidate entry points: the "Known issues / tech debt" list below, edge-case coverage (Yoyo+Reverse composition, seek across EveryLoop delays, nested-sequence kill semantics), and a fresh end-to-end review of Runtime/.
2. Phase 1.15 — Release hygiene and documentation: LICENSE, CHANGELOG.md, XML docs on every public type/member, reconcile all docs, v0.1 tag.

## Infra (2026-07-02)

- **.NET stub harness** (`tools~/compile-check/`): compiles Runtime + Samples + EditMode tests against UnityEngine stubs, runs the suite via NUnitLite in <1s. Unity-only tests carry `[Category("RequiresUnity")]`.
- **CI**: `.github/workflows/ci.yml` runs the harness on push to `main` and PRs. Branch protection (require CI, no direct push) still to be enabled on GitHub by the user.
- **ADR 0010**: `SetDefaults` duration parameter removed (dead code — creation methods require explicit duration).
- **Roadmap refinement (2026-07-03)**: sequence `SetLoops` + global/per-phase time scale added to 1.10; LICENSE/CHANGELOG/XML-docs added to the 1.14 gate (since moved to 1.15, 2026-07-07); `SetLink` and Awaitables (Unity 6 native `Awaitable`) promoted to Planned in M2; Editor preview window promoted M4→M3; blendable tweens flagged needs-ADR; cross-timeline `globalTime` marked drop-unless-needed.

## Open questions / decisions pending

- `AddLabel(name, Position)` resolves at definition time (only `Insert`/`AddPause` defer label resolution to `Start()`); duplicate label names throw.

## Known issues / tech debt

- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- An abandoned (never-started) `SequenceBuilder` leaks its already-allocated child store slots until the next `TweenStore.Reset()`; the LeakDetector warns via finalizer.

### From code review (2026-07-06, verified + triaged 2026-07-07)

Resolved 2026-07-07:

- ~~Incremental loop `GetCycleEnds` O(cycleIndex)~~ — fixed with a cached cycle base (O(1) amortized; backward jumps recompute once). No interface change was needed, contrary to the review's suggestion.
- ~~Elastic/BounceExact parameters ignored~~ — implemented parametric Penner elastic (amplitude/period) and amplitude-scaled BounceExact; defaults reduce exactly to the former hardcoded constants.
- ~~Docs claimed `TweenData<T>` pooling that doesn't exist~~ — `docs/architecture/performance.md` amended to state reality; pooling itself is scheduled (below).
- ~~`Easing.Linear()` "allocates a struct per call"~~ — **struck**: `EaseRef` is a readonly struct; `new` on it is stack construction, zero heap alloc. The finding misread C# struct semantics.

Resolved in phase 1.12 (2026-07-07): `TweenData<T>` pooling, swap-remove active lists, target multimap — all landed (see Done). The 8-callback-list-field collapse was **deliberately dropped**: pooled reuse amortizes the per-instance cost, so the collapse would save memory, not allocations; revisit only if record memory shows up in profiling (M2+).

Remaining tracked debt:

- ~~Safe mode absent~~ — implemented in 1.13 (this branch).
- **Builder buffer callback duplication** (`TransferCallbacks` + callback lists duplicated between `TweenBuilderBuffer`/`SequenceBuilderBuffer`) — both copies were touched in 1.12 (method-group alloc fix) but the extraction is still pending; do it next time either changes.
- **`Interpolators.Get<T>()` dictionary lookup per `Build()`** — valid micro-opt (generic static cache), but the cache must handle `Interpolators.Reset()` re-registration (version stamp) or interpolator-swapping tests break. Low urgency; fold into M2 fast paths.

## Test status

- Compile-check harness: 184 tests green + 172 in the PATWEEN_RELEASE leg (2026-07-07, includes phase 1.13).
- Unity (Editor + Runtime + Performance): verified 2026-07-07 through phase 1.12; **1.13 not yet run in Unity**.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `docs/guides/testing.md`.
