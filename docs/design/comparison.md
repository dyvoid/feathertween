# Engine Comparison

How PATween's design choices compare to DOTween, GSAP, PrimeTween, and LitMotion. Use this table to understand where PATween sits on the trade-off spectrum: static vs extension API, struct vs reference handles, managed vs Burst hot path, and so on.

| Concern              | DOTween                  | GSAP                          | PrimeTween                          | LitMotion                                | PATween                                      |
|----------------------|--------------------------|-------------------------------|-------------------------------------|------------------------------------------|----------------------------------------------|
| Public handle        | class reference          | object reference              | struct + isAlive                    | struct (StorageId,Index,Version)         | struct handle + generation                   |
| Builder vs handle    | merged                   | merged                        | merged (config = args)              | split (MotionBuilder → MotionHandle)      | split (TweenBuilder → Tween, explicit Start) |
| Storage              | pooled classes           | objects + linked list         | pooled (closed)                     | unmanaged[]+managed[] SoA, Burst         | pooled classes (SoA-ready, Burst in M5)      |
| Runner               | hidden MonoBehaviour     | rAF + global timeline         | hidden MonoBehaviour                | PlayerLoop (8 phases) + editor mirror    | PlayerLoop (3+Manual) + editor mirror        |
| Nested sequences     | sequences, flat root     | universal parent-timeline     | flat sequences with nesting         | sequence = tween + (time, child) list    | universal parent-sequence (from M1)        |
| Shortcut style       | extensions on Unity types| n/a                           | static on `Tween`                   | none (just `LMotion.Create` + `Bind`)    | static on `PATween`                          |
| Position param       | typed only               | string DSL + numeric          | typed (Group/Chain/Insert)          | typed (Append/Insert/Join)               | typed `Position` (M1), string parse (M4)   |
| Reverse              | yes                      | yes                           | no (fire new tween)                 | no (use PlaybackSpeed)                   | yes (derived from sequence model)            |
| Generic core         | lambda getter/setter     | string property names         | lambda + target-capture             | lambda + 0/1/2/3-state target-capture    | lambda + target-capture                      |
| Custom value types   | plugin class             | n/a                           | extension methods on `Tween`        | `IMotionAdapter<TValue,TOptions>`        | `IInterpolator<T>`                           |
| Eases                | enum + curve + delegate  | string + parameterized        | enum + curve + Easing factories     | enum + curve (native, Burst-friendly)    | `EaseRef` via `Easing.X(...)` factories      |
| Hot loop             | managed                  | managed (JS)                  | managed                             | [BurstCompile] IJobParallelFor           | managed (M1–M4), Burst job (M5)              |
| Auto-kill            | yes, per frame           | n/a                           | yes, per frame                      | manual via SetLink                       | yes, per frame for UnityEngine.Object        |
| Awaitable            | Task + coroutine yield   | promise (then)                | async/await + yield                 | MotionAwaiter + UniTask                  | core struct awaiter + UniTask asmdef         |
| Designer serialize   | TweenParams-ish          | n/a                           | `TweenSettings<T>` + inspector      | `SerializableMotionSettings<T,O>`        | `TweenSettings`/`TweenSettings<T>` + drawer  |
| Safe mode            | yes                      | implicit JS try/catch         | yes (auto on destroy)               | per-tween CancelOnError                  | yes, default on in editor + per-tween        |
| Status granularity   | IsPlaying/IsComplete     | n/a                           | isAlive only                        | full enum (Scheduled→Disposed)            | full `TweenStatus` enum                      |
| Delay semantics      | first-loop only          | first-loop only               | first-loop only                     | FirstLoop or EveryLoop                   | FirstLoop or EveryLoop                       |

## Naming and packaging

- Asmdef: `PATween` (runtime), `PATween.Editor` (editor), `PATween.UniTask` (optional)
- Root namespace: `PATween`
- Unity package: `com.<vendor>.patween`
- Minimum Unity: 6000.3
