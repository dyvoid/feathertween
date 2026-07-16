using System;
using UnityEngine;

namespace Dyvoid.FeatherTween
{
	/// <summary>
	/// Factories producing <see cref="EaseRef"/> values for <c>SetEase</c>.
	/// Parameters travel inside the returned value; there is no shared state.
	/// </summary>
	public static class Easing
	{
		/// <summary>Constant speed.</summary>
		public static EaseRef Linear() => new EaseRef(EaseType.Linear, 0f, 0f, null, null);

		/// <summary>Sinusoidal ease-in.</summary>
		public static EaseRef InSine() => new EaseRef(EaseType.InSine, 0f, 0f, null, null);
		/// <summary>Sinusoidal ease-out.</summary>
		public static EaseRef OutSine() => new EaseRef(EaseType.OutSine, 0f, 0f, null, null);
		/// <summary>Sinusoidal ease-in-out.</summary>
		public static EaseRef InOutSine() => new EaseRef(EaseType.InOutSine, 0f, 0f, null, null);

		/// <summary>Quadratic ease-in.</summary>
		public static EaseRef InQuad() => new EaseRef(EaseType.InQuad, 0f, 0f, null, null);
		/// <summary>Quadratic ease-out.</summary>
		public static EaseRef OutQuad() => new EaseRef(EaseType.OutQuad, 0f, 0f, null, null);
		/// <summary>Quadratic ease-in-out.</summary>
		public static EaseRef InOutQuad() => new EaseRef(EaseType.InOutQuad, 0f, 0f, null, null);

		/// <summary>Cubic ease-in.</summary>
		public static EaseRef InCubic() => new EaseRef(EaseType.InCubic, 0f, 0f, null, null);
		/// <summary>Cubic ease-out.</summary>
		public static EaseRef OutCubic() => new EaseRef(EaseType.OutCubic, 0f, 0f, null, null);
		/// <summary>Cubic ease-in-out.</summary>
		public static EaseRef InOutCubic() => new EaseRef(EaseType.InOutCubic, 0f, 0f, null, null);

		/// <summary>Quartic ease-in.</summary>
		public static EaseRef InQuart() => new EaseRef(EaseType.InQuart, 0f, 0f, null, null);
		/// <summary>Quartic ease-out.</summary>
		public static EaseRef OutQuart() => new EaseRef(EaseType.OutQuart, 0f, 0f, null, null);
		/// <summary>Quartic ease-in-out.</summary>
		public static EaseRef InOutQuart() => new EaseRef(EaseType.InOutQuart, 0f, 0f, null, null);

		/// <summary>Quintic ease-in.</summary>
		public static EaseRef InQuint() => new EaseRef(EaseType.InQuint, 0f, 0f, null, null);
		/// <summary>Quintic ease-out.</summary>
		public static EaseRef OutQuint() => new EaseRef(EaseType.OutQuint, 0f, 0f, null, null);
		/// <summary>Quintic ease-in-out.</summary>
		public static EaseRef InOutQuint() => new EaseRef(EaseType.InOutQuint, 0f, 0f, null, null);

		/// <summary>Exponential ease-in.</summary>
		public static EaseRef InExpo() => new EaseRef(EaseType.InExpo, 0f, 0f, null, null);
		/// <summary>Exponential ease-out.</summary>
		public static EaseRef OutExpo() => new EaseRef(EaseType.OutExpo, 0f, 0f, null, null);
		/// <summary>Exponential ease-in-out.</summary>
		public static EaseRef InOutExpo() => new EaseRef(EaseType.InOutExpo, 0f, 0f, null, null);

		/// <summary>Circular ease-in.</summary>
		public static EaseRef InCirc() => new EaseRef(EaseType.InCirc, 0f, 0f, null, null);
		/// <summary>Circular ease-out.</summary>
		public static EaseRef OutCirc() => new EaseRef(EaseType.OutCirc, 0f, 0f, null, null);
		/// <summary>Circular ease-in-out.</summary>
		public static EaseRef InOutCirc() => new EaseRef(EaseType.InOutCirc, 0f, 0f, null, null);

		/// <summary>Pulls back past the start before moving; larger <paramref name="overshoot"/> pulls farther.</summary>
		public static EaseRef InBack(float overshoot = 1.70158f) => new EaseRef(EaseType.InBack, overshoot, 0f, null, null);
		/// <summary>Overshoots the end and settles back; larger <paramref name="overshoot"/> overshoots farther.</summary>
		public static EaseRef OutBack(float overshoot = 1.70158f) => new EaseRef(EaseType.OutBack, overshoot, 0f, null, null);
		/// <summary>Back ease on both halves.</summary>
		public static EaseRef InOutBack(float overshoot = 1.70158f) => new EaseRef(EaseType.InOutBack, overshoot, 0f, null, null);

		/// <summary>Spring oscillation growing into the motion (Penner elastic; <paramref name="amplitude"/> scales swing, <paramref name="period"/> sets oscillation length).</summary>
		public static EaseRef InElastic(float amplitude = 1f, float period = 0.3f) => new EaseRef(EaseType.InElastic, amplitude, period, null, null);
		/// <summary>Spring oscillation settling at the end (Penner elastic; <paramref name="amplitude"/> scales swing, <paramref name="period"/> sets oscillation length).</summary>
		public static EaseRef OutElastic(float amplitude = 1f, float period = 0.3f) => new EaseRef(EaseType.OutElastic, amplitude, period, null, null);
		/// <summary>Elastic ease on both halves.</summary>
		public static EaseRef InOutElastic(float amplitude = 1f, float period = 0.3f) => new EaseRef(EaseType.InOutElastic, amplitude, period, null, null);

		/// <summary>Bounces at the start.</summary>
		public static EaseRef InBounce() => new EaseRef(EaseType.InBounce, 0f, 0f, null, null);
		/// <summary>Bounces at the end.</summary>
		public static EaseRef OutBounce() => new EaseRef(EaseType.OutBounce, 0f, 0f, null, null);
		/// <summary>Bounce on both halves.</summary>
		public static EaseRef InOutBounce() => new EaseRef(EaseType.InOutBounce, 0f, 0f, null, null);

		/// <summary>Samples a user <see cref="AnimationCurve"/> as the ease shape (evaluated over t = 0..1).</summary>
		public static EaseRef Curve(AnimationCurve curve)
		{
			if (curve == null)
			{
				throw new ArgumentNullException(nameof(curve));
			}
			return new EaseRef(EaseType.Curve, 0f, 0f, curve, null);
		}

		/// <summary>Evaluates a user delegate as the ease shape; <paramref name="fn"/> maps linear t (0..1) to eased progress.</summary>
		public static EaseRef Custom(Func<float, float> fn)
		{
			if (fn == null)
			{
				throw new ArgumentNullException(nameof(fn));
			}
			return new EaseRef(EaseType.Custom, 0f, 0f, null, fn);
		}

		/// <summary>Physically-derived bounce landing exactly at the end; <paramref name="amplitude"/> scales bounce height.</summary>
		public static EaseRef BounceExact(float amplitude) => new EaseRef(EaseType.BounceExact, amplitude, 0f, null, null);
	}
}
