namespace dyvoid.FeatherTween
{
	/// <summary>How a tween or sequence traverses cycles when looping.</summary>
	public enum LoopType
	{
		/// <summary>Every cycle plays forward from the start value.</summary>
		Restart,
		/// <summary>Alternate cycles play backward (ping-pong). On a tween the return leg applies the same ease from end to start, so an <c>OutCubic</c> tween decelerates into both ends; see <see cref="Rewind"/> for a time-reversed return. A sequence walks its timeline backward, which is time-reversed by nature.</summary>
		Yoyo,
		/// <summary>Each cycle adds the end-start delta on top of the previous cycle's landing value. Tween-only; sequences treat it as <see cref="Restart"/>.</summary>
		Incremental,
		/// <summary>Alternate cycles replay the previous cycle backward in time, as if time ran in reverse: the ease is mirrored, so an <c>OutCubic</c> tween decelerates into the end, then leaves it slowly and arrives at the start fast. Compare <see cref="Yoyo"/>, which applies the ease forward on the return leg. On a sequence this is the same as <see cref="Yoyo"/>.</summary>
		Rewind,
	}
}
