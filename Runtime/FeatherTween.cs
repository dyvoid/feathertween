using System;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween
{
	public static partial class FT
	{
		/// Pre-sizes the shared store (tweens and sequences live in one pool).
		/// Grows only; a smaller value than the current capacity is a no-op.
		public static void SetCapacity(int capacity)
		{
			TweenStore.EnsureCapacity(capacity);
		}

		/// Engine-side global playback rate, applied at the hidden root of every
		/// phase. Composes multiplicatively with per-phase and per-tween scales.
		/// Distinct from Unity's Time.timeScale: it also governs tweens using
		/// SetUpdate(..., ignoreTimeScale: true). Negative values throw.
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

		/// Per-phase playback rate; composes with the global scale.
		public static void SetTimeScale(UpdatePhase phase, float scale)
		{
			if (scale < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(scale), "[FeatherTween] Phase time scale cannot be negative.");
			}
			FeatherTweenRunner.EnsureInitialized();
			FeatherTweenRunner.SetPhaseTimeScale(phase, scale);
		}

		public static float GetTimeScale(UpdatePhase phase)
		{
			FeatherTweenRunner.EnsureInitialized();
			return FeatherTweenRunner.GetPhaseTimeScale(phase);
		}

		// Scratch for bulk-op snapshots: ops mutate the lists they iterate
		// (kill/free), so ids+generations are copied first. Main-thread only,
		// like the store itself.
		private static readonly System.Collections.Generic.List<int> bulkIds = new System.Collections.Generic.List<int>(64);
		private static readonly System.Collections.Generic.List<uint> bulkGens = new System.Collections.Generic.List<uint>(64);

		/// Kills every tween and sequence whose target is `target` (set via
		/// SetTarget or a typed shortcut). Sequence children with the target are
		/// reached too. O(k) in that target's tween count via the target map.
		public static void Kill(object target, bool complete = false)
		{
			if (target == null || !TweenStore.TryGetByTarget(target, out var ids))
			{
				return;
			}
			SnapshotIds(ids);
			for (var i = 0; i < bulkIds.Count; i++)
			{
				TweenOps.Kill(bulkIds[i], bulkGens[i], complete);
			}
		}

		/// True while at least one live tween or sequence targets `target`.
		public static bool IsTweening(object target)
		{
			return target != null
				&& TweenStore.TryGetByTarget(target, out var ids)
				&& ids.Count > 0;
		}

		public static void KillAll(bool complete = false)
		{
			SnapshotAllActive();
			for (var i = 0; i < bulkIds.Count; i++)
			{
				TweenOps.Kill(bulkIds[i], bulkGens[i], complete);
			}
			TweenStore.FlushPoolReturns();
		}

		public static void PauseAll()
		{
			SnapshotAllActive();
			for (var i = 0; i < bulkIds.Count; i++)
			{
				TweenOps.Pause(bulkIds[i], bulkGens[i]);
			}
		}

		public static void ResumeAll()
		{
			SnapshotAllActive();
			for (var i = 0; i < bulkIds.Count; i++)
			{
				TweenOps.Resume(bulkIds[i], bulkGens[i]);
			}
		}

		private static void SnapshotIds(System.Collections.Generic.List<int> ids)
		{
			bulkIds.Clear();
			bulkGens.Clear();
			for (var i = 0; i < ids.Count; i++)
			{
				bulkIds.Add(ids[i]);
				bulkGens.Add(TweenStore.GetGeneration(ids[i]));
			}
		}

		// Roots only: sequence children follow their parent (pause/kill cascade
		// through the parent's walk and OnFree).
		private static void SnapshotAllActive()
		{
			bulkIds.Clear();
			bulkGens.Clear();
			AppendActive(TweenStore.ActiveUpdate);
			AppendActive(TweenStore.ActiveLate);
			AppendActive(TweenStore.ActiveFixed);
			AppendActive(TweenStore.ActiveManual);
		}

		private static void AppendActive(System.Collections.Generic.List<int> list)
		{
			if (list == null)
			{
				return; // store not initialized yet: nothing to bulk-op
			}
			for (var i = 0; i < list.Count; i++)
			{
				bulkIds.Add(list[i]);
				bulkGens.Add(TweenStore.GetGeneration(list[i]));
			}
		}

		public static SequenceBuilder Sequence()
		{
			var buf = SequenceBuilderBufferPool.Rent();
			return new SequenceBuilder(buf);
		}

		// Creation methods follow one shape: subject first (the thing being
		// animated — a getter/setter pair, a setter, or a typed target), then
		// endpoint value(s), then duration (docs/guides/conventions.md).

		/// Animates from the current value (read via `getter` when playback
		/// begins) to `to`.
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

		/// Animates from `from` to the current value (read via `getter` at snap
		/// time; ADR 0007).
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

		/// Animates from `from` to `to`. Both endpoints are explicit, so no
		/// getter exists: the values are captured at this call, not at playback
		/// (unlike To/From, which sample the getter lazily).
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
