# Testing

How to run, structure, and set up FeatherTween's tests. For the design rationale behind the test categories, see `../architecture/overview.md` and `../architecture/performance.md`.

## Structure

```text
Tests/
  Editor/          -- EditMode tests ( builder validation, lifecycle, loops, easing, leak detection )
  Runtime/         -- PlayMode tests ( PlayerLoop ticking, update order, auto-kill timing )
  Performance/     -- EditMode tests ( allocation guards + throughput benchmarks )
```

Each folder is its own assembly definition with the `UNITY_INCLUDE_TESTS` define constraint.

Tests reach internal types (`TweenStore`, `FeatherTweenRunner`, `Interpolators`) via `InternalsVisibleTo` in `Runtime/Internal/AssemblyInfo.cs`. Any new test assembly must be added there or it will fail with `CS0122` (inaccessible) errors.

## Running tests

Run via the Unity **Test Runner** window (Window > General > Test Runner) or headless with `Unity -runTests`.

## Fast feedback: the .NET stub harness (no Unity)

`tools~/compile-check/` (tilde suffix: Unity ignores the folder, like `Samples~`) compiles Runtime + Samples + EditMode tests against a
minimal `UnityEngine` stub and runs the EditMode suite via NUnitLite in well
under a second. CI runs this on every push (`.github/workflows/ci.yml`).

```sh
dotnet run --project tools~/compile-check/FeatherTween.TestRunner.csproj -c Release -- --noresult --where "cat != RequiresUnity"
```

Tests that genuinely need the Unity runtime are tagged `[Category("RequiresUnity")]`
and excluded there. The harness is a fast pre-check, not a replacement: the
in-Unity Editor + Runtime + Performance run remains the merge gate. See
`tools~/compile-check/README.md` for stub rules.

CI also runs a second harness leg with `-p:DefineConstants=FEATHERTWEEN_RELEASE`,
which compiles the safe-mode wrapper and off-thread assertions out. Tests that
depend on that debug layer are tagged `[Category("RequiresSafeMode")]` and
excluded from that leg; `ReleaseModeTests` (compiled only under the define)
proves the wrapper is gone by asserting a throwing setter propagates despite
`SetSafeMode(true)`.

## Performance & allocation testing

Two distinct concerns, deliberately separated by intent:

- **Allocation guards (hard fail).** Steady-state ticking must allocate zero managed bytes. Measured deterministically with `System.GC.GetAllocatedBytesForCurrentThread()` around a synchronous `FeatherTweenRunner.ManualTick` loop (no frame/thread noise). These protect the core pooled, zero-alloc design promise and are CI-safe hard failures.
- **Throughput benchmarks (report only).** Tick cost at 1k/10k tweens and `Start()` cost, measured with `Unity.PerformanceTesting`'s `Measure.Method`. Numbers are noisy (machine/thermal dependent) and must never gate a build. Run locally when investigating a perf question.

The suite lives in an isolated `Tests/Performance` EditMode asmdef so the slow/noisy benchmarks do not run alongside the fast correctness tests and so the `Unity.PerformanceTesting` dependency is contained.

Why EditMode + `ManualTick` instead of PlayMode: `ManualTick` advances the runner synchronously, giving deterministic, frame-independent allocation measurements.

## Consumer project setup

FeatherTween's tests only run inside a Unity project that consumes the package. Two setup steps are required in that **consuming project**.

### Make tests visible in Test Runner

When FeatherTween is consumed as a UPM package (linked via `file:`), Unity hides its tests by default. The test asmdefs use the `UNITY_INCLUDE_TESTS` define constraint, which is only active for packages listed as `testables`. Add the package to the consuming project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.dyvoid.feathertween": "file:../path/to/Tween"
  },
  "testables": [
    "com.dyvoid.feathertween"
  ]
}
```

The name must match `package.json` (`com.dyvoid.feathertween`). Embedding the source under `Assets/` instead would surface tests automatically, but the `testables` entry is the correct mechanism for the package workflow.

### Install the performance test dependency

The performance asmdef references `Unity.PerformanceTesting`. The consuming project must have the package installed (it is test-only):

```json
{
  "dependencies": {
    "com.unity.test-framework.performance": "3.0.3"
  }
}
```

Use the version that resolves for your Unity 6000.3 install if 3.0.3 is unavailable.
