using System;
using dyvoid.FeatherTween.Internal;

namespace dyvoid.FeatherTween
{
	/// <summary>
	/// Immutable handle to a running tween, returned by <c>TweenBuilder&lt;T&gt;.Start()</c>.
	/// Generation-checked: after the tween dies, every method no-ops and every
	/// property reads a dead default — a stale handle is always safe to hold.
	/// </summary>
	public readonly struct Tween : IEquatable<Tween>
	{
		private readonly int id;
		private readonly uint generation;

		internal int Id => id;
		internal uint Generation => generation;

		/// <summary>True while the handle refers to a live (not yet recycled) tween.</summary>
		public bool IsAlive => TweenStore.IsAlive(id, generation);

		/// <summary>Current lifecycle state; <see cref="TweenStatus.Disposed"/> when dead.</summary>
		public TweenStatus Status
		{
			get
			{
				var data = TweenStore.Get(id, generation);
				return data == null ? TweenStatus.Disposed : data.Status;
			}
		}

		/// <summary>Progress across all loops [0,1]; an infinite loop reports progress within its current cycle. Mirrors <c>Sequence.TotalProgress</c>.</summary>
		public float TotalProgress
		{
			get
			{
				var data = TweenStore.Get(id, generation);
				return data == null ? 0f : data.TotalProgress;
			}
		}

		/// <summary>One cycle in seconds, excluding delays. Mirrors <c>Sequence.Duration</c>.</summary>
		public float Duration
		{
			get
			{
				var data = TweenStore.Get(id, generation);
				return data == null ? 0f : (float)data.CycleDuration;
			}
		}

		internal Tween(int id, uint generation)
		{
			this.id = id;
			this.generation = generation;
		}

		/// <summary>Starts playback if the tween was created paused or is delayed.</summary>
		public void Play() => TweenOps.Play(id, generation);

		/// <summary>Halts playback, keeping the playhead; resume with <see cref="Resume"/>.</summary>
		public void Pause() => TweenOps.Pause(id, generation);

		/// <summary>Resumes playback from the paused playhead.</summary>
		public void Resume() => TweenOps.Resume(id, generation);

		/// <summary>Rewinds to the start (including the initial delay) and plays forward.</summary>
		public void Restart() => TweenOps.Restart(id, generation);

		/// <summary>Flips playback direction mid-flight; delays are part of the timeline and are traversed backward too.</summary>
		public void Reverse() => TweenOps.Reverse(id, generation);

		/// <summary>Jumps to the end of the final cycle, applies end values, and fires <c>OnComplete</c>.</summary>
		public void Complete() => TweenOps.Complete(id, generation);

		/// <summary>Per-tween playback rate; composes with phase and global scales.</summary>
		public void SetTimeScale(float scale) => TweenOps.SetTimeScale(id, generation, scale);

		/// <summary>Repositions the playhead on the tween's local timeline (the initial delay occupies [0, delay)). Preserves play/pause state.</summary>
		public void Seek(float time, bool fireCallbacks = false) => TweenOps.Seek(id, generation, time, fireCallbacks);

		/// <summary>Stops and frees the tween; with <paramref name="complete"/> it first jumps to its end values.</summary>
		public void Kill(bool complete = false) => TweenOps.Kill(id, generation, complete);

		/// <summary>
		/// Makes <c>await tween</c> work; the compiler calls this, you do not. Resumes when the
		/// tween ends, completed or killed, and immediately if the handle is already dead.
		/// No UniTask or <c>Awaitable</c> dependency — see ADR 0013.
		/// </summary>
		public TweenAwaiter GetAwaiter() => new TweenAwaiter(id, generation);

		/// <summary>
		/// An <c>Awaitable</c> that completes when the tween ends, for composing with Unity's own
		/// async APIs or converting to UniTask with <c>AsUniTask()</c>. Prefer <c>await tween</c>
		/// directly when you just want to wait. Each call returns a fresh <c>Awaitable</c>; never
		/// await the same instance twice, as Unity pools them.
		/// </summary>
		public UnityEngine.Awaitable WaitForCompletion() => AwaitOps.WaitForEnd(id, generation);

		/// <summary>Coroutine equivalent: <c>yield return tween.ToYieldInstruction();</c>.</summary>
		public TweenYieldInstruction ToYieldInstruction() => new TweenYieldInstruction(id, generation);

		/// <summary>Absolute remaining cycle count, counting the in-progress cycle as the first; negative means loop forever.</summary>
		public void SetRemainingCycles(int cycles)
		{
			var data = TweenStore.Get(id, generation);
			data?.SetRemainingCyclesAbsolute(cycles);
		}

		/// <summary>Graceful stop for looping tweens: complete at the next cycle boundary going forward, settling on the end value.</summary>
		public void CompleteAtCycleEnd()
		{
			var data = TweenStore.Get(id, generation);
			data?.SetStopAtNextBoundary(true);
		}

		/// <summary>Graceful stop for looping tweens: complete on a backward/reversed cycle crossing, settling on the start value.</summary>
		public void CompleteAtCycleStart()
		{
			var data = TweenStore.Get(id, generation);
			data?.SetStopAtNextBoundary(false);
		}

		// Late subscriptions on a dead handle are no-ops: the handle cannot know
		// whether its record completed or was killed, so firing either callback
		// would be a guess (phase 1.15; Documentation~/api/handles.md).
		/// <summary>Subscribes to completion. On a dead handle this is a no-op (it cannot know whether the record completed or was killed).</summary>
		public Tween OnComplete(Action cb)
		{
			var data = TweenStore.Get(id, generation);
			data?.AddOnComplete(cb);
			return this;
		}

		/// <summary>Subscribes to kill (not fired on completion). No-op on a dead handle.</summary>
		public Tween OnKill(Action cb)
		{
			var data = TweenStore.Get(id, generation);
			data?.AddOnKill(cb);
			return this;
		}

		/// <summary>Subscribes to per-cycle completion (each loop boundary). No-op on a dead handle.</summary>
		public Tween OnStepComplete(Action cb)
		{
			var data = TweenStore.Get(id, generation);
			data?.AddOnStepComplete(cb);
			return this;
		}

		/// <summary>Two handles are equal when they refer to the same tween instance (same slot and generation).</summary>
		public bool Equals(Tween other) => id == other.id && generation == other.generation;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is Tween other && Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode() => unchecked((int)(id * 397 ^ generation));

		/// <summary>Handle equality (same slot and generation).</summary>
		public static bool operator ==(Tween a, Tween b) => a.Equals(b);

		/// <summary>Handle inequality.</summary>
		public static bool operator !=(Tween a, Tween b) => !a.Equals(b);
	}
}
