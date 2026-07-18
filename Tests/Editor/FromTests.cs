using NUnit.Framework;
using dyvoid.FeatherTween;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween.Tests
{
	[TestFixture]
	public class FromTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void RootFrom_SnapsPropertySynchronouslyInsideStart()
		{
			var v = 5f;
			FT.From(() => v, x => v = x, 0f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.That(v, Is.EqualTo(0f).Within(1e-6f),
				"From should snap setter(fromValue) at Start, before any tick.");
		}

		[Test]
		public void RootFrom_AnimatesFromSuppliedValue_ToCurrentValueAtStart()
		{
			var v = 10f;
			FT.From(() => v, x => v = x, 0f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.That(v, Is.EqualTo(0f).Within(1e-6f));

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(5f).Within(1e-3f), "Halfway: midpoint between 0 and original 10.");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(10f).Within(1e-3f));
		}

		[Test]
		public void ToThenFrom_BehavesAsFrom()
		{
			var v = 3f;
			FT.To(() => v, x => v = x, 9f, 1f)
				.From()
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.That(v, Is.EqualTo(9f).Within(1e-6f), "From should snap to supplied To value.");

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(3f).Within(1e-3f), "Animates back to property's original value (3).");
		}

		[Test]
		public void FromTo_InvokesSetterWithFromAtSnapTime()
		{
			var v = 42f;
			FT.FromTo(x => v = x, -1f, 100f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.That(v, Is.EqualTo(-1f).Within(1e-6f));

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(49.5f).Within(1e-3f));

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(100f).Within(1e-3f));
		}

		[Test]
		public void From_GetterRead_AtSnapTime_NotEarlier()
		{
			var v = 1f;
			var calls = 0;
			var builder = FT.From(
				() => { calls++; return v; },
				x => v = x,
				0f,
				1f)
				.SetUpdate(UpdatePhase.Manual);

			Assert.That(calls, Is.Zero, "Getter must not be called during builder construction.");

			builder.Start();
			Assert.That(calls, Is.EqualTo(1), "Getter is called exactly once at Start (snap time).");
		}

		[Test]
		public void From_GetterRead_AfterModification_CapturesPostModificationValue()
		{
			var v = 1f;
			var builder = FT.From(() => v, x => v = x, 0f, 1f)
				.SetUpdate(UpdatePhase.Manual);

			v = 50f;
			builder.Start();

			Assert.That(v, Is.EqualTo(0f).Within(1e-6f), "Should snap to fromValue.");

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(50f).Within(1e-3f), "Should animate to the post-modification getter reading.");
		}

		[Test]
		public void FromTo_NullSetter_Throws()
		{
			Assert.Throws<System.ArgumentNullException>(
				() => FT.FromTo<float>(null, 0f, 1f, 1f));
		}

		[Test]
		public void BuilderFromValue_PlaysFromValueToCreationEnd()
		{
			var v = 5f;
			FT.To(() => v, x => v = x, 10f, 1f)
				.From(0f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.That(v, Is.EqualTo(0f).Within(1e-6f),
				"From(value) snaps setter(value) at Start, like FromTo.");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(5f).Within(1e-3f), "midpoint between explicit 0 and creation end 10");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(10f).Within(1e-3f));
		}

		[Test]
		public void BuilderFromValue_GetterNeverRead()
		{
			var v = 0f;
			var calls = 0;
			FT.To(() => { calls++; return v; }, x => v = x, 1f, 1f)
				.From(0.5f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(1.0);
			Assert.That(calls, Is.Zero, "explicit endpoints: the getter must never be sampled");
			Assert.That(v, Is.EqualTo(1f).Within(1e-3f));
		}
	}
}
