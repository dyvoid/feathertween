# Showcase sample — "the movie"

Design spec for the feature-showcase sample (implemented in phase 1.17 as `Samples~/Showcase/`). It is the package's only sample: the older demos (BasicUsage, SequenceDemo, MiniShowcase, ComposedDemo) were removed once it landed (user decision, 2026-07-16).

## Concept

The entire sample is **one nested sequence**: a master timeline ("the movie") whose chapters are nested sequences, each demonstrating one feature area on its own screen. Title cards, captions, and screen transitions are themselves tweens on the same timeline. A player UI (seek bar, play/pause, reverse, speed) sits below the screen — and is not chrome: the controls literally call `Seek`, `Pause`/`Resume`, `Reverse`, and `SetTimeScale` on the root sequence.

Every screen states its intent in an on-screen caption, including expected outcomes where checkable by eye ("the box lands exactly on the grid line at the loop boundary"). A viewer who doesn't read the code can tell correct from broken.

## Why it exists (three jobs, one artifact)

1. **Hardest test of the timeline model.** The movie only works if state is a pure function of the playhead. Scrubbing backward through chapters full of From-snaps, incremental loops, nested delays, and callbacks surfaces compositional bugs unit tests are too narrow to catch. It is the M3 editor-preview scrubber, dogfooded early through the public API.
2. **Falsifiable visual testing.** Captions turn eyeballing into checking: each screen is a visual assertion with its expected result written next to it.
3. **The v0.1 dogfood gate.** Building a multi-minute synchronized timeline with UI controls is a real consumer workload; it satisfies the "dogfood in a real project before declaring the API stable" exit criterion and will generate API-friction findings the way the consistency pass did.

## Structural rule: what's on the timeline vs. what can't be

Everything time-composable lives on the master timeline. Two categories don't, and the boundary is itself an honest illustration of the library's model:

- **Infinite loops** never appear open-ended inside the movie (an open window would make the movie unendable). They appear as bounded excerpts via `SetRemainingCycles(int)` / `CompleteAtCycleEnd()` — which conveniently *demonstrates those APIs*.
- **Imperative/interactive features** (kill-by-target, bulk ops, button-triggered tweens) live in the final playground chapter, where the movie parks its playhead and hands over buttons.

## Chapters

Each chapter = one nested sequence, appended to the master with a label (`AddLabel("ch3-loops", ...)`); the seek bar shows chapter markers built from the labels.

1. **Title** — logo assembly out of individual tweens; doubles as an ease sampler (each letter uses a different ease, captioned).
2. **Creation semantics** — `To` vs `From` vs `FromTo` side by side on three identical objects, with the ADR 0007 snap moment made visible (a flash marker when the property snaps). Caption states capture-at-creation (`FromTo`, `From(value)`) vs lazy getter sampling (`To`/`From`).
3. **Eases gallery** — grid of dots running every `EaseType` in parallel, including parametric variants (`OutBack(overshoot)`, `Elastic(s, p)`, `BounceExact(amp)`) and `Curve`/`Custom`. Grid lines make over/undershoot readable.
4. **Loops and delays** — `Restart` / `Yoyo` / `Rewind` / `Incremental` in four lanes; `FirstLoop` vs `EveryLoop` delay shown with a visible countdown bar. Incremental lane runs bounded cycles and must land exactly on grid lines (caption asserts the landing positions).
5. **Sequence composition** — a mini scene assembled step by step: `Append`, `Join`, `Insert(time, …)`, `Insert(Position.AtLabel(...))`, `AppendInterval`, `AppendCallback` (callback fires a visible ping), nested child sequence with its own loops. The chapter draws its own timing diagram as it plays.
6. **Control surface (meta-chapter)** — a mini-player *inside the movie* drives a small child sequence: seeks it, reverses it, scales its time, stops an infinite yoyo with `CompleteAtCycleEnd()`. Demonstrates that control ops compose — the outer scrubber can scrub a chapter that is itself scrubbing.
7. **Typed shortcuts** — every shipped shortcut (`Move`/`LocalMove`/`Scale`/`Rotate`/`LocalRotate`/`Fade`/`Color`/`FillAmount`) doing its literal thing on labeled targets.
8. **Playground (finale, off-timeline)** — the movie pauses at its last label. Buttons: punch-style one-shots, `Kill(target)` vs `Kill(target, complete: true)` on running tweens, `IsTweening` readout, `KillAll`, global time-scale slider. A "replay" button seeks the master back to 0.

## Player UI requirements

- Seek bar bound two-way: dragging calls `Seek(time)` on the root; during playback the bar tracks `TotalProgress`. Chapter markers from labels; click a marker to seek to its label time.
- Play/pause as a **single fixed-position, fixed-width toggle** — it must not change slot or size
  with playback state, or it reshuffles under the cursor while scrubbing. Replay is a separate,
  always-present button. Plus reverse (toggles direction mid-flight) and speed control (0.25×–4×
  via `SetTimeScale`).
- Known rough edge: `Seek` never changes `Status` (see [`../api/handles.md`](../api/handles.md)), so a
  timeline scrubbed to its end reads `Completed`, and `Play()` from there replays from 0 instead of
  resuming at the playhead. The transport makes this legible rather than hiding it; resolving it
  properly is an API-semantics question, not a sample fix.
- The master sequence is `SetAutoKill(false)` so the movie is replayable and seekable after completion.

## Test protocol (manual, per release)

1. Play through forward at 1× — every caption's stated outcome visibly true.
2. Play through reversed from the end.
3. Random scrubbing: drag the seek bar erratically, then release at several points — the frame at any playhead position must match what forward playback shows at that time (pure-function-of-time check).
4. Seek directly to each chapter marker from both directions.
5. Run at 4× and 0.25× — no desync between lanes or captions.
6. Profiler: zero per-frame managed alloc during chapters 1–7 playback.

## Constraints

- Uses only public API (it is a consumer, not a test harness with internal access).
- Same asmdef/consumer setup as existing samples (`Samples~/Showcase/`).
- Depends on the 1.15 surface being final; API friction discovered while building it feeds back before the stable declaration, not after.
