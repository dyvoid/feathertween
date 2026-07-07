using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace PATween.Internal
{
	internal static class PATweenRunner
	{
		private static RootSequenceData rootUpdate;
		private static RootSequenceData rootLate;
		private static RootSequenceData rootFixed;
		private static RootSequenceData rootManual;

		private static readonly List<int> pendingKills = new List<int>(64);
		private static readonly List<int> tickSnapshotIds = new List<int>(256);
		private static readonly List<uint> tickSnapshotGens = new List<uint>(256);

		private static int mainThreadId;
		private static bool installed;

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
			InsertAfter<Update.ScriptRunBehaviourUpdate>(ref loop, typeof(PATweenUpdate), TickUpdate);
			InsertAfter<PreLateUpdate.ScriptRunBehaviourLateUpdate>(ref loop, typeof(PATweenLateUpdate), TickLate);
			InsertAfter<FixedUpdate.ScriptRunBehaviourFixedUpdate>(ref loop, typeof(PATweenFixedUpdate), TickFixed);
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
			Remove(ref loop, typeof(PATweenUpdate));
			Remove(ref loop, typeof(PATweenLateUpdate));
			Remove(ref loop, typeof(PATweenFixedUpdate));
			PlayerLoop.SetPlayerLoop(loop);
			installed = false;
		}

		public static void ManualTick(double deltaTime)
		{
			AssertMainThread();
			var root = globalTimeScale * scaleManual;
			var scaled = deltaTime * root;
			rootManual.Advance(scaled, scaled);
			TickActive(TweenStore.ActiveManual, scaled, scaled);
			LeakDetector.Drain();
		}

		internal static void TickEditorDelta(double deltaTime)
		{
			AssertMainThread();
			var root = globalTimeScale * scaleUpdate;
			var scaled = deltaTime * root;
			rootUpdate.Advance(scaled, scaled);
			TickActive(TweenStore.ActiveUpdate, scaled, scaled);
			LeakDetector.Drain();
		}

		internal static void TickUpdate()
		{
			AssertMainThread();
			var root = globalTimeScale * scaleUpdate;
			double scaled = Time.deltaTime * root;
			double unscaled = Time.unscaledDeltaTime * root;
			rootUpdate.Advance(scaled, unscaled);
			TickActive(TweenStore.ActiveUpdate, scaled, unscaled);
			LeakDetector.Drain();
		}

		internal static void TickLate()
		{
			AssertMainThread();
			var root = globalTimeScale * scaleLate;
			double scaled = Time.deltaTime * root;
			double unscaled = Time.unscaledDeltaTime * root;
			rootLate.Advance(scaled, unscaled);
			TickActive(TweenStore.ActiveLate, scaled, unscaled);
			LeakDetector.Drain();
		}

		internal static void TickFixed()
		{
			AssertMainThread();
			var root = globalTimeScale * scaleFixed;
			double scaled = Time.fixedDeltaTime * root;
			double unscaled = Time.fixedUnscaledDeltaTime * root;
			rootFixed.Advance(scaled, unscaled);
			TickActive(TweenStore.ActiveFixed, scaled, unscaled);
			LeakDetector.Drain();
		}

		private static void TickActive(List<int> active, double scaledDt, double unscaledDt)
		{
			TweenCommandQueue.BeginTick();
			try
			{
				TickActiveCore(active, scaledDt, unscaledDt);
			}
			finally
			{
				// Drains callbacks' deferred Kill/Complete/Restart/Reverse commands.
				TweenCommandQueue.EndTick();
			}
		}

		private static void TickActiveCore(List<int> active, double scaledDt, double unscaledDt)
		{
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
						data.Status = TweenStatus.Cancelled;
						data.InvokeOnKill();
						pendingKills.Add(id);
						continue;
					}
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
					pendingKills.Add(id);
				}
			}

			if (pendingKills.Count > 0)
			{
				for (var i = 0; i < pendingKills.Count; i++)
				{
					TweenStore.Free(pendingKills[i]);
				}
				pendingKills.Clear();
			}
		}

		private static void InsertAfter<TAnchor>(ref PlayerLoopSystem loop, Type newType, PlayerLoopSystem.UpdateFunction update)
		{
			var anchorType = typeof(TAnchor);
			InsertAfterRecursive(ref loop, anchorType, newType, update);
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

		private static void AssertMainThread()
		{
			if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
			{
				throw new InvalidOperationException(
					"[PATween] PATweenRunner must be ticked from the main thread.");
			}
		}

		private struct PATweenUpdate {}
		private struct PATweenLateUpdate {}
		private struct PATweenFixedUpdate {}
	}
}
