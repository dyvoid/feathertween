using System;

namespace Dyvoid.FeatherTween
{
	/// <summary>
	/// A position on a sequence's timeline, used by <c>Insert</c>, <c>AddLabel</c>
	/// and <c>AddPause</c>: an absolute time, a label (with offset), relative to
	/// the previously added child, or the current end of the sequence.
	/// </summary>
	public readonly struct Position
	{
		internal enum PositionKind : byte
		{
			End = 0,
			Time = 1,
			Label = 2,
			AfterPrevious = 3,
			WithPrevious = 4,
		}

		private readonly PositionKind kind;
		private readonly float time;
		private readonly string label;

		internal PositionKind Kind => kind;
		internal float Time => time;
		internal string Label => label;

		private Position(PositionKind kind, float time, string label)
		{
			this.kind = kind;
			this.time = time;
			this.label = label;
		}

		/// <summary>The current end of the sequence (its duration at resolution time).</summary>
		public static Position End => new Position(PositionKind.End, 0f, null);

		/// <summary>An absolute time on the sequence's timeline, in seconds.</summary>
		public static Position AtTime(float time)
		{
			if (time < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(time), "Position time cannot be negative.");
			}
			return new Position(PositionKind.Time, time, null);
		}

		/// <summary>The time of a label (plus <paramref name="offset"/> seconds). Undefined labels resolve when the sequence starts; only <c>Insert</c>/<c>AddPause</c> may reference a label defined later.</summary>
		public static Position AtLabel(string label, float offset = 0f)
		{
			if (string.IsNullOrEmpty(label))
			{
				throw new ArgumentException("Label cannot be null or empty.", nameof(label));
			}
			return new Position(PositionKind.Label, offset, label);
		}

		/// <summary>The end of the most recently added child (plus <paramref name="offset"/> seconds).</summary>
		public static Position AfterPrevious(float offset = 0f)
		{
			return new Position(PositionKind.AfterPrevious, offset, null);
		}

		/// <summary>The start of the most recently added child (plus <paramref name="offset"/> seconds).</summary>
		public static Position WithPrevious(float offset = 0f)
		{
			return new Position(PositionKind.WithPrevious, offset, null);
		}
	}
}
