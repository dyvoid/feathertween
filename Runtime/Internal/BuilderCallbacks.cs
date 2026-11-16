using System;
using System.Collections.Generic;

namespace dyvoid.FeatherTween.Internal
{
	// Builder-side staging for lifecycle callbacks, shared by TweenBuilderBuffer
	// and SequenceBuilderBuffer so the two cannot drift. Lists are created on
	// first use and kept across pool cycles; Clear only empties them.
	internal sealed class BuilderCallbacks
	{
		private List<CallbackEntry> onStart;
		private List<CallbackEntry> onPlay;
		private List<CallbackEntry> onPause;
		private List<Action<float>> onUpdate;
		private List<CallbackEntry> onStepComplete;
		private List<CallbackEntry> onComplete;
		private List<CallbackEntry> onKill;
		private List<CallbackEntry> onRewind;

		public void AddOnStart(Action cb) => Add(ref onStart, cb);
		public void AddOnPlay(Action cb) => Add(ref onPlay, cb);
		public void AddOnPause(Action cb) => Add(ref onPause, cb);
		public void AddOnStepComplete(Action cb) => Add(ref onStepComplete, cb);
		public void AddOnComplete(Action cb) => Add(ref onComplete, cb);
		public void AddOnKill(Action cb) => Add(ref onKill, cb);
		public void AddOnRewind(Action cb) => Add(ref onRewind, cb);

		// Target-capture entries; only OnComplete and OnKill have that overload.
		public void AddOnComplete(CallbackEntry entry)
		{
			onComplete ??= new List<CallbackEntry>();
			onComplete.Add(entry);
		}

		public void AddOnKill(CallbackEntry entry)
		{
			onKill ??= new List<CallbackEntry>();
			onKill.Add(entry);
		}

		public void AddOnUpdate(Action<float> cb)
		{
			if (cb == null)
			{
				return;
			}
			onUpdate ??= new List<Action<float>>();
			onUpdate.Add(cb);
		}

		// Direct loops, no method-group arguments: converting data.AddOnX to a
		// delegate allocates even when the list is null, which broke the
		// zero-alloc creation budget (Documentation~/architecture/performance.md).
		// Entries outside OnComplete/OnKill are always plain.
		public void TransferTo(TweenData data)
		{
			if (onStart != null)
			{
				for (var i = 0; i < onStart.Count; i++) data.AddOnStart(onStart[i].Plain);
			}
			if (onPlay != null)
			{
				for (var i = 0; i < onPlay.Count; i++) data.AddOnPlay(onPlay[i].Plain);
			}
			if (onPause != null)
			{
				for (var i = 0; i < onPause.Count; i++) data.AddOnPause(onPause[i].Plain);
			}
			if (onStepComplete != null)
			{
				for (var i = 0; i < onStepComplete.Count; i++) data.AddOnStepComplete(onStepComplete[i].Plain);
			}
			if (onComplete != null)
			{
				for (var i = 0; i < onComplete.Count; i++) data.AddOnComplete(onComplete[i]);
			}
			if (onKill != null)
			{
				for (var i = 0; i < onKill.Count; i++) data.AddOnKill(onKill[i]);
			}
			if (onRewind != null)
			{
				for (var i = 0; i < onRewind.Count; i++) data.AddOnRewind(onRewind[i].Plain);
			}
			if (onUpdate != null)
			{
				for (var i = 0; i < onUpdate.Count; i++) data.AddOnUpdate(onUpdate[i]);
			}
		}

		public void Clear()
		{
			onStart?.Clear();
			onPlay?.Clear();
			onPause?.Clear();
			onUpdate?.Clear();
			onStepComplete?.Clear();
			onComplete?.Clear();
			onKill?.Clear();
			onRewind?.Clear();
		}

		private static void Add(ref List<CallbackEntry> list, Action cb)
		{
			if (cb == null)
			{
				return;
			}
			list ??= new List<CallbackEntry>();
			list.Add(CallbackEntry.FromAction(cb));
		}
	}
}
