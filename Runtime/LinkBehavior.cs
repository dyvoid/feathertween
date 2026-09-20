namespace dyvoid.FeatherTween
{
	/// <summary>
	/// How a tween or sequence reacts to the active state of the <c>GameObject</c> it was
	/// linked to with <c>SetLink</c>. Every behavior also kills on destruction — a destroyed
	/// object leaves nothing to pause or restart.
	/// </summary>
	public enum LinkBehavior
	{
		/// <summary>Kill when the linked object is destroyed, and nothing else (the default).</summary>
		KillOnDestroy = 0,
		/// <summary>Kill when the linked object becomes inactive in the hierarchy.</summary>
		KillOnDisable = 1,
		/// <summary>Pause when the linked object becomes inactive; stay paused after it is re-enabled.</summary>
		PauseOnDisable = 2,
		/// <summary>Pause when the linked object becomes inactive, resume where it stopped when it is re-enabled.</summary>
		PauseOnDisableResumeOnEnable = 3,
		/// <summary>Pause when the linked object becomes inactive, restart from the beginning when it is re-enabled.</summary>
		RestartOnEnable = 4,
	}
}
