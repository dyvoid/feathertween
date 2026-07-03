using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PATween;
using PATween.Internal;

namespace PATween.Tests
{
	[TestFixture]
	public class TweenBuilderTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			PATweenRunner.Reset();
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
			PATweenRunner.ManualTick(0.016);
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

		// Firing matrix (api.md §3.14): Kill(true) is a completion path — OnKill
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
			Assert.That(kills, Is.Zero, "Kill(true) completes; OnKill must not fire (§3.14)");
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
				"Complete() must not fire OnKill even with autoKill (§3.14)");
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

		[Test]
		public void LateSubscription_OnDisposed_FiresImmediately()
		{
			var t = TweenBuilderFactory.Create<float>().Start();
			t.Kill();

			var fired = 0;
			t.OnComplete(() => fired++);
			t.OnKill(() => fired++);
			Assert.That(fired, Is.EqualTo(2));
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
