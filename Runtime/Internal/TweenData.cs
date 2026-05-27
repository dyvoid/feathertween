using System;
using System.Collections.Generic;

namespace PATween.Internal
{
	internal class TweenData
	{
		private TweenStatus status;
		private UpdatePhase phase;
		private bool autoKill;
		private List<Action> onComplete;
		private List<Action> onKill;

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
			onComplete?.Clear();
			onKill?.Clear();
		}
	}
}
