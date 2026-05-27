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
			data.Interpolator = Interpolators.Get<T>();

			if (data.Getter != null)
			{
				data.StartValue = data.Getter();
			}
			else
			{
				data.StartValue = default;
			}

			if (data.Relative)
			{
				data.EndValue = data.Interpolator.Add(data.StartValue, data.EndValue);
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
			data.Status = TweenStatus.Playing;

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
