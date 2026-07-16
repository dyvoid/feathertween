# ADR 0011: API consistency pass (subject-first creation, getter-less `FromTo`)

Status: Accepted (2026-07-14)

## Context

A pre-v0.1 review of the whole public surface found the creation methods
carrying three vocabularies for endpoints (`end`, `fromValue`, `from`/`to`),
`FromTo` demanding a getter the runtime never reads (`SnapMode.FromTo`
resolves start values without it), mixed `time`/`seconds`/`atTime` parameter
naming, `SetDelay` silently clamping negatives while every sibling throws,
alias methods (`Chain`/`Group`) that existed only for tween children, and
handle surfaces that had drifted apart (`Sequence` exposed
`Duration`/`TotalProgress` but not `SetRemainingCycles`; `Tween` the
reverse).

The getter/setter pair is also the API's biggest ergonomic complaint: three
mentions of the animated value per `To` call. The getter exists to sample
the start value lazily at snap time — so any call that states both
endpoints explicitly doesn't need it.

## Decision

One shape for creation methods: **subject first** (typed target, or
getter/setter pair, shrinking to a lone setter when the engine never reads
the value), **then endpoints, then duration**. Composition methods stay
position-first (`Insert(time, child)`), matching GSAP/DOTween muscle
memory; the two families follow different rules on purpose.

Concretely:

- `FT.FromTo(setter, from, to, duration)` — the getter parameter is
  removed, not defaulted. Both endpoints are explicit, so values are
  captured at the creation call, not at snap time (documented alongside
  ADR 0007 semantics).
- `TweenBuilder<T>.From(T value)` — explicit start on any builder
  (`FT.Fade(cg, 1f, 0.5f).From(0f)`), the shortcut-flavored spelling of the
  same semantic.
- Endpoint parameters are `from`/`to` (qualified where needed: `toAlpha`).
- Positions are `time`, spans are `seconds`; `Seek(time)`,
  `Sequence.Insert(time, …)`.
- `SetDelay` throws on negatives; enum params are named after their type
  (`loopType`, `delayType`).
- `Chain`/`Group` aliases removed. `Join`/`Prepend` gained
  `SequenceBuilder` overloads so nested sequences are first-class in every
  composition method.
- Handle symmetry: `Tween.Duration`/`Tween.TotalProgress` and
  `Sequence.SetRemainingCycles(int|bool)` added, with matching semantics
  (`Duration` = one cycle; boundary-stop mirrors `TweenData<T>`).

Subject-last (trailing-lambda) ordering was considered and rejected: it
wins only on `FromTo` in isolation, reads backwards on the typed shortcuts
(the 90% API), collides with the planned M2 extension-method form where the
receiver is structurally first, and dangles builder chains off closing
lambda braces. The principled trailing-delegate design (LitMotion-style
`Bind` terminator) is a different architecture, noted as a possible v2
direction, not a parameter order.

## Consequences

Breaking for pre-v0.1 consumers (none known; the package is unreleased).
The `From(value)`/`FromTo` capture-at-creation semantic must stay
documented next to the lazy getter semantics of `To`/`From`, since the
difference is observable inside sequences. `SetRelative` remains a
`SnapMode.None`-only feature and is ignored by explicit-endpoint tweens;
supporting relative `FromTo` would need a second pristine-value slot in
`TweenData<T>` and is deliberately out of scope.
