using UnityEditor;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween.Editor
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
