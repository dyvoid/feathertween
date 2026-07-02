using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PATween.Internal;

namespace PATween.Tests
{
	[TestFixture]
	public class PATweenRunnerPlayModeTests
	{
		[UnityTest]
		public IEnumerator UpdateRoot_AdvancesEachFrame()
		{
			yield return null;
			var before = PATweenRunner.RootUpdate.LocalTime;
			yield return null;
			var after = PATweenRunner.RootUpdate.LocalTime;
			Assert.That(after, Is.GreaterThan(before));
		}

		[UnityTest]
		public IEnumerator LateRoot_AdvancesEachFrame()
		{
			yield return null;
			var before = PATweenRunner.RootLate.LocalTime;
			yield return null;
			var after = PATweenRunner.RootLate.LocalTime;
			Assert.That(after, Is.GreaterThan(before));
		}

		[UnityTest]
		public IEnumerator FixedRoot_Advances()
		{
			yield return new WaitForFixedUpdate();
			var before = PATweenRunner.RootFixed.LocalTime;
			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();
			var after = PATweenRunner.RootFixed.LocalTime;
			Assert.That(after, Is.GreaterThan(before));
		}

		[UnityTest]
		public IEnumerator AutoKill_FiresFrameAfter_ObjectDestroy()
		{
			var go = new GameObject("__patween_destroy_target__");
			var t = global::PATween.PATween.To(() => 0f, _ => { }, 1f, 30f)
				.SetTarget(go)
				.Start();

			yield return null;
			Assert.That(t.IsAlive, Is.True);

			Object.Destroy(go);
			yield return null;
			yield return null;

			Assert.That(t.IsAlive, Is.False);
		}

		[UnityTest]
		public IEnumerator RunnerTicks_AfterScriptUpdate()
		{
			var go = new GameObject("PATweenProbe");
			var probe = go.AddComponent<UpdateOrderProbe>();

			// Two frames: a newly added component's first Update can be
			// deferred, so guarantee at least one full Update+LateUpdate pass.
			yield return null;
			yield return null;

			Assert.That(probe.SawUpdate, Is.True);
			Assert.That(probe.TickedBetweenUpdateAndLate, Is.True,
				"PATween runner must tick after ScriptRunBehaviourUpdate.");

			Object.Destroy(go);
		}

		// The before/after pair must be sampled within a single frame; comparing
		// across frames races with script execution order (no runner tick happens
		// between one frame's LateUpdate and the next frame's Update).
		private class UpdateOrderProbe : MonoBehaviour
		{
			public bool SawUpdate { get; private set; }
			public bool TickedBetweenUpdateAndLate { get; private set; }

			private double rootTimeAtUpdate;
			private bool updateRanThisFrame;

			private void Update()
			{
				rootTimeAtUpdate = PATweenRunner.RootUpdate.LocalTime;
				updateRanThisFrame = true;
				SawUpdate = true;
			}

			private void LateUpdate()
			{
				if (updateRanThisFrame
					&& PATweenRunner.RootUpdate.LocalTime > rootTimeAtUpdate)
				{
					TickedBetweenUpdateAndLate = true;
				}
				updateRanThisFrame = false;
			}
		}
	}
}
