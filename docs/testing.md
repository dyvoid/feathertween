# Testing

How to run, structure, and set up PATween's tests. For the design rationale behind the test categories, see `architecture.md` and `docs/implementation.md` §8.

## Structure

```text
Tests/
  Editor/          -- EditMode tests ( builder validation, lifecycle, loops, easing, leak detection )
  Runtime/         -- PlayMode tests ( PlayerLoop ticking, update order, auto-kill timing )
  Performance/     -- EditMode tests ( allocation guards + throughput benchmarks )
```

Each folder is its own assembly definition with the `UNITY_INCLUDE_TESTS` define constraint.

## Running tests

Run via the Unity **Test Runner** window (Window > General > Test Runner) or headless with `Unity -runTests`.

## Performance & allocation testing

Two distinct concerns, deliberately separated by intent:

- **Allocation guards (hard fail).** Steady-state ticking must allocate zero managed bytes. Measured deterministically with `System.GC.GetAllocatedBytesForCurrentThread()` around a synchronous `PATweenRunner.ManualTick` loop (no frame/thread noise). These protect the core pooled, zero-alloc design promise and are CI-safe hard failures.
- **Throughput benchmarks (report only).** Tick cost at 1k/10k tweens and `Start()` cost, measured with `Unity.PerformanceTesting`'s `Measure.Method`. Numbers are noisy (machine/thermal dependent) and must never gate a build. Run locally when investigating a perf question.

The suite lives in an isolated `Tests/Performance` EditMode asmdef so the slow/noisy benchmarks do not run alongside the fast correctness tests and so the `Unity.PerformanceTesting` dependency is contained.

Why EditMode + `ManualTick` instead of PlayMode: `ManualTick` advances the runner synchronously, giving deterministic, frame-independent allocation measurements.

## Consumer project setup

PATween's tests only run inside a Unity project that consumes the package. Two setup steps are required in that **consuming project**.

### 1. Make tests visible in Test Runner

When PATween is consumed as a UPM package (linked via `file:`), Unity hides its tests by default. The test asmdefs use the `UNITY_INCLUDE_TESTS` define constraint, which is only active for packages listed as `testables`. Add the package to the consuming project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.patween.patween": "file:../path/to/Tween"
  },
  "testables": [
    "com.patween.patween"
  ]
}
```

The name must match `package.json` (`com.patween.patween`). Embedding the source under `Assets/` instead would surface tests automatically, but the `testables` entry is the correct mechanism for the package workflow.

### 2. Install the performance test dependency

The performance asmdef references `Unity.PerformanceTesting`. The consuming project must have the package installed (it is test-only):

```json
{
  "dependencies": {
    "com.unity.test-framework.performance": "3.0.3"
  }
}
```

Use the version that resolves for your Unity 6000.3 install if 3.0.3 is unavailable.
