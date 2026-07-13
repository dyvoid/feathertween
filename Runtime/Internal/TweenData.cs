using System;
using System.Collections.Generic;

namespace Dyvoid.FeatherTween.Internal
{
	internal class TweenData
	{
		private TweenStatus status;
		private UpdatePhase phase;
		private bool autoKill;
		private bool ignoreTimeScale;
		private object target;
		private bool isUnityObject;
		private float timeScale = 1f;
		private int direction = 1;
		private int selfId = -1;
		private bool startFired;
		private bool safeMode = SafeModeDefault.Value;
		private bool cancelOnError;

		private List<CallbackEntry> onStart;
		private List<CallbackEntry> onPlay;
		private List<CallbackEntry> onPause;
		private List<Action<float>> onUpdate;
		private List<CallbackEntry> onStepComplete;
		private List<CallbackEntry> onComplete;
		private List<CallbackEntry> onKill;
		private List<CallbackEntry> onRewind;

		public int Direction
		{
			get => direction;
			set => direction = value == 0 ? 1 : (value > 0 ? 1 : -1);
		}

		// Store slot index, set by TweenStore.SetData/SetDataDetached. Lets data
		// free itself (e.g. a sequence cancelling from inside its own Step).
		public int SelfId
		{
			get => selfId;
			set => selfId = value;
		}

		// True once OnStart/OnPlay fired for the current playhead lifecycle;
		// ResetPlayhead re-arms.
		protected bool StartFired
		{
			get => startFired;
			set => startFired = value;
		}

		public virtual void SetRemainingCyclesAbsolute(int cycles) { }
		public virtual void SetStopAtNextBoundary(bool stopAtEndValue) { }

		public virtual void ResetPlayhead()
		{
			startFired = false;
		}

		public virtual void ForceComplete() { }
		public virtual bool StartsDelayed() => false;

		// Repositions the playhead (docs/api/handles.md). Silent (fireCallbacks=false) renders a
		// single sample at the target; firing walks loop boundaries in temporal
		// order (OnStepComplete forward, OnRewind backward). Never changes Status.
		public virtual void SeekTo(double seconds, bool fireCallbacks) { }

		// Deferred start-value capture: root tweens resolve at Start(), sequenced
		// children resolve when the parent playhead first crosses their window.
		public virtual void ResolveStartValues() { }
		public virtual void RearmStartValues() { }

		// Called by TweenStore.Free after the slot is released; sequences use it
		// to cascade-free their child slots.
		public virtual void OnFree() { }

		// Recycles the record into its per-type pool (deferred to end of tick by
		// TweenStore). SequenceData is not pooled: its entry array is unique per
		// build, so recycling would only save the small header object.
		public virtual void ReturnToPool() { }

		public TweenStatus Status
		{
			get => status;
			set => status = value;
		}

		public UpdatePhase Phase
		{
			get => phase;
			set => phase = value;
		}

		public bool AutoKill
		{
			get => autoKill;
			set => autoKill = value;
		}

		public bool IgnoreTimeScale
		{
			get => ignoreTimeScale;
			set => ignoreTimeScale = value;
		}

		public object Target
		{
			get => target;
			set
			{
				target = value;
				isUnityObject = value is UnityEngine.Object;
			}
		}

		public bool IsUnityObject => isUnityObject;

		// Per-tween try/catch around setter and callback invocations. Compiled
		// out entirely under FEATHERTWEEN_RELEASE (docs/architecture/overview.md).
		public bool SafeMode
		{
			get => safeMode;
			set => safeMode = value;
		}

		// With safe mode: a setter exception kills the tween silently and fires
		// OnKill; a callback exception cancels the tween (deferred) after logging.
		// Without it, safe mode logs the setter exception and kills without OnKill.
		public bool CancelOnError
		{
			get => cancelOnError;
			set => cancelOnError = value;
		}

		// Per-tween playback rate. Engine-side: applies to scaled and unscaled
		// time alike (IgnoreTimeScale only opts out of Unity's Time.timeScale).
		// Negative values are rejected in TweenOps; direction is owned by Reverse.
		public float TimeScale
		{
			get => timeScale;
			set => timeScale = value;
		}

		public virtual void Step(double scaledDelta, double unscaledDelta)
		{
		}

		public void AddOnStart(Action cb) => Add(ref onStart, CallbackEntry.FromAction(cb), cb != null);
		public void AddOnPlay(Action cb) => Add(ref onPlay, CallbackEntry.FromAction(cb), cb != null);
		public void AddOnPause(Action cb) => Add(ref onPause, CallbackEntry.FromAction(cb), cb != null);
		public void AddOnStepComplete(Action cb) => Add(ref onStepComplete, CallbackEntry.FromAction(cb), cb != null);
		public void AddOnComplete(Action cb) => Add(ref onComplete, CallbackEntry.FromAction(cb), cb != null);
		public void AddOnKill(Action cb) => Add(ref onKill, CallbackEntry.FromAction(cb), cb != null);
		public void AddOnRewind(Action cb) => Add(ref onRewind, CallbackEntry.FromAction(cb), cb != null);

		public void AddOnComplete(CallbackEntry entry) => Add(ref onComplete, entry, true);
		public void AddOnKill(CallbackEntry entry) => Add(ref onKill, entry, true);

		public void AddOnUpdate(Action<float> cb)
		{
			if (cb == null)
			{
				return;
			}
			onUpdate ??= new List<Action<float>>();
			onUpdate.Add(cb);
		}

		public void InvokeOnStart() => InvokeList(onStart);
		public void InvokeOnPlay() => InvokeList(onPlay);
		public void InvokeOnPause() => InvokeList(onPause);
		public void InvokeOnStepComplete() => InvokeList(onStepComplete);
		public void InvokeOnComplete() => InvokeList(onComplete);
		public void InvokeOnKill() => InvokeList(onKill);
		public void InvokeOnRewind() => InvokeList(onRewind);

		public void InvokeOnUpdate(float easedT)
		{
			if (onUpdate == null)
			{
				return;
			}
			TweenCommandQueue.EnterCallback();
			try
			{
				for (var i = 0; i < onUpdate.Count; i++)
				{
#if !FEATHERTWEEN_RELEASE
					if (safeMode)
					{
						try
						{
							onUpdate[i]?.Invoke(easedT);
						}
						catch (Exception e)
						{
							HandleCallbackError(e);
						}
						continue;
					}
#endif
					onUpdate[i]?.Invoke(easedT);
				}
			}
			finally
			{
				TweenCommandQueue.ExitCallback();
			}
		}

		// Fires OnStart + the initial OnPlay exactly once per playhead lifecycle,
		// on the first tick that actually renders (matches the no-first-frame-pop
		// snap timing). Subsequent Resume/Play fire OnPlay via TweenOps.
		public void FireStartIfPending()
		{
			if (startFired)
			{
				return;
			}
			startFired = true;
			InvokeOnStart();
			InvokeOnPlay();
		}

		private static void Add(ref List<CallbackEntry> list, CallbackEntry entry, bool valid)
		{
			if (!valid)
			{
				return;
			}
			list ??= new List<CallbackEntry>();
			list.Add(entry);
		}

		private void InvokeList(List<CallbackEntry> list)
		{
			if (list == null)
			{
				return;
			}
			TweenCommandQueue.EnterCallback();
			try
			{
				for (var i = 0; i < list.Count; i++)
				{
#if !FEATHERTWEEN_RELEASE
					if (safeMode)
					{
						try
						{
							list[i].Invoke();
						}
						catch (Exception e)
						{
							HandleCallbackError(e);
						}
						continue;
					}
#endif
					list[i].Invoke();
				}
			}
			finally
			{
				TweenCommandQueue.ExitCallback();
			}
		}

#if !FEATHERTWEEN_RELEASE
		// Safe-mode setter exception: the value write failed mid-step, so the
		// animation contract is broken — kill the tween. CancelOnError kills
		// silently and fires OnKill; without it, log and dispose without OnKill
		// (docs/api/handles.md firing matrix).
		protected void CancelFromError(Exception e)
		{
			if (!cancelOnError)
			{
				UnityEngine.Debug.LogException(e);
			}
			Status = TweenStatus.Cancelled;
			if (cancelOnError)
			{
				InvokeOnKill();
			}
			if (selfId >= 0)
			{
				TweenStore.Free(selfId);
			}
		}

		// Safe-mode callback exception: user-code side effect, always logged.
		// Remaining callbacks in the list still run; CancelOnError additionally
		// cancels the tween (deferred — we are inside a callback scope).
		protected void HandleCallbackError(Exception e)
		{
			UnityEngine.Debug.LogException(e);
			if (cancelOnError && selfId >= 0
				&& (status == TweenStatus.Playing
					|| status == TweenStatus.Delayed
					|| status == TweenStatus.Paused))
			{
				TweenOps.Kill(selfId, TweenStore.GetGeneration(selfId), complete: false);
			}
		}
#endif

		public virtual void Reset()
		{
			status = TweenStatus.Disposed;
			phase = UpdatePhase.Update;
			autoKill = true;
			ignoreTimeScale = false;
			target = null;
			isUnityObject = false;
			timeScale = 1f;
			direction = 1;
			selfId = -1;
			startFired = false;
			safeMode = SafeModeDefault.Value;
			cancelOnError = false;
			onStart?.Clear();
			onPlay?.Clear();
			onPause?.Clear();
			onUpdate?.Clear();
			onStepComplete?.Clear();
			onComplete?.Clear();
			onKill?.Clear();
			onRewind?.Clear();
		}
	}
}
