using System;
using NUnit.Framework;
using UnityEngine.LowLevel;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween.Tests
{
	[TestFixture]
	public class FeatherTweenRunnerTests
	{
		[SetUp]
		public void SetUp()
		{
			FeatherTweenRunner.Reset();
		}

		[Test]
		public void ManualTick_AdvancesOnlyManualRoot()
		{
			var u = FeatherTweenRunner.RootUpdate.LocalTime;
			var l = FeatherTweenRunner.RootLate.LocalTime;
			var f = FeatherTweenRunner.RootFixed.LocalTime;
			var m = FeatherTweenRunner.RootManual.LocalTime;

			FeatherTweenRunner.ManualTick(0.25);

			Assert.That(FeatherTweenRunner.RootUpdate.LocalTime, Is.EqualTo(u));
			Assert.That(FeatherTweenRunner.RootLate.LocalTime, Is.EqualTo(l));
			Assert.That(FeatherTweenRunner.RootFixed.LocalTime, Is.EqualTo(f));
			Assert.That(FeatherTweenRunner.RootManual.LocalTime, Is.EqualTo(m + 0.25));
		}

		// FT.ManualTick is the public spelling (Documentation~/guides/conventions.md);
		// added in 1.16 when doc reconciliation found only the internal runner
		// method existed. It both advances the Manual root and ticks a Manual
		// tween end to end through the public API alone.
		[Test]
		public void FT_ManualTick_DrivesManualPhaseThroughPublicApi()
		{
			var m = FeatherTweenRunner.RootManual.LocalTime;

			var value = 0f;
			var tween = FT.To(() => value, v => value = v, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FT.ManualTick(0.5);

			Assert.That(FeatherTweenRunner.RootManual.LocalTime, Is.EqualTo(m + 0.5));
			Assert.That(value, Is.EqualTo(0.5f).Within(1e-4f));

			FT.ManualTick(0.6);
			Assert.That(value, Is.EqualTo(1f).Within(1e-4f));
			Assert.That(tween.IsAlive, Is.False);
		}

		[Test]
		public void TickEditorDelta_AdvancesUpdateRoot()
		{
			var before = FeatherTweenRunner.RootUpdate.LocalTime;
			FeatherTweenRunner.TickEditorDelta(0.5);
			Assert.That(FeatherTweenRunner.RootUpdate.LocalTime, Is.EqualTo(before + 0.5));
		}

		[Test]
		public void Reset_ResetsAllRootLocalTimes_AndKeepsRootsValid()
		{
			FeatherTweenRunner.ManualTick(1.5);
			FeatherTweenRunner.TickEditorDelta(2.0);
			Assert.That(FeatherTweenRunner.RootManual.LocalTime, Is.GreaterThan(0d));
			Assert.That(FeatherTweenRunner.RootUpdate.LocalTime, Is.GreaterThan(0d));

			FeatherTweenRunner.Reset();

			Assert.That(FeatherTweenRunner.RootUpdate, Is.Not.Null);
			Assert.That(FeatherTweenRunner.RootLate, Is.Not.Null);
			Assert.That(FeatherTweenRunner.RootFixed, Is.Not.Null);
			Assert.That(FeatherTweenRunner.RootManual, Is.Not.Null);
			Assert.That(FeatherTweenRunner.RootUpdate.LocalTime, Is.Zero);
			Assert.That(FeatherTweenRunner.RootManual.LocalTime, Is.Zero);
		}

		[Test]
		public void ManualTick_AccumulatorDrift_BelowOneNanosecondPerSecond_Over60s()
		{
			const double targetSeconds = 60d;
			const double dt = 1d / 60d;
			const int steps = 3600;

			for (var i = 0; i < steps; i++)
			{
				FeatherTweenRunner.ManualTick(dt);
			}

			var actual = FeatherTweenRunner.RootManual.LocalTime;
			var drift = Math.Abs(actual - targetSeconds);
			Assert.That(drift / targetSeconds, Is.LessThan(1e-9),
				$"Drift was {drift} over {targetSeconds}s (allowed: {1e-9 * targetSeconds}s).");
		}

		[Test]
		[Category("RequiresSafeMode")] // the off-thread assert is compiled out under FEATHERTWEEN_RELEASE
		public void OffThreadManualTick_Throws()
		{
			Exception caught = null;
			var task = System.Threading.Tasks.Task.Run(() =>
			{
				try { FeatherTweenRunner.ManualTick(0.1); }
				catch (Exception e) { caught = e; }
			});
			task.Wait();

			Assert.That(caught, Is.Not.Null);
			Assert.That(caught, Is.InstanceOf<InvalidOperationException>());
		}

		// Needs Unity's real PlayerLoop; excluded from the .NET compile-check
		// harness (see tools~/compile-check).
		[Test, Category("RequiresUnity")]
		public void Install_InjectsThreePlayerLoopSubsystems()
		{
			FeatherTweenRunner.Uninstall();
			var before = CountFeatherTweenSubsystems(PlayerLoop.GetCurrentPlayerLoop());
			FeatherTweenRunner.Install();
			var after = CountFeatherTweenSubsystems(PlayerLoop.GetCurrentPlayerLoop());
			FeatherTweenRunner.Uninstall();

			Assert.That(after - before, Is.EqualTo(3));
		}

		private static int CountFeatherTweenSubsystems(PlayerLoopSystem system)
		{
			var count = 0;
			if (system.subSystemList != null)
			{
				foreach (var sub in system.subSystemList)
				{
					if (sub.type != null && sub.type.FullName != null && sub.type.FullName.Contains("FeatherTween"))
					{
						count++;
					}
					count += CountFeatherTweenSubsystems(sub);
				}
			}
			return count;
		}
	}
}
