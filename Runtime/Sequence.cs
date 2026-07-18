using System;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween
{
	/// <summary>
	/// Immutable handle to a running sequence, returned by <c>SequenceBuilder.Start()</c>.
	/// Generation-checked: after the sequence dies, every method no-ops and every
	/// property reads a dead default — a stale handle is always safe to hold.
	/// </summary>
	public readonly struct Sequence : IEquatable<Sequence>
	{
		private readonly int id;
		private readonly uint generation;

		internal int Id => id;
		internal uint Generation => generation;

		/// <summary>True while the handle refers to a live (not yet recycled) sequence.</summary>
		public bool IsAlive => TweenStore.IsAlive(id, generation);

		/// <summary>Current lifecycle state; <see cref="TweenStatus.Disposed"/> when dead.</summary>
		public TweenStatus Status
		{
			get
			{
				var data = TweenStore.Get(id, generation);
				return data == null ? TweenStatus.Disposed : data.Status;
			}
		}

		/// <summary>Progress across all loops [0,1]; <see cref="Duration"/> stays a single cycle.</summary>
		public float TotalProgress
		{
			get
			{
				var data = TweenStore.Get(id, generation);
				return data == null ? 0f : data.TotalProgress;
			}
		}

		/// <summary>One cycle in seconds, excluding the initial delay.</summary>
		public float Duration
		{
			get
			{
				var data = TweenStore.Get(id, generation);
				return data == null ? 0f : (float)data.CycleDuration;
			}
		}

		internal Sequence(int id, uint generation)
		{
			this.id = id;
			this.generation = generation;
		}

		/// <summary>Starts playback if the sequence was created paused or is delayed.</summary>
		public void Play() => TweenOps.Play(id, generation);

		/// <summary>Halts playback, keeping the playhead; resume with <see cref="Resume"/>.</summary>
		public void Pause() => TweenOps.Pause(id, generation);

		/// <summary>Resumes playback from the paused playhead (also releases an <c>AddPause</c> halt).</summary>
		public void Resume() => TweenOps.Resume(id, generation);

		/// <summary>Rewinds to the start (including the initial delay) and plays forward.</summary>
		public void Restart() => TweenOps.Restart(id, generation);

		/// <summary>Jumps to the end of the final cycle, applies end state, and fires <c>OnComplete</c>.</summary>
		public void Complete() => TweenOps.Complete(id, generation);

		/// <summary>Per-sequence playback rate; composes with phase and global scales and cascades to children.</summary>
		public void SetTimeScale(float scale) => TweenOps.SetTimeScale(id, generation, scale);

		/// <summary>Repositions the playhead in post-delay sequence time, spanning all loops. Preserves play/pause state; with <paramref name="fireCallbacks"/> a crossed <c>AddPause</c> halts the seek there.</summary>
		public void Seek(float time, bool fireCallbacks = false) => TweenOps.Seek(id, generation, time, fireCallbacks);

		/// <summary>Flips playback direction mid-flight; delays are part of the timeline and are traversed backward too.</summary>
		public void Reverse() => TweenOps.Reverse(id, generation);

		/// <summary>Absolute remaining cycle count, counting the in-progress cycle as the first; negative means loop forever.</summary>
		public void SetRemainingCycles(int cycles)
		{
			var data = TweenStore.Get(id, generation);
			data?.SetRemainingCyclesAbsolute(cycles);
		}

		/// <summary>Graceful stop for looping sequences: complete at the next cycle boundary going forward, settling on the end state.</summary>
		public void CompleteAtCycleEnd()
		{
			var data = TweenStore.Get(id, generation);
			data?.SetStopAtNextBoundary(true);
		}

		/// <summary>Graceful stop for looping sequences: complete on a backward/reversed cycle crossing, settling on the start state.</summary>
		public void CompleteAtCycleStart()
		{
			var data = TweenStore.Get(id, generation);
			data?.SetStopAtNextBoundary(false);
		}

		/// <summary>Mid-play insertion of a tween at an absolute sequence time. The builder is consumed. Structural mutation: deferred to end of tick when called from inside a callback. Unlike build-time composition, <c>SetDefaults</c> values from the original builder are not applied (they live in the consumed builder, not the running sequence) — set ease/loops/delay explicitly on the child.</summary>
		public void Insert<T>(float time, TweenBuilder<T> tween)
		{
			if (time < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(time), "Insert time cannot be negative.");
			}
			var data = TweenStore.Get(id, generation) as SequenceData;
			if (data == null)
			{
				throw new InvalidOperationException("[FeatherTween] Insert on a dead or invalid sequence.");
			}

			var childBuffer = tween.Buffer;
			if (childBuffer == null || childBuffer.Released || childBuffer.Generation != tween.Generation)
			{
				throw new InvalidOperationException(
					"[FeatherTween] Child builder was already consumed (started or appended elsewhere).");
			}

			if (!childBuffer.PhaseExplicit)
			{
				childBuffer.ApplyInheritedPhase(data.Phase, data.IgnoreTimeScale);
			}
			else if (childBuffer.Phase != data.Phase)
			{
				var childPhase = childBuffer.Phase;
				TweenBuilderBufferPool<T>.Return(childBuffer);
				throw new ArgumentException(
					$"[FeatherTween] Child update phase {childPhase} does not match sequence phase {data.Phase}.");
			}

			var childData = childBuffer.Build();
			childData.AutoKill = false;

			double start = time;
			if (childData.DelayType == DelayType.FirstLoop && childData.Delay > 0f)
			{
				// Absorb the first-loop delay into the window start, matching
				// build-time Append/Insert semantics.
				start += childData.Delay;
				childData.Delay = 0f;
				childData.Status = TweenStatus.Playing;
			}

			var infinite = childData.LoopCount < 0;
			var length = infinite
				? 0d
				: (double)childData.Duration * childData.LoopCount
					+ (childData.DelayType == DelayType.EveryLoop ? (double)childData.Delay * childData.LoopCount : 0d);

			var (childId, childGen) = TweenStore.Allocate();
			TweenStore.SetDataDetached(childId, childData);
			TweenBuilderBufferPool<T>.Return(childBuffer);

			if (TweenCommandQueue.TryDeferInsert(id, generation, childId, childGen, start, length, infinite))
			{
				return;
			}
			data.InsertChild(childId, childGen, start, length, infinite);
		}

		/// <summary>Stops and frees the sequence and its children; with <paramref name="complete"/> it first jumps to its end state.</summary>
		public void Kill(bool complete = false) => TweenOps.Kill(id, generation, complete);

		// Late subscriptions on a dead handle are no-ops: the handle cannot know
		// whether its record completed or was killed, so firing either callback
		// would be a guess (phase 1.15; Documentation~/api/handles.md).
		/// <summary>Subscribes to completion. On a dead handle this is a no-op (it cannot know whether the record completed or was killed).</summary>
		public Sequence OnComplete(Action cb)
		{
			var data = TweenStore.Get(id, generation);
			data?.AddOnComplete(cb);
			return this;
		}

		/// <summary>Subscribes to kill (not fired on completion). No-op on a dead handle.</summary>
		public Sequence OnKill(Action cb)
		{
			var data = TweenStore.Get(id, generation);
			data?.AddOnKill(cb);
			return this;
		}

		/// <summary>Subscribes to per-cycle completion (each loop boundary). No-op on a dead handle.</summary>
		public Sequence OnStepComplete(Action cb)
		{
			var data = TweenStore.Get(id, generation);
			data?.AddOnStepComplete(cb);
			return this;
		}

		/// <summary>Two handles are equal when they refer to the same sequence instance (same slot and generation).</summary>
		public bool Equals(Sequence other) => id == other.id && generation == other.generation;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is Sequence other && Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode() => unchecked((int)(id * 397 ^ generation));

		/// <summary>Handle equality (same slot and generation).</summary>
		public static bool operator ==(Sequence a, Sequence b) => a.Equals(b);

		/// <summary>Handle inequality.</summary>
		public static bool operator !=(Sequence a, Sequence b) => !a.Equals(b);
	}
}
