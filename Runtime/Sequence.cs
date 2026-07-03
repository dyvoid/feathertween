using System;
using PATween.Internal;

namespace PATween
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

		public float Duration
		{
			get
			{
				var data = TweenStore.Get(id, generation) as SequenceData;
				return data == null ? 0f : (float)data.Duration;
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
