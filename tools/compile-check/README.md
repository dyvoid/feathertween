# compile-check — .NET test harness (no Unity required)

Compiles the package Runtime, Samples, and EditMode tests against a minimal
`UnityEngine` stub (`UnityStubs.cs`) and runs the EditMode suite via NUnitLite.
This is what CI runs on every push; it covers everything except code that needs
the real Unity runtime (PlayerLoop injection, PlayMode tests).

```sh
# run the EditMode suite (excludes tests tagged RequiresUnity)
dotnet run --project PATween.TestRunner.csproj -c Release -- --noresult --where "cat != RequiresUnity"

# compile-check Runtime + Samples only
dotnet build PATween.CompileCheck.csproj
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
  `docs/guides/testing.md`).
