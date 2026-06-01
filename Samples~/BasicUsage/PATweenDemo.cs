using System.Collections.Generic;
using UnityEngine;
using PATween;
using static PATween.PATween;

namespace PATween.Samples
{
	// Attach to an empty GameObject and press Play.
	// Spawns a row of primitives, each showcasing a different PATween feature.
	public class PATweenDemo : MonoBehaviour
	{
		[Header("Layout")]
		[Tooltip("Horizontal spacing between demo objects.")]
		[SerializeField]
		private float spacing = 2.5f;

		[Header("Timing")]
		[Tooltip("Base duration (seconds) used by the demos.")]
		[SerializeField]
		private float duration = 1.5f;

		[Header("Feature Toggles")]
		[SerializeField]
		private bool moveDemo = true;
		[SerializeField]
		private bool scaleDemo = true;
		[SerializeField]
		private bool rotateDemo = true;
		[SerializeField]
		private bool colorDemo = true;
		[SerializeField]
		private bool bounceDemo = true;

		private readonly List<Tween> tweens = new List<Tween>();
		private readonly List<GameObject> spawned = new List<GameObject>();

		private void Start()
		{
			var slot = 0;

			if (moveDemo)
			{
				SetupMove(NextPosition(ref slot));
			}
			if (scaleDemo)
			{
				SetupScale(NextPosition(ref slot));
			}
			if (rotateDemo)
			{
				SetupRotate(NextPosition(ref slot));
			}
			if (colorDemo)
			{
				SetupColor(NextPosition(ref slot));
			}
			if (bounceDemo)
			{
				SetupBounce(NextPosition(ref slot));
			}
		}

		private void OnDestroy()
		{
			for (var i = 0; i < tweens.Count; i++)
			{
				tweens[i].Kill();
			}
			tweens.Clear();

			for (var i = 0; i < spawned.Count; i++)
			{
				if (spawned[i] != null)
				{
					Destroy(spawned[i]);
				}
			}
			spawned.Clear();
		}

		private Vector3 NextPosition(ref int slot)
		{
			var pos = transform.position + Vector3.right * (slot * spacing);
			slot++;
			return pos;
		}

		private void SetupMove(Vector3 origin)
		{
			var go = Spawn(PrimitiveType.Cube, origin, Color.cyan, "Move");
			var t = go.transform;
			var top = origin + Vector3.up * 2f;

			var tween = To(() => t.position, p => t.position = p, top, duration)
				.SetEase(Easing.InOutSine())
				.SetLoops(-1, LoopType.Yoyo)
				.SetTarget(go)
				.Start();

			tweens.Add(tween);
		}

		private void SetupScale(Vector3 origin)
		{
			var go = Spawn(PrimitiveType.Sphere, origin, Color.green, "Scale");
			var t = go.transform;
			var big = Vector3.one * 1.75f;

			var tween = To(() => t.localScale, s => t.localScale = s, big, duration)
				.SetEase(Easing.OutBack())
				.SetLoops(-1, LoopType.Yoyo)
				.SetTarget(go)
				.Start();

			tweens.Add(tween);
		}

		private void SetupRotate(Vector3 origin)
		{
			var go = Spawn(PrimitiveType.Capsule, origin, Color.yellow, "Rotate");
			var t = go.transform;
			var target = Quaternion.Euler(0f, 180f, 0f);

			var tween = To(() => t.rotation, r => t.rotation = r, target, duration)
				.SetEase(Easing.InOutCubic())
				.SetLoops(-1, LoopType.Incremental)
				.SetTarget(go)
				.Start();

			tweens.Add(tween);
		}

		private void SetupColor(Vector3 origin)
		{
			var go = Spawn(PrimitiveType.Cube, origin, Color.red, "Color");
			var renderer = go.GetComponent<Renderer>();

			var tween = To(() => renderer.material.color, c => renderer.material.color = c, Color.blue, duration)
				.SetEase(Easing.Linear())
				.SetLoops(-1, LoopType.Yoyo)
				.SetTarget(go)
				.Start();

			tweens.Add(tween);
		}

		private void SetupBounce(Vector3 origin)
		{
			var go = Spawn(PrimitiveType.Cube, origin + Vector3.up * 2f, Color.magenta, "Bounce");
			var t = go.transform;
			var ground = origin;

			var tween = To(() => t.position, p => t.position = p, ground, duration)
				.SetEase(Easing.OutBounce())
				.SetLoops(-1, LoopType.Restart)
				.SetDelay(0.25f, DelayType.EveryLoop)
				.SetTarget(go)
				.Start();

			tweens.Add(tween);
		}

		private GameObject Spawn(PrimitiveType type, Vector3 position, Color color, string label)
		{
			var go = GameObject.CreatePrimitive(type);
			go.name = $"PATweenDemo_{label}";
			go.transform.SetParent(transform, worldPositionStays: true);
			go.transform.position = position;
			go.GetComponent<Renderer>().material.color = color;
			spawned.Add(go);
			return go;
		}
	}
}
