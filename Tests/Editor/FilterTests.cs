using System;
using System.Collections.Generic;
using NUnit.Framework;
using PATween;
using PATween.Internal;

namespace PATween.Tests
{
	// Phase 1.12: target-indexed multimap (Kill(target)/IsTweening), bulk ops
	// (KillAll/PauseAll/ResumeAll), and the storage surgery around them
	// (swap-remove active lists, pooled TweenData records).
	[TestFixture]
	public class FilterTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			PATweenRunner.Reset();
			Interpolators.Reset();
		}

		private static Tween ManualTween(object target, Action<float> setter = null, float duration = 10f)
		{
			return global::PATween.PATween.To(() => 0f, setter ?? (_ => { }), 1f, duration)
				.SetUpdate(UpdatePhase.Manual)
				.SetTarget(target)
				.Start();
		}

		[Test]
		public void KillTarget_RemovesOnlyThatTargetsTweens()
		{
			var targets = new object[20];
			var handles = new List<Tween>();
			for (var i = 0; i < targets.Length; i++)
			{
				targets[i] = new object();
				for (var j = 0; j < 10; j++)
				{
					handles.Add(ManualTween(targets[i]));
				}
			}

			global::PATween.PATween.Kill(targets[7]);

			for (var i = 0; i < handles.Count; i++)
			{
				var expectAlive = i / 10 != 7;
				Assert.That(handles[i].IsAlive, Is.EqualTo(expectAlive), $"handle {i}");
			}
			Assert.That(global::PATween.PATween.IsTweening(targets[7]), Is.False);
			Assert.That(global::PATween.PATween.IsTweening(targets[8]), Is.True);
		}

		[Test]
		public void KillTarget_Complete_SnapsToEndAndFiresOnComplete()
		{
			var target = new object();
			var v = 0f;
			var completed = false;
			global::PATween.PATween.To(() => v, x => v = x, 1f, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.SetTarget(target)
				.OnComplete(() => completed = true)
				.Start();

			PATweenRunner.ManualTick(1.0);
			global::PATween.PATween.Kill(target, complete: true);

			Assert.That(v, Is.EqualTo(1f).Within(1e-3f));
			Assert.That(completed, Is.True);
		}

		[Test]
		public void IsTweening_TrueIffLiveTweenExists()
		{
			var target = new object();
			Assert.That(global::PATween.PATween.IsTweening(target), Is.False, "nothing started");

			var t = ManualTween(target);
			Assert.That(global::PATween.PATween.IsTweening(target), Is.True);

			t.Kill();
			Assert.That(global::PATween.PATween.IsTweening(target), Is.False, "multimap entry removed with last tween");
		}

		[Test]
		public void MultimapEntry_SurvivesUntilLastTweenDies()
		{
			var target = new object();
			var a = ManualTween(target);
			var b = ManualTween(target);

			a.Kill();
			Assert.That(global::PATween.PATween.IsTweening(target), Is.True, "one of two still live");
			b.Kill();
			Assert.That(global::PATween.PATween.IsTweening(target), Is.False);
		}

		[Test]
		public void SequenceWithSetTarget_ParticipatesInMultimap()
		{
			var target = new object();
			var v = 0f;
			var sb = global::PATween.PATween.Sequence().SetUpdate(UpdatePhase.Manual).SetTarget(target);
			sb.Append(global::PATween.PATween.To(() => v, x => v = x, 1f, 10f));
			var seq = sb.Start();

			Assert.That(global::PATween.PATween.IsTweening(target), Is.True);

			global::PATween.PATween.Kill(target);
			Assert.That(seq.IsAlive, Is.False, "Kill(target) reached the sequence");
			Assert.That(global::PATween.PATween.IsTweening(target), Is.False);
		}

		[Test]
		public void ShortcutTarget_ReachableThroughKillTarget()
		{
			var go = new UnityEngine.GameObject("filter-test");
			var t = global::PATween.PATween.Move(go.transform, UnityEngine.Vector3.one, 10f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.That(global::PATween.PATween.IsTweening(go.transform), Is.True);
			global::PATween.PATween.Kill(go.transform);
			Assert.That(t.IsAlive, Is.False);
			UnityEngine.Object.DestroyImmediate(go);
		}

		[Test]
		public void KillAll_KillsEveryPhase()
		{
			var a = ManualTween(null);
			var b = global::PATween.PATween.To(() => 0f, _ => { }, 1f, 10f).Start(); // Update phase

			global::PATween.PATween.KillAll();
			Assert.That(a.IsAlive, Is.False);
			Assert.That(b.IsAlive, Is.False);
		}

		[Test]
		public void PauseAll_ResumeAll_RoundTrip()
		{
			var v = 0f;
			var t = global::PATween.PATween.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			PATweenRunner.ManualTick(0.25);
			global::PATween.PATween.PauseAll();
			Assert.That(t.Status, Is.EqualTo(TweenStatus.Paused));

			PATweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(0.25f).Within(1e-3f), "paused: no advance");

			global::PATween.PATween.ResumeAll();
			PATweenRunner.ManualTick(0.25);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-3f), "resumed");
		}

		[Test]
		public void BatchKill_SwapRemove_KeepsRemainingTweensTicking()
		{
			// Kills a large slice mid-list; the swap-removed survivors must keep
			// ticking correctly (regression guard for the O(1) removal).
			var values = new float[30];
			var handles = new Tween[30];
			for (var i = 0; i < 30; i++)
			{
				var idx = i;
				handles[i] = global::PATween.PATween.To(() => values[idx], x => values[idx] = x, 1f, 1f)
					.SetUpdate(UpdatePhase.Manual)
					.Start();
			}

			for (var i = 5; i < 25; i++)
			{
				handles[i].Kill();
			}

			PATweenRunner.ManualTick(0.5);
			for (var i = 0; i < 30; i++)
			{
				var expected = (i >= 5 && i < 25) ? 0f : 0.5f;
				Assert.That(values[i], Is.EqualTo(expected).Within(1e-3f), $"tween {i}");
			}
		}

		[Test]
		public void PooledRecord_ReuseDoesNotLeakStateAcrossTweens()
		{
			// Configure a heavily-customized tween, kill it, then start a plain
			// one: the recycled record must behave like a fresh default.
			var v1 = 0f;
			var loops = 0;
			var t1 = global::PATween.PATween.To(() => v1, x => v1 = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetLoops(-1, LoopType.Yoyo)
				.SetDelay(0.5f, DelayType.EveryLoop)
				.SetEase(Easing.OutBounce())
				.OnStepComplete(() => loops++)
				.Start();
			PATweenRunner.ManualTick(2.0);
			t1.Kill();
			PATweenRunner.ManualTick(0.016); // flush pool returns

			var v2 = 0f;
			global::PATween.PATween.To(() => v2, x => v2 = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();
			var loopsBefore = loops;
			PATweenRunner.ManualTick(0.5);
			Assert.That(v2, Is.EqualTo(0.5f).Within(1e-3f), "linear, no delay, no yoyo residue");
			PATweenRunner.ManualTick(0.6);
			Assert.That(v2, Is.EqualTo(1f).Within(1e-3f), "completed once, no loop residue");
			Assert.That(loops, Is.EqualTo(loopsBefore), "no callback residue on the recycled record");
		}

		private static readonly Func<float> zeroGetter = () => 0f;
		private static readonly Action<float> noopSetter = _ => { };

		[Test]
		public void CreateKillCycle_AfterWarmup_ZeroManagedAlloc()
		{
			// §8.1: with cached delegates, a create/kill cycle is alloc-free once
			// pools are warm (TweenData records, builder buffers, store lists).
			for (var round = 0; round < 3; round++)
			{
				SpawnKillBatch(64);
			}

			var before = GC.GetAllocatedBytesForCurrentThread();
			SpawnKillBatch(64);
			var delta = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.That(delta, Is.Zero,
				$"Create/kill cycle allocated {delta} bytes after warmup; TweenData pooling must make Start() alloc-free.");
		}

		private static void SpawnKillBatch(int count)
		{
			for (var i = 0; i < count; i++)
			{
				global::PATween.PATween.To(zeroGetter, noopSetter, 1f, 100f)
					.SetUpdate(UpdatePhase.Manual)
					.SetAutoKill(false)
					.Start();
			}
			PATweenRunner.ManualTick(0.016);
			global::PATween.PATween.KillAll();
			PATweenRunner.ManualTick(0.016); // flush pool returns
		}

		[Test]
		public void KillFromCallback_Defers_NoCorruption()
		{
			var target = new object();
			var killedInCallback = false;
			global::PATween.PATween.To(() => 0f, _ => { }, 1f, 0.1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetTarget(target)
				.OnComplete(() =>
				{
					global::PATween.PATween.Kill(target);
					killedInCallback = true;
				})
				.Start();
			ManualTween(target, duration: 10f);

			PATweenRunner.ManualTick(0.2);
			Assert.That(killedInCallback, Is.True);
			Assert.That(global::PATween.PATween.IsTweening(target), Is.False, "bulk kill from callback drained at end of tick");
		}
	}
}
