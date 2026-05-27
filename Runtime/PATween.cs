using PATween.Internal;

public static class PATween
{
	public static void SetCapacity(int tweens, int sequences)
	{
		TweenStore.EnsureCapacity(tweens + sequences);
	}
}
