// Only compiled in the PATWEEN_RELEASE CI leg. Behavioral proof that the
// safe-mode wrapper is compiled out: with the wrapper gone, SetSafeMode(true)
// is inert and a throwing setter propagates out of the tick.
#if PATWEEN_RELEASE
using System;
using NUnit.Framework;
using PATween;
using PATween.Internal;

namespace PATween.Tests
{
	[TestFixture]
	public class ReleaseModeTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			PATweenRunner.Reset();
			Interpolators.Reset();
		}

		[Test]
		public void Release_SetterThrows_PropagatesDespiteSafeMode()
		{
			global::PATween.PATween.To(
					() => 0f,
					_ => throw new InvalidOperationException("boom"),
					1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.SetSafeMode(true)
				.SetCancelOnError(true)
				.Start();

			Assert.Throws<InvalidOperationException>(() => PATweenRunner.ManualTick(0.5));
		}
	}
}
#endif
