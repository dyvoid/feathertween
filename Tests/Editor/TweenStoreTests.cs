using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Dyvoid.FeatherTween;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween.Tests
{
	[TestFixture]
	public class TweenStoreTests
	{
		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
		}

		[Test]
		public void Allocate_AndFree_InRandomOrder_BumpsGenerationsAndReusesIds()
		{
			const int count = 32;
			var handles = new (int id, uint gen)[count];
			for (var i = 0; i < count; i++)
			{
				handles[i] = TweenStore.Allocate();
			}

			var rng = new Random(12345);
			var order = new List<int>(count);
			for (var i = 0; i < count; i++)
			{
				order.Add(i);
			}
			for (var i = order.Count - 1; i > 0; i--)
			{
				var j = rng.Next(i + 1);
				(order[i], order[j]) = (order[j], order[i]);
			}

			var oldGenerations = new Dictionary<int, uint>();
			foreach (var idx in order)
			{
				var (id, gen) = handles[idx];
				oldGenerations[id] = gen;
				TweenStore.Free(id);
			}

			for (var i = 0; i < count; i++)
			{
				var (id, gen) = TweenStore.Allocate();
				Assert.That(oldGenerations.ContainsKey(id), Is.True, "Reused id should come from freed pool.");
				Assert.That(gen, Is.Not.EqualTo(oldGenerations[id]), "Generation should be bumped on recycle.");
			}
		}

		[Test]
		public void StaleHandle_ReportsNotAlive_AndStatusDisposed()
		{
			var (id, gen) = TweenStore.Allocate();
			TweenStore.Free(id);

			Assert.That(TweenStore.IsAlive(id, gen), Is.False);

			var staleHandle = new Tween(id, gen);
			Assert.That(staleHandle.IsAlive, Is.False);
			Assert.That(staleHandle.Status, Is.EqualTo(TweenStatus.Disposed));
		}

		[Test]
		public void DefaultHandle_IsAlwaysStale()
		{
			Tween defaultTween = default;
			Sequence defaultSequence = default;

			Assert.That(defaultTween.IsAlive, Is.False);
			Assert.That(defaultTween.Status, Is.EqualTo(TweenStatus.Disposed));
			Assert.That(defaultSequence.IsAlive, Is.False);
			Assert.That(defaultSequence.Status, Is.EqualTo(TweenStatus.Disposed));
		}

		[Test]
		public void Reset_ClearsFreeAndActiveLists_AndBumpsAllGenerations()
		{
			var capacity = TweenStore.Capacity;
			var (id, genBefore) = TweenStore.Allocate();
			var handleBefore = new Tween(id, genBefore);
			Assert.That(handleBefore.IsAlive, Is.True);

			TweenStore.Reset();

			Assert.That(handleBefore.IsAlive, Is.False, "Handle from before reset must be stale.");
			Assert.That(TweenStore.FreeCount, Is.EqualTo(capacity), "All slots returned to free list.");
			Assert.That(TweenStore.ActiveUpdate.Count, Is.Zero);
			Assert.That(TweenStore.ActiveLate.Count, Is.Zero);
			Assert.That(TweenStore.ActiveFixed.Count, Is.Zero);
			Assert.That(TweenStore.ActiveManual.Count, Is.Zero);
		}

		[Test]
		public void AllocateFreeCycle_AfterWarmup_ProducesNoManagedAlloc()
		{
			for (var i = 0; i < 1024; i++)
			{
				var (id, _) = TweenStore.Allocate();
				TweenStore.Free(id);
			}

			System.GC.Collect();
			System.GC.WaitForPendingFinalizers();
			System.GC.Collect();

			var before = System.GC.GetTotalMemory(false);
			for (var i = 0; i < 10_000; i++)
			{
				var (id, _) = TweenStore.Allocate();
				TweenStore.Free(id);
			}
			var after = System.GC.GetTotalMemory(false);

			Assert.That(after - before, Is.LessThan(64 * 1024),
				$"Steady-state alloc/free should not allocate. Delta: {after - before} bytes.");
		}

		[Test]
		public void PoolExhaustion_DoublesBackingArrays_TwiceInSuccession()
		{
			var initial = TweenStore.Capacity;
			for (var i = 0; i < initial; i++)
			{
				TweenStore.Allocate();
			}
			Assert.That(TweenStore.FreeCount, Is.Zero);

			TweenStore.Allocate();
			Assert.That(TweenStore.Capacity, Is.EqualTo(initial * 2));

			while (TweenStore.FreeCount > 0)
			{
				TweenStore.Allocate();
			}

			TweenStore.Allocate();
			Assert.That(TweenStore.Capacity, Is.EqualTo(initial * 4));
		}

		[Test]
		[Category("RequiresSafeMode")] // the off-thread assert is compiled out under FEATHERTWEEN_RELEASE
		public void OffThreadAllocate_ThrowsInSafeMode()
		{
			Exception caught = null;
			var task = Task.Run(() =>
			{
				try
				{
					TweenStore.Allocate();
				}
				catch (Exception e)
				{
					caught = e;
				}
			});
			task.Wait();

			Assert.That(caught, Is.Not.Null);
			Assert.That(caught, Is.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public void SetCapacity_Grows_ButDoesNotShrink()
		{
			var initial = TweenStore.Capacity;
			FT.SetCapacity(initial * 4, 0);
			Assert.That(TweenStore.Capacity, Is.GreaterThanOrEqualTo(initial * 4));

			var grown = TweenStore.Capacity;
			FT.SetCapacity(8, 0);
			Assert.That(TweenStore.Capacity, Is.EqualTo(grown), "SetCapacity must not shrink.");
		}

	}
}
