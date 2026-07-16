using System;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween
{
	/// <summary>
	/// Mutable composition surface for a sequence: append, join, insert and
	/// prepend tweens, nested sequences, intervals, callbacks, labels and pauses,
	/// then call <see cref="Start"/> for a <see cref="Sequence"/> handle. Copies
	/// alias one shared backing record; after the builder is consumed (started or
	/// nested into another sequence), every alias is invalid and further use throws.
	/// Child builders passed to composition methods are consumed too.
	/// </summary>
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

		/// <summary>Selects the driving <see cref="UpdatePhase"/>; children inherit it, and an explicitly mismatched child throws at append.</summary>
		public SequenceBuilder SetUpdate(UpdatePhase phase, bool ignoreTimeScale = false)
		{
			ValidateOrThrow();
			buffer.Phase = phase;
			buffer.IgnoreTimeScale = ignoreTimeScale;
			return this;
		}

		/// <summary>Whether the sequence frees itself on completion (default true). Disable to keep it seekable/restartable; kill it explicitly when done.</summary>
		public SequenceBuilder SetAutoKill(bool value)
		{
			ValidateOrThrow();
			buffer.AutoKill = value;
			return this;
		}

		/// <summary>Associates a target for <c>FT.Kill(target)</c>/<c>FT.IsTweening(target)</c>; destroyed <c>UnityEngine.Object</c> targets auto-kill the sequence.</summary>
		public SequenceBuilder SetTarget(object target)
		{
			ValidateOrThrow();
			buffer.Target = target;
			return this;
		}

		/// <summary>Defers playback by <paramref name="seconds"/> before the first cycle. Negative values throw.</summary>
		public SequenceBuilder SetDelay(float seconds)
		{
			ValidateOrThrow();
			if (seconds < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(seconds), "Delay cannot be negative.");
			}
			buffer.Delay = seconds;
			return this;
		}

		/// <summary>Sequence-level looping. Yoyo traverses children in reverse window order on odd cycles; Incremental has no sequence-level meaning and is treated as Restart. Negative <paramref name="count"/> loops forever.</summary>
		public SequenceBuilder SetLoops(int count, LoopType loopType = LoopType.Restart)
		{
			ValidateOrThrow();
			buffer.SetLoops(count, loopType);
			return this;
		}

		/// <summary>Try/catch around the sequence's own callback invocations (children carry their own flag). See <c>TweenBuilder&lt;T&gt;.SetSafeMode</c>.</summary>
		public SequenceBuilder SetSafeMode(bool value)
		{
			ValidateOrThrow();
			buffer.SafeMode = value;
			return this;
		}

		/// <summary>In safe mode, a callback exception is logged and cancels the sequence.</summary>
		public SequenceBuilder SetCancelOnError(bool value)
		{
			ValidateOrThrow();
			buffer.CancelOnError = value;
			return this;
		}

		/// <summary>How the sequence reacts when a child is auto-killed (e.g. destroyed target); see <see cref="SequenceCancelBehavior"/>.</summary>
		public SequenceBuilder SetCancelBehavior(SequenceCancelBehavior behavior)
		{
			ValidateOrThrow();
			buffer.CancelBehavior = behavior;
			return this;
		}

		/// <summary>Default ease/loops/delay applied to children appended after this call (frozen per child at append). No duration default: every creation method requires an explicit duration (ADR 0010).</summary>
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

		/// <summary>Called once, when playback first begins (after any initial delay).</summary>
		public SequenceBuilder OnStart(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnStart(cb);
			return this;
		}

		/// <summary>Called whenever playback begins or resumes.</summary>
		public SequenceBuilder OnPlay(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnPlay(cb);
			return this;
		}

		/// <summary>Called whenever playback pauses (including <c>AddPause</c> halts).</summary>
		public SequenceBuilder OnPause(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnPause(cb);
			return this;
		}

		/// <summary>Called every tick with the normalized playhead (playhead / duration, 0..1).</summary>
		public SequenceBuilder OnUpdate(Action<float> cb)
		{
			ValidateOrThrow();
			buffer.AddOnUpdate(cb);
			return this;
		}

		/// <summary>Called at the end of each loop cycle.</summary>
		public SequenceBuilder OnStepComplete(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnStepComplete(cb);
			return this;
		}

		/// <summary>Called when the playhead returns to the start (reverse playback or rewind).</summary>
		public SequenceBuilder OnRewind(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnRewind(cb);
			return this;
		}

		/// <summary>Called once, when the final cycle finishes. Not called on kill.</summary>
		public SequenceBuilder OnComplete(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnComplete(cb);
			return this;
		}

		/// <summary>Zero-alloc target-capture overload: pass state explicitly and use a static lambda so the compiler emits no closure.</summary>
		public SequenceBuilder OnComplete<TTarget>(TTarget state, Action<TTarget> cb)
			where TTarget : class
		{
			ValidateOrThrow();
			if (state != null && cb != null)
			{
				buffer.AddOnComplete(CallbackEntry.FromTargetCapture(state, cb));
			}
			return this;
		}

		/// <summary>Called when the sequence is killed (not on completion).</summary>
		public SequenceBuilder OnKill(Action cb)
		{
			ValidateOrThrow();
			buffer.AddOnKill(cb);
			return this;
		}

		/// <summary>Zero-alloc target-capture overload of <see cref="OnKill(Action)"/>.</summary>
		public SequenceBuilder OnKill<TTarget>(TTarget state, Action<TTarget> cb)
			where TTarget : class
		{
			ValidateOrThrow();
			if (state != null && cb != null)
			{
				buffer.AddOnKill(CallbackEntry.FromTargetCapture(state, cb));
			}
			return this;
		}

		/// <summary>Adds the tween at the current end of the sequence; the cursor advances past it. Consumes <paramref name="child"/>.</summary>
		public SequenceBuilder Append<T>(TweenBuilder<T> child)
		{
			ValidateOrThrow();
			var (id, gen, absorb, length, infinite) = ConsumeTween(child);
			var start = buffer.Cursor + absorb;
			buffer.AddEntry(id, gen, start, length, infinite, SequenceChildKind.Tween, -1);
			buffer.SetAppendAnchors(start, infinite ? start : start + length);
			return this;
		}

		/// <summary>Adds a nested sequence at the current end; the cursor advances past all its cycles. Consumes <paramref name="child"/>.</summary>
		public SequenceBuilder Append(SequenceBuilder child)
		{
			ValidateOrThrow();
			var (id, gen, length, infinite) = ConsumeSequence(child);
			var start = buffer.Cursor;
			buffer.AddEntry(id, gen, start, length, infinite, SequenceChildKind.Tween, -1);
			buffer.SetAppendAnchors(start, infinite ? start : start + length);
			return this;
		}

		/// <summary>Adds the tween starting alongside the most recently appended child. Consumes <paramref name="child"/>.</summary>
		public SequenceBuilder Join<T>(TweenBuilder<T> child)
		{
			ValidateOrThrow();
			var (id, gen, absorb, length, infinite) = ConsumeTween(child);
			var start = buffer.JoinAnchor + absorb;
			buffer.AddEntry(id, gen, start, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		/// <summary>Adds a nested sequence starting alongside the most recently appended child. Consumes <paramref name="child"/>.</summary>
		public SequenceBuilder Join(SequenceBuilder child)
		{
			ValidateOrThrow();
			var (id, gen, length, infinite) = ConsumeSequence(child);
			var start = buffer.JoinAnchor;
			buffer.AddEntry(id, gen, start, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		/// <summary>Adds the tween at an absolute time on the sequence's timeline. Consumes <paramref name="child"/>.</summary>
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

		/// <summary>Adds the tween at a <see cref="Position"/> (label references may resolve at <see cref="Start"/>). Consumes <paramref name="child"/>.</summary>
		public SequenceBuilder Insert<T>(Position position, TweenBuilder<T> child)
		{
			ValidateOrThrow();
			var (id, gen, absorb, length, infinite) = ConsumeTween(child);
			AddPositionedEntry(id, gen, position, absorb, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		/// <summary>Adds a nested sequence at an absolute time on the sequence's timeline. Consumes <paramref name="child"/>.</summary>
		public SequenceBuilder Insert(float time, SequenceBuilder child)
		{
			ValidateOrThrow();
			if (time < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(time), "Insert time cannot be negative; use SetDelay on the sequence to defer.");
			}
			var (id, gen, length, infinite) = ConsumeSequence(child);
			buffer.AddEntry(id, gen, time, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		/// <summary>Adds a nested sequence at a <see cref="Position"/> (label references may resolve at <see cref="Start"/>). Consumes <paramref name="child"/>.</summary>
		public SequenceBuilder Insert(Position position, SequenceBuilder child)
		{
			ValidateOrThrow();
			var (id, gen, length, infinite) = ConsumeSequence(child);
			AddPositionedEntry(id, gen, position, 0d, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		/// <summary>Advances the cursor by <paramref name="seconds"/> of empty time.</summary>
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

		/// <summary>Fires <paramref name="cb"/> when the playhead crosses the current cursor position (in either direction).</summary>
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

		/// <summary>Adds the tween at time 0, shifting all existing children later. Consumes <paramref name="child"/>.</summary>
		public SequenceBuilder Prepend<T>(TweenBuilder<T> child)
		{
			ValidateOrThrow();
			var (id, gen, absorb, length, infinite) = ConsumeTween(child);
			buffer.ShiftAll(absorb + (infinite ? 0d : length));
			buffer.AddEntry(id, gen, absorb, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		/// <summary>Adds a nested sequence at time 0, shifting all existing children later. Consumes <paramref name="child"/>.</summary>
		public SequenceBuilder Prepend(SequenceBuilder child)
		{
			ValidateOrThrow();
			var (id, gen, length, infinite) = ConsumeSequence(child);
			// The child's own delay lives inside its window (ConsumeSequence
			// folds it into length), so there is no absorb component here.
			buffer.ShiftAll(infinite ? 0d : length);
			buffer.AddEntry(id, gen, 0d, length, infinite, SequenceChildKind.Tween, -1);
			return this;
		}

		/// <summary>Shifts all existing children later by <paramref name="seconds"/> of empty time.</summary>
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

		/// <summary>Fires <paramref name="cb"/> when the playhead crosses time 0.</summary>
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

		/// <summary>Names an absolute time for later <c>Position.AtLabel</c> addressing. Duplicate names throw.</summary>
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
				throw new ArgumentException($"[FeatherTween] Label '{name}' is already defined.", nameof(name));
			}
			buffer.DefineLabel(name, time);
			return this;
		}

		/// <summary>Names a <see cref="Position"/>-addressed time. Resolves at definition time: referencing a label defined later throws (only <c>Insert</c>/<c>AddPause</c> defer).</summary>
		public SequenceBuilder AddLabel(string name, Position position)
		{
			ValidateOrThrow();
			ValidateLabelName(name);
			if (buffer.TryResolveLabel(name, out _))
			{
				throw new ArgumentException($"[FeatherTween] Label '{name}' is already defined.", nameof(name));
			}
			// Label positions resolve at definition time; referencing an
			// undefined label here throws (only Insert/AddPause defer).
			if (!TryResolvePosition(position, out var time))
			{
				throw new InvalidOperationException(
					$"[FeatherTween] AddLabel position references undefined label '{position.Label}'.");
			}
			if (time < 0d)
			{
				throw new ArgumentOutOfRangeException(nameof(position), "Label time cannot be negative.");
			}
			buffer.DefineLabel(name, time);
			return this;
		}

		/// <summary>Halts the playhead when it reaches <paramref name="time"/> going forward; resume with <c>Resume()</c> or a seek.</summary>
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

		/// <summary>Halts the playhead at a <see cref="Position"/> (label references may resolve at <see cref="Start"/>).</summary>
		public SequenceBuilder AddPause(Position position, Action onPause = null)
		{
			ValidateOrThrow();
			var cbIndex = buffer.AddCallbackSlot(onPause);
			AddPositionedEntry(-1, 0, position, 0d, 0d, false, SequenceChildKind.Pause, cbIndex);
			return this;
		}

		/// <summary>Removes all composed children (freeing their store slots); with <paramref name="labels"/> the label table is cleared too.</summary>
		public SequenceBuilder Clear(bool labels = false)
		{
			ValidateOrThrow();
			buffer.ClearEntries(labels);
			return this;
		}

		/// <summary>Consumes the builder, resolves deferred label positions, registers the sequence with the runner, and returns its handle.</summary>
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
					"[FeatherTween] Child builder was already consumed (started or appended elsewhere).");
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
					$"[FeatherTween] Child update phase {childPhase} does not match sequence phase {buffer.Phase}.");
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

		private (int id, uint gen, double length, bool infinite) ConsumeSequence(SequenceBuilder child)
		{
			var childBuffer = child.Buffer;
			if (childBuffer == null || childBuffer == buffer)
			{
				throw new InvalidOperationException(
					"[FeatherTween] Cannot nest a sequence builder into itself or pass an invalid builder.");
			}
			if (childBuffer.Released || childBuffer.Generation != child.Generation)
			{
				throw new InvalidOperationException(
					"[FeatherTween] Child sequence builder was already consumed (started or appended elsewhere).");
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
					$"[FeatherTween] Child sequence phase {childPhase} does not match parent phase {buffer.Phase}.");
			}

			var data = childBuffer.Build();
			data.AutoKill = false;
			// Window spans all of the child's cycles; an infinite child gets an
			// open window, mirroring ConsumeTween.
			var infinite = childBuffer.Loops < 0;
			var length = infinite
				? 0d
				: childBuffer.Delay + data.CycleDuration * childBuffer.Loops;

			var (id, gen) = TweenStore.Allocate();
			TweenStore.SetDataDetached(id, data);
			SequenceBuilderBufferPool.Return(childBuffer);

			return (id, gen, length, infinite);
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
					"[FeatherTween] SequenceBuilder used after Start() or invalid alias.");
			}
		}
	}
}
