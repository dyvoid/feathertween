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

		public bool Equals(Tween other) => id == other.id && generation == other.generation;

		public override bool Equals(object obj) => obj is Tween other && Equals(other);

		public override int GetHashCode() => unchecked((int)(id * 397 ^ generation));

		public static bool operator ==(Tween a, Tween b) => a.Equals(b);

		public static bool operator !=(Tween a, Tween b) => !a.Equals(b);
	}
}
