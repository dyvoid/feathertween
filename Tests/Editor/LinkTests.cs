using System;
using NUnit.Framework;
using dyvoid.FeatherTween;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween.Tests
{
	[TestFixture]
	public class LinkTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void KillOnDestroy_SurvivesDisable()
		{
			var go = new UnityEngine.GameObject("link");
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go)
				.Start();

			go.SetActive(false);
			FeatherTweenRunner.ManualTick(1d);

			Assert.That(t.IsAlive, Is.True);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing));
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void KillOnDestroy_KillsWhenLinkedObjectDestroyed()
		{
			var go = new UnityEngine.GameObject("link");
			var v = 0f;
			var killed = false;
			var t = FT.To(() => v, x => v = x, 1f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go)
				.OnKill(() => killed = true)
				.Start();

			UnityEngine.Object.DestroyImmediate(go);
			FeatherTweenRunner.ManualTick(1d);

			Assert.That(t.IsAlive, Is.False);
			Assert.That(killed, Is.True, "a link kill is a kill, so OnKill fires");
		}

		[Test]
		public void EveryBehavior_KillsOnDestroy()
		{
			foreach (LinkBehavior behavior in Enum.GetValues(typeof(LinkBehavior)))
			{
				TweenStore.Reset();
				FeatherTweenRunner.Reset();

				var go = new UnityEngine.GameObject("link");
				var v = 0f;
				var t = FT.To(() => v, x => v = x, 1f, 10f)
					.SetUpdate(UpdatePhase.Manual)
					.SetLink(go, behavior)
					.Start();

				UnityEngine.Object.DestroyImmediate(go);
				FeatherTweenRunner.ManualTick(1d);

				Assert.That(t.IsAlive, Is.False, $"{behavior} must still die with its object");
			}
		}

		[Test]
		public void KillOnDisable_KillsOnTheDisableEdge()
		{
			var go = new UnityEngine.GameObject("link");
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go, LinkBehavior.KillOnDisable)
				.Start();

			FeatherTweenRunner.ManualTick(1d);
			Assert.That(t.IsAlive, Is.True);

			go.SetActive(false);
			FeatherTweenRunner.ManualTick(1d);
			Assert.That(t.IsAlive, Is.False);
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void KillOnDisable_KillsATweenStartedOnAnAlreadyInactiveObject()
		{
			var go = new UnityEngine.GameObject("link");
			go.SetActive(false);
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go, LinkBehavior.KillOnDisable)
				.Start();

			FeatherTweenRunner.ManualTick(0.1);

			Assert.That(t.IsAlive, Is.False, "link state is seeded active, so the first tick sees a disable edge");
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void PauseOnDisable_HoldsValueAndDoesNotResume()
		{
			var go = new UnityEngine.GameObject("link");
			var v = 0f;
			var paused = 0;
			var t = FT.To(() => v, x => v = x, 10f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go, LinkBehavior.PauseOnDisable)
				.OnPause(() => paused++)
				.Start();

			FeatherTweenRunner.ManualTick(2d);
			go.SetActive(false);
			FeatherTweenRunner.ManualTick(3d);

			Assert.That(t.Status, Is.EqualTo(TweenStatus.Paused));
			Assert.That(paused, Is.EqualTo(1));
			Assert.That(v, Is.EqualTo(2f).Within(1e-3f), "no time accrues while paused");

			go.SetActive(true);
			FeatherTweenRunner.ManualTick(3d);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Paused), "PauseOnDisable never resumes on its own");
			Assert.That(v, Is.EqualTo(2f).Within(1e-3f));
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void PauseOnDisableResumeOnEnable_ResumesWhereItStopped()
		{
			var go = new UnityEngine.GameObject("link");
			var v = 0f;
			var plays = 0;
			var t = FT.To(() => v, x => v = x, 10f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go, LinkBehavior.PauseOnDisableResumeOnEnable)
				.OnPlay(() => plays++)
				.Start();

			FeatherTweenRunner.ManualTick(2d);
			go.SetActive(false);
			FeatherTweenRunner.ManualTick(5d);
			Assert.That(v, Is.EqualTo(2f).Within(1e-3f));

			go.SetActive(true);
			FeatherTweenRunner.ManualTick(3d);

			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing));
			Assert.That(v, Is.EqualTo(5f).Within(1e-3f), "resumes from 2, not from 0");
			Assert.That(plays, Is.EqualTo(2), "initial activation plus the link resume");
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void PauseOnDisableResumeOnEnable_DoesNotResumeAUserPausedTween()
		{
			var go = new UnityEngine.GameObject("link");
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 10f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go, LinkBehavior.PauseOnDisableResumeOnEnable)
				.Start();

			t.Pause();
			go.SetActive(false);
			FeatherTweenRunner.ManualTick(1d);
			go.SetActive(true);
			FeatherTweenRunner.ManualTick(1d);

			Assert.That(t.Status, Is.EqualTo(TweenStatus.Paused),
				"the link never paused it, so the link does not get to resume it");
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void RestartOnEnable_ReplaysFromZero()
		{
			var go = new UnityEngine.GameObject("link");
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 10f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go, LinkBehavior.RestartOnEnable)
				.Start();

			FeatherTweenRunner.ManualTick(4d);
			Assert.That(v, Is.EqualTo(4f).Within(1e-3f));

			go.SetActive(false);
			FeatherTweenRunner.ManualTick(1d);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Paused));

			go.SetActive(true);
			FeatherTweenRunner.ManualTick(1d);

			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing));
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "restarted, so one second in from zero");
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void RestartOnEnable_ReplaysACompletedTweenThatWasKeptAlive()
		{
			var go = new UnityEngine.GameObject("link");
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.SetLink(go, LinkBehavior.RestartOnEnable)
				.Start();

			FeatherTweenRunner.ManualTick(2d);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Completed));

			go.SetActive(false);
			FeatherTweenRunner.ManualTick(0.1);
			go.SetActive(true);
			FeatherTweenRunner.ManualTick(0.5);

			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing));
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "the pooled-object case: it replays");
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void Link_DrivesAWholeSequence()
		{
			var go = new UnityEngine.GameObject("link");
			var a = 0f;
			var b = 0f;
			var s = FT.Sequence()
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go, LinkBehavior.PauseOnDisableResumeOnEnable)
				.Append(FT.To(() => a, x => a = x, 10f, 10f))
				.Append(FT.To(() => b, x => b = x, 10f, 10f))
				.Start();

			FeatherTweenRunner.ManualTick(2d);
			go.SetActive(false);
			FeatherTweenRunner.ManualTick(5d);

			Assert.That(s.Status, Is.EqualTo(TweenStatus.Paused));
			Assert.That(a, Is.EqualTo(2f).Within(1e-3f));

			go.SetActive(true);
			FeatherTweenRunner.ManualTick(3d);

			Assert.That(s.Status, Is.EqualTo(TweenStatus.Playing));
			Assert.That(a, Is.EqualTo(5f).Within(1e-3f));
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void SetLink_OnASequenceChild_Throws()
		{
			var go = new UnityEngine.GameObject("link");
			var v = 0f;
			var child = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go, LinkBehavior.KillOnDisable);

			var seq = FT.Sequence().SetUpdate(UpdatePhase.Manual);
			Assert.That(() => seq.Append(child), Throws.InvalidOperationException);
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void SetLink_OnANestedSequence_Throws()
		{
			var go = new UnityEngine.GameObject("link");
			var v = 0f;
			var inner = FT.Sequence()
				.SetUpdate(UpdatePhase.Manual)
				.SetLink(go, LinkBehavior.KillOnDisable)
				.Append(FT.To(() => v, x => v = x, 1f, 1f));

			var outer = FT.Sequence().SetUpdate(UpdatePhase.Manual);
			Assert.That(() => outer.Append(inner), Throws.InvalidOperationException);
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void SetLink_NullTarget_Throws()
		{
			var v = 0f;
			Assert.That(
				() => FT.To(() => v, x => v = x, 1f, 1f).SetLink(null),
				Throws.ArgumentNullException);
			Assert.That(
				() => FT.Sequence().SetLink(null),
				Throws.ArgumentNullException);
		}
	}
}
