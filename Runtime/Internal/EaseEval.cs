using System;
using UnityEngine;

namespace PATween.Internal
{
	internal static class EaseEval
	{
		private const float DefaultBackOvershoot = 1.70158f;

		public static float Evaluate(EaseType type, float t, float a, float b, AnimationCurve curve, Func<float, float> custom)
		{
			switch (type)
			{
				case EaseType.Linear: return t;
				case EaseType.InSine: return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
				case EaseType.OutSine: return Mathf.Sin(t * Mathf.PI * 0.5f);
				case EaseType.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

				case EaseType.InQuad: return t * t;
				case EaseType.OutQuad: { var u = 1f - t; return 1f - u * u; }
				case EaseType.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;

				case EaseType.InCubic: return t * t * t;
				case EaseType.OutCubic: { var u = 1f - t; return 1f - u * u * u; }
				case EaseType.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

				case EaseType.InQuart: return t * t * t * t;
				case EaseType.OutQuart: { var u = 1f - t; return 1f - u * u * u * u; }
				case EaseType.InOutQuart: return t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) * 0.5f;

				case EaseType.InQuint: return t * t * t * t * t;
				case EaseType.OutQuint: { var u = 1f - t; return 1f - u * u * u * u * u; }
				case EaseType.InOutQuint: return t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) * 0.5f;

				case EaseType.InExpo: return t == 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
				case EaseType.OutExpo: return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
				case EaseType.InOutExpo:
					if (t == 0f) return 0f;
					if (t >= 1f) return 1f;
					return t < 0.5f
						? Mathf.Pow(2f, 20f * t - 10f) * 0.5f
						: (2f - Mathf.Pow(2f, -20f * t + 10f)) * 0.5f;

				case EaseType.InCirc: return 1f - Mathf.Sqrt(1f - t * t);
				case EaseType.OutCirc: { var u = t - 1f; return Mathf.Sqrt(1f - u * u); }
				case EaseType.InOutCirc:
					return t < 0.5f
						? (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) * 0.5f
						: (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) * 0.5f;

				case EaseType.InBack:
				{
					var c1 = a == 0f ? DefaultBackOvershoot : a;
					var c3 = c1 + 1f;
					return c3 * t * t * t - c1 * t * t;
				}
				case EaseType.OutBack:
				{
					var c1 = a == 0f ? DefaultBackOvershoot : a;
					var c3 = c1 + 1f;
					var u = t - 1f;
					return 1f + c3 * u * u * u + c1 * u * u;
				}
				case EaseType.InOutBack:
				{
					var c1 = a == 0f ? DefaultBackOvershoot : a;
					var c2 = c1 * 1.525f;
					return t < 0.5f
						? Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2) * 0.5f
						: (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) * 0.5f;
				}

				case EaseType.InElastic:
				{
					if (t == 0f) return 0f;
					if (t >= 1f) return 1f;
					var c4 = (2f * Mathf.PI) / 3f;
					return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * c4);
				}
				case EaseType.OutElastic:
				{
					if (t == 0f) return 0f;
					if (t >= 1f) return 1f;
					var c4 = (2f * Mathf.PI) / 3f;
					return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
				}
				case EaseType.InOutElastic:
				{
					if (t == 0f) return 0f;
					if (t >= 1f) return 1f;
					var c5 = (2f * Mathf.PI) / 4.5f;
					return t < 0.5f
						? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * c5)) * 0.5f
						: (Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * c5)) * 0.5f + 1f;
				}

				case EaseType.OutBounce: return OutBounce(t);
				case EaseType.InBounce: return 1f - OutBounce(1f - t);
				case EaseType.InOutBounce:
					return t < 0.5f
						? (1f - OutBounce(1f - 2f * t)) * 0.5f
						: (1f + OutBounce(2f * t - 1f)) * 0.5f;

				case EaseType.Curve:
					return curve != null ? curve.Evaluate(t) : t;
				case EaseType.Custom:
					return custom != null ? custom(t) : t;
				case EaseType.BounceExact:
					return OutBounce(t);

				default: return t;
			}
		}

		private static float OutBounce(float t)
		{
			const float n1 = 7.5625f;
			const float d1 = 2.75f;

			if (t < 1f / d1)
			{
				return n1 * t * t;
			}
			if (t < 2f / d1)
			{
				t -= 1.5f / d1;
				return n1 * t * t + 0.75f;
			}
			if (t < 2.5f / d1)
			{
				t -= 2.25f / d1;
				return n1 * t * t + 0.9375f;
			}
			t -= 2.625f / d1;
			return n1 * t * t + 0.984375f;
		}
	}
}
