using System;

namespace dyvoid.FeatherTween.Internal
{
	// Fires one Action at most once, however many callback lists it is
	// registered on. An awaiter registers on OnComplete and on the disposal hook,
	// and two real paths fire both:
	//
	//   - auto-kill: OnComplete fires, then TweenStore.Free fires disposal;
	//   - a SetAutoKill(false) tween that completes, is Restart()ed (or replayed
	//     by LinkBehavior.RestartOnEnable) and completes again fires OnComplete
	//     twice on the same list.
	//
	// Resuming an async state machine twice throws, so the guard is load-bearing
	// rather than defensive. (An earlier comment here claimed the case was
	// "completes then is killed later" — it is not: TweenOps.Kill short-circuits
	// on an already-completed record and fires nothing.)
	internal sealed class OneShotSignal
	{
		private Action action;

		public OneShotSignal(Action action)
		{
			this.action = action;
		}

		public void Fire()
		{
			var pending = action;
			if (pending == null)
			{
				return;
			}
			action = null;
			pending();
		}
	}
}
