# ADR 0005: Ease as `EaseRef` value type

## Status

Accepted

## Context

Easings need parameters (`overshoot` for `OutBack`, `strength` and `period` for `Elastic`). Options: global mutable state, a class hierarchy, or a value type that carries parameters.

## Decision

Represent an ease as an `EaseRef` value type produced by `Easing.X(...)` factories:

- `Easing.OutBack(overshoot)`
- `Easing.Elastic(strength, period)`
- `Easing.BounceExact(amplitudeMeters)`
- `Easing.Curve(animCurve)`
- `Easing.Custom(easeFunc)`

The ease and its parameters travel together. The tween stores one `EaseRef`. New eases can be plugged in without API change.

## Consequences

- **Positive**: No global mutable state. Thread-safe to create and pass around.
- **Positive**: Parameters are visible at the call site.
- **Positive**: Adding a new ease is a single factory method; no plumbing changes.
- **Negative**: `EaseRef` must be sized to fit the largest parameterized ease.
- **Mitigation**: The set of built-in eases is bounded; custom eases can use the `Custom` delegate path if they need large state.
