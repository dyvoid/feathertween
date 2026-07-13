using System;
using UnityEngine;

namespace Dyvoid.FeatherTween
{
	public static class Easing
	{
		public static EaseRef Linear() => new EaseRef(EaseType.Linear, 0f, 0f, null, null);

		public static EaseRef InSine() => new EaseRef(EaseType.InSine, 0f, 0f, null, null);
		public static EaseRef OutSine() => new EaseRef(EaseType.OutSine, 0f, 0f, null, null);
		public static EaseRef InOutSine() => new EaseRef(EaseType.InOutSine, 0f, 0f, null, null);

		public static EaseRef InQuad() => new EaseRef(EaseType.InQuad, 0f, 0f, null, null);
		public static EaseRef OutQuad() => new EaseRef(EaseType.OutQuad, 0f, 0f, null, null);
		public static EaseRef InOutQuad() => new EaseRef(EaseType.InOutQuad, 0f, 0f, null, null);

		public static EaseRef InCubic() => new EaseRef(EaseType.InCubic, 0f, 0f, null, null);
		public static EaseRef OutCubic() => new EaseRef(EaseType.OutCubic, 0f, 0f, null, null);
		public static EaseRef InOutCubic() => new EaseRef(EaseType.InOutCubic, 0f, 0f, null, null);

		public static EaseRef InQuart() => new EaseRef(EaseType.InQuart, 0f, 0f, null, null);
		public static EaseRef OutQuart() => new EaseRef(EaseType.OutQuart, 0f, 0f, null, null);
		public static EaseRef InOutQuart() => new EaseRef(EaseType.InOutQuart, 0f, 0f, null, null);

		public static EaseRef InQuint() => new EaseRef(EaseType.InQuint, 0f, 0f, null, null);
		public static EaseRef OutQuint() => new EaseRef(EaseType.OutQuint, 0f, 0f, null, null);
		public static EaseRef InOutQuint() => new EaseRef(EaseType.InOutQuint, 0f, 0f, null, null);

		public static EaseRef InExpo() => new EaseRef(EaseType.InExpo, 0f, 0f, null, null);
		public static EaseRef OutExpo() => new EaseRef(EaseType.OutExpo, 0f, 0f, null, null);
		public static EaseRef InOutExpo() => new EaseRef(EaseType.InOutExpo, 0f, 0f, null, null);

		public static EaseRef InCirc() => new EaseRef(EaseType.InCirc, 0f, 0f, null, null);
		public static EaseRef OutCirc() => new EaseRef(EaseType.OutCirc, 0f, 0f, null, null);
		public static EaseRef InOutCirc() => new EaseRef(EaseType.InOutCirc, 0f, 0f, null, null);

		public static EaseRef InBack(float overshoot = 1.70158f) => new EaseRef(EaseType.InBack, overshoot, 0f, null, null);
		public static EaseRef OutBack(float overshoot = 1.70158f) => new EaseRef(EaseType.OutBack, overshoot, 0f, null, null);
		public static EaseRef InOutBack(float overshoot = 1.70158f) => new EaseRef(EaseType.InOutBack, overshoot, 0f, null, null);

		public static EaseRef InElastic(float amplitude = 1f, float period = 0.3f) => new EaseRef(EaseType.InElastic, amplitude, period, null, null);
		public static EaseRef OutElastic(float amplitude = 1f, float period = 0.3f) => new EaseRef(EaseType.OutElastic, amplitude, period, null, null);
		public static EaseRef InOutElastic(float amplitude = 1f, float period = 0.3f) => new EaseRef(EaseType.InOutElastic, amplitude, period, null, null);

		public static EaseRef InBounce() => new EaseRef(EaseType.InBounce, 0f, 0f, null, null);
		public static EaseRef OutBounce() => new EaseRef(EaseType.OutBounce, 0f, 0f, null, null);
		public static EaseRef InOutBounce() => new EaseRef(EaseType.InOutBounce, 0f, 0f, null, null);

		public static EaseRef Curve(AnimationCurve curve)
		{
			if (curve == null)
			{
				throw new ArgumentNullException(nameof(curve));
			}
			return new EaseRef(EaseType.Curve, 0f, 0f, curve, null);
		}

		public static EaseRef Custom(Func<float, float> fn)
		{
			if (fn == null)
			{
				throw new ArgumentNullException(nameof(fn));
			}
			return new EaseRef(EaseType.Custom, 0f, 0f, null, fn);
		}

		public static EaseRef BounceExact(float amplitude) => new EaseRef(EaseType.BounceExact, amplitude, 0f, null, null);
	}
}
