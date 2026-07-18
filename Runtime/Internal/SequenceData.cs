using System;
using System.Collections.Generic;

namespace dyvoid.FeatherTween.Internal
{
	internal sealed class SequenceData : TweenData
	{
		private SequenceChildEntry[] entries;
		private readonly List<Action> callbacks;
		private double duration;
		private readonly float delay;
		private readonly SequenceCancelBehavior cancelBehavior;
		private int loopCount; // mutable: SetRemainingCyclesAbsolute rewrites it
		private readonly LoopType loopType;

		// Post-delay position spanning all cycles: [0, duration * loopCount].
		private double playheadTotal;
		private double delayRemaining;
		private int orderCounter;

		// SetRemainingCycles(bool): stop at the next cycle boundary in the
		// travel direction (mirrors TweenData<T>).
		private bool stopAtNextBoundary;
		private bool stopAtStartBoundary;

		public override double CycleDuration => duration;

		// Progress across all loops [0,1]; an infinite loop reports progress
		// within its current cycle.
		public override float TotalProgress
		{
			get
			{
				var total = loopCount < 0 ? duration : TotalDuration;
				if (total <= 0d)
				{
					return 1f;
				}
				var p = loopCount < 0
					? (playheadTotal % duration) / duration
					: playheadTotal / total;
				return (float)(p > 1d ? 1d : (p < 0d ? 0d : p));
			}
		}

		private double TotalDuration => loopCount < 0 ? double.PositiveInfinity : duration * loopCount;

		public SequenceData(
			SequenceChildEntry[] entries,
			List<Action> callbacks,
			double duration,
			float delay,
			SequenceCancelBehavior cancelBehavior,
			int loopCount = 1,
			LoopType loopType = LoopType.Restart)
		{
			this.entries = entries;
			this.callbacks = callbacks;
			this.duration = duration;
			this.delay = delay;
			this.cancelBehavior = cancelBehavior;
			this.loopCount = loopCount == 0 ? 1 : loopCount;
			// Incremental has no sequence-level meaning (children re-snap from
			// current values on each cycle, so relative children shift naturally);
			// treat it as Restart.
			this.loopType = loopType == LoopType.Yoyo ? LoopType.Yoyo : LoopType.Restart;
			delayRemaining = delay;
			for (var i = 0; i < entries.Length; i++)
			{
				if (entries[i].Order >= orderCounter)
				{
					orderCounter = entries[i].Order + 1;
				}
			}
		}

		public override bool StartsDelayed() => delay > 0f;

		public override void SetRemainingCyclesAbsolute(int cycles)
		{
			if (cycles < 0)
			{
				loopCount = -1;
				return;
			}
			// Mirrors TweenData<T>: the in-progress cycle counts as the first
			// of the remaining ones, so 0 clamps to 1 (loopCount 0 would never
			// satisfy any completion check).
			loopCount = CurrentCycle(forward: true) + (cycles == 0 ? 1 : cycles);
		}

		public override void SetStopAtNextBoundary(bool stopAtEndValue)
		{
			stopAtNextBoundary = stopAtEndValue;
			stopAtStartBoundary = !stopAtEndValue;
		}

		public override void Step(double scaledDelta, double unscaledDelta)
		{
			if (Status != TweenStatus.Playing && Status != TweenStatus.Delayed)
			{
				return;
			}

			var dt = (IgnoreTimeScale ? unscaledDelta : scaledDelta) * TimeScale * Direction;

			if (delayRemaining > 0d)
			{
				if (dt < 0d)
				{
					// Reversed inside the delay: the delay is part of the timeline
					// (phase 1.15), so it counts back toward playhead 0 (a fully
					// restored delay) instead of stalling.
					delayRemaining -= dt;
					if (delayRemaining > delay)
					{
						delayRemaining = delay;
					}
					Status = TweenStatus.Delayed;
					return;
				}
				if (dt <= delayRemaining)
				{
					delayRemaining -= dt;
					Status = TweenStatus.Delayed;
					return;
				}
				dt -= delayRemaining;
				delayRemaining = 0d;
			}

			Status = TweenStatus.Playing;
			FireStartIfPending();

			var target = playheadTotal + dt;

			// SetRemainingCycles(bool): clamp the walk at the next cycle
			// boundary in the travel direction and complete there.
			var stoppedAtBoundary = false;
			var stoppedForward = false;
			if (duration > 0d)
			{
				if (dt > 0d && stopAtNextBoundary)
				{
					var boundary = (CurrentCycle(forward: true) + 1) * duration;
					if (target >= boundary)
					{
						target = boundary;
						stoppedAtBoundary = true;
						stoppedForward = true;
					}
				}
				else if (dt < 0d && stopAtStartBoundary)
				{
					var boundary = CurrentCycle(forward: false) * duration;
					if (target <= boundary)
					{
						target = boundary;
						stoppedAtBoundary = true;
					}
				}
			}

			// Reversed past playhead 0: the walk clamps there and the overshoot
			// re-enters the initial delay, counting it back up (phase 1.15).
			if (target < 0d && delay > 0f)
			{
				delayRemaining = Math.Min(delay, -target);
			}

			if (!AdvanceTo(target, fire: true, haltOnPause: true, out var paused))
			{
				return; // sequence killed itself mid-walk
			}

			InvokeOnUpdate(CycleProgress());

			if (delayRemaining > 0d)
			{
				Status = TweenStatus.Delayed;
				return;
			}

			if (paused)
			{
				Status = TweenStatus.Paused;
				InvokeOnPause();
				return;
			}

			if (stoppedAtBoundary)
			{
				// The clamp targets the nearest boundary, so the walk crossed
				// no intermediate ones; fire this boundary's callback here.
				if (stoppedForward)
				{
					InvokeOnStepComplete();
				}
				else
				{
					InvokeOnRewind();
				}
				Status = TweenStatus.Completed;
				InvokeOnComplete();
				return;
			}

			if (Direction > 0 && loopCount > 0 && playheadTotal >= TotalDuration)
			{
				// The final cycle boundary is a loop end like any other (Documentation~/api/handles.md).
				InvokeOnStepComplete();
				Status = TweenStatus.Completed;
				InvokeOnComplete();
				// No OnKill: natural completion never fires OnKill (Documentation~/api/handles.md).
			}
		}

		// Repositions the playhead in post-delay total time. Preserves Status,
		// except a pause entry crossed while firing halts and pauses (Documentation~/api/handles.md).
		public override void SeekTo(double seconds, bool fireCallbacks)
		{
			delayRemaining = 0d;
			if (!AdvanceTo(seconds, fireCallbacks, haltOnPause: fireCallbacks, out var paused))
			{
				return;
			}
			if (fireCallbacks)
			{
				InvokeOnUpdate(CycleProgress());
			}
			if (paused && Status != TweenStatus.Paused)
			{
				Status = TweenStatus.Paused;
				InvokeOnPause();
			}
		}

		private float CycleProgress()
		{
			if (duration <= 0d)
			{
				return 1f;
			}
			var local = playheadTotal - CurrentCycle(forward: true) * duration;
			var p = local / duration;
			return (float)(p > 1d ? 1d : (p < 0d ? 0d : p));
		}

		// Boundary-aware cycle index for the current playhead. At an exact cycle
		// boundary the forward walker treats it as the start of the next cycle,
		// the backward walker as the end of the previous one.
		private int CurrentCycle(bool forward)
		{
			if (duration <= 0d)
			{
				return 0;
			}
			var c = (int)Math.Floor(playheadTotal / duration);
			if (!forward && playheadTotal == c * duration)
			{
				c--;
			}
			if (c < 0)
			{
				c = 0;
			}
			if (loopCount > 0 && c >= loopCount)
			{
				c = loopCount - 1;
			}
			return c;
		}

		// Walks the playhead to target, cycle by cycle. Yoyo cycles map to
		// backward local walks, so one local walker serves all four
		// direction/loop combinations. Returns false when the sequence killed
		// itself mid-walk; sets paused when a pause entry halted the walk.
		private bool AdvanceTo(double target, bool fire, bool haltOnPause, out bool paused)
		{
			paused = false;
			var total = TotalDuration;
			var requested = target;
			if (target < 0d)
			{
				target = 0d;
			}
			if (target > total)
			{
				target = total;
			}

			if (duration <= 0d)
			{
				playheadTotal = target;
				return true;
			}

			if (playheadTotal == target && requested > target)
			{
				// Forward push against the end: entries sitting exactly at the
				// playhead (e.g. a callback right after a pause at the sequence
				// end) still need a zero-length firing pass.
				var c = CurrentCycle(forward: true);
				if (loopType != LoopType.Yoyo || (c & 1) == 0)
				{
					var local = playheadTotal - c * duration;
					var haltedFlat = double.NaN;
					if (!AdvanceLocalForward(local, local, fire, haltOnPause, ref haltedFlat))
					{
						return false;
					}
					paused = !double.IsNaN(haltedFlat);
				}
				return true;
			}

			var guard = 0;
			while (playheadTotal != target)
			{
				if (++guard > 1_000_000)
				{
					UnityEngine.Debug.LogError("[FeatherTween] Sequence advance walk failed to converge; aborting.");
					break;
				}

				var forward = target > playheadTotal;
				var c = CurrentCycle(forward);
				var cycleStart = c * duration;
				var cycleEnd = cycleStart + duration;
				var segTarget = forward ? Math.Min(target, cycleEnd) : Math.Max(target, cycleStart);

				var reversedCycle = loopType == LoopType.Yoyo && (c & 1) == 1;
				var localFrom = reversedCycle ? cycleEnd - playheadTotal : playheadTotal - cycleStart;
				var localTo = reversedCycle ? cycleEnd - segTarget : segTarget - cycleStart;

				if (!AdvanceLocal(localFrom, localTo, fire, haltOnPause, out var haltedAt))
				{
					return false;
				}
				if (!double.IsNaN(haltedAt))
				{
					playheadTotal = reversedCycle ? cycleEnd - haltedAt : cycleStart + haltedAt;
					paused = true;
					return true;
				}

				playheadTotal = segTarget;
				if (playheadTotal == target)
				{
					break;
				}

				if (forward)
				{
					if (fire)
					{
						InvokeOnStepComplete();
					}
					// Entering the next cycle: Restart replays from armed entries;
					// a Yoyo odd cycle starts from the end state the even cycle
					// just left, and its backward local walk re-arms as it goes.
					if (!(loopType == LoopType.Yoyo))
					{
						RearmEntries();
					}
				}
				else
				{
					if (fire)
					{
						InvokeOnRewind();
					}
					// Entering the previous cycle at its local end. Restart cycles
					// need entries repositioned to their end state; a Yoyo even→odd
					// backward crossing lands at local 0 with entries already
					// re-armed by the walk that got here.
					if (loopType != LoopType.Yoyo)
					{
						SetEntriesToEndState();
					}
				}
			}
			return true;
		}

		// Advances the cycle-local playhead from 'from' to 'to' (either
		// direction). haltedAt is NaN unless a pause entry halted a forward walk.
		// Returns false when a child auto-kill cancelled the whole sequence.
		private bool AdvanceLocal(double from, double to, bool fire, bool haltOnPause, out double haltedAt)
		{
			haltedAt = double.NaN;
			if (to > from)
			{
				return AdvanceLocalForward(from, to, fire, haltOnPause, ref haltedAt);
			}
			if (to < from)
			{
				AdvanceLocalBackward(from, to, fire);
			}
			return true;
		}

		private bool AdvanceLocalForward(double prev, double next, bool fire, bool haltOnPause, ref double haltedAt)
		{
			// An unfired pause clamps the walk; entries are sorted, so the first
			// match is the earliest.
			var pauseIndex = -1;
			if (haltOnPause)
			{
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
							if (fire && !InvokeEntryCallback(e.CallbackIndex))
							{
								return false;
							}
						}
						break;
					case SequenceChildKind.Pause:
						if (i == pauseIndex)
						{
							e.Finished = true;
							if (fire && !InvokeEntryCallback(e.CallbackIndex))
							{
								return false;
							}
						}
						else if (!haltOnPause && e.Start <= next)
						{
							// Force-complete / silent walks blow through pauses.
							e.Finished = true;
							if (fire && !InvokeEntryCallback(e.CallbackIndex))
							{
								return false;
							}
						}
						break;
					case SequenceChildKind.Tween:
						if (!StepChildForward(ref e, prev, next, fire))
						{
							return false;
						}
						break;
				}
			}

			if (pauseIndex >= 0)
			{
				haltedAt = next;
			}
			return true;
		}

		// Returns false when a child auto-kill cancelled the whole sequence.
		private bool StepChildForward(ref SequenceChildEntry e, double prev, double next, bool fire)
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

			if (fire)
			{
				var status = child.Status;
				if (status == TweenStatus.Playing || status == TweenStatus.Delayed)
				{
					child.Step(childDelta, childDelta);
				}
			}
			else
			{
				child.SeekTo(next - e.Start, fireCallbacks: false);
				if (!e.Infinite && next >= e.End)
				{
					child.Status = TweenStatus.Completed;
				}
			}
			if (child.Status == TweenStatus.Completed)
			{
				e.Finished = true;
			}
			return true;
		}

		private void AdvanceLocalBackward(double from, double to, bool fire)
		{
			for (var i = entries.Length - 1; i >= 0; i--)
			{
				ref var e = ref entries[i];
				switch (e.Kind)
				{
					case SequenceChildKind.Callback:
					case SequenceChildKind.Pause:
						// Crossed backward: re-arm so a forward replay fires again.
						// Nothing is invoked on the way back.
						if (e.Start > to && e.Finished)
						{
							e.Finished = false;
						}
						break;
					case SequenceChildKind.Tween:
						StepChildBackward(ref e, to, fire);
						break;
				}
			}
		}

		private void StepChildBackward(ref SequenceChildEntry e, double to, bool fire)
		{
			var child = TweenStore.Get(e.Id, e.Gen);
			if (child == null)
			{
				return;
			}

			if (to <= e.Start)
			{
				// Crossed the child's start: render it at its start value (the
				// walk passes through local 0), then re-arm the snap so a forward
				// replay snaps again (Documentation~/api/handles.md).
				if (e.Entered || e.Finished)
				{
					child.SeekTo(0d, fire);
					e.Entered = false;
					e.Finished = false;
					child.ResetPlayhead();
					child.RearmStartValues();
					child.Status = child.StartsDelayed() ? TweenStatus.Delayed : TweenStatus.Playing;
				}
				return;
			}

			if (!e.Infinite && to >= e.End)
			{
				return; // window entirely before the target; untouched
			}

			// Target lands inside the child's window: reposition it there.
			if (!e.Entered)
			{
				e.Entered = true;
				child.ResolveStartValues();
			}
			e.Finished = false;
			if (child.Status == TweenStatus.Completed)
			{
				child.Status = TweenStatus.Playing;
			}
			child.SeekTo(to - e.Start, fire);
		}

		// Re-arms every entry for a fresh forward pass (loop wrap, Restart).
		private void RearmEntries()
		{
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

		// Positions every entry at its end state (backward crossing into a
		// completed Restart cycle). Silent: boundary callbacks belong to the
		// walker, not to this repositioning.
		private void SetEntriesToEndState()
		{
			for (var i = 0; i < entries.Length; i++)
			{
				ref var e = ref entries[i];
				if (e.Kind != SequenceChildKind.Tween)
				{
					e.Finished = true;
					continue;
				}
				var child = TweenStore.Get(e.Id, e.Gen);
				if (child == null)
				{
					e.Finished = true;
					continue;
				}
				if (!e.Entered)
				{
					e.Entered = true;
					child.ResolveStartValues();
				}
				if (!e.Infinite)
				{
					child.SeekTo(e.Length, fireCallbacks: false);
					child.Status = TweenStatus.Completed;
					e.Finished = true;
				}
			}
		}

		// Returns false when a safe-mode callback error with CancelOnError
		// killed the sequence mid-walk (same unwind contract as child auto-kill).
		private bool InvokeEntryCallback(int index)
		{
			if (index < 0 || callbacks == null || index >= callbacks.Count)
			{
				return true;
			}
#if !FEATHERTWEEN_RELEASE
			if (SafeMode)
			{
				try
				{
					callbacks[index]?.Invoke();
				}
				catch (Exception e)
				{
					UnityEngine.Debug.LogException(e);
					if (CancelOnError)
					{
						Status = TweenStatus.Cancelled;
						InvokeOnKill();
						TweenStore.Free(SelfId);
						return false;
					}
				}
				return true;
			}
#endif
			callbacks[index]?.Invoke();
			return true;
		}

		// Walks the playhead to the end with callbacks (Complete / Kill(true)
		// semantics); pauses are crossed, not halted at. An infinite loop
		// completes its current cycle (Documentation~/api/handles.md).
		public override void ForceComplete()
		{
			delayRemaining = 0d;
			double target;
			if (loopCount > 0)
			{
				target = TotalDuration;
			}
			else
			{
				var c = CurrentCycle(forward: true);
				target = (c + 1) * duration;
			}
			AdvanceTo(target, fire: true, haltOnPause: false, out _);
			// The final cycle boundary is a loop end like any other (Documentation~/api/handles.md);
			// intermediate boundaries fired inside the walk.
			InvokeOnStepComplete();
		}

		public override void ResetPlayhead()
		{
			base.ResetPlayhead();
			playheadTotal = 0d;
			delayRemaining = delay;
			stopAtNextBoundary = false;
			stopAtStartBoundary = false;
			RearmEntries();
		}

		// Mid-play insertion (phase 1.10): rebuilds the sorted entry array.
		// Allocates; mid-play Insert is a structural edit, not a hot-path op.
		public void InsertChild(int id, uint gen, double start, double length, bool infinite)
		{
			var entry = new SequenceChildEntry
			{
				Id = id,
				Gen = gen,
				Start = start,
				Length = infinite ? 0d : length,
				Infinite = infinite,
				Kind = SequenceChildKind.Tween,
				CallbackIndex = -1,
				Order = orderCounter++,
			};

			var newEntries = new SequenceChildEntry[entries.Length + 1];
			var insertAt = entries.Length;
			for (var i = 0; i < entries.Length; i++)
			{
				if (entries[i].Start > start)
				{
					insertAt = i;
					break;
				}
			}
			Array.Copy(entries, 0, newEntries, 0, insertAt);
			newEntries[insertAt] = entry;
			Array.Copy(entries, insertAt, newEntries, insertAt + 1, entries.Length - insertAt);
			entries = newEntries;

			var end = entry.Infinite ? start : entry.End;
			if (end > duration)
			{
				duration = end;
			}

			// If it lands behind the playhead in the current cycle, it plays on
			// the next loop (or on a backward pass); crossing logic handles both.
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
				// disposal, not a kill — no OnKill (Documentation~/api/handles.md). Children cut short by
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
