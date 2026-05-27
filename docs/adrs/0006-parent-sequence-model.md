# ADR 0006: Parent-sequence model from M1

## Status

Accepted

## Context

GSAP uses a parent timeline model: every animation has a parent, and control methods (`timeScale`, `pause`, `reverse`, `seek`) compose recursively. Unity's `Timeline` package uses a different model. We needed a model that supports nesting from the first milestone.

## Decision

Every animation has `_start`, `_end`, `_timeScale`, `_parent`. A hidden root sequence owned by the runner contains all top-level tweens. The public type is named `Sequence` to avoid clashing with Unity's `Timeline` package.

M2 nested sequences slot in for free because the parent field already exists.

## Consequences

- **Positive**: `timeScale`, `pause`, `reverse`, `seek` compose recursively out of the box.
- **Positive**: Nested sequences require no structural changes in M2.
- **Positive**: A single runner root simplifies global pause/resume.
- **Negative**: Every tween carries a parent pointer even when not nested.
- **Mitigation**: The pointer is small (one field) and the simplicity of uniform recursion outweighs the cost.
