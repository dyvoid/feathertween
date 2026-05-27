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
		private double elapsed;
		private IInterpolator<T> interpolator;
		private bool relative;
		private EaseRef ease;

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

		public double Elapsed
		{
			get => elapsed;
			set => elapsed = value;
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

		public override void Step(double scaledDelta, double unscaledDelta)
		{
			if (Status != TweenStatus.Playing)
			{
				return;
			}

			var dt = IgnoreTimeScale ? unscaledDelta : scaledDelta;
			elapsed += dt;

			float t;
			var completed = false;
			if (duration <= 0f)
			{
				t = 1f;
				completed = true;
			}
			else if (elapsed >= duration)
			{
				t = 1f;
				completed = true;
			}
			else
			{
				t = (float)(elapsed / duration);
			}

			var easedT = ease.Evaluate(t);
			var value = interpolator.Lerp(startValue, endValue, easedT);
			setter(value);

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

		public override void Reset()
		{
			base.Reset();
			getter = null;
			setter = null;
			startValue = default;
			endValue = default;
			duration = 0f;
			elapsed = 0d;
			interpolator = null;
			relative = false;
			ease = Easing.Linear();
		}
	}
}
