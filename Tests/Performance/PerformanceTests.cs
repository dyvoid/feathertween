using System;
using NUnit.Framework;
using Unity.PerformanceTesting;
using PATween;
using PATween.Internal;

namespace PATween.Tests.Performance
{
	// Two distinct concerns live here:
	//   1. Allocation guards   -> hard pass/fail. Steady-state ticking must not allocate managed memory.
	//   2. Throughput measures -> report only. Numbers for eyeballing regressions, never gate a build.
	[TestFixture]
	public class PerformanceTests
	{
		private static readonly Action<float> noopSetter = _ => { };
		private static readonly Func<float> zeroGetter = () => 0f;

		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			PATweenRunner.Reset();
			Interpolators.Reset();
			global::PATween.PATween.SetCapacity(200_000, 0);
		}

		private static void SpawnManualTweens(int count)
		{
			for (var i = 0; i < count; i++)
			{
				global::PATween.PATween.To(zeroGetter, noopSetter, 1f, 100_000f)
					.SetUpdate(UpdatePhase.Manual)
					.SetAutoKill(false)
					.Start();
			}
		}

		// Warms every lazy path: pool growth, snapshot-list capacity, interpolator lookup.
		private static void Warmup(int ticks)
		{
			for (var i = 0; i < ticks; i++)
			{
				PATweenRunner.ManualTick(0.016);
			}
		}

		[Test]
		public void Create_AfterWarmup_ZeroManagedAlloc()
		{
			// docs/architecture/performance.md: creation is 1 pooled TweenData<T> + 1 delegate pair. With
			// cached static delegates the whole create/kill cycle must be
			// alloc-free once pools are warm (records, builder buffers, lists).
			const int batch = 256;
			for (var round = 0; round < 3; round++)
			{
				SpawnKillBatch(batch);
			}

			var before = GC.GetAllocatedBytesForCurrentThread();
			SpawnKillBatch(batch);
			var delta = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.That(delta, Is.Zero,
				$"Create/kill cycle allocated {delta} bytes after warmup; TweenData pooling must make Start() alloc-free.");
		}

		private static void SpawnKillBatch(int count)
		{
			for (var i = 0; i < count; i++)
			{
				global::PATween.PATween.To(zeroGetter, noopSetter, 1f, 100_000f)
					.SetUpdate(UpdatePhase.Manual)
					.SetAutoKill(false)
					.Start();
			}
			PATweenRunner.ManualTick(0.016);
			global::PATween.PATween.KillAll();
			PATweenRunner.ManualTick(0.016); // drain pool returns
		}

		[Test]
		public void Tick_SteadyState1kTweens_ZeroManagedAlloc()
		{
			SpawnManualTweens(1000);
			Warmup(120);

			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 600; i++)
			{
				PATweenRunner.ManualTick(0.016);
			}
			var delta = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.That(delta, Is.Zero,
				$"Steady-state ticking allocated {delta} bytes over 600 ticks; must be zero.");
		}

		[Test]
		public void Tick_AfterKillRealloc_ZeroManagedAlloc()
		{
			SpawnManualTweens(1000);
			Warmup(60);

			TweenStore.Reset();
			SpawnManualTweens(1000);
			Warmup(120);

			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 300; i++)
			{
				PATweenRunner.ManualTick(0.016);
			}
			var delta = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.That(delta, Is.Zero,
				$"Ticking after kill/realloc allocated {delta} bytes; free-list reuse must not allocate.");
		}

		[Test]
		public void Tick_SteadyStateCallbackDispatch_ZeroManagedAlloc()
		{
			var sink = 0f;
			for (var i = 0; i < 100; i++)
			{
				global::PATween.PATween.To(zeroGetter, noopSetter, 1f, 1f)
					.SetUpdate(UpdatePhase.Manual)
					.SetLoops(-1, LoopType.Restart)
					.OnUpdate(t => sink = t)
					.OnStepComplete(() => sink += 1f)
					.Start();
			}
			Warmup(120);

			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 600; i++)
			{
				PATweenRunner.ManualTick(0.016);
			}
			var delta = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.That(delta, Is.Zero,
				$"Callback dispatch allocated {delta} bytes over 600 ticks; must be zero (docs/architecture/performance.md).");
		}

		[Test]
		public void Tick_SteadyState100Sequences_ZeroManagedAlloc()
		{
			for (var i = 0; i < 100; i++)
			{
				var sb = global::PATween.PATween.Sequence()
					.SetUpdate(UpdatePhase.Manual)
					.SetAutoKill(false);
				sb.Append(global::PATween.PATween.To(zeroGetter, noopSetter, 1f, 100_000f));
				sb.Join(global::PATween.PATween.To(zeroGetter, noopSetter, 1f, 100_000f));
				sb.Append(global::PATween.PATween.To(zeroGetter, noopSetter, 1f, 100_000f));
				sb.Start();
			}
			Warmup(120);

			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 600; i++)
			{
				PATweenRunner.ManualTick(0.016);
			}
			var delta = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.That(delta, Is.Zero,
				$"Steady-state sequence ticking allocated {delta} bytes over 600 ticks; must be zero.");
		}

		[Test, Performance]
		public void Throughput_Tick1kTweens()
		{
			SpawnManualTweens(1000);
			Warmup(60);

			Measure.Method(() => PATweenRunner.ManualTick(0.016))
				.WarmupCount(20)
				.MeasurementCount(100)
				.Run();
		}

		[Test, Performance]
		public void Throughput_Tick10kTweens()
		{
			SpawnManualTweens(10_000);
			Warmup(60);

			Measure.Method(() => PATweenRunner.ManualTick(0.016))
				.WarmupCount(20)
				.MeasurementCount(100)
				.Run();
		}

		[Test, Performance]
		public void Throughput_StartTween()
		{
			Warmup(1);

			Measure.Method(() =>
				{
					global::PATween.PATween.To(zeroGetter, noopSetter, 1f, 100_000f)
						.SetUpdate(UpdatePhase.Manual)
						.SetAutoKill(false)
						.Start();
				})
				.WarmupCount(50)
				.MeasurementCount(200)
				.Run();
		}
	}
}
