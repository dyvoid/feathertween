using System.Collections.Generic;
using UnityEngine;
using Dyvoid.FeatherTween;

namespace Dyvoid.FeatherTween.Samples.MiniShowcase
{
	// Attach to an empty GameObject and press Play.
	//
	// A small preview of the phase 1.17 showcase ("the movie",
	// docs/design/showcase-sample.md): the whole sample is ONE master sequence
	// whose chapters are nested sequences, and the on-screen panel is a real
	// player — seek bar, play/pause, reverse, and speed all drive the master
	// sequence through the public control surface.
	//
	// Every chapter captions what is supposed to happen, so the scene is
	// visually testable without reading the code:
	//
	//   1. Ease race    - three cubes, three eases, one arrival line.
	//   2. Loops        - Yoyo returns home; Incremental lands on its marker.
	//   3. From drop    - deferred From snap pops the cube up at chapter entry.
	//   4. Finale       - synchronized pulse; everything ends where it started.
	//
	// Because everything lives on one timeline, state is a pure function of the
	// playhead: scrub the seek bar back and forth and every frame must match
	// what forward playback shows at that time.
	//
	// Kept deliberately small; the full chaptered showcase with markers and a
	// playground chapter lands in phase 1.17.
	public class FeatherTweenMiniShowcase : MonoBehaviour
	{
		private struct Chapter
		{
			public string Name;
			public string Caption;
			public float Start;
		}

		// Chapter timing (seconds). Constants so the captions' "lands exactly"
		// claims stay true by construction.
		private const float RaceDuration = 3f;
		private const float LoopsDuration = 3f;
		private const float DropDuration = 2f;
		private const float FinaleDuration = 2f;

		private readonly List<Chapter> chapters = new List<Chapter>();
		private readonly List<GameObject> spawned = new List<GameObject>();

		private GameObject[] racers;
		private GameObject yoyoCube;
		private GameObject incrementalCube;
		private GameObject dropCube;
		private Renderer dropRenderer;

		private Sequence movie;
		private float totalDuration;
		private float speed = 1f;

		private void Start()
		{
			SpawnCast();
			BuildMovie();
		}

		private void OnDestroy()
		{
			movie.Kill();
			foreach (var go in spawned)
			{
				if (go != null)
				{
					Destroy(go);
				}
			}
		}

		private void SpawnCast()
		{
			racers = new GameObject[3];
			racers[0] = Spawn(PrimitiveType.Cube, new Vector3(-4f, 4.5f, 0f), UnityEngine.Color.white, "RacerLinear");
			racers[1] = Spawn(PrimitiveType.Cube, new Vector3(-4f, 3.3f, 0f), UnityEngine.Color.cyan, "RacerInOutQuad");
			racers[2] = Spawn(PrimitiveType.Cube, new Vector3(-4f, 2.1f, 0f), UnityEngine.Color.yellow, "RacerOutBounce");

			// Arrival line for chapter 1: all three racers finish on it together.
			var line = Spawn(PrimitiveType.Cube, new Vector3(4f, 3.3f, 0.6f), UnityEngine.Color.gray, "ArrivalLine");
			line.transform.localScale = new Vector3(0.1f, 4f, 0.1f);

			yoyoCube = Spawn(PrimitiveType.Cube, new Vector3(-4f, 0.5f, 0f), UnityEngine.Color.green, "YoyoCube");
			incrementalCube = Spawn(PrimitiveType.Cube, new Vector3(-4f, -0.9f, 0f), UnityEngine.Color.magenta, "IncrementalCube");

			// Landing marker for the incremental cube: 3 cycles x +2 units from
			// x = -4 puts it exactly at x = 2.
			var pad = Spawn(PrimitiveType.Cube, new Vector3(2f, -1.6f, 0f), UnityEngine.Color.gray, "IncrementalMarker");
			pad.transform.localScale = new Vector3(1f, 0.1f, 1f);

			dropCube = Spawn(PrimitiveType.Cube, new Vector3(4f, -0.9f, 0f), UnityEngine.Color.red, "DropCube");
			dropRenderer = dropCube.GetComponent<Renderer>();
			var dropPad = Spawn(PrimitiveType.Cube, new Vector3(4f, -1.6f, 0f), UnityEngine.Color.gray, "DropMarker");
			dropPad.transform.localScale = new Vector3(1f, 0.1f, 1f);
		}

		// One master sequence; each chapter is a nested sequence appended with a
		// label, and the chapter list drives the caption panel and jump buttons.
		private void BuildMovie()
		{
			var master = FT.Sequence()
				.SetAutoKill(false); // replayable and seekable after completion

			AddChapter(master, "1. Ease race", RaceDuration,
				"Three cubes race with different eases (Linear, InOutQuad, OutBounce). " +
				"They take different paths but ALL cross the line together at this chapter's end.",
				BuildEaseRace());

			AddChapter(master, "2. Loops", LoopsDuration,
				"Green runs 4 Yoyo cycles and must END BACK AT ITS START. " +
				"Magenta runs 3 Incremental cycles of +2 and must LAND EXACTLY ON THE MARKER.",
				BuildLoops());

			AddChapter(master, "3. From drop", DropDuration,
				"The red cube POPS UP the instant this chapter starts (deferred From snap, " +
				"ADR 0007), then bounce-drops back onto its marker. Note: scrubbing back " +
				"BEFORE this chapter parks it at the raised start — the pre-movie rest pose " +
				"was never on the timeline, so a backward crossing renders the tween's start value.",
				BuildFromDrop());

			AddChapter(master, "4. Finale", FinaleDuration,
				"Everything pulses once in sync (Join) while the red cube flashes white. " +
				"At the end the scene matches the frame before this chapter.",
				BuildFinale());

			movie = master.Start();
		}

		// The chapter builder must not touch totalDuration itself: as an argument
		// it is evaluated before this method records Start, so the duration is
		// passed explicitly and accumulated here.
		private void AddChapter(SequenceBuilder master, string name, float duration, string caption, SequenceBuilder chapter)
		{
			master.AddLabel(name, totalDuration);
			chapters.Add(new Chapter { Name = name, Caption = caption, Start = totalDuration });
			master.Append(chapter);
			totalDuration += duration;
		}

		private SequenceBuilder BuildEaseRace()
		{
			var ch = FT.Sequence();
			ch.Append(FT.Move(racers[0].transform, new Vector3(4f, 4.5f, 0f), RaceDuration)
				.SetEase(Easing.Linear()));
			ch.Join(FT.Move(racers[1].transform, new Vector3(4f, 3.3f, 0f), RaceDuration)
				.SetEase(Easing.InOutQuad()));
			ch.Join(FT.Move(racers[2].transform, new Vector3(4f, 2.1f, 0f), RaceDuration)
				.SetEase(Easing.OutBounce()));
			return ch;
		}

		private SequenceBuilder BuildLoops()
		{
			var ch = FT.Sequence();
			// 4 yoyo cycles of 0.75s fill the chapter; an even cycle count ends
			// exactly at the start position.
			ch.Append(FT.Move(yoyoCube.transform, new Vector3(-1f, 0.5f, 0f), LoopsDuration / 4f)
				.SetLoops(4, LoopType.Yoyo)
				.SetEase(Easing.InOutQuad()));
			// Incremental re-adds the (end - start) delta each cycle:
			// -4 -> -2 -> 0 -> 2, landing on the marker.
			ch.Join(FT.Move(incrementalCube.transform, new Vector3(-2f, -0.9f, 0f), LoopsDuration / 3f)
				.SetLoops(3, LoopType.Incremental)
				.SetEase(Easing.InOutQuad()));
			return ch;
		}

		private SequenceBuilder BuildFromDrop()
		{
			var ch = FT.Sequence();
			// From(value): both endpoints explicit, but the snap to the raised
			// position is deferred until the parent playhead enters this chapter
			// window — the visible "pop" IS the deferred snap moment.
			ch.Append(FT.Move(dropCube.transform, new Vector3(4f, -0.9f, 0f), DropDuration)
				.From(new Vector3(4f, 2.5f, 0f))
				.SetEase(Easing.OutBounce()));
			return ch;
		}

		private SequenceBuilder BuildFinale()
		{
			var ch = FT.Sequence();
			// A 2-cycle Yoyo returns every value to where it started, so the
			// finale leaves no trace — checkable by scrubbing across it.
			var pulse = FinaleDuration / 2f;
			ch.Append(FT.Scale(dropCube.transform, 1.6f, pulse).SetLoops(2, LoopType.Yoyo));
			foreach (var racer in racers)
			{
				ch.Join(FT.Scale(racer.transform, 1.6f, pulse).SetLoops(2, LoopType.Yoyo));
			}
			ch.Join(FT.Scale(yoyoCube.transform, 1.6f, pulse).SetLoops(2, LoopType.Yoyo));
			ch.Join(FT.Scale(incrementalCube.transform, 1.6f, pulse).SetLoops(2, LoopType.Yoyo));
			ch.Join(FT.To(
					() => dropRenderer.material.color,
					c => dropRenderer.material.color = c,
					UnityEngine.Color.white, pulse)
				.SetLoops(2, LoopType.Yoyo));
			return ch;
		}

		// The playhead in seconds, derived from the handle — the same value the
		// seek bar writes back through Seek(). No local playback state.
		private float CurrentTime => movie.TotalProgress * movie.Duration;

		private Chapter CurrentChapter(float time)
		{
			var current = chapters[0];
			for (var i = 1; i < chapters.Count; i++)
			{
				if (chapters[i].Start <= time)
				{
					current = chapters[i];
				}
			}
			return current;
		}

		private void OnGUI()
		{
			GUILayout.BeginArea(new Rect(10f, 10f, 420f, 230f), GUI.skin.box);

			var time = CurrentTime;
			var chapter = CurrentChapter(time);
			GUILayout.Label(
				$"FeatherTween MiniShowcase — {chapter.Name}  ({time:0.0}s / {totalDuration:0.0}s, {movie.Status})",
				GUILayout.Height(22f));
			// Fixed height so multi-line captions don't shove the controls around.
			GUILayout.Label(chapter.Caption, GUILayout.Height(58f));

			// Seek bar, bound two-way: it tracks the playhead during playback and
			// drags call Seek() on the master sequence.
			var target = GUILayout.HorizontalSlider(time, 0f, totalDuration);
			if (Mathf.Abs(target - time) > 0.01f)
			{
				movie.Seek(target);
			}

			GUILayout.BeginHorizontal();
			if (movie.Status == TweenStatus.Paused)
			{
				if (GUILayout.Button("Play")) movie.Resume();
			}
			else if (movie.Status == TweenStatus.Completed)
			{
				if (GUILayout.Button("Replay")) movie.Restart();
			}
			else
			{
				if (GUILayout.Button("Pause")) movie.Pause();
			}
			if (GUILayout.Button("Reverse")) movie.Reverse();
			if (GUILayout.Button("Restart")) movie.Restart();
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label($"speed {speed:0.00}x", GUILayout.Width(90f));
			foreach (var s in new[] { 0.25f, 1f, 2f, 4f })
			{
				if (GUILayout.Button($"{s:0.##}x"))
				{
					speed = s;
					movie.SetTimeScale(s);
				}
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			foreach (var ch in chapters)
			{
				// "1. Ease race" -> "1"
				if (GUILayout.Button(ch.Name.Substring(0, 1)))
				{
					movie.Seek(ch.Start);
				}
			}
			GUILayout.EndHorizontal();

			GUILayout.EndArea();
		}

		private GameObject Spawn(PrimitiveType type, Vector3 position, UnityEngine.Color color, string label)
		{
			var go = GameObject.CreatePrimitive(type);
			go.name = $"FeatherTweenMiniShowcase_{label}";
			go.transform.SetParent(transform, worldPositionStays: true);
			go.transform.position = position;
			go.GetComponent<Renderer>().material.color = color;
			spawned.Add(go);
			return go;
		}
	}
}
