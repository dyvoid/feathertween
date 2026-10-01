# ADR 0014 — `LoopType.Rewind` replays cycles backward in time

**Status**: Accepted
**Date**: 2026-10-01
**Milestone**: M2

## Context

`LoopType.Rewind` shipped in v0.1.0 as a public enum value with no implementation: every code path
treated it as `Restart`, and no test exercised it. Two definitions were on record, and they
disagreed:

- The enum's own XML doc: "like `Restart`, but the value snaps back to the start at the cycle
  boundary instead of holding the end".
- Its cited source ([`influences.md`](../design/influences.md)), PrimeTween's `CycleMode.Rewind`:
  "rewinds the tween as if time was reversed. Easing is reversed on the backward cycle."
  (PrimeTween README, read 2026-10-01.)

The first is nearly indistinguishable from `Restart`: a `Restart` loop already returns to the start
at every boundary, so the two would differ only in the final value and in what an `EveryLoop` delay
holds. The second fills a real gap. FeatherTween's tween `Yoyo` is PrimeTween's `Yoyo`: the return
leg applies the ease forward from end to start, so an `OutCubic` tween decelerates into both ends.
There was no way to get the time-reversed return that DOTween calls yoyo and that a sequence's
`Yoyo` already produces.

## Decision

`Rewind` follows PrimeTween. On a tween, odd cycles replay the previous cycle backward in time: the
sample at in-cycle time `t` is the forward sample at `1 - t`, i.e. `Lerp(start, end, ease(1 - t))`.
Even cycles play forward. On a sequence, `Rewind` is `Yoyo`: a sequence's odd cycles already walk
the timeline backward, which replays every child time-reversed.

Implementation: `Rewind` shares `Yoyo`'s swapped cycle endpoints (`end → start` on odd cycles) and
evaluates the ease as `1 - ease(1 - t)`, so cycle ends, `Complete()`, `EveryLoop` delay holds and
`OnUpdate`'s "1 at a cycle end" all fall out of the existing `Yoyo` paths.

## Consequences

- Behavior change for anyone who used `Rewind` and got `Restart`; listed under `Changed` in the
  CHANGELOG with a migration note (use `Restart`).
- With a symmetric ease (`Linear`, `InOutQuad`, …) `Yoyo` and `Rewind` produce the same motion.
  The Showcase loop lanes switched to an asymmetric ease so the difference is visible.
- Tween `Yoyo` and sequence `Yoyo` remain different in kind (ease forward vs time-reversed). That
  asymmetry predates this ADR; `Rewind` now names the time-reversed behavior for both.
