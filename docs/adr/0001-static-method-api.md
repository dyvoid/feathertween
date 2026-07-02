# ADR 0001: Static-method API instead of extension methods

## Status

Accepted

## Context

DOTween popularized extension methods on Unity types (`transform.DOMove(...)`). GSAP uses a static factory (`gsap.to(target, ...)`). Both are familiar to Unity developers. We needed to pick one for the core API.

## Decision

Use a static-method API: typed shortcuts live as static methods on `PATween` (`PATween.Move(transform, ...)`), not as extension methods on Unity types.

## Consequences

- **Positive**: No namespace pollution on `Transform`, `CanvasGroup`, `Material`, etc. One discoverable entry point (`PATween.`). Less risk of symbol collision in large projects.
- **Positive**: Aliasing and invalidation semantics are easier to explain when the entry point is explicit.
- **Negative**: Slightly more verbose than DOTween-style extensions.
- **Mitigation**: A small optional `PATween.Extensions` asmdef can be added in a later milestone to provide DOTween-style wrappers for users who prefer them.
