using System;
using System.Collections.Generic;

namespace PATween.Internal
{
	internal class TweenData
	{
		private TweenStatus status;
		private UpdatePhase phase;
		private bool autoKill;
		private bool ignoreTimeScale;
		private object target;
		private bool isUnityObject;
		private int direction = 1;
		private List<Action> onComplete;
		private List<Action> onKill;
		private List<Action> onRewind;

		public int Direction
		{
			get => direction;
			set => direction = value == 0 ? 1 : (value > 0 ? 1 : -1);
		}

		public virtual void SetRemainingCyclesAbsolute(int cycles) { }
		public virtual void SetStopAtNextBoundary(bool stopAtEndValue) { }

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

		public virtual void Step(double scaledDelta, double unscaledDelta)
		{
		}

		public void AddOnComplete(Action cb)
		{
			if (cb == null)
			{
				return;
			}
			onComplete ??= new List<Action>();
			onComplete.Add(cb);
		}

		public void AddOnKill(Action cb)
		{
			if (cb == null)
			{
				return;
			}
			onKill ??= new List<Action>();
			onKill.Add(cb);
		}

		public void AddOnRewind(Action cb)
		{
			if (cb == null)
			{
				return;
			}
			onRewind ??= new List<Action>();
			onRewind.Add(cb);
		}

		public void InvokeOnRewind()
		{
			if (onRewind == null)
			{
				return;
			}
			for (var i = 0; i < onRewind.Count; i++)
			{
				onRewind[i]?.Invoke();
			}
		}

		public void InvokeOnComplete()
		{
			if (onComplete == null)
			{
				return;
			}
			for (var i = 0; i < onComplete.Count; i++)
			{
				onComplete[i]?.Invoke();
			}
		}

		public void InvokeOnKill()
		{
			if (onKill == null)
			{
				return;
			}
			for (var i = 0; i < onKill.Count; i++)
			{
				onKill[i]?.Invoke();
			}
		}

		public virtual void Reset()
		{
			status = TweenStatus.Disposed;
			phase = UpdatePhase.Update;
			autoKill = true;
			ignoreTimeScale = false;
			target = null;
			isUnityObject = false;
			direction = 1;
			onComplete?.Clear();
			onKill?.Clear();
			onRewind?.Clear();
		}
	}
}
