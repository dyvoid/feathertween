using System;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween
{
	/// <summary>
	/// The FeatherTween entry point: creation methods (<see cref="To{T}"/>,
	/// <see cref="From{T}"/>, <see cref="FromTo{T}"/>, <see cref="Sequence"/>,
	/// typed shortcuts), bulk operations, and engine-level settings.
	/// </summary>
	public static partial class FT
	{
		/// <summary>
		/// Pre-sizes the shared store (tweens and sequences live in one pool).
		/// Grows only; a smaller value than the current capacity is a no-op.
		/// </summary>
		public static void SetCapacity(int capacity)
		{
			TweenStore.EnsureCapacity(capacity);
		}

		/// <summary>
		/// Advances the <see cref="UpdatePhase.Manual"/> phase by
		/// <paramref name="deltaTime"/> seconds. Manual tweens tick only here;
		/// no other phase is affected. Main thread only.
		/// </summary>
		public static void ManualTick(double deltaTime)
		{
			FeatherTweenRunner.ManualTick(deltaTime);
		}

		/// <summary>
		/// Engine-side global playback rate, applied at the hidden root of every
		/// phase. Composes multiplicatively with per-phase and per-tween scales.
		/// Distinct from Unity's <c>Time.timeScale</c>: it also governs tweens using
		/// <c>SetUpdate(..., ignoreTimeScale: true)</c>. Negative values throw.
		/// </summary>
		public static float GlobalTimeScale
		{
			get
			{
				FeatherTweenRunner.EnsureInitialized();
				return FeatherTweenRunner.GlobalTimeScale;
			}
			set
			{
				if (value < 0f)
				{
					throw new ArgumentOutOfRangeException(nameof(value), "[FeatherTween] Global time scale cannot be negative.");
				}
				FeatherTweenRunner.EnsureInitialized();
				FeatherTweenRunner.GlobalTimeScale = value;
			}
		}

		/// <summary>Per-phase playback rate; composes with the global scale. Negative values throw.</summary>
		public static void SetTimeScale(UpdatePhase phase, float scale)
		{
			if (scale < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(scale), "[FeatherTween] Phase time scale cannot be negative.");
			}
			FeatherTweenRunner.EnsureInitialized();
			FeatherTweenRunner.SetPhaseTimeScale(phase, scale);
		}

		/// <summary>The current playback rate of one phase (see <see cref="SetTimeScale"/>).</summary>
		public static float GetTimeScale(UpdatePhase phase)
		{
			FeatherTweenRunner.EnsureInitialized();
			return FeatherTweenRunner.GetPhaseTimeScale(phase);
		}

		// Scratch for bulk-op snapshots: ops mutate the lists they iterate
		// (kill/free), so ids+generations are copied first. Pooled per bulk op
		// (not one shared pair): kill/pause callbacks fire synchronously and may
		// issue a nested bulk op, which must not clobber the snapshot an outer
		// bulk op is still iterating. Main-thread only, like the store itself.
		private sealed class BulkSnapshot
		{
			public readonly System.Collections.Generic.List<int> Ids = new System.Collections.Generic.List<int>(64);
			public readonly System.Collections.Generic.List<uint> Gens = new System.Collections.Generic.List<uint>(64);
		}

		private static readonly System.Collections.Generic.Stack<BulkSnapshot> bulkPool =
			new System.Collections.Generic.Stack<BulkSnapshot>(4);

		private static BulkSnapshot RentBulk()
		{
			return bulkPool.Count > 0 ? bulkPool.Pop() : new BulkSnapshot();
		}

		private static void ReturnBulk(BulkSnapshot snapshot)
		{
			snapshot.Ids.Clear();
			snapshot.Gens.Clear();
			bulkPool.Push(snapshot);
		}

		/// <summary>
		/// Kills every tween and sequence whose target is <paramref name="target"/>
		/// (set via <c>SetTarget</c> or a typed shortcut). Sequence children with the
		/// target are reached too; with <paramref name="complete"/> they first jump
		/// to their end values.
		/// </summary>
		public static void Kill(object target, bool complete = false)
		{
			if (target == null || !TweenStore.TryGetByTarget(target, out var ids))
			{
				return;
			}
			var snapshot = RentBulk();
			try
			{
				SnapshotIds(snapshot, ids);
				for (var i = 0; i < snapshot.Ids.Count; i++)
				{
					TweenOps.Kill(snapshot.Ids[i], snapshot.Gens[i], complete);
				}
			}
			finally
			{
				ReturnBulk(snapshot);
			}
		}

		/// <summary>True while at least one live tween or sequence targets <paramref name="target"/>.</summary>
		public static bool IsTweening(object target)
		{
			return target != null
				&& TweenStore.TryGetByTarget(target, out var ids)
				&& ids.Count > 0;
		}

		/// <summary>Kills every live tween and sequence; with <paramref name="complete"/> they first jump to their end values.</summary>
		public static void KillAll(bool complete = false)
		{
			var snapshot = RentBulk();
			try
			{
				SnapshotAllActive(snapshot);
				for (var i = 0; i < snapshot.Ids.Count; i++)
				{
					TweenOps.Kill(snapshot.Ids[i], snapshot.Gens[i], complete);
				}
			}
			finally
			{
				ReturnBulk(snapshot);
			}
			// Recycling freed records is only safe when nothing is mid-step; from
			// inside a callback (mid-tick) the runner flushes at end of tick.
			if (!TweenCommandQueue.InCallback)
			{
				TweenStore.FlushPoolReturns();
			}
		}

		/// <summary>Pauses every live root tween and sequence (children follow their parent).</summary>
		public static void PauseAll()
		{
			var snapshot = RentBulk();
			try
			{
				SnapshotAllActive(snapshot);
				for (var i = 0; i < snapshot.Ids.Count; i++)
				{
					TweenOps.Pause(snapshot.Ids[i], snapshot.Gens[i]);
				}
			}
			finally
			{
				ReturnBulk(snapshot);
			}
		}

		/// <summary>Resumes every live root tween and sequence (children follow their parent).</summary>
		public static void ResumeAll()
		{
			var snapshot = RentBulk();
			try
			{
				SnapshotAllActive(snapshot);
				for (var i = 0; i < snapshot.Ids.Count; i++)
				{
					TweenOps.Resume(snapshot.Ids[i], snapshot.Gens[i]);
				}
			}
			finally
			{
				ReturnBulk(snapshot);
			}
		}

		private static void SnapshotIds(BulkSnapshot snapshot, System.Collections.Generic.List<int> ids)
		{
			for (var i = 0; i < ids.Count; i++)
			{
				snapshot.Ids.Add(ids[i]);
				snapshot.Gens.Add(TweenStore.GetGeneration(ids[i]));
			}
		}

		// Roots only: sequence children follow their parent (pause/kill cascade
		// through the parent's walk and OnFree).
		private static void SnapshotAllActive(BulkSnapshot snapshot)
		{
			AppendActive(snapshot, TweenStore.ActiveUpdate);
			AppendActive(snapshot, TweenStore.ActiveLate);
			AppendActive(snapshot, TweenStore.ActiveFixed);
			AppendActive(snapshot, TweenStore.ActiveManual);
		}

		private static void AppendActive(BulkSnapshot snapshot, System.Collections.Generic.List<int> list)
		{
			if (list == null)
			{
				return; // store not initialized yet: nothing to bulk-op
			}
			for (var i = 0; i < list.Count; i++)
			{
				snapshot.Ids.Add(list[i]);
				snapshot.Gens.Add(TweenStore.GetGeneration(list[i]));
			}
		}

		/// <summary>Creates an empty sequence builder; compose with <c>Append</c>/<c>Join</c>/<c>Insert</c>, then <c>Start()</c>.</summary>
		public static SequenceBuilder Sequence()
		{
			var buf = SequenceBuilderBufferPool.Rent();
			return new SequenceBuilder(buf);
		}

		// Creation methods follow one shape: subject first (the thing being
		// animated — a getter/setter pair, a setter, or a typed target), then
		// endpoint value(s), then duration (Documentation~/guides/conventions.md).

		/// <summary>
		/// Animates from the current value (read via <paramref name="getter"/> when
		/// playback begins) to <paramref name="to"/> over <paramref name="duration"/> seconds.
		/// </summary>
		public static TweenBuilder<T> To<T>(Func<T> getter, Action<T> setter, T to, float duration)
		{
			if (getter == null)
			{
				throw new ArgumentNullException(nameof(getter));
			}
			if (setter == null)
			{
				throw new ArgumentNullException(nameof(setter));
			}
			if (duration < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
			}

			var buf = TweenBuilderBufferPool<T>.Rent();
			buf.Getter = getter;
			buf.Setter = setter;
			buf.EndValue = to;
			buf.Duration = duration;
			return new TweenBuilder<T>(buf);
		}

		/// <summary>
		/// Animates from <paramref name="from"/> to the current value (read via
		/// <paramref name="getter"/> at snap time; ADR 0007).
		/// </summary>
		public static TweenBuilder<T> From<T>(Func<T> getter, Action<T> setter, T from, float duration)
		{
			if (getter == null)
			{
				throw new ArgumentNullException(nameof(getter));
			}
			if (setter == null)
			{
				throw new ArgumentNullException(nameof(setter));
			}
			if (duration < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
			}

			var buf = TweenBuilderBufferPool<T>.Rent();
			buf.Getter = getter;
			buf.Setter = setter;
			buf.EndValue = from;
			buf.Duration = duration;
			buf.SnapMode = SnapMode.From;
			return new TweenBuilder<T>(buf);
		}

		/// <summary>
		/// Animates from <paramref name="from"/> to <paramref name="to"/>. Both
		/// endpoints are explicit, so no getter exists: the values are captured at
		/// this call, not at playback (unlike To/From, which sample the getter lazily).
		/// </summary>
		public static TweenBuilder<T> FromTo<T>(Action<T> setter, T from, T to, float duration)
		{
			if (setter == null)
			{
				throw new ArgumentNullException(nameof(setter));
			}
			if (duration < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
			}

			var buf = TweenBuilderBufferPool<T>.Rent();
			buf.Setter = setter;
			buf.FromValue = from;
			buf.EndValue = to;
			buf.Duration = duration;
			buf.SnapMode = SnapMode.FromTo;
			return new TweenBuilder<T>(buf);
		}
	}
}
