using UnityEngine;
using Dyvoid.FeatherTween;

namespace Dyvoid.FeatherTween.Samples.ComposedDemo
{
	// Attach to an empty GameObject and press Play.
	//
	// The M1 acceptance demo (docs/planning/phases.md phase 1.14): one scene
	// composing every core feature area so a single visual pass covers them.
	//
	//   Orbiters  - three cubes on infinite Incremental rotation loops with a
	//               Yoyo scale pulse (typed shortcuts, infinite loops).
	//   Wave      - a rank of pillars stretched by generic To tweens using
	//               DelayType.EveryLoop staggering and a parametric elastic
	//               ease. Killable as a group via their shared SetTarget tag.
	//   Hero show - a master sequence: Append/Join moves, a nested
	//               sub-sequence, a label-based Insert with a deferred From
	//               drop, callbacks, and OnComplete auto-restart.
	//
	// The OnGUI panel drives the control surface at runtime: pause/resume all,
	// reverse and seek on the hero sequence, global time scale, and a
	// target-filtered kill of the wave.
	public class FeatherTweenComposedDemo : MonoBehaviour
	{
		[Header("Timing")]
		[Tooltip("Base duration (seconds) for the hero sequence steps.")]
		[SerializeField]
		private float stepDuration = 1f;

		[Tooltip("Idle time after the hero show completes before it restarts.")]
		[SerializeField]
		private float restartDelay = 1f;

		[Header("Wave")]
		[Tooltip("Number of pillars in the wave.")]
		[SerializeField]
		private int pillarCount = 8;

		private const string WaveTag = "composed-demo-wave";

		private GameObject hero;
		private GameObject buddy;
		private GameObject dropIn;
		private GameObject[] orbiters;
		private GameObject[] pillars;
		private Renderer heroRenderer;

		private Sequence show;
		private string phaseLabel = "starting";
		private float showProgress;
		private bool paused;
		private float globalScale = 1f;
		private bool waveAlive = true;

		private void Start()
		{
			SpawnOrbiters();
			SpawnWave();
			SpawnHeroCast();
			BuildHeroShow();
		}

		private void OnDestroy()
		{
			show.Kill();
			FT.Kill(WaveTag);
			FT.GlobalTimeScale = 1f;
			foreach (var go in orbiters) DestroySpawned(go);
			foreach (var go in pillars) DestroySpawned(go);
			DestroySpawned(hero);
			DestroySpawned(buddy);
			DestroySpawned(dropIn);
		}

		// Infinite loops: Incremental rotation keeps adding the same delta each
		// cycle; the Yoyo scale pulse plays forward/backward forever.
		private void SpawnOrbiters()
		{
			orbiters = new GameObject[3];
			for (var i = 0; i < orbiters.Length; i++)
			{
				var go = Spawn(PrimitiveType.Cube, new Vector3(-5f, 1.5f * i, -3f), UnityEngine.Color.cyan, $"Orbiter{i}");
				orbiters[i] = go;

				FT.LocalRotate(go.transform, new Vector3(0f, 120f, 0f), stepDuration)
					.SetLoops(-1, LoopType.Incremental)
					.SetEase(Easing.InOutSine())
					.Start();

				FT.Scale(go.transform, 1.4f, stepDuration * 0.5f)
					.SetLoops(-1, LoopType.Yoyo)
					.Start();
			}
		}

		// Generic To tweens on a lambda pair; EveryLoop delays produce the
		// staggered wave. All share one SetTarget tag so the filtered bulk
		// kill below can take them all out with one call.
		private void SpawnWave()
		{
			pillars = new GameObject[pillarCount];
			for (var i = 0; i < pillarCount; i++)
			{
				var go = Spawn(PrimitiveType.Cylinder, new Vector3(i * 0.8f - 2f, 0f, 3f), UnityEngine.Color.green, $"Pillar{i}");
				go.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
				pillars[i] = go;

				var t = go.transform;
				FT.To(
						() => t.localScale.y,
						y => t.localScale = new Vector3(0.3f, y, 0.3f),
						2f, stepDuration * 0.6f)
					.SetLoops(-1, LoopType.Yoyo)
					.SetDelay(i * 0.1f, DelayType.EveryLoop)
					.SetEase(Easing.OutElastic(amplitude: 1.2f, period: 0.4f))
					.SetTarget(WaveTag)
					.Start();
			}
		}

		private void SpawnHeroCast()
		{
			hero = Spawn(PrimitiveType.Capsule, Vector3.zero, UnityEngine.Color.white, "Hero");
			buddy = Spawn(PrimitiveType.Sphere, new Vector3(0f, 0f, 1.5f), UnityEngine.Color.yellow, "Buddy");
			dropIn = Spawn(PrimitiveType.Cube, new Vector3(4f, 0f, 1.5f), UnityEngine.Color.magenta, "DropIn");
			heroRenderer = hero.GetComponent<Renderer>();
		}

		// The master sequence: sequential and parallel children, a nested
		// sub-sequence, a label defined after use, and a deferred From snap.
		private void BuildHeroShow()
		{
			var heroT = hero.transform;
			var buddyT = buddy.transform;
			var dropInT = dropIn.transform;

			var sb = FT.Sequence()
				.SetDefaults(ease: Easing.InOutQuad())
				.SetAutoKill(false)
				.OnStart(() => phaseLabel = "hero: moving out")
				.OnUpdate(t => showProgress = t)
				.OnComplete(OnShowComplete);

			// Append + Join: hero moves right while the buddy circles under it.
			sb.Append(FT.Move(heroT, new Vector3(4f, 0f, 0f), stepDuration));
			sb.Join(FT.Move(buddyT, new Vector3(4f, 0f, 1.5f), stepDuration));

			// Nested sub-sequence: a hop composed of up + spin, then down.
			var hop = FT.Sequence()
				.SetDefaults(ease: Easing.OutQuad());
			hop.AppendCallback(() => phaseLabel = "hero: hop (nested sequence)");
			hop.Append(FT.Move(heroT, new Vector3(4f, 2f, 0f), stepDuration * 0.5f));
			hop.Join(FT.LocalRotate(heroT, new Vector3(0f, 180f, 0f), stepDuration * 0.5f));
			hop.Append(FT.Move(heroT, new Vector3(4f, 0f, 0f), stepDuration * 0.5f)
				.SetEase(Easing.OutBounce()));
			sb.Append(hop);

			// Callback + material flash via a generic To on the material color.
			sb.AppendCallback(() => phaseLabel = "hero: flash");
			sb.Append(FT.To(
				() => heroRenderer.material.color,
				c => heroRenderer.material.color = c,
				UnityEngine.Color.red, stepDuration * 0.5f)
				.SetLoops(2, LoopType.Yoyo));

			// Label-based Insert with a deferred From: the drop-in cube snaps
			// to its raised spot only when the playhead reaches the finale.
			sb.Insert(
				Position.AtLabel("finale"),
				FT.From(
						() => dropInT.position, p => dropInT.position = p,
						new Vector3(4f, 3f, 1.5f), stepDuration)
					.SetEase(Easing.OutBounce()));
			sb.AddLabel("finale", Position.End);
			sb.AppendCallback(() => phaseLabel = "finale: cube drops in");

			show = sb.Start();
		}

		private void OnShowComplete()
		{
			phaseLabel = $"complete - restarting in {restartDelay:0.0}s";
			Invoke(nameof(RestartShow), restartDelay);
		}

		private void RestartShow()
		{
			heroRenderer.material.color = UnityEngine.Color.white;
			hero.transform.position = Vector3.zero;
			hero.transform.rotation = Quaternion.identity;
			buddy.transform.position = new Vector3(0f, 0f, 1.5f);
			dropIn.transform.position = new Vector3(4f, 0f, 1.5f);

			show.Restart();
		}

		// Runtime control surface: bulk ops, handle ops, global time scale,
		// and a target-filtered kill.
		private void OnGUI()
		{
			GUILayout.BeginArea(new Rect(10f, 10f, 320f, 400f), GUI.skin.box);
			// Fixed-height header: the phase label wraps to two lines at times,
			// and without a reserved height that shoves the buttons around
			// mid-click. Reserve two lines up front so the controls never move.
			GUILayout.Label($"FeatherTween ComposedDemo - {phaseLabel}", GUILayout.Height(36f));
			GUILayout.Label($"hero status {show.Status} | progress {showProgress:P0}", GUILayout.Height(20f));

			if (GUILayout.Button(paused ? "Resume all" : "Pause all"))
			{
				if (paused) FT.ResumeAll();
				else FT.PauseAll();
				paused = !paused;
			}

			if (GUILayout.Button("Reverse hero show"))
			{
				show.Reverse();
			}

			if (GUILayout.Button("Seek hero show to 25%"))
			{
				show.Seek(show.Duration * 0.25f);
			}

			GUILayout.BeginHorizontal();
			GUILayout.Label($"global time scale {globalScale:0.0}x");
			var newScale = GUILayout.HorizontalSlider(globalScale, 0.1f, 3f);
			GUILayout.EndHorizontal();
			if (!Mathf.Approximately(newScale, globalScale))
			{
				globalScale = newScale;
				FT.GlobalTimeScale = globalScale;
			}

			GUI.enabled = waveAlive;
			if (GUILayout.Button("Kill wave (filtered by target tag)"))
			{
				FT.Kill(WaveTag);
				waveAlive = false;
			}
			GUI.enabled = true;

			GUILayout.EndArea();
		}

		private GameObject Spawn(PrimitiveType type, Vector3 position, UnityEngine.Color color, string label)
		{
			var go = GameObject.CreatePrimitive(type);
			go.name = $"FeatherTweenComposedDemo_{label}";
			go.transform.SetParent(transform, worldPositionStays: true);
			go.transform.position = position;
			go.GetComponent<Renderer>().material.color = color;
			return go;
		}

		private void DestroySpawned(GameObject go)
		{
			if (go != null)
			{
				Destroy(go);
			}
		}
	}
}
