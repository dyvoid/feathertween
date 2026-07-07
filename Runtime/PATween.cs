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
