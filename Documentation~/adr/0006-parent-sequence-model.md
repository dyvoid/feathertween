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

## Addendum (2026-07-16): mechanism as implemented

The recursive-composition *outcome* above shipped, but the mechanism evolved
during M1 and this ADR's field sketch (`_start`, `_end`, `_timeScale`,
`_parent` on every animation) does not describe the code. As implemented:

- **Children carry no parent pointer.** Ownership is top-down: a
  `SequenceData` owns `SequenceChildEntry` records, and the child's window
  (`start`, `length`, open/infinite) lives on the *entry*, not on the child.
  Child records are stored detached (`TweenStore.SetDataDetached`) — they are
  never on a phase's active list and are stepped only by their parent.
- **The hidden root is per-phase**: four `RootSequenceData` instances
  (`Update`/`Late`/`Fixed`/`Manual`) owned by the runner, each iterating a
  flat active-id list. Global and per-phase time scales are applied at these
  roots, so `pause`/`timeScale`/`seek`/`reverse` still compose recursively —
  by the parent scaling the `dt`/playhead it feeds its children, not by
  children consulting a parent.

Anyone extending storage (M5 SoA) should trust this addendum, not the
original sketch. The decision itself — parent-sequence model, hidden root,
public type named `Sequence` — stands unchanged.
