using UnityEngine;
using PATween;
using static PATween.PATween;

namespace PATween.Samples.SequenceDemo
{
	// Attach to an empty GameObject and press Play.
	//
	// A small choreographed show that exercises every phase 1.8 sequence
	// feature, in stage order:
	//
	//   1. Append        - the runner cube moves right, then up (sequential).
	//   2. SetDefaults   - the moves take their ease from the sequence
	//                      defaults; no SetEase on the builders.
	//   3. Join          - the buddy sphere scales up in parallel with the
	//                      runner's second move.
	//   4. AppendCallback- the runner flashes yellow when the moves finish.
	//   5. AddPause      - the show freezes mid-way and auto-resumes after
	//                      pauseHold seconds (watch the console).
	//   6. AddLabel +
	//      Insert(label) - the "finale" label is defined AFTER it is used;
	//                      resolution happens at Start().
	//   7. Deferred From - the drop-in capsule snaps to its raised position
	//                      only when the playhead reaches the finale window,
	//                      then slides down into the line-up. Before that
	//                      moment it is visibly untouched.
	//   8. Restart       - OnComplete schedules a Restart, so the whole show
	//                      loops, which also proves the From snap re-arms.
	public class PATweenSequenceDemo : MonoBehaviour
	{
		[Header("Timing")]
		[Tooltip("Duration (seconds) each sequence step takes.")]
		[SerializeField]
		private float stepDuration = 1f;

		[Tooltip("How long the AddPause freeze lasts before auto-resume.")]
		[SerializeField]
		private float pauseHold = 1.5f;

		[Tooltip("Idle time after the show completes before it restarts.")]
		[SerializeField]
		private float restartDelay = 1f;

		private GameObject runner;
		private GameObject buddy;
		private GameObject dropIn;
		private Renderer runnerRenderer;
		private Sequence show;
		private string phaseLabel = "starting";

		private void Start()
		{
			runner = Spawn(PrimitiveType.Cube, Vector3.zero, Color.cyan, "Runner");
			buddy = Spawn(PrimitiveType.Sphere, new Vector3(0f, 0f, 2f), Color.green, "Buddy");
			dropIn = Spawn(PrimitiveType.Capsule, new Vector3(4f, 0f, 4f), Color.magenta, "DropIn");
			runnerRenderer = runner.GetComponent<Renderer>();

			BuildShow();
		}

		private void OnDestroy()
		{
			show.Kill();
			DestroySpawned(runner);
			DestroySpawned(buddy);
			DestroySpawned(dropIn);
		}

		private void BuildShow()
		{
			var runnerT = runner.transform;
			var buddyT = buddy.transform;
			var dropInT = dropIn.transform;

			var sb = Sequence()
				.SetDefaults(ease: Easing.InOutSine())
				.SetAutoKill(false)
				.OnComplete(OnShowComplete);

			// 1. Append: two sequential moves. The ease comes from SetDefaults.
			sb.AppendCallback(() => phaseLabel = "append: move right");
			sb.Append(To(() => runnerT.position, p => runnerT.position = p, new Vector3(4f, 0f, 0f), stepDuration));

			sb.AppendCallback(() => phaseLabel = "append + join: up, buddy scales");
			sb.Append(To(() => runnerT.position, p => runnerT.position = p, new Vector3(4f, 2f, 0f), stepDuration));

			// 3. Join: runs in parallel with the move above.
			sb.Join(To(() => buddyT.localScale, s => buddyT.localScale = s, Vector3.one * 1.8f, stepDuration));

			// 4. AppendCallback: zero-duration event between children.
			sb.AppendCallback(() =>
			{
				phaseLabel = "callback: flash";
				runnerRenderer.material.color = Color.yellow;
			});

			// 5. AddPause: freezes here; onPause schedules the resume.
			sb.AppendCallback(() => phaseLabel = $"paused (auto-resume in {pauseHold:0.0}s)");
			sb.AddPause(Position.End, () => Invoke(nameof(ResumeShow), pauseHold));

			// Settle back down after the pause.
			sb.AppendCallback(() => phaseLabel = "resume: settle down");
			sb.Append(To(() => runnerT.position, p => runnerT.position = p, new Vector3(4f, 0f, 0f), stepDuration));

			// 6. Insert at a label that is only defined afterwards - resolution
			// happens at Start(). 7. The From child does not snap (the capsule
			// does not teleport up) until the playhead reaches this window.
			sb.Insert(
				Position.AtLabel("finale"),
				From(() => dropInT.position, p => dropInT.position = p, new Vector3(4f, 3f, 4f), stepDuration)
					.SetEase(Easing.OutBounce()));
			sb.AddLabel("finale", Position.End);

			sb.AppendCallback(() => phaseLabel = "finale: capsule drops in");

			show = sb.Start();
		}

		private void ResumeShow()
		{
			show.Resume();
		}

		private void OnShowComplete()
		{
			phaseLabel = $"complete - restarting in {restartDelay:0.0}s";
			Invoke(nameof(RestartShow), restartDelay);
		}

		private void RestartShow()
		{
			// Reset the stage props the sequence does not tween back itself.
			buddy.transform.localScale = Vector3.one;
			dropIn.transform.position = new Vector3(4f, 0f, 4f);
			runnerRenderer.material.color = Color.cyan;
			runner.transform.position = Vector3.zero;

			show.Restart();
		}

		private void OnGUI()
		{
			GUI.Label(new Rect(10f, 10f, 600f, 22f), $"PATween SequenceDemo - {phaseLabel}");
			GUI.Label(new Rect(10f, 32f, 600f, 22f),
				$"duration {show.Duration:0.0}s | status {show.Status}");
		}

		private GameObject Spawn(PrimitiveType type, Vector3 position, Color color, string label)
		{
			var go = GameObject.CreatePrimitive(type);
			go.name = $"PATweenSequenceDemo_{label}";
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
