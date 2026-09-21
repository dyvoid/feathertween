using System;
using NUnit.Framework;
using dyvoid.FeatherTween;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween.Tests
{
	// The awaiter is driven by hand rather than through `await`: the contract that
	// matters is IsCompleted / OnCompleted / GetResult, and exercising it directly
	// pins the exact resume count, which an async test method would hide.
	[TestFixture]
	public class AwaiterTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
		}

		private static Tween ManualTween(float duration = 1f, bool autoKill = true)
		{
			var v = 0f;
			return FT.To(() => v, x => v = x, 1f, duration)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(autoKill)
				.Start();
		}

		[Test]
		public void IsCompleted_FalseWhilePlaying_TrueAfterCompletion()
		{
			var t = ManualTween(autoKill: false);
			Assert.That(t.GetAwaiter().IsCompleted, Is.False);

			FeatherTweenRunner.ManualTick(1.1);

			Assert.That(t.Status, Is.EqualTo(TweenStatus.Completed));
			Assert.That(t.GetAwaiter().IsCompleted, Is.True);
		}

		[Test]
		public void IsCompleted_TrueForDeadHandle()
		{
			var t = ManualTween();
			t.Kill();
			Assert.That(t.IsAlive, Is.False);
			Assert.That(t.GetAwaiter().IsCompleted, Is.True, "a dead handle must not park an await forever");
		}

		[Test]
		public void OnCompleted_ResumesOnNaturalCompletion()
		{
			var t = ManualTween(autoKill: false);
			var resumed = 0;
			t.GetAwaiter().OnCompleted(() => resumed++);

			Assert.That(resumed, Is.Zero);
			FeatherTweenRunner.ManualTick(1.1);
			Assert.That(resumed, Is.EqualTo(1));
		}

		[Test]
		public void OnCompleted_ResumesOnKill()
		{
			var t = ManualTween();
			var resumed = 0;
			t.GetAwaiter().OnCompleted(() => resumed++);

			t.Kill();
			Assert.That(resumed, Is.EqualTo(1), "a killed tween resumes the await rather than hanging");
		}

		[Test]
		public void OnCompleted_ResumesOnAutoKillCompletion()
		{
			var t = ManualTween();
			var resumed = 0;
			t.GetAwaiter().OnCompleted(() => resumed++);

			FeatherTweenRunner.ManualTick(1.1);

			Assert.That(t.IsAlive, Is.False);
			Assert.That(resumed, Is.EqualTo(1));
		}

		[Test]
		public void OnCompleted_ResumesExactlyOnce_WhenCompletedThenKilled()
		{
			// The case OneShotSignal exists for: with autoKill off the record
			// survives completion, so a later Kill would fire OnKill on the same
			// continuation and resume an already-resumed state machine.
			var t = ManualTween(autoKill: false);
			var resumed = 0;
			t.GetAwaiter().OnCompleted(() => resumed++);

			FeatherTweenRunner.ManualTick(1.1);
			Assert.That(resumed, Is.EqualTo(1));

			t.Kill();
			Assert.That(resumed, Is.EqualTo(1), "second resume would throw inside a real async method");
		}

		[Test]
		public void OnCompleted_OnDeadHandle_ResumesImmediately()
		{
			var t = ManualTween();
			t.Kill();

			var resumed = 0;
			t.GetAwaiter().OnCompleted(() => resumed++);

			Assert.That(resumed, Is.EqualTo(1));
		}

		[Test]
		public void OnCompleted_NullContinuation_DoesNotThrow()
		{
			var t = ManualTween();
			Assert.That(() => t.GetAwaiter().OnCompleted(null), Throws.Nothing);
		}

		[Test]
		public void GetResult_DoesNotThrow()
		{
			var t = ManualTween();
			Assert.That(() => t.GetAwaiter().GetResult(), Throws.Nothing);
		}

		[Test]
		public void YieldInstruction_KeepsWaitingUntilCompletion()
		{
			var t = ManualTween(autoKill: false);
			var y = t.ToYieldInstruction();

			Assert.That(y.keepWaiting, Is.True);
			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(y.keepWaiting, Is.True, "mid-flight still waits");

			FeatherTweenRunner.ManualTick(0.6);
			Assert.That(y.keepWaiting, Is.False);
		}

		[Test]
		public void YieldInstruction_StopsWaitingOnKill()
		{
			var t = ManualTween();
			var y = t.ToYieldInstruction();

			Assert.That(y.keepWaiting, Is.True);
			t.Kill();
			Assert.That(y.keepWaiting, Is.False);
		}

		[Test]
		public void YieldInstruction_DeadHandle_NeverWaits()
		{
			var t = ManualTween();
			t.Kill();
			Assert.That(t.ToYieldInstruction().keepWaiting, Is.False);
		}

		[Test]
		public void YieldInstruction_PausedTween_KeepsWaiting()
		{
			var t = ManualTween();
			t.Pause();
			Assert.That(t.ToYieldInstruction().keepWaiting, Is.True, "paused is not finished");
		}

		[Test]
		public void WaitForCompletion_ReturnsAnAwaitable_ForLiveAndDeadHandles()
		{
			var live = ManualTween();
			Assert.That(live.WaitForCompletion(), Is.Not.Null);

			var dead = ManualTween();
			dead.Kill();
			Assert.That(dead.WaitForCompletion(), Is.Not.Null, "a dead handle still hands back a completed Awaitable");
		}

		[Test]
		public void Sequence_Awaiter_ResumesOnCompletion()
		{
			var a = 0f;
			var s = FT.Sequence()
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.Append(FT.To(() => a, x => a = x, 1f, 1f))
				.Start();

			var resumed = 0;
			s.GetAwaiter().OnCompleted(() => resumed++);
			Assert.That(s.GetAwaiter().IsCompleted, Is.False);

			FeatherTweenRunner.ManualTick(1.1);

			Assert.That(resumed, Is.EqualTo(1));
			Assert.That(s.GetAwaiter().IsCompleted, Is.True);
		}

		[Test]
		public void Sequence_YieldInstructionAndWaitForCompletion_Work()
		{
			var a = 0f;
			var s = FT.Sequence()
				.SetUpdate(UpdatePhase.Manual)
				.Append(FT.To(() => a, x => a = x, 1f, 1f))
				.Start();

			var y = s.ToYieldInstruction();
			Assert.That(y.keepWaiting, Is.True);
			Assert.That(s.WaitForCompletion(), Is.Not.Null);

			FeatherTweenRunner.ManualTick(1.1);
			Assert.That(y.keepWaiting, Is.False);
		}

		[Test]
		public void BuilderAwaiter_StartsTheTween()
		{
			var v = 0f;
			var builder = FT.To(() => v, x => v = x, 1f, 1f).SetUpdate(UpdatePhase.Manual);

			var awaiter = builder.GetAwaiter();

			Assert.That(awaiter.IsCompleted, Is.False, "awaiting a builder starts it, so it is live and running");
			var resumed = 0;
			awaiter.OnCompleted(() => resumed++);
			FeatherTweenRunner.ManualTick(1.1);
			Assert.That(resumed, Is.EqualTo(1));
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "the tween actually ran");
		}

		[Test]
		public void BuilderAwaiter_ConsumesTheBuilder()
		{
			var v = 0f;
			var builder = FT.To(() => v, x => v = x, 1f, 1f).SetUpdate(UpdatePhase.Manual);
			builder.GetAwaiter();

			Assert.That(() => builder.Start(), Throws.InvalidOperationException,
				"GetAwaiter consumed it, exactly as Start() would");
		}

		[Test]
		public void SequenceBuilderAwaiter_StartsTheSequence()
		{
			var a = 0f;
			var builder = FT.Sequence()
				.SetUpdate(UpdatePhase.Manual)
				.Append(FT.To(() => a, x => a = x, 1f, 1f));

			var awaiter = builder.GetAwaiter();
			var resumed = 0;
			awaiter.OnCompleted(() => resumed++);

			FeatherTweenRunner.ManualTick(1.1);
			Assert.That(resumed, Is.EqualTo(1));
		}

		[Test]
		public void TwoAwaitersOnOneTween_BothResume()
		{
			var t = ManualTween();
			var first = 0;
			var second = 0;
			t.GetAwaiter().OnCompleted(() => first++);
			t.GetAwaiter().OnCompleted(() => second++);

			FeatherTweenRunner.ManualTick(1.1);

			Assert.That(first, Is.EqualTo(1));
			Assert.That(second, Is.EqualTo(1));
		}
	}
}
