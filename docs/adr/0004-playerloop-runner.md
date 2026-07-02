# ADR 0004: PlayerLoop injection runner, no MonoBehaviour

## Status

Accepted

## Context

Unity tween engines usually drive updates from a `MonoBehaviour` (DOTween's `DOTweenComponent`) or from a global system (LitMotion's `MotionDispatcher`). We needed a runner that works in Edit mode and avoids scene clutter.

## Decision

Use PlayerLoop injection for runtime updates (`Update`, `LateUpdate`, `FixedUpdate`) and `EditorApplication.update` for Edit mode. No `MonoBehaviour` is created in the scene.

Tweens register their preferred update phase (`Update`, `LateUpdate`, `FixedUpdate`, or `Manual`). The runner dispatches each phase from the injected loop.

## Consequences

- **Positive**: Works in Edit mode without scene objects.
- **Positive**: No hidden `GameObject` clutter in the hierarchy.
- **Positive**: `Manual` mode allows explicit `deltaTime` injection for deterministic tests or custom loops.
- **Negative**: Slightly more setup code than a `MonoBehaviour`.
- **Mitigation**: Setup is one-time and hidden from users.
