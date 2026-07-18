using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using dyvoid.FeatherTween;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween.Tests
{
	// Phase 1.13 — safe mode wraps setter and callback invocations in
	// try/catch. Semantics under test (Documentation~/architecture/overview.md):
	// setter exception kills the tween (CancelOnError: silent + OnKill;
	// otherwise logged, no OnKill); callback exception is logged and the tween
	// continues (CancelOnError: also cancelled, deferred). Every test sets
	// SetSafeMode explicitly because the default differs between Editor and
	// player builds.
	[TestFixture]
	[Category("RequiresSafeMode")] // excluded from the FEATHERTWEEN_RELEASE CI leg: the wrapper is compiled out there
	public class SafeModeTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void SetterThrows_CancelOnError_KillsTweenAndFiresOnKill()
		{
			var killed = false;
			var t = FT.To(
					() => 0f,
					_ => throw new InvalidOperationException("boom"),
					1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(true)
				.SetCancelOnError(true)
				.OnKill(() => killed = true)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);

			Assert.That(killed, Is.True);
			Assert.That(t.IsAlive, Is.False);
		}

		[Test]
		public void SetterThrows_NoCancelOnError_LogsAndKillsWithoutOnKill()
		{
			LogAssert.Expect(LogType.Exception, new Regex("boom"));

			var killed = false;
			var t = FT.To(
					() => 0f,
					_ => throw new InvalidOperationException("boom"),
					1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(true)
				.SetCancelOnError(false)
				.OnKill(() => killed = true)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);

			Assert.That(killed, Is.False);
			Assert.That(t.IsAlive, Is.False);
		}

		[Test]
		public void SetterThrows_SafeModeOff_Propagates()
		{
			FT.To(
					() => 0f,
					_ => throw new InvalidOperationException("boom"),
					1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(false)
				.Start();

			Assert.Throws<InvalidOperationException>(() => FeatherTweenRunner.ManualTick(0.5));
		}

		[Test]
		public void SetterThrows_OtherTweensInTickSurvive()
		{
			var other = 0f;
			FT.To(
					() => 0f,
					_ => throw new InvalidOperationException("boom"),
					1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(true)
				.SetCancelOnError(true)
				.Start();
			FT.To(() => other, v => other = v, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);

			Assert.That(other, Is.EqualTo(0.5f).Within(1e-4f));
		}

		[Test]
		public void CallbackThrows_SafeMode_LogsAndTweenContinues()
		{
			LogAssert.Expect(LogType.Exception, new Regex("cb-boom"));
			LogAssert.Expect(LogType.Exception, new Regex("cb-boom"));

			var v = 0f;
			var laterCallbackRan = false;
			var t = FT.To(() => v, x => v = x, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(true)
				.SetCancelOnError(false)
				.OnUpdate(_ => throw new InvalidOperationException("cb-boom"))
				.OnUpdate(_ => laterCallbackRan = true)
				.Start();

			FeatherTweenRunner.ManualTick(0.25);
			FeatherTweenRunner.ManualTick(0.25);

			Assert.That(t.IsAlive, Is.True);
			Assert.That(v, Is.EqualTo(0.5f).Within(1e-4f));
			Assert.That(laterCallbackRan, Is.True, "callbacks after the throwing one must still run");
		}

		[Test]
		public void CallbackThrows_CancelOnError_CancelsTweenAndFiresOnKill()
		{
			LogAssert.Expect(LogType.Exception, new Regex("cb-boom"));

			var killed = false;
			var t = FT.To(() => 0f, _ => { }, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(true)
				.SetCancelOnError(true)
				.OnStart(() => throw new InvalidOperationException("cb-boom"))
				.OnKill(() => killed = true)
				.Start();

			FeatherTweenRunner.ManualTick(0.1);

			Assert.That(killed, Is.True);
			Assert.That(t.IsAlive, Is.False);
		}

		[Test]
		public void FromSnapSetterThrows_AtStart_ReturnsDeadHandle()
		{
			var t = FT.FromTo<float>(
					_ => throw new InvalidOperationException("snap-boom"),
					0f, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(true)
				.SetCancelOnError(true)
				.Start();

			Assert.That(t.IsAlive, Is.False);
		}

		[Test]
		public void Complete_SetterThrows_CancelOnError_FiresOnKillNotOnComplete()
		{
			var killed = false;
			var completed = false;
			var t = FT.To(
					() => 0f,
					_ => throw new InvalidOperationException("boom"),
					1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(true)
				.SetCancelOnError(true)
				.OnComplete(() => completed = true)
				.OnKill(() => killed = true)
				.Start();

			t.Complete();

			Assert.That(killed, Is.True);
			Assert.That(completed, Is.False);
			Assert.That(t.IsAlive, Is.False);
		}

		[Test]
		public void SequenceCallbackThrows_CancelOnError_KillsSequence()
		{
			LogAssert.Expect(LogType.Exception, new Regex("seq-boom"));

			var killed = false;
			var v = 0f;
			var s = FT.Sequence()
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(true)
				.SetCancelOnError(true)
				.Append(FT.To(() => v, x => v = x, 1f, 1f))
				.AppendCallback(() => throw new InvalidOperationException("seq-boom"))
				.OnKill(() => killed = true)
				.Start();

			FeatherTweenRunner.ManualTick(1.5);

			Assert.That(killed, Is.True);
			Assert.That(s.IsAlive, Is.False);
		}

		[Test]
		public void SequenceChildSetterThrows_ChildDies_SequenceCompletes()
		{
			var completed = false;
			var v = 0f;
			var s = FT.Sequence()
				.SetUpdate(UpdatePhase.Manual)
				.Append(FT.To(
						() => 0f,
						_ => throw new InvalidOperationException("child-boom"),
						1f, 1f)
					.SetSafeMode(true)
					.SetCancelOnError(true))
				.Append(FT.To(() => v, x => v = x, 1f, 1f))
				.OnComplete(() => completed = true)
				.Start();

			FeatherTweenRunner.ManualTick(2.5);

			Assert.That(completed, Is.True);
			Assert.That(s.IsAlive, Is.False);
			Assert.That(v, Is.EqualTo(1f).Within(1e-4f));
		}

		[Test]
		public void OffThreadStart_Throws()
		{
			Exception caught = null;
			var task = Task.Run(() =>
			{
				try
				{
					FT.To(() => 0f, _ => { }, 1f, 1f).Start();
				}
				catch (Exception e)
				{
					caught = e;
				}
			});
			task.Wait();

			Assert.That(caught, Is.InstanceOf<InvalidOperationException>());
		}
	}
}
