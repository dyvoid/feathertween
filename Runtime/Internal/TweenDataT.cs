using System;

namespace Dyvoid.FeatherTween.Internal
{
	internal class TweenData<T> : TweenData
	{
		private Func<T> getter;
		private Action<T> setter;
		private T startValue;
		private T endValue;
		private float duration;
		private double localTime;
		private IInterpolator<T> interpolator;
		private bool relative;
		private EaseRef ease;

		private int loopCount;
		private LoopType loopType;
		private float delay;
		private DelayType delayType;

		private SnapMode snapMode;
		private T fromValue;
		private bool snapPending;

		private int lastCycleIndex;
		private bool stopAtNextBoundary;
		private bool stopAtStartBoundary;

		// Incremental-loop cycle base cache. cycleIndex advances by at most 1 per
		// step, so tracking the last base keeps GetCycleEnds O(1) amortized instead
		// of O(cycleIndex) per frame; -1 means invalid (recompute from startValue).
		private int incrementalCacheIndex = -1;
		private T incrementalCacheBase;

		public Func<T> Getter
		{
			get => getter;
			set => getter = value;
		}

		public Action<T> Setter
		{
			get => setter;
			set => setter = value;
		}

		public T StartValue
		{
			get => startValue;
			set => startValue = value;
		}

		public T EndValue
		{
			get => endValue;
			set => endValue = value;
		}

		public float Duration
		{
			get => duration;
			set => duration = value;
		}

		public double LocalTime
		{
			get => localTime;
			set => localTime = value;
		}

		public IInterpolator<T> Interpolator
		{
			get => interpolator;
			set => interpolator = value;
		}

		public bool Relative
		{
			get => relative;
			set => relative = value;
		}

		public EaseRef Ease
		{
			get => ease;
			set => ease = value;
		}

		public int LoopCount
		{
			get => loopCount;
			set => loopCount = value;
		}

		public LoopType LoopType
		{
			get => loopType;
			set => loopType = value;
		}

		public float Delay
		{
			get => delay;
			set => delay = value;
		}

		public DelayType DelayType
		{
			get => delayType;
			set => delayType = value;
		}

		public SnapMode SnapMode
		{
			get => snapMode;
			set => snapMode = value;
		}

		public T FromValue
		{
			get => fromValue;
			set => fromValue = value;
		}

		public bool SnapPending
		{
			get => snapPending;
			set => snapPending = value;
		}

		public override double CycleDuration => duration;

		public override float TotalProgress
		{
			get
			{
				if (duration <= 0f)
				{
					return 1f;
				}
				// Same slot math as Step: EveryLoop delays live inside the
				// cycle slot, a FirstLoop delay sits before cycle 0.
				var everyLoop = delayType == DelayType.EveryLoop && delay > 0f;
				double cycleSlot = everyLoop ? (delay + (double)duration) : duration;
				double firstDelayOffset = everyLoop ? 0d : delay;
				var active = localTime - firstDelayOffset;
				if (active < 0d)
				{
					active = 0d;
				}
				var p = loopCount < 0
					? (active % cycleSlot) / cycleSlot
					: active / (cycleSlot * loopCount);
				return (float)(p > 1d ? 1d : (p < 0d ? 0d : p));
			}
		}

		// All value writes funnel through here. Safe mode wraps the setter in a
		// try/catch; a throw kills the tween (CancelFromError) and the caller
		// must stop touching it. Returns false when the tween killed itself.
		// FEATHERTWEEN_RELEASE compiles the wrapper out to a bare call.
		private bool ApplySetter(T value)
		{
#if !FEATHERTWEEN_RELEASE
			if (SafeMode)
			{
				try
				{
					setter(value);
					return true;
				}
				catch (Exception e)
				{
					CancelFromError(e);
					return false;
				}
			}
#endif
			setter(value);
			return true;
		}

		public override void ResolveStartValues()
		{
			if (!snapPending)
			{
				return;
			}
			snapPending = false;
			incrementalCacheIndex = -1;

			// fromValue holds the pristine user-supplied value (the relative delta
			// for None, the 'from' argument otherwise) so re-resolving after a
			// Restart re-arm cannot compound mutated state.
			switch (snapMode)
			{
				case SnapMode.None:
					startValue = getter != null ? getter() : default;
					if (relative)
					{
						endValue = interpolator.Add(startValue, fromValue);
					}
					break;
				case SnapMode.From:
					startValue = fromValue;
					endValue = getter != null ? getter() : default;
					if (setter != null && !ApplySetter(startValue))
					{
						return;
					}
					break;
				case SnapMode.FromTo:
					startValue = fromValue;
					if (setter != null && !ApplySetter(startValue))
					{
						return;
					}
					break;
			}
		}

		public override void RearmStartValues()
		{
			snapPending = true;
		}

		public override void SetRemainingCyclesAbsolute(int cycles)
		{
			if (cycles < 0)
			{
				loopCount = -1;
			}
			else
			{
				// The in-progress cycle counts as the first remaining one, so 0
				// clamps to 1; a loopCount of 0 would never satisfy any completion
				// check and loop forever.
				loopCount = lastCycleIndex + (cycles == 0 ? 1 : cycles);
			}
		}

		public override void SetStopAtNextBoundary(bool stopAtEndValue)
		{
			stopAtNextBoundary = stopAtEndValue;
			stopAtStartBoundary = !stopAtEndValue;
		}

		public override void ResetPlayhead()
		{
			base.ResetPlayhead();
			localTime = 0d;
			lastCycleIndex = 0;
			stopAtNextBoundary = false;
			stopAtStartBoundary = false;
			incrementalCacheIndex = -1;
			Direction = 1;
		}

		public override bool StartsDelayed()
		{
			return delay > 0f && delayType == DelayType.FirstLoop;
		}

		public override void ForceComplete()
		{
			// Sync the playhead to the completed position so a later Seek on a
			// non-autokill tween starts from where Complete() left it (phase 1.15).
			var everyLoop = delayType == DelayType.EveryLoop && delay > 0f;
			double cycleSlot = everyLoop ? (delay + (double)duration) : duration;
			double firstDelayOffset = everyLoop ? 0d : delay;
			var completedCycles = loopCount > 0 ? loopCount : lastCycleIndex + 1;
			localTime = firstDelayOffset + cycleSlot * completedCycles;

			var finalIndex = loopCount > 0 ? loopCount - 1 : lastCycleIndex;
			if (setter != null)
			{
				GetCycleEnds(finalIndex, out _, out var cycleTo);
				if (!ApplySetter(cycleTo))
				{
					return;
				}
			}

			// Complete()/Kill(true) fire OnStepComplete for the remaining loop
			// boundaries; an infinite loop completes its current iteration (Documentation~/api/handles.md).
			var remaining = loopCount < 0 ? 1 : loopCount - lastCycleIndex;
			for (var i = 0; i < remaining; i++)
			{
				InvokeOnStepComplete();
			}
			if (loopCount > 0)
			{
				lastCycleIndex = loopCount;
			}
			else if (loopCount < 0)
			{
				lastCycleIndex++;
			}
		}

		public override void Step(double scaledDelta, double unscaledDelta)
		{
			if (Status != TweenStatus.Playing && Status != TweenStatus.Delayed)
			{
				return;
			}

			var dir = Direction;
			var dt = (IgnoreTimeScale ? unscaledDelta : scaledDelta) * TimeScale;

			var everyLoop = delayType == DelayType.EveryLoop && delay > 0f;
			// Double addition, matching ForceComplete/TotalProgress: a float-summed
			// slot can land localTime on the other side of a cycle boundary.
			double cycleSlot = everyLoop ? (delay + (double)duration) : Math.Max(duration, 1e-9);
			double firstDelayOffset = everyLoop ? 0d : delay;

			localTime += dt * dir;

			// CompleteAtCycleStart while still in cycle 0: there is no lower
			// cycle boundary to cross (the backward-crossing branch below never
			// runs), so reaching the cycle-0 content start completes here. The
			// content start sits at localTime == delay for both delay types
			// (FirstLoop: firstDelayOffset; EveryLoop: the in-slot delay region).
			if (dir < 0 && stopAtStartBoundary && lastCycleIndex == 0 && localTime <= delay)
			{
				localTime = delay;
				if (setter != null)
				{
					GetCycleEnds(0, out var startFrom, out var startTo);
					if (!ApplySetter(interpolator.Lerp(startFrom, startTo, ease.Evaluate(0f))))
					{
						return;
					}
				}
				InvokeOnRewind();
				Status = TweenStatus.Completed;
				InvokeOnComplete();
				return;
			}

			if (loopCount < 0)
			{
				// ADR 0009: infinite loops wrap on a backward crossing instead of
				// clamping. A FirstLoop delay sits before cycle 0 only, so the wrap
				// floor is the delay offset — the initial delay is never re-entered
				// backward (it still plays out forward). An EveryLoop delay lives
				// inside the cycle slot, so the wrap re-enters the previous cycle's
				// delay region (phase 1.15).
				if (dir < 0 && localTime < firstDelayOffset)
				{
					localTime += cycleSlot;
					if (lastCycleIndex == 0)
					{
						InvokeOnRewind();
					}
				}
			}
			else if (localTime < 0d)
			{
				localTime = 0d;
			}

			if (!everyLoop && localTime < delay)
			{
				Status = TweenStatus.Delayed;
				return;
			}

			var activeLocalTime = localTime - firstDelayOffset;
			if (activeLocalTime < 0d)
			{
				activeLocalTime = 0d;
			}

			var cycleIndex = (int)Math.Floor(activeLocalTime / cycleSlot);
			if (cycleIndex < 0)
			{
				cycleIndex = 0;
			}

			var inSlot = activeLocalTime - cycleIndex * cycleSlot;

			var inDelay = false;
			float tInCycle;
			if (everyLoop && inSlot < delay)
			{
				inDelay = true;
				tInCycle = 0f;
			}
			else
			{
				var cycleStartOffset = everyLoop ? delay : 0d;
				var cycleElapsed = inSlot - cycleStartOffset;
				if (duration <= 0f)
				{
					tInCycle = 1f;
				}
				else
				{
					tInCycle = (float)(cycleElapsed / duration);
					if (tInCycle > 1f) tInCycle = 1f;
					if (tInCycle < 0f) tInCycle = 0f;
				}
			}

			var entryCycleIndex = lastCycleIndex;
			var completed = false;
			var completedByExhaustion = false;
			if (dir > 0 && loopCount > 0 && cycleIndex >= loopCount)
			{
				completed = true;
				completedByExhaustion = true;
				cycleIndex = loopCount - 1;
				tInCycle = 1f;
			}

			var stoppedAtBoundary = false;
			if (dir > 0 && cycleIndex > lastCycleIndex)
			{
				if (stopAtNextBoundary)
				{
					completed = true;
					stoppedAtBoundary = true;
					tInCycle = 1f;
					cycleIndex = lastCycleIndex;
				}
				else
				{
					lastCycleIndex = cycleIndex;
				}
			}
			else if (dir < 0 && cycleIndex < lastCycleIndex)
			{
				InvokeOnRewind();
				lastCycleIndex = cycleIndex;
				if (stopAtStartBoundary)
				{
					completed = true;
					tInCycle = 0f;
				}
			}

			Status = inDelay ? TweenStatus.Delayed : TweenStatus.Playing;

			if (inDelay)
			{
				if (everyLoop && cycleIndex > 0)
				{
					GetCycleEnds(cycleIndex - 1, out _, out var prevEnd);
					if (!ApplySetter(prevEnd))
					{
						return;
					}
				}
			}
			else
			{
				FireStartIfPending();
				GetCycleEnds(cycleIndex, out var cycleFrom, out var cycleTo);
				var easedT = ease.Evaluate(tInCycle);
				var value = interpolator.Lerp(cycleFrom, cycleTo, easedT);
				if (!ApplySetter(value))
				{
					return;
				}
				InvokeOnUpdate(easedT);
			}

			// Forward boundary crossings, including the final one on natural
			// completion (Documentation~/api/handles.md: "per loop end").
			if (dir > 0)
			{
				var boundaries = 0;
				if (completedByExhaustion)
				{
					boundaries = loopCount - entryCycleIndex;
				}
				else if (stoppedAtBoundary)
				{
					boundaries = 1;
				}
				else if (cycleIndex > entryCycleIndex)
				{
					boundaries = cycleIndex - entryCycleIndex;
				}
				for (var i = 0; i < boundaries; i++)
				{
					InvokeOnStepComplete();
				}
			}

			if (completed)
			{
				if (completedByExhaustion)
				{
					lastCycleIndex = loopCount; // ForceComplete must not refire boundaries
				}
				Status = TweenStatus.Completed;
				InvokeOnComplete();
				// No OnKill here: natural completion never fires OnKill (Documentation~/api/handles.md).
			}
		}

		public override void SeekTo(double seconds, bool fireCallbacks)
		{
			var everyLoop = delayType == DelayType.EveryLoop && delay > 0f;
			double cycleSlot = everyLoop ? (delay + (double)duration) : Math.Max(duration, 1e-9);
			double firstDelayOffset = everyLoop ? 0d : delay;

			if (seconds < 0d)
			{
				seconds = 0d;
			}
			if (loopCount > 0)
			{
				var total = firstDelayOffset + cycleSlot * loopCount;
				if (seconds > total)
				{
					seconds = total;
				}
			}

			// A seek before the first tick still needs start-value capture; the
			// snapPending guard makes this a no-op when already resolved.
			ResolveStartValues();

			localTime = seconds;

			var activeLocalTime = seconds - firstDelayOffset;
			if (activeLocalTime < 0d)
			{
				activeLocalTime = 0d;
			}
			var cycleIndex = (int)Math.Floor(activeLocalTime / cycleSlot);
			if (cycleIndex < 0)
			{
				cycleIndex = 0;
			}
			if (loopCount > 0 && cycleIndex >= loopCount)
			{
				cycleIndex = loopCount - 1;
			}
			var inSlot = activeLocalTime - cycleIndex * cycleSlot;

			float tInCycle;
			var cycleStartOffset = everyLoop ? delay : 0d;
			var cycleElapsed = inSlot - cycleStartOffset;
			if (duration <= 0f)
			{
				tInCycle = 1f;
			}
			else
			{
				tInCycle = (float)(cycleElapsed / duration);
				if (tInCycle > 1f) tInCycle = 1f;
				if (tInCycle < 0f) tInCycle = 0f;
			}

			if (fireCallbacks)
			{
				for (var i = lastCycleIndex; i < cycleIndex; i++)
				{
					InvokeOnStepComplete();
				}
				for (var i = lastCycleIndex; i > cycleIndex; i--)
				{
					InvokeOnRewind();
				}
			}
			lastCycleIndex = cycleIndex;

			if (setter != null)
			{
				GetCycleEnds(cycleIndex, out var cycleFrom, out var cycleTo);
				var easedT = ease.Evaluate(tInCycle);
				if (!ApplySetter(interpolator.Lerp(cycleFrom, cycleTo, easedT)))
				{
					return;
				}
				if (fireCallbacks)
				{
					InvokeOnUpdate(easedT);
				}
			}
		}

		private void GetCycleEnds(int cycleIndex, out T cycleFrom, out T cycleTo)
		{
			switch (loopType)
			{
				case LoopType.Yoyo:
					if ((cycleIndex & 1) == 1)
					{
						cycleFrom = endValue;
						cycleTo = startValue;
					}
					else
					{
						cycleFrom = startValue;
						cycleTo = endValue;
					}
					break;
				case LoopType.Incremental:
				{
					var delta = interpolator.Subtract(endValue, startValue);
					if (cycleIndex == 0)
					{
						incrementalCacheBase = startValue;
					}
					else if (incrementalCacheIndex == cycleIndex - 1)
					{
						incrementalCacheBase = interpolator.Add(incrementalCacheBase, delta);
					}
					else if (incrementalCacheIndex != cycleIndex)
					{
						// Backward jump or cold cache: recompute once from scratch.
						var shifted = startValue;
						for (var i = 0; i < cycleIndex; i++)
						{
							shifted = interpolator.Add(shifted, delta);
						}
						incrementalCacheBase = shifted;
					}
					incrementalCacheIndex = cycleIndex;
					cycleFrom = incrementalCacheBase;
					cycleTo = interpolator.Add(incrementalCacheBase, delta);
					break;
				}
				default:
					cycleFrom = startValue;
					cycleTo = endValue;
					break;
			}
		}

		public override void ReturnToPool()
		{
			TweenDataPool<T>.Return(this);
		}

		public override void Reset()
		{
			base.Reset();
			getter = null;
			setter = null;
			startValue = default;
			endValue = default;
			duration = 0f;
			localTime = 0d;
			interpolator = null;
			relative = false;
			ease = Easing.Linear();
			loopCount = 1;
			loopType = LoopType.Restart;
			delay = 0f;
			delayType = DelayType.FirstLoop;
			snapMode = SnapMode.None;
			fromValue = default;
			snapPending = false;
			lastCycleIndex = 0;
			stopAtNextBoundary = false;
			stopAtStartBoundary = false;
			incrementalCacheIndex = -1;
			incrementalCacheBase = default;
		}
	}
}
