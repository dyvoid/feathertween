# ADR 0007: `From` / `FromTo` snap semantics

## Status

Accepted

## Context

`From` and `FromTo` tweens need to snap the target property to the start value at some point. DOTween snaps immediately on creation. GSAP snaps when the playhead reaches the tween. These behaviors differ for sequenced children with delays. We needed a rule that matches user expectations for both root and sequenced tweens.

## Decision

- **Root tween**: Snaps at `.Start()` regardless of its own `SetDelay`. The delay only defers interpolation, not the snap. Matches DOTween behavior.
- **Sequenced child** with a parent-imposed offset `> 0`: Snaps when the parent playhead first crosses `child._start`. Matches GSAP. The property is not forced to the From-value before the child should play. The child's own `SetDelay` further offsets interpolation but not the snap.

## Consequences

- **Positive**: Root tweens behave like DOTween (familiar to Unity developers).
- **Positive**: Sequenced children behave like GSAP (familiar to motion designers).
- **Negative**: Two different rules depending on context.
- **Mitigation**: The rule maps naturally to the lifecycle: root tweens are autonomous, sequenced children are governed by the parent playhead. Documented in API reference.
