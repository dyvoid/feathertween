using System;
using System.Collections.Generic;

namespace dyvoid.FeatherTween.Internal
{
	// Registry so TweenStore.Reset can clear every per-type pool without
	// knowing the closed generic types (Fast Enter Play Mode hygiene).
	internal static class TweenDataPoolRegistry
	{
		private static readonly List<Action> clearers = new List<Action>();

		public static void Register(Action clear)
		{
			clearers.Add(clear);
		}

		public static void ClearAll()
		{
			for (var i = 0; i < clearers.Count; i++)
			{
				clearers[i]();
			}
		}
	}

	// Per-type pool for TweenData<T> records so Start() is alloc-free after
	// warmup (Documentation~/architecture/performance.md). Instances are Reset() on return, so Rent hands out a
	// clean record. Returns are deferred by TweenStore until end of tick, so
	// an in-flight walk never sees its instance re-rented mid-step.
	internal static class TweenDataPool<T>
	{
		private const int MaxPooled = 1024;

		private static readonly Stack<TweenData<T>> pool = new Stack<TweenData<T>>(32);

		static TweenDataPool()
		{
			TweenDataPoolRegistry.Register(pool.Clear);
		}

		public static TweenData<T> Rent()
		{
			return pool.Count > 0 ? pool.Pop() : new TweenData<T>();
		}

		public static void Return(TweenData<T> data)
		{
			if (pool.Count >= MaxPooled)
			{
				return;
			}
			data.Reset();
			pool.Push(data);
		}
	}
}
