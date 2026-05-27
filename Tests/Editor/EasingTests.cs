using NUnit.Framework;
using UnityEngine;
using PATween;
using PATween.Internal;

namespace PATween.Tests
{
	[TestFixture]
	public class EasingTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			PATweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void AllEases_AtZero_ReturnZero()
		{
			foreach (var type in System.Enum.GetValues(typeof(EaseType)))
			{
				var ease = MakeEase((EaseType)type);
				Assert.That(ease.Evaluate(0f), Is.EqualTo(0f).Within(1e-4f), $"{type} at t=0");
			}
		}

		[Test]
		public void AllEases_AtOne_ReturnOne()
		{
			foreach (var type in System.Enum.GetValues(typeof(EaseType)))
			{
				var ease = MakeEase((EaseType)type);
				Assert.That(ease.Evaluate(1f), Is.EqualTo(1f).Within(1e-4f), $"{type} at t=1");
			}
		}

		[Test]
		public void DocumentedMidpoints()
		{
			AssertMid(Easing.Linear(), 0.5f);
			AssertMid(Easing.InQuad(), 0.25f);
			AssertMid(Easing.OutQuad(), 0.75f);
			AssertMid(Easing.InOutQuad(), 0.5f);
			AssertMid(Easing.InCubic(), 0.125f);
			AssertMid(Easing.OutCubic(), 0.875f);
			AssertMid(Easing.InOutCubic(), 0.5f);
			AssertMid(Easing.InQuart(), 0.0625f);
			AssertMid(Easing.OutQuart(), 0.9375f);
			AssertMid(Easing.InQuint(), 0.03125f);
			AssertMid(Easing.OutQuint(), 0.96875f);
			AssertMid(Easing.InSine(), 1f - Mathf.Cos(Mathf.PI / 4f));
			AssertMid(Easing.OutSine(), Mathf.Sin(Mathf.PI / 4f));
			AssertMid(Easing.InOutSine(), 0.5f);
		}

		[Test]
		public void Curve_RoundTrips()
		{
			var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
			var ease = Easing.Curve(curve);
			Assert.That(ease.Curve, Is.SameAs(curve));
			Assert.That(ease.Evaluate(0.5f), Is.EqualTo(curve.Evaluate(0.5f)).Within(1e-4f));
		}

		[Test]
		public void Custom_InvokesUserDelegate()
		{
			var calls = 0;
			var ease = Easing.Custom(t =>
			{
				calls++;
				return t * t;
			});
			var v = ease.Evaluate(0.5f);
			Assert.That(calls, Is.EqualTo(1));
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-4f));
		}

		[Test]
		public void OutBack_OvershootParameter_AffectsAmplitude()
		{
			var lo = Easing.OutBack(1f);
			var hi = Easing.OutBack(5f);
			var loPeak = MaxOver(lo, 100);
			var hiPeak = MaxOver(hi, 100);
			Assert.That(hiPeak, Is.GreaterThan(loPeak));
		}

		[Test]
		public void OutBack_Default_Overshoots()
		{
			var ease = Easing.OutBack();
			var peak = MaxOver(ease, 200);
			Assert.That(peak, Is.GreaterThan(1f));
		}

		[Test]
		public void SetEase_AppliedDuringStep()
		{
			var v = 0f;
			global::PATween.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetEase(Easing.InQuad())
				.Start();

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f));
		}

		[Test]
		public void SetEase_AnimationCurve_Applied()
		{
			var curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
			var v = 0f;
			global::PATween.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetEase(curve)
				.Start();

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(curve.Evaluate(0.5f)).Within(1e-3f));
		}

		private static EaseRef MakeEase(EaseType type)
		{
			switch (type)
			{
				case EaseType.Linear: return Easing.Linear();
				case EaseType.InSine: return Easing.InSine();
				case EaseType.OutSine: return Easing.OutSine();
				case EaseType.InOutSine: return Easing.InOutSine();
				case EaseType.InQuad: return Easing.InQuad();
				case EaseType.OutQuad: return Easing.OutQuad();
				case EaseType.InOutQuad: return Easing.InOutQuad();
				case EaseType.InCubic: return Easing.InCubic();
				case EaseType.OutCubic: return Easing.OutCubic();
				case EaseType.InOutCubic: return Easing.InOutCubic();
				case EaseType.InQuart: return Easing.InQuart();
				case EaseType.OutQuart: return Easing.OutQuart();
				case EaseType.InOutQuart: return Easing.InOutQuart();
				case EaseType.InQuint: return Easing.InQuint();
				case EaseType.OutQuint: return Easing.OutQuint();
				case EaseType.InOutQuint: return Easing.InOutQuint();
				case EaseType.InExpo: return Easing.InExpo();
				case EaseType.OutExpo: return Easing.OutExpo();
				case EaseType.InOutExpo: return Easing.InOutExpo();
				case EaseType.InCirc: return Easing.InCirc();
				case EaseType.OutCirc: return Easing.OutCirc();
				case EaseType.InOutCirc: return Easing.InOutCirc();
				case EaseType.InBack: return Easing.InBack();
				case EaseType.OutBack: return Easing.OutBack();
				case EaseType.InOutBack: return Easing.InOutBack();
				case EaseType.InElastic: return Easing.InElastic();
				case EaseType.OutElastic: return Easing.OutElastic();
				case EaseType.InOutElastic: return Easing.InOutElastic();
				case EaseType.InBounce: return Easing.InBounce();
				case EaseType.OutBounce: return Easing.OutBounce();
				case EaseType.InOutBounce: return Easing.InOutBounce();
				case EaseType.Curve: return Easing.Curve(AnimationCurve.Linear(0f, 0f, 1f, 1f));
				case EaseType.Custom: return Easing.Custom(t => t);
				case EaseType.BounceExact: return Easing.BounceExact(0.1f);
				default: return Easing.Linear();
			}
		}

		private static void AssertMid(EaseRef ease, float expected)
		{
			Assert.That(ease.Evaluate(0.5f), Is.EqualTo(expected).Within(1e-3f), ease.Type.ToString());
		}

		private static float MaxOver(EaseRef ease, int samples)
		{
			var max = float.NegativeInfinity;
			for (var i = 0; i <= samples; i++)
			{
				var t = (float)i / samples;
				var v = ease.Evaluate(t);
				if (v > max)
				{
					max = v;
				}
			}
			return max;
		}
	}
}
