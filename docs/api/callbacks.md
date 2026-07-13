# Callbacks

## Setting callbacks

Set on the builder:

```csharp
.OnStart(Action)
.OnPlay(Action)
.OnPause(Action)
.OnUpdate(Action<float>)
.OnStepComplete(Action)
.OnComplete(Action)
.OnKill(Action)
.OnRewind(Action)
```

Each has a plain and a target-capture overload. Implemented as direct delegates, not delegate lists or params arrays, so invocation allocates zero managed bytes.

## Late subscription

On the `Tween`/`Sequence` handle, only completion-shaped events can be subscribed after `.Start()`:

```csharp
public Tween OnComplete(Action cb);     // multicast
public Tween OnKill(Action cb);         // multicast
public Tween OnStepComplete(Action cb); // multicast
```

Other callbacks (`OnUpdate`, `OnStart`, `OnPlay`, `OnPause`, `OnRewind`) must be set during build.

Both builder-side and handle-side subscriptions for the completion-shaped events are **multicast**: each call appends to one invocation list per slot. Multiple builder-side `OnComplete` calls on the same builder append in call order; the second does not overwrite the first. Handlers fire in registration order. The list is allocated lazily on second subscription and pooled on tween recycle.

## Capture discipline

Target-capture overloads only avoid allocation when the lambda body does not capture any outer variables:

```csharp
// Zero alloc: static lambda, only the supplied state parameter is used.
FT.To(this, () => x.value, (s, v) => s.x.value = v, 10f, 1f)
    .OnComplete(this, s => s.HandleDone())
    .Start();
```

If you reference `this`, a local, or any field outside the supplied state parameter, the C# compiler emits a closure-allocating delegate and the zero-alloc benefit is lost. Use `static` lambdas where possible. An optional Roslyn analyzer (M2) can enforce this.

## Timing details

- `OnStart` and the initial `OnPlay` fire on the first tick that actually renders (after any delay), once per playhead lifecycle; `Restart` re-arms them.
- Subsequent `Resume`/`Play` fire `OnPlay` each time.
- `OnUpdate(float)` receives the eased in-cycle progress on a tween (`1f` at every cycle end regardless of ease shape) and the normalized playhead (`0..1`) on a sequence.
- Target-capture overloads exist on `OnComplete`/`OnKill` only; all other slots take plain delegates.

See [handles.md](handles.md) for the lifecycle state machine and the `OnComplete` vs `OnKill` firing matrix.
