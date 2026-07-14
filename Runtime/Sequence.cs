using System;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween
{
	public readonly struct Sequence : IEquatable<Sequence>
	{
		private readonly int id;
		private readonly uint generation;

		internal int Id => id;
		internal uint Generation => generation;

		public bool IsAlive => TweenStore.IsAlive(id, generation);

		public TweenStatus Status
		{
			get
			{
				var data = TweenStore.Get(id, generation);
				return data == null ? TweenStatus.Disposed : data.Status;
			}
		}

		/// Progress across all loops [0,1]; Duration stays a single cycle.
		public float TotalProgress
		{
			get
			{
				var data = TweenStore.Get(id, generation);
				return data == null ? 0f : data.TotalProgress;
			}
		}

		/// One cycle in seconds, excluding the initial delay.
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

		public void Play() => TweenOps.Play(id, generation);

		public void Pause() => TweenOps.Pause(id, generation);

		public void Resume() => TweenOps.Resume(id, generation);

		public void Restart() => TweenOps.Restart(id, generation);

		public void Complete() => TweenOps.Complete(id, generation);

		public void SetTimeScale(float scale) => TweenOps.SetTimeScale(id, generation, scale);

		/// Repositions the playhead in post-delay sequence time, spanning all
		/// loops. Preserves play/pause state; with fireCallbacks a crossed
		/// AddPause halts the seek there (docs/api/handles.md).
		public void Seek(float time, bool fireCallbacks = false) => TweenOps.Seek(id, generation, time, fireCallbacks);

		public void Reverse() => TweenOps.Reverse(id, generation);

		/// Absolute remaining cycle count, counting the in-progress cycle as
		/// the first; negative means loop forever.
		public void SetRemainingCycles(int cycles)
		{
			var data = TweenStore.Get(id, generation);
			data?.SetRemainingCyclesAbsolute(cycles);
		}

		/// Graceful stop for looping sequences: complete at the next cycle
		/// boundary in the travel direction (true stops going forward, false
		/// stops on a backward/reversed crossing).
		public void SetRemainingCycles(bool stopAtEndValue)
		{
			var data = TweenStore.Get(id, generation);
			data?.SetStopAtNextBoundary(stopAtEndValue);
		}

		/// Mid-play insertion of a tween at an absolute sequence time. The
		/// builder is consumed. Structural mutation: deferred to end of tick when
		/// called from inside a callback (docs/api/handles.md).
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

		public void Kill(bool complete = false) => TweenOps.Kill(id, generation, complete);

		public Sequence OnComplete(Action cb)
		{
			var data = TweenStore.Get(id, generation);
			if (data == null)
			{
				cb?.Invoke();
				return this;
			}
			data.AddOnComplete(cb);
			return this;
		}

		public Sequence OnKill(Action cb)
		{
			var data = TweenStore.Get(id, generation);
			if (data == null)
			{
				cb?.Invoke();
				return this;
			}
			data.AddOnKill(cb);
			return this;
		}

		public Sequence OnStepComplete(Action cb)
		{
			var data = TweenStore.Get(id, generation);
			data?.AddOnStepComplete(cb);
			return this;
		}

		public bool Equals(Sequence other) => id == other.id && generation == other.generation;

		public override bool Equals(object obj) => obj is Sequence other && Equals(other);

		public override int GetHashCode() => unchecked((int)(id * 397 ^ generation));

		public static bool operator ==(Sequence a, Sequence b) => a.Equals(b);

		public static bool operator !=(Sequence a, Sequence b) => !a.Equals(b);
	}
}
