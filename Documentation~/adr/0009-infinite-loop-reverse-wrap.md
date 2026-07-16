# ADR 0009 — Infinite-loop `Reverse()` wraps instead of clamping

## Status

Accepted (M1, phase 1.7).

## Context

When a leaf tween is reversed, `localTime` decreases each tick. For a finite-loop tween, hitting `localTime == 0` is a meaningful terminal state (the tween is "fully rewound to start") and clamping is correct.

For an **infinite-loop** tween, the user's intent in calling `Reverse()` is to keep the oscillation alive in the opposite direction, not to rewind to the absolute origin and freeze. Clamping at `0` produces a dead tween that the user must explicitly restart, which contradicts the "infinite" contract.

## Decision

When `localTime` would become negative on a reverse step:

- Finite-loop tween (`loopCount >= 1`): clamp `localTime = 0`. Tween holds at start.
- Infinite-loop tween (`loopCount < 0`): wrap by adding one cycle slot (`delay + duration` for `EveryLoop` delay, otherwise `duration`). The tween reenters the previous iteration window and continues playing backwards.

`OnRewind` fires on the wrap, treating it as a normal cycle-boundary crossing.

## Consequences

`Reverse()` on an infinite tween is now safe to call at any `localTime` without freezing. Pairs naturally with Yoyo: an infinite Yoyo + Reverse keeps oscillating, just inverted in phase.

The wrap operation is one addition per Step on the rare frame where rewind crosses zero — negligible cost.

This deviates from the simpler clamp-only model, but the simpler model breaks the user's mental model of "infinite means infinite, in either direction".

## Addendum (phase 1.15, 2026-07-16) — composition with reverse-through-delay

With delays now part of the timeline (a reversed finite tween counts its initial delay back down to playhead 0), the wrap floor depends on where the delay lives:

- **`FirstLoop` delay**: sits before cycle 0 only. The backward wrap floor is the delay offset — an infinite tween wraps into the previous iteration's *content* and never re-enters the initial delay (which still plays out normally going forward). Reversing while still inside the initial delay also wraps rather than stalling.
- **`EveryLoop` delay**: lives inside each cycle slot, so backward playback passes through the previous cycle's delay region (holding the last written value, status `Delayed`) before wrapping a full `delay + duration` slot at 0.

The wrap applies only to backward motion; finite tweens still clamp at playhead 0 (inside the delay, status `Delayed`).
