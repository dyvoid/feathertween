# Tweens

## Generic tween creation

The lambda core is the most flexible form. You supply a getter, a setter, an end value, and a duration.

```csharp
Tween t = PATween.To(
    () => obj.value,
    v  => obj.value = v,
    endValue: 10f,
    duration: 1f
)
.SetEase(Easing.OutCubic)
.SetLoops(2, LoopType.Yoyo)
.OnComplete(() => Debug.Log("done"))
.Start();
```

Supported value types in M1: `float`, `Vector2`, `Vector3`, `Vector4`, `Color`, `Quaternion`, `int` (snapping). Custom blittable types via `IInterpolator<T>`.

### Zero-alloc target-capture variant

```csharp
PATween.To(this, () => x.value, (s, v) => s.x.value = v, 10f, 1f)
    .OnComplete(this, s => s.HandleDone())
    .Start();
```

Target-capture overloads avoid closure allocation when the lambda body does not capture any outer variables. If you reference `this`, a local, or any field outside the supplied state parameter, the C# compiler emits a closure-allocating delegate and the benefit is lost. Use `static` lambdas where possible.

## Typed shortcuts

Static methods on `PATween` cover common Unity components. Each shortcut builds a lambda pair internally, sets `target` automatically, and returns a `TweenBuilder<T>`.

```csharp
PATween.Move(transform, new Vector3(2, 3, 4), 1f).Start();
PATween.LocalMove(transform, new Vector3(2, 3, 4), 1f).Start();
PATween.Rotate(transform, new Vector3(0, 180, 0), 1f).Start();
PATween.LocalRotate(transform, new Vector3(0, 180, 0), 1f).Start();
PATween.Scale(transform, Vector3.one * 2f, 1f).Start();

PATween.Fade(canvasGroup, 0f, 0.5f).Start();
PATween.Color(image, UnityEngine.Color.red, 0.3f).Start();
PATween.Fade(image, 0f, 0.3f).Start();
PATween.FillAmount(image, 0.5f, 0.3f).Start();
```

Euler overloads take a `Vector3`; quaternion overloads take a `Quaternion`. The shortcut sets the kill-filter target automatically so `PATween.Kill(transform)` reaches the tween.

### Fire and forget

```csharp
PATween.Move(transform, target, 1f).SetEase(Easing.OutCubic).Start();
```

A started shortcut behaves exactly like a generic tween: it exposes the full handle control surface, supports callbacks, and participates in sequences.

## From / FromTo

See [builders.md](builders.md#from--fromto) for the full builder methods. The short version:

```csharp
PATween.Move(transform, target.position, 1f).From().Start();
PATween.From(() => x, v => x = v, startValue, 1f).Start();
PATween.FromTo(() => x, v => x = v, from, to, 1f).Start();
```

Root tweens snap at `.Start()`; sequenced children snap when the parent playhead first crosses their start time.
