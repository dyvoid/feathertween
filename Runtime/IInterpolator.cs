namespace dyvoid.FeatherTween
{
	/// <summary>
	/// Value math for a tweenable type <typeparamref name="T"/>. Register custom
	/// implementations via <see cref="Interpolators.Register{T}"/>.
	/// </summary>
	public interface IInterpolator<T>
	{
		/// <summary>Interpolates between <paramref name="from"/> and <paramref name="to"/> at eased progress <paramref name="t"/> (0..1, may over/undershoot for elastic-style eases).</summary>
		T Lerp(T from, T to, float t);
		/// <summary>Component-wise addition; used by <c>SetRelative</c> and <see cref="LoopType.Incremental"/> loops.</summary>
		T Add(T a, T b);
		/// <summary>Component-wise subtraction; used to derive deltas for <see cref="LoopType.Incremental"/> loops.</summary>
		T Subtract(T a, T b);
	}
}
