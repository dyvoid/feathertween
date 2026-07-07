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
		private List<CallbackEntry> onStart;
		private List<CallbackEntry> onPlay;
		private List<CallbackEntry> onPause;
		private List<Action<float>> onUpdate;
		private List<CallbackEntry> onStepComplete;
		private List<CallbackEntry> onComplete;
		private List<CallbackEntry> onKill;
		private List<CallbackEntry> onRewind;

		// Explicit-set flags: SetDefaults on a SequenceBuilder only cascades into
		// values the user did not set on the child builder.
		private bool phaseExplicit;
		private bool easeExplicit;
		private bool loopsExplicit;
		private bool delayExplicit;

		public uint Generation => generation;
		public bool Released => released;

		public bool PhaseExplicit => phaseExplicit;
		public bool EaseExplicit => easeExplicit;
		public bool LoopsExplicit => loopsExplicit;
		public bool DelayExplicit => delayExplicit;

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
			set => duration = value;
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

		public void AddOnStart(Action cb) => Add(ref onStart, cb);
		public void AddOnPlay(Action cb) => Add(ref onPlay, cb);
		public void AddOnPause(Action cb) => Add(ref onPause, cb);
		public void AddOnStepComplete(Action cb) => Add(ref onStepComplete, cb);
		public void AddOnComplete(Action cb) => Add(ref onComplete, cb);
		public void AddOnKill(Action cb) => Add(ref onKill, cb);
		public void AddOnRewind(Action cb) => Add(ref onRewind, cb);

		public void AddOnComplete(CallbackEntry entry)
		{
			onComplete ??= new List<CallbackEntry>();
			onComplete.Add(entry);
		}

		public void AddOnKill(CallbackEntry entry)
		{
			onKill ??= new List<CallbackEntry>();
			onKill.Add(entry);
		}

		public void AddOnUpdate(Action<float> cb)
		{
			if (cb == null)
			{
				return;
			}
			onUpdate ??= new List<Action<float>>();
			onUpdate.Add(cb);
		}

		private static void Add(ref List<CallbackEntry> list, Action cb)
		{
			if (cb == null)
			{
				return;
			}
			list ??= new List<CallbackEntry>();
			list.Add(CallbackEntry.FromAction(cb));
		}

		// Cascade writes from SequenceBuilder.SetDefaults; bypass the explicit flags.
		public void ApplyDefaultEase(EaseRef value) => ease = value;

		public void ApplyDefaultLoops(int count, LoopType type)
		{
			loopCount = count;
			loopType = type;
		}

		public void ApplyDefaultDelay(float value) => delay = value;
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
			var data = TweenDataPool<T>.Rent();
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

			TransferCallbacks(data);

			data.Status = delay > 0f && delayType == DelayType.FirstLoop
				? TweenStatus.Delayed
				: TweenStatus.Playing;

			return data;
		}

		// Direct loops, no method-group arguments: converting data.AddOnX to a
		// delegate allocates even when the list is null, which broke the
		// zero-alloc creation budget (§8.1). Entries outside OnComplete/OnKill
		// are always plain (target-capture is OnComplete/OnKill only).
		private void TransferCallbacks(TweenData data)
		{
			if (onStart != null)
			{
				for (var i = 0; i < onStart.Count; i++) data.AddOnStart(onStart[i].Plain);
			}
			if (onPlay != null)
			{
				for (var i = 0; i < onPlay.Count; i++) data.AddOnPlay(onPlay[i].Plain);
			}
			if (onPause != null)
			{
				for (var i = 0; i < onPause.Count; i++) data.AddOnPause(onPause[i].Plain);
			}
			if (onStepComplete != null)
			{
				for (var i = 0; i < onStepComplete.Count; i++) data.AddOnStepComplete(onStepComplete[i].Plain);
			}
			if (onComplete != null)
			{
				for (var i = 0; i < onComplete.Count; i++) data.AddOnComplete(onComplete[i]);
			}
			if (onKill != null)
			{
				for (var i = 0; i < onKill.Count; i++) data.AddOnKill(onKill[i]);
			}
			if (onRewind != null)
			{
				for (var i = 0; i < onRewind.Count; i++) data.AddOnRewind(onRewind[i].Plain);
			}
			if (onUpdate != null)
			{
				for (var i = 0; i < onUpdate.Count; i++) data.AddOnUpdate(onUpdate[i]);
			}
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
			ClearCallbacks();
		}

		private void ClearCallbacks()
		{
			onStart?.Clear();
			onPlay?.Clear();
			onPause?.Clear();
			onUpdate?.Clear();
			onStepComplete?.Clear();
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
			ClearCallbacks();
		}
	}
}
