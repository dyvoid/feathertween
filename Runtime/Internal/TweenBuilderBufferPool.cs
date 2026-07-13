using System.Collections.Generic;

namespace Dyvoid.FeatherTween.Internal
{
	internal static class TweenBuilderBufferPool<T>
	{
		private static readonly Stack<TweenBuilderBuffer<T>> pool = new Stack<TweenBuilderBuffer<T>>();

		public static int PoolSize => pool.Count;

		public static TweenBuilderBuffer<T> Rent()
		{
			TweenBuilderBuffer<T> buf;
			if (pool.Count > 0)
			{
				buf = pool.Pop();
			}
			else
			{
				buf = new TweenBuilderBuffer<T>();
			}
			buf.Rent();
			return buf;
		}

		public static void Return(TweenBuilderBuffer<T> buf)
		{
			buf.Release();
			pool.Push(buf);
		}
	}
}
