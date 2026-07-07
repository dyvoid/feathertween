using System;
using PATween.Internal;

namespace PATween
{
	public static partial class PATween
	{
		public static void SetCapacity(int tweens, int sequences)
		{
			TweenStore.EnsureCapacity(tweens + sequences);
		}

		/// Engine-side global playback rate, applied at the hidden root of every
		/// phase. Composes multiplicatively with per-phase and per-tween scales.
		/// Distinct from Unity's Time.timeScale: it also governs tweens using
		/// SetUpdate(..., ignoreTimeScale: true).
		public static void SetGlobalTimeScale(float scale)
		{
			if (scale < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(scale), "[PATween] Global time scale cannot be negative.");
			}
			PATweenRunner.EnsureInitialized();
			PATweenRunner.GlobalTimeScale = scale;
		}

		public static float GetGlobalTimeScale()
		{
			PATweenRunner.EnsureInitialized();
			return PATweenRunner.GlobalTimeScale;
		}

		/// Per-phase playback rate; composes with the global scale.
		public static void SetTimeScale(UpdatePhase phase, float scale)
		{
			if (scale < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(scale), "[PATween] Phase time scale cannot be negative.");
			}
			PATweenRunner.EnsureInitialized();
			PATweenRunner.SetPhaseTimeScale(phase, scale);
		}

		public static float GetTimeScale(UpdatePhase phase)
		{
			PATweenRunner.EnsureInitialized();
			return PATweenRunner.GetPhaseTimeScale(phase);
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

		public static TweenBuilder<T> To<T>(Func<T> getter, Action<T> setter, T end, float duration)
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
			buf.EndValue = end;
			buf.Duration = duration;
			return new TweenBuilder<T>(buf);
		}

		public static TweenBuilder<T> From<T>(Func<T> getter, Action<T> setter, T fromValue, float duration)
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
				throw new ArgumentOutOfRangeException(nameof(duration));
			}

			var buf = TweenBuilderBufferPool<T>.Rent();
			buf.Getter = getter;
			buf.Setter = setter;
			buf.EndValue = fromValue;
			buf.Duration = duration;
			buf.SnapMode = SnapMode.From;
			return new TweenBuilder<T>(buf);
		}

		public static TweenBuilder<T> FromTo<T>(Func<T> getter, Action<T> setter, T from, T to, float duration)
		{
			if (setter == null)
			{
				throw new ArgumentNullException(nameof(setter));
			}
			if (duration < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(duration));
			}

			var buf = TweenBuilderBufferPool<T>.Rent();
			buf.Getter = getter;
			buf.Setter = setter;
			buf.FromValue = from;
			buf.EndValue = to;
			buf.Duration = duration;
			buf.SnapMode = SnapMode.FromTo;
			return new TweenBuilder<T>(buf);
		}
	}
}
