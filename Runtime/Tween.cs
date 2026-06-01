using System;
using PATween.Internal;

namespace PATween
{
	public readonly struct Tween : IEquatable<Tween>
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

		internal Tween(int id, uint generation)
		{
			this.id = id;
			this.generation = generation;
		}

		public void Play()
		{
			var data = TweenStore.Get(id, generation);
			if (data == null)
			{
				return;
			}
			if (data.Status == TweenStatus.Paused)
			{
				data.Status = TweenStatus.Playing;
				return;
			}
			if (data.Status == TweenStatus.Completed)
			{
				data.ResetPlayhead();
				data.Status = data.StartsDelayed() ? TweenStatus.Delayed : TweenStatus.Playing;
			}
		}

		public void Pause()
		{
			var data = TweenStore.Get(id, generation);
			if (data == null)
			{
				return;
			}
			if (data.Status == TweenStatus.Playing || data.Status == TweenStatus.Delayed)
			{
				data.Status = TweenStatus.Paused;
			}
		}

		public void Resume()
		{
			var data = TweenStore.Get(id, generation);
			if (data == null)
			{
				return;
			}
			if (data.Status == TweenStatus.Paused)
			{
				data.Status = TweenStatus.Playing;
			}
		}

		public void Restart()
		{
			var data = TweenStore.Get(id, generation);
			if (data == null)
			{
				return;
			}
			if (data.Status == TweenStatus.Disposed)
			{
				return;
			}
			data.ResetPlayhead();
			data.Status = data.StartsDelayed() ? TweenStatus.Delayed : TweenStatus.Playing;
		}

		public void Reverse()
		{
			var data = TweenStore.Get(id, generation);
			if (data == null)
			{
				return;
			}
			data.Direction = -data.Direction;
		}

		public void SetRemainingCycles(int cycles)
		{
			var data = TweenStore.Get(id, generation);
			data?.SetRemainingCyclesAbsolute(cycles);
		}

		public void SetRemainingCycles(bool stopAtEndValue)
		{
			var data = TweenStore.Get(id, generation);
			data?.SetStopAtNextBoundary(stopAtEndValue);
		}

		public void Complete()
		{
			var data = TweenStore.Get(id, generation);
			if (data == null)
			{
				return;
			}
			if (data.Status == TweenStatus.Disposed || data.Status == TweenStatus.Cancelled)
			{
				return;
			}
			var alreadyCompleted = data.Status == TweenStatus.Completed;
			if (!alreadyCompleted)
			{
				data.ForceComplete();
			}
			data.Status = TweenStatus.Completed;
			if (!alreadyCompleted)
			{
				data.InvokeOnComplete();
			}
			if (data.AutoKill)
			{
				data.InvokeOnKill();
				TweenStore.Free(id);
			}
		}

		public void Kill(bool complete = false)
		{
			var data = TweenStore.Get(id, generation);
			if (data == null)
			{
				return;
			}
			if (complete && data.Status != TweenStatus.Completed && data.Status != TweenStatus.Cancelled)
			{
				data.ForceComplete();
				data.Status = TweenStatus.Completed;
				data.InvokeOnComplete();
			}
			else if (!complete && data.Status != TweenStatus.Completed)
			{
				data.Status = TweenStatus.Cancelled;
			}
			data.InvokeOnKill();
			TweenStore.Free(id);
		}

		public Tween OnComplete(Action cb)
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

		public Tween OnKill(Action cb)
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

		public bool Equals(Tween other) => id == other.id && generation == other.generation;

		public override bool Equals(object obj) => obj is Tween other && Equals(other);

		public override int GetHashCode() => unchecked((int)(id * 397 ^ generation));

		public static bool operator ==(Tween a, Tween b) => a.Equals(b);

		public static bool operator !=(Tween a, Tween b) => !a.Equals(b);
	}
}
