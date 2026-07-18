namespace dyvoid.FeatherTween
{
	/// <summary>How a sequence reacts when one of its children is auto-killed (e.g. its target was destroyed).</summary>
	public enum SequenceCancelBehavior
	{
		/// <summary>The sequence keeps playing; the dead child's window becomes a no-op (the default).</summary>
		ContinueOnChildAutoKill = 0,
		/// <summary>The whole sequence is killed when any child is auto-killed.</summary>
		KillSequenceOnChildAutoKill = 1,
	}
}
