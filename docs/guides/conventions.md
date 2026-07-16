# Code Conventions

Canonical C# style for FT. AI agents additionally apply the project's Unity dev skill, which encodes the same rules in more detail; this document is the human-facing source of truth.

## Formatting

- Tab size 4, keep tabs (literal tabs, not spaces).
- Braces on their own line. Always use braces for `if` / `else`, even single-statement.
- One statement and one declaration per line.
- Attributes one per line for classes and methods. Short field attributes may be inline if still readable.
- Blank line between methods/properties; no double blank lines; newline at end of file.

## Naming

- **PascalCase**: classes, structs, enums, methods, properties, events, constructors, public constants.
- **camelCase**: private/protected/internal fields, parameters, locals.
- Interfaces prefix with `I`.
- Boolean names use affirmative/negative phrasing (`isAlive`, `hasAmmo`).
- File and folder names PascalCase, English, no spaces.

## Public API shape rules

These are API-design invariants (ADR 0011), not just style:

- **Creation methods are subject-first**: the thing being animated comes first (a typed target like `transform`, or the getter/setter pair — shrinking to a lone setter when the engine never reads the value), then endpoint value(s), then duration. Endpoint parameters are named `from` / `to` (with a qualifying suffix where the value needs one, e.g. `toAlpha`).
- **Sequence composition methods are position-first**: `(position, child)`, as in `Insert(time, child)`; `Append`/`Join`/`Prepend` derive the position. Every child-taking method accepts both `TweenBuilder<T>` and `SequenceBuilder`. One name per operation — no aliases.
- **Timeline positions are named `time`; spans/durations are named `seconds`** (`Insert(time, …)`, `Seek(time)`, `AppendInterval(seconds)`, `SetDelay(seconds)`).
- **Enum parameters are named after their type** in camelCase (`loopType`, `delayType`).
- **Negative time inputs throw** (`ArgumentOutOfRangeException`); nothing silently clamps.
- **Callbacks keep the `Callback` suffix in sequence composition**: `AppendCallback(Action)` is the only spelling — no `Append(Action)` sugar, which would ambiguate with `Append(TweenBuilder<T>)`/`Append(SequenceBuilder)` composition (decided 2026-07-16).
- **Rotation default is shortest-path slerp** (`Quaternion.SlerpUnclamped`); euler overloads are pure conversions. Any alternative rotate mode is a new additive parameter, never a change to the default.
- **Manual ticking is global-only**: `FT.ManualTick(dt)` drives the `Manual` phase; there is no per-tween `Tick`.

## Static API usage in samples and docs

The public entry point is the static class `FT`. Call it as `FT.To(...)`, `FT.Sequence(...)`, `FT.Move(...)`, etc.

Do **not** use `using static Dyvoid.FeatherTween.FT;` in samples, documentation, or any FeatherTween-authored code. It dumps every static method into scope and shadows common Unity types (`Color`, `Image`, `Text`, etc.) and .NET primitive types, producing cryptic `CS0119` compiler errors.

## Access and fields

- Always declare visibility; default to `private`.
- Fields are never `public`/`internal`. Expose via `[SerializeField]` or a public property.
- Public properties have explicit `get`/`set` accessors. Expression-bodied (`=>`) is fine for simple read-only properties.

## Member layout order

Within a class, group members in this order, separated by blank lines:

1. Static properties / fields
2. Constants
3. Properties
4. Fields — `[SerializeField] protected`, then `[SerializeField] private`, then `protected`, then `private`
5. Constructors
6. Editor-only Unity callbacks (`Reset`, `OnValidate`)
7. Unity lifecycle (`Awake` → `OnEnable` → `Start` → per-frame → `OnDisable` → `OnDestroy`)
8. Other methods, grouped by visibility
9. Nested types

## Memory management

Dispose/clean up: instantiated objects, coroutines, `while` loops, event listeners, `Resources`-loaded assets, and any System.IO handles.

## Comments

- Start with a capital, end with a period. One space after `//`.
- XML comments only where they add value; use `<see cref="..."/>` for references.
- Do not add comments that merely describe a change being made.
- TODO comments state the required work, potential issues, and date.

## Misc

- Use `[Header]`, `[Space]`, `[Tooltip]` on serialized fields where helpful.
- Validate dependencies early (`Assert` in `Awake`).
- `var` unless the type is ambiguous; otherwise explicit.
- String interpolation for short dynamic strings; `StringBuilder`/`string.Format` for longer ones.
- Mark pure methods `static`.
