using System.Collections.Generic;

namespace dyvoid.FeatherTween.Internal
{
	internal static class SequenceBuilderBufferPool
	{
		private static readonly Stack<SequenceBuilderBuffer> pool = new Stack<SequenceBuilderBuffer>();

		public static int PoolSize => pool.Count;

		public static SequenceBuilderBuffer Rent()
		{
			SequenceBuilderBuffer buf;
			if (pool.Count > 0)
			{
				buf = pool.Pop();
			}
			else
			{
				buf = new SequenceBuilderBuffer();
			}
			buf.Rent();
			return buf;
		}

		public static void Return(SequenceBuilderBuffer buf)
		{
			buf.Release();
			pool.Push(buf);
		}
	}
}
