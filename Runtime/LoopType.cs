namespace Dyvoid.FeatherTween
{
	/// <summary>How a tween or sequence traverses cycles when looping.</summary>
	public enum LoopType
	{
		/// <summary>Every cycle plays forward from the start value.</summary>
		Restart,
		/// <summary>Alternate cycles play backward (ping-pong).</summary>
		Yoyo,
		/// <summary>Each cycle adds the end-start delta on top of the previous cycle's landing value. Tween-only; sequences treat it as <see cref="Restart"/>.</summary>
		Incremental,
		/// <summary>Like <see cref="Restart"/>, but the value snaps back to the start at the cycle boundary instead of holding the end.</summary>
		Rewind,
	}
}
