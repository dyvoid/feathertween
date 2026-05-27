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
		private float duration;
		private List<Action> onComplete;
		private List<Action> onKill;

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

		public List<Action> OnComplete => onComplete;
		public List<Action> OnKill => onKill;

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
			duration = 0f;
			onComplete?.Clear();
			onKill?.Clear();
		}
	}
}
