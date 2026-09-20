# Awaiters and TweenSettings — planned surface (M2)

> **None of this ships in v0.1.0.** Awaitables and `TweenSettings` are M2 `Planned`
> ([`ROADMAP.md`](../ROADMAP.md)); the code samples below will not compile against the current
> package. This page is the design intent the M2 implementation is held to, kept here so the
> shape is settled before it is built. Every other page under `api/` documents shipped API.

## Core awaiter (planned)

Both `TweenBuilder<T>` and `Tween` expose `GetAwaiter()` returning a `TweenAwaiter` struct (implements `INotifyCompletion`). The builder's awaiter calls `.Start()` internally before returning the handle's awaiter.

```csharp
await FT.Move(transform, p, 1f);                  // builder.GetAwaiter() starts implicitly
Tween t = FT.Move(transform, p, 1f).Start();
await t;                                                // await an already-running handle
```

The awaiter resolves on **any** terminal status: `Completed`, `Cancelled`, or auto-killed (target destroyed). The continuation fires **exactly once** at the terminal transition. Within a single tick the firing order is fixed: `OnStepComplete` (per-loop boundary if applicable) → `OnComplete` (if completed normally) → `OnKill` (if killed, including auto-kill) → awaiter continuation. Users differentiate outcomes by checking `tween.Status` after `await`. The bare core awaiter does not throw on cancel.

Allocation: the `TweenAwaiter` struct is alloc-free on the await side. Registering the continuation allocates one delegate per await (standard C# state machine behavior). No `TaskCompletionSource`.

## UniTask integration (M3 candidate)

A separate asmdef `FeatherTween.UniTask` with a `FEATHERTWEEN_UNITASK` define would add cancellation-aware await semantics. The M2 awaiter targets Unity 6's native `Awaitable`, so this ships only if a consumer needs UniTask interop.

```csharp
public static UniTask ToUniTask(this Tween t,
    TweenCancelBehavior cancelBehavior = TweenCancelBehavior.Kill,
    CancellationToken cancellationToken = default);

public static UniTask ToUniTask<T>(this TweenBuilder<T> b,
    TweenCancelBehavior cancelBehavior = TweenCancelBehavior.Kill,
    CancellationToken cancellationToken = default);   // calls .Start() internally
```

Cancellation behaviors: `Kill`, `Complete`, `Pause`, `KillAndThrow`, `CompleteAndCancelAwait`.

Core does **not** reference UniTask. The bare `await tween;` path uses the core awaiter.

## `TweenSettings` (planned)

Designer-facing serializable structs for inspector workflows.

```csharp
[Serializable]
public struct TweenSettings
{
    public float duration;
    public float delay;
    public EaseType ease;          // "Curve" entry selects the curve field
    public AnimationCurve curve;
    public float easeParamA;       // overshoot / strength
    public float easeParamB;       // period
    public int loops;
    public LoopType loopType;
    public UpdatePhase updatePhase;
    public bool ignoreTimeScale;
}

[Serializable]
public struct TweenSettings<T>
{
    public T startValue;
    public T endValue;
    public bool useStartValue;     // if false, animate from current
    public TweenSettings settings;

    public TweenSettings<T> WithDirection(bool toEndValue);
}
```

Usage:

```csharp
[SerializeField] TweenSettings<float> fadeAnim;
public void SetOpen(bool open) =>
    FT.Fade(canvasGroup, fadeAnim.WithDirection(toEndValue: open)).Start();
```

The custom drawer collapses common fields into a single line with a foldout for advanced options. The AnimationCurve field is hidden unless `ease == Curve`. No reflection at runtime.
