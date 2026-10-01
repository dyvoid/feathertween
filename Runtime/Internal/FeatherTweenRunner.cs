using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace dyvoid.FeatherTween.Internal
{
	internal static class FeatherTweenRunner
	{
		private static RootSequenceData rootUpdate;
		private static RootSequenceData rootLate;
		private static RootSequenceData rootFixed;
		private static RootSequenceData rootManual;

		private static readonly List<int> pendingKills = new List<int>(64);
		private static readonly List<uint> pendingKillGens = new List<uint>(64);
		private static readonly List<int> tickSnapshotIds = new List<int>(256);
		private static readonly List<uint> tickSnapshotGens = new List<uint>(256);

		private static int mainThreadId;
		private static bool installed;

		// True while a phase is being ticked. The tick works on shared static
		// scratch lists (snapshot, pending kills) and owns the deferred-command
		// drain, so a second tick started from inside the first one - a callback
		// or setter calling FT.ManualTick - would overwrite the outer snapshot
		// mid-iteration and drain the outer tick's commands early.
		private static bool ticking;

		internal static bool IsTicking => ticking;

		// Engine-side rate control, applied at the hidden roots so it composes
		// recursively with per-tween TimeScale. Distinct from Unity's
		// Time.timeScale: it scales the unscaled delta too, so IgnoreTimeScale
		// tweens are still governed by it.
		private static float globalTimeScale = 1f;
		private static float scaleUpdate = 1f;
		private static float scaleLate = 1f;
		private static float scaleFixed = 1f;
		private static float scaleManual = 1f;

		public static float GlobalTimeScale
		{
			get => globalTimeScale;
			set => globalTimeScale = value;
		}

		public static void SetPhaseTimeScale(UpdatePhase phase, float scale)
		{
			switch (phase)
			{
				case UpdatePhase.Update: scaleUpdate = scale; break;
				case UpdatePhase.Late: scaleLate = scale; break;
				case UpdatePhase.Fixed: scaleFixed = scale; break;
				case UpdatePhase.Manual: scaleManual = scale; break;
			}
		}

		public static float GetPhaseTimeScale(UpdatePhase phase)
		{
			switch (phase)
			{
				case UpdatePhase.Update: return scaleUpdate;
				case UpdatePhase.Late: return scaleLate;
				case UpdatePhase.Fixed: return scaleFixed;
				case UpdatePhase.Manual: return scaleManual;
				default: return 1f;
			}
		}

		public static RootSequenceData RootUpdate => rootUpdate;
		public static RootSequenceData RootLate => rootLate;
		public static RootSequenceData RootFixed => rootFixed;
		public static RootSequenceData RootManual => rootManual;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void RuntimeBootstrap()
		{
			Reset();
			Install();
		}

		public static void EnsureInitialized()
		{
			if (mainThreadId == 0 || rootManual == null)
			{
				Reset();
			}
		}

		public static void Reset()
		{
			mainThreadId = Thread.CurrentThread.ManagedThreadId;
			rootUpdate = new RootSequenceData(UpdatePhase.Update);
			rootLate = new RootSequenceData(UpdatePhase.Late);
			rootFixed = new RootSequenceData(UpdatePhase.Fixed);
			rootManual = new RootSequenceData(UpdatePhase.Manual);
			pendingKills.Clear();
			pendingKillGens.Clear();
			ticking = false;
			globalTimeScale = 1f;
			scaleUpdate = 1f;
			scaleLate = 1f;
			scaleFixed = 1f;
			scaleManual = 1f;
			TweenCommandQueue.Reset();
		}

		public static void Install()
		{
			if (installed)
			{
				return;
			}

			var loop = PlayerLoop.GetCurrentPlayerLoop();
			var ok = InsertAfter<Update.ScriptRunBehaviourUpdate>(ref loop, typeof(FeatherTweenUpdate), TickUpdate);
			ok &= InsertAfter<PreLateUpdate.ScriptRunBehaviourLateUpdate>(ref loop, typeof(FeatherTweenLateUpdate), TickLate);
			ok &= InsertAfter<FixedUpdate.ScriptRunBehaviourFixedUpdate>(ref loop, typeof(FeatherTweenFixedUpdate), TickFixed);
			if (!ok)
			{
				// A project that replaced the player loop can drop the anchor
				// systems; without this warning "tweens never tick" is opaque.
				Debug.LogWarning(
					"[FeatherTween] Could not find one or more PlayerLoop anchor systems; "
					+ "tweens in the affected phase(s) will not tick. Another system may have "
					+ "replaced the default player loop.");
			}
			PlayerLoop.SetPlayerLoop(loop);
			installed = true;
		}

		public static void Uninstall()
		{
			if (!installed)
			{
				return;
			}

			var loop = PlayerLoop.GetCurrentPlayerLoop();
			Remove(ref loop, typeof(FeatherTweenUpdate));
			Remove(ref loop, typeof(FeatherTweenLateUpdate));
			Remove(ref loop, typeof(FeatherTweenFixedUpdate));
			PlayerLoop.SetPlayerLoop(loop);
			installed = false;
		}

		public static void ManualTick(double deltaTime)
		{
			AssertMainThread();
			ThrowIfTicking();
			var root = globalTimeScale * scaleManual;
			var scaled = deltaTime * root;
			rootManual.Advance(scaled, scaled);
			TickActive(TweenStore.ActiveManual, scaled, scaled);
			LeakDetector.Drain();
		}

		internal static void TickEditorDelta(double deltaTime)
		{
			AssertMainThread();
			ThrowIfTicking();
			var root = globalTimeScale * scaleUpdate;
			var scaled = deltaTime * root;
			rootUpdate.Advance(scaled, scaled);
			TickActive(TweenStore.ActiveUpdate, scaled, scaled);
			LeakDetector.Drain();
		}

		// The PlayerLoop hooks stay installed after play mode ends, and Unity
		// runs custom PlayerLoop delegates in edit mode too. EditorRunner drives
		// edit-mode ticking (via TickEditorDelta), so the player-loop ticks must
		// not also fire there or edit-mode tweens would be double-advanced.
		private static bool SkipPlayerLoopTick()
		{
#if UNITY_EDITOR
			return !Application.isPlaying;
#else
			return false;
#endif
		}

		internal static void TickUpdate()
		{
			if (SkipPlayerLoopTick())
			{
				return;
			}
			AssertMainThread();
			ThrowIfTicking();
			var root = globalTimeScale * scaleUpdate;
			double scaled = Time.deltaTime * root;
			double unscaled = Time.unscaledDeltaTime * root;
			rootUpdate.Advance(scaled, unscaled);
			TickActive(TweenStore.ActiveUpdate, scaled, unscaled);
			LeakDetector.Drain();
		}

		internal static void TickLate()
		{
			if (SkipPlayerLoopTick())
			{
				return;
			}
			AssertMainThread();
			ThrowIfTicking();
			var root = globalTimeScale * scaleLate;
			double scaled = Time.deltaTime * root;
			double unscaled = Time.unscaledDeltaTime * root;
			rootLate.Advance(scaled, unscaled);
			TickActive(TweenStore.ActiveLate, scaled, unscaled);
			LeakDetector.Drain();
		}

		internal static void TickFixed()
		{
			if (SkipPlayerLoopTick())
			{
				return;
			}
			AssertMainThread();
			ThrowIfTicking();
			var root = globalTimeScale * scaleFixed;
			double scaled = Time.fixedDeltaTime * root;
			double unscaled = Time.fixedUnscaledDeltaTime * root;
			rootFixed.Advance(scaled, unscaled);
			TickActive(TweenStore.ActiveFixed, scaled, unscaled);
			LeakDetector.Drain();
		}

		private static void TickActive(List<int> active, double scaledDt, double unscaledDt)
		{
			ticking = true;
			TweenCommandQueue.BeginTick();
			try
			{
				TickActiveCore(active, scaledDt, unscaledDt);
			}
			finally
			{
				// Drains callbacks' deferred Kill/Complete/Restart/Reverse commands.
				TweenCommandQueue.EndTick();
				// Recycle freed records only after the drain: deferred commands may
				// free more tweens, and nothing is mid-step anymore.
				TweenStore.FlushPoolReturns();
				ticking = false;
			}
		}

		private static void ThrowIfTicking()
		{
			if (ticking)
			{
				throw new InvalidOperationException(
					"[FeatherTween] A tick is already in progress. FT.ManualTick cannot be called from "
					+ "inside a FeatherTween callback, setter or getter; call it from your own update loop.");
			}
		}

		private static void TickActiveCore(List<int> active, double scaledDt, double unscaledDt)
		{
			// Stale entries can survive a tick aborted by an exception (safe mode
			// off); freeing them now could kill unrelated tweens in re-used slots.
			pendingKills.Clear();
			pendingKillGens.Clear();
			tickSnapshotIds.Clear();
			tickSnapshotGens.Clear();
			for (var i = 0; i < active.Count; i++)
			{
				var snapId = active[i];
				tickSnapshotIds.Add(snapId);
				tickSnapshotGens.Add(TweenStore.GetGeneration(snapId));
			}

			for (var i = 0; i < tickSnapshotIds.Count; i++)
			{
				var id = tickSnapshotIds[i];
				if (!TweenStore.IsAlive(id, tickSnapshotGens[i]))
				{
					continue;
				}
				var data = TweenStore.GetByIndex(id);
				if (data == null)
				{
					continue;
				}

				if (data.IsUnityObject)
				{
					var uo = data.Target as UnityEngine.Object;
					if (uo == null)
					{
						// Same rule as the link kill below and TweenOps.Kill: a
						// completed record is at its terminal value and disposes
						// without callbacks (Documentation~/api/handles.md).
						if (data.Status != TweenStatus.Completed)
						{
							data.Status = TweenStatus.Cancelled;
							data.InvokeOnKill();
						}
						QueueKill(id, tickSnapshotGens[i]);
						continue;
					}
				}

				// Before the status gate: a link-paused tween must still be
				// polled, or PauseOnDisableResumeOnEnable could never resume.
				if (data.HasLink && data.PollLink())
				{
					// A completed record is already at its terminal value, so it
					// disposes without callbacks - the firing matrix in
					// Documentation~/api/handles.md, same rule as TweenOps.Kill.
					if (data.Status != TweenStatus.Completed)
					{
						data.Status = TweenStatus.Cancelled;
						data.InvokeOnKill();
					}
					QueueKill(id, tickSnapshotGens[i]);
					continue;
				}

				var status = data.Status;
				if (status == TweenStatus.Paused
					|| status == TweenStatus.Completed
					|| status == TweenStatus.Cancelled
					|| status == TweenStatus.Disposed)
				{
					continue;
				}

				data.Step(scaledDt, unscaledDt);

				if (data.Status == TweenStatus.Completed && data.AutoKill)
				{
					QueueKill(id, tickSnapshotGens[i]);
				}
			}

			if (pendingKills.Count > 0)
			{
				for (var i = 0; i < pendingKills.Count; i++)
				{
					// Generation-checked: if something freed the record earlier in
					// the tick and its slot was re-rented, the new tenant survives.
					if (TweenStore.IsAlive(pendingKills[i], pendingKillGens[i]))
					{
						TweenStore.Free(pendingKills[i]);
					}
				}
				pendingKills.Clear();
				pendingKillGens.Clear();
			}
		}

		private static void QueueKill(int id, uint generation)
		{
			pendingKills.Add(id);
			pendingKillGens.Add(generation);
		}

		private static bool InsertAfter<TAnchor>(ref PlayerLoopSystem loop, Type newType, PlayerLoopSystem.UpdateFunction update)
		{
			var anchorType = typeof(TAnchor);
			return InsertAfterRecursive(ref loop, anchorType, newType, update);
		}

		private static bool InsertAfterRecursive(ref PlayerLoopSystem system, Type anchorType, Type newType, PlayerLoopSystem.UpdateFunction update)
		{
			if (system.subSystemList == null)
			{
				return false;
			}

			for (var i = 0; i < system.subSystemList.Length; i++)
			{
				if (system.subSystemList[i].type == anchorType)
				{
					var inserted = new PlayerLoopSystem
					{
						type = newType,
						updateDelegate = update,
					};
					var newList = new PlayerLoopSystem[system.subSystemList.Length + 1];
					Array.Copy(system.subSystemList, 0, newList, 0, i + 1);
					newList[i + 1] = inserted;
					Array.Copy(system.subSystemList, i + 1, newList, i + 2, system.subSystemList.Length - i - 1);
					system.subSystemList = newList;
					return true;
				}

				if (InsertAfterRecursive(ref system.subSystemList[i], anchorType, newType, update))
				{
					return true;
				}
			}
			return false;
		}

		private static void Remove(ref PlayerLoopSystem system, Type target)
		{
			if (system.subSystemList == null)
			{
				return;
			}

			for (var i = 0; i < system.subSystemList.Length; i++)
			{
				if (system.subSystemList[i].type == target)
				{
					var newList = new PlayerLoopSystem[system.subSystemList.Length - 1];
					Array.Copy(system.subSystemList, 0, newList, 0, i);
					Array.Copy(system.subSystemList, i + 1, newList, i, system.subSystemList.Length - i - 1);
					system.subSystemList = newList;
					return;
				}
				Remove(ref system.subSystemList[i], target);
			}
		}

		// Off-thread guard, part of the safe-mode debug layer: compiled out
		// under FEATHERTWEEN_RELEASE along with the try/catch wrappers.
		private static void AssertMainThread()
		{
#if !FEATHERTWEEN_RELEASE
			if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
			{
				throw new InvalidOperationException(
					"[FeatherTween] FeatherTweenRunner must be ticked from the main thread.");
			}
#endif
		}

		private struct FeatherTweenUpdate {}
		private struct FeatherTweenLateUpdate {}
		private struct FeatherTweenFixedUpdate {}
	}
}
