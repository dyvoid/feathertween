using System;
using UnityEngine;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween
{
	/// <summary>
	/// A value-type reference to an ease shape plus its parameters. Produced by
	/// the <see cref="Easing"/> factories; stored per tween, no shared state and
	/// no heap allocation for the built-in shapes.
	/// </summary>
	public readonly struct EaseRef
	{
		private readonly EaseType type;
		private readonly float paramA;
		private readonly float paramB;
		private readonly AnimationCurve curve;
		private readonly Func<float, float> custom;

		/// <summary>The ease shape this reference evaluates.</summary>
		public EaseType Type => type;
		/// <summary>First shape parameter (overshoot for Back, amplitude for Elastic/BounceExact; 0 otherwise).</summary>
		public float ParamA => paramA;
		/// <summary>Second shape parameter (period for Elastic; 0 otherwise).</summary>
		public float ParamB => paramB;
		/// <summary>The user curve when <see cref="Type"/> is <see cref="EaseType.Curve"/>, else null.</summary>
		public AnimationCurve Curve => curve;
		/// <summary>The user delegate when <see cref="Type"/> is <see cref="EaseType.Custom"/>, else null.</summary>
		public Func<float, float> Custom => custom;

		internal EaseRef(EaseType type, float paramA, float paramB, AnimationCurve curve, Func<float, float> custom)
		{
			this.type = type;
			this.paramA = paramA;
			this.paramB = paramB;
			this.curve = curve;
			this.custom = custom;
		}

		/// <summary>Maps linear progress <paramref name="t"/> (0..1) to eased progress (may over/undershoot for Back/Elastic shapes).</summary>
		public float Evaluate(float t)
		{
			return EaseEval.Evaluate(type, t, paramA, paramB, curve, custom);
		}

		/// <summary>A linear ease; the default when no ease is set.</summary>
		public static EaseRef Linear => new EaseRef(EaseType.Linear, 0f, 0f, null, null);
	}
}
