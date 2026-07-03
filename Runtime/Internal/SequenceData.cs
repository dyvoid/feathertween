using System;
using System.Collections.Generic;

namespace PATween.Internal
{
	internal sealed class SequenceData : TweenData
	{
		private readonly SequenceChildEntry[] entries;
		private readonly List<Action> callbacks;
		private readonly double duration;
		private readonly float delay;
		private readonly SequenceCancelBehavior cancelBehavior;

		private double localTime;
		private double playhead;

		public double Duration => duration;

		public SequenceData(
			SequenceChildEntry[] entries,
			List<Action> callbacks,
			double duration,
			float delay,
			SequenceCancelBehavior cancelBehavior)
		{
			this.entries = entries;
			this.callbacks = callbacks;
			this.duration = duration;
			this.delay = delay;
			this.cancelBehavior = cancelBehavior;
		}

		public override bool StartsDelayed() => delay > 0f;

		public override void Step(double scaledDelta, double unscaledDelta)
		{
			if (Status != TweenStatus.Playing && Status != TweenStatus.Delayed)
			{
				return;
			}

			var dt = IgnoreTimeScale ? unscaledDelta : scaledDelta;
			localTime += dt;

			if (localTime < delay)
			{
				Status = TweenStatus.Delayed;
				return;
			}
			Status = TweenStatus.Playing;
			FireStartIfPending();

			var prev = playhead;
			var next = localTime - delay;
			if (next < prev)
			{
				next = prev;
			}

			// An unfired pause clamps the playhead; entries are sorted, so the
			// first match is the earliest.
			var pauseIndex = -1;
			for (var i = 0; i < entries.Length; i++)
			{
				ref var e = ref entries[i];
				if (e.Kind == SequenceChildKind.Pause && !e.Finished && e.Start <= next)
				{
					next = e.Start;
					pauseIndex = i;
					break;
				}
			}

			// Entries after the pause in (start, order) ordering are held until
			// resume, even at the exact same timestamp.
			var limit = pauseIndex >= 0 ? pauseIndex : entries.Length - 1;
			for (var i = 0; i <= limit; i++)
			{
				ref var e = ref entries[i];
				if (e.Finished)
				{
					continue;
				}

				switch (e.Kind)
				{
					case SequenceChildKind.Callback:
						if (e.Start <= next)
						{
							e.Finished = true;
							InvokeEntryCallback(e.CallbackIndex);
						}
						break;
					case SequenceChildKind.Pause:
						if (i == pauseIndex)
						{
							e.Finished = true;
							InvokeEntryCallback(e.CallbackIndex);
						}
						break;
					case SequenceChildKind.Tween:
						if (!StepChild(ref e, prev, next))
						{
							return; // sequence killed itself mid-step
						}
						break;
				}
			}

			playhead = next;
			localTime = next + delay;

			InvokeOnUpdate(duration > 0d ? (float)(next / duration > 1d ? 1d : next / duration) : 1f);

			if (pauseIndex >= 0)
			{
				Status = TweenStatus.Paused;
				InvokeOnPause();
				return;
			}

			if (next >= duration)
			{
				ForceComplete();
				Status = TweenStatus.Completed;
				InvokeOnComplete();
				// No OnKill: natural completion never fires OnKill (§3.14).
			}
		}

		// Returns false when a child auto-kill cancelled the whole sequence.
		private bool StepChild(ref SequenceChildEntry e, double prev, double next)
		{
			if (next <= e.Start)
			{
				return true;
			}

			var child = TweenStore.Get(e.Id, e.Gen);
			if (child == null)
			{
				e.Finished = true;
				return true;
			}

			if (child.IsUnityObject && (UnityEngine.Object)child.Target == null)
			{
				child.Status = TweenStatus.Cancelled;
				child.InvokeOnKill();
				TweenStore.Free(e.Id);
				e.Finished = true;

				if (cancelBehavior == SequenceCancelBehavior.KillSequenceOnChildAutoKill)
				{
					Status = TweenStatus.Cancelled;
					InvokeOnKill();
					TweenStore.Free(SelfId);
					return false;
				}
				return true;
			}

			if (!e.Entered)
			{
				e.Entered = true;
				child.ResolveStartValues();
			}

			var from = prev > e.Start ? prev : e.Start;
			var childDelta = next - from;
			if (childDelta <= 0d)
			{
				return true;
			}

			var status = child.Status;
			if (status == TweenStatus.Playing || status == TweenStatus.Delayed)
			{
				child.Step(childDelta, childDelta);
			}
			if (child.Status == TweenStatus.Completed)
			{
				e.Finished = true;
			}
			return true;
		}

		private void InvokeEntryCallback(int index)
		{
			if (index < 0 || callbacks == null || index >= callbacks.Count)
			{
				return;
			}
			callbacks[index]?.Invoke();
		}

		public override void ForceComplete()
		{
			for (var i = 0; i < entries.Length; i++)
			{
				ref var e = ref entries[i];
				if (e.Finished)
				{
					continue;
				}

				switch (e.Kind)
				{
					case SequenceChildKind.Callback:
						e.Finished = true;
						InvokeEntryCallback(e.CallbackIndex);
						break;
					case SequenceChildKind.Pause:
						e.Finished = true;
						break;
					case SequenceChildKind.Tween:
					{
						e.Finished = true;
						if (e.Infinite)
						{
							break; // no meaningful end value to snap to
						}
						var child = TweenStore.Get(e.Id, e.Gen);
						if (child == null)
						{
							break;
						}
						if (!e.Entered)
						{
							e.Entered = true;
							child.ResolveStartValues();
						}
						if (child.Status != TweenStatus.Completed
							&& child.Status != TweenStatus.Cancelled)
						{
							child.ForceComplete();
							child.Status = TweenStatus.Completed;
							child.InvokeOnComplete();
						}
						break;
					}
				}
			}

			playhead = duration;
			localTime = duration + delay;
		}

		public override void ResetPlayhead()
		{
			base.ResetPlayhead();
			localTime = 0d;
			playhead = 0d;
			for (var i = 0; i < entries.Length; i++)
			{
				ref var e = ref entries[i];
				if (e.Kind != SequenceChildKind.Tween)
				{
					e.Finished = false;
					continue;
				}
				var child = TweenStore.Get(e.Id, e.Gen);
				if (child == null)
				{
					continue; // cancelled/freed child stays finished
				}
				e.Entered = false;
				e.Finished = false;
				child.ResetPlayhead();
				child.RearmStartValues();
				child.Status = child.StartsDelayed() ? TweenStatus.Delayed : TweenStatus.Playing;
			}
		}

		public override void OnFree()
		{
			for (var i = 0; i < entries.Length; i++)
			{
				ref var e = ref entries[i];
				if (e.Kind != SequenceChildKind.Tween)
				{
					continue;
				}
				var child = TweenStore.Get(e.Id, e.Gen);
				if (child == null)
				{
					continue;
				}
				// Completed children are at their terminal value; freeing them is
				// disposal, not a kill — no OnKill (§3.14). Children cut short by
				// a parent Kill(false) are cancelled and get OnKill.
				if (child.Status != TweenStatus.Completed)
				{
					child.Status = TweenStatus.Cancelled;
					child.InvokeOnKill();
				}
				TweenStore.Free(e.Id);
			}
		}
	}
}
