# Git Strategy for FeatherTween

## Core Approach: `main` is the last release

Two long-lived branches:

- **`main`** — always equals the most recent tagged release. Nothing lands here except a release
  merge. It stays GitHub's **default branch**: it is what a consumer lands on, so it shows stable,
  released code and a README that describes what they can actually install.
- **`develop`** — the integration branch. All work merges here, and every task branch is cut from
  here. Not the default branch; contributors check it out explicitly.

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

- `main` stays the default branch (consumer-facing landing page)
- No direct push to `main` or `develop`
- Open pull requests against `develop`, never `main`
- Require fast-forward / rebase-based merges
- Require CI to pass before merge

---

## Versioning

[Semantic Versioning](https://semver.org) per Unity package convention (`package.json` `version`
field).

**Pre-1.0, minor versions may contain breaking changes.** This is what `0.x` means in semver, and
it is deliberate: M2-M5 carry work that may move the API or its observable behaviour (blendable
tweens, SoA storage, the `Seek`/`Completed` question). v0.1.0 was released with a stronger promise
than the version number implied; that promise is withdrawn rather than half-kept. In exchange,
every break gets a `### Changed` or `### Removed` entry in `CHANGELOG.md` with a one-line migration
note. `1.0.0` is where the stability promise gets made, earned by real usage.

The version number tracks **compatibility impact, not effort**: additive API is a minor bump however
small, and a month of internal work with no surface change is a patch. How large a release *feels*
belongs in the changelog, not the number.

### `develop` carries the next version with a `-dev` suffix

`develop`'s `package.json` reads the version it is heading for, suffixed: `0.2.0-dev`. Without this
both branches claim the same version and there is no way to tell a working copy from a release
short of reading git history. The suffix is dropped by the release commit.

### Release cadence

Release when a **coherent chunk** is done and usable, not per merge and not per milestone. Per merge
makes releases meaningless; per milestone leaves `main` — the consumer-facing landing page — stale
for months. A milestone may therefore span several releases: M2's "production stickiness" pair
(`SetLink` + awaitables) is `0.2.0`, and the rest of M2 follows in later versions.
