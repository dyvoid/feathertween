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

## Registration

```csharp
FT.RegisterInterpolator<T, TInterp>() where TInterp : struct, IInterpolator<T>;
```

- Main-thread only; throws if called off-thread.
- Idempotent if the same `TInterp` is re-registered for the same `T`. Re-registering with a different `TInterp` while any tween of `T` is live throws.
- `TweenStore.Reset()` clears registrations; built-ins are re-registered automatically by the runner installer.

## Example

```csharp
public struct RectInterpolator : IInterpolator<Rect>
{
    public Rect Lerp(Rect a, Rect b, float t) => Rect.Lerp(a, b, t);
    public Rect Add(Rect a, Rect b) => new(a.x + b.x, a.y + b.y, a.width + b.width, a.height + b.height);
    public Rect Subtract(Rect a, Rect b) => new(a.x - b.x, a.y - b.y, a.width - b.width, a.height - b.height);
}

FT.RegisterInterpolator<Rect, RectInterpolator>();
```

M5 adds `IBurstInterpolator<T>` with an `unmanaged` constraint for the Burst path. M1-registered managed interpolators continue to work unchanged.
