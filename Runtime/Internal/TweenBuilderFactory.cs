namespace dyvoid.FeatherTween.Internal
{
	internal static class TweenBuilderFactory
	{
		public static TweenBuilder<T> Create<T>()
		{
			var buf = TweenBuilderBufferPool<T>.Rent();
			return new TweenBuilder<T>(buf);
		}
	}
}
