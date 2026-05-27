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
		public IEnumerator RunnerTicks_AfterScriptUpdate()
		{
			var go = new GameObject("PATweenProbe");
			var probe = go.AddComponent<UpdateOrderProbe>();

			yield return null;

			Assert.That(probe.UpdateRanThisFrame, Is.True);
			Assert.That(probe.RootTimeAfterUpdate, Is.GreaterThan(probe.RootTimeBeforeUpdate),
				"PATween runner must tick after ScriptRunBehaviourUpdate.");

			Object.Destroy(go);
		}

		private class UpdateOrderProbe : MonoBehaviour
		{
			public double RootTimeBeforeUpdate { get; private set; }
			public double RootTimeAfterUpdate { get; private set; }
			public bool UpdateRanThisFrame { get; private set; }

			private void Update()
			{
				RootTimeBeforeUpdate = PATweenRunner.RootUpdate.LocalTime;
				UpdateRanThisFrame = true;
			}

			private void LateUpdate()
			{
				RootTimeAfterUpdate = PATweenRunner.RootUpdate.LocalTime;
			}
		}
	}
}
