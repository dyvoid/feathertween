using NUnit.Framework;
using Dyvoid.FeatherTween;
using Dyvoid.FeatherTween.Internal;
using UnityEngine;
using UnityEngine.UI;

namespace Dyvoid.FeatherTween.Tests
{
	// Phase 1.11: typed shortcuts on the lambda core. Each shortcut must move
	// the right property of the right component, auto-set the target, and
	// compose with From() like any generic builder.
	[TestFixture]
	public class ShortcutTests
	{
		private GameObject go;

		[SetUp]
		public void SetUp()
		{
			TweenStore.Reset();
			FeatherTweenRunner.Reset();
			Interpolators.Reset();
			go = new GameObject("shortcut-test");
		}

		[TearDown]
		public void TearDown()
		{
			if (go != null)
			{
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Move_TweensWorldPosition()
		{
			var t = go.transform;
			t.position = Vector3.zero;
			FT.Move(t, new Vector3(2f, 4f, 6f), 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(t.position.x, Is.EqualTo(1f).Within(1e-3f));
			Assert.That(t.position.y, Is.EqualTo(2f).Within(1e-3f));
			Assert.That(t.position.z, Is.EqualTo(3f).Within(1e-3f));

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(t.position.x, Is.EqualTo(2f).Within(1e-3f));
		}

		[Test]
		public void LocalMove_TweensLocalPosition_NotWorld()
		{
			var t = go.transform;
			t.position = new Vector3(9f, 9f, 9f);
			t.localPosition = Vector3.zero;
			FT.LocalMove(t, Vector3.right * 2f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(t.localPosition.x, Is.EqualTo(1f).Within(1e-3f));
		}

		[Test]
		public void Scale_TweensLocalScale_UniformOverload()
		{
			var t = go.transform;
			t.localScale = Vector3.one;
			FT.Scale(t, 3f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(t.localScale.x, Is.EqualTo(2f).Within(1e-3f));
			Assert.That(t.localScale.y, Is.EqualTo(2f).Within(1e-3f));
			Assert.That(t.localScale.z, Is.EqualTo(2f).Within(1e-3f));
		}

		[Test]
		public void Rotate_EulerOverload_ReachesTargetRotation()
		{
			var t = go.transform;
			t.rotation = Quaternion.identity;
			FT.Rotate(t, new Vector3(0f, 90f, 0f), 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(1.0);
			var expected = Quaternion.Euler(0f, 90f, 0f);
			Assert.That(Mathf.Abs(Quaternion.Dot(t.rotation, expected)), Is.EqualTo(1f).Within(1e-3f),
				"end rotation matches Euler(0,90,0)");
		}

		[Test]
		public void LocalRotate_TweensLocalRotation()
		{
			var t = go.transform;
			t.localRotation = Quaternion.identity;
			FT.LocalRotate(t, new Vector3(0f, 0f, 180f), 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(1.0);
			var expected = Quaternion.Euler(0f, 0f, 180f);
			Assert.That(Mathf.Abs(Quaternion.Dot(t.localRotation, expected)), Is.EqualTo(1f).Within(1e-3f));
		}

		[Test]
		public void Fade_TweensCanvasGroupAlpha()
		{
			var cg = go.AddComponent<CanvasGroup>();
			cg.alpha = 1f;
			FT.Fade(cg, 0f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.75);
			Assert.That(cg.alpha, Is.EqualTo(0.25f).Within(1e-3f));
		}

		[Test]
		public void Color_TweensImageColor()
		{
			var img = go.AddComponent<Image>();
			img.color = new Color(0f, 0f, 0f, 1f);
			FT.Color(img, new Color(1f, 0.5f, 0f, 1f), 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(img.color.r, Is.EqualTo(0.5f).Within(1e-3f));
			Assert.That(img.color.g, Is.EqualTo(0.25f).Within(1e-3f));
			Assert.That(img.color.a, Is.EqualTo(1f).Within(1e-3f));
		}

		[Test]
		public void ImageFade_TweensOnlyAlpha()
		{
			var img = go.AddComponent<Image>();
			img.color = new Color(0.2f, 0.4f, 0.6f, 1f);
			FT.Fade(img, 0f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(img.color.a, Is.EqualTo(0.5f).Within(1e-3f));
			Assert.That(img.color.r, Is.EqualTo(0.2f).Within(1e-3f), "rgb untouched");
		}

		[Test]
		public void FillAmount_TweensImageFill()
		{
			var img = go.AddComponent<Image>();
			img.fillAmount = 0f;
			FT.FillAmount(img, 1f, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(img.fillAmount, Is.EqualTo(0.25f).Within(1e-3f));
		}

		[Test]
		public void Shortcut_From_SnapsRightProperty()
		{
			var t = go.transform;
			t.position = new Vector3(5f, 0f, 0f);
			FT.Move(t, new Vector3(1f, 0f, 0f), 1f)
				.SetUpdate(UpdatePhase.Manual)
				.From()
				.Start();

			Assert.That(t.position.x, Is.EqualTo(1f).Within(1e-3f), "From snaps position to the given value");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(t.position.x, Is.EqualTo(3f).Within(1e-3f), "moves back toward the captured 5");
		}

		[Test]
		public void Shortcut_FromValue_PlaysExplicitFromToEnd()
		{
			var t = go.transform;
			t.position = new Vector3(99f, 0f, 0f); // current value must be ignored
			FT.Move(t, new Vector3(10f, 0f, 0f), 1f)
				.SetUpdate(UpdatePhase.Manual)
				.From(new Vector3(0f, 0f, 0f))
				.Start();

			Assert.That(t.position.x, Is.EqualTo(0f).Within(1e-3f), "From(value) snaps to the explicit start");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(t.position.x, Is.EqualTo(5f).Within(1e-3f), "midpoint between explicit 0 and end 10");

			FeatherTweenRunner.ManualTick(0.5);
			Assert.That(t.position.x, Is.EqualTo(10f).Within(1e-3f));
		}

		[Test]
		public void Shortcut_AutoSetsTarget()
		{
			var t = go.transform;
			var tween = FT.Move(t, Vector3.one, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start();

			var data = TweenStore.Get(tween.Id, tween.Generation);
			Assert.That(data, Is.Not.Null);
			Assert.That(data.Target, Is.SameAs(t), "target auto-set for Kill(target)/auto-kill");
		}

		[Test]
		public void Shortcut_DestroyedTarget_AutoKills()
		{
			var t = go.transform;
			var killed = false;
			FT.Move(t, Vector3.one, 1f)
				.SetUpdate(UpdatePhase.Manual)
				.Start()
				.OnKill(() => killed = true);

			FeatherTweenRunner.ManualTick(0.25);
			Object.DestroyImmediate(go);
			go = null;
			FeatherTweenRunner.ManualTick(0.25);
			Assert.That(killed, Is.True, "destroyed Unity target auto-kills the tween");
		}

		[Test]
		public void Shortcut_NullTarget_Throws()
		{
			Assert.Throws<System.ArgumentNullException>(
				() => FT.Move(null, Vector3.one, 1f));
			Assert.Throws<System.ArgumentNullException>(
				() => FT.Fade((CanvasGroup)null, 0f, 1f));
		}
	}
}
