## 8. Performance plan

### 8.1 Allocation budget

- Tween creation: 1 `TweenData<T>` from pool (no alloc after warmup) + 1 delegate pair (getter, setter) for generic form. Typed shortcuts may amortize delegates via cached statics where possible.
- Per-frame step: 0 managed alloc.
- Callback dispatch: 0 alloc; single delegates, not delegate lists, not params arrays.
- Awaiter: `TweenAwaiter` struct is alloc-free on the await side. The continuation registration allocates one delegate per await (standard C# state machine behavior). No `TaskCompletionSource`.
- `Kill(target)` is a linear scan of the active list; O(n) per call. Replaced by the target-indexed multimap in phase 1.12 (filters and bulk ops).

### 8.2 SoA-readiness

Hot fields (`_start, _end, _localTime, _duration, _timeScale, _easeParamA, _easeParamB`) sit at the top of `TweenData` in a struct-of-floats region. The M5 SoA split into `NativeArray<TweenHot>` + managed sidecar does not break public API.

### 8.3 Benchmark methodology

Implemented in the `Tests/Performance` EditMode asmdef. Two separate concerns:

**Allocation guards (hard fail).** Deterministic zero-managed-alloc assertions using `System.GC.GetAllocatedBytesForCurrentThread()` around a synchronous `PATweenRunner.ManualTick` loop. `ManualTick` avoids frame/thread noise, so the delta is exact and CI-safe.

- Steady-state tick of 1k tweens after warmup: delta must be exactly 0 bytes.
- Tick after a kill/realloc cycle: free-list reuse must not allocate.

**Throughput benchmarks (report only).** `Unity.PerformanceTesting` `Measure.Method` cases; numbers are machine-dependent and must never gate a build.

- Tick cost at 1k and 10k concurrent float tweens.
- `Start()` cost per tween.

Cross-engine comparison (DOTween, PrimeTween, LitMotion) is deferred until there is a competitive claim to make; the harness structure mirrors LitMotion's `Tests.Benchmark` so it can be added apples-to-apples later.

The `Unity.PerformanceTesting` package (`com.unity.test-framework.performance`) is a test-only dependency the consuming project must install.

---

## 10. Milestones

### M1 — Core (decomposed into testable phases)

M1 is the foundation that everything else is built on. It is divided into 14 sequenced phases. Each phase has a narrow deliverable, a green test suite, and an exit criterion. **Phase 1.1 produces scaffolding only — not a working tween engine.** Each subsequent phase layers one capability on top of the previous, and only that capability is what gets tested at that phase's gate.

Phases land as separate PRs / git tags (`m1.1`, `m1.2`, ...). M1 is declared complete when phase 1.14 passes. The base architecture lands in 1.1–1.3 and is frozen against retroactive change after 1.3 ships.

**Production-cut reshuffle (2026-07-02, after 1.8 shipped)**: M1 was trimmed to the minimum for a production-ready engine — correctness and footgun-removal over polish — and renumbered to stay linear. Hand-written zero-alloc fast paths (originally 1.12) and TweenSettings serialization (originally 1.15) moved to M2 with their full specs: per-frame ticking is already zero-alloc, so fast paths only remove a per-creation delegate pair (pure optimization, no API impact), and TweenSettings is designer-facing sugar with no consumer yet. The cross-engine comparative benchmark moved out of the acceptance gate to M2 for the same reason (marketing claim, not production readiness). Renumbering: old 1.13 → 1.12 (filters), old 1.14 → 1.13 (safe mode), old 1.16 → 1.14 (acceptance). Git history and documents predating this date use the old numbering.

#### Phase 1.1 — Storage and handle scaffold (no animation)

**Deliverable**: `TweenStore` skeleton (pooled `TweenData` slots, generation ids, free list, per-phase active lists). `Tween` and `Sequence` struct handles with generation check. `TweenStatus` enum. `TweenStore.Reset()` for Fast Enter Play Mode and `[InitializeOnLoad]`. `PATween.SetCapacity(int tweens, int sequences)`. Internal-only `Allocate` / `Free` test seams. Pool exhaustion behavior: grow by doubling, emit `Debug.LogWarning` in Editor (see §4.3). **No PlayerLoop, no interpolation, no builder.**

**Tests**:

- Allocate N slots in a random order, free, verify generation bumps and ids reused
- Stale handles report `IsAlive == false`; control methods on stale handles no-op
- `Reset()` clears active and free lists and bumps every generation
- 10k allocate/free cycles produce 0 managed alloc after pool warmup
- Exhausting the pool doubles the backing arrays; a second exhaustion doubles again; verified with sequential Allocate calls past the initial capacity
- Multi-thread allocate from a `Task.Run` asserts in safe mode

**Exit**: store API has 100% line coverage and can be used as a generic id allocator with no tween semantics leaking in.

#### Phase 1.2 — PlayerLoop runner and root scheduler

**Deliverable**: PlayerLoop injection for `Update` / `LateUpdate` / `FixedUpdate`. `Manual` phase via `PATweenRunner.ManualTick(dt)`. Editor mirror via `EditorApplication.update`. Hidden root sequence per phase advances `_localTime` (`double` accumulator). Time sources per anchor 5 / §4.4. Main-thread assertion. Reset on Fast Enter Play Mode + assembly reload.

**Tests**:

- A probe MonoBehaviour records ordering; the runner ticks after script update for each phase
- `ManualTick` advances only the `Manual` root
- Editor-mode root tickable from an `[InitializeOnLoad]` test
- Domain reload + Fast Enter Play Mode both end with empty active lists and no generation conflict
- Time accumulator drift below 1e-9 per second over a 60s synthetic run with nested timescales

**Exit**: bare runner ticks a no-op root in all four phases, deterministically.

#### Phase 1.3 — Builder/handle split and lifecycle

**Deliverable**: `TweenBuilder<T>` struct + pooled backing class. Aliasing semantics per anchor 2. `.Start()` consumes the buffer and registers a `TweenData<T>` **stub** that only carries the lifecycle state machine — no interpolation logic yet. Use-after-consume throws in safe mode, no-ops in release. Finalizer leak detection via `ConcurrentQueue<int>` drained by the 1.2 runner. `TweenStatus` transitions per §3.14.

**Tests**:

- `.Start()` produces a live handle; second `.Start()` on the same builder throws
- Builder copy + modify mutates source (documents aliasing); `Status` after consume reads `Disposed`
- GC'd unconsumed builder logs exactly one warning, on the main thread, after the next tick
- Every transition in §3.14 is exercised; illegal transitions silently no-op
- `Pause` / `Resume` / `Kill` on the stub change status and fire `OnKill` (no setter wired yet)

**Exit**: stub tweens move through the full lifecycle without animating anything. **Base architecture is frozen against retroactive change after this gate.**

#### Phase 1.4 — Generic tween core (lambda, linear ease)

**Deliverable**: `PATween.To<T>(getter, setter, end, duration)` for `float`. `IInterpolator<T>` registry; built-ins for `float`, `Vector2/3/4`, `Color`, `Quaternion`, `int`. Update step: dt → t → setter (linear ease only). `_isUnityObject` cached flag + auto-kill scan. `SetTarget`, `SetUpdate`, `SetAutoKill`, `SetRelative`, `ignoreTimeScale`.

**Tests**:

- 0→1 over 1s tween samples `0.5 ± 1 frame` at 0.5s
- Setter invoked exactly once per tween per tick
- 1k float tweens for 60s: 0 per-frame managed alloc (Profiler delta bounded)
- Auto-kill fires the frame after `Object.Destroy(target)`
- Re-registering an interpolator while a tween of `T` is live throws (§3.16)

**Exit**: a working linear float tween end-to-end.

#### Phase 1.5 — Full ease system

**Deliverable**: `EaseRef`, `Easing.X` factories for the full standard set. `Easing.Curve(AnimationCurve)`, `Easing.Custom(EaseFunction)`, `Easing.BounceExact`, parametric `OutBack` / `Elastic`. Static eval table.

**Tests**:

- Each `EaseType` returns documented values at t = 0, 0.25, 0.5, 0.75, 1 (fixture vector)
- `Curve` round-trips an `AnimationCurve`
- `Custom` invokes the user delegate
- Parametric eases honor their parameters (`OutBack(2)` overshoots farther than `OutBack(1)`)

**Exit**: full ease library with no virtual dispatch in the hot path.

#### Phase 1.6 — From / FromTo with deferred snap

**Deliverable**: `From()` and `FromTo()` builder methods. `_snapPending` flag. Root snap at `.Start()` regardless of own delay (anchor 13 / §4.7). Sequenced child snap deferred to parent-window entry (validated in 1.8). Backward seek re-arms the flag (validated in 1.10).

**Tests**:

- Root `From` snaps the property synchronously inside `.Start()`
- `FromTo` invokes `setter(from)` at snap time
- `From` getter is read at snap time, not earlier
- `SetDelay` interaction deferred to 1.7

**Exit**: From semantics correct in the root case; sequenced case picked up in 1.8.

#### Phase 1.7 — Loops, delays, direction, reverse

**Deliverable**: `SetLoops(count, LoopType)` for `Restart | Yoyo | Incremental | Rewind`. `SetDelay(seconds, FirstLoop | EveryLoop)`. Per-node `_direction` flag, `Reverse()`, `SetRemainingCycles(int)` / `SetRemainingCycles(bool)`. Yoyo composition rule (§4.10).

**Tests**:

- Yoyo loop produces the documented ping-pong sample sequence at expected times
- Incremental loop adds delta each cycle
- `Reverse()` mid-flight rewinds `_localTime` toward 0; `OnRewind` fires at boundary crossings
- `EveryLoop` delay applied per cycle
- `SetRemainingCycles(true)` stops at the next end-value boundary; infinite loops keep `-1` sentinel
- `From().SetDelay(0.5)` snaps at Start; interpolation begins at 0.5s

**Exit**: loops and reverse semantics complete on a leaf tween.

#### Phase 1.8 — Sequence builder

**Deliverable**: `SequenceBuilder` with `Append` / `Insert` / `Join` / `AppendInterval` / `AppendCallback` / `Prepend*` / `AddLabel` / `AddPause` / `Clear`. `SetDefaults` cascade frozen at append. `Position` typed values; label resolution at `.Start()`. `int[]` heterogeneous child storage. `SetTarget`, `SetCancelBehavior` (`SequenceCancelBehavior` enum). Sequence invariants per §5.2. Sequenced `From` snap deferred to parent-window entry.

**Tests**:

- `Append` / `Insert(time)` / `Insert(Position)` / `Join` produce expected child `_start` and `_end`
- Defaults cascade applied at append, then frozen against later builder mutation
- Label resolution: defined-after-use works at Start; undefined throws at Start
- 100 mixed-`T` `Append` calls produce no boxing alloc (ETW / Profiler check)
- `ContinueOnChildAutoKill` vs `KillSequenceOnChildAutoKill` produce documented behavior
- Sequenced `From` with `Insert(2.0, ...)` does not snap until parent reaches 2s

**Exit**: nested sequences with overlap and labels.

#### Phase 1.9 — Callbacks (full set + multicast + reentrancy)

**Deliverable**: builder-side `OnStart` / `OnPlay` / `OnPause` / `OnUpdate` / `OnStepComplete` / `OnComplete` / `OnKill` / `OnRewind`. Zero-alloc target-capture overloads on `OnComplete` and `OnKill` only; standard delegates for all other callbacks. Handle-side multicast for completion-shaped events. Deferred-mutation command buffer drained at end of tick.

**Design constraint (feeds 1.10)**: `SequenceData.Step` is currently a forward-only delta walk; phase 1.10 reworks it into a shared `AdvanceTo(from, to, fireCallbacks)` boundary walk used by both tick and `Seek`, in both directions. The 1.9 command buffer and `OnUpdate` / `OnStepComplete` dispatch must not bake in forward-only or tick-only assumptions — callbacks fire from boundary crossings, not from "the tick advanced".

**Tests**:

- Firing matrix in §3.14 verified end-to-end for tween and sequence
- Multicast: 3 subscribers fire in registration order
- `Kill` from inside `OnComplete` deferred to end-of-tick (no list-mutation crash)
- Sequence `Insert` from a child's `OnComplete` schedules for the next tick

**Exit**: callbacks safe to use for arbitrary mutation.

#### Phase 1.10 — Seek and remaining control surface

**Deliverable**: `Seek(seconds, fireCallbacks)` per §3.15. `Pause` / `Resume` / `Restart` / `Play` / `Complete` / `Kill(complete)`. `SetTimeScale` (negative rejected). `SetCancelOnError`. Mid-play `Sequence.Insert` (the data plumbing is part of M1; the API was originally tagged M2 but ships here). **`SequenceBuilder.SetLoops(count, LoopType)`** — sequence-level looping via the same `AdvanceTo` boundary walk (loop wrap is a boundary crossing; Restart and Yoyo at minimum, Incremental if it falls out naturally). **Global and per-phase time scale** — `PATween.SetGlobalTimeScale(float)` and `PATween.SetTimeScale(UpdatePhase, float)` applied at the hidden root sequences (goal 1: timeScale composes recursively; the roots already exist, this exposes the knob).

**Tests**:

- Seek without callbacks: single sample at target time; `AppendCallback` / `AddPause` between current and target do not fire
- Seek with callbacks: walks boundaries in temporal order; `AddPause` halts at the pause
- Backward seek across `child._start` re-arms the snap; forward replay snaps again
- Negative `SetTimeScale` throws in safe mode, clamps to 0 in release
- `Kill(true)` on a sequence walks playhead to end with callbacks; `Kill(false)` disposes immediately
- Sequence with `SetLoops(2, Restart)`: children replay with re-armed snaps; callbacks fire per loop; `Duration` reports a single cycle, `TotalProgress` spans all loops
- Sequence with `SetLoops(2, Yoyo)`: second cycle traverses children in reverse window order
- `SetGlobalTimeScale(0.5)` halves observed progress in all phases; per-phase scale composes multiplicatively with per-tween `SetTimeScale`
- Interaction with `ignoreTimeScale` decided and documented (root scale is engine-side, distinct from Unity `Time.timeScale`; proposal: root scale applies to all tweens, `ignoreTimeScale` only opts out of Unity's)

**Exit**: full handle control surface; sequences loop; time is globally controllable.

#### Phase 1.11 — Typed shortcuts (lambda)

**Deliverable**: `PATween.Move` / `Rotate` / `Scale` / `LocalMove` / `LocalRotate` for `Transform`. `Fade` for `CanvasGroup`. `Color`, `FillAmount` for `Image`. All built on the lambda core; per-tween `(getter, setter)` alloc accepted at this phase as the lambda baseline.

**Tests**:

- Each shortcut moves the right component to the right value over time
- Target auto-set so `PATween.Kill(target)` reaches the tween (verified in 1.12)
- `From()` chained on each shortcut snaps the right property

**Exit**: ergonomic API surface against the lambda core.

#### Phase 1.12 — Filters and bulk ops

**Deliverable**: `Dictionary<object, List<int>>` target-indexed multimap maintained on Start / Kill / Recycle. `Kill(target)`, `IsTweening(target)`, `Kill(id)`, `KillAll`, `PauseAll`, `ResumeAll`.

**Tests**:

- 1k tweens across 100 targets: `Kill(target)` removes only that target's tweens, completes under N μs (target hardware budget set in 1.14)
- Multimap entry removed when the last tween for a target dies
- `IsTweening(target)` returns true iff at least one live tween exists for the target
- Sequence with `SetTarget` participates in the multimap

**Exit**: O(1) bulk filtering.

#### Phase 1.13 — Safe mode and assertions

**Deliverable**: `[Conditional]`-style try/catch wrapper around setter and callback invocations; toggleable per tween via `SetSafeMode`. Default on in Editor, off in release. `SetCancelOnError`. Off-thread assertion in safe mode.

**Tests**:

- Setter that throws kills the tween and fires `OnKill` (with `SetCancelOnError(true)`)
- Off-thread `.Start()` asserts in safe mode
- Release build (`PATWEEN_RELEASE` define) skips the wrapper; verified with IL inspection or an alloc benchmark

**Exit**: safe mode and assertion path verified.

#### Phase 1.14 — M1 acceptance

**Deliverable**: composed demo (intro + sequenced multi-tween + overlap + callback + From + label). Performance benchmark suite: 10k float tweens; 1k 10-child sequences. The cross-engine comparative benchmark (DOTween / PrimeTween recordings, LitMotion cost comparison) lives in M2 — it is a competitive claim, not a production-readiness gate. **Release hygiene**: `LICENSE` file (license choice is the user's), `CHANGELOG.md` per UPM convention (Package Manager displays it; keep-a-changelog format), and XML doc comments (`///`) on every public type and member.

**Tests**:

- All unit tests green across all phases (Editor + Runtime + Performance, in Unity and in the `tools~/compile-check` harness)
- 0 per-frame managed alloc verified across the benchmark
- Composed demo verified visually
- No CS1591 (missing XML doc) warnings on the public surface; LICENSE and CHANGELOG.md present and referenced from package.json where applicable

**Exit**: M1 release tag (v0.1); dogfood in a real project before declaring the API stable.

### M2 — Polish and ecosystem

The first three items carry full specs because they were moved out of M1 in the production-cut reshuffle.

#### Hand-written zero-alloc fast paths (moved from M1)

**Deliverable**: override `Move` / `LocalMove` / `Scale` / `Fade` / `Color` to bypass the lambda core; each emits a static `IInterpolator<T>` instance and a no-closure setter dispatched through a typed-shortcut handle. Generic `PATween.To` keeps the lambda pair until M5.

**Tests**: 10k `PATween.Move` tweens for 60s: 0 per-frame **and** 0 per-Start managed alloc (Profiler.GetMonoUsedSizeLong delta bounded). Behavior identical to the 1.11 lambda baseline (golden-trace test seeded with same RNG / same easing). Hand-written and lambda paths can coexist in the same sequence.

#### TweenSettings serialization (moved from M1)

**Deliverable**: `[Serializable] TweenSettings` and `TweenSettings<T>`. `WithDirection`. PropertyDrawer with foldout; AnimationCurve hidden unless `ease == Curve`.

**Tests**: round-trip serialize/deserialize; `WithDirection(true)` swaps start/end; PropertyDrawer renders the documented one-line + foldout layout (Editor-only snapshot); `PATween.From(rect, settings)` plays the configured tween.

#### Cross-engine comparative benchmark (moved from M1 acceptance)

Composed demo reproducible against DOTween / PrimeTween reference recordings. Per-tween cost within 1.5x of LitMotion's **managed dispatch path** (force-enabled by using a non-blittable value type or `WithCancelOnError`, which bypasses LitMotion's Burst job). The benchmark configuration must be documented alongside results so the number is falsifiable.

#### Remaining M2 items

**Planned — production stickiness (do these first in M2)**:

- `SetLink(GameObject, LinkBehavior)` with KillOn/PauseOn/RestartOn variants. `SetTarget` auto-kill only covers *destroyed* objects; pooled objects are disabled and reused, and `PauseOnDisable`/`KillOnDisable` is what prevents that footgun class.
- Awaitables: `TweenAwaiter` for `await tween` (zero-alloc, main-thread resume), built on Unity 6's native `Awaitable` since the package targets 6000.3. Pooled `CustomYieldInstruction` for coroutine `yield return tween.WaitForCompletion()`. Plus `Tween.WaitForKill`, `WaitForPosition`, `WaitForElapsedLoops`.

**Candidates**:

- Zero-alloc target-capture overloads for all callbacks (`OnStart`, `OnPlay`, `OnPause`, `OnUpdate`, `OnStepComplete`, `OnRewind`)
- Typed shortcuts: `RectTransform`, `Material` (color/float/vector by property name), `SpriteRenderer`, `Camera`, `Light`, `AudioSource`
- Shake/Punch shortcuts: `ShakePosition`, `ShakeRotation`, `ShakeScale`, `PunchPosition`, `ShakeCamera`
- `PATween.Extensions` asmdef: optional `transform.PAMove(...)` style extension wrappers around the static shortcuts
- 2-state target-capture overloads (`OnComplete<T0,T1>(s0, s1, (s0,s1) => ...)`)
- `AddPause` and sequence `PlayLabel(string)`
- More filter overloads (string id if profiler justifies)
- Improve safe mode reporting (collected per-frame diagnostics)

### M3 — Power features

- Editor preview window (promoted from M4: a scrubber is 1.10's `Seek` + the existing editor-mode ticking; the highest-leverage designer feature on the roadmap)
- Stagger helpers (`PATween.Stagger(targets, ...)`)
- Speed-based tweens (`PATween.PositionAtSpeed`, etc.)
- Path tweens (Linear, CatmullRom) and `LookAt` modes — ship as a separate asmdef (`PATween.Paths`) to protect the minimal-core goal
- Blendable tweens (additive) — **requires an ADR before commitment**: additive composition means multiple writers per property, which cuts against the one-setter-per-tween storage model; this is the only roadmap item that could force an architectural rework
- All parametric eases live (`Easing.OutBack(overshoot)`, `Easing.BounceExact(amp)`, `Easing.Elastic(s, p)`)
- `TweenAssetSO` for shared presets
- UniTask asmdef (weakened case: M2 awaitables target Unity 6 native `Awaitable`; only ship if a consumer actually needs UniTask interop)

### M4 — GSAP parity sugar

- `Position.Parse` for GSAP string DSL (`"+=0.3"`, `"<"`, `">"`, labels)
- `tweenTo(label)` / `tweenFromTo`
- `invalidate()` and `repeatRefresh`
- `globalTime` / cross-timeline coordinate conversion — weakest item on the roadmap (GSAP-ism with no obvious Unity user); drop unless a concrete need appears

### M5 — Optional optimization pass

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

---

## 11. Risks and open questions

### Risks

- **Parent-timeline-everywhere overhead at the root**: every top-level tween pays for being a "child" of a hidden root. Mitigation: the root iterates a plain flat `List<int>` of active child ids, no per-child parent traversal at the root. Confidence 8/10 this is essentially free.
- **Generation-id check on every public op**: one extra branch per call. Negligible. Confidence 9/10.
- **Vtable dispatch per tween per frame**: each `TweenData<T>.Step()` is virtual in v1. ~1-2 ns per tween on modern CPUs, dominated by setter delegate invocation. Acceptable for M1–M4. M5 SoA path eliminates it.
- **Delegate alloc per generic tween**: unavoidable in v1 without source gen. One getter + one setter per tween. Acceptable for M1–M4.
- **Edit-mode runner**: PlayerLoop is play-mode only. Edit mode uses a parallel `EditorApplication.update` driver over the same store. Assembly reload triggers `TweenStore.Reset()`.
- **Safe mode in release**: people will leave it on and complain about cost, or turn it off and complain about silent failures. Default split (on in Editor, off in Release) is a reasonable compromise; document loudly.
- **Builder GC without consumption**: forgetting `.Start()` is a silent no-op at runtime. The pooled backing record's finalizer enqueues the leak id onto a lock-free `ConcurrentQueue<int>`; the next PlayerLoop tick drains the queue on the main thread and emits `Debug.LogWarning`. No Unity API calls from the finalizer thread. Best-effort Editor diagnostic only; the Roslyn analyzer (optional, M2) is the hard guarantee. Release builds skip both.

### Open questions (not blocking M1)

1. `globalTimeScale` knob on `PATween` (slow-mo everything)? Trivial to add; deferred until requested.
2. Quaternion tweens: shortest-path vs. euler-additive? DOTween offers `RotateMode.Fast`, `FastBeyond360`, etc. Pick a sane default and a single alternative for M2.
3. Should `Tween.SetUpdate(Manual)` allow per-tween manual ticking (`tween.Tick(dt)`) or only via the global `Manual` root? Lean toward the latter for API minimalism.
4. Should `Append(Action)` exist as sugar for `AppendCallback(Action)`? Cheap convenience, possible ambiguity with `Append(Tween)`. Defer.
