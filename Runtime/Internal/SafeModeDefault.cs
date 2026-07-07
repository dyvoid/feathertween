namespace PATween.Internal
{
	// Compile-time default for per-tween safe mode: on in the Editor, off in
	// player builds. PATWEEN_RELEASE compiles the whole wrapper out (see
	// docs/architecture/overview.md "Safe mode"), so the value is moot there.
	internal static class SafeModeDefault
	{
#if UNITY_EDITOR && !PATWEEN_RELEASE
		public const bool Value = true;
#else
		public const bool Value = false;
#endif
	}
}
