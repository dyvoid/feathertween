namespace dyvoid.FeatherTween.Internal
{
	internal enum SequenceChildKind : byte
	{
		Tween = 0,
		Callback = 1,
		Pause = 2,
	}

	internal struct SequenceChildEntry
	{
		public int Id;
		public uint Gen;
		public double Start;
		public double Length;
		public bool Infinite;
		public SequenceChildKind Kind;
		public int CallbackIndex;
		public int Order;

		// Runtime state, reset by SequenceData.ResetPlayhead.
		public bool Entered;
		public bool Finished;

		public double End => Start + Length;
	}
}
