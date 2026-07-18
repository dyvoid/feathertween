using System.Collections.Generic;
using NUnit.Framework;
using Dyvoid.FeatherTween;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween.Tests
{
	[TestFixture]
	public class LoopTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void Yoyo_PingPongs_AcrossTwoCycles()
		{
			var v = 0f;
			FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(2, LoopType.Yoyo)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "cycle 0, mid");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "boundary at end of cycle 0 / start of cycle 1");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "cycle 1 (yoyo) mid");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0f).Within(1e-3f), "yoyo finishes back at start");
		}

		[Test]
		public void Incremental_AddsDeltaEachCycle()
		{
			var v = 0f;
			FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(3, LoopType.Incremental)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f));

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(1.5f).Within(1e-3f), "into cycle 1, mid: 1 + 0.5");

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(2.5f).Within(1e-3f), "into cycle 2, mid: 2 + 0.5");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(3f).Within(1e-3f), "completes at 3");
		}

		[Test]
		public void Incremental_HighCycleCount_StaysCorrect()
		{
			// Guards the O(1) cycle-base cache: value must stay exact deep into
			// an incremental loop, not just for the first few cycles.
			var v = 0f;
			FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1, LoopType.Incremental)
				.Start();

			for (var i = 0; i < 200; i++)
			{
				FeatherTweenRunner.ManualTick(1.0);
			}
			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(200.5f).Within(1e-2f));
		}

		[Test]
		public void Incremental_ReverseAcrossCycles_RecomputesBase()
		{
			// Backward jumps invalidate the incremental cache's +1 fast path;
			// the base must be recomputed, not advanced.
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(5, LoopType.Incremental)
				.Start();

			FeatherTweenRunner.ManualTick(3.5);
			Assert.That(v, Is.EqualTo(3.5f).Within(1e-3f), "cycle 3, mid");

			t.Reverse();
			FeatherTweenRunner.ManualTick(2.0);
			Assert.That(v, Is.EqualTo(1.5f).Within(1e-3f), "back in cycle 1, mid");

			t.Reverse();
			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(2.5f).Within(1e-3f), "forward again into cycle 2, mid");
		}

		[Test]
		public void Restart_RepeatsSamePattern()
		{
			var v = 0f;
			FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(2, LoopType.Restart)
				.Start();

			FeatherTweenRunner.ManualTick(1.5);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "into cycle 1 mid: pattern restarts at 0");
		}

		[Test]
		public void Reverse_RewindsPlayhead_TowardZero()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 10f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.7);
			Assert.That(v, Is.EqualTo(7f).Within(1e-3f));

			t.Reverse();
			FeatherTweenRunner.ManualTick(0.4);
			Assert.That(v, Is.EqualTo(3f).Within(1e-3f));

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(0f).Within(1e-3f));
		}

		[Test]
		public void Reverse_FiresOnRewind_AtCycleBoundary()
		{
			var v = 0f;
			var rewinds = 0;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(3, LoopType.Restart)
				.OnRewind(() => rewinds++)
				.Start();

			FeatherTweenRunner.ManualTick(2.5);
			Assert.That(rewinds, Is.Zero, "no OnRewind while moving forward");

			t.Reverse();
			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(rewinds, Is.EqualTo(1), "crossed cycle 2 -> cycle 1 boundary backward");

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(rewinds, Is.EqualTo(2), "crossed cycle 1 -> cycle 0 boundary backward");
		}

		[Test]
		public void EveryLoopDelay_AppliedPerCycle()
		{
			var v = 0f;
			FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(2, LoopType.Restart)
				.SetDelay(0.5f, DelayType.EveryLoop)
				.Start();

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(0f), "in cycle 0 delay");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "cycle 0 interpolating");

			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "cycle 0 reached end");

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "in cycle 1 delay, holds previous value");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "cycle 1 interpolating");
		}

		[Test]
		public void FirstLoopDelay_OnlyOnce()
		{
			var v = 0f;
			FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(2, LoopType.Restart)
				.SetDelay(0.5f, DelayType.FirstLoop)
				.Start();

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(0f), "in initial delay");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "cycle 0 interpolating");

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "cycle 1 interpolating, no second delay");
		}

		[Test]
		public void CompleteAtCycleEnd_StopAtNextEnd_Completes()
		{
			var v = 0f;
			var completed = false;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1, LoopType.Restart)
				.OnComplete(() => completed = true)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			t.CompleteAtCycleEnd();

			FeatherTweenRunner.ManualTick(0.6);
			Assert.That(completed, Is.True);
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f));
		}

		[Test]
		public void CompleteAtCycleStart_ReversedCrossing_CompletesAtStartValue()
		{
			var v = 0f;
			var completed = false;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1, LoopType.Restart)
				.OnComplete(() => completed = true)
				.Start();

			FeatherTweenRunner.ManualTick(1.5);
			t.Reverse();
			t.CompleteAtCycleStart();

			FeatherTweenRunner.ManualTick(0.6);
			Assert.That(completed, Is.True, "completes on the backward cycle crossing");
			Assert.That(v, Is.EqualTo(0f).Within(1e-3f), "settles on the start value");
		}

		[Test]
		public void SetLoops_Zero_ClampsToOne_AndCompletes()
		{
			var v = 0f;
			var completed = false;
			FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(0)
				.OnComplete(() => completed = true)
				.Start();

			FeatherTweenRunner.ManualTick(1.5);
			Assert.That(completed, Is.True, "SetLoops(0) clamps to one cycle");
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f));
		}

		[Test]
		public void SetRemainingCycles_Zero_InFirstCycle_CompletesAtCycleEnd()
		{
			var v = 0f;
			var completed = false;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1)
				.OnComplete(() => completed = true)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			t.SetRemainingCycles(0);

			FeatherTweenRunner.ManualTick(0.6);
			Assert.That(completed, Is.True, "0 counts the in-progress cycle as the last");
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f));
		}

		[Test]
		public void CompleteAtCycleStart_SingleCycle_ReversedToStart_Completes()
		{
			var v = 0f;
			var completed = false;
			var rewound = false;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.OnComplete(() => completed = true)
				.OnRewind(() => rewound = true)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			t.Reverse();
			t.CompleteAtCycleStart();

			FeatherTweenRunner.ManualTick(0.6);
			Assert.That(completed, Is.True, "cycle 0 has no lower boundary to cross; completes at the start");
			Assert.That(rewound, Is.True, "OnRewind fires at the start");
			Assert.That(v, Is.EqualTo(0f).Within(1e-3f), "settles on the start value");
		}

		// --- Reverse through delay (phase 1.15): the delay is part of the
		// timeline; a reversed tween counts it back down before playhead 0. ---

		[Test]
		public void Reverse_FromContent_BackThroughFirstLoopDelay_HoldsAtZero()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetDelay(0.5f, DelayType.FirstLoop)
				.Start();

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "0.5 into content after the delay");

			t.Reverse();
			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(0f).Within(1e-3f), "rewound to the content start");

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Delayed), "counting the delay back down");

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Delayed), "holds at playhead 0, no stall or wrap");

			t.Reverse();
			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "forward again: delay replays, then content");
		}

		[Test]
		public void Reverse_DuringInitialDelay_Finite_CountsDownAndHolds()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetDelay(0.5f, DelayType.FirstLoop)
				.Start();

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Delayed));

			t.Reverse();
			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Delayed), "clamped at playhead 0 inside the delay");
			Assert.That(v, Is.EqualTo(0f).Within(1e-6f), "no value writes in the delay");

			t.Reverse();
			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "recovers forward through the full delay");
		}

		// --- ADR 0009 x reverse-through-delay composition: infinite loops wrap
		// backward instead of clamping, and the wrap floor depends on where the
		// delay lives (phase 1.15). ---

		[Test]
		public void InfiniteFirstLoopDelay_BackwardWrap_SkipsInitialDelay()
		{
			var v = 0f;
			var rewinds = 0;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1, LoopType.Restart)
				.SetDelay(0.5f, DelayType.FirstLoop)
				.OnRewind(() => rewinds++)
				.Start();

			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "cycle 0 after the initial delay");

			t.Reverse();
			FeatherTweenRunner.ManualTick(0.5);
			// Backward past the cycle-0 start wraps within cycle content; the
			// FirstLoop delay sits before cycle 0 only and is never re-entered.
			Assert.That(v, Is.EqualTo(0.75f).Within(1e-3f), "wrapped into the previous iteration's content");
			Assert.That(rewinds, Is.EqualTo(1), "the wrap is a cycle-boundary crossing");
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing), "never stalls in the delay");
		}

		[Test]
		public void InfiniteEveryLoopDelay_BackwardWrap_ReentersDelaySlot()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1, LoopType.Restart)
				.SetDelay(0.5f, DelayType.EveryLoop)
				.Start();

			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "cycle 0 content, past the in-slot delay");

			t.Reverse();
			FeatherTweenRunner.ManualTick(0.5);
			// Back inside cycle 0's delay portion: EveryLoop delays live in the
			// cycle slot, so the playhead passes through them going backward.
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Delayed), "re-entered the in-slot delay backward");
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "delay holds the last written value");

			FeatherTweenRunner.ManualTick(0.5);
			// Crossing below 0 wraps a full slot (delay + duration) into the
			// previous iteration's content.
			Assert.That(v, Is.EqualTo(0.75f).Within(1e-3f), "wrapped into the previous slot's content");
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing));
		}

		[Test]
		public void InfiniteFirstLoopDelay_ReverseDuringInitialDelay_WrapsIntoContent()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1, LoopType.Restart)
				.SetDelay(0.5f, DelayType.FirstLoop)
				.Start();

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Delayed), "in the initial delay");

			t.Reverse();
			FeatherTweenRunner.ManualTick(0.25);
			// Infinite means infinite in either direction (ADR 0009): reversing
			// inside the initial delay wraps into cycle content instead of
			// stalling at playhead 0.
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing));
			Assert.That(v, Is.GreaterThan(0f), "playing backward through wrapped content");
		}

		[Test]
		public void InfiniteEveryLoopDelay_ReverseDuringInitialDelay_WrapsIntoContent()
		{
			var v = 0f;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1, LoopType.Restart)
				.SetDelay(0.5f, DelayType.EveryLoop)
				.Start();

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Delayed), "in cycle 0's in-slot delay");

			t.Reverse();
			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Playing), "wrapped below 0 into the previous slot");
			Assert.That(v, Is.EqualTo(0.75f).Within(1e-3f), "landed in the previous iteration's content");
		}

		[Test]
		public void From_WithDelay_SnapsAtStart_InterpolationStartsAfterDelay()
		{
			var v = 5f;
			FT.From(() => v, x => v = x, 0f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetDelay(0.5f, DelayType.FirstLoop)
				.Start();

			Assert.That(v, Is.EqualTo(0f).Within(1e-6f), "From snaps at Start regardless of delay");

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(0f).Within(1e-6f), "still in delay, value unchanged");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(1.25f).Within(1e-3f), "interpolating: 0 + (5-0)*0.25 = 1.25");

			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(v, Is.EqualTo(5f).Within(1e-3f), "completed");
		}
	}
}
