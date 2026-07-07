# Awaiters and TweenSettings

## Core awaiter

Both `TweenBuilder<T>` and `Tween` expose `GetAwaiter()` returning a `TweenAwaiter` struct (implements `INotifyCompletion`). The builder's awaiter calls `.Start()` internally before returning the handle's awaiter.

```csharp
await PATween.Move(transform, p, 1f);                  // builder.GetAwaiter() starts implicitly
Tween t = PATween.Move(transform, p, 1f).Start();
await t;                                                // await an already-running handle
```

The awaiter resolves on **any** terminal status: `Completed`, `Cancelled`, or auto-killed (target destroyed). The continuation fires **exactly once** at the terminal transition. Within a single tick the firing order is fixed: `OnStepComplete` (per-loop boundary if applicable) → `OnComplete` (if completed normally) → `OnKill` (if killed, including auto-kill) → awaiter continuation. Users differentiate outcomes by checking `tween.Status` after `await`. The bare core awaiter does not throw on cancel.

Allocation: the `TweenAwaiter` struct is alloc-free on the await side. Registering the continuation allocates one delegate per await (standard C# state machine behavior). No `TaskCompletionSource`.

## UniTask integration

A separate asmdef `PATween.UniTask` with `PATWEEN_UNITASK` define adds cancellation-aware await semantics.

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

## `TweenSettings`

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
[SerializeField] TweenSettings<float> windowAnim;
public void SetOpen(bool open) =>
    PATween.AnchoredPosY(rect, windowAnim.WithDirection(toEndValue: open)).Start();
```

The custom drawer collapses common fields into a single line with a foldout for advanced options. The AnimationCurve field is hidden unless `ease == Curve`. No reflection at runtime.
