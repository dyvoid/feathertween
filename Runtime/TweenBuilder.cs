using System;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween
{
	/// <summary>
	/// Mutable configuration for a tween before it runs. Chain setters, then call
	/// <see cref="Start"/> to consume the builder and get a <see cref="Tween"/>
	/// handle. Copies alias one shared backing record; after the builder is
	/// consumed (started or appended to a sequence), every alias is invalid and
	/// further use throws.
	/// </summary>
	public struct TweenBuilder<T>
	{
		private TweenBuilderBuffer<T> buffer;
		private uint generation;

		internal TweenBuilderBuffer<T> Buffer => buffer;
		internal uint Generation => generation;

		/// <summary>Reads <see cref="TweenStatus.Delayed"/> while the builder is un-consumed, <see cref="TweenStatus.Disposed"/> after.</summary>
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

		/// <summary>Selects the driving <see cref="UpdatePhase"/>; with <paramref name="ignoreTimeScale"/> the tween uses unscaled delta time (but still honors FeatherTween's own scales).</summary>
		public TweenBuilder<T> SetUpdate(UpdatePhase phase, bool ignoreTimeScale = false)
		{
			ValidateOrThrow();
			buffer.Phase = phase;
			buffer.IgnoreTimeScale = ignoreTimeScale;
			return this;
		}

		/// <summary>Whether the tween frees itself on completion (default true). Disable to keep it seekable/restartable; kill it explicitly when done.</summary>
		public TweenBuilder<T> SetAutoKill(bool value)
		{
			ValidateOrThrow();
			buffer.AutoKill = value;
			return this;
		}

		/// <summary>Associates a target for <c>FT.Kill(target)</c>/<c>FT.IsTweening(target)</c>; destroyed <c>UnityEngine.Object</c> targets auto-kill the tween.</summary>
		public TweenBuilder<T> SetTarget(object target)
		{
			ValidateOrThrow();
			buffer.Target = target;
			return this;
		}

		/// <summary>Try/catch around setter and callback invocations. Default on in the Editor, off in player builds; <c>FEATHERTWEEN_RELEASE</c> compiles the wrapper out entirely.</summary>
		public TweenBuilder<T> SetSafeMode(bool value)
		{
			ValidateOrThrow();
			buffer.SafeMode = value;
			return this;
		}

		/// <summary>In safe mode: a setter exception kills the tween silently and fires <c>OnKill</c>; a callback exception is logged and cancels the tween.</summary>
		public TweenBuilder<T> SetCancelOnError(bool value)
		{
			ValidateOrThrow();
			buffer.CancelOnError = value;
			return this;
		}

		/// <summary>Treats the end value as an offset from the start value sampled at playback (no effect on <c>FromTo</c>-style tweens, whose endpoints are explicit).</summary>
		public TweenBuilder<T> SetRelative(bool value)
		{
			ValidateOrThrow();
			buffer.Relative = value;
			return this;
		}

		/// <summary>Sets the ease shape (see <see cref="Easing"/> factories). Default is linear.</summary>
		public TweenBuilder<T> SetEase(EaseRef ease)
		{
			ValidateOrThrow();
			buffer.Ease = ease;
			return this;
		}

		/// <summary>Uses an <c>AnimationCurve</c> as the ease shape (shorthand for <c>SetEase(Easing.Curve(curve))</c>).</summary>
		public TweenBuilder<T> SetEase(UnityEngine.AnimationCurve curve)
		{
			ValidateOrThrow();
			buffer.Ease = Easing.Curve(curve);
			return this;
		}

		/// <summary>Swap mode: the creation method's end value becomes the start, and the tween plays to the value the getter reads at snap time (ADR 0007).</summary>
		public TweenBuilder<T> From()
		{
			ValidateOrThrow();
			buffer.SnapMode = SnapMode.From;
			return this;
		}

		/// <summary>Explicit start value: the tween plays from <paramref name="value"/> to the creation method's end value. The getter is never read — both endpoints are known at this call, exactly like <c>FT.FromTo</c>, so <c>SetRelative</c> is ignored.</summary>
		public TweenBuilder<T> From(T value)
		{
			ValidateOrThrow();
			buffer.FromValue = value;
			buffer.SnapMode = SnapMode.FromTo;
			return this;
		}

		/// <summary>Plays <paramref name="count"/> cycles (negative = forever) traversed per <paramref name="loopType"/>.</summary>
		public TweenBuilder<T> SetLoops(int count, LoopType loopType = LoopType.Restart)
		{
			ValidateOrThrow();
			buffer.LoopCount = count < 0 ? -1 : count;
			buffer.LoopType = loopType;
			return this;
		}

		/// <summary>Defers playback by <paramref name="seconds"/>; <paramref name="delayType"/> chooses first-cycle-only or every cycle. Negative values throw.</summary>
		public TweenBuilder<T> SetDelay(float seconds, DelayType delayType = DelayType.FirstLoop)
		{
			ValidateOrThrow();
			if (seconds < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(seconds), "Delay cannot be negative.");
			}
			buffer.Delay = seconds;
			buffer.DelayType = delayType;
			return this;
		}

		/// <summary>Called once, when playback first begins (after any initial delay).</summary>
		public TweenBuilder<T> OnStart(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnStart(cb);
			return this;
		}

		/// <summary>Called whenever playback begins or resumes.</summary>
		public TweenBuilder<T> OnPlay(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnPlay(cb);
			return this;
		}

		/// <summary>Called whenever playback pauses.</summary>
		public TweenBuilder<T> OnPause(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnPause(cb);
			return this;
		}

		/// <summary>Called every tick with the eased in-cycle progress used for the value write (1f at a cycle end regardless of ease shape).</summary>
		public TweenBuilder<T> OnUpdate(Action<float> cb)
		{
			ValidateOrThrow();
			buffer.AddOnUpdate(cb);
			return this;
		}

		/// <summary>Called at the end of each loop cycle.</summary>
		public TweenBuilder<T> OnStepComplete(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnStepComplete(cb);
			return this;
		}

		/// <summary>Called when the playhead returns to the start (reverse playback or rewind).</summary>
		public TweenBuilder<T> OnRewind(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnRewind(cb);
			return this;
		}

		/// <summary>Called once, when the final cycle finishes. Not called on kill.</summary>
		public TweenBuilder<T> OnComplete(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnComplete(cb);
			return this;
		}

		/// <summary>Zero-alloc target-capture overload: pass state explicitly and use a static lambda so the compiler emits no closure.</summary>
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

		/// <summary>Called when the tween is killed (not on completion).</summary>
		public TweenBuilder<T> OnKill(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnKill(cb);
			return this;
		}

		/// <summary>Zero-alloc target-capture overload of <see cref="OnKill(Action)"/>.</summary>
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

		/// <summary>Consumes the builder, registers the tween with the runner, and returns its handle. Root <c>From</c>/<c>FromTo</c> tweens snap their start value here.</summary>
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
					"[FeatherTween] TweenBuilder used after Start() or invalid alias.");
			}
		}
	}
}
