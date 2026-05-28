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

		public uint Generation => generation;
		public bool Released => released;

		public UpdatePhase Phase
		{
			get => phase;
			set => phase = value;
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
			set => duration = value;
		}

		public EaseRef Ease
		{
			get => ease;
			set => ease = value;
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
			onComplete?.Clear();
			onKill?.Clear();
			onRewind?.Clear();
		}
	}
}
