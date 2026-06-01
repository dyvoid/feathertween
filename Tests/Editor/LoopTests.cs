using System.Collections.Generic;
using NUnit.Framework;
using PATween;
using PATween.Internal;

namespace PATween.Tests
{
	[TestFixture]
	public class LoopTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			PATweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void Yoyo_PingPongs_AcrossTwoCycles()
		{
			var v = 0f;
			global::PATween.PATween.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(2, LoopType.Yoyo)
				.Start();

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "cycle 0, mid");

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "boundary at end of cycle 0 / start of cycle 1");

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "cycle 1 (yoyo) mid");

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0f).Within(1e-3f), "yoyo finishes back at start");
		}

		[Test]
		public void Incremental_AddsDeltaEachCycle()
		{
			var v = 0f;
			global::PATween.PATween.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(3, LoopType.Incremental)
				.Start();

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f));

			PATweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(1.5f).Within(1e-3f), "into cycle 1, mid: 1 + 0.5");

			PATweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(2.5f).Within(1e-3f), "into cycle 2, mid: 2 + 0.5");

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(3f).Within(1e-3f), "completes at 3");
		}

		[Test]
		public void Restart_RepeatsSamePattern()
		{
			var v = 0f;
			global::PATween.PATween.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(2, LoopType.Restart)
				.Start();

			PATweenRunner.ManualTick(1.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "into cycle 1 mid: pattern restarts at 0");
		}

		[Test]
		public void Reverse_RewindsPlayhead_TowardZero()
		{
			var v = 0f;
			var t = global::PATween.PATween.To(() => v, x => v = x, 10f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			PATweenRunner.ManualTick(0.7);
			Assert.That(v, Is.EqualTo(7f).Within(1e-3f));

			t.Reverse();
			PATweenRunner.ManualTick(0.4);
			Assert.That(v, Is.EqualTo(3f).Within(1e-3f));

			PATweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(0f).Within(1e-3f));
		}

		[Test]
		public void Reverse_FiresOnRewind_AtCycleBoundary()
		{
			var v = 0f;
			var rewinds = 0;
			var t = global::PATween.PATween.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(3, LoopType.Restart)
				.OnRewind(() => rewinds++)
				.Start();

			PATweenRunner.ManualTick(2.5);
			Assert.That(rewinds, Is.Zero, "no OnRewind while moving forward");

			t.Reverse();
			PATweenRunner.ManualTick(1.0);
			Assert.That(rewinds, Is.EqualTo(1), "crossed cycle 2 -> cycle 1 boundary backward");

			PATweenRunner.ManualTick(1.0);
			Assert.That(rewinds, Is.EqualTo(2), "crossed cycle 1 -> cycle 0 boundary backward");
		}

		[Test]
		public void EveryLoopDelay_AppliedPerCycle()
		{
			var v = 0f;
			global::PATween.PATween.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(2, LoopType.Restart)
				.SetDelay(0.5f, DelayType.EveryLoop)
				.Start();

			PATweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(0f), "in cycle 0 delay");

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "cycle 0 interpolating");

			PATweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "cycle 0 reached end");

			PATweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "in cycle 1 delay, holds previous value");

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "cycle 1 interpolating");
		}

		[Test]
		public void FirstLoopDelay_OnlyOnce()
		{
			var v = 0f;
			global::PATween.PATween.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(2, LoopType.Restart)
				.SetDelay(0.5f, DelayType.FirstLoop)
				.Start();

			PATweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(0f), "in initial delay");

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "cycle 0 interpolating");

			PATweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "cycle 1 interpolating, no second delay");
		}

		[Test]
		public void SetRemainingCycles_StopAtNextEnd_Completes()
		{
			var v = 0f;
			var completed = false;
			var t = global::PATween.PATween.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1, LoopType.Restart)
				.OnComplete(() => completed = true)
				.Start();

			PATweenRunner.ManualTick(0.5);
			t.SetRemainingCycles(true);

			PATweenRunner.ManualTick(0.6);
			Assert.That(completed, Is.True);
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f));
		}

		[Test]
		public void From_WithDelay_SnapsAtStart_InterpolationStartsAfterDelay()
		{
			var v = 5f;
			global::PATween.PATween.From(() => v, x => v = x, 0f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetDelay(0.5f, DelayType.FirstLoop)
				.Start();

			Assert.That(v, Is.EqualTo(0f).Within(1e-6f), "From snaps at Start regardless of delay");

			PATweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(0f).Within(1e-6f), "still in delay, value unchanged");

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(1.25f).Within(1e-3f), "interpolating: 0 + (5-0)*0.25 = 1.25");

			PATweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(5f).Within(1e-3f), "completed");
		}
	}
}
