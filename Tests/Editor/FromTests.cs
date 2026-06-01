using NUnit.Framework;
using PATween;
using PATween.Internal;

namespace PATween.Tests
{
	[TestFixture]
	public class FromTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			PATweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void RootFrom_SnapsPropertySynchronouslyInsideStart()
		{
			var v = 5f;
			global::PATween.PATween.From(() => v, x => v = x, 0f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.That(v, Is.EqualTo(0f).Within(1e-6f),
				"From should snap setter(fromValue) at Start, before any tick.");
		}

		[Test]
		public void RootFrom_AnimatesFromSuppliedValue_ToCurrentValueAtStart()
		{
			var v = 10f;
			global::PATween.PATween.From(() => v, x => v = x, 0f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.That(v, Is.EqualTo(0f).Within(1e-6f));

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(5f).Within(1e-3f), "Halfway: midpoint between 0 and original 10.");

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(10f).Within(1e-3f));
		}

		[Test]
		public void ToThenFrom_BehavesAsFrom()
		{
			var v = 3f;
			global::PATween.PATween.To(() => v, x => v = x, 9f, 1f)
				.From()
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.That(v, Is.EqualTo(9f).Within(1e-6f), "From should snap to supplied To value.");

			PATweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(3f).Within(1e-3f), "Animates back to property's original value (3).");
		}

		[Test]
		public void FromTo_InvokesSetterWithFromAtSnapTime()
		{
			var v = 42f;
			global::PATween.PATween.FromTo(() => v, x => v = x, -1f, 100f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			Assert.That(v, Is.EqualTo(-1f).Within(1e-6f));

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(49.5f).Within(1e-3f));

			PATweenRunner.ManualTick(0.5);
			Assert.That(v, Is.EqualTo(100f).Within(1e-3f));
		}

		[Test]
		public void From_GetterRead_AtSnapTime_NotEarlier()
		{
			var v = 1f;
			var calls = 0;
			var builder = global::PATween.PATween.From(
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
			var builder = global::PATween.PATween.From(() => v, x => v = x, 0f, 1f)
				.SetUpdate(UpdatePhase.Manual);

			v = 50f;
			builder.Start();

			Assert.That(v, Is.EqualTo(0f).Within(1e-6f), "Should snap to fromValue.");

			PATweenRunner.ManualTick(1.0);
			Assert.That(v, Is.EqualTo(50f).Within(1e-3f), "Should animate to the post-modification getter reading.");
		}

		[Test]
		public void FromTo_NullGetter_Allowed()
		{
			var v = 0f;
			Assert.DoesNotThrow(() =>
			{
				global::PATween.PATween.FromTo<float>(null, x => v = x, 0f, 1f, 1f)
					.SetUpdate(UpdatePhase.Manual)
					.Start();
			});
		}
	}
}
