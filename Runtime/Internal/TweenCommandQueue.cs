using System.Collections.Generic;
using UnityEngine;

namespace Dyvoid.FeatherTween.Internal
{
	// Reentrancy guard: structural mutation (Kill / Complete / Restart /
	// Reverse) issued from inside a callback is deferred and executed either
	// at end of tick (when the runner is ticking) or when the outermost
	// callback returns (handle ops invoked outside a tick). See Documentation~/api/handles.md.
	//
	// Boundary-walk note (phase 1.10): commands carry no notion of playback
	// direction or tick phase; they replay handle ops verbatim, so a
	// bidirectional AdvanceTo drains the same queue unchanged.
	internal static class TweenCommandQueue
	{
		internal enum Op : byte
		{
			Kill = 0,
			KillComplete = 1,
			Complete = 2,
			Restart = 3,
			Reverse = 4,
			SequenceInsert = 5,
		}

		private struct Command
		{
			public Op Op;
			public int Id;
			public uint Gen;
			// SequenceInsert payload: the pre-built detached child and its window.
			public int ChildId;
			public uint ChildGen;
			public double Time;
			public double Length;
			public bool Infinite;
		}

		private const int DrainCap = 65536;

		private static readonly List<Command> queue = new List<Command>(32);
		private static int callbackDepth;
		private static bool inTick;
		private static bool draining;

		public static bool InCallback => callbackDepth > 0;
		public static int PendingCount => queue.Count;

		public static void EnterCallback()
		{
			callbackDepth++;
		}

		public static void ExitCallback()
		{
			callbackDepth--;
			if (callbackDepth == 0 && !inTick && !draining)
			{
				Drain();
			}
		}

		public static void BeginTick()
		{
			inTick = true;
		}

		public static void EndTick()
		{
			inTick = false;
			Drain();
		}

		// Returns true when the op was deferred; caller must not execute it.
		public static bool TryDefer(Op op, int id, uint gen)
		{
			if (callbackDepth == 0)
			{
				return false;
			}
			queue.Add(new Command { Op = op, Id = id, Gen = gen });
			return true;
		}

		// Mid-play Sequence.Insert from inside a callback: the child is already
		// built and detached in the store; only the entry splice is deferred.
		public static bool TryDeferInsert(int seqId, uint seqGen, int childId, uint childGen, double time, double length, bool infinite)
		{
			if (callbackDepth == 0)
			{
				return false;
			}
			queue.Add(new Command
			{
				Op = Op.SequenceInsert,
				Id = seqId,
				Gen = seqGen,
				ChildId = childId,
				ChildGen = childGen,
				Time = time,
				Length = length,
				Infinite = infinite,
			});
			return true;
		}

		public static void Drain()
		{
			if (draining || queue.Count == 0)
			{
				return;
			}
			draining = true;
			try
			{
				var executed = 0;
				// Commands enqueued by callbacks fired during the drain are
				// processed in the same drain, in order.
				for (var i = 0; i < queue.Count; i++)
				{
					if (++executed > DrainCap)
					{
						Debug.LogError(
							"[FeatherTween] Deferred-command drain exceeded 65536 commands; " +
							"likely a callback cycle (e.g. OnComplete restarting itself " +
							"and completing synchronously). Remaining commands dropped.");
						break;
					}
					var cmd = queue[i];
					Execute(cmd);
				}
				queue.Clear();
			}
			finally
			{
				draining = false;
			}
		}

		public static void Reset()
		{
			queue.Clear();
			callbackDepth = 0;
			inTick = false;
			draining = false;
		}

		private static void Execute(Command cmd)
		{
			switch (cmd.Op)
			{
				case Op.Kill:
					TweenOps.Kill(cmd.Id, cmd.Gen, complete: false, allowDefer: false);
					break;
				case Op.KillComplete:
					TweenOps.Kill(cmd.Id, cmd.Gen, complete: true, allowDefer: false);
					break;
				case Op.Complete:
					TweenOps.Complete(cmd.Id, cmd.Gen, allowDefer: false);
					break;
				case Op.Restart:
					TweenOps.Restart(cmd.Id, cmd.Gen, allowDefer: false);
					break;
				case Op.Reverse:
					TweenOps.Reverse(cmd.Id, cmd.Gen, allowDefer: false);
					break;
				case Op.SequenceInsert:
				{
					var seq = TweenStore.Get(cmd.Id, cmd.Gen) as SequenceData;
					if (seq != null)
					{
						seq.InsertChild(cmd.ChildId, cmd.ChildGen, cmd.Time, cmd.Length, cmd.Infinite);
					}
					else if (TweenStore.IsAlive(cmd.ChildId, cmd.ChildGen))
					{
						// Sequence died before the drain; don't leak the built child.
						TweenStore.Free(cmd.ChildId);
					}
					break;
				}
			}
		}
	}
}
