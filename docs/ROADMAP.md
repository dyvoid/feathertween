# Roadmap

Lightweight feature-candidate tracker. For the detailed milestone/phase plan, see [`docs/implementation.md`](implementation.md).

Status values: `Candidate` — idea worth tracking; `Planned` — decision made, not started; `In flight` — actively being worked on; `Done` — shipped.

## M1 — Core

| Feature | Status | Description | ADR |
|---------|--------|-------------|-----|
| Storage and handle scaffold | Done | Pooled `TweenData` slots, generation ids, free list, per-phase active lists | — |
| PlayerLoop runner and root scheduler | Done | PlayerLoop injection for `Update` / `LateUpdate` / `FixedUpdate`, Editor mirror | 0004 |
| Builder/handle split and lifecycle | Done | `TweenBuilder<T>` + pooled backing, `.Start()` consumes buffer, leak detection | 0002, 0003 |
| Generic tween core | Done | `PATween.To<T>`, `IInterpolator<T>` registry, linear ease, auto-kill | — |
| Full ease system | Done | `EaseRef`, `Easing.X` factories, parametric eases, static eval table | 0005 |
| From / FromTo with deferred snap | Done | `From()` / `FromTo()` builder methods, snap semantics | 0007 |
| Loops, delays, direction, reverse | Done | `SetLoops`, `SetDelay`, `Reverse()`, yoyo/incremental/rewind | 0009 |
| Sequence builder | Done | `SequenceBuilder`, Append/Insert/Join, labels, Position values | 0006 |
| Callbacks (full set + multicast + reentrancy) | Done | Builder-side callbacks, handle-side multicast, deferred mutation | — |
| Seek and remaining control surface | Planned | `Seek`, `Restart`, `Complete`, `Kill(complete)`, mid-play `Insert`, sequence `SetLoops`, global/per-phase time scale | — |
| Typed shortcuts (lambda) | Planned | `Move`, `Rotate`, `Scale`, `Fade`, `Color` — lambda baseline | — |
| Filters and bulk ops | Planned | Target-indexed multimap, `Kill(target)`, `IsTweening`, `KillAll` | — |
| Safe mode and assertions | Planned | Try/catch wrapper, off-thread assertions, release-build skip | — |
| M1 acceptance (production cut) | Planned | Composed demo, all suites green, zero-alloc verified; LICENSE, CHANGELOG, XML docs; v0.1 tag | — |

Production-cut reshuffle (2026-07-02): hand-written zero-alloc fast paths, TweenSettings serialization, and the cross-engine comparative benchmark moved to M2; M1 renumbered to stay linear (now 14 phases, acceptance is 1.14). See `implementation.md` §10.

## M2 — Polish and ecosystem

| Feature | Status | Description |
|---------|--------|-------------|
| Hand-written zero-alloc fast paths | Planned | Moved from M1: bypass lambda core for common shortcuts, 0 alloc on `Start` |
| TweenSettings serialization | Planned | Moved from M1: `[Serializable] TweenSettings<T>`, PropertyDrawer, `WithDirection` |
| Cross-engine comparative benchmark | Planned | Moved from M1 acceptance: DOTween/PrimeTween recordings, cost vs LitMotion managed path |
| Zero-alloc target-capture overloads for all callbacks | Candidate | Extend beyond `OnComplete` / `OnKill` |
| `SetLink(GameObject, LinkBehavior)` | Planned | KillOn/PauseOn/RestartOn variants; covers the pooled-object footgun `SetTarget` auto-kill misses |
| Typed shortcuts expansion | Candidate | `RectTransform`, `Material`, `SpriteRenderer`, `Camera`, `Light`, `AudioSource` |
| Shake / Punch shortcuts | Candidate | `ShakePosition`, `ShakeRotation`, `ShakeScale`, `PunchPosition` |
| Extension method asmdef | Candidate | Optional `transform.PAMove(...)` wrappers |
| Awaitables | Planned | `TweenAwaiter` on Unity 6 native `Awaitable`, `WaitForCompletion`, `WaitForKill`, `WaitForPosition` |
| Improved safe-mode reporting | Candidate | Collected per-frame diagnostics |

## M3 — Power features

| Feature | Status | Description |
|---------|--------|-------------|
| Editor preview window | Candidate | Promoted from M4: scrubber = 1.10 `Seek` + existing editor ticking |
| UniTask integration | Candidate | Weakened case (M2 awaitables use native `Awaitable`); only if a consumer needs it |
| Stagger helpers | Candidate | `PATween.Stagger(targets, ...)` |
| Speed-based tweens | Candidate | `PATween.PositionAtSpeed`, etc. |
| Path tweens | Candidate | Linear / CatmullRom paths, `LookAt` modes; separate `PATween.Paths` asmdef |
| Blendable tweens | Candidate | Additive composition; **needs ADR first** (multiple writers per property vs storage model) |
| `TweenAssetSO` | Candidate | Shared preset ScriptableObjects |

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
