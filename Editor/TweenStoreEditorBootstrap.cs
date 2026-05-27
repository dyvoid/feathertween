using UnityEditor;
using PATween.Internal;

namespace PATween.Editor
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
