# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

**Pre-1.0, minor versions may contain breaking changes** — what `0.x` means in semver. Every break
is listed here under `### Changed` or `### Removed` with a migration note. `1.0.0` is where the
stability promise gets made.

## [Unreleased]

### Changed

- **Versioning policy**: the stronger-than-semver stability promise made at 0.1.0 is withdrawn. It
  was stricter than the version number implied, it is redundant with the ADR process that actually
  governs API changes here, and it would bind exactly the M2-M5 work most likely to need a break.
  Nothing in the API changed; only the promise about future ones did. Full policy in
  [`Documentation~/git-strategy.md`](Documentation~/git-strategy.md#versioning).

### Added

- **`SetLink(GameObject, LinkBehavior)`** on `TweenBuilder<T>` and `SequenceBuilder`: ties an
  animation's lifetime to a `GameObject`'s active state, which is what object pooling needs —
  `SetTarget` auto-kill only fires for *destroyed* objects. Behaviors: `KillOnDestroy` (default),
  `KillOnDisable`, `PauseOnDisable`, `PauseOnDisableResumeOnEnable`, `RestartOnEnable`. The runner
  reads `activeInHierarchy` once per tick per linked record; no component is attached to your
  objects (ADR 0012). Links are root-level — appending a linked builder into a sequence throws, so
  link the sequence instead.

## [0.1.0] - 2026-07-18

Initial release. The public API is declared stable at this version; breaking
changes from here on follow semantic versioning.

### Added

- **Core tween engine**: `FT.To<T>(getter, setter, to, duration)` lambda tweens
  for `float`, `Vector2/3/4`, `Color`, `Quaternion`, and `int` (extensible via
  `Interpolators.Register`), driven by a `MonoBehaviour`-free PlayerLoop runner
  with `Update`, `LateUpdate`, `FixedUpdate`, and `Manual`
  (`FT.ManualTick(dt)`) phases, plus Editor-mode ticking.
- **Builder/handle split**: mutable `TweenBuilder<T>` / `SequenceBuilder`
  structs configure an animation; `.Start()` returns immutable, generation-safe
  `Tween` / `Sequence` struct handles. Stale handles no-op.
- **Creation semantics**: `To`, `From()` deferred snap, `From(value)`, and
  `FromTo(setter, from, to, duration)` — start values captured at creation or
  sampled lazily per the documented snap rules.
- **Ease system**: full standard ease set as `Easing.X()` value-type factories
  (`EaseRef`), parametric `OutBack(overshoot)` / `Elastic(amplitude, period)` /
  `BounceExact(amplitude)`, `Easing.Curve(AnimationCurve)`, and
  `Easing.Custom(EaseFunction)`.
- **Loops, delays, direction**: `SetLoops(count | -1, loopType)` with
  `Restart` / `Yoyo` / `Rewind` / `Incremental`; `SetDelay(seconds, delayType)`
  with `FirstLoop` / `EveryLoop`; `Reverse()` through delays and loop
  boundaries — state is a pure function of the playhead.
- **Sequences**: `FT.Sequence()` with `Append` / `Join` / `Insert` / `Prepend` /
  `AppendInterval` / `AppendCallback` / `AddLabel` / `AddPause`, `Position`
  time/label addressing, nested sequences (first-class in all composition
  methods), `SetDefaults` cascade, and `SequenceCancelBehavior`.
- **Callbacks**: `OnStart` / `OnPlay` / `OnPause` / `OnUpdate` / `OnComplete` /
  `OnStepComplete` / `OnRewind` / `OnKill`, target-capture zero-alloc
  `OnComplete` / `OnKill` overloads, reentrancy-safe deferred mutation.
- **Control surface**: `Pause` / `Resume` / `Kill(complete)` / `Restart` /
  `Reverse` / `Seek(time)` / `Complete`, `CompleteAtCycleEnd()` /
  `CompleteAtCycleStart()`, `SetRemainingCycles(int)`, per-tween
  `SetTimeScale`, per-phase `FT.SetTimeScale(phase, scale)`, and
  `FT.GlobalTimeScale`.
- **Typed shortcuts**: `FT.Move` / `LocalMove` / `Scale` / `Rotate` /
  `LocalRotate` (Transform), `Fade` (CanvasGroup), `Color` / `Fade` /
  `FillAmount` (Image).
- **Filters and bulk ops**: `Kill(target)`, `IsTweening(target)`, `KillAll`,
  `PauseAll`, `ResumeAll` over a target-indexed multimap.
- **Safe mode**: try/catch around setters and callbacks (default on in Editor,
  compiled out under `FEATHERTWEEN_RELEASE`), `SetSafeMode`,
  `SetCancelOnError`, main-thread assertions.
- **Performance posture**: pooled storage, zero per-frame managed allocation in
  steady-state ticking (guarded by allocation tests).
- **Samples**: Showcase — a guided tour of the whole feature set as one
  scrubbable, captioned timeline with a player panel.

### Known limitations

- A composed sequence that is never started leaks tween capacity until the
  next domain reload. Always end a `SequenceBuilder` in `Start()` or
  `Clear()`; see "Known limitation" in `Documentation~/api/builders.md` for details.
- Performance tests require the consuming project to install
  `com.unity.test-framework.performance`.
