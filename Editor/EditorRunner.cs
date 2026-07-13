using UnityEditor;
using Dyvoid.FeatherTween.Internal;

namespace Dyvoid.FeatherTween.Editor
{
	[InitializeOnLoad]
	internal static class EditorRunner
	{
		private static double lastTime;
		private static bool subscribed;

		static EditorRunner()
		{
			Install();
		}

		internal static void Install()
		{
			if (subscribed)
			{
				return;
			}
			FeatherTweenRunner.EnsureInitialized();
			lastTime = EditorApplication.timeSinceStartup;
			EditorApplication.update += Tick;
			subscribed = true;
		}

		internal static void Uninstall()
		{
			if (!subscribed)
			{
				return;
			}
			EditorApplication.update -= Tick;
			subscribed = false;
		}

		private static void Tick()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
			{
				lastTime = EditorApplication.timeSinceStartup;
				return;
			}
			var now = EditorApplication.timeSinceStartup;
			var dt = now - lastTime;
			lastTime = now;
			FeatherTweenRunner.TickEditorDelta(dt);
		}
	}
}
