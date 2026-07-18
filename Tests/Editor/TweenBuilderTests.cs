using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using dyvoid.FeatherTween;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween.Tests
{
	[TestFixture]
	public class TweenBuilderTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
		}

		[Test]
		public void SetDelay_Negative_Throws_BuilderStaysValid()
		{
			var v = 0f;
			var b = FT.To(() => v, x => v = x, 1f, 1f).SetUpdate(UpdatePhase.Manual);
			Assert.Throws<ArgumentOutOfRangeException>(() => b.SetDelay(-0.1f));

			// The throw must not consume the builder.
			var t = b.Start();
			Assert.That(t.IsAlive, Is.True);
		}

		[Test]
		public void Start_ProducesLiveHandle()
		{
			var b = TweenBuilderFactory.Create<float>();
			var t = b.Start();
			Assert.That(t.IsAlive, Is.True);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing));
		}

		[Test]
		public void Start_TwiceOnSameBuilder_Throws()
		{
			var b = TweenBuilderFactory.Create<float>();
			b.Start();
			Assert.Throws<InvalidOperationException>(() => b.Start());
		}

		[Test]
		public void BuilderCopy_SharesBackingBuffer()
		{
			var a = TweenBuilderFactory.Create<float>();
			var alias = a;
			alias = alias.SetAutoKill(false);

			Assert.That(a.Buffer, Is.SameAs(alias.Buffer));
			Assert.That(a.Buffer.AutoKill, Is.False, "Mutating alias must mutate shared buffer.");

			a.Start();
		}

		[Test]
		public void BuilderStatus_AfterConsume_ReadsDisposed()
		{
			var b = TweenBuilderFactory.Create<float>();
			Assert.That(b.Status, Is.EqualTo(TweenStatus.Delayed));
			b.Start();
			Assert.That(b.Status, Is.EqualTo(TweenStatus.Disposed));
		}

		[Test]
		public void Alias_AfterConsume_AlsoInvalidates()
		{
			var a = TweenBuilderFactory.Create<float>();
			var alias = a;
			a.Start();

			Assert.That(alias.Status, Is.EqualTo(TweenStatus.Disposed));
			Assert.Throws<InvalidOperationException>(() => alias.Start());
		}

		[Test]
		public void UnconsumedBuilder_OnGC_LogsWarningOnNextTick()
		{
			DrainLeaks();
			CreateAndDropUnconsumed();
			System.GC.Collect();
			System.GC.WaitForPendingFinalizers();
			System.GC.Collect();
			System.GC.WaitForPendingFinalizers();

			LogAssert.Expect(LogType.Warning, new Regex(@"Unconsumed TweenBuilder leaked"));
			FeatherTweenRunner.ManualTick(0.016);
		}

		[Test]
		public void Pause_TransitionsToPaused()
		{
			var t = TweenBuilderFactory.Create<float>().Start();
			t.Pause();
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Paused));
		}

		[Test]
		public void Resume_FromPaused_TransitionsBackToPlaying()
		{
			var t = TweenBuilderFactory.Create<float>().Start();
			t.Pause();
			t.Resume();
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing));
		}

		[Test]
		public void Kill_False_FiresOnKill_AndDisposes()
		{
			var fired = 0;
			var t = TweenBuilderFactory.Create<float>().OnKill(() => fired++).Start();
			t.Kill();
			Assert.That(fired, Is.EqualTo(1));
			Assert.That(t.IsAlive, Is.False);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Disposed));
		}

		// Firing matrix (Documentation~/api/handles.md): Kill(true) is a completion path — OnKill
		// fires only on Kill(false), auto-kill, or error.
		[Test]
		public void Kill_True_FiresOnCompleteOnly_AndDisposes()
		{
			var completes = 0;
			var kills = 0;
			var t = TweenBuilderFactory.Create<float>()
				.OnComplete(() => completes++)
				.OnKill(() => kills++)
				.Start();

			t.Kill(true);

			Assert.That(completes, Is.EqualTo(1));
			Assert.That(kills, Is.Zero, "Kill(true) completes; OnKill must not fire (Documentation~/api/handles.md)");
			Assert.That(t.IsAlive, Is.False);
		}

		[Test]
		public void Complete_WithAutoKill_FiresOnCompleteOnly_AndDisposes()
		{
			var order = new System.Collections.Generic.List<string>();
			var t = TweenBuilderFactory.Create<float>()
				.OnComplete(() => order.Add("complete"))
				.OnKill(() => order.Add("kill"))
				.Start();

			t.Complete();

			Assert.That(order, Is.EqualTo(new[] { "complete" }),
				"Complete() must not fire OnKill even with autoKill (Documentation~/api/handles.md)");
			Assert.That(t.IsAlive, Is.False);
		}

		[Test]
		public void Complete_WithoutAutoKill_FiresOnCompleteOnly_AndStaysAliveAtCompleted()
		{
			var completes = 0;
			var kills = 0;
			var t = TweenBuilderFactory.Create<float>()
				.SetAutoKill(false)
				.OnComplete(() => completes++)
				.OnKill(() => kills++)
				.Start();

			t.Complete();

			Assert.That(completes, Is.EqualTo(1));
			Assert.That(kills, Is.Zero);
			Assert.That(t.IsAlive, Is.True);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Completed));
		}

		[Test]
		public void IllegalTransition_OnDisposed_NoOp()
		{
			var t = TweenBuilderFactory.Create<float>().Start();
			t.Kill();

			t.Pause();
			t.Resume();
			t.Play();
			t.Complete();
			t.Kill();

			Assert.That(t.Status, Is.EqualTo(TweenStatus.Disposed));
		}

		// Phase 1.15: all late subscriptions on a dead handle are no-ops — the
		// handle cannot know whether its record completed or was killed, so
		// firing either callback would be a guess. This keeps "OnComplete fires
		// only on actual completion" and "OnKill fires only on actual kills"
		// unconditional.
		[Test]
		public void LateSubscription_OnDeadHandle_NoOps()
		{
			var t = TweenBuilderFactory.Create<float>().Start();
			t.Kill();

			var fired = 0;
			t.OnComplete(() => fired++);
			t.OnKill(() => fired++);
			t.OnStepComplete(() => fired++);
			Assert.That(fired, Is.Zero, "dead-handle late subscriptions must not fire");
		}

		[Test]
		public void LateSubscription_OnDeadSequenceHandle_NoOps()
		{
			var sb = FT.Sequence().SetUpdate(UpdatePhase.Manual);
			sb.Append(TweenBuilderFactory.Create<float>());
			var s = sb.Start();
			s.Kill();

			var fired = 0;
			s.OnComplete(() => fired++);
			s.OnKill(() => fired++);
			s.OnStepComplete(() => fired++);
			Assert.That(fired, Is.Zero, "dead-handle late subscriptions must not fire");
		}

		private static void CreateAndDropUnconsumed()
		{
			_ = TweenBuilderFactory.Create<float>();
		}

		private static void DrainLeaks()
		{
			System.GC.Collect();
			System.GC.WaitForPendingFinalizers();
			System.GC.Collect();
			LeakDetector.Drain();
		}
	}
}
