using System;
using System.Collections.Generic;

namespace PATween.Internal
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

		private EaseRef defaultEase;
		private bool hasDefaultEase;
		private int defaultLoops;
		private LoopType defaultLoopType;
		private bool hasDefaultLoops;
		private float defaultDelay;
		private bool hasDefaultDelay;
		private float defaultDuration;
		private bool hasDefaultDuration;

		private readonly List<SequenceChildEntry> entries = new List<SequenceChildEntry>();
		private readonly List<Action> callbacks = new List<Action>();
		private readonly Dictionary<string, double> labels = new Dictionary<string, double>();
		private readonly List<PendingLabelRef> pendingLabels = new List<PendingLabelRef>();
		private List<Action> onComplete;
		private List<Action> onKill;

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
			float? childDelay,
			float? duration)
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
			if (duration.HasValue)
			{
				defaultDuration = duration.Value;
				hasDefaultDuration = true;
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
			if (hasDefaultDuration && !child.DurationExplicit)
			{
				child.ApplyDefaultDuration(defaultDuration);
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

		public SequenceData Build()
		{
			for (var i = 0; i < pendingLabels.Count; i++)
			{
				var pending = pendingLabels[i];
				if (!labels.TryGetValue(pending.Label, out var labelTime))
				{
					throw new InvalidOperationException(
						$"[PATween] Sequence label '{pending.Label}' is undefined at Start().");
				}
				var e = entries[pending.EntryIndex];
				e.Start = labelTime + pending.Offset;
				if (e.Start < 0d)
				{
					throw new ArgumentOutOfRangeException(
						nameof(pending.Offset),
						$"[PATween] Label '{pending.Label}' with offset resolves to a negative time.");
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
				cancelBehavior);
			data.Phase = phase;
			data.IgnoreTimeScale = ignoreTimeScale;
			data.AutoKill = autoKill;
			data.Target = target;
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
			data.Status = delay > 0f ? TweenStatus.Delayed : TweenStatus.Playing;
			return data;
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
			onComplete?.Clear();
			onKill?.Clear();
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
			hasDefaultEase = false;
			hasDefaultLoops = false;
			hasDefaultDelay = false;
			hasDefaultDuration = false;
			entries.Clear();
			callbacks.Clear();
			labels.Clear();
			pendingLabels.Clear();
			onComplete?.Clear();
			onKill?.Clear();
			cursor = 0d;
			maxEnd = 0d;
			joinAnchor = 0d;
			lastAddedStart = 0d;
			lastAddedEnd = 0d;
			orderCounter = 0;
		}
	}
}
