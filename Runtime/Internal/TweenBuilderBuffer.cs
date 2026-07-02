using System;
using System.Collections.Generic;

namespace PATween.Internal
{
	internal sealed class TweenBuilderBuffer<T>
	{
		private readonly int leakId;
		private uint generation;
		private bool released;

		private UpdatePhase phase;
		private bool ignoreTimeScale;
		private bool autoKill;
		private bool relative;
		private object target;
		private Func<T> getter;
		private Action<T> setter;
		private T endValue;
		private T fromValue;
		private float duration;
		private EaseRef ease;
		private SnapMode snapMode;
		private int loopCount;
		private LoopType loopType;
		private float delay;
		private DelayType delayType;
		private List<Action> onComplete;
		private List<Action> onKill;
		private List<Action> onRewind;

		// Explicit-set flags: SetDefaults on a SequenceBuilder only cascades into
		// values the user did not set on the child builder.
		private bool phaseExplicit;
		private bool easeExplicit;
		private bool loopsExplicit;
		private bool delayExplicit;
		private bool durationExplicit;

		public uint Generation => generation;
		public bool Released => released;

		public bool PhaseExplicit => phaseExplicit;
		public bool EaseExplicit => easeExplicit;
		public bool LoopsExplicit => loopsExplicit;
		public bool DelayExplicit => delayExplicit;
		public bool DurationExplicit => durationExplicit;

		public UpdatePhase Phase
		{
			get => phase;
			set
			{
				phase = value;
				phaseExplicit = true;
			}
		}

		public bool IgnoreTimeScale
		{
			get => ignoreTimeScale;
			set => ignoreTimeScale = value;
		}

		public bool AutoKill
		{
			get => autoKill;
			set => autoKill = value;
		}

		public bool Relative
		{
			get => relative;
			set => relative = value;
		}

		public object Target
		{
			get => target;
			set => target = value;
		}

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

		public T EndValue
		{
			get => endValue;
			set => endValue = value;
		}

		public float Duration
		{
			get => duration;
			set
			{
				duration = value;
				durationExplicit = true;
			}
		}

		public EaseRef Ease
		{
			get => ease;
			set
			{
				ease = value;
				easeExplicit = true;
			}
		}

		public T FromValue
		{
			get => fromValue;
			set => fromValue = value;
		}

		public SnapMode SnapMode
		{
			get => snapMode;
			set => snapMode = value;
		}

		public int LoopCount
		{
			get => loopCount;
			set
			{
				loopCount = value;
				loopsExplicit = true;
			}
		}

		public LoopType LoopType
		{
			get => loopType;
			set => loopType = value;
		}

		public float Delay
		{
			get => delay;
			set
			{
				delay = value;
				delayExplicit = true;
			}
		}

		public DelayType DelayType
		{
			get => delayType;
			set => delayType = value;
		}

		public List<Action> OnComplete => onComplete;
		public List<Action> OnKill => onKill;
		public List<Action> OnRewind => onRewind;

		public TweenBuilderBuffer()
		{
			leakId = LeakDetector.Register();
			generation = 1;
			ResetConfig();
		}

		~TweenBuilderBuffer()
		{
			if (!released)
			{
				LeakDetector.EnqueueLeak(leakId);
			}
		}

		public void AddOnComplete(Action cb)
		{
			if (cb == null)
			{
				return;
			}
			onComplete ??= new List<Action>();
			onComplete.Add(cb);
		}

		public void AddOnKill(Action cb)
		{
			if (cb == null)
			{
				return;
			}
			onKill ??= new List<Action>();
			onKill.Add(cb);
		}

		public void AddOnRewind(Action cb)
		{
			if (cb == null)
			{
				return;
			}
			onRewind ??= new List<Action>();
			onRewind.Add(cb);
		}

		// Cascade writes from SequenceBuilder.SetDefaults; bypass the explicit flags.
		public void ApplyDefaultEase(EaseRef value) => ease = value;

		public void ApplyDefaultLoops(int count, LoopType type)
		{
			loopCount = count;
			loopType = type;
		}

		public void ApplyDefaultDelay(float value) => delay = value;
		public void ApplyDefaultDuration(float value) => duration = value;
		public void ApplyInheritedPhase(UpdatePhase value, bool inheritedIgnoreTimeScale)
		{
			phase = value;
			ignoreTimeScale = inheritedIgnoreTimeScale;
		}

		// Constructs the runtime data record without resolving start values.
		// Root tweens resolve immediately in TweenBuilder.Start(); sequenced
		// children resolve on parent-window entry.
		public TweenData<T> Build()
		{
			var data = new TweenData<T>();
			data.Phase = phase;
			data.AutoKill = autoKill;
			data.IgnoreTimeScale = ignoreTimeScale;
			data.Target = target;
			data.Getter = getter;
			data.Setter = setter;
			data.EndValue = endValue;
			data.Duration = duration;
			data.Relative = relative;
			data.Ease = ease;
			data.LoopCount = loopCount;
			data.LoopType = loopType;
			data.Delay = delay;
			data.DelayType = delayType;
			data.Direction = 1;
			data.Interpolator = Interpolators.Get<T>();
			data.SnapMode = snapMode;
			// Pristine value for (re-)resolution: FromTo carries the explicit
			// 'from'; From and relative-None carry the user-supplied end/delta.
			data.FromValue = snapMode == SnapMode.FromTo ? fromValue : endValue;
			data.SnapPending = true;

			if (onComplete != null)
			{
				for (var i = 0; i < onComplete.Count; i++)
				{
					data.AddOnComplete(onComplete[i]);
				}
			}
			if (onKill != null)
			{
				for (var i = 0; i < onKill.Count; i++)
				{
					data.AddOnKill(onKill[i]);
				}
			}
			if (onRewind != null)
			{
				for (var i = 0; i < onRewind.Count; i++)
				{
					data.AddOnRewind(onRewind[i]);
				}
			}

			data.Status = delay > 0f && delayType == DelayType.FirstLoop
				? TweenStatus.Delayed
				: TweenStatus.Playing;

			return data;
		}

		public void Rent()
		{
			released = false;
			ResetConfig();
		}

		public void Release()
		{
			released = true;
			generation = unchecked(generation + 1);
			if (generation == 0)
			{
				generation = 1;
			}
			onComplete?.Clear();
			onKill?.Clear();
			onRewind?.Clear();
		}

		private void ResetConfig()
		{
			phase = UpdatePhase.Update;
			ignoreTimeScale = false;
			autoKill = true;
			relative = false;
			target = null;
			getter = null;
			setter = null;
			endValue = default;
			fromValue = default;
			duration = 0f;
			ease = Easing.Linear();
			snapMode = SnapMode.None;
			loopCount = 1;
			loopType = LoopType.Restart;
			delay = 0f;
			delayType = DelayType.FirstLoop;
			phaseExplicit = false;
			easeExplicit = false;
			loopsExplicit = false;
			delayExplicit = false;
			durationExplicit = false;
			onComplete?.Clear();
			onKill?.Clear();
			onRewind?.Clear();
		}
	}
}
