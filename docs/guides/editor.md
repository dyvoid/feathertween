## 6. Serialization and Editor

### 6.1 TweenSettings drawer

A `PropertyDrawer` for `TweenSettings` and `TweenSettings<T>` showing one line: `[duration] [ease ▼] [loops]` with a foldout for delay, loopType, easeParamA/B, ignoreTimeScale, and the AnimationCurve (visible only when `ease == Curve`). The generic form additionally shows `startValue`/`endValue` with a `useStartValue` toggle.

### 6.2 TweenAssetSO (optional, M3+)

`ScriptableObject` wrapping a `TweenSettings` (or `TweenSettings<T>`) and a string id. Shared/reusable presets for designers. Deferred until we have user demand.

### 6.3 Editor preview (M4+)

A small EditorWindow that lists active tweens by target, with progress bars and pause/kill buttons. Powered by the same store API the runtime uses.

---

## 7. UniTask integration

Separate asmdef `PATween.UniTask` with `PATWEEN_UNITASK` define.

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
