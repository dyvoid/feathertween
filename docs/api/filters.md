# Filters and Bulk Operations

Static methods on `FeatherTween` for finding or terminating groups of tweens.

```csharp
FT.Kill(target);                    // by target (object)
FT.Kill(id: 42);                    // by int id
FT.KillAll();
FT.PauseAll(); 
FT.ResumeAll();
FT.IsTweening(target);
```

`Kill(target)` resolves through a target-indexed multimap maintained on `Start` / `Kill` / `Recycle`. It is O(k) where k is the number of tweens owned by that target. `Free()` is an O(1) swap-remove from the active list via a slot→index map.

`KillAll`/`PauseAll`/`ResumeAll` operate on root tweens only; children follow their parent sequence's state.
