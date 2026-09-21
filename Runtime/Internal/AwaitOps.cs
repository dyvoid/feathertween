namespace dyvoid.FeatherTween.Internal
{
	// Shared await/coroutine plumbing behind the Tween and Sequence handles, so
	// the two never drift. Mirrors TweenOps, which does the same for the control
	// surface.
	internal static class AwaitOps
	{
		// One AwaitableCompletionSource per call. Unity pools the Awaitable it
		// hands out, but the source itself is ours; this is a per-await cost on
		// an explicitly asynchronous operation, not per-tick work, so it does not
		// touch the steady-state zero-alloc budget (Documentation~/architecture/performance.md).
		public static UnityEngine.Awaitable WaitForEnd(int id, uint generation)
		{
			var source = new UnityEngine.AwaitableCompletionSource();
			var data = TweenStore.Get(id, generation);
			if (data == null || IsTerminal(data.Status))
			{
				// Already over: complete now rather than wait for a callback that
				// will never fire.
				source.SetResult();
				return source.Awaitable;
			}

			var signal = new OneShotSignal(source.SetResult);
			data.AddOnComplete(signal.Fire);
			data.AddOnKill(signal.Fire);
			return source.Awaitable;
		}

		// Polled by CustomYieldInstruction once per frame. Deliberately poll-based
		// rather than callback-based: Unity owns the instruction across frames, and
		// a poll cannot leave a coroutine parked if the tween dies in a way no
		// callback covers.
		public static bool KeepWaiting(int id, uint generation)
		{
			var data = TweenStore.Get(id, generation);
			return data != null && !IsTerminal(data.Status);
		}

		private static bool IsTerminal(TweenStatus status)
		{
			return status == TweenStatus.Completed
				|| status == TweenStatus.Cancelled
				|| status == TweenStatus.Disposed;
		}
	}
}
