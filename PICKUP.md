# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-16 (phase 1.15 closed: Unity-verified by user, merged to `main`; API final for v0.1)

## Current position

- **Milestone**: M1 (Core), production cut. See `docs/planning/phases.md`.
- **Done through**: Phase 1.15 (API finalization) merged to `main`, Unity-tested by the user 2026-07-16 ("works great"), incl. the new MiniShowcase sample. **The public API is final for v0.1.**
- **Remaining M1**: 1.16 (hygiene + docs, v0.1 tag) → 1.17 (showcase "movie" sample, dogfood gate, v0.1 declared stable).
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

- **Phase 1.15 — API finalization — merged to `main`, Unity-verified by user 2026-07-16.** Both harness legs green (211 + 199). Changes:
  - **Reverse-through-delay**: delays are part of the timeline. Tween: finite loops count the initial delay back down and hold `Delayed` at playhead 0; infinite loops wrap per ADR 0009 with a delay-aware floor (`FirstLoop` delay never re-entered backward; `EveryLoop` delay passed through inside the slot) — see ADR 0009 addendum. Sequence: fixed the infinite `Delayed` stall (negative dt now re-grows `delayRemaining`; reversing out of content past playhead 0 re-enters the delay).
  - **Dead-handle late subscriptions all no-op**: `OnComplete`/`OnKill`/`OnStepComplete` on dead `Tween`/`Sequence` handles do nothing (was: late-fire).
  - **`duration <= 0` + `SetLoops(-1)` throws** at `Start()`/append (validated in `TweenBuilderBuffer.Build`).
  - **`IntInterpolator.Lerp` rounds to nearest** (`Mathf.RoundToInt`), symmetric across 0.
  - **`ForceComplete` syncs `localTime`** so Complete-then-Seek starts from the completed position; `TotalProgress` reads 1 after `Complete()`.
  - **`CompleteAtCycleEnd()`/`CompleteAtCycleStart()`** replace `SetRemainingCycles(bool)` on both handles.
  - **`FT.SetCapacity(int)`** (single param) and **`FT.GlobalTimeScale` property** (setter throws on negative) replace the old spellings; all call sites (tests, ComposedDemo sample) updated.
  - **ADR 0008 guard**: verified — no detectable gap without a Lerp-only interpolator tier; recorded as ADR 0008 addendum, no code change.
  - **Docs**: handles.md (new methods, dead-handle rule, reverse-through-delay, Complete sync, `GlobalTimeScale`), builders.md, sequences.md (`AddLabel` definition-time note), interpolators.md (int rounding, ADR 0008 note), overview.md (manual-phase cleanup contract), ADR 0008/0009 addenda, risks.md.
  - **Tests**: 14 new/updated (reverse-through-delay ×7 incl. wrap composition, dead-handle ×2, zero-duration throw ×2, int rounding, Complete-then-Seek, `CompleteAtCycleStart`).

- **New sample: MiniShowcase** (`Samples~/MiniShowcase/`, merged with 1.15, user-tested) — a small preview of the 1.17 showcase: one master sequence, four captioned chapters (ease race, loops with exact landing markers, deferred From drop, synchronized finale), and a player panel whose seek bar / play-pause / reverse / speed / chapter jumps drive the root sequence. Registered in `package.json` samples and README. Exists because ComposedDemo is hard to visually test (no stated expected outcomes); this one captions what should happen per chapter, borrowing 1.17's falsifiable-visual-testing idea. Stub harness gained `GUILayout.Width` and `Color.gray`.

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

1. **Phase 1.16 — Release hygiene and documentation**: LICENSE, CHANGELOG.md, XML docs on every public type/member, reconcile all docs, v0.1 tag.
2. **Phase 1.17 — Showcase sample "the movie"** (new, 2026-07-16): the whole sample is one nested master sequence — chaptered feature screens with self-describing captions and a seek-bar/player UI driving the root sequence. Spec: `docs/design/showcase-sample.md`. Serves as the v0.1 dogfood gate and the pure-function-of-time stress test; M1 closes and v0.1 is declared stable at its exit.

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

- Compile-check harness: 211 tests green + 199 in the FEATHERTWEEN_RELEASE leg (2026-07-16, includes the 14 phase-1.15 tests).
- Unity: phase 1.15 + MiniShowcase tested by the user 2026-07-16 ("works great"); prior full sweep (196 green + ADR 0011) confirmed the same day.

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `docs/guides/testing.md`.
