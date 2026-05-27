using System;
using UnityEngine;
using PATween.Internal;

namespace PATween
{
	public readonly struct EaseRef
	{
		private readonly EaseType type;
		private readonly float paramA;
		private readonly float paramB;
		private readonly AnimationCurve curve;
		private readonly Func<float, float> custom;

		public EaseType Type => type;
		public float ParamA => paramA;
		public float ParamB => paramB;
		public AnimationCurve Curve => curve;
		public Func<float, float> Custom => custom;

		internal EaseRef(EaseType type, float paramA, float paramB, AnimationCurve curve, Func<float, float> custom)
		{
			this.type = type;
			this.paramA = paramA;
			this.paramB = paramB;
			this.curve = curve;
			this.custom = custom;
		}

		public float Evaluate(float t)
		{
			return EaseEval.Evaluate(type, t, paramA, paramB, curve, custom);
		}

		public static EaseRef Linear => new EaseRef(EaseType.Linear, 0f, 0f, null, null);
	}
}
