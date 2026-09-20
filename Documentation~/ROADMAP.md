# Roadmap

Lightweight feature-candidate tracker. For the detailed milestone/phase plan, see [`Documentation~/planning/phases.md`](planning/phases.md).

Status values: `Candidate` — idea worth tracking; `Planned` — decision made, not started; `In flight` — actively being worked on; `Done` — shipped.

## M1 — Core

| Feature | Status | Description | ADR |
|---------|--------|-------------|-----|
| Storage and handle scaffold | Done | Pooled `TweenData` slots, generation ids, free list, per-phase active lists | — |
| PlayerLoop runner and root scheduler | Done | PlayerLoop injection for `Update` / `LateUpdate` / `FixedUpdate`, Editor mirror | 0004 |
| Builder/handle split and lifecycle | Done | `TweenBuilder<T>` + pooled backing, `.Start()` consumes buffer, leak detection | 0002, 0003 |
| Generic tween core | Done | `FT.To<T>`, `IInterpolator<T>` registry, linear ease, auto-kill | — |
| Full ease system | Done | `EaseRef`, `Easing.X` factories, parametric eases, static eval table | 0005 |
| From / FromTo with deferred snap | Done | `From()` / `FromTo()` builder methods, snap semantics | 0007 |
| Loops, delays, direction, reverse | Done | `SetLoops`, `SetDelay`, `Reverse()`, yoyo/incremental/rewind | 0009 |
| Sequence builder | Done | `SequenceBuilder`, Append/Insert/Join, labels, Position values | 0006 |
| Callbacks (full set + multicast + reentrancy) | Done | Builder-side callbacks, handle-side multicast, deferred mutation | — |
| Seek and remaining control surface | Done | `Seek`, `Restart`, `Complete`, `Kill(complete)`, mid-play `Insert`, sequence `SetLoops`, global/per-phase time scale | — |
| Typed shortcuts (lambda) | Done | `Move`, `Rotate`, `Scale`, `Fade`, `Color` — lambda baseline | — |
| Filters and bulk ops | Done | Target-indexed multimap, `Kill(target)`, `IsTweening`, `KillAll` | — |
| Safe mode and assertions | Done | Try/catch wrapper, off-thread assertions, release-build skip | — |
| M1 dev acceptance | Done | Composed demo, all suites green, zero-alloc verified | — |
| API consistency pass | Done | Getter-less `FromTo`, `From(value)`, subject-first param naming, handle symmetry | 0011 |
| API finalization (1.15) | Done | All decided semantics landed (reverse-through-delay, dead-handle late-subscription no-ops, zero-duration loop throw, int rounding, `CompleteAtCycleEnd`/`Start`, `SetCapacity(int)`, `GlobalTimeScale` property); API final for v0.1 | — |
| Release hygiene and documentation (1.16) | Done | MIT LICENSE, CHANGELOG, XML docs on the full public surface (CS1591 CI gate), docs reconciliation, `FT.ManualTick` exposure | — |
| Showcase sample "the movie" (1.17) | Done | Shipped (`Samples~/Showcase/`, eight chapters per spec); Unity manual test protocol and zero-alloc profiler check passed 2026-07-18; v0.1.0 tagged, M1 closed (`Documentation~/design/showcase-sample.md`) | — |

## M2 — Polish and ecosystem

| Feature | Status | Description |
|---------|--------|-------------|
| Hand-written zero-alloc fast paths | Planned | Bypass lambda core for common shortcuts, 0 alloc on `Start` |
| TweenSettings serialization | Planned | `[Serializable] TweenSettings<T>`, PropertyDrawer, `WithDirection` |
| Cross-engine comparative benchmark | Planned | DOTween/PrimeTween recordings, cost vs LitMotion managed path |
| Zero-alloc target-capture overloads for all callbacks | Candidate | Extend beyond `OnComplete` / `OnKill` |
| `SetLink(GameObject, LinkBehavior)` | Done | `KillOnDestroy`, `KillOnDisable`, `PauseOnDisable`, `PauseOnDisableResumeOnEnable`, `RestartOnEnable`; covers the pooled-object footgun `SetTarget` auto-kill misses (ADR 0012) |
| Typed shortcuts expansion | Candidate | `RectTransform`, `Material`, `SpriteRenderer`, `Camera`, `Light`, `AudioSource` |
| Shake / Punch shortcuts | Candidate | `ShakePosition`, `ShakeRotation`, `ShakeScale`, `PunchPosition` |
| Extension method asmdef | Candidate | Optional `transform.PAMove(...)` wrappers |
| Value modifiers | Planned | Optional `Func<T,T>` post-processor applied to the eased value before the setter: snap-to-grid, rounding, angle wrap, clamp |
| `yoyoEase` | Planned | Separate optional `EaseRef` for the return leg of a Yoyo cycle |
| Awaitables | Planned | `TweenAwaiter` on Unity 6 native `Awaitable`, `WaitForCompletion`, `WaitForKill`, `WaitForPosition` |
| Improved safe-mode reporting | Candidate | Collected per-frame diagnostics |
| Roslyn analyzer | Candidate | Compile-time diagnostics for the two footguns the runtime can only warn about after the fact: a builder that is never consumed by `Start()`/`Clear()`, and a non-static lambda in a target-capture callback overload |

## M3 — Power features

| Feature | Status | Description |
|---------|--------|-------------|
| Editor preview window | Candidate | Scrubber = 1.10 `Seek` + existing editor ticking |
| UniTask integration | Candidate | Weakened case (M2 awaitables use native `Awaitable`); only if a consumer needs it |
| Stagger helpers | Candidate | `FT.Stagger(targets, ...)` |
| Speed-based tweens | Candidate | `FT.PositionAtSpeed`, etc. |
| Path tweens | Candidate | Linear / CatmullRom paths, `LookAt` modes; separate `FeatherTween.Paths` asmdef |
| Blendable tweens | Candidate | Additive composition; **needs ADR first** (multiple writers per property vs storage model) |
| `TweenAssetSO` | Candidate | Shared preset ScriptableObjects |
| Keyframe tweens | Candidate | Single tween through multiple values (`A → B → C`) with per-segment ease; one slot, one handle; stays a pure function of time so Seek/Reverse/yoyo compose for free; **needs ADR first** (keyframe storage vs pooled record layout) |
| Text / string tweening | Candidate | Typewriter reveal and number counters (TMP); needs a dedicated alloc-conscious path — `string` doesn't fit the blittable `IInterpolator<T>` core |
| Runtime retargeting + `quickTo` | Candidate | `ChangeEndValue`/`ChangeStartValue` mid-flight (tween stays seekable) plus a GSAP `quickTo`-style reusable retargetable tween; covers follow-a-moving-target without simulation state; distinct from `invalidate` (M4), which re-reads start values |

## M4 — GSAP parity sugar

| Feature | Status | Description |
|---------|--------|-------------|
| `Position.Parse` DSL | Candidate | `"+=0.3"`, `"<"`, `">"`, label references |
| `tweenTo` / `tweenFromTo` | Candidate | GSAP-style timeline navigation |
| `invalidate` / `repeatRefresh` | Candidate | Runtime re-evaluation of start values |
| Cross-timeline coordinates | Candidate | `globalTime` conversion — weakest roadmap item; drop unless a concrete need appears |

## M5 — Optional optimization pass

| Feature | Status | Description |
|---------|--------|-------------|
| SoA storage | Candidate | Blittable hot-path arrays for Burst/Jobs |
| Per-type generic storage | Candidate | `TweenStorage<TValue, TInterpolator>` |
| Burst-optimized eval loop | Candidate | SIMD-friendly ease evaluation |
| NativeContainer integration | Candidate | `NativeTweenHandle` for ECS/Job interop |

---

## Archive

_Nothing archived yet._
