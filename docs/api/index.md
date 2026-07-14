# API Guide

This section documents the public FeatherTween API. If you are new to the library, start with the quickstart below, then jump to the topic that matches what you are building.

## Quickstart

```csharp
using Dyvoid.FeatherTween;

// A one-shot tween
Tween t = FT.To(
    () => transform.position.x,
    v  => transform.position = transform.position with { x = v },
    to: 10f,
    duration: 1f
)
.SetEase(Easing.OutCubic())
.SetLoops(2, LoopType.Yoyo)
.OnComplete(() => Debug.Log("done"))
.Start();

// Both endpoints known? Skip the getter.
FT.FromTo(v => alpha = v, 0f, 1f, 0.3f).Start();

// Or use a typed shortcut
FT.Move(transform, new Vector3(2, 3, 4), 1f)
    .SetEase(Easing.OutBack(1.5f))
    .Start();

// A sequence
Sequence seq = FT.Sequence()
    .Append(FT.Move(transform, new Vector3(0, 5, 0), 0.5f))
    .AppendInterval(0.25f)
    .AppendCallback(() => Debug.Log("midpoint"))
    .Append(FT.Scale(transform, Vector3.one * 2f, 0.5f))
    .Start();

// Await a tween
await FT.Fade(canvasGroup, 0f, 0.5f); // M2 (planned): awaitable handles
```

## API map

| Topic | Document |
|-------|----------|
| Builders (`TweenBuilder<T>`, `SequenceBuilder`) | [builders.md](builders.md) |
| Handles (`Tween`, `Sequence`, control surface, lifecycle) | [handles.md](handles.md) |
| Callbacks and firing matrix | [callbacks.md](callbacks.md) |
| Generic tweens and typed shortcuts | [tweens.md](tweens.md) |
| Sequences and `Position` | [sequences.md](sequences.md) |
| Easings (`EaseRef`, `Easing.X(...)`) | [easings.md](easings.md) |
| Awaiters and `TweenSettings` | [awaiters.md](awaiters.md) |
| Filters and bulk operations | [filters.md](filters.md) |
| Custom value types (`IInterpolator<T>`) | [interpolators.md](interpolators.md) |

## Namespaces

- `FeatherTween` — public API
- `Dyvoid.FeatherTween.Internal` — runtime internals (not intended for direct use)
- `Dyvoid.FeatherTween.Editor` — inspectors and editor tools
- `FeatherTween.UniTask` — optional UniTask integration (separate asmdef)
