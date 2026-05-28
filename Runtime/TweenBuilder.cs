using System;
using PATween.Internal;

namespace PATween
{
	public struct TweenBuilder<T>
	{
		private TweenBuilderBuffer<T> buffer;
		private uint generation;

		internal TweenBuilderBuffer<T> Buffer => buffer;
		internal uint Generation => generation;

		public TweenStatus Status
		{
			get
			{
				if (!IsValid())
				{
					return TweenStatus.Disposed;
				}
				return TweenStatus.Delayed;
			}
		}

		internal TweenBuilder(TweenBuilderBuffer<T> buffer)
		{
			this.buffer = buffer;
			this.generation = buffer.Generation;
		}

		public TweenBuilder<T> SetUpdate(UpdatePhase phase, bool ignoreTimeScale = false)
		{
			ValidateOrThrow();
			buffer.Phase = phase;
			buffer.IgnoreTimeScale = ignoreTimeScale;
			return this;
		}

		public TweenBuilder<T> SetAutoKill(bool value)
		{
			ValidateOrThrow();
			buffer.AutoKill = value;
			return this;
		}

		public TweenBuilder<T> SetTarget(object target)
		{
			ValidateOrThrow();
			buffer.Target = target;
			return this;
		}

		public TweenBuilder<T> SetRelative(bool value)
		{
			ValidateOrThrow();
			buffer.Relative = value;
			return this;
		}

		public TweenBuilder<T> SetEase(EaseRef ease)
		{
			ValidateOrThrow();
			buffer.Ease = ease;
			return this;
		}

		public TweenBuilder<T> SetEase(UnityEngine.AnimationCurve curve)
		{
			ValidateOrThrow();
			buffer.Ease = Easing.Curve(curve);
			return this;
		}

		public TweenBuilder<T> From()
		{
			ValidateOrThrow();
			buffer.SnapMode = SnapMode.From;
			return this;
		}

		public TweenBuilder<T> SetLoops(int count, LoopType type = LoopType.Restart)
		{
			ValidateOrThrow();
			buffer.LoopCount = count < 0 ? -1 : count;
			buffer.LoopType = type;
			return this;
		}

		public TweenBuilder<T> SetDelay(float seconds, DelayType type = DelayType.FirstLoop)
		{
			ValidateOrThrow();
			if (seconds < 0f)
			{
				seconds = 0f;
			}
			buffer.Delay = seconds;
			buffer.DelayType = type;
			return this;
		}

		public TweenBuilder<T> OnRewind(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnRewind(cb);
			return this;
		}

		public TweenBuilder<T> OnComplete(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnComplete(cb);
			return this;
		}

		public TweenBuilder<T> OnKill(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnKill(cb);
			return this;
		}

		public Tween Start()
		{
			ValidateOrThrow();

			var data = new TweenData<T>();
			data.Phase = buffer.Phase;
			data.AutoKill = buffer.AutoKill;
			data.IgnoreTimeScale = buffer.IgnoreTimeScale;
			data.Target = buffer.Target;
			data.Getter = buffer.Getter;
			data.Setter = buffer.Setter;
			data.EndValue = buffer.EndValue;
			data.Duration = buffer.Duration;
			data.Relative = buffer.Relative;
			data.Ease = buffer.Ease;
			data.LoopCount = buffer.LoopCount;
			data.LoopType = buffer.LoopType;
			data.Delay = buffer.Delay;
			data.DelayType = buffer.DelayType;
			data.Direction = 1;
			data.Interpolator = Interpolators.Get<T>();

			var snapValue = default(T);
			var snap = false;
			switch (buffer.SnapMode)
			{
				case SnapMode.None:
					data.StartValue = data.Getter != null ? data.Getter() : default;
					if (data.Relative)
					{
						data.EndValue = data.Interpolator.Add(data.StartValue, data.EndValue);
					}
					break;
				case SnapMode.From:
					data.StartValue = data.EndValue;
					data.EndValue = data.Getter != null ? data.Getter() : default;
					snapValue = data.StartValue;
					snap = true;
					break;
				case SnapMode.FromTo:
					data.StartValue = buffer.FromValue;
					snapValue = data.StartValue;
					snap = true;
					break;
			}

			if (snap && data.Setter != null)
			{
				data.Setter(snapValue);
			}

			if (buffer.OnComplete != null)
			{
				for (var i = 0; i < buffer.OnComplete.Count; i++)
				{
					data.AddOnComplete(buffer.OnComplete[i]);
				}
			}
			if (buffer.OnKill != null)
			{
				for (var i = 0; i < buffer.OnKill.Count; i++)
				{
					data.AddOnKill(buffer.OnKill[i]);
				}
			}
			if (buffer.OnRewind != null)
			{
				for (var i = 0; i < buffer.OnRewind.Count; i++)
				{
					data.AddOnRewind(buffer.OnRewind[i]);
				}
			}
			data.Status = buffer.Delay > 0f && buffer.DelayType == DelayType.FirstLoop
				? TweenStatus.Delayed
				: TweenStatus.Playing;

			var (id, gen) = TweenStore.Allocate();
			TweenStore.SetData(id, data);

			TweenBuilderBufferPool<T>.Return(buffer);
			buffer = null;
			generation = 0;

			return new Tween(id, gen);
		}

		private bool IsValid()
		{
			return buffer != null && !buffer.Released && buffer.Generation == generation;
		}

		private void ValidateOrThrow()
		{
			if (!IsValid())
			{
				throw new InvalidOperationException(
					"[PATween] TweenBuilder used after Start() or invalid alias.");
			}
		}
	}
}
