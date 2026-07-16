# Custom Value Types

FeatherTween can tween any value type for which you provide an `IInterpolator<T>`.

## `IInterpolator<T>`

```csharp
public interface IInterpolator<T>
{
    T Lerp(T from, T to, float t);
    T Add(T a, T b);
    T Subtract(T a, T b);
}
```

Built-ins in M1: `float`, `Vector2`, `Vector3`, `Vector4`, `Color`, `Quaternion`, `int`.

- `int` lerps by rounding to nearest (symmetric across 0), not truncating.
- All three members are required and assumed meaningful: `Add`/`Subtract` power `SetRelative` and `Incremental` loops (ADR 0008). There is no "Lerp-only" registration, so `Incremental` cannot be detected as unsupported at `Start()` — an interpolator whose `Subtract` throws surfaces that exception on the first incremental cycle computation.

## Registration

```csharp
Interpolators.Register<T>(IInterpolator<T> interpolator);
Interpolators.Get<T>();   // throws if none registered
```

- Registering while **any** tween of `T` is live throws (`InvalidOperationException`), even when re-registering the same implementation — the interpolator is captured per tween at build time, and a mid-flight swap would split behavior between old and new tweens.
- Like the rest of the engine, the registry is main-thread only.

## Example

```csharp
public struct RectInterpolator : IInterpolator<Rect>
{
    public Rect Lerp(Rect a, Rect b, float t) => Rect.Lerp(a, b, t);
    public Rect Add(Rect a, Rect b) => new(a.x + b.x, a.y + b.y, a.width + b.width, a.height + b.height);
    public Rect Subtract(Rect a, Rect b) => new(a.x - b.x, a.y - b.y, a.width - b.width, a.height - b.height);
}

Interpolators.Register<Rect>(new RectInterpolator());
```

M5 adds `IBurstInterpolator<T>` with an `unmanaged` constraint for the Burst path. M1-registered managed interpolators continue to work unchanged.
