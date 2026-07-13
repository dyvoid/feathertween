using UnityEditor;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween.Editor
{
	internal static class TweenStoreEditorBootstrap
	{
		[InitializeOnLoadMethod]
		private static void OnLoad()
		{
			TweenStore.Reset();
		}
	}
}
