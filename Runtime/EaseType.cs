namespace Dyvoid.FeatherTween
{
	/// <summary>
	/// The built-in ease shapes. Obtain one as an <see cref="EaseRef"/> via the
	/// <see cref="Easing"/> factories rather than using this enum directly.
	/// </summary>
	public enum EaseType
	{
		/// <summary>Constant speed.</summary>
		Linear,
		/// <summary>Sinusoidal, accelerating from rest.</summary>
		InSine,
		/// <summary>Sinusoidal, decelerating to rest.</summary>
		OutSine,
		/// <summary>Sinusoidal in, then out.</summary>
		InOutSine,
		/// <summary>Quadratic, accelerating from rest.</summary>
		InQuad,
		/// <summary>Quadratic, decelerating to rest.</summary>
		OutQuad,
		/// <summary>Quadratic in, then out.</summary>
		InOutQuad,
		/// <summary>Cubic, accelerating from rest.</summary>
		InCubic,
		/// <summary>Cubic, decelerating to rest.</summary>
		OutCubic,
		/// <summary>Cubic in, then out.</summary>
		InOutCubic,
		/// <summary>Quartic, accelerating from rest.</summary>
		InQuart,
		/// <summary>Quartic, decelerating to rest.</summary>
		OutQuart,
		/// <summary>Quartic in, then out.</summary>
		InOutQuart,
		/// <summary>Quintic, accelerating from rest.</summary>
		InQuint,
		/// <summary>Quintic, decelerating to rest.</summary>
		OutQuint,
		/// <summary>Quintic in, then out.</summary>
		InOutQuint,
		/// <summary>Exponential, accelerating from rest.</summary>
		InExpo,
		/// <summary>Exponential, decelerating to rest.</summary>
		OutExpo,
		/// <summary>Exponential in, then out.</summary>
		InOutExpo,
		/// <summary>Circular, accelerating from rest.</summary>
		InCirc,
		/// <summary>Circular, decelerating to rest.</summary>
		OutCirc,
		/// <summary>Circular in, then out.</summary>
		InOutCirc,
		/// <summary>Pulls back past the start before moving (parametric overshoot).</summary>
		InBack,
		/// <summary>Overshoots the end and settles back (parametric overshoot).</summary>
		OutBack,
		/// <summary>Back in, then out.</summary>
		InOutBack,
		/// <summary>Spring oscillation growing into the motion (parametric amplitude/period).</summary>
		InElastic,
		/// <summary>Spring oscillation settling at the end (parametric amplitude/period).</summary>
		OutElastic,
		/// <summary>Elastic in, then out.</summary>
		InOutElastic,
		/// <summary>Bounces at the start.</summary>
		InBounce,
		/// <summary>Bounces at the end.</summary>
		OutBounce,
		/// <summary>Bounce in, then out.</summary>
		InOutBounce,
		/// <summary>Sampled from a user <c>AnimationCurve</c> (see <see cref="Easing.Curve"/>).</summary>
		Curve,
		/// <summary>Evaluated by a user delegate (see <see cref="Easing.Custom"/>).</summary>
		Custom,
		/// <summary>Physically-derived bounce with exact landing (see <see cref="Easing.BounceExact"/>).</summary>
		BounceExact,
	}
}
