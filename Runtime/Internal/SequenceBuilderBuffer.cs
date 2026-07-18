using System;
using System.Collections.Generic;

namespace dyvoid.FeatherTween.Internal
{
	internal sealed class SequenceBuilderBuffer
	{
		private struct PendingLabelRef
		{
			public int EntryIndex;
			public string Label;
			public double Offset;
		}

		private readonly int leakId;
		private uint generation;
		private bool released;

		private UpdatePhase phase;
		private bool phaseExplicit;
		private bool ignoreTimeScale;
		private bool autoKill;
		private object target;
		private float delay;
		private SequenceCancelBehavior cancelBehavior;
		private int loops;
		private LoopType loopType;
		private bool safeMode;
		private bool cancelOnError;

		private EaseRef defaultEase;
		private bool hasDefaultEase;
		private int defaultLoops;
		private LoopType defaultLoopType;
		private bool hasDefaultLoops;
		private float defaultDelay;
		private bool hasDefaultDelay;

		private readonly List<SequenceChildEntry> entries = new List<SequenceChildEntry>();
		private readonly List<Action> callbacks = new List<Action>();
		private readonly Dictionary<string, double> labels = new Dictionary<string, double>();
		private readonly List<PendingLabelRef> pendingLabels = new List<PendingLabelRef>();
		private List<CallbackEntry> onStart;
		private List<CallbackEntry> onPlay;
		private List<CallbackEntry> onPause;
		private List<Action<float>> onUpdate;
		private List<CallbackEntry> onStepComplete;
		private List<CallbackEntry> onComplete;
		private List<CallbackEntry> onKill;
		private List<CallbackEntry> onRewind;

		private double cursor;
		private double maxEnd;
		private double joinAnchor;
		private double lastAddedStart;
		private double lastAddedEnd;
		private int orderCounter;

		private static readonly Comparison<SequenceChildEntry> startOrderComparison = CompareEntries;

		public uint Generation => generation;
		public bool Released => released;

		public UpdatePhase Phase
		{
			get => phase;
			set
			{
				phase = value;
				phaseExplicit = true;
			}
		}

		public bool PhaseExplicit => phaseExplicit;

		// Inheritance from a parent sequence; bypasses the explicit flag.
		public void ApplyInheritedPhase(UpdatePhase value, bool inheritedIgnoreTimeScale)
		{
			phase = value;
			ignoreTimeScale = inheritedIgnoreTimeScale;
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

		public object Target
		{
			get => target;
			set => target = value;
		}

		public float Delay
		{
			get => delay;
			set => delay = value;
		}

		public SequenceCancelBehavior CancelBehavior
		{
			get => cancelBehavior;
			set => cancelBehavior = value;
		}

		public bool SafeMode
		{
			get => safeMode;
			set => safeMode = value;
		}

		public bool CancelOnError
		{
			get => cancelOnError;
			set => cancelOnError = value;
		}

		public void SetLoops(int count, LoopType type)
		{
			loops = count < 0 ? -1 : (count == 0 ? 1 : count);
			loopType = type;
		}

		public int Loops => loops;

		public double Cursor => cursor;
		public double JoinAnchor => joinAnchor;
		public double LastAddedStart => lastAddedStart;
		public double LastAddedEnd => lastAddedEnd;
		public int EntryCount => entries.Count;
		public double Duration => Math.Max(cursor, maxEnd);

		public SequenceBuilderBuffer()
		{
			leakId = LeakDetector.Register();
			generation = 1;
			ResetConfig();
		}

		~SequenceBuilderBuffer()
		{
			if (!released)
			{
				LeakDetector.EnqueueLeak(leakId);
			}
		}

		public void SetDefaults(
			EaseRef? ease,
			int? loops,
			LoopType loopType,
			float? childDelay)
		{
			if (ease.HasValue)
			{
				defaultEase = ease.Value;
				hasDefaultEase = true;
			}
			if (loops.HasValue)
			{
				defaultLoops = loops.Value < 0 ? -1 : loops.Value;
				defaultLoopType = loopType;
				hasDefaultLoops = true;
			}
			if (childDelay.HasValue)
			{
				defaultDelay = childDelay.Value < 0f ? 0f : childDelay.Value;
				hasDefaultDelay = true;
			}
		}

		public void ApplyDefaults<T>(TweenBuilderBuffer<T> child)
		{
			if (hasDefaultEase && !child.EaseExplicit)
			{
				child.ApplyDefaultEase(defaultEase);
			}
			if (hasDefaultLoops && !child.LoopsExplicit)
			{
				child.ApplyDefaultLoops(defaultLoops, defaultLoopType);
			}
			if (hasDefaultDelay && !child.DelayExplicit)
			{
				child.ApplyDefaultDelay(defaultDelay);
			}
		}

		public int AddCallbackSlot(Action cb)
		{
			if (cb == null)
			{
				return -1;
			}
			callbacks.Add(cb);
			return callbacks.Count - 1;
		}

		public int AddEntry(
			int id, uint gen, double start, double length, bool infinite,
			SequenceChildKind kind, int callbackIndex)
		{
			var entry = new SequenceChildEntry
			{
				Id = id,
				Gen = gen,
				Start = start,
				Length = infinite ? 0d : length,
				Infinite = infinite,
				Kind = kind,
				CallbackIndex = callbackIndex,
				Order = orderCounter++,
			};
			entries.Add(entry);

			if (!double.IsNaN(start))
			{
				TrackAdded(entry.Start, entry.End);
			}
			return entries.Count - 1;
		}

		public void DeferEntryToLabel(int entryIndex, string label, double offset)
		{
			pendingLabels.Add(new PendingLabelRef
			{
				EntryIndex = entryIndex,
				Label = label,
				Offset = offset,
			});
		}

		public void SetAppendAnchors(double start, double end)
		{
			joinAnchor = start;
			cursor = end;
		}

		public void AdvanceCursor(double seconds)
		{
			cursor += seconds;
		}

		public bool TryResolveLabel(string label, out double time)
		{
			return labels.TryGetValue(label, out time);
		}

		public void DefineLabel(string label, double time)
		{
			labels[label] = time;
		}

		public void ShiftAll(double shift)
		{
			if (shift <= 0d)
			{
				return;
			}
			for (var i = 0; i < entries.Count; i++)
			{
				var e = entries[i];
				if (!double.IsNaN(e.Start))
				{
					e.Start += shift;
					entries[i] = e;
				}
			}
			if (labels.Count > 0)
			{
				// Small builder-side dictionary; rebuild is fine at build time.
				var keys = new List<string>(labels.Keys);
				for (var i = 0; i < keys.Count; i++)
				{
					labels[keys[i]] += shift;
				}
			}
			cursor += shift;
			maxEnd += shift;
			joinAnchor += shift;
			lastAddedStart += shift;
			lastAddedEnd += shift;
		}

		public void ClearEntries(bool clearLabels)
		{
			for (var i = 0; i < entries.Count; i++)
			{
				var e = entries[i];
				if (e.Kind == SequenceChildKind.Tween && TweenStore.IsAlive(e.Id, e.Gen))
				{
					TweenStore.Free(e.Id);
				}
			}
			entries.Clear();
			callbacks.Clear();
			pendingLabels.Clear();
			if (clearLabels)
			{
				labels.Clear();
			}
			cursor = 0d;
			maxEnd = 0d;
			joinAnchor = 0d;
			lastAddedStart = 0d;
			lastAddedEnd = 0d;
			orderCounter = 0;
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

		public SequenceData Build()
		{
			for (var i = 0; i < pendingLabels.Count; i++)
			{
				var pending = pendingLabels[i];
				if (!labels.TryGetValue(pending.Label, out var labelTime))
				{
					throw new InvalidOperationException(
						$"[FeatherTween] Sequence label '{pending.Label}' is undefined at Start().");
				}
				var e = entries[pending.EntryIndex];
				e.Start = labelTime + pending.Offset;
				if (e.Start < 0d)
				{
					throw new ArgumentOutOfRangeException(
						nameof(pending.Offset),
						$"[FeatherTween] Label '{pending.Label}' with offset resolves to a negative time.");
				}
				entries[pending.EntryIndex] = e;
				TrackAdded(e.Start, e.Infinite ? e.Start : e.End);
			}
			pendingLabels.Clear();

			var sorted = entries.ToArray();
			Array.Sort(sorted, startOrderComparison);

			var data = new SequenceData(
				sorted,
				new List<Action>(callbacks),
				Duration,
				delay,
				cancelBehavior,
				loops,
				loopType);
			data.Phase = phase;
			data.IgnoreTimeScale = ignoreTimeScale;
			data.AutoKill = autoKill;
			data.Target = target;
			data.SafeMode = safeMode;
			data.CancelOnError = cancelOnError;
			TransferCallbacks(data);
			data.Status = delay > 0f ? TweenStatus.Delayed : TweenStatus.Playing;
			return data;
		}

		// Direct loops, no method-group arguments: converting data.AddOnX to a
		// delegate allocates even when the list is null (Documentation~/architecture/performance.md).
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

		public void TrackAdded(double start, double end)
		{
			lastAddedStart = start;
			lastAddedEnd = end;
			if (end > maxEnd)
			{
				maxEnd = end;
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
			entries.Clear();
			callbacks.Clear();
			labels.Clear();
			pendingLabels.Clear();
			ClearCallbackSlots();
		}

		private void ClearCallbackSlots()
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

		private static int CompareEntries(SequenceChildEntry a, SequenceChildEntry b)
		{
			var byStart = a.Start.CompareTo(b.Start);
			return byStart != 0 ? byStart : a.Order.CompareTo(b.Order);
		}

		private void ResetConfig()
		{
			phase = UpdatePhase.Update;
			phaseExplicit = false;
			ignoreTimeScale = false;
			autoKill = true;
			target = null;
			delay = 0f;
			cancelBehavior = SequenceCancelBehavior.ContinueOnChildAutoKill;
			loops = 1;
			loopType = LoopType.Restart;
			safeMode = SafeModeDefault.Value;
			cancelOnError = false;
			hasDefaultEase = false;
			hasDefaultLoops = false;
			hasDefaultDelay = false;
			entries.Clear();
			callbacks.Clear();
			labels.Clear();
			pendingLabels.Clear();
			ClearCallbackSlots();
			cursor = 0d;
			maxEnd = 0d;
			joinAnchor = 0d;
			lastAddedStart = 0d;
			lastAddedEnd = 0d;
			orderCounter = 0;
		}
	}
}
