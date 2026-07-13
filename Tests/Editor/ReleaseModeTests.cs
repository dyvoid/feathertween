// Only compiled in the FEATHERTWEEN_RELEASE CI leg. Behavioral proof that the
// safe-mode wrapper is compiled out: with the wrapper gone, SetSafeMode(true)
// is inert and a throwing setter propagates out of the tick.
#if FEATHERTWEEN_RELEASE
using System;
using NUnit.Framework;
using Dyvoid.FeatherTween;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween.Tests
{
	[TestFixture]
	public class ReleaseModeTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void Release_SetterThrows_PropagatesDespiteSafeMode()
		{
			global::Dyvoid.FeatherTween.FT.To(
					() => 0f,
					_ => throw new InvalidOperationException("boom"),
					1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(true)
				.SetCancelOnError(true)
				.Start();

			Assert.Throws<InvalidOperationException>(() => FeatherTweenRunner.ManualTick(0.5));
		}
	}
}
#endif
