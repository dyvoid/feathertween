# ADR 0010: No duration in the SetDefaults cascade

## Status

Accepted

## Context

`SequenceBuilder.SetDefaults` cascades values into subsequently appended child
builders that have not explicitly set them (§5.4). The original API sketch
included `duration` in the cascade. Building the SequenceDemo sample exposed
that this is dead code: every tween creation method (`To`, `From`, `FromTo`,
and the planned typed shortcuts) takes duration as a required argument, so
every child builder always has an explicitly set duration and the cascade
could never apply.

The alternative — treating the constructor duration as non-explicit so the
cascade can override it — was rejected: `Append(To(..., 0.8f))` being silently
stretched to a sequence default of 0.5s is surprising in exactly the way this
library tries to avoid.

## Decision

Drop `duration` from `SetDefaults`. The cascade covers `ease`, `loops`
(with `loopType`), and `delay` — all genuinely optional on child builders.
If a future milestone adds duration-optional creation overloads, the cascade
parameter can be reintroduced additively without breaking anything.

## Consequences

- **Positive**: no dead public API surface; no silent duration overrides.
- **Positive**: the explicit-set flag machinery on `TweenBuilderBuffer` stays
  minimal (phase, ease, loops, delay).
- **Negative**: divergence from the original api.md sketch; docs updated in
  the same change.
