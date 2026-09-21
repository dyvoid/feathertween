using System;

namespace dyvoid.FeatherTween.Internal
{
	// Fires one Action at most once, however many callback lists it is
	// registered on. An awaiter registers on both OnComplete and OnKill so that
	// it resumes whichever way the tween ends; the firing matrix
	// (Documentation~/api/handles.md) makes those mutually exclusive for a single lifecycle, but a
	// SetAutoKill(false) tween that completes and is killed later would otherwise
	// resume the same continuation twice — which throws inside an async state
	// machine. This guard is the difference.
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
