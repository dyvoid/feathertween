using System;
using System.Collections.Generic;
using NUnit.Framework;
using PATween;
using PATween.Internal;
using UnityEngine;

namespace PATween.Tests
{
	[TestFixture]
	public class SequenceTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			PATweenRunner.Reset();
			Interpolators.Reset();
		}

		private static TweenBuilder<float> FloatTween(Func<float> getter, Action<float> setter, float end, float duration)
		{
			return global::PATween.PATween.To(getter, setter, end, duration);
		}

		private static SequenceBuilder ManualSequence()
		{
			return global::PATween.PATween.Sequence().SetUpdate(UpdatePhase.Manual);
		}

		[Test]
		public void Append_ChildrenRunSequentially()
		{
			var a = 0f;
			var b = 0f;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => a, v => a = v, 1f, 1f));
			sb.Append(FloatTween(() => b, v => b = v, 1f, 1f));
			var seq = sb.Start();

			Assert.That(seq.Duration, Is.EqualTo(2f).Within(1e-4f));

			PATweenRunner.ManualTick(0.5);
			Assert.That(a, Is.EqualTo(0.5f).Within(1e-3f), "first child mid");
			Assert.That(b, Is.EqualTo(0f).Within(1e-3f), "second child not started");

			PATweenRunner.ManualTick(1.0);
			Assert.That(a, Is.EqualTo(1f).Within(1e-3f), "first child done");
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f), "second child mid");
		}

		[Test]
		public void Join_SharesStartWithPreviousAppend()
		{
			var a = 0f;
			var b = 0f;
			var c = 0f;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => a, v => a = v, 1f, 1f));
			sb.Join(FloatTween(() => b, v => b = v, 1f, 1f));
			sb.Join(FloatTween(() => c, v => c = v, 1f, 1f));
			sb.Start();

			PATweenRunner.ManualTick(0.5);
			Assert.That(a, Is.EqualTo(0.5f).Within(1e-3f));
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f), "joined child shares start time");
			Assert.That(c, Is.EqualTo(0.5f).Within(1e-3f), "join-after-join shares the original anchor");
		}

		[Test]
		public void Insert_AtTime_SetsChildWindow_AndExtendsDuration()
		{
			var a = 0f;
			var b = 0f;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => a, v => a = v, 1f, 1f));
			sb.Insert(2f, FloatTween(() => b, v => b = v, 1f, 1f));
			var seq = sb.Start();

			Assert.That(seq.Duration, Is.EqualTo(3f).Within(1e-4f), "insert beyond end extends duration");

			PATweenRunner.ManualTick(1.5);
			Assert.That(b, Is.EqualTo(0f).Within(1e-3f), "inserted child not reached at 1.5");

			PATweenRunner.ManualTick(1.0);
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f), "inserted child mid at 2.5");
		}

		[Test]
		public void Insert_NegativeTime_Throws()
		{
			var v = 0f;
			var sb = ManualSequence();
			Assert.Throws<ArgumentOutOfRangeException>(
				() => sb.Insert(-0.1f, FloatTween(() => v, x => v = x, 1f, 1f)));
		}

		[Test]
		public void Label_DefinedAfterUse_ResolvesAtStart()
		{
			var v = 0f;
			var sb = ManualSequence();
			sb.Insert(Position.AtLabel("intro", 0.5f), FloatTween(() => v, x => v = x, 1f, 1f));
			sb.AddLabel("intro", 1f);
			var seq = sb.Start();

			Assert.That(seq.Duration, Is.EqualTo(2.5f).Within(1e-4f), "child at label 1 + 0.5 offset, 1s long");

			PATweenRunner.ManualTick(2.0);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "child mid at 2.0 (window 1.5..2.5)");
		}

		[Test]
		public void Label_UndefinedAtStart_Throws()
		{
			var v = 0f;
			var sb = ManualSequence();
			sb.Insert(Position.AtLabel("missing"), FloatTween(() => v, x => v = x, 1f, 1f));
			Assert.Throws<InvalidOperationException>(() => sb.Start());
		}

		[Test]
		public void DefaultsCascade_AppliedAtAppend_ThenFrozen()
		{
			var a = 0f;
			var b = 0f;
			var sb = ManualSequence().SetDefaults(loops: 2);
			sb.Append(FloatTween(() => a, v => a = v, 1f, 1f));
			sb.SetDefaults(loops: 4);
			sb.Append(FloatTween(() => b, v => b = v, 1f, 1f));
			var seq = sb.Start();

			// First child froze loops:2 at append (length 2); second got loops:4 (length 4).
			Assert.That(seq.Duration, Is.EqualTo(6f).Within(1e-4f));

			PATweenRunner.ManualTick(1.5);
			Assert.That(a, Is.EqualTo(0.5f).Within(1e-3f), "first child restarts its second loop");
		}

		[Test]
		public void DefaultsCascade_DoesNotOverrideExplicitChildSettings()
		{
			var a = 0f;
			var sb = ManualSequence().SetDefaults(loops: 4);
			sb.Append(FloatTween(() => a, v => a = v, 1f, 1f).SetLoops(1));
			var seq = sb.Start();

			Assert.That(seq.Duration, Is.EqualTo(1f).Within(1e-4f), "explicit SetLoops(1) wins over default");
		}

		[Test]
		public void SameBuilder_AppendedTwice_Throws()
		{
			var v = 0f;
			var builder = FloatTween(() => v, x => v = x, 1f, 1f);
			var sb = ManualSequence();
			sb.Append(builder);
			Assert.Throws<InvalidOperationException>(() => sb.Append(builder));
		}

		[Test]
		public void AppendCallback_FiresAtItsTime_InOrder()
		{
			var v = 0f;
			var log = new List<string>();
			var sb = ManualSequence();
			sb.AppendCallback(() => log.Add("start"));
			sb.Append(FloatTween(() => v, x => v = x, 1f, 1f));
			sb.AppendCallback(() => log.Add("end"));
			sb.Start();

			PATweenRunner.ManualTick(0.5);
			Assert.That(log, Is.EqualTo(new[] { "start" }));

			PATweenRunner.ManualTick(0.5);
			Assert.That(log, Is.EqualTo(new[] { "start", "end" }));
		}

		[Test]
		public void AddPause_HaltsPlayhead_ResumeContinues()
		{
			var v = 0f;
			var pauseFired = false;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => v, x => v = x, 1f, 1f));
			sb.AddPause(0.5f, () => pauseFired = true);
			var seq = sb.Start();

			PATweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "playhead clamped at the pause");
			Assert.That(pauseFired, Is.True);
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Paused));

			seq.Resume();
			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f), "resumed to completion");
		}

		[Test]
		public void CallbackOrderedAfterPause_AtSameTime_HeldUntilResume()
		{
			var v = 0f;
			var log = new List<string>();
			var sb = ManualSequence();
			sb.Append(FloatTween(() => v, x => v = x, 1f, 1f));
			sb.AddPause(1f);
			sb.AppendCallback(() => log.Add("after"));
			var seq = sb.Start();

			PATweenRunner.ManualTick(1.5);
			Assert.That(log, Is.Empty, "callback positioned after the pause must not fire while paused");
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Paused));

			seq.Resume();
			PATweenRunner.ManualTick(0.01);
			Assert.That(log, Is.EqualTo(new[] { "after" }));
		}

		[Test]
		public void EmptySequence_CompletesOnFirstTick()
		{
			var completed = false;
			ManualSequence().OnComplete(() => completed = true).Start();

			PATweenRunner.ManualTick(0.016);
			Assert.That(completed, Is.True);
		}

		[Test]
		public void SequencedFrom_DoesNotSnapUntilParentReachesWindow()
		{
			var v = 5f;
			var sb = ManualSequence();
			sb.Insert(2f, global::PATween.PATween.From(() => v, x => v = x, 0f, 1f));
			sb.Start();

			PATweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(5f).Within(1e-3f), "no snap before the child window");

			PATweenRunner.ManualTick(1.5);
			Assert.That(v, Is.EqualTo(2.5f).Within(1e-3f), "snapped to 0 at window entry, then interpolates 0 -> 5");
		}

		[Test]
		public void CancelBehavior_Continue_SkipsDeadChild_SequenceFinishes()
		{
			var go = new GameObject("PATween_Test_Continue");
			var a = 0f;
			var b = 0f;
			var completed = false;
			var sb = ManualSequence().SetCancelBehavior(SequenceCancelBehavior.ContinueOnChildAutoKill);
			sb.Append(FloatTween(() => a, v => a = v, 1f, 1f).SetTarget(go));
			sb.Append(FloatTween(() => b, v => b = v, 1f, 1f));
			sb.OnComplete(() => completed = true);
			sb.Start();

			UnityEngine.Object.DestroyImmediate(go);

			PATweenRunner.ManualTick(1.5);
			Assert.That(a, Is.EqualTo(0f).Within(1e-3f), "dead-target child never wrote");
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f), "sequence continued past the dead child");

			PATweenRunner.ManualTick(0.5);
			Assert.That(completed, Is.True);
		}

		[Test]
		public void CancelBehavior_KillSequence_PropagatesChildAutoKill()
		{
			var go = new GameObject("PATween_Test_Kill");
			var a = 0f;
			var b = 0f;
			var killed = false;
			var sb = ManualSequence().SetCancelBehavior(SequenceCancelBehavior.KillSequenceOnChildAutoKill);
			sb.Append(FloatTween(() => a, v => a = v, 1f, 1f).SetTarget(go));
			sb.Append(FloatTween(() => b, v => b = v, 1f, 1f));
			sb.OnKill(() => killed = true);
			var seq = sb.Start();

			UnityEngine.Object.DestroyImmediate(go);

			PATweenRunner.ManualTick(0.5);
			Assert.That(killed, Is.True);
			Assert.That(seq.IsAlive, Is.False);
			Assert.That(b, Is.EqualTo(0f).Within(1e-3f), "remaining child never ran");
		}

		[Test]
		public void AppendInterval_CreatesGapBetweenChildren()
		{
			var a = 0f;
			var b = 0f;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => a, v => a = v, 1f, 1f));
			sb.AppendInterval(1f);
			sb.Append(FloatTween(() => b, v => b = v, 1f, 1f));
			var seq = sb.Start();

			Assert.That(seq.Duration, Is.EqualTo(3f).Within(1e-4f));

			PATweenRunner.ManualTick(1.5);
			Assert.That(b, Is.EqualTo(0f).Within(1e-3f), "still in the interval gap");

			PATweenRunner.ManualTick(1.0);
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f), "second child mid at 2.5");
		}

		[Test]
		public void Prepend_ShiftsExistingChildrenAndLabels()
		{
			var a = 0f;
			var b = 0f;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => a, v => a = v, 1f, 1f));
			sb.AddLabel("mark", 0.5f);
			sb.Prepend(FloatTween(() => b, v => b = v, 1f, 1f));
			var c = 0f;
			sb.Insert(Position.AtLabel("mark"), FloatTween(() => c, v => c = v, 1f, 1f));
			var seq = sb.Start();

			Assert.That(seq.Duration, Is.EqualTo(2.5f).Within(1e-4f), "label shifted to 1.5, its child ends at 2.5");

			PATweenRunner.ManualTick(0.5);
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f), "prepended child runs first");
			Assert.That(a, Is.EqualTo(0f).Within(1e-3f), "original child shifted to start at 1");

			PATweenRunner.ManualTick(1.0);
			Assert.That(a, Is.EqualTo(0.5f).Within(1e-3f));
		}

		[Test]
		public void NestedSequence_RunsInsideParentWindow()
		{
			var a = 0f;
			var b = 0f;
			var inner = ManualSequence();
			inner.Append(FloatTween(() => b, v => b = v, 1f, 1f));

			var outer = ManualSequence();
			outer.Append(FloatTween(() => a, v => a = v, 1f, 1f));
			outer.Append(inner);
			var seq = outer.Start();

			Assert.That(seq.Duration, Is.EqualTo(2f).Within(1e-4f));

			PATweenRunner.ManualTick(0.5);
			Assert.That(b, Is.EqualTo(0f).Within(1e-3f), "nested sequence not reached");

			PATweenRunner.ManualTick(1.0);
			Assert.That(b, Is.EqualTo(0.5f).Within(1e-3f), "nested child mid");
		}

		[Test]
		public void MixedValueTypes_StoredInTypedSlots()
		{
			var f = 0f;
			var v3 = Vector3.zero;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => f, v => f = v, 1f, 1f));
			sb.Append(global::PATween.PATween.To(() => v3, v => v3 = v, Vector3.one, 1f));
			sb.Start();

			Assert.That(TweenStore.HasLiveOfType<float>(), Is.True);
			Assert.That(TweenStore.HasLiveOfType<Vector3>(), Is.True, "heterogeneous children stay in typed TweenData<T> slots");
		}

		[Test]
		public void ChildDelay_AbsorbedIntoInsertionOffset()
		{
			var v = 0f;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => v, x => v = x, 1f, 1f).SetDelay(0.5f));
			var seq = sb.Start();

			Assert.That(seq.Duration, Is.EqualTo(1.5f).Within(1e-4f));

			PATweenRunner.ManualTick(0.4);
			Assert.That(v, Is.EqualTo(0f).Within(1e-3f), "inside absorbed delay");

			PATweenRunner.ManualTick(0.6);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "child mid at 1.0 (window 0.5..1.5)");
		}

		[Test]
		public void Completion_FreesSequenceAndChildren()
		{
			var v = 0f;
			var freeBefore = TweenStore.FreeCount;
			var sb = ManualSequence();
			sb.Append(FloatTween(() => v, x => v = x, 1f, 1f));
			var seq = sb.Start();

			PATweenRunner.ManualTick(1.5);
			Assert.That(seq.IsAlive, Is.False, "auto-killed on completion");
			Assert.That(TweenStore.FreeCount, Is.EqualTo(freeBefore), "sequence and child slots returned to the pool");
		}

		[Test]
		public void Restart_ReplaysChildren_AndRearmsFromSnap()
		{
			var v = 5f;
			var sb = ManualSequence().SetAutoKill(false);
			sb.Append(global::PATween.PATween.From(() => v, x => v = x, 0f, 1f));
			var seq = sb.Start();

			PATweenRunner.ManualTick(2.0);
			Assert.That(v, Is.EqualTo(5f).Within(1e-3f), "From child ends back at the original value");
			Assert.That(seq.Status, Is.EqualTo(TweenStatus.Completed));

			seq.Restart();
			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(2.5f).Within(1e-3f), "restart re-armed the snap and replays");
		}

		[Test]
		public void PhaseMismatch_ExplicitChildPhase_Throws()
		{
			var v = 0f;
			var sb = ManualSequence();
			Assert.Throws<ArgumentException>(() => sb.Append(
				FloatTween(() => v, x => v = x, 1f, 1f).SetUpdate(UpdatePhase.Update)));
		}

		[Test]
		public void BuilderAfterStart_Throws()
		{
			var sb = ManualSequence();
			sb.Start();
			var v = 0f;
			Assert.Throws<InvalidOperationException>(
				() => sb.Append(FloatTween(() => v, x => v = x, 1f, 1f)));
		}
	}
}
