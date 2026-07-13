using System;

namespace Dyvoid.FeatherTween
{
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

		public static Position End => new Position(PositionKind.End, 0f, null);

		public static Position AtTime(float seconds)
		{
			if (seconds < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(seconds), "Position time cannot be negative.");
			}
			return new Position(PositionKind.Time, seconds, null);
		}

		public static Position AtLabel(string label, float offset = 0f)
		{
			if (string.IsNullOrEmpty(label))
			{
				throw new ArgumentException("Label cannot be null or empty.", nameof(label));
			}
			return new Position(PositionKind.Label, offset, label);
		}

		public static Position AfterPrevious(float offset = 0f)
		{
			return new Position(PositionKind.AfterPrevious, offset, null);
		}

		public static Position WithPrevious(float offset = 0f)
		{
			return new Position(PositionKind.WithPrevious, offset, null);
		}
	}
}
