# Editor & Integration

## What ships today

`FeatherTween.Editor` contains no inspector UI. It is two files:

- `EditorRunner` — mirrors the PlayerLoop runner via `EditorApplication.update`, so tweens created
  outside play mode advance at normal speed. Runtime PlayerLoop ticks no-op in edit mode, so a
  leftover hook after exiting play mode cannot double-advance an edit-mode tween.
- `TweenStoreEditorBootstrap` — resets `TweenStore` on assembly reload so generation ids stay
  coherent across domain reloads and Fast Enter Play Mode.

Everything below is planned surface, not shipped.


## TweenSettings drawer (M2, planned)

A `PropertyDrawer` for `TweenSettings` and `TweenSettings<T>` showing one line: `[duration] [ease ▼] [loops]` with a foldout for delay, loopType, easeParamA/B, ignoreTimeScale, and the AnimationCurve (visible only when `ease == Curve`). The generic form additionally shows `startValue`/`endValue` with a `useStartValue` toggle.

## TweenAssetSO (optional, M3+)

`ScriptableObject` wrapping a `TweenSettings` (or `TweenSettings<T>`) and a string id. Shared/reusable presets for designers. Deferred until we have user demand.

## Editor preview (M3, planned)

A small EditorWindow that lists active tweens by target, with progress bars and pause/kill buttons. Powered by the same store API the runtime uses.

