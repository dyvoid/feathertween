# Code Conventions

Canonical C# style for PATween. AI agents additionally apply the project's Unity dev skill, which encodes the same rules in more detail; this document is the human-facing source of truth.

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
