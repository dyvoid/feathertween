using System;
using PATween;
using PATween.Internal;

public static class PATween
{
	public static void SetCapacity(int tweens, int sequences)
	{
		TweenStore.EnsureCapacity(tweens + sequences);
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
}
