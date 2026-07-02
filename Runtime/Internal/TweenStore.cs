using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace PATween.Internal
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
				Debug.LogWarning($"[PATween] TweenStore grew to {data.Length}. Consider PATween.SetCapacity to pre-size.");
#endif
			}

			var id = freeList.Pop();
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
		}

		public static void Free(int id)
		{
			AssertMainThread();
			EnsureInitialized();

			if (id < 0 || id >= data.Length)
			{
				return;
			}

			var freed = data[id];
			if (freed != null)
			{
				RemoveFromActiveList(id, freed.Phase);
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
			freed?.OnFree();
		}

		private static void AddToActiveList(int id, UpdatePhase phase)
		{
			GetActiveList(phase).Add(id);
		}

		private static void RemoveFromActiveList(int id, UpdatePhase phase)
		{
			var list = GetActiveList(phase);
			for (var i = list.Count - 1; i >= 0; i--)
			{
				if (list[i] == id)
				{
					list.RemoveAt(i);
					return;
				}
			}
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
			freeList = new Stack<int>(capacity);
			activeUpdate = new List<int>();
			activeLate = new List<int>();
			activeFixed = new List<int>();
			activeManual = new List<int>();

			for (var i = 0; i < capacity; i++)
			{
				generations[i] = 1;
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
			for (var i = oldCapacity; i < newCapacity; i++)
			{
				generations[i] = 1;
			}
			for (var i = newCapacity - 1; i >= oldCapacity; i--)
			{
				freeList.Push(i);
			}
		}

		private static void AssertMainThread()
		{
			if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
			{
				throw new System.InvalidOperationException(
					"[PATween] TweenStore must be accessed from the main thread.");
			}
		}
	}
}
