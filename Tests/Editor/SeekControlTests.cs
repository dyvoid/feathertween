using System;
using System.Collections.Generic;
using NUnit.Framework;
using Dyvoid.FeatherTween;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween.Tests
{
	// Phase 1.10: Seek traversal (Documentation~/api/handles.md), sequence SetLoops/Reverse, mid-play
	// Insert, and global / per-phase / per-tween time scale.
	[TestFixture]
	public class SeekControlTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
		}

		private static TweenBuilder<float> FloatTween(Func<float> getter, Action<float> setter, float end, float duration)
		{
			return FT.To(getter, setter, end, duration);
		}

		private static SequenceBuilder ManualSequence()
		{
			return FT.Sequence().SetUpdate(UpdatePhase.Manual);
		}

		// --- Tween Seek ---

		[Test]
		public void TweenSeek_Silent_RendersSingleSample()
		{
			var v = 0f;
			var steps = 0;
			var t = FloatTween(() => v, x => v = x, 10f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(3, LoopType.Restart)
				.OnStepComplete(() => steps++)
				.Start();

			FeatherTweenRunner.ManualTick(0.1);
			t.Seek(2.5f);
			Assert.That(v, Is.EqualTo(5f).Within(1e-3f), "sample at cycle 2 mid");
			Assert.That(steps, Is.Zero, "silent seek fires no boundaries");
		}

		[Test]
		public void TweenSeek_WithCallbacks_WalksBoundaries()
		{
			var v = 0f;
			var steps = 0;
			var rewinds = 0;
			var t = FloatTween(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(4, LoopType.Restart)
				.OnStepComplete(() => steps++)
				.OnRewind(() => rewinds++)
				.Start();

			FeatherTweenRunner.ManualTick(0.1);
			t.Seek(2.5f, fireCallbacks: true);
			Assert.That(steps, Is.EqualTo(2), "crossed 2 boundaries forward");

			t.Seek(0.5f, fireCallbacks: true);
			Assert.That(rewinds, Is.EqualTo(2), "crossed 2 boundaries backward");
		}

		[Test]
		public void TweenSeek_PreservesPauseState()
		{
			var v = 0f;
			var t = FloatTween(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.2);
			t.Pause();
			t.Seek(0.75f);
			Assert.That(v, Is.EqualTo(0.75f).Within(1e-3f));
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Paused), "seek preserves pause");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.75f).Within(1e-3f), "still paused, no advance");
		}

		[Test]
		public void TweenSeek_ClampsToTimeline()
		{
			var v = 0f;
			var t = FloatTween(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.Start();

			FeatherTweenRunner.ManualTick(0.01);
			t.Seek(99f);
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "clamped to end");
			t.Seek(-5f);
			Assert.That(v, Is.EqualTo(0f).Within(1e-3f), "clamped to start");
		}

		// --- Sequence Seek ---

		[Test]
		public void SequenceSeek_Silent_SkipsCallbacksAndPauses()
		{
			var v = 0f;
			var log = new List<string>();
			var sb = ManualSequence();
			sb.Append(FloatTween(() => v, x => v = x, 1f, 1f));
			sb.AppendCallback(() => log.Add("cb"));
			sb.AddPause(1f);
			sb.Append(FloatTween(() => v, x => v = x, 2f, 1f));
			var seq = sb.Start();

			FeatherTweenRunner.ManualTick(0.1);
			seq.Seek(1.5f);
			Assert.That(log, Is.Empty, "silent seek fires nothing");
			Assert.That(v, Is.EqualTo(1.5f).Within(1e-3f), "second child sampled mid");
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Playing), "not halted by the skipped pause");

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(1.75f).Within(1e-3f), "play continues from the sought position");
			Assert.That(log, Is.Empty, "skipped callback stays consumed");
		}

		[Test]
		public void SequenceSeek_WithCallbacks_HaltsAtPause()
		{
			var v = 0f;
			var log = new List<string>();
			var sb = ManualSequence();
			sb.Append(FloatTween(() => v, x => v = x, 1f, 1f));
			sb.AppendCallback(() => log.Add("cb"));
			sb.AddPause(1.5f);
			sb.Append(FloatTween(() => v, x => v = x, 2f, 1f).SetEase(Easing.Linear()));
			var seq = sb.Start();

			FeatherTweenRunner.ManualTick(0.1);
			seq.Seek(2.5f, fireCallbacks: true);
			Assert.That(log, Is.EqualTo(new[] { "cb" }), "callback crossed in order");
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Paused), "halted at the pause");
			// Pause sits at 1.5; the second child spans [1, 2], so its local 0.5
			// sample is v = 1.5.
			Assert.That(v, Is.EqualTo(1.5f).Within(1e-3f), "playhead left at the pause");
		}

		[Test]
		public void SequenceSeek_Backward_RearmsFromSnap()
		{
			var v = 5f;
			var sb = ManualSequence();
			sb.Append(FT.From(() => v, x => v = x, 0f, 1f));
			var seq = sb.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(2.5f).Within(1e-3f), "From snapped to 0, moving toward 5");

			// Mutate the target mid-flight, then seek backward across the child
			// start: the snap must re-arm and capture fresh values on replay.
			seq.Seek(0f, fireCallbacks: true);
			v = 8f;
			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(4f).Within(1e-3f), "re-snap: 0 -> 8, mid = 4");
		}

		// --- Sequence loops ---

		[Test]
		public void SequenceSetLoops_Restart_ChildrenReplayWithRearmedSnaps()
		{
			var v = 0f;
			var steps = 0;
			var completed = false;
			// FromTo: fixed endpoints replay exactly per cycle. (A To child
			// re-snaps from its current value on loop wrap by deferred-snap
			// design, which would make cycle 1 a constant hold at the end value.)
			var sb = ManualSequence().SetLoops(2, LoopType.Restart);
			sb.Append(FT.FromTo(x => v = x, 0f, 1f, 1f));
			var seq = sb.Start().OnStepComplete(() => steps++);
			seq.OnComplete(() => completed = true);

			Assert.That(seq.Duration, Is.EqualTo(1f).Within(1e-4f), "Duration reports a single cycle");

			FeatherTweenRunner.ManualTick(1.5);
			Assert.That(seq.TotalProgress, Is.EqualTo(0.75f).Within(1e-3f), "TotalProgress spans all loops");
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "cycle 1 replays the pattern (re-snapped from 0)");
			Assert.That(steps, Is.EqualTo(1), "one boundary crossed");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(completed, Is.True);
			Assert.That(steps, Is.EqualTo(2), "final loop end fires OnStepComplete too");
		}

		[Test]
		public void SequenceSetLoops_CallbacksFirePerLoop()
		{
			var v = 0f;
			var calls = 0;
			var sb = ManualSequence().SetLoops(3, LoopType.Restart);
			sb.Append(FloatTween(() => v, x => v = x, 1f, 0.5f));
			sb.AppendCallback(() => calls++);
			sb.Start();

			FeatherTweenRunner.ManualTick(2.0);
			Assert.That(calls, Is.EqualTo(3), "AppendCallback re-arms per cycle");
		}

		[Test]
		public void SequenceSetLoops_Yoyo_SecondCycleReversesChildren()
		{
			var a = 0f;
			var b = 0f;
			var sb = ManualSequence().SetLoops(2, LoopType.Yoyo);
			sb.Append(FloatTween(() => a, x => a = x, 1f, 1f));
			sb.Append(FloatTween(() => b, x => b = x, 1f, 1f));
			var seq = sb.Start();

			FeatherTweenRunner.ManualTick(2.0);
			Assert.That(a, Is.EqualTo(1f).Within(1e-3f));
			Assert.That(b, Is.EqualTo(1f).Within(1e-3f), "cycle 0 complete");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f), "yoyo: b rewinds first");
			Assert.That(a, Is.EqualTo(1f).Within(1e-3f), "a untouched so far");

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(b, Is.EqualTo(0f).Within(1e-3f), "b fully rewound");
			Assert.That(a, Is.EqualTo(0.5f).Within(1e-3f), "a rewinding");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(a, Is.EqualTo(0f).Within(1e-3f), "yoyo ends at start values");
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Completed).Or.EqualTo(TweenStatus.Disposed));
		}

		[Test]
		public void SequenceSetLoops_Infinite_KeepsCycling()
		{
			var v = 0f;
			var steps = 0;
			var sb = ManualSequence().SetLoops(-1, LoopType.Restart);
			sb.Append(FT.FromTo(x => v = x, 0f, 1f, 1f));
			var seq = sb.Start().OnStepComplete(() => steps++);

			FeatherTweenRunner.ManualTick(5.5);
			Assert.That(steps, Is.EqualTo(5));
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f));
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Playing));
		}

		[Test]
		public void SequenceCompleteAtCycleEnd_StopAtNextEnd_Completes()
		{
			var v = 0f;
			var completed = false;
			var sb = ManualSequence().SetLoops(-1, LoopType.Restart);
			sb.Append(FT.FromTo(x => v = x, 0f, 1f, 1f));
			var seq = sb.Start();
			seq.OnComplete(() => completed = true);

			FeatherTweenRunner.ManualTick(0.5);
			seq.CompleteAtCycleEnd();

			FeatherTweenRunner.ManualTick(0.6);
			Assert.That(completed, Is.True, "infinite sequence completes at the next cycle boundary");
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "stopped at the cycle end value");
		}

		[Test]
		public void SequenceSetRemainingCycles_Absolute_CompletesAfterCount()
		{
			var v = 0f;
			var steps = 0;
			var completed = false;
			var sb = ManualSequence().SetLoops(-1, LoopType.Restart);
			sb.Append(FT.FromTo(x => v = x, 0f, 1f, 1f));
			var seq = sb.Start().OnStepComplete(() => steps++);
			seq.OnComplete(() => completed = true);

			FeatherTweenRunner.ManualTick(0.5);
			seq.SetRemainingCycles(2); // in-progress cycle counts as the first

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(completed, Is.False, "mid second (final) cycle");

			FeatherTweenRunner.ManualTick(0.6);
			Assert.That(steps, Is.EqualTo(2));
			Assert.That(completed, Is.True);
		}

		// --- Tween handle Duration / TotalProgress (mirror the Sequence members) ---

		[Test]
		public void TweenHandle_Duration_And_TotalProgress()
		{
			var v = 0f;
			var t = FloatTween(() => v, x => v = x, 1f, 2f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(2, LoopType.Restart)
				.SetAutoKill(false)
				.Start();

			Assert.That(t.Duration, Is.EqualTo(2f).Within(1e-4f), "Duration reports a single cycle");

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(t.TotalProgress, Is.EqualTo(0.25f).Within(1e-3f), "TotalProgress spans all loops");

			FeatherTweenRunner.ManualTick(2.0);
			Assert.That(t.TotalProgress, Is.EqualTo(0.75f).Within(1e-3f));

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(t.TotalProgress, Is.EqualTo(1f).Within(1e-3f));
		}

		[Test]
		public void TweenHandle_TotalProgress_InfiniteLoops_ReportsCycleProgress()
		{
			var v = 0f;
			var t = FloatTween(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1)
				.Start();

			FeatherTweenRunner.ManualTick(2.25);
			Assert.That(t.TotalProgress, Is.EqualTo(0.25f).Within(1e-3f),
				"infinite loop reports progress within the current cycle");
		}

		// --- Sequence Reverse ---

		[Test]
		public void SequenceReverse_RewindsChildrenInWindowOrder()
		{
			var a = 0f;
			var b = 0f;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => a, x => a = x, 1f, 1f));
			sb.Append(FloatTween(() => b, x => b = x, 1f, 1f));
			var seq = sb.Start();

			FeatherTweenRunner.ManualTick(1.5);
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f));

			seq.Reverse();
			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(b, Is.EqualTo(0f).Within(1e-3f), "b rewound");
			Assert.That(a, Is.EqualTo(0.5f).Within(1e-3f), "a rewinding");

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(a, Is.EqualTo(0f).Within(1e-3f), "clamped at sequence start");
		}

		// --- Kill / Complete walks ---

		[Test]
		public void SequenceKillComplete_WalksToEndWithCallbacks()
		{
			var v = 0f;
			var log = new List<string>();
			var sb = ManualSequence();
			sb.Append(FloatTween(() => v, x => v = x, 1f, 1f));
			sb.AppendCallback(() => log.Add("mid"));
			sb.AddPause(1f);
			sb.Append(FloatTween(() => v, x => v = x, 2f, 1f).OnComplete(() => log.Add("child2")));
			var seq = sb.Start();

			FeatherTweenRunner.ManualTick(0.25);
			seq.Kill(complete: true);
			Assert.That(v, Is.EqualTo(2f).Within(1e-3f), "walked to end value");
			Assert.That(log, Does.Contain("mid"), "callback crossed");
			Assert.That(log, Does.Contain("child2"), "child completion fired");
			Assert.That(seq.IsAlive, Is.False);
		}

		[Test]
		public void SequenceKill_DisposesImmediately()
		{
			var v = 0f;
			var killed = false;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => v, x => v = x, 1f, 1f));
			var seq = sb.Start().OnKill(() => killed = true);

			FeatherTweenRunner.ManualTick(0.25);
			seq.Kill();
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "value left where it was");
			Assert.That(killed, Is.True);
			Assert.That(seq.IsAlive, Is.False);
		}

		// --- Mid-play Insert ---

		[Test]
		public void SequenceInsert_MidPlay_NewChildPlaysInWindow()
		{
			var a = 0f;
			var b = 0f;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => a, x => a = x, 1f, 2f));
			var seq = sb.Start();

			FeatherTweenRunner.ManualTick(0.5);
			seq.Insert(1f, FloatTween(() => b, x => b = x, 1f, 1f).SetUpdate(UpdatePhase.Manual));

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f), "inserted child mid-window");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(b, Is.EqualTo(1f).Within(1e-3f));
			Assert.That(a, Is.EqualTo(1f).Within(1e-3f));
		}

		[Test]
		public void SequenceInsert_FromChildCallback_DefersToEndOfTick()
		{
			var a = 0f;
			var b = 0f;
			Sequence seq = default;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => a, x => a = x, 1f, 1f)
				.OnComplete(() => seq.Insert(1.5f, FloatTween(() => b, x => b = x, 1f, 1f).SetUpdate(UpdatePhase.Manual))));
			sb.AppendInterval(2f);
			seq = sb.Start();

			FeatherTweenRunner.ManualTick(1.0); // completes child; insert deferred, no crash
			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f), "inserted child playing on later ticks");
		}

		// --- Reverse through delay (phase 1.15): a sequence delay is part of
		// the timeline; reversing counts it back toward playhead 0 instead of
		// stalling in Delayed forever. ---

		[Test]
		public void SequenceReverse_DuringInitialDelay_CountsBackDown_NoStall()
		{
			var v = 0f;
			var sb = ManualSequence().SetDelay(0.5f);
			sb.Append(FT.FromTo(x => v = x, 0f, 1f, 1f));
			var seq = sb.Start();

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Delayed));

			seq.Reverse();
			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Delayed), "holds at playhead 0 inside the delay");
			Assert.That(v, Is.EqualTo(0f).Within(1e-6f), "children untouched");

			seq.Reverse();
			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "recovers forward through the full delay");
		}

		[Test]
		public void SequenceReverse_FromContent_BackThroughDelay()
		{
			var v = 0f;
			var sb = ManualSequence().SetDelay(0.5f);
			sb.Append(FT.FromTo(x => v = x, 0f, 1f, 1f));
			var seq = sb.Start();

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "0.5 into content after the delay");

			seq.Reverse();
			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(0f).Within(1e-3f), "walked back to the content start");
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Delayed), "overshoot re-entered the delay");

			seq.Reverse();
			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "forward: remaining delay, then content");
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Playing));
		}

		// --- Complete() playhead sync (phase 1.15) ---

		[Test]
		public void Complete_SyncsPlayhead_SeekAfterwardStartsFromCompletedPosition()
		{
			var v = 0f;
			var t = FloatTween(() => v, x => v = x, 1f, 2f)
				.SetUpdate(UpdatePhase.Manual)
				.SetAutoKill(false)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			t.Complete();
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f));
			Assert.That(t.TotalProgress, Is.EqualTo(1f).Within(1e-4f), "playhead sits at the completed position");

			t.Seek(1f);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "Seek repositions from the synced playhead");
		}

		// --- Time scale ---

		[Test]
		public void GlobalTimeScale_HalvesObservedProgress()
		{
			var v = 0f;
			FloatTween(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FT.GlobalTimeScale = 0.5f;
			Assert.That(FT.GlobalTimeScale, Is.EqualTo(0.5f), "property round-trips");
			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f));
		}

		[Test]
		public void PhaseAndTweenScales_ComposeMultiplicatively()
		{
			var v = 0f;
			var t = FloatTween(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FT.GlobalTimeScale = 0.5f;
			FT.SetTimeScale(UpdatePhase.Manual, 0.5f);
			t.SetTimeScale(2f);

			FeatherTweenRunner.ManualTick(1.0);
			// 1.0 * 0.5 (global) * 0.5 (phase) * 2 (tween) = 0.5
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f));
		}

		[Test]
		public void SequenceTimeScale_AppliesToChildren()
		{
			var v = 0f;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => v, x => v = x, 1f, 1f));
			var seq = sb.Start();
			seq.SetTimeScale(0.5f);

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "sequence scale slows its children");
		}

		[Test]
		public void NegativeTimeScale_Throws()
		{
			var v = 0f;
			var t = FloatTween(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.Throws<ArgumentOutOfRangeException>(() => t.SetTimeScale(-1f));
			Assert.Throws<ArgumentOutOfRangeException>(() => FT.GlobalTimeScale = -1f);
			Assert.Throws<ArgumentOutOfRangeException>(() => FT.SetTimeScale(UpdatePhase.Manual, -0.5f));
		}

		[Test]
		public void GlobalTimeScale_GovernsIgnoreTimeScaleTweens()
		{
			// Root scale is engine-side, distinct from Unity's Time.timeScale:
			// ignoreTimeScale only opts out of Unity's, not ours.
			var v = 0f;
			FloatTween(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual, ignoreTimeScale: true)
				.Start();

			FT.GlobalTimeScale = 0.25f;
			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f));
		}
	}
}
