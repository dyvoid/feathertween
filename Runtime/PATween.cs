using PATween.Internal;

namespace PATween
{
	public static class PATween
	{
		public static void SetCapacity(int tweens, int sequences)
		{
			TweenStore.EnsureCapacity(tweens + sequences);
		}
	}
}
