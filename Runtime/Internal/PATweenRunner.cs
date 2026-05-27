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

		private static int mainThreadId;
		private static bool installed;

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

		public static void Reset()
		{
			mainThreadId = Thread.CurrentThread.ManagedThreadId;
			rootUpdate = new RootSequenceData(UpdatePhase.Update);
			rootLate = new RootSequenceData(UpdatePhase.Late);
			rootFixed = new RootSequenceData(UpdatePhase.Fixed);
			rootManual = new RootSequenceData(UpdatePhase.Manual);
			pendingKills.Clear();
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
			rootManual.Advance(deltaTime, deltaTime);
			TickActive(TweenStore.ActiveManual, deltaTime, deltaTime);
			LeakDetector.Drain();
		}

		internal static void TickEditorDelta(double deltaTime)
		{
			AssertMainThread();
			rootUpdate.Advance(deltaTime, deltaTime);
			TickActive(TweenStore.ActiveUpdate, deltaTime, deltaTime);
			LeakDetector.Drain();
		}

		internal static void TickUpdate()
		{
			AssertMainThread();
			double scaled = Time.deltaTime;
			double unscaled = Time.unscaledDeltaTime;
			rootUpdate.Advance(scaled, unscaled);
			TickActive(TweenStore.ActiveUpdate, scaled, unscaled);
			LeakDetector.Drain();
		}

		internal static void TickLate()
		{
			AssertMainThread();
			double scaled = Time.deltaTime;
			double unscaled = Time.unscaledDeltaTime;
			rootLate.Advance(scaled, unscaled);
			TickActive(TweenStore.ActiveLate, scaled, unscaled);
		}

		internal static void TickFixed()
		{
			AssertMainThread();
			double scaled = Time.fixedDeltaTime;
			double unscaled = Time.fixedUnscaledDeltaTime;
			rootFixed.Advance(scaled, unscaled);
			TickActive(TweenStore.ActiveFixed, scaled, unscaled);
		}

		private static void TickActive(List<int> active, double scaledDt, double unscaledDt)
		{
			for (var i = 0; i < active.Count; i++)
			{
				var id = active[i];
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
