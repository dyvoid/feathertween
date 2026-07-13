# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-08 (hardening pass: full Runtime/ review, 3 bugs fixed + 2 nested-loop tests; open semantics decisions logged below)

## Current position

- **Milestone**: M1 (Core), production cut. See `docs/planning/phases.md`.
- **Done through**: Phase 1.14 (dev acceptance) merged to `main`. Unity run 2026-07-07: 196 tests green including the new 10k-tween and 1k×10-child-sequence zero-alloc guards. **Composed demo visual pass**: confirm it has been eyeballed in Play mode; if not, it is the one open 1.14 exit item.
- **Phase restructure (user decision, 2026-07-07)**: release hygiene (LICENSE, CHANGELOG, XML docs) split out of 1.14 into a new **1.15**, which now carries the v0.1 tag. Between 1.14 and 1.15 sits a hardening pass (full test sweep + code review) — everything must be flawless before 1.15 documents it.
- **Remaining M1**: hardening pass → 1.15 (hygiene + docs, v0.1 tag).
- **Branch**: trunk-based on `main`; short-lived branches `task/1.x-phase-name` / `fix/...`, fast-forward merge.

## Done

- **Phases 1.1–1.12** merged to `main`: storage/handle scaffold, PlayerLoop runner, builder/handle split, generic tween core, ease system, From/FromTo, loops/delays/reverse, sequence builder, callbacks, seek/control, typed shortcuts, filters/bulk ops, storage surgery.
- **Documentation pass (this session)**: reorganized docs into `design/`, `architecture/`, `api/`, `guides/`, and `planning/`; removed monolithic section numbering and production-cut reshuffle noise; split `implementation.md` and `reference.md` into focused files.
- **Sample static-import fix**: removed `using static Dyvoid.FeatherTween.FT;` from `Samples~/BasicUsage/FeatherTweenDemo.cs` and `Samples~/SequenceDemo/FeatherTweenSequenceDemo.cs`; calls are now `FT.To(...)`, `FT.Sequence(...)`, and `FT.From(...)`. This fixes the `CS0119` collision with `UnityEngine.Color` that broke the compile-check CI. Added AGENTS.md invariant (#8) and `docs/guides/conventions.md` rule.
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

### From code review (2026-07-08) — semantics decisions needed

Deliberate non-fixes from the hardening-pass review: the behavior is questionable but the right answer is a design decision, not a patch. Decide before 1.15 documents the contracts.

- **`Reverse()` during an initial delay stalls a sequence forever**: in `SequenceData.Step` a negative `dt` never decrements `delayRemaining`, so the sequence reports `Delayed` every frame and never moves. Decide: rewind through the delay, settle at playhead 0, or no-op while delayed. Tween-level delay has the same question.
- **Dead-handle `OnKill(cb)` fires the callback immediately** (test-locked in `TweenBuilderTests`). This conflicts with "natural completion never fires OnKill": a naturally-completed auto-killed tween returns a dead handle, so a late `OnKill` subscription fires for a tween that was never killed. Decide: keep the late-fire convenience and document the exception, or make dead-handle `OnKill` a no-op (updates that test).
- **Zero-duration tween + `SetLoops(-1, Incremental)` can freeze a frame**: `cycleSlot` clamps to 1e-9, `cycleIndex` explodes, and the incremental cold-cache recompute in `GetCycleEnds` loops `cycleIndex` times. Decide: reject `duration <= 0` with infinite loops at build time, or clamp.
- **Manual-phase destroyed-target cleanup depends on `ManualTick` being called**: if manual ticking stops, tweens on destroyed Unity objects in that phase are never auto-killed and `byTarget` pins them until reset. Probably a doc note ("keep ticking or kill explicitly"), but decide.
- **`IntInterpolator.Lerp` truncates toward zero**, so negative-range int tweens step asymmetrically around 0. Floor/round would be uniform; changing it alters observable values, so it needs decision + test updates in one move.
- **`TweenData<T>.ForceComplete` leaves `localTime` stale**: after `Complete()` on a non-autokill tween, a later `Seek` starts from the old playhead. Harmless today (`ResetPlayhead` covers Restart/Play), but a trap for future timeline features.

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
