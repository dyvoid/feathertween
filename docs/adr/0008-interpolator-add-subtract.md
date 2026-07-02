# ADR 0008 — `IInterpolator<T>` carries `Add` and `Subtract`

## Status

Accepted (M1, phase 1.7).

## Context

`Incremental` loops require composing the per-cycle delta as `start + N · (end - start)` for arbitrary `T`. The minimal `IInterpolator<T>` shape (`Lerp` only) cannot express the subtraction step generically. Workarounds (precompute delta at builder time, inverse-lerp extrapolation, restrict `Incremental` to built-in types) all push the same operation into a less-discoverable place or constrain user-defined interpolators.

## Decision

`IInterpolator<T>` exposes three operations:

```csharp
public interface IInterpolator<T>
{
    T Lerp(T from, T to, float t);
    T Add(T a, T b);
    T Subtract(T a, T b);
}
```

`Add` is the inverse-direction composition (used by `SetRelative` and `Incremental`). `Subtract` is the difference (used by `Incremental` to derive cycle delta from `start` and `end`). The `in` modifier on parameters is omitted; copy elision by the JIT covers built-in struct sizes (`float`, `Vector2..4`, `Color`, `Quaternion`) and the `in` indirection has no measured benefit at v1 scale.

For `Quaternion`, `Subtract(a, b) := a * Quaternion.Inverse(b)` (the rotation that takes `b` to `a`).

## Consequences

User-defined interpolators must implement all three. The contract is small enough that this is not a burden, and it makes `Incremental` work uniformly for any `T`.

If a future interpolator type cannot meaningfully define `Subtract` (e.g. a non-group type), `Incremental` is unsupported for that type; the builder can throw at `.Start()` time.
