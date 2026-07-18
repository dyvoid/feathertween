using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace dyvoid.FeatherTween.Internal
{
	internal static class LeakDetector
	{
		private static readonly ConcurrentQueue<int> queue = new ConcurrentQueue<int>();
		private static int nextId;

		public static int Register()
		{
			return Interlocked.Increment(ref nextId);
		}

		public static void EnqueueLeak(int id)
		{
			queue.Enqueue(id);
		}

		public static void Drain()
		{
			while (queue.TryDequeue(out var id))
			{
				Debug.LogWarning($"[FeatherTween] Unconsumed TweenBuilder leaked (diagnostic id {id}). Did you forget to call .Start()?");
			}
		}

		public static int PendingCount => queue.Count;
	}
}
