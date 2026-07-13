namespace Dyvoid.FeatherTween
{
	public interface IInterpolator<T>
	{
		T Lerp(T from, T to, float t);
		T Add(T a, T b);
		T Subtract(T a, T b);
	}
}
