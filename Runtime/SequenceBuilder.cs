using System;
using PATween.Internal;

namespace PATween
{
	public struct SequenceBuilder
	{
		private SequenceBuilderBuffer buffer;
		private uint generation;

		internal SequenceBuilderBuffer Buffer => buffer;
		internal uint Generation => generation;

		internal SequenceBuilder(SequenceBuilderBuffer buffer)
		{
			this.buffer = buffer;
			this.generation = buffer.Generation;
		}

		public SequenceBuilder SetUpdate(UpdatePhase phase, bool ignoreTimeScale = false)
		{
			ValidateOrThrow();
			buffer.Phase = phase;
			buffer.IgnoreTimeScale = ignoreTimeScale;
			return this;
		}

		public SequenceBuilder SetAutoKill(bool value)
		{
			ValidateOrThrow();
			buffer.AutoKill = value;
			return this;
		}

		public SequenceBuilder SetTarget(object target)
		{
			ValidateOrThrow();
			buffer.Target = target;
			return this;
		}

		public SequenceBuilder SetDelay(float seconds)
		{
			ValidateOrThrow();
			buffer.Delay = seconds < 0f ? 0f : seconds;
			return this;
		}

		public SequenceBuilder SetCancelBehavior(SequenceCancelBehavior behavior)
		{
			ValidateOrThrow();
			buffer.CancelBehavior = behavior;
			return this;
		}

		// No duration default: every creation method requires an explicit
		// duration, so a duration cascade could never apply (ADR 0010).
		public SequenceBuilder SetDefaults(
			EaseRef? ease = null,
			int? loops = null,
			LoopType loopType = LoopType.Restart,
			float? delay = null)
		{
			ValidateOrThrow();
			buffer.SetDefaults(ease, loops, loopType, delay);
			return this;
		}

		public SequenceBuilder OnComplete(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnComplete(cb);
			return this;
		}

		public SequenceBuilder OnKill(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnKill(cb);
			return this;
		}

		public SequenceBuilder Append<T>(TweenBuilder<T> child)
		{
			ValidateOrThrow();
			var (id, gen, absorb, length, infinite) = ConsumeTween(child);
			var start = buffer.Cursor + absorb;
			buffer.AddEntry(id, gen, start, length, infinite, SequenceChildKind.Tween, -1);
			buffer.SetAppendAnchors(start, infinite ? start : start + length);
			return this;
		}

		public SequenceBuilder Chain<T>(TweenBuilder<T> child) => Append(child);

		public SequenceBuilder Append(SequenceBuilder child)
		{
			ValidateOrThrow();
			var (id, gen, length) = ConsumeSequence(child);
			var start = buffer.Cursor;
			buffer.AddEntry(id, gen, start, length, false, SequenceChildKind.Tween, -1);
			buffer.SetAppendAnchors(start, start + length);
			return this;
		}

		public SequenceBuilder Join<T>(TweenBuilder<T> child)
		{
			ValidateOrThrow();
			var (id, gen, absorb, length, infinite) = ConsumeTween(child);
			var start = buffer.JoinAnchor + absorb;
			buffer.AddEntry(id, gen, start, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		public SequenceBuilder Group<T>(TweenBuilder<T> child) => Join(child);

		public SequenceBuilder Insert<T>(float time, TweenBuilder<T> child)
		{
			ValidateOrThrow();
			if (time < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(time), "Insert time cannot be negative; use SetDelay on the sequence to defer.");
			}
			var (id, gen, absorb, length, infinite) = ConsumeTween(child);
			buffer.AddEntry(id, gen, time + absorb, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		public SequenceBuilder Insert<T>(Position position, TweenBuilder<T> child)
		{
			ValidateOrThrow();
			var (id, gen, absorb, length, infinite) = ConsumeTween(child);
			AddPositionedEntry(id, gen, position, absorb, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		public SequenceBuilder Insert(float time, SequenceBuilder child)
		{
			ValidateOrThrow();
			if (time < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(time), "Insert time cannot be negative; use SetDelay on the sequence to defer.");
			}
			var (id, gen, length) = ConsumeSequence(child);
			buffer.AddEntry(id, gen, time, length, false, SequenceChildKind.Tween, -1);
			return this;
		}

		public SequenceBuilder Insert(Position position, SequenceBuilder child)
		{
			ValidateOrThrow();
			var (id, gen, length) = ConsumeSequence(child);
			AddPositionedEntry(id, gen, position, 0d, length, false, SequenceChildKind.Tween, -1);
			return this;
		}

		public SequenceBuilder AppendInterval(float seconds)
		{
			ValidateOrThrow();
			if (seconds < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(seconds), "Interval cannot be negative.");
			}
			buffer.AdvanceCursor(seconds);
			return this;
		}

		public SequenceBuilder AppendCallback(Action cb)
		{
			ValidateOrThrow();
			if (cb == null)
			{
				throw new ArgumentNullException(nameof(cb));
			}
			var cbIndex = buffer.AddCallbackSlot(cb);
			buffer.AddEntry(-1, 0, buffer.Cursor, 0d, false, SequenceChildKind.Callback, cbIndex);
			return this;
		}

		public SequenceBuilder Prepend<T>(TweenBuilder<T> child)
		{
			ValidateOrThrow();
			var (id, gen, absorb, length, infinite) = ConsumeTween(child);
			buffer.ShiftAll(absorb + (infinite ? 0d : length));
			buffer.AddEntry(id, gen, absorb, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		public SequenceBuilder PrependInterval(float seconds)
		{
			ValidateOrThrow();
			if (seconds < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(seconds), "Interval cannot be negative.");
			}
			buffer.ShiftAll(seconds);
			return this;
		}

		public SequenceBuilder PrependCallback(Action cb)
		{
			ValidateOrThrow();
			if (cb == null)
			{
				throw new ArgumentNullException(nameof(cb));
			}
			var cbIndex = buffer.AddCallbackSlot(cb);
			buffer.AddEntry(-1, 0, 0d, 0d, false, SequenceChildKind.Callback, cbIndex);
			return this;
		}

		public SequenceBuilder AddLabel(string name, float time)
		{
			ValidateOrThrow();
			ValidateLabelName(name);
			if (time < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(time), "Label time cannot be negative.");
			}
			if (buffer.TryResolveLabel(name, out _))
			{
				throw new ArgumentException($"[PATween] Label '{name}' is already defined.", nameof(name));
			}
			buffer.DefineLabel(name, time);
			return this;
		}

		public SequenceBuilder AddLabel(string name, Position position)
		{
			ValidateOrThrow();
			ValidateLabelName(name);
			if (buffer.TryResolveLabel(name, out _))
			{
				throw new ArgumentException($"[PATween] Label '{name}' is already defined.", nameof(name));
			}
			// Label positions resolve at definition time; referencing an
			// undefined label here throws (only Insert/AddPause defer).
			if (!TryResolvePosition(position, out var time))
			{
				throw new InvalidOperationException(
					$"[PATween] AddLabel position references undefined label '{position.Label}'.");
			}
			if (time < 0d)
			{
				throw new ArgumentOutOfRangeException(nameof(position), "Label time cannot be negative.");
			}
			buffer.DefineLabel(name, time);
			return this;
		}

		public SequenceBuilder AddPause(float time, Action onPause = null)
		{
			ValidateOrThrow();
			if (time < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(time), "Pause time cannot be negative.");
			}
			var cbIndex = buffer.AddCallbackSlot(onPause);
			buffer.AddEntry(-1, 0, time, 0d, false, SequenceChildKind.Pause, cbIndex);
			return this;
		}

		public SequenceBuilder AddPause(Position position, Action onPause = null)
		{
			ValidateOrThrow();
			var cbIndex = buffer.AddCallbackSlot(onPause);
			AddPositionedEntry(-1, 0, position, 0d, 0d, false, SequenceChildKind.Pause, cbIndex);
			return this;
		}

		public SequenceBuilder Clear(bool labels = false)
		{
			ValidateOrThrow();
			buffer.ClearEntries(labels);
			return this;
		}

		public Sequence Start()
		{
			ValidateOrThrow();

			var data = buffer.Build();

			var (id, gen) = TweenStore.Allocate();
			TweenStore.SetData(id, data);

			SequenceBuilderBufferPool.Return(buffer);
			buffer = null;
			generation = 0;

			return new Sequence(id, gen);
		}

		private void AddPositionedEntry(
			int id, uint gen, Position position, double absorb, double length, bool infinite,
			SequenceChildKind kind, int callbackIndex)
		{
			if (position.Kind == Position.PositionKind.Label
				&& !buffer.TryResolveLabel(position.Label, out _))
			{
				var index = buffer.AddEntry(id, gen, double.NaN, length, infinite, kind, callbackIndex);
				buffer.DeferEntryToLabel(index, position.Label, position.Time + absorb);
				return;
			}

			TryResolvePosition(position, out var start);
			start += absorb;
			if (start < 0d)
			{
				throw new ArgumentOutOfRangeException(
					nameof(position), "Position resolves to a negative time.");
			}
			buffer.AddEntry(id, gen, start, length, infinite, kind, callbackIndex);
		}

		private bool TryResolvePosition(Position position, out double time)
		{
			switch (position.Kind)
			{
				case Position.PositionKind.Time:
					time = position.Time;
					return true;
				case Position.PositionKind.Label:
					if (buffer.TryResolveLabel(position.Label, out var labelTime))
					{
						time = labelTime + position.Time;
						return true;
					}
					time = 0d;
					return false;
				case Position.PositionKind.AfterPrevious:
					time = buffer.LastAddedEnd + position.Time;
					return true;
				case Position.PositionKind.WithPrevious:
					time = buffer.LastAddedStart + position.Time;
					return true;
				default: // End
					time = buffer.Duration + position.Time;
					return true;
			}
		}

		private (int id, uint gen, double absorb, double length, bool infinite) ConsumeTween<T>(TweenBuilder<T> child)
		{
			var childBuffer = child.Buffer;
			if (childBuffer == null || childBuffer.Released || childBuffer.Generation != child.Generation)
			{
				throw new InvalidOperationException(
					"[PATween] Child builder was already consumed (started or appended elsewhere).");
			}

			if (!childBuffer.PhaseExplicit)
			{
				childBuffer.ApplyInheritedPhase(buffer.Phase, buffer.IgnoreTimeScale);
			}
			else if (childBuffer.Phase != buffer.Phase)
			{
				var childPhase = childBuffer.Phase;
				TweenBuilderBufferPool<T>.Return(childBuffer);
				throw new ArgumentException(
					$"[PATween] Child update phase {childPhase} does not match sequence phase {buffer.Phase}.");
			}

			buffer.ApplyDefaults(childBuffer);

			var data = childBuffer.Build();
			data.AutoKill = false;

			var absorb = 0d;
			if (data.DelayType == DelayType.FirstLoop && data.Delay > 0f)
			{
				absorb = data.Delay;
				data.Delay = 0f;
				data.Status = TweenStatus.Playing;
			}

			var infinite = data.LoopCount < 0;
			var length = infinite
				? 0d
				: (double)data.Duration * data.LoopCount
					+ (data.DelayType == DelayType.EveryLoop ? (double)data.Delay * data.LoopCount : 0d);

			var (id, gen) = TweenStore.Allocate();
			TweenStore.SetDataDetached(id, data);
			TweenBuilderBufferPool<T>.Return(childBuffer);

			return (id, gen, absorb, length, infinite);
		}

		private (int id, uint gen, double length) ConsumeSequence(SequenceBuilder child)
		{
			var childBuffer = child.Buffer;
			if (childBuffer == null || childBuffer == buffer)
			{
				throw new InvalidOperationException(
					"[PATween] Cannot nest a sequence builder into itself or pass an invalid builder.");
			}
			if (childBuffer.Released || childBuffer.Generation != child.Generation)
			{
				throw new InvalidOperationException(
					"[PATween] Child sequence builder was already consumed (started or appended elsewhere).");
			}

			if (!childBuffer.PhaseExplicit)
			{
				childBuffer.ApplyInheritedPhase(buffer.Phase, buffer.IgnoreTimeScale);
			}
			else if (childBuffer.Phase != buffer.Phase)
			{
				var childPhase = childBuffer.Phase;
				childBuffer.ClearEntries(clearLabels: true);
				SequenceBuilderBufferPool.Return(childBuffer);
				throw new ArgumentException(
					$"[PATween] Child sequence phase {childPhase} does not match parent phase {buffer.Phase}.");
			}

			var data = childBuffer.Build();
			data.AutoKill = false;
			var length = childBuffer.Delay + data.Duration;

			var (id, gen) = TweenStore.Allocate();
			TweenStore.SetDataDetached(id, data);
			SequenceBuilderBufferPool.Return(childBuffer);

			return (id, gen, length);
		}

		private static void ValidateLabelName(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				throw new ArgumentException("Label name cannot be null or empty.", nameof(name));
			}
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
					"[PATween] SequenceBuilder used after Start() or invalid alias.");
			}
		}
	}
}
