namespace dyvoid.FeatherTween
{
	/// <summary>Lifecycle state of a tween or sequence.</summary>
	public enum TweenStatus
	{
		/// <summary>Started, but the initial delay has not elapsed yet.</summary>
		Delayed,
		/// <summary>Actively advancing and writing values.</summary>
		Playing,
		/// <summary>Halted by <c>Pause()</c>; resumes from the same playhead.</summary>
		Paused,
		/// <summary>Reached the end of its final cycle (end values applied).</summary>
		Completed,
		/// <summary>Stopped early by <c>Kill()</c> or a safe-mode error.</summary>
		Cancelled,
		/// <summary>The handle no longer refers to a live record (recycled slot or consumed builder).</summary>
		Disposed,
	}
}
