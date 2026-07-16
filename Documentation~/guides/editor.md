# Editor & Integration

## TweenSettings drawer

A `PropertyDrawer` for `TweenSettings` and `TweenSettings<T>` showing one line: `[duration] [ease ▼] [loops]` with a foldout for delay, loopType, easeParamA/B, ignoreTimeScale, and the AnimationCurve (visible only when `ease == Curve`). The generic form additionally shows `startValue`/`endValue` with a `useStartValue` toggle.

## TweenAssetSO (optional, M3+)

`ScriptableObject` wrapping a `TweenSettings` (or `TweenSettings<T>`) and a string id. Shared/reusable presets for designers. Deferred until we have user demand.

## Editor preview (M4+)

A small EditorWindow that lists active tweens by target, with progress bars and pause/kill buttons. Powered by the same store API the runtime uses.

