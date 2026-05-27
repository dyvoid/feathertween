using System;
using NUnit.Framework;
using UnityEngine.LowLevel;
using PATween.Internal;

namespace PATween.Tests
{
	[TestFixture]
	public class PATweenRunnerTests
	{
		[SetUp]
		public void SetUp()
		{
			PATweenRunner.Reset();
		}

		[Test]
		public void ManualTick_AdvancesOnlyManualRoot()
		{
			var u = PATweenRunner.RootUpdate.LocalTime;
			var l = PATweenRunner.RootLate.LocalTime;
			var f = PATweenRunner.RootFixed.LocalTime;
			var m = PATweenRunner.RootManual.LocalTime;

			PATweenRunner.ManualTick(0.25);

			Assert.That(PATweenRunner.RootUpdate.LocalTime, Is.EqualTo(u));
			Assert.That(PATweenRunner.RootLate.LocalTime, Is.EqualTo(l));
			Assert.That(PATweenRunner.RootFixed.LocalTime, Is.EqualTo(f));
			Assert.That(PATweenRunner.RootManual.LocalTime, Is.EqualTo(m + 0.25));
		}

		[Test]
		public void TickEditorDelta_AdvancesUpdateRoot()
		{
			var before = PATweenRunner.RootUpdate.LocalTime;
			PATweenRunner.TickEditorDelta(0.5);
			Assert.That(PATweenRunner.RootUpdate.LocalTime, Is.EqualTo(before + 0.5));
		}

		[Test]
		public void Reset_ResetsAllRootLocalTimes_AndKeepsRootsValid()
		{
			PATweenRunner.ManualTick(1.5);
			PATweenRunner.TickEditorDelta(2.0);
			Assert.That(PATweenRunner.RootManual.LocalTime, Is.GreaterThan(0d));
			Assert.That(PATweenRunner.RootUpdate.LocalTime, Is.GreaterThan(0d));

			PATweenRunner.Reset();

			Assert.That(PATweenRunner.RootUpdate, Is.Not.Null);
			Assert.That(PATweenRunner.RootLate, Is.Not.Null);
			Assert.That(PATweenRunner.RootFixed, Is.Not.Null);
			Assert.That(PATweenRunner.RootManual, Is.Not.Null);
			Assert.That(PATweenRunner.RootUpdate.LocalTime, Is.Zero);
			Assert.That(PATweenRunner.RootManual.LocalTime, Is.Zero);
		}

		[Test]
		public void ManualTick_AccumulatorDrift_BelowOneNanosecondPerSecond_Over60s()
		{
			const double targetSeconds = 60d;
			const double dt = 1d / 60d;
			const int steps = 3600;

			for (var i = 0; i < steps; i++)
			{
				PATweenRunner.ManualTick(dt);
			}

			var actual = PATweenRunner.RootManual.LocalTime;
			var drift = Math.Abs(actual - targetSeconds);
			Assert.That(drift / targetSeconds, Is.LessThan(1e-9),
				$"Drift was {drift} over {targetSeconds}s (allowed: {1e-9 * targetSeconds}s).");
		}

		[Test]
		public void OffThreadManualTick_Throws()
		{
			Exception caught = null;
			var task = System.Threading.Tasks.Task.Run(() =>
			{
				try { PATweenRunner.ManualTick(0.1); }
				catch (Exception e) { caught = e; }
			});
			task.Wait();

			Assert.That(caught, Is.Not.Null);
			Assert.That(caught, Is.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public void Install_InjectsThreePlayerLoopSubsystems()
		{
			PATweenRunner.Uninstall();
			var before = CountPATweenSubsystems(PlayerLoop.GetCurrentPlayerLoop());
			PATweenRunner.Install();
			var after = CountPATweenSubsystems(PlayerLoop.GetCurrentPlayerLoop());
			PATweenRunner.Uninstall();

			Assert.That(after - before, Is.EqualTo(3));
		}

		private static int CountPATweenSubsystems(PlayerLoopSystem system)
		{
			var count = 0;
			if (system.subSystemList != null)
			{
				foreach (var sub in system.subSystemList)
				{
					if (sub.type != null && sub.type.FullName != null && sub.type.FullName.Contains("PATween"))
					{
						count++;
					}
					count += CountPATweenSubsystems(sub);
				}
			}
			return count;
		}
	}
}
