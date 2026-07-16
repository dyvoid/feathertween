# Milestones and Phases

## M1 — Core

M1 is the foundation that everything else is built on. It is divided into 14 sequenced phases. Each phase has a narrow deliverable, a green test suite, and an exit criterion. **Phase 1.1 produces scaffolding only — not a working tween engine.** Each subsequent phase layers one capability on top of the previous, and only that capability is what gets tested at that phase's gate.

Phases land as separate PRs / git tags (`m1.1`, `m1.2`, ...). M1 is declared complete when phase 1.14 passes. The base architecture lands in 1.1–1.3 and is frozen against retroactive change after 1.3 ships.

### Phase 1.1 — Storage and handle scaffold (no animation)

**Deliverable**: `TweenStore` skeleton (pooled `TweenData` slots, generation ids, free list, per-phase active lists). `Tween` and `Sequence` struct handles with generation check. `TweenStatus` enum. `TweenStore.Reset()` for Fast Enter Play Mode and `[InitializeOnLoad]`. `FT.SetCapacity(int tweens, int sequences)`. Internal-only `Allocate` / `Free` test seams. Pool exhaustion behavior: grow by doubling, emit `Debug.LogWarning` in Editor. **No PlayerLoop, no interpolation, no builder.**

**Tests**:

- Allocate N slots in a random order, free, verify generation bumps and ids reused
- Stale handles report `IsAlive == false`; control methods on stale handles no-op
- `Reset()` clears active and free lists and bumps every generation
- 10k allocate/free cycles produce 0 managed alloc after pool warmup
- Exhausting the pool doubles the backing arrays; a second exhaustion doubles again; verified with sequential Allocate calls past the initial capacity
- Multi-thread allocate from a `Task.Run` asserts in safe mode

**Exit**: store API has 100% line coverage and can be used as a generic id allocator with no tween semantics leaking in.

### Phase 1.2 — PlayerLoop runner and root scheduler

**Deliverable**: PlayerLoop injection for `Update` / `LateUpdate` / `FixedUpdate`. `Manual` phase via `FeatherTweenRunner.ManualTick(dt)`. Editor mirror via `EditorApplication.update`. Hidden root sequence per phase advances `_localTime` (`double` accumulator). Time sources per design anchors / runner docs. Main-thread assertion. Reset on Fast Enter Play Mode + assembly reload.

**Tests**:

- A probe MonoBehaviour records ordering; the runner ticks after script update for each phase
- `ManualTick` advances only the `Manual` root
- Editor-mode root tickable from an `[InitializeOnLoad]` test
- Domain reload + Fast Enter Play Mode both end with empty active lists and no generation conflict
- Time accumulator drift below 1e-9 per second over a 60s synthetic run with nested timescales

**Exit**: bare runner ticks a no-op root in all four phases, deterministically.

### Phase 1.3 — Builder/handle split and lifecycle

**Deliverable**: `TweenBuilder<T>` struct + pooled backing class. Aliasing semantics per design anchor. `.Start()` consumes the buffer and registers a `TweenData<T>` **stub** that only carries the lifecycle state machine — no interpolation logic yet. Use-after-consume throws in safe mode, no-ops in release. Finalizer leak detection via `ConcurrentQueue<int>` drained by the runner. `TweenStatus` transitions per lifecycle docs.

**Tests**:

- `.Start()` produces a live handle; second `.Start()` on the same builder throws
- Builder copy + modify mutates source (documents aliasing); `Status` after consume reads `Disposed`
- GC'd unconsumed builder logs exactly one warning, on the main thread, after the next tick
- Every lifecycle transition is exercised; illegal transitions silently no-op
- `Pause` / `Resume` / `Kill` on the stub change status and fire `OnKill` (no setter wired yet)

**Exit**: stub tweens move through the full lifecycle without animating anything. **Base architecture is frozen against retroactive change after this gate.**

### Phase 1.4 — Generic tween core (lambda, linear ease)

**Deliverable**: `FT.To<T>(getter, setter, end, duration)` for `float`. `IInterpolator<T>` registry; built-ins for `float`, `Vector2/3/4`, `Color`, `Quaternion`, `int`. Update step: dt → t → setter (linear ease only). `_isUnityObject` cached flag + auto-kill scan. `SetTarget`, `SetUpdate`, `SetAutoKill`, `SetRelative`, `ignoreTimeScale`.

**Tests**:

- 0→1 over 1s tween samples `0.5 ± 1 frame` at 0.5s
- Setter invoked exactly once per tween per tick
- 1k float tweens for 60s: 0 per-frame managed alloc (Profiler delta bounded)
- Auto-kill fires the frame after `Object.Destroy(target)`
- Re-registering an interpolator while a tween of `T` is live throws

**Exit**: a working linear float tween end-to-end.

### Phase 1.5 — Full ease system

**Deliverable**: `EaseRef`, `Easing.X` factories for the full standard set. `Easing.Curve(AnimationCurve)`, `Easing.Custom(EaseFunction)`, `Easing.BounceExact`, parametric `OutBack` / `Elastic`. Static eval table.

**Tests**:

- Each `EaseType` returns documented values at t = 0, 0.25, 0.5, 0.75, 1 (fixture vector)
- `Curve` round-trips an `AnimationCurve`
- `Custom` invokes the user delegate
- Parametric eases honor their parameters (`OutBack(2)` overshoots farther than `OutBack(1)`)

**Exit**: full ease library with no virtual dispatch in the hot path.

### Phase 1.6 — From / FromTo with deferred snap

**Deliverable**: `From()` and `FromTo()` builder methods. `_snapPending` flag. Root snap at `.Start()` regardless of own delay. Sequenced child snap deferred to parent-window entry. Backward seek re-arms the flag.

**Tests**:

- Root `From` snaps the property synchronously inside `.Start()`
- `FromTo` invokes setter with the supplied start value at snap time
- Sequenced child does not snap before its parent-local `_start`
- Backward seek re-snaps on forward re-entry

**Exit**: `From` / `FromTo` work for root tweens and sequenced children without first-frame pops.

### Phase 1.7 — Loops, delays, direction, reverse

**Deliverable**: `SetLoops`, `SetDelay`, `Reverse()`, yoyo / incremental / rewind loop types. Cycle math and boundary events. Per-tween `timeScale`.

**Tests**:

- Yoyo ping-pongs values; incremental adds deltas; rewind snaps back
- Delay defers first update without deferring `From` snap
- `Reverse()` flips direction mid-flight; `Restart()` resets direction to forward
- Infinite loops (`SetLoops(-1)`) keep running until manually stopped

**Exit**: loops and delays behave identically for root tweens and sequenced children.

### Phase 1.8 — Sequence builder

**Deliverable**: `SequenceBuilder` with `Append`, `Insert`, `Join`, `Prepend*`, `AppendInterval`, `AppendCallback`, `AddLabel`, `AddPause`, `Clear`. `Position` type. `SequenceCancelBehavior`. `SetDefaults` cascade frozen at append. Label resolution at `Start()`. Typed child storage (ids only, no boxing). Sequenced-From snap deferred to parent-window entry. Nested sequences. `Sequence` handle control surface.

**Tests**:

- Append/Insert/Join produce expected durations and child ordering
- Labels resolve at `Start()`; duplicate labels throw
- `AddPause` halts playhead; `Resume()` / seek continue
- Nested sequences compose timeScale/pause/reverse recursively
- Zero-alloc sequence tick after warmup

**Exit**: a choreographed sequence can be built and played.

### Phase 1.9 — Callbacks

**Deliverable**: full callback set on both builders, target-capture `OnComplete`/`OnKill`, handle-side `OnStepComplete`, `TweenCommandQueue` deferred-mutation buffer, `TweenOps` dedupe of handle logic, firing-matrix compliance.

**Tests**:

- Every callback fires at the documented moment
- Callbacks from inside callbacks defer structural mutation safely
- `OnKill` does not fire on completion paths
- Zero-alloc callback dispatch after warmup

**Exit**: callbacks are robust and reentrancy-safe.

### Phase 1.10 — Seek and remaining control surface

**Deliverable**: `Tween.Seek` / `Sequence.Seek`; `SequenceBuilder.SetLoops`; `Sequence.Reverse`; `Sequence.TotalProgress`; mid-play `Sequence.Insert`; per-tween `SetTimeScale` + `FT.SetGlobalTimeScale` + per-phase `FT.SetTimeScale`.

**Tests**:

- Seek silent-sample and boundary-walking modes
- Sequence loops (Restart / Yoyo) and reverse
- Mid-play insert does not corrupt active children
- Global and per-phase time scales compose correctly

**Exit**: full runtime control surface works for tweens and sequences.

### Phase 1.11 — Typed shortcuts (lambda)

**Deliverable**: `FT.Move/LocalMove/Scale/Rotate/LocalRotate` (Transform, Euler + Quaternion overloads), `Fade` (CanvasGroup), `Color`/`Fade`/`FillAmount` (Image) — all lambda-core builders with auto-set target.

**Tests**:

- Each shortcut produces the expected end value
- Shortcuts share the same lifecycle and control surface as generic tweens
- Target is set automatically for kill-filter scope

**Exit**: common Unity component tweens are one-liners.

### Phase 1.12 — Filters and bulk ops

**Deliverable**: target-indexed multimap maintained on Start / Kill / Recycle. `Kill(target)`, `IsTweening(target)`, `Kill(id)`, `KillAll`, `PauseAll`, `ResumeAll`. Storage surgery bundled here: per-type `TweenData<T>` pooling so `Start()` is alloc-free after warmup; swap-remove + index map for active lists so `Free()` stops being an O(n) scan.

**Tests**:

- 1k tweens across 100 targets: `Kill(target)` removes only that target's tweens
- Multimap entry removed when the last tween for a target dies
- `IsTweening(target)` returns true iff at least one live tween exists for the target
- Sequence with `SetTarget` participates in the multimap

**Exit**: O(1) bulk filtering.

### Phase 1.13 — Safe mode and assertions

**Deliverable**: `[Conditional]`-style try/catch wrapper around setter and callback invocations; toggleable per tween via `SetSafeMode`. Default on in Editor, off in release. `SetCancelOnError`. Off-thread assertion in safe mode.

**Tests**:

- Setter that throws kills the tween and fires `OnKill` (with `SetCancelOnError(true)`)
- Off-thread `.Start()` asserts in safe mode
- Release build (`FEATHERTWEEN_RELEASE` define) skips the wrapper; verified with IL inspection or an alloc benchmark

**Exit**: safe mode and assertion path verified.

### Phase 1.14 — M1 dev acceptance

**Deliverable**: composed demo. Performance benchmark suite: 10k float tweens; 1k 10-child sequences. This closes the *development* part of M1; it is followed by a hardening pass (full test sweep + code review) before 1.15 finalizes the API. (The hardening pass ran 2026-07-08.)

**Tests**:

- All unit tests green across all phases (Editor + Runtime + Performance, in Unity and in the `tools~/compile-check` harness)
- 0 per-frame managed alloc verified across the benchmark
- Composed demo verified visually

**Exit**: dev-complete; hardening pass (testing + code review) finds nothing blocking.

### Phase 1.15 — API finalization

**Deliverable**: the public surface and its observable semantics are final. Every pending API/semantics decision accumulated in ADRs and the hardening-pass review is resolved and implemented, so 1.16 documents a frozen contract instead of a moving one. All items below are breaking-change-free-zone work (pre-v0.1). Decisions taken 2026-07-16:

- **`Reverse()` during an initial delay**: the delay is part of the timeline — a reversed tween/sequence counts the delay back down before reaching playhead 0 (fixes the current infinite `Delayed` stall where negative `dt` never decrements `delayRemaining`). Applies to both tween-level and sequence-level delays.
- **Dead-handle `OnKill(cb)`**: becomes a no-op (was: fires immediately, test-locked). The invariant "OnKill fires only on actual kills, at most once, at kill time" holds unconditionally; update the existing `TweenBuilderTests` pin.
- **`duration <= 0` with infinite loops**: throw at `Start()` (was: 1e-9 clamp that can explode `cycleIndex` and freeze a frame on `Incremental` cold-cache recompute). Matches ADR 0011's throw-on-invalid direction.
- **`IntInterpolator.Lerp`**: round to nearest (was: truncate toward zero, which steps asymmetrically across 0). Update affected value-expectation tests in the same change.
- **`TweenData<T>.ForceComplete` stale `localTime`**: fix — sync the playhead on `Complete()` so a later `Seek` on a non-autokill tween starts from the completed position.
- **Manual-phase destroyed-target cleanup**: documented contract, not a mechanism — "keep calling `ManualTick` or kill explicitly; tweens on destroyed targets in a stopped manual phase are not auto-killed."
- **ADR 0008 follow-through**: verify the `Incremental`-without-meaningful-`Subtract` guard story for custom interpolators; throw at `Start()` if the gap is real, otherwise record why not.
- **`AddLabel` resolution**: confirmed as designed — labels resolve at definition time (only `Insert`/`AddPause` defer to `Start()`), duplicate names throw. Document it; no code change.
- **Fast-path freeze check (design only, pulled from M2)**: verify that the M2 hand-written zero-alloc fast paths can live *inside* the existing `TweenBuilder<T>` return type (e.g. buffer-side setter-mode discriminator + target ref instead of a closure pair), so shortcut signatures like `FT.Move(...) → TweenBuilder<Vector3>` are safe to freeze at v0.1. If they can't, decide the shortcut return type now — changing it post-v0.1 breaks the most-used methods in the library. No fast-path implementation in this phase.
- **Blessed API-shape decisions (2026-07-16, see `docs/planning/risks.md` and `docs/guides/conventions.md`)**: rotation default is shortest-path slerp; manual ticking is global-only (no per-tween `Tick`); no `Append(Action)` sugar. No code changes — these confirm current behavior as contract.
- **Abandoned `SequenceBuilder` leak**: documented behavior, not a bug — never-started builders pin child store slots until `TweenStore.Reset()`; the LeakDetector finalizer warning is the mitigation. 1.16 documents it as a known limitation.

**Tests**: new/updated coverage for each behavior change above (reverse-through-delay for tween and sequence, dead-handle OnKill no-op, zero-duration+infinite-loop throw, int rounding golden values, Complete-then-Seek playhead); full suite green in the compile-check harness and in Unity (Editor + PlayMode).

**Exit**: PICKUP's "Open questions / decisions pending" section is empty; the public API is declared final for v0.1.

### Phase 1.16 — Release hygiene and documentation

**Deliverable**: `LICENSE` file, `CHANGELOG.md` per UPM convention, and XML doc comments on every public type and member. Reconcile all docs with the final M1 surface.

**Tests**:

- No CS1591 (missing XML doc) warnings on the public surface; LICENSE and CHANGELOG.md present and referenced from package.json where applicable

**Exit**: M1 release tag (v0.1); dogfood in a real project before declaring the API stable.

## M2 — Polish and ecosystem

### Hand-written zero-alloc fast paths

**Deliverable**: override `Move` / `LocalMove` / `Scale` / `Fade` / `Color` to bypass the lambda core; each emits a static `IInterpolator<T>` instance and a no-closure setter dispatched through a typed-shortcut handle. Generic `FT.To` keeps the lambda pair until M5.

**Tests**: 10k `FT.Move` tweens for 60s: 0 per-frame **and** 0 per-Start managed alloc. Behavior identical to the 1.11 lambda baseline (golden-trace test). Hand-written and lambda paths can coexist in the same sequence.

### TweenSettings serialization

**Deliverable**: `[Serializable] TweenSettings` and `TweenSettings<T>`. `WithDirection`. PropertyDrawer with foldout; AnimationCurve hidden unless `ease == Curve`.

**Tests**: round-trip serialize/deserialize; `WithDirection(true)` swaps start/end; PropertyDrawer renders the documented one-line + foldout layout (Editor-only snapshot); `FT.From(rect, settings)` plays the configured tween.

### Cross-engine comparative benchmark

Composed demo reproducible against DOTween / PrimeTween reference recordings. Per-tween cost within 1.5x of LitMotion's **managed dispatch path** (force-enabled by using a non-blittable value type or `WithCancelOnError`, which bypasses LitMotion's Burst job). The benchmark configuration must be documented alongside results so the number is falsifiable.

### Remaining M2 items

**Planned — production stickiness (do these first in M2)**:

- `SetLink(GameObject, LinkBehavior)` with KillOn/PauseOn/RestartOn variants. `SetTarget` auto-kill only covers *destroyed* objects; pooled objects are disabled and reused, and `PauseOnDisable`/`KillOnDisable` is what prevents that footgun class.
- Awaitables: `TweenAwaiter` for `await tween` (zero-alloc, main-thread resume), built on Unity 6's native `Awaitable` since the package targets 6000.3. Pooled `CustomYieldInstruction` for coroutine `yield return tween.WaitForCompletion()`. Plus `Tween.WaitForKill`, `WaitForPosition`, `WaitForElapsedLoops`.

**Candidates**:

- Zero-alloc target-capture overloads for all callbacks (`OnStart`, `OnPlay`, `OnPause`, `OnUpdate`, `OnStepComplete`, `OnRewind`)
- Typed shortcuts: `RectTransform`, `Material` (color/float/vector by property name), `SpriteRenderer`, `Camera`, `Light`, `AudioSource`
- Shake/Punch shortcuts: `ShakePosition`, `ShakeRotation`, `ShakeScale`, `PunchPosition`, `ShakeCamera`
- `FeatherTween.Extensions` asmdef: optional `transform.PAMove(...)` style extension wrappers around the static shortcuts
- 2-state target-capture overloads (`OnComplete<T0,T1>(s0, s1, (s0,s1) => ...)`)
- `AddPause` and sequence `PlayLabel(string)`
- More filter overloads (string id if profiler justifies)
- Improve safe mode reporting (collected per-frame diagnostics)

## M3 — Power features

- Editor preview window (scrubber is 1.10's `Seek` + the existing editor-mode ticking; the highest-leverage designer feature on the roadmap)
- Stagger helpers (`FT.Stagger(targets, ...)`)
- Speed-based tweens (`FT.PositionAtSpeed`, etc.)
- Path tweens (Linear, CatmullRom) and `LookAt` modes — ship as a separate asmdef (`FeatherTween.Paths`) to protect the minimal-core goal
- Blendable tweens (additive) — **requires an ADR before commitment**: additive composition means multiple writers per property, which cuts against the one-setter-per-tween storage model; this is the only roadmap item that could force an architectural rework
- All parametric eases live (`Easing.OutBack(overshoot)`, `Easing.BounceExact(amp)`, `Easing.Elastic(s, p)`)
- `TweenAssetSO` for shared presets
- UniTask asmdef (weakened case: M2 awaitables target Unity 6 native `Awaitable`; only ship if a consumer actually needs UniTask interop)

## M4 — GSAP parity sugar

- `Position.Parse` for GSAP string DSL (`"+=0.3"`, `"<"`, `">"`, labels)
- `tweenTo(label)` / `tweenFromTo`
- `invalidate()` and `repeatRefresh`
- `globalTime` / cross-timeline coordinate conversion — weakest item on the roadmap (GSAP-ism with no obvious Unity user); drop unless a concrete need appears

## M5 — Optional optimization pass

- **SoA storage**: split `TweenData` into:
  - `TweenDataHot[]` (blittable struct: `start, end, duration, elapsed, timeScale, easeParamA, easeParamB, status`) consumed by Burst.
  - `TweenDataManaged[]` (callbacks, target Object refs, AnimationCurve, EaseFunction delegate) consumed by the managed dispatch loop.
- **Per-type generic storage**: `TweenStorage<TValue, TInterpolator>` where `TInterpolator : unmanaged, IBurstInterpolator<TValue>`. A new `IBurstInterpolator<T> : IInterpolator<T>` interface (added in M5, additive, not breaking) carries the `unmanaged` constraint and unlocks Burst (no virtual dispatch). M1-registered managed `IInterpolator<T>` implementations continue to work in the managed dispatch path.
- **Dual dispatch**: the runner maintains two child lists per phase, a managed list (M1 path, vtable per tick) and a Burst list (M5 path, scheduled as one job per `(TValue, TInterpolator)` storage). Each tween chooses its list at `.Start()` based on whether a `IBurstInterpolator<T>` is registered for `T`.
- **Hot job**:

  ```csharp
  [BurstCompile]
  struct TweenStepJob<TValue, TInterpolator> : IJobParallelFor
      where TValue : unmanaged
      where TInterpolator : unmanaged, IInterpolator<TValue>
  {
      [NativeDisableUnsafePtrRestriction] TweenDataHot<TValue>* DataPtr;
      [ReadOnly] public double DeltaTime;
      [WriteOnly] public NativeArray<TValue> Output;
      [WriteOnly] public NativeList<int>.ParallelWriter CompletedIndexList;
      public void Execute(int i) { /* advance, ease, lerp, write output */ }
  }
  ```

  Schedule 16-element batches in parallel, then a single-threaded managed loop reads `Output[i]` and invokes the user setter delegate per tween. Completed indices go to a parallel writer for batch cleanup.
- **`AnimationCurve` Burst path**: convert to `NativeAnimationCurve` or `UnsafeAnimationCurve` at registration time (LitMotion does this).
- **Source generator for typed shortcuts** to eliminate the per-tween delegate alloc (each generated shortcut emits a static struct interpolator + a no-closure setter).
- **Optional**: `IMotionOptions`-style extension for special parameters (path tweens, color spaces, integer snap modes) carried alongside `TValue`.

**Migration note**: promoting `IInterpolator<T>` to `IBurstInterpolator<T>` is **user-visible**. M1-registered interpolators continue to run on the managed dispatch path unchanged, but custom interpolators that want the Burst path must add the `unmanaged` constraint on `T` and reimplement against the `IBurstInterpolator<T>` interface. Source edits are required, not just a recompile. Documented up front so adopters can plan.
