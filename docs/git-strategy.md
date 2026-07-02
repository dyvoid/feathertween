# Git Strategy

## Core Approach

Feature branches from `develop`. Short-lived branches (one phase or one fix at a time). Fast-forward merges to `main` are handled by the user; agent merges land on `develop` only with explicit go-ahead.

---

## Branch Naming

```
main
develop
feature/1.8-sequence-builder
feature/1.9-callbacks
fix/playhead-reset-on-restart
```

---

## Merging

- **Feature &rarr; develop**: `git merge --no-ff` — keep feature history visible.
- **Develop &rarr; main**: fast-forward (user handles).
- **Rebase onto `develop`** before opening a merge request; never merge `develop` into your branch.
- **No squashing** — each atomic commit is a meaningful unit; squashing destroys the audit trail.

---

## Commits

One commit = one task or prompt session. Keep commits atomic and scoped.

AI-generated code has no inherent intent — the commit message is the only record of *why* this code exists. Use [Conventional Commits](https://www.conventionalcommits.org):

```
feat(sequence): add Append and Join to SequenceBuilder
fix(runner): harden TickActive against reentrancy
docs(api): document new OnComplete overloads
```

---

## Generated Sources

Do not commit generated source files. They create noisy diffs and painful merge conflicts. Commit lockfiles for reproducibility; regenerate everything else from source.

---

## Code Review

Review diffs skeptically — AI code looks clean but can be subtly wrong.

High-blast-radius files always get manual review:

- `.gitignore`
- `.gitattributes`
- Authentication, authorization, or anything touching secrets
- Dependency changes (lockfiles, package manifests)
- Refactors that cut across multiple modules
