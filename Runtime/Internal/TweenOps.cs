namespace PATween.Internal
{
	// Shared control-surface cores used by both the Tween and Sequence handles
	// and by the deferred-command queue. Callback firing follows the matrix in
	// api.md §3.14: OnKill fires only on Kill(false), auto-kill, or error —
	// never on completion (natural, Complete(), or Kill(true)).
	internal static class TweenOps
	{
		public static void Play(int id, uint gen)
		{
			var data = TweenStore.Get(id, gen);
			if (data == null)
			{
				return;
			}
			if (data.Status == TweenStatus.Paused)
			{
				data.Status = TweenStatus.Playing;
				data.InvokeOnPlay();
				return;
			}
			if (data.Status == TweenStatus.Completed)
			{
				data.ResetPlayhead();
				data.Status = data.StartsDelayed() ? TweenStatus.Delayed : TweenStatus.Playing;
			}
		}

		public static void Pause(int id, uint gen)
		{
			var data = TweenStore.Get(id, gen);
			if (data == null)
			{
				return;
			}
			if (data.Status == TweenStatus.Playing || data.Status == TweenStatus.Delayed)
			{
				data.Status = TweenStatus.Paused;
				data.InvokeOnPause();
			}
		}

		public static void Resume(int id, uint gen)
		{
			var data = TweenStore.Get(id, gen);
			if (data == null)
			{
				return;
			}
			if (data.Status == TweenStatus.Paused)
			{
				data.Status = TweenStatus.Playing;
				data.InvokeOnPlay();
			}
		}

		public static void Restart(int id, uint gen, bool allowDefer = true)
		{
			if (allowDefer && TweenCommandQueue.TryDefer(TweenCommandQueue.Op.Restart, id, gen))
			{
				return;
			}
			var data = TweenStore.Get(id, gen);
			if (data == null || data.Status == TweenStatus.Disposed)
			{
				return;
			}
			data.ResetPlayhead();
			data.Status = data.StartsDelayed() ? TweenStatus.Delayed : TweenStatus.Playing;
		}

		public static void Reverse(int id, uint gen, bool allowDefer = true)
		{
			if (allowDefer && TweenCommandQueue.TryDefer(TweenCommandQueue.Op.Reverse, id, gen))
			{
				return;
			}
			var data = TweenStore.Get(id, gen);
			if (data == null)
			{
				return;
			}
			data.Direction = -data.Direction;
		}

		public static void Complete(int id, uint gen, bool allowDefer = true)
		{
			if (allowDefer && TweenCommandQueue.TryDefer(TweenCommandQueue.Op.Complete, id, gen))
			{
				return;
			}
			var data = TweenStore.Get(id, gen);
			if (data == null)
			{
				return;
			}
			if (data.Status == TweenStatus.Disposed || data.Status == TweenStatus.Cancelled)
			{
				return;
			}
			var alreadyCompleted = data.Status == TweenStatus.Completed;
			if (!alreadyCompleted)
			{
				data.ForceComplete();
				data.Status = TweenStatus.Completed;
				data.InvokeOnComplete();
			}
			if (data.AutoKill)
			{
				TweenStore.Free(id);
			}
		}

		public static void Kill(int id, uint gen, bool complete, bool allowDefer = true)
		{
			if (allowDefer && TweenCommandQueue.TryDefer(
				complete ? TweenCommandQueue.Op.KillComplete : TweenCommandQueue.Op.Kill, id, gen))
			{
				return;
			}
			var data = TweenStore.Get(id, gen);
			if (data == null)
			{
				return;
			}

			// Kill on an already-Completed tween: transition to Disposed, no
			// callbacks — the tween is at its terminal value (§3.14).
			if (data.Status == TweenStatus.Completed)
			{
				TweenStore.Free(id);
				return;
			}

			if (complete && data.Status != TweenStatus.Cancelled)
			{
				data.ForceComplete();
				data.Status = TweenStatus.Completed;
				data.InvokeOnComplete();
			}
			else
			{
				data.Status = TweenStatus.Cancelled;
				data.InvokeOnKill();
			}
			TweenStore.Free(id);
		}
	}
}
