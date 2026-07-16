# compile-check — .NET test harness (no Unity required)

This folder ends in `~` so Unity never imports it (same mechanism as `Samples~`).
Never place build output or DLLs in a Unity-visible package folder.

Compiles the package Runtime, Samples, and EditMode tests against a minimal
`UnityEngine` stub (`UnityStubs.cs`) and runs the EditMode suite via NUnitLite.
This is what CI runs on every push; it covers everything except code that needs
the real Unity runtime (PlayerLoop injection, PlayMode tests).

```sh
# run the EditMode suite (excludes tests tagged RequiresUnity)
dotnet run --project FeatherTween.TestRunner.csproj -c Release -- --noresult --where "cat != RequiresUnity"

# release leg: safe mode compiled out (excludes RequiresSafeMode; compiles in ReleaseModeTests)
dotnet run --project FeatherTween.TestRunner.csproj -c Release -p:DefineConstants=FEATHERTWEEN_RELEASE -- --noresult --where "cat != RequiresUnity && cat != RequiresSafeMode"

# compile-check Runtime + Samples only
dotnet build FeatherTween.CompileCheck.csproj
```

Rules of the stub:

- `UnityStubs.cs` is a syntax/behavior-minimal surface, extended on demand when
  new Unity APIs are used. Keep members trivial; the only deliberately faithful
  behavior is `UnityEngine.Object`'s destroyed-fake-null equality, which the
  auto-kill tests depend on.
- Tests that genuinely require the Unity runtime get `[Category("RequiresUnity")]`
  and are excluded here; they still run in the Unity Test Runner.
- This harness never replaces the in-Unity run — Editor + Runtime + Performance
  suites in a real Unity project remain the merge gate (see
  `Documentation~/guides/testing.md`).
