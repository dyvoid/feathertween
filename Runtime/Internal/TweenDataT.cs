using System;

namespace PATween.Internal
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

		public override void ResolveStartValues()
		{
			if (!snapPending)
			{
				return;
			}
			snapPending = false;

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
					setter?.Invoke(startValue);
					break;
				case SnapMode.FromTo:
					startValue = fromValue;
					setter?.Invoke(startValue);
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
				loopCount = lastCycleIndex + cycles;
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
			Direction = 1;
		}

		public override bool StartsDelayed()
		{
			return delay > 0f && delayType == DelayType.FirstLoop;
		}

		public override void ForceComplete()
		{
			var finalIndex = loopCount > 0 ? loopCount - 1 : lastCycleIndex;
			if (setter != null)
			{
				GetCycleEnds(finalIndex, out _, out var cycleTo);
				setter(cycleTo);
			}

			// Complete()/Kill(true) fire OnStepComplete for the remaining loop
			// boundaries; an infinite loop completes its current iteration (§3.14).
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
			var dt = IgnoreTimeScale ? unscaledDelta : scaledDelta;

			var everyLoop = delayType == DelayType.EveryLoop && delay > 0f;
			double cycleSlot = everyLoop ? (delay + duration) : Math.Max(duration, 1e-9);
			double firstDelayOffset = everyLoop ? 0d : delay;

			localTime += dt * dir;
			if (localTime < 0d)
			{
				if (loopCount < 0)
				{
					localTime += cycleSlot;
					if (lastCycleIndex == 0)
					{
						InvokeOnRewind();
					}
				}
				else
				{
					localTime = 0d;
				}
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
					setter(prevEnd);
				}
			}
			else
			{
				FireStartIfPending();
				GetCycleEnds(cycleIndex, out var cycleFrom, out var cycleTo);
				var easedT = ease.Evaluate(tInCycle);
				var value = interpolator.Lerp(cycleFrom, cycleTo, easedT);
				setter(value);
				InvokeOnUpdate(easedT);
			}

			// Forward boundary crossings, including the final one on natural
			// completion (§3.14: "per loop end").
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
				// No OnKill here: natural completion never fires OnKill (§3.14).
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
					var shifted = startValue;
					for (var i = 0; i < cycleIndex; i++)
					{
						shifted = interpolator.Add(shifted, delta);
					}
					cycleFrom = shifted;
					cycleTo = interpolator.Add(shifted, delta);
					break;
				}
				default:
					cycleFrom = startValue;
					cycleTo = endValue;
					break;
			}
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
		}
	}
}
