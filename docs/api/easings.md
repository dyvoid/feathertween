# Easings

 PATween stores one `EaseRef` value per tween. The ease type and its parameters travel together, and new eases can be added without changing the public API.

 ## `EaseRef`

 ```csharp
 public readonly struct EaseRef
 {
     public readonly EaseType type;
     public readonly float    paramA;   // overshoot, strength, amplitude
     public readonly float    paramB;   // period
     public readonly AnimationCurve curve;   // only when type == Curve
     public readonly EaseFunction   func;    // only when type == Custom
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
     BounceExact,   // amplitude in user units (meters/degrees)
     Curve,         // delegates to AnimationCurve slot
     Custom,        // delegates to EaseFunction slot
 }
 ```

 ## `Easing` factories

 ```csharp
 public static class Easing
 {
     public static EaseRef Linear { get; }
     public static EaseRef OutCubic { get; }
     // ... all standard eases as cached EaseRef values

     public static EaseRef OutBack(float overshoot);
     public static EaseRef Bounce(float strength);
     public static EaseRef BounceExact(float amplitude);
     public static EaseRef Elastic(float strength, float period = 0.3f);
     public static EaseRef Curve(AnimationCurve curve);
     public static EaseRef Custom(EaseFunction func);
 }
 ```

 Standard eases are cached as static `EaseRef` instances. Parametric variants construct a struct on the stack (zero heap allocation). The hot path indexes a static function table by `EaseType`, so there is no virtual dispatch.

 ## Usage

 ```csharp
 .SetEase(Easing.OutCubic)
 .SetEase(Easing.OutBack(1.5f))
 .SetEase(Easing.Elastic(1f, 0.3f))
 .SetEase(Easing.Curve(myAnimationCurve))
 .SetEase(Easing.Custom((t, a, b) => t * t))
 ```

 `Curve` reads the `EaseRef.curve` slot at evaluation time. `Custom` reads `EaseRef.func`. All other types use the static eval table.
