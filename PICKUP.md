# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-16 (phase restructure: new 1.15 = API finalization, docs/hygiene moved to 1.16; all pending semantics decisions taken — see phases.md 1.15)

## Current position

- **Milestone**: M1 (Core), production cut. See `docs/planning/phases.md`.
- **Done through**: Phase 1.14 (dev acceptance) merged to `main`, all exit items closed. Unity run 2026-07-07: 196 tests green including the new 10k-tween and 1k×10-child-sequence zero-alloc guards. Composed demo visual pass confirmed by user 2026-07-16.
- **Phase restructure (user decision, 2026-07-16)**: new **1.15 — API finalization** (implement the pending semantics decisions so the surface is frozen); release hygiene/docs moved to **1.16**, which now carries the v0.1 tag. Rationale: 1.16 documents contracts, so the contracts must be final first.
- **Remaining M1**: 1.15 (API finalization) → 1.16 (hygiene + docs, v0.1 tag).
- **Branch**: trunk-based on `main`; short-lived branches `task/1.x-phase-name` / `fix/...`, fast-forward merge.

## Done

- **Phases 1.1–1.12** merged to `main`: storage/handle scaffold, PlayerLoop runner, builder/handle split, generic tween core, ease system, From/FromTo, loops/delays/reverse, sequence builder, callbacks, seek/control, typed shortcuts, filters/bulk ops, storage surgery.
- **Documentation pass (this session)**: reorganized docs into `design/`, `architecture/`, `api/`, `guides/`, and `planning/`; removed monolithic section numbering and production-cut reshuffle noise; split `implementation.md` and `reference.md` into focused files.
- **Sample static-import fix**: removed `using static Dyvoid.FeatherTween.FT;` from `Samples~/BasicUsage/FeatherTweenDemo.cs` and `Samples~/SequenceDemo/FeatherTweenSequenceDemo.cs`; calls are now `FT.To(...)`, `FT.Sequence(...)`, and `FT.From(...)`. This fixes the `CS0119` collision with `UnityEngine.Color` that broke the compile-check CI. Added AGENTS.md invariant (#8) and `docs/guides/conventions.md` rule.
- **README badges**: enabled CI status, last commit, and issues shields; updated Status and project structure to reflect phases 1.1–1.12 done and both samples.
- Earlier history: see git log.

## In flight

_Nothing in flight._

## Recently landed

- **API consistency pass (ADR 0011) — merged to `main` (`9d0bc99`), Unity-verified 2026-07-16** — user-driven ergonomics/consistency sweep of the whole public surface. Breaking (pre-v0.1, so free):
  - `FT.FromTo(setter, from, to, duration)` — getter removed (it was dead: `SnapMode.FromTo` never read it). One lambda instead of two.
  - `TweenBuilder<T>.From(T value)` — explicit start on any builder; `FT.Fade(cg, 1f, .5f).From(0f)` is a lambda-free FromTo on shortcuts.
  - Param renames: endpoints are `from`/`to` (`toAlpha`, `uniformTo` on shortcuts); enum params named after type (`loopType`, `delayType`); timeline positions `time`, spans `seconds` (`Seek(time)`, `Sequence.Insert(time, …)`, `Position.AtTime(time)`).
  - `SetDelay` now throws on negatives (was: silent clamp) on both builders.
  - `Chain`/`Group` aliases removed; `Join(SequenceBuilder)` + `Prepend(SequenceBuilder)` added (nested sequences now first-class in all composition methods).
  - Handle symmetry: `Tween.Duration`/`Tween.TotalProgress` (mirror Sequence semantics), `Sequence.SetRemainingCycles(int|bool)` (SequenceData grew boundary-stop mirroring `TweenData<T>`; loopCount no longer readonly).
  - Docs: api pages updated to the new signatures; drift fixed where touched (`endValue:`→`to:`, `SetEase(Easing.OutCubic())` parens, nonexistent creation-side target-capture overload + awaitables marked M2-planned, phantom `Progress`/`SetId` removed); conventions.md gained "Public API shape rules"; ADR 0011 records the decision incl. the rejected subject-last ordering.
  - Tests: 197 + 185 (release leg) green on the compile-check harness; Unity Editor/PlayMode sweep confirmed by user 2026-07-16.

## Next up

1. **Phase 1.15 — API finalization**: implement the decided semantics (full list with rationale in `docs/planning/phases.md` 1.15): reverse-through-delay, dead-handle `OnKill` no-op, throw on `duration <= 0` + infinite loops, `IntInterpolator` round-to-nearest, `ForceComplete` playhead sync, manual-phase cleanup doc note, ADR 0008 `Subtract` guard verification, `AddLabel` doc note. Plus the **fast-path freeze check** (design-only: confirm M2 fast paths fit inside `TweenBuilder<T>` so shortcut signatures freeze safely). Exit: "Open questions / decisions pending" below is empty; API final for v0.1.
2. **Phase 1.16 — Release hygiene and documentation**: LICENSE, CHANGELOG.md, XML docs on every public type/member, reconcile all docs, v0.1 tag.

## Infra (2026-07-02)

- **.NET stub harness** (`tools~/compile-check/`): compiles Runtime + Samples + EditMode tests against UnityEngine stubs, runs the suite via NUnitLite in <1s. Unity-only tests carry `[Category("RequiresUnity")]`.
- **CI**: `.github/workflows/ci.yml` runs the harness on push to `main` and PRs. Branch protection (require CI, no direct push) still to be enabled on GitHub by the user.
- **ADR 0010**: `SetDefaults` duration parameter removed (dead code — creation methods require explicit duration).
- **Roadmap refinement (2026-07-03)**: sequence `SetLoops` + global/per-phase time scale added to 1.10; LICENSE/CHANGELOG/XML-docs added to the 1.14 gate (since moved to 1.15, 2026-07-07); `SetLink` and Awaitables (Unity 6 native `Awaitable`) promoted to Planned in M2; Editor preview window promoted M4→M3; blendable tweens flagged needs-ADR; cross-timeline `globalTime` marked drop-unless-needed.

## Open questions / decisions pending

_None. All semantics decisions from the 2026-07-08 review were taken by the user on 2026-07-16 and are scheduled as implementation work in phase 1.15 — see `docs/planning/phases.md` for the decided behavior of each (reverse-through-delay, dead-handle `OnKill` no-op, zero-duration+infinite-loop throw at `Start()`, `IntInterpolator` round-to-nearest, `ForceComplete` playhead sync, manual-phase cleanup doc note, ADR 0008 `Subtract` guard verification, `AddLabel` definition-time resolution confirmed)._

## Known issues / tech debt

- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- An abandoned (never-started) `SequenceBuilder` leaks its already-allocated child store slots until the next `TweenStore.Reset()`; the LeakDetector warns via finalizer. **Decided 2026-07-16: documented behavior, not a bug** — the warning is the mitigation; 1.16 documents it as a known limitation.

### From code review (2026-07-06, verified + triaged 2026-07-07)

Resolved 2026-07-07:

- ~~Incremental loop `GetCycleEnds` O(cycleIndex)~~ — fixed with a cached cycle base (O(1) amortized; backward jumps recompute once). No interface change was needed, contrary to the review's suggestion.
- ~~Elastic/BounceExact parameters ignored~~ — implemented parametric Penner elastic (amplitude/period) and amplitude-scaled BounceExact; defaults reduce exactly to the former hardcoded constants.
- ~~Docs claimed `TweenData<T>` pooling that doesn't exist~~ — `docs/architecture/performance.md` amended to state reality; pooling itself is scheduled (below).
- ~~`Easing.Linear()` "allocates a struct per call"~~ — **struck**: `EaseRef` is a readonly struct; `new` on it is stack construction, zero heap alloc. The finding misread C# struct semantics.

Resolved in phase 1.12 (2026-07-07): `TweenData<T>` pooling, swap-remove active lists, target multimap — all landed (see Done). The 8-callback-list-field collapse was **deliberately dropped**: pooled reuse amortizes the per-instance cost, so the collapse would save memory, not allocations; revisit only if record memory shows up in profiling (M2+).

### From code review (2026-07-08, full Runtime/ read — hardening pass)

Fixed same day (harness: 186 + 174 release-leg green, including 2 new nested-loop tests):

- **Stale `pendingKills` after an exception-aborted tick** — the list is now cleared at the start of `TickActiveCore`; entries surviving a throw (safe mode off) could free re-used slots and kill unrelated tweens on the next frame.
- **`TweenStore.Free` double-free hazard** — new `slotFree` bitmap; a second `Free` of the same slot is now a no-op instead of pushing a duplicate free-list entry (which would alias one slot between two future `Allocate` calls). Note: `Allocate` → `Free` without `SetData` remains legal (tests pin it); the guard keys on free-list membership, not on `data[id]`.
- **Nested sequence loops truncated** — `ConsumeSequence` ignored the child's `SetLoops`: window was `delay + one cycle`, so a looping child was cancelled after ~1 cycle and an infinite child was treated as finite. Now `delay + duration × loops`, with an open window for `SetLoops(-1)`, matching `ConsumeTween` and the invariant in `docs/architecture/sequence.md` ("infinite child does not extend Duration"). New tests: `NestedSequence_WithLoops_PlaysAllCycles`, `NestedSequence_InfiniteLoops_OpenWindow_DoesNotExtendDuration`.
- Mojibake (`�`) in three `SequenceData` comments.

Not fixed on purpose — see "Open questions / decisions pending" above.

Remaining tracked debt:

- ~~Safe mode absent~~ — implemented in 1.13 (this branch).
- **Builder buffer callback duplication** (`TransferCallbacks` + callback lists duplicated between `TweenBuilderBuffer`/`SequenceBuilderBuffer`) — both copies were touched in 1.12 (method-group alloc fix) but the extraction is still pending; do it next time either changes.
- **`Interpolators.Get<T>()` dictionary lookup per `Build()`** — valid micro-opt (generic static cache), but the cache must handle `Interpolators.Reset()` re-registration (version stamp) or interpolator-swapping tests break. Low urgency; fold into M2 fast paths.

## Test status

- Compile-check harness: 186 tests green + 174 in the FEATHERTWEEN_RELEASE leg (2026-07-08, includes hardening-pass fixes and 2 new nested-loop tests).
- Unity (Editor + Runtime + Performance): 196 green 2026-07-07 (through 1.14); hardening-pass changes verified in Unity 2026-07-08 (store tests re-run green after `slotFree` fix).

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `docs/guides/testing.md`.
