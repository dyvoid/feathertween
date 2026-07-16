# Tweens

## Generic tween creation

Every creation method follows one shape: **subject first** (the thing being animated), then endpoint value(s), then duration. For generic tweens the subject is a getter/setter pair — or just a setter when the engine never needs to read the value.

```csharp
Tween t = FT.To(
    () => obj.value,
    v  => obj.value = v,
    to: 10f,
    duration: 1f
)
.SetEase(Easing.OutCubic())
.SetLoops(2, LoopType.Yoyo)
.OnComplete(() => Debug.Log("done"))
.Start();
```

When both endpoints are explicit, no getter exists — `FromTo` takes only the setter:

```csharp
FT.FromTo(v => obj.value = v, 0f, 10f, 1f).Start();
```

Supported value types in M1: `float`, `Vector2`, `Vector3`, `Vector4`, `Color`, `Quaternion`, `int` (snapping). Custom blittable types via `IInterpolator<T>`.

### Zero-alloc target-capture callbacks

```csharp
FT.To(() => x.value, v => x.value = v, 10f, 1f)
    .OnComplete(this, s => s.HandleDone())
    .Start();
```

`OnComplete`/`OnKill` target-capture overloads avoid closure allocation when the lambda body does not capture any outer variables. If you reference `this`, a local, or any field outside the supplied state parameter, the C# compiler emits a closure-allocating delegate and the benefit is lost. Use `static` lambdas where possible. Target-capture overloads for tween *creation* (state passed alongside the getter/setter pair) are an M2 candidate and do not exist yet.

## Typed shortcuts

Static methods on `FT` cover common Unity components. Each shortcut builds a lambda pair internally, sets `target` automatically, and returns a `TweenBuilder<T>`.

```csharp
FT.Move(transform, new Vector3(2, 3, 4), 1f).Start();
FT.LocalMove(transform, new Vector3(2, 3, 4), 1f).Start();
FT.Rotate(transform, new Vector3(0, 180, 0), 1f).Start();
FT.LocalRotate(transform, new Vector3(0, 180, 0), 1f).Start();
FT.Scale(transform, Vector3.one * 2f, 1f).Start();

FT.Fade(canvasGroup, 0f, 0.5f).Start();
FT.Color(image, UnityEngine.Color.red, 0.3f).Start();
FT.Fade(image, 0f, 0.3f).Start();
FT.FillAmount(image, 0.5f, 0.3f).Start();
```

Euler overloads take a `Vector3`; quaternion overloads take a `Quaternion`. The shortcut sets the kill-filter target automatically so `FT.Kill(transform)` reaches the tween.

### Fire and forget

```csharp
FT.Move(transform, target, 1f).SetEase(Easing.OutCubic()).Start();
```

A started shortcut behaves exactly like a generic tween: it exposes the full handle control surface, supports callbacks, and participates in sequences.

## From / FromTo

See [builders.md](builders.md#from--fromto) for the full builder methods. The short version:

```csharp
FT.Move(transform, dest, 1f).From().Start();          // swap: end value is the start
FT.Move(transform, dest, 1f).From(spawnPos).Start();  // explicit start, no lambdas at all
FT.From(() => x, v => x = v, from, 1f).Start();
FT.FromTo(v => x = v, from, to, 1f).Start();
```

Root tweens snap at `.Start()`; sequenced children snap when the parent playhead first crosses their start time. `To` and `From` read the getter lazily at snap time; `FromTo` and `From(value)` have both endpoints explicit, so their values are captured when the creation call runs — inside a sequence, a mutation between build and playback is picked up by the getter forms but not by the explicit forms.
