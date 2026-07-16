using System;
using NUnit.Framework;
using UnityEngine;
using Dyvoid.FeatherTween;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween.Tests
{
	[TestFixture]
	public class TweenCoreTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void Tween_LinearFloat_SamplesMidwayAtHalfDuration()
		{
			var v = 0f;
			FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-4f));
		}

		[Test]
		public void Tween_LinearFloat_ReachesEndAtDuration()
		{
			var v = 0f;
			FT.To(() => v, x => v = x, 10f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(10f).Within(1e-4f));
		}

		// Phase 1.15: int interpolation rounds to nearest. Truncation stepped
		// asymmetrically across 0 (2.6 -> 2 but -2.6 -> -2).
		[Test]
		public void IntInterpolator_RoundsToNearest_SymmetricAcrossZero()
		{
			var v = 0;
			FT.To(() => v, x => v = x, 10, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.26);
			Assert.That(v, Is.EqualTo(3), "2.6 rounds to 3 (truncation gave 2)");

			TweenStore.Reset();
			var w = 0;
			FT.To(() => w, x => w = x, -10, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.26);
			Assert.That(w, Is.EqualTo(-3), "-2.6 rounds to -3, symmetric with the positive case");
		}

		// Phase 1.15: zero duration with infinite loops has no meaningful
		// playhead; throw at Start() instead of clamping.
		[Test]
		public void ZeroDuration_WithInfiniteLoops_ThrowsAtStart()
		{
			var v = 0f;
			var b = FT.To(() => v, x => v = x, 1f, 0f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1, LoopType.Restart);
			Assert.Throws<InvalidOperationException>(() => b.Start());
		}

		[Test]
		public void ZeroDuration_WithFiniteLoops_StillCompletes()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 0f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(2, LoopType.Restart)
				.Start();

			FeatherTweenRunner.ManualTick(0.1);
			Assert.That(v, Is.EqualTo(1f).Within(1e-6f));
			Assert.That(t.IsAlive, Is.False, "zero-duration finite tween completes and auto-kills");
		}

		[Test]
		public void Setter_InvokedExactlyOncePerTick()
		{
			var calls = 0;
			FT.To(() => 0f, _ => calls++, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.1);
			Assert.That(calls, Is.EqualTo(1));

			FeatherTweenRunner.ManualTick(0.1);
			Assert.That(calls, Is.EqualTo(2));

			FeatherTweenRunner.ManualTick(0.1);
			Assert.That(calls, Is.EqualTo(3));
		}

		[Test]
		public void Tween_AutoKillsAfterCompletion()
		{
			var t = FT.To(() => 0f, _ => { }, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(1.1);
			Assert.That(t.IsAlive, Is.False);
		}

		[Test]
		public void Tween_AutoKillOff_StaysAliveAtCompleted()
		{
			var t = FT.To(() => 0f, _ => { }, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.Start();

			FeatherTweenRunner.ManualTick(1.1);
			Assert.That(t.IsAlive, Is.True);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Completed));
		}

		[Test]
		public void DestroyedUnityObjectTarget_TriggersAutoKill()
		{
			var go = new GameObject("__feathertween_test__");
			var t = FT.To(() => 0f, _ => { }, 1f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetTarget(go)
				.Start();

			Assert.That(t.IsAlive, Is.True);

			UnityEngine.Object.DestroyImmediate(go);
			FeatherTweenRunner.ManualTick(0.016);

			Assert.That(t.IsAlive, Is.False, "Auto-kill should fire when target was destroyed.");
		}

		[Test]
		public void Interpolators_Reregister_WithLiveTweenOfT_Throws()
		{
			var t = FT.To(() => 0f, _ => { }, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.Throws<InvalidOperationException>(() => Interpolators.Register<float>(new FloatInterpolator()));

			t.Kill();
			Assert.DoesNotThrow(() => Interpolators.Register<float>(new FloatInterpolator()));
		}

		[Test]
		public void Relative_AddsEndValueToCapturedStart()
		{
			var v = 5f;
			FT.To(() => v, x => v = x, 3f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetRelative(true)
				.Start();

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(8f).Within(1e-4f));
		}

		[Test]
		public void IgnoreTimeScale_UsesUnscaledDelta()
		{
			var v = 0f;
			FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual, ignoreTimeScale: true)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-4f));
		}

		[Test]
		public void OnComplete_FiresAtCompletion()
		{
			var fired = 0;
			FT.To(() => 0f, _ => { }, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.OnComplete(() => fired++)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(fired, Is.Zero);

			FeatherTweenRunner.ManualTick(0.6);
			Assert.That(fired, Is.EqualTo(1));
		}

		[Test]
		public void ManyTweens_TickedTogether_AllAdvance()
		{
			var values = new float[100];
			for (var i = 0; i < 100; i++)
			{
				var idx = i;
				FT.To(() => 0f, x => values[idx] = x, 1f, 1f)
					.SetUpdate(UpdatePhase.Manual)
					.Start();
			}

			FeatherTweenRunner.ManualTick(0.5);
			for (var i = 0; i < 100; i++)
			{
				Assert.That(values[i], Is.EqualTo(0.5f).Within(1e-4f), $"index {i}");
			}
		}

		[Test]
		public void Alloc_SteadyState1kTweens_BoundedDelta()
		{
			FT.SetCapacity(2048);

			for (var i = 0; i < 1000; i++)
			{
				FT.To(() => 0f, _ => { }, 1f, 600f)
					.SetUpdate(UpdatePhase.Manual)
					.Start();
			}

			for (var i = 0; i < 60; i++)
			{
				FeatherTweenRunner.ManualTick(0.016);
			}

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			var before = GC.GetTotalMemory(false);
			for (var i = 0; i < 600; i++)
			{
				FeatherTweenRunner.ManualTick(0.016);
			}
			var after = GC.GetTotalMemory(false);

			Assert.That(after - before, Is.LessThan(1024L * 1024L),
				$"Steady-state alloc delta over 600 ticks: {after - before} bytes.");
		}
	}
}
