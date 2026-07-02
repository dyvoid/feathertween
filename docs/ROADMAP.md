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
| Sequence builder | In flight | `SequenceBuilder`, Append/Insert/Join, labels, Position values | 0006 |
| Callbacks (full set + multicast + reentrancy) | Planned | Builder-side callbacks, handle-side multicast, deferred mutation | — |
| Seek and remaining control surface | Planned | `Seek`, `Restart`, `Complete`, `Kill(complete)`, mid-play `Insert` | — |
| Typed shortcuts (lambda) | Planned | `Move`, `Rotate`, `Scale`, `Fade`, `Color` — lambda baseline | — |
| Hand-written zero-alloc fast paths | Planned | Bypass lambda core for common shortcuts, 0 alloc on `Start` and tick | — |
| Filters and bulk ops | Planned | Target-indexed multimap, `Kill(target)`, `IsTweening`, `KillAll` | — |
| Safe mode and assertions | Planned | Try/catch wrapper, off-thread assertions, release-build skip | — |
| TweenSettings serialization | Planned | `[Serializable] TweenSettings<T>`, PropertyDrawer, `WithDirection` | — |
| M1 acceptance | Planned | Composed demo, cross-engine benchmark, 30+ unit tests green | — |

## M2 — Polish and ecosystem

| Feature | Status | Description |
|---------|--------|-------------|
| Zero-alloc target-capture overloads for all callbacks | Candidate | Extend beyond `OnComplete` / `OnKill` |
| `SetLink(GameObject, LinkBehavior)` | Candidate | KillOn/PauseOn/RestartOn variants |
| Typed shortcuts expansion | Candidate | `RectTransform`, `Material`, `SpriteRenderer`, `Camera`, `Light`, `AudioSource` |
| Shake / Punch shortcuts | Candidate | `ShakePosition`, `ShakeRotation`, `ShakeScale`, `PunchPosition` |
| Extension method asmdef | Candidate | Optional `transform.PAMove(...)` wrappers |
| Awaitables | Candidate | `TweenAwaiter`, `WaitForCompletion`, `WaitForKill`, `WaitForPosition` |
| Improved safe-mode reporting | Candidate | Collected per-frame diagnostics |

## M3 — Power features

| Feature | Status | Description |
|---------|--------|-------------|
| UniTask integration | Candidate | Dedicated asmdef for UniTask support |
| Stagger helpers | Candidate | `PATween.Stagger(targets, ...)` |
| Speed-based tweens | Candidate | `PATween.PositionAtSpeed`, etc. |
| Path tweens | Candidate | Linear / CatmullRom paths, `LookAt` modes |
| Blendable tweens | Candidate | Additive composition |
| `TweenAssetSO` | Candidate | Shared preset ScriptableObjects |

## M4 — GSAP parity sugar

| Feature | Status | Description |
|---------|--------|-------------|
| `Position.Parse` DSL | Candidate | `"+=0.3"`, `"<"`, `">"`, label references |
| `tweenTo` / `tweenFromTo` | Candidate | GSAP-style timeline navigation |
| `invalidate` / `repeatRefresh` | Candidate | Runtime re-evaluation of start values |
| Cross-timeline coordinates | Candidate | `globalTime` conversion |
| Editor preview window | Candidate | Visual scrubber in Editor |

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
