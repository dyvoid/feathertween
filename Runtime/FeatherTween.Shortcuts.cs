using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dyvoid.FeatherTween
{
	// Typed shortcuts (phase 1.11). Each builds a lambda pair on the generic
	// core, auto-sets the target so Kill(target)/IsTweening(target) reach the
	// tween, and returns the ordinary builder for chaining (From, SetEase, ...).
	// The per-creation delegate-pair allocation is the accepted lambda baseline;
	// hand-written zero-alloc fast paths are an M2 deliverable.
	public static partial class FT
	{
		public static TweenBuilder<Vector3> Move(Transform target, Vector3 to, float duration)
		{
			RequireTarget(target);
			return To(() => target.position, v => target.position = v, to, duration)
				.SetTarget(target);
		}

		public static TweenBuilder<Vector3> LocalMove(Transform target, Vector3 to, float duration)
		{
			RequireTarget(target);
			return To(() => target.localPosition, v => target.localPosition = v, to, duration)
				.SetTarget(target);
		}

		public static TweenBuilder<Vector3> Scale(Transform target, Vector3 to, float duration)
		{
			RequireTarget(target);
			return To(() => target.localScale, v => target.localScale = v, to, duration)
				.SetTarget(target);
		}

		public static TweenBuilder<Vector3> Scale(Transform target, float uniformTo, float duration)
			=> Scale(target, new Vector3(uniformTo, uniformTo, uniformTo), duration);

		public static TweenBuilder<Quaternion> Rotate(Transform target, Quaternion to, float duration)
		{
			RequireTarget(target);
			return To(() => target.rotation, v => target.rotation = v, to, duration)
				.SetTarget(target);
		}

		public static TweenBuilder<Quaternion> Rotate(Transform target, Vector3 eulerAngles, float duration)
			=> Rotate(target, Quaternion.Euler(eulerAngles.x, eulerAngles.y, eulerAngles.z), duration);

		public static TweenBuilder<Quaternion> LocalRotate(Transform target, Quaternion to, float duration)
		{
			RequireTarget(target);
			return To(() => target.localRotation, v => target.localRotation = v, to, duration)
				.SetTarget(target);
		}

		public static TweenBuilder<Quaternion> LocalRotate(Transform target, Vector3 eulerAngles, float duration)
			=> LocalRotate(target, Quaternion.Euler(eulerAngles.x, eulerAngles.y, eulerAngles.z), duration);

		public static TweenBuilder<float> Fade(CanvasGroup target, float toAlpha, float duration)
		{
			RequireTarget(target);
			return To(() => target.alpha, v => target.alpha = v, toAlpha, duration)
				.SetTarget(target);
		}

		public static TweenBuilder<UnityEngine.Color> Color(Image target, UnityEngine.Color to, float duration)
		{
			RequireTarget(target);
			return To(() => target.color, v => target.color = v, to, duration)
				.SetTarget(target);
		}

		public static TweenBuilder<float> Fade(Image target, float toAlpha, float duration)
		{
			RequireTarget(target);
			return To(
					() => target.color.a,
					v =>
					{
						var c = target.color;
						c.a = v;
						target.color = c;
					},
					toAlpha, duration)
				.SetTarget(target);
		}

		public static TweenBuilder<float> FillAmount(Image target, float to, float duration)
		{
			RequireTarget(target);
			return To(() => target.fillAmount, v => target.fillAmount = v, to, duration)
				.SetTarget(target);
		}

		private static void RequireTarget(UnityEngine.Object target)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target), "[FeatherTween] Shortcut target is null or destroyed.");
			}
		}
	}
}
