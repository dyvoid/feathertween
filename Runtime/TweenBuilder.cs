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

		// Try/catch around setter and callback invocations. Default on in the
		// Editor, off in player builds; PATWEEN_RELEASE compiles the wrapper out
		// entirely (docs/architecture/overview.md "Safe mode").
		public TweenBuilder<T> SetSafeMode(bool value)
		{
			ValidateOrThrow();
			buffer.SafeMode = value;
			return this;
		}

		// In safe mode: a setter exception kills the tween silently and fires
		// OnKill; a callback exception is logged and cancels the tween.
		public TweenBuilder<T> SetCancelOnError(bool value)
		{
			ValidateOrThrow();
			buffer.CancelOnError = value;
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

		public TweenBuilder<T> OnStart(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnStart(cb);
			return this;
		}

		public TweenBuilder<T> OnPlay(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnPlay(cb);
			return this;
		}

		public TweenBuilder<T> OnPause(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnPause(cb);
			return this;
		}

		// Receives the eased in-cycle progress used for the value write (1f at
		// a cycle end regardless of ease shape).
		public TweenBuilder<T> OnUpdate(Action<float> cb)
		{
			ValidateOrThrow();
			buffer.AddOnUpdate(cb);
			return this;
		}

		public TweenBuilder<T> OnStepComplete(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnStepComplete(cb);
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

		// Zero-alloc target-capture overload (anchor 15): pass state explicitly
		// and use a static lambda so the compiler emits no closure.
		public TweenBuilder<T> OnComplete<TTarget>(TTarget state, Action<TTarget> cb)
			where TTarget : class
		{
			ValidateOrThrow();
			if (state != null && cb != null)
			{
				buffer.AddOnComplete(CallbackEntry.FromTargetCapture(state, cb));
			}
			return this;
		}

		public TweenBuilder<T> OnKill(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnKill(cb);
			return this;
		}

		public TweenBuilder<T> OnKill<TTarget>(TTarget state, Action<TTarget> cb)
			where TTarget : class
		{
			ValidateOrThrow();
			if (state != null && cb != null)
			{
				buffer.AddOnKill(CallbackEntry.FromTargetCapture(state, cb));
			}
			return this;
		}

		public Tween Start()
		{
			ValidateOrThrow();

			var data = buffer.Build();
			data.ResolveStartValues();

			// A safe-mode From/FromTo snap whose setter threw cancelled the
			// record before it ever got a store slot: recycle it and hand back
			// a dead handle instead of storing an unkillable corpse.
			if (data.Status == TweenStatus.Cancelled)
			{
				data.ReturnToPool();
				TweenBuilderBufferPool<T>.Return(buffer);
				buffer = null;
				generation = 0;
				return new Tween(-1, 0);
			}

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
