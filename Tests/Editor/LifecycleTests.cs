using NUnit.Framework;
using dyvoid.FeatherTween;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween.Tests
{
	[TestFixture]
	public class LifecycleTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void Restart_AfterCompletion_ReplaysFromZero()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.Start();

			FeatherTweenRunner.ManualTick(1.1);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Completed));

			t.Restart();
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing));

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "replays from zero, not stuck at end");
		}

		[Test]
		public void Play_AfterCompletion_ReplaysFromZero()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.Start();

			FeatherTweenRunner.ManualTick(1.1);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Completed));

			t.Play();
			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f));
		}

		[Test]
		public void Restart_DuringDelay_ReappliesDelay()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.SetDelay(0.5f, DelayType.FirstLoop)
				.Start();

			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "0.25 into animation after delay");

			t.Restart();
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Delayed), "delay reapplied");

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Delayed), "still inside reapplied delay");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "interpolating again after delay");
		}

		[Test]
		public void Complete_SnapsPropertyToEndValue()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 10f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.Start();

			FeatherTweenRunner.ManualTick(0.3);
			Assert.That(v, Is.EqualTo(3f).Within(1e-3f));

			t.Complete();
			Assert.That(v, Is.EqualTo(10f).Within(1e-6f), "snaps to end on Complete");
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Completed));
		}

		[Test]
		public void Complete_Yoyo_SnapsToFinalCycleValue()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 10f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.SetLoops(2, LoopType.Yoyo)
				.Start();

			FeatherTweenRunner.ManualTick(0.3);
			t.Complete();
			Assert.That(v, Is.EqualTo(0f).Within(1e-6f), "2-cycle yoyo ends back at start value");
		}

		[Test]
		public void Kill_Complete_SnapsToEndValue()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 10f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.3);
			Assert.That(v, Is.EqualTo(3f).Within(1e-3f));

			t.Kill(complete: true);
			Assert.That(v, Is.EqualTo(10f).Within(1e-6f));
			Assert.That(t.IsAlive, Is.False);
		}

		[Test]
		public void Callback_KillsAnotherTween_DoesNotSkipSibling()
		{
			var a = 0f;
			var b = 0f;
			var c = 0f;

			var killTarget = default(Tween);

			FT.To(() => a, x => a = x, 1f, 0.5f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.OnComplete(() => killTarget.Kill())
				.Start();

			killTarget = FT.To(() => b, x => b = x, 1f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.Start();

			FT.To(() => c, x => c = x, 1f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.Start();

			FeatherTweenRunner.ManualTick(0.6);

			Assert.That(killTarget.IsAlive, Is.False, "B killed from A's completion callback");
			Assert.That(c, Is.GreaterThan(0f), "C (after B in the active list) must still tick this frame");
		}
	}
}
