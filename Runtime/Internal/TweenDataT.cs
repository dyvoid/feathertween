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

			var completed = false;
			if (dir > 0 && loopCount > 0 && cycleIndex >= loopCount)
			{
				completed = true;
				cycleIndex = loopCount - 1;
				tInCycle = 1f;
			}

			if (dir > 0 && cycleIndex > lastCycleIndex)
			{
				if (stopAtNextBoundary)
				{
					completed = true;
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
				GetCycleEnds(cycleIndex, out var cycleFrom, out var cycleTo);
				var easedT = ease.Evaluate(tInCycle);
				var value = interpolator.Lerp(cycleFrom, cycleTo, easedT);
				setter(value);
			}

			if (completed)
			{
				Status = TweenStatus.Completed;
				InvokeOnComplete();
				if (AutoKill)
				{
					InvokeOnKill();
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
			lastCycleIndex = 0;
			stopAtNextBoundary = false;
			stopAtStartBoundary = false;
		}
	}
}
