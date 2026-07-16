namespace Dyvoid.FeatherTween
{
	/// <summary>The PlayerLoop phase that drives a tween or sequence.</summary>
	public enum UpdatePhase
	{
		/// <summary>Ticks after script <c>Update</c> (the default).</summary>
		Update,
		/// <summary>Ticks after script <c>LateUpdate</c>.</summary>
		Late,
		/// <summary>Ticks after script <c>FixedUpdate</c>, using the fixed delta.</summary>
		Fixed,
		/// <summary>Ticks only when <see cref="FT.ManualTick"/> is called.</summary>
		Manual,
	}
}
