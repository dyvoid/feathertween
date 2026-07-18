namespace dyvoid.FeatherTween
{
	/// <summary>When a <c>SetDelay</c> delay applies relative to loop cycles.</summary>
	public enum DelayType
	{
		/// <summary>The delay runs once, before the first cycle only (the default).</summary>
		FirstLoop,
		/// <summary>The delay runs again before every cycle.</summary>
		EveryLoop,
	}
}
