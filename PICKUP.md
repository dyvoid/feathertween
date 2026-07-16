# PICKUP

Where the last session left off. Update this when you stop, so the next session starts with context instead of archaeology.
Keep this file short and current, prune stale detail. Git history is the archive.

Last updated: 2026-07-16 (phases 1.16 done + 1.17 implemented on `claude/stoic-archimedes-4sr9d2`; Unity verification of the Showcase is the only thing left in M1)

## Current position

- **Milestone**: M1 (Core), production cut. See `docs/planning/phases.md`.
- **Done through**: Phase 1.16 (release hygiene + docs) complete; phase 1.17 Showcase sample **implemented**, on branch `claude/stoic-archimedes-4sr9d2` (not yet merged to `main`).
- **Remaining M1**: user runs the 1.17 manual test protocol in Unity (`docs/design/showcase-sample.md`) + the zero-alloc profiler check; fix anything found; then **v0.1 tag + stable declaration** at 1.17's exit, and M1 closes.
- **Branch**: this session's work is on `claude/stoic-archimedes-4sr9d2`; merge to `main` after Unity verification.

## This session (2026-07-16, phases 1.16 + 1.17)

- **Phase 1.16 — Release hygiene and documentation — done.**
  - MIT `LICENSE` + `CHANGELOG.md` (Keep a Changelog; `[0.1.0] - Unreleased`); package.json gained `"license": "MIT"`.
  - XML `<summary>` docs on every public Runtime type/member (converted the old tagless `///` comments). New CI leg `FeatherTween.DocCheck.csproj` compiles Runtime with **CS1591 as error** (Runtime only; stubs/samples excluded).
  - **`FT.ManualTick(double)` added**: doc reconciliation found the decided public spelling (conventions.md, risks.md: "global `Manual` root only (`FT.ManualTick`)") was never exposed — only the internal runner method existed, so consumers could not drive the Manual phase at all. Forwarder + public-API test (`FT_ManualTick_DrivesManualPhaseThroughPublicApi`).
  - **Docs reconciliation** (worst drift): `docs/api/easings.md` and `interpolators.md` described APIs that never shipped (cached `EaseRef` properties, `Easing.Elastic/Bounce` spellings, `EaseFunction` type, `FT.RegisterInterpolator`) — rewritten to the real surface. Smaller fixes across index/filters/tweens/sequences/callbacks/builders/handles (C#10 `with` in the quickstart, phantom `FT.Kill(id:)`, missing parens on ease factories, `GetAwaiter` marked M2). Abandoned-SequenceBuilder leak documented as a known limitation (builders.md + CHANGELOG).
- **Phase 1.17 — Showcase sample "the movie" — implemented** (`Samples~/Showcase/FeatherTweenShowcase.cs` + asmdef; flagship entry in package.json + README).
  - Eight chapters per spec; one nested master sequence; slide transitions on the timeline (explicit `From` endpoints everywhere for scrub determinism, except chapter 2's deliberately lazy To/From lanes, which get a deterministic reset pin at chapter start); player panel (two-way seek bar, chapter jump buttons from labels, play/pause/reverse/restart, 0.25–4× speed); chapter 6 drives a **detached** infinite yoyo via scripted `AppendCallback`s ending in `CompleteAtCycleEnd()`; playground parks at `AddPause` and offers punch one-shots, `Kill`/`Kill(complete)`, `IsTweening` readout, `KillAll` (+ full scene rebuild on Replay if used), `FT.GlobalTimeScale` slider.
  - **Headless structural dogfood passed** (scratch console replica through `FT.ManualTick`): 31s duration/label math exact, AddPause parks at 30.5s, incremental lane lands exactly, pure-function-of-time under 100 random scrubs (9 probes × 5 sample times), reverse-to-start holds, restart works.
  - Stub harness additions for the sample: `RectTransform`, `Canvas`, `RenderMode`, `Image.Type/FillMethod`, `GUILayout.Button(params)`, `GUILayout.Space`, `Transform.SetParent(Transform)`.
- **API friction found while dogfooding**: only the `FT.ManualTick` gap (fixed in 1.16). Nothing else — the surface held up.

## Next up

1. **User**: import the Showcase sample in Unity, run the manual test protocol (forward 1×, reversed, random scrub, chapter seeks from both directions, 0.25×/4×, zero per-frame alloc in profiler during chapters 1–7).
2. Fix any protocol findings; merge to `main`.
3. **v0.1 tag + stable declaration** (user go-ahead; move `package.json` version off `0.0.0-dev` and date the CHANGELOG entry at the same time). M1 closes.
4. M2 begins: `SetLink` + Awaitables first (production stickiness), then zero-alloc fast paths.

## Test status

- Compile-check harness: **212 green + 200 in the FEATHERTWEEN_RELEASE leg** (2026-07-16, includes the new `FT.ManualTick` test).
- Doc-check leg (CS1591 as error on Runtime): green; wired into CI.
- Unity: 1.15 + MiniShowcase verified by user 2026-07-16; **1.16/1.17 changes not yet Unity-tested** (XML docs are comment-only; the Showcase sample and `FT.ManualTick` are the things to exercise).

## Known issues / tech debt

- Performance tests require the consuming project to install `com.unity.test-framework.performance` (test-only dependency).
- Abandoned (never-started) `SequenceBuilder` pins child store slots until `TweenStore.Reset()` — documented behavior (builders.md, CHANGELOG known limitations), LeakDetector warning is the mitigation.
- **Builder buffer callback duplication** (`TransferCallbacks` duplicated between `TweenBuilderBuffer`/`SequenceBuilderBuffer`) — extract next time either changes.
- **`Interpolators.Get<T>()` dictionary lookup per `Build()`** — valid micro-opt; fold into M2 fast paths (cache needs an `Interpolators.Reset()` version stamp).

## Consumer setup reminders

- Tests require consumer-project setup (`testables` + `com.unity.test-framework.performance`). Full steps: `docs/guides/testing.md`.
