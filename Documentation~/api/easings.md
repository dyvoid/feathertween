# Easings

FeatherTween stores one `EaseRef` value per tween. The ease type and its parameters travel together, and new eases can be added without changing the public API.

## `EaseRef`

```csharp
public readonly struct EaseRef
{
    public EaseType Type { get; }
    public float    ParamA { get; }   // overshoot (Back), amplitude (Elastic/BounceExact)
    public float    ParamB { get; }   // period (Elastic)
    public AnimationCurve    Curve { get; }    // only when Type == Curve
    public Func<float,float> Custom { get; }   // only when Type == Custom

    public float Evaluate(float t);
    public static EaseRef Linear { get; }      // the default ease
}
```

## `EaseType`

```csharp
public enum EaseType
{
    Linear,
    InSine,    OutSine,    InOutSine,
    InQuad,    OutQuad,    InOutQuad,
    InCubic,   OutCubic,   InOutCubic,
    InQuart,   OutQuart,   InOutQuart,
    InQuint,   OutQuint,   InOutQuint,
    InExpo,    OutExpo,    InOutExpo,
    InCirc,    OutCirc,    InOutCirc,
    InBack,    OutBack,    InOutBack,
    InElastic, OutElastic, InOutElastic,
    InBounce,  OutBounce,  InOutBounce,
    Curve,         // delegates to the AnimationCurve slot
    Custom,        // delegates to the Func<float,float> slot
    BounceExact,   // amplitude in user units (meters/degrees)
}
```

## `Easing` factories

Every factory is a method (including the parameterless ones) returning an `EaseRef` by value:

```csharp
public static class Easing
{
    public static EaseRef Linear();
    public static EaseRef OutCubic();
    // ... the full standard set: In/Out/InOut × Sine, Quad, Cubic, Quart,
    // Quint, Expo, Circ, Bounce

    public static EaseRef InBack(float overshoot = 1.70158f);     // also Out/InOut
    public static EaseRef OutElastic(float amplitude = 1f, float period = 0.3f);  // also In/InOut
    public static EaseRef BounceExact(float amplitude);
    public static EaseRef Curve(AnimationCurve curve);
    public static EaseRef Custom(Func<float, float> fn);          // fn maps linear t (0..1) to eased progress
}
```

`EaseRef` is a readonly struct, so every factory call is stack construction — zero heap allocation, parametric or not. The hot path indexes a static function table by `EaseType`, so there is no virtual dispatch.

## Usage

```csharp
.SetEase(Easing.OutCubic())
.SetEase(Easing.OutBack(1.5f))
.SetEase(Easing.OutElastic(1f, 0.3f))
.SetEase(Easing.Curve(myAnimationCurve))
.SetEase(myAnimationCurve)              // shorthand for Easing.Curve(...)
.SetEase(Easing.Custom(t => t * t))
```

`Curve` reads the `EaseRef.Curve` slot at evaluation time. `Custom` reads `EaseRef.Custom`. All other types use the static eval table.
