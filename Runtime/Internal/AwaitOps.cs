using System;

namespace dyvoid.FeatherTween.Internal
{
	// Shared await/coroutine plumbing behind the Tween and Sequence handles, so
	// the two never drift. Mirrors TweenOps, which does the same for the control
	// surface.
	internal static class AwaitOps
	{
		public static bool IsTerminal(TweenStatus status)
		{
			return status == TweenStatus.Completed
				|| status == TweenStatus.Cancelled
				|| status == TweenStatus.Disposed;
		}

		public static bool IsFinished(int id, uint generation)
		{
			var data = TweenStore.Get(id, generation);
			return data == null || IsTerminal(data.Status);
		}

		// Registers a resume that fires exactly once, whichever way the animation
		// ends. OnComplete covers a natural end (which a SetAutoKill(false) record
		// survives, so no free follows); the disposal hook covers every death,
		// including the ones no user-facing callback reaches - a safe-mode setter
		// exception without CancelOnError sets Cancelled and frees the record
		// without firing OnKill at all (Documentation~/api/handles.md firing matrix).
		// Registering on OnKill instead of the disposal hook is what left an await
		// parked forever in that case.
		//
		// Auto-kill fires both: OnComplete, then the free. A tween that completes,
		// is Restart()ed and completes again fires OnComplete twice. OneShotSignal
		// collapses either into one resume, because resuming an async state machine
		// twice throws.
		public static bool TryRegisterResume(int id, uint generation, Action resume)
		{
			var data = TweenStore.Get(id, generation);
			if (data == null || IsTerminal(data.Status))
			{
				return false;
			}

			var signal = new OneShotSignal(resume);
			data.AddOnComplete(signal.Fire);
			data.AddOnDisposed(signal.Fire);
			return true;
		}

		// One AwaitableCompletionSource per call. Unity pools the Awaitable it
		// hands out, but the source itself is ours; this is a per-await cost on
		// an explicitly asynchronous operation, not per-tick work, so it does not
		// touch the steady-state zero-alloc budget (Documentation~/architecture/performance.md).
		public static UnityEngine.Awaitable WaitForEnd(int id, uint generation)
		{
			var source = new UnityEngine.AwaitableCompletionSource();
			if (!TryRegisterResume(id, generation, source.SetResult))
			{
				// Already over: complete now rather than wait for a callback that
				// will never fire.
				source.SetResult();
			}
			return source.Awaitable;
		}

		// Polled by CustomYieldInstruction once per frame. Deliberately poll-based
		// rather than callback-based: Unity owns the instruction across frames, and
		// a poll cannot leave a coroutine parked if the tween dies in a way no
		// callback covers.
		public static bool KeepWaiting(int id, uint generation)
		{
			return !IsFinished(id, generation);
		}
	}
}
