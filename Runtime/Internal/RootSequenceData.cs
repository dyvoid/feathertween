namespace Dyvoid.FeatherTween.Internal
{
	internal sealed class RootSequenceData
	{
		private readonly UpdatePhase phase;
		private double localTime;
		private double localTimeUnscaled;

		public UpdatePhase Phase => phase;
		public double LocalTime => localTime;
		public double LocalTimeUnscaled => localTimeUnscaled;

		public RootSequenceData(UpdatePhase phase)
		{
			this.phase = phase;
		}

		public void Advance(double scaledDelta, double unscaledDelta)
		{
			localTime += scaledDelta;
			localTimeUnscaled += unscaledDelta;
		}

		public void Reset()
		{
			localTime = 0d;
			localTimeUnscaled = 0d;
		}
	}
}
