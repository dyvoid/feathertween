# Git Strategy for PATween

## Core Approach: Trunk-Based Development

Single `main` branch. Short-lived branches (hours, not days). Everything merges fast or gets scrapped.

---

## Branch Naming

```
main
task/1.8-sequence-builder
experiment/soa-storage
fix/playhead-reset-on-restart
```

---

## Merging

- **Fast-forward only** — no merge commits, keeps history linear
- **Rebase onto `main`** before merging, never merge `main` into your branch
- **No squashing** — each atomic commit is a meaningful unit; squashing destroys the audit trail

---

## Commits

One commit = one AI task or prompt session. Keep commits atomic and scoped.

AI-generated code has no inherent intent — the commit message is the only record of *why* this code
exists. Use [Conventional Commits](https://www.conventionalcommits.org):

```
feat(sequence): add Append and Join to SequenceBuilder
fix(runner): harden TickActive against reentrancy
chore(deps): update lockfile
```

Annotate AI-assisted commits in the body, not the subject, to keep the subject readable:

```
feat(sequence): add Append and Join to SequenceBuilder

ai-assisted: <model> | prompt: .prompts/1.8-sequence-builder.md
```

---

## Prompt Versioning

Store prompts that generated significant code alongside the code:

```
.prompts/
  1.8-sequence-builder.md
  1.9-callbacks.md
```

---

## Feature Flags

Not applicable pre-1.0: M1 phases build directly on `main` and the package is not yet released to
consumers, so there is no shipped surface to guard. Revisit once M1 ships and in-progress M2+ work
needs to land on `main` without appearing in a tagged release.

---

## Generated Sources

Do not commit generated source files. They create noisy diffs and painful merge conflicts. Commit
lockfiles for reproducibility; regenerate everything else from source.

---

## Code Review

Review diffs skeptically — AI code looks clean but can be subtly wrong.

High-blast-radius files always get manual review:

- `.gitignore` / `.gitattributes`
- Anything touching secrets, auth, or permissions
- `package.json` (Unity package manifest — versioning, dependencies)

---

## Branch Protection

Enforce the strategy at the repo level on GitHub:

- No direct push to `main`
- Require fast-forward / rebase-based merges

---

## Versioning

Follow [Semantic Versioning](https://semver.org) per Unity package convention (`package.json`
`version` field). Tag milestone completions (`v0.1.0` at M1 exit, etc.) once M1 ships; pre-1.0, breaking
changes are expected between phases and don't require a major bump.
