# Git Strategy for FeatherTween

## Core Approach: `main` is the last release

Two long-lived branches:

- **`main`** — always equals the most recent tagged release. Nothing lands here except a release merge.
- **`develop`** — the integration branch. All work merges here; it is the default branch for day-to-day development and the base for every task branch.

Work branches stay short-lived (hours, not days) and branch from `develop`.

Before v0.1.0 the project was trunk-based on `main` alone. That worked while nothing was
published; now that the package is consumed at a tagged version, `main` has to be a stable
thing to point a consumer at, and unreleased M2 work cannot sit on it.

---

## Branch Naming

```
main                              last release, tagged
develop                           integration
task/2.1-setlink
experiment/soa-storage
fix/playhead-reset-on-restart
```

---

## Merging

- **Task branch → `develop`**: rebase onto `develop`, fast-forward only — no merge commits
- **`develop` → `main`**: release only, at the moment a version is tagged
- **No squashing** — each atomic commit is a meaningful unit; squashing destroys the audit trail
- Never merge `main` into a task branch; rebase instead

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

---

## Releasing

1. `develop` is green (see [CI gates](#ci) below) and `CHANGELOG.md` has a dated section for the version.
2. Bump `version` in `package.json`.
3. Fast-forward `main` to `develop`.
4. Tag the release commit on `main` (`v0.1.0`, `v0.2.0`, …).

Unreleased work lives on `develop` until the next release, so there is no need for feature flags
to hide in-progress features from consumers — `main` simply does not carry them yet.

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

## CI

CI is load-bearing here — slow or weak pipelines break the entire strategy.

`.github/workflows/ci.yml` runs on every push to `main` or `develop` and on every PR. What it runs
and how to reproduce it locally: [`guides/testing.md`](guides/testing.md).

Before anything merges to `develop`:

- CI green
- Unity Test Runner green (Editor + Runtime + Performance) for changes touching Runtime code —
  CI cannot cover `RequiresUnity` tests, PlayMode, or Performance
- New code must be covered by tests — AI optimizes for code that *looks* correct, not code that *is* correct

---

## Branch Protection

Enforce the strategy at the repo level on GitHub:

- `develop` is the default branch
- No direct push to `main` or `develop`
- Require fast-forward / rebase-based merges
- Require CI to pass before merge

---

## Versioning

[Semantic Versioning](https://semver.org) per Unity package convention (`package.json` `version`
field). The public API is stable as of `v0.1.0`; breaking changes follow semver from there.
