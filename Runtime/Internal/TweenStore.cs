using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace dyvoid.FeatherTween.Internal
{
	internal static class TweenStore
	{
		private const int DefaultCapacity = 128;

		private static TweenData[] data;
		private static uint[] generations;
		private static Stack<int> freeList;
		private static List<int> activeUpdate;
		private static List<int> activeLate;
		private static List<int> activeFixed;
		private static List<int> activeManual;

		// Slot -> position in its phase's active list (-1 when not listed);
		// makes RemoveFromActiveList an O(1) swap-remove.
		private static int[] activeIndex;

		// True while the slot sits on the free list. Guards Free against
		// double-freeing a slot, which would push a duplicate free-list entry
		// and alias the slot between two future Allocate calls.
		private static bool[] slotFree;

		// Target-indexed multimap for Kill(target)/IsTweening(target). Includes
		// detached (sequence-child) slots so bulk kills reach nested tweens.
		private static readonly Dictionary<object, List<int>> byTarget = new Dictionary<object, List<int>>();
		private static readonly Stack<List<int>> targetListPool = new Stack<List<int>>(16);

		// Freed records queue here and recycle at end of tick, so an in-flight
		// step never sees its instance re-rented mid-walk.
		private static readonly List<TweenData> pendingPoolReturns = new List<TweenData>(64);

		private static int mainThreadId;
		private static bool initialized;

		public static int Capacity => data?.Length ?? 0;
		public static int FreeCount => freeList?.Count ?? 0;

		internal static List<int> ActiveUpdate => activeUpdate;
		internal static List<int> ActiveLate => activeLate;
		internal static List<int> ActiveFixed => activeFixed;
		internal static List<int> ActiveManual => activeManual;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void RuntimeBootstrap()
		{
			Reset();
		}

		public static void Reset()
		{
			mainThreadId = Thread.CurrentThread.ManagedThreadId;

			if (data == null)
			{
				AllocateArrays(DefaultCapacity);
			}
			else
			{
				ClearAndBumpAll();
			}

			byTarget.Clear();
			targetListPool.Clear();
			pendingPoolReturns.Clear();
			TweenDataPoolRegistry.ClearAll();

			initialized = true;
		}

		public static void EnsureCapacity(int capacity)
		{
			AssertMainThread();
			EnsureInitialized();

			if (capacity <= data.Length)
			{
				return;
			}

			var newCapacity = data.Length;
			while (newCapacity < capacity)
			{
				newCapacity *= 2;
			}

			Grow(newCapacity);
		}

		public static (int id, uint generation) Allocate()
		{
			AssertMainThread();
			EnsureInitialized();

			if (freeList.Count == 0)
			{
				Grow(data.Length * 2);
#if UNITY_EDITOR
				Debug.LogWarning($"[FeatherTween] TweenStore grew to {data.Length}. Consider FT.SetCapacity to pre-size.");
#endif
			}

			var id = freeList.Pop();
			slotFree[id] = false;
			var gen = generations[id];
			return (id, gen);
		}

		public static void SetData(int id, TweenData newData)
		{
			AssertMainThread();
			if (id < 0 || id >= data.Length)
			{
				return;
			}
			newData.SelfId = id;
			data[id] = newData;
			AddToActiveList(id, newData.Phase);
			AddToTargetMap(id, newData.Target);
		}

		// Sequence children occupy store slots but are ticked by their parent,
		// never by the runner's active lists.
		public static void SetDataDetached(int id, TweenData newData)
		{
			AssertMainThread();
			if (id < 0 || id >= data.Length)
			{
				return;
			}
			newData.SelfId = id;
			data[id] = newData;
			AddToTargetMap(id, newData.Target);
		}

		public static void Free(int id)
		{
			AssertMainThread();
			EnsureInitialized();

			if (id < 0 || id >= data.Length)
			{
				return;
			}

			if (slotFree[id])
			{
				return; // already on the free list; a second push would alias the slot
			}
			slotFree[id] = true;

			var freed = data[id];
			if (freed != null)
			{
				RemoveFromActiveList(id, freed.Phase);
				RemoveFromTargetMap(id, freed.Target);
				data[id] = null;
			}
			generations[id] = unchecked(generations[id] + 1);
			if (generations[id] == 0)
			{
				generations[id] = 1;
			}
			freeList.Push(id);

			// After bookkeeping so a cascade (sequence freeing children) sees a
			// consistent store and cannot double-free this slot.
			if (freed != null)
			{
				freed.OnFree();
				pendingPoolReturns.Add(freed);
			}
		}

		// Recycles freed records into their per-type pools. Called by the runner
		// at end of tick; safe to call any time no step is mid-flight.
		public static void FlushPoolReturns()
		{
			for (var i = 0; i < pendingPoolReturns.Count; i++)
			{
				pendingPoolReturns[i].ReturnToPool();
			}
			pendingPoolReturns.Clear();
		}

		private static void AddToTargetMap(int id, object target)
		{
			if (target == null)
			{
				return;
			}
			if (!byTarget.TryGetValue(target, out var list))
			{
				list = targetListPool.Count > 0 ? targetListPool.Pop() : new List<int>(4);
				byTarget.Add(target, list);
			}
			list.Add(id);
		}

		private static void RemoveFromTargetMap(int id, object target)
		{
			if (target == null || !byTarget.TryGetValue(target, out var list))
			{
				return;
			}
			for (var i = list.Count - 1; i >= 0; i--)
			{
				if (list[i] == id)
				{
					list[i] = list[list.Count - 1];
					list.RemoveAt(list.Count - 1);
					break;
				}
			}
			if (list.Count == 0)
			{
				byTarget.Remove(target);
				targetListPool.Push(list);
			}
		}

		public static bool TryGetByTarget(object target, out List<int> ids)
		{
			if (target == null)
			{
				ids = null;
				return false;
			}
			return byTarget.TryGetValue(target, out ids);
		}

		private static void AddToActiveList(int id, UpdatePhase phase)
		{
			var list = GetActiveList(phase);
			activeIndex[id] = list.Count;
			list.Add(id);
		}

		// O(1) swap-remove; the runner ticks a snapshot, so in-list order is
		// not load-bearing.
		private static void RemoveFromActiveList(int id, UpdatePhase phase)
		{
			var idx = activeIndex[id];
			if (idx < 0)
			{
				return;
			}
			var list = GetActiveList(phase);
			var lastPos = list.Count - 1;
			var lastId = list[lastPos];
			list[idx] = lastId;
			activeIndex[lastId] = idx;
			list.RemoveAt(lastPos);
			activeIndex[id] = -1;
		}

		private static List<int> GetActiveList(UpdatePhase phase)
		{
			return phase switch
			{
				UpdatePhase.Update => activeUpdate,
				UpdatePhase.Late => activeLate,
				UpdatePhase.Fixed => activeFixed,
				UpdatePhase.Manual => activeManual,
				_ => activeUpdate,
			};
		}

		public static bool IsAlive(int id, uint generation)
		{
			if (data == null || id < 0 || id >= data.Length)
			{
				return false;
			}
			return generations[id] == generation && generation != 0;
		}

		public static TweenData Get(int id, uint generation)
		{
			if (!IsAlive(id, generation))
			{
				return null;
			}
			return data[id];
		}

		internal static TweenData GetByIndex(int id)
		{
			if (data == null || id < 0 || id >= data.Length)
			{
				return null;
			}
			return data[id];
		}

		internal static uint GetGeneration(int id)
		{
			if (generations == null || id < 0 || id >= generations.Length)
			{
				return 0;
			}
			return generations[id];
		}

		internal static bool HasLiveOfType<T>()
		{
			if (data == null)
			{
				return false;
			}
			var target = typeof(TweenData<T>);
			for (var i = 0; i < data.Length; i++)
			{
				if (data[i] != null && data[i].GetType() == target)
				{
					return true;
				}
			}
			return false;
		}

		private static void EnsureInitialized()
		{
			if (!initialized)
			{
				Reset();
			}
		}

		private static void AllocateArrays(int capacity)
		{
			data = new TweenData[capacity];
			generations = new uint[capacity];
			activeIndex = new int[capacity];
			slotFree = new bool[capacity];
			freeList = new Stack<int>(capacity);
			activeUpdate = new List<int>();
			activeLate = new List<int>();
			activeFixed = new List<int>();
			activeManual = new List<int>();

			for (var i = 0; i < capacity; i++)
			{
				generations[i] = 1;
				activeIndex[i] = -1;
				slotFree[i] = true;
			}
			for (var i = capacity - 1; i >= 0; i--)
			{
				freeList.Push(i);
			}
		}

		private static void ClearAndBumpAll()
		{
			activeUpdate.Clear();
			activeLate.Clear();
			activeFixed.Clear();
			activeManual.Clear();
			freeList.Clear();

			for (var i = 0; i < data.Length; i++)
			{
				data[i] = null;
				activeIndex[i] = -1;
				slotFree[i] = true;
				generations[i] = unchecked(generations[i] + 1);
				if (generations[i] == 0)
				{
					generations[i] = 1;
				}
			}
			for (var i = data.Length - 1; i >= 0; i--)
			{
				freeList.Push(i);
			}
		}

		private static void Grow(int newCapacity)
		{
			var oldCapacity = data.Length;
			System.Array.Resize(ref data, newCapacity);
			System.Array.Resize(ref generations, newCapacity);
			System.Array.Resize(ref activeIndex, newCapacity);
			System.Array.Resize(ref slotFree, newCapacity);
			for (var i = oldCapacity; i < newCapacity; i++)
			{
				generations[i] = 1;
				activeIndex[i] = -1;
				slotFree[i] = true;
			}
			for (var i = newCapacity - 1; i >= oldCapacity; i--)
			{
				freeList.Push(i);
			}
		}

		// Off-thread guard, part of the safe-mode debug layer: compiled out
		// under FEATHERTWEEN_RELEASE along with the try/catch wrappers.
		private static void AssertMainThread()
		{
#if !FEATHERTWEEN_RELEASE
			if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
			{
				throw new System.InvalidOperationException(
					"[FeatherTween] TweenStore must be accessed from the main thread.");
			}
#endif
		}
	}
}
