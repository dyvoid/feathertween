using System;
using System.Collections.Generic;
using NUnit.Framework;
using dyvoid.FeatherTween;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween.Tests
{
	[TestFixture]
	public class CallbackTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
		}

		private static TweenBuilder<float> ManualTween(Action<float> setter, float duration = 1f)
		{
			return FT.To(() => 0f, setter, 1f, duration)
				.SetUpdate(UpdatePhase.Manual);
		}

		private static TweenBuilder<float> ManualTween(float duration = 1f)
		{
			return ManualTween(_ => { }, duration);
		}

		// -- firing matrix ------------------------------------------------

		[Test]
		public void NaturalCompletion_FiresStepCompleteAndComplete_NeverKill()
		{
			var log = new List<string>();
			ManualTween()
				.SetLoops(2)
				.OnStepComplete(() => log.Add("step"))
				.OnComplete(() => log.Add("complete"))
				.OnKill(() => log.Add("kill"))
				.Start();

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(log, Is.EqualTo(new[] { "step" }), "first loop boundary");

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(log, Is.EqualTo(new[] { "step", "step", "complete" }),
				"final boundary + complete; no kill on natural completion (Documentation~/api/handles.md)");
		}

		[Test]
		public void KillFalse_FiresOnKillOnly()
		{
			var log = new List<string>();
			var t = ManualTween()
				.OnStepComplete(() => log.Add("step"))
				.OnComplete(() => log.Add("complete"))
				.OnKill(() => log.Add("kill"))
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			t.Kill(false);

			Assert.That(log, Is.EqualTo(new[] { "kill" }));
		}

		[Test]
		public void Complete_FiresRemainingStepCompletes()
		{
			var steps = 0;
			var t = ManualTween()
				.SetLoops(3)
				.OnStepComplete(() => steps++)
				.Start();

			FeatherTweenRunner.ManualTick(1.5); // one boundary crossed
			Assert.That(steps, Is.EqualTo(1));

			t.Complete();
			Assert.That(steps, Is.EqualTo(3), "Complete() fires the remaining loop boundaries");
		}

		[Test]
		public void KillOnAlreadyCompleted_FiresNoCallbacks()
		{
			var log = new List<string>();
			var t = ManualTween()
				.SetAutoKill(false)
				.OnComplete(() => log.Add("complete"))
				.OnKill(() => log.Add("kill"))
				.Start();

			FeatherTweenRunner.ManualTick(1.5);
			Assert.That(log, Is.EqualTo(new[] { "complete" }));

			t.Kill(true);
			Assert.That(log, Is.EqualTo(new[] { "complete" }),
				"Kill on Completed disposes without callbacks (Documentation~/api/handles.md)");
			Assert.That(t.IsAlive, Is.False);
		}

		// -- new callback slots --------------------------------------------

		[Test]
		public void ZeroDuration_FiresStartUpdateCompleteInOrder()
		{
			var log = new List<string>();
			FT.To(() => 0f, _ => { }, 1f, 0f)
				.SetUpdate(UpdatePhase.Manual)
				.OnStart(() => log.Add("start"))
				.OnUpdate(t => log.Add($"update:{t:0.#}"))
				.OnComplete(() => log.Add("complete"))
				.Start();

			FeatherTweenRunner.ManualTick(0.016);
			Assert.That(log, Is.EqualTo(new[] { "start", "update:1", "complete" }));
		}

		[Test]
		public void OnStart_FiresOncePerLifecycle_RearmedByRestart()
		{
			var starts = 0;
			var t = ManualTween()
				.SetAutoKill(false)
				.OnStart(() => starts++)
				.Start();

			FeatherTweenRunner.ManualTick(0.3);
			FeatherTweenRunner.ManualTick(0.3);
			Assert.That(starts, Is.EqualTo(1));

			t.Restart();
			FeatherTweenRunner.ManualTick(0.3);
			Assert.That(starts, Is.EqualTo(2), "Restart re-arms OnStart");
		}

		[Test]
		public void OnStart_DeferredByDelay()
		{
			var started = false;
			ManualTween().SetDelay(0.5f).OnStart(() => started = true).Start();

			FeatherTweenRunner.ManualTick(0.3);
			Assert.That(started, Is.False, "still delayed");

			FeatherTweenRunner.ManualTick(0.4);
			Assert.That(started, Is.True);
		}

		[Test]
		public void OnPlay_FiresOnActivationAndResume_OnPauseOnPause()
		{
			var plays = 0;
			var pauses = 0;
			var t = ManualTween()
				.OnPlay(() => plays++)
				.OnPause(() => pauses++)
				.Start();

			FeatherTweenRunner.ManualTick(0.2);
			Assert.That(plays, Is.EqualTo(1), "initial activation");

			t.Pause();
			Assert.That(pauses, Is.EqualTo(1));

			t.Resume();
			Assert.That(plays, Is.EqualTo(2), "resume fires OnPlay");
		}

		[Test]
		public void OnUpdate_ReceivesEasedProgress_EndsAtOne()
		{
			var values = new List<float>();
			ManualTween().OnUpdate(values.Add).Start();

			FeatherTweenRunner.ManualTick(0.25);
			FeatherTweenRunner.ManualTick(0.25);
			FeatherTweenRunner.ManualTick(0.6);

			Assert.That(values.Count, Is.EqualTo(3));
			Assert.That(values[0], Is.EqualTo(0.25f).Within(1e-3f));
			Assert.That(values[1], Is.EqualTo(0.5f).Within(1e-3f));
			Assert.That(values[2], Is.EqualTo(1f).Within(1e-3f));
		}

		[Test]
		public void OnStepComplete_FiresPerCrossedBoundary_InOneTick()
		{
			var steps = 0;
			ManualTween()
				.SetLoops(4)
				.OnStepComplete(() => steps++)
				.Start();

			FeatherTweenRunner.ManualTick(2.5); // crosses boundaries at 1 and 2
			Assert.That(steps, Is.EqualTo(2));

			FeatherTweenRunner.ManualTick(2.0); // boundary at 3 + completion at 4
			Assert.That(steps, Is.EqualTo(4));
		}

		// -- multicast ------------------------------------------------------

		[Test]
		public void OnComplete_Multicast_FiresInRegistrationOrder()
		{
			var log = new List<string>();
			var t = ManualTween()
				.OnComplete(() => log.Add("builder-1"))
				.OnComplete(() => log.Add("builder-2"))
				.Start();
			t.OnComplete(() => log.Add("handle-3"));

			FeatherTweenRunner.ManualTick(1.5);
			Assert.That(log, Is.EqualTo(new[] { "builder-1", "builder-2", "handle-3" }));
		}

		// -- target-capture overloads ----------------------------------------

		private sealed class CaptureState
		{
			public int Hits;
		}

		[Test]
		public void OnComplete_TargetCapture_ReceivesState()
		{
			var state = new CaptureState();
			ManualTween()
				.OnComplete(state, static s => s.Hits++)
				.Start();

			FeatherTweenRunner.ManualTick(1.5);
			Assert.That(state.Hits, Is.EqualTo(1));
		}

		[Test]
		public void OnKill_TargetCapture_ReceivesState()
		{
			var state = new CaptureState();
			var t = ManualTween()
				.OnKill(state, static s => s.Hits++)
				.Start();

			t.Kill(false);
			Assert.That(state.Hits, Is.EqualTo(1));
		}

		// -- reentrancy / deferred mutation ----------------------------------

		[Test]
		public void KillOtherTween_FromOnComplete_DeferredToEndOfTick()
		{
			var bValue = 0f;
			var b = FT.To(() => bValue, v => bValue = v, 1f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			ManualTween()
				.OnComplete(() => b.Kill())
				.Start();

			// A completes this tick; its OnComplete kills B — deferred, so B
			// still receives this tick's value write, then dies after the tick.
			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(bValue, Is.EqualTo(0.1f).Within(1e-3f), "B stepped this tick before the deferred kill");
			Assert.That(b.IsAlive, Is.False, "deferred kill applied at end of tick");
		}

		[Test]
		public void SelfKill_FromOwnOnComplete_NoDoubleFire()
		{
			var completes = 0;
			var kills = 0;
			Tween t = default;
			t = ManualTween()
				.SetAutoKill(false)
				.OnComplete(() =>
				{
					completes++;
					t.Kill(false);
				})
				.OnKill(() => kills++)
				.Start();

			FeatherTweenRunner.ManualTick(1.5);

			Assert.That(completes, Is.EqualTo(1));
			Assert.That(t.IsAlive, Is.False);
			// The deferred Kill(false) lands on a Completed tween: disposal
			// without callbacks (Documentation~/api/handles.md).
			Assert.That(kills, Is.Zero);
		}

		[Test]
		public void Restart_FromOwnOnComplete_ReplaysNextTick()
		{
			var completes = 0;
			var v = 0f;
			Tween t = default;
			t = FT.To(() => 0f, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.OnComplete(() =>
				{
					completes++;
					if (completes == 1)
					{
						t.Restart();
					}
				})
				.Start();

			FeatherTweenRunner.ManualTick(1.5);
			Assert.That(completes, Is.EqualTo(1));
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing), "deferred Restart applied after tick");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "replaying");
		}

		[Test]
		public void HandleOpOutsideTick_CallbackMutation_DrainsAfterOutermostCallback()
		{
			var b = ManualTween(10f).Start();
			var t = ManualTween()
				.OnComplete(() => b.Kill())
				.Start();

			// Complete() invoked outside any tick: the kill issued inside
			// OnComplete defers, then drains when the outermost callback returns.
			t.Complete();
			Assert.That(b.IsAlive, Is.False);
		}

		[Test]
		public void DeferredCommand_OnStaleHandle_NoOps()
		{
			var other = ManualTween().Start();
			other.Kill(); // now stale

			ManualTween()
				.OnComplete(() => other.Kill(true))
				.Start();

			Assert.DoesNotThrow(() => FeatherTweenRunner.ManualTick(1.5));
		}

		// -- sequence integration ---------------------------------------------

		[Test]
		public void Sequence_NaturalCompletion_NoOnKill_ChildrenNoOnKill()
		{
			var log = new List<string>();
			var v = 0f;
			var sb = FT.Sequence().SetUpdate(UpdatePhase.Manual);
			sb.Append(FT.To(() => 0f, x => v = x, 1f, 1f)
				.OnKill(() => log.Add("child-kill")));
			sb.OnComplete(() => log.Add("complete"));
			sb.OnKill(() => log.Add("kill"));
			sb.Start();

			FeatherTweenRunner.ManualTick(1.5);
			Assert.That(log, Is.EqualTo(new[] { "complete" }),
				"neither the sequence nor its completed children fire OnKill on natural completion");
		}

		[Test]
		public void Sequence_KillFalse_ChildrenGetOnKill()
		{
			var log = new List<string>();
			var sb = FT.Sequence().SetUpdate(UpdatePhase.Manual);
			sb.Append(FT.To(() => 0f, _ => { }, 1f, 1f)
				.OnKill(() => log.Add("child-kill")));
			sb.OnKill(() => log.Add("seq-kill"));
			var seq = sb.Start();

			FeatherTweenRunner.ManualTick(0.5);
			seq.Kill(false);

			Assert.That(log, Is.EqualTo(new[] { "seq-kill", "child-kill" }));
		}

		[Test]
		public void Sequence_KillFromChildOnComplete_DeferredNoCrash()
		{
			Sequence seq = default;
			var secondChildTicked = false;
			var sb = FT.Sequence().SetUpdate(UpdatePhase.Manual);
			sb.Append(FT.To(() => 0f, _ => { }, 1f, 1f)
				.OnComplete(() => seq.Kill()));
			sb.Append(FT.To(() => 0f, _ => secondChildTicked = true, 1f, 1f));
			seq = sb.Start();

			Assert.DoesNotThrow(() => FeatherTweenRunner.ManualTick(1.5));
			Assert.That(seq.IsAlive, Is.False, "kill applied after the tick");
			Assert.That(secondChildTicked, Is.True,
				"second child still stepped this tick before the deferred kill");
		}

		[Test]
		public void Sequence_StartAndUpdateCallbacks_Fire()
		{
			var started = false;
			var lastT = -1f;
			var sb = FT.Sequence().SetUpdate(UpdatePhase.Manual);
			sb.Append(FT.To(() => 0f, _ => { }, 1f, 2f));
			sb.OnStart(() => started = true);
			sb.OnUpdate(t => lastT = t);
			sb.Start();

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(started, Is.True);
			Assert.That(lastT, Is.EqualTo(0.5f).Within(1e-3f));
		}

		[Test]
		public void Sequence_PauseEntry_FiresOnPause()
		{
			var pauses = 0;
			var sb = FT.Sequence().SetUpdate(UpdatePhase.Manual);
			sb.Append(FT.To(() => 0f, _ => { }, 1f, 1f));
			sb.AddPause(0.5f);
			sb.OnPause(() => pauses++);
			sb.Start();

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(pauses, Is.EqualTo(1), "AddPause halting the sequence fires OnPause");
		}
	}
}
