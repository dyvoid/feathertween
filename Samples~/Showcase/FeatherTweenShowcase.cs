using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Dyvoid.FeatherTween;

namespace Dyvoid.FeatherTween.Samples.Showcase
{
	// Attach to an empty GameObject and press Play.
	//
	// A guided tour of FeatherTween. The entire sample is ONE nested master
	// sequence: every chapter is a nested sequence appended with a label, screen
	// transitions are themselves tweens on the same timeline, and the player
	// panel drives the master sequence through the public control surface
	// (Seek / Pause / Resume / Reverse / SetTimeScale). Because everything lives
	// on one timeline, state is a pure function of the playhead: scrub the seek
	// bar erratically and any frame will match what forward playback shows at
	// that time.
	//
	// Every chapter captions its expected outcome, so you can tell correct from
	// broken without reading the code. Two things deliberately do NOT live on
	// the timeline (the boundary is part of the demonstration):
	//   - infinite loops appear only as bounded excerpts (chapter 6 stops one
	//     with CompleteAtCycleEnd);
	//   - imperative features (kill-by-target, bulk ops, one-shots) live in the
	//     final playground chapter, where the timeline parks at an AddPause and
	//     hands over buttons.
	public class FeatherTweenShowcase : MonoBehaviour
	{
		private struct Chapter
		{
			public string Name;
			public string Caption;
			public float Start;
		}

		// ---- timing constants -------------------------------------------------
		// Chapter content durations (seconds). Constants so the captions'
		// "lands exactly" claims stay true by construction.
		private const float SlideDuration = 0.5f;   // per side (in + out)
		private const float TitleContent = 3f;
		private const float CreationContent = 3f;
		private const float EasesContent = 3f;
		private const float LoopsContent = 4f;
		private const float ComposeContent = 3f;
		private const float ControlContent = 4f;
		private const float ShortcutContent = 3f;

		private const float OffscreenX = 16f;       // chapter groups slide in from +X, out to -X

		private readonly List<Chapter> chapters = new List<Chapter>();
		private readonly List<GameObject> spawned = new List<GameObject>();

		// ---- cast -------------------------------------------------------------
		private Transform[] groups;                 // one root per chapter
		private GameObject[] titleBlocks;
		private GameObject creationA, creationB, creationC;
		private Renderer creationARend, creationBRend, creationCRend;
		private GameObject[] easeDots;
		private string[] easeNames;
		private EaseRef[] easeRefs;
		private GameObject loopRestart, loopYoyo, loopRewind, loopIncremental;
		private GameObject delayFirst, delayEvery, delayBarFirst, delayBarEvery;
		private GameObject hero, satellite, pingSphere;
		private Renderer heroRend;
		private GameObject[] diagramBars;
		private GameObject controlCube;
		private GameObject scMove, scLocalMove, scScale, scRotate, scLocalRotate;
		private CanvasGroup uiFadeGroup;
		private Image uiColorImage, uiFadeImage, uiFillImage;
		private GameObject playP1, playP2;

		private Sequence showcase;                  // the master timeline
		private Sequence controlInner;              // chapter 6's driven sequence (deliberately detached)
		private float totalDuration;
		private float speed = 1f;

		private void Start()
		{
			SpawnCast();
			BuildShowcase();
		}

		private void OnDestroy()
		{
			showcase.Kill();
			controlInner.Kill();
			foreach (var go in spawned)
			{
				if (go != null)
				{
					Destroy(go);
				}
			}
		}

		// ---- cast construction -------------------------------------------------

		private void SpawnCast()
		{
			groups = new Transform[8];
			for (var i = 0; i < groups.Length; i++)
			{
				var g = new GameObject($"FeatherTweenShowcase_Chapter{i + 1}");
				g.transform.SetParent(transform, worldPositionStays: true);
				g.transform.position = new Vector3(OffscreenX, 0f, 0f);
				spawned.Add(g);
				groups[i] = g.transform;
			}

			// 1. Title: seven blocks assemble into a bar, one ease each.
			titleBlocks = new GameObject[7];
			for (var i = 0; i < titleBlocks.Length; i++)
			{
				titleBlocks[i] = Spawn(groups[0], PrimitiveType.Cube,
					new Vector3(-3f + i * 1f, -7f, 0f), TitleColor(i), $"Title{i}");
			}

			// 2. Creation semantics: three identical cubes, three lanes.
			creationA = Spawn(groups[1], PrimitiveType.Cube, new Vector3(-3f, 2.2f, 0f), UnityEngine.Color.white, "CreationTo");
			creationB = Spawn(groups[1], PrimitiveType.Cube, new Vector3(-3f, 0.8f, 0f), UnityEngine.Color.white, "CreationFrom");
			creationC = Spawn(groups[1], PrimitiveType.Cube, new Vector3(-3f, -0.6f, 0f), UnityEngine.Color.white, "CreationFromTo");
			creationARend = creationA.GetComponent<Renderer>();
			creationBRend = creationB.GetComponent<Renderer>();
			creationCRend = creationC.GetComponent<Renderer>();
			SpawnLane(groups[1], -3f, 2.2f);
			SpawnLane(groups[1], 3f, 2.2f);
			SpawnLane(groups[1], -3f, 0.8f);
			SpawnLane(groups[1], 3f, 0.8f);
			SpawnLane(groups[1], -3f, -0.6f);
			SpawnLane(groups[1], 3f, -0.6f);

			// 3. Eases gallery: every EaseType plus parametric/curve/custom
			// variants, as a two-column grid of dots.
			BuildEaseTable();
			easeDots = new GameObject[easeRefs.Length];
			for (var i = 0; i < easeDots.Length; i++)
			{
				var col = i / 18;                       // 0 = left column, 1 = right
				var row = i % 18;
				var x0 = col == 0 ? -4f : 0.6f;
				var y = 3.2f - row * 0.35f;
				easeDots[i] = Spawn(groups[2], PrimitiveType.Sphere, new Vector3(x0, y, 0f),
					new UnityEngine.Color(0.4f + 0.6f * (row / 17f), 0.8f, 1f - 0.6f * (row / 17f), 1f), $"Ease_{easeNames[i]}");
				easeDots[i].transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
			}
			foreach (var x in new[] { -4f, -0.6f, 0.6f, 4f })
			{
				var line = Spawn(groups[2], PrimitiveType.Cube, new Vector3(x, 0.225f, 0.3f), UnityEngine.Color.gray, "EaseGrid");
				line.transform.localScale = new Vector3(0.03f, 6.5f, 0.03f);
			}

			// 4. Loops and delays: four loop-type lanes + two delay lanes with
			// countdown bars. Marker pad where the incremental lane must land.
			loopRestart = Spawn(groups[3], PrimitiveType.Cube, new Vector3(-3f, 3f, 0f), UnityEngine.Color.white, "LoopRestart");
			loopYoyo = Spawn(groups[3], PrimitiveType.Cube, new Vector3(-3f, 2f, 0f), UnityEngine.Color.green, "LoopYoyo");
			loopRewind = Spawn(groups[3], PrimitiveType.Cube, new Vector3(-3f, 1f, 0f), UnityEngine.Color.cyan, "LoopRewind");
			loopIncremental = Spawn(groups[3], PrimitiveType.Cube, new Vector3(-3f, 0f, 0f), UnityEngine.Color.magenta, "LoopIncremental");
			var incMarker = Spawn(groups[3], PrimitiveType.Cube, new Vector3(3f, -0.7f, 0f), UnityEngine.Color.gray, "IncrementalMarker");
			incMarker.transform.localScale = new Vector3(1f, 0.1f, 1f);
			delayFirst = Spawn(groups[3], PrimitiveType.Cube, new Vector3(-3f, -1.4f, 0f), UnityEngine.Color.yellow, "DelayFirstLoop");
			delayEvery = Spawn(groups[3], PrimitiveType.Cube, new Vector3(-3f, -2.6f, 0f), UnityEngine.Color.red, "DelayEveryLoop");
			delayBarFirst = Spawn(groups[3], PrimitiveType.Cube, new Vector3(-3f, -0.9f, 0f), UnityEngine.Color.yellow, "DelayBarFirst");
			delayBarEvery = Spawn(groups[3], PrimitiveType.Cube, new Vector3(-3f, -2.1f, 0f), UnityEngine.Color.red, "DelayBarEvery");
			delayBarFirst.transform.localScale = new Vector3(1.5f, 0.12f, 0.12f);
			delayBarEvery.transform.localScale = new Vector3(1.5f, 0.12f, 0.12f);

			// 5. Sequence composition: a hero cube, an orbiting satellite (nested
			// sequence), a callback ping sphere, and a self-drawing timing diagram.
			hero = Spawn(groups[4], PrimitiveType.Cube, new Vector3(-3f, 1.2f, 0f), UnityEngine.Color.white, "ComposeHero");
			heroRend = hero.GetComponent<Renderer>();
			satellite = Spawn(groups[4], PrimitiveType.Cube, new Vector3(0f, 2.4f, 0f), UnityEngine.Color.cyan, "ComposeSatellite");
			satellite.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
			pingSphere = Spawn(groups[4], PrimitiveType.Sphere, new Vector3(3f, 2.4f, 0f), UnityEngine.Color.yellow, "ComposePing");
			diagramBars = new GameObject[5];
			for (var i = 0; i < diagramBars.Length; i++)
			{
				diagramBars[i] = Spawn(groups[4], PrimitiveType.Cube, new Vector3(-3f, -1.2f - i * 0.3f, 0f),
					UnityEngine.Color.gray, $"DiagramBar{i}");
				diagramBars[i].transform.localScale = new Vector3(0.0001f, 0.14f, 0.14f);
			}

			// 6. Control surface: the driven cube + a marker at its far cycle end.
			controlCube = Spawn(groups[5], PrimitiveType.Cube, new Vector3(-2f, 0.5f, 0f), UnityEngine.Color.magenta, "ControlCube");
			var ctlMarker = Spawn(groups[5], PrimitiveType.Cube, new Vector3(2f, -0.2f, 0f), UnityEngine.Color.gray, "ControlMarker");
			ctlMarker.transform.localScale = new Vector3(1f, 0.1f, 1f);

			// 7. Typed shortcuts: one target per shortcut, plus a UI corner for
			// the CanvasGroup/Image shortcuts.
			scMove = Spawn(groups[6], PrimitiveType.Cube, new Vector3(-3.6f, 0.5f, 0f), UnityEngine.Color.white, "ShortcutMove");
			scLocalMove = Spawn(groups[6], PrimitiveType.Cube, new Vector3(-1.8f, 0.5f, 0f), UnityEngine.Color.cyan, "ShortcutLocalMove");
			scScale = Spawn(groups[6], PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), UnityEngine.Color.green, "ShortcutScale");
			scRotate = Spawn(groups[6], PrimitiveType.Cube, new Vector3(1.8f, 0.5f, 0f), UnityEngine.Color.yellow, "ShortcutRotate");
			scLocalRotate = Spawn(groups[6], PrimitiveType.Cube, new Vector3(3.6f, 0.5f, 0f), UnityEngine.Color.magenta, "ShortcutLocalRotate");
			SpawnShortcutUi();

			// 8. Playground targets.
			playP1 = Spawn(groups[7], PrimitiveType.Cube, new Vector3(-1.5f, 0.5f, 0f), UnityEngine.Color.green, "PlaygroundP1");
			playP2 = Spawn(groups[7], PrimitiveType.Cube, new Vector3(1.5f, 0.5f, 0f), UnityEngine.Color.cyan, "PlaygroundP2");
		}

		private static UnityEngine.Color TitleColor(int i)
		{
			var t = i / 6f;
			return new UnityEngine.Color(0.3f + 0.7f * t, 0.9f - 0.5f * t, 1f, 1f);
		}

		private void SpawnLane(Transform parent, float x, float y)
		{
			var tick = Spawn(parent, PrimitiveType.Cube, new Vector3(x, y - 0.7f, 0f), UnityEngine.Color.gray, "LaneTick");
			tick.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f);
		}

		// The UI shortcuts need real uGUI components; a small screen-space canvas
		// in the bottom-right corner hosts them.
		private void SpawnShortcutUi()
		{
			var canvasGo = new GameObject("FeatherTweenShowcase_Canvas");
			spawned.Add(canvasGo);
			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			var groupGo = new GameObject("FeatherTweenShowcase_UiGroup");
			spawned.Add(groupGo);
			groupGo.transform.SetParent(canvasGo.transform);
			uiFadeGroup = groupGo.AddComponent<CanvasGroup>();

			uiColorImage = SpawnImage(groupGo.transform, -330f, "UiColor");
			uiFadeImage = SpawnImage(groupGo.transform, -230f, "UiFade");
			uiFillImage = SpawnImage(groupGo.transform, -130f, "UiFill");
			uiFillImage.type = Image.Type.Filled;
			uiFillImage.fillMethod = Image.FillMethod.Horizontal;
		}

		private Image SpawnImage(Transform parent, float x, string label)
		{
			var go = new GameObject($"FeatherTweenShowcase_{label}");
			spawned.Add(go);
			go.transform.SetParent(parent);
			var image = go.AddComponent<Image>();
			var rt = go.GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(1f, 0f);
			rt.anchorMax = new Vector2(1f, 0f);
			rt.pivot = new Vector2(0f, 0f);
			rt.anchoredPosition = new Vector2(x, 20f);
			rt.sizeDelta = new Vector2(80f, 40f);
			return image;
		}

		private void BuildEaseTable()
		{
			var names = new List<string>();
			var refs = new List<EaseRef>();
			void Add(string n, EaseRef e) { names.Add(n); refs.Add(e); }

			Add("Linear", Easing.Linear());
			Add("InSine", Easing.InSine()); Add("OutSine", Easing.OutSine()); Add("InOutSine", Easing.InOutSine());
			Add("InQuad", Easing.InQuad()); Add("OutQuad", Easing.OutQuad()); Add("InOutQuad", Easing.InOutQuad());
			Add("InCubic", Easing.InCubic()); Add("OutCubic", Easing.OutCubic()); Add("InOutCubic", Easing.InOutCubic());
			Add("InQuart", Easing.InQuart()); Add("OutQuart", Easing.OutQuart()); Add("InOutQuart", Easing.InOutQuart());
			Add("InQuint", Easing.InQuint()); Add("OutQuint", Easing.OutQuint()); Add("InOutQuint", Easing.InOutQuint());
			Add("InExpo", Easing.InExpo()); Add("OutExpo", Easing.OutExpo()); Add("InOutExpo", Easing.InOutExpo());
			Add("InCirc", Easing.InCirc()); Add("OutCirc", Easing.OutCirc()); Add("InOutCirc", Easing.InOutCirc());
			Add("InBack", Easing.InBack()); Add("OutBack", Easing.OutBack()); Add("InOutBack", Easing.InOutBack());
			Add("OutBack(2.5)", Easing.OutBack(2.5f));
			Add("InElastic", Easing.InElastic()); Add("OutElastic", Easing.OutElastic()); Add("InOutElastic", Easing.InOutElastic());
			Add("OutElastic(2,.6)", Easing.OutElastic(2f, 0.6f));
			Add("InBounce", Easing.InBounce()); Add("OutBounce", Easing.OutBounce()); Add("InOutBounce", Easing.InOutBounce());
			Add("BounceExact(1)", Easing.BounceExact(1f));
			Add("Curve", Easing.Curve(AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)));
			Add("Custom t^2", Easing.Custom(t => t * t));

			easeNames = names.ToArray();
			easeRefs = refs.ToArray();
		}

		// ---- the showcase timeline ----------------------------------------------

		private void BuildShowcase()
		{
			chapters.Clear();
			totalDuration = 0f;

			var master = FT.Sequence()
				.SetAutoKill(false); // replayable and seekable after completion

			AddChapter(master, "1. Title", TitleContent,
				"Seven blocks assemble into a bar — each flies up with a DIFFERENT ease " +
				"(Linear, OutQuad, OutCubic, OutBack, OutElastic, OutBounce, InOutSine, left to right). " +
				"All seven land level at the same instant, whatever path they took.",
				BuildTitleChapter());

			AddChapter(master, "2. Creation semantics", CreationContent,
				"Top: To — lazy, reads its start from the scene when its window begins. " +
				"Middle: From() — snaps to the FAR tick, plays BACK to where it was (right to left). " +
				"Bottom: FromTo — both endpoints explicit, captured at build time. " +
				"Each cube flashes at the moment its start value is applied (the snap).",
				BuildCreationChapter());

			AddChapter(master, "3. Eases gallery", EasesContent,
				"Every EaseType at once, incl. parametric (OutBack 2.5, Elastic 2/0.6, BounceExact), " +
				"Curve and Custom. All dots depart and arrive together; only their path differs. " +
				"Back/Elastic dots visibly cross the grid lines (over/undershoot).",
				BuildEasesChapter());

			AddChapter(master, "4. Loops and delays", LoopsContent,
				"Four lanes, 1s cycles: Restart teleports home; Yoyo ping-pongs; Rewind snaps back; " +
				"Incremental adds +1.5 per cycle and must LAND EXACTLY ON THE MARKER (x=3) after 4 cycles. " +
				"Below: FirstLoop delays once, EveryLoop delays before EVERY cycle — the countdown bars " +
				"drain exactly while their cube waits.",
				BuildLoopsChapter());

			AddChapter(master, "5. Sequence composition", ComposeContent,
				"One chapter built with every composition op: Append (hero slides), Join (it grows in " +
				"parallel), AppendInterval, AppendCallback (the yellow ping), Insert(1.0, spin), " +
				"Insert(AtLabel) color pulse, and a NESTED 2-cycle child (satellite shuttle). " +
				"The gray bars draw the timing diagram: each grows exactly during its op's window.",
				BuildComposeChapter());

			AddChapter(master, "6. Control surface", ControlContent,
				"A player inside the player: the magenta cube runs an INFINITE yoyo that is not on this " +
				"timeline. Scripted callbacks drive it through its handle — restart, 2x speed, reverse, " +
				"then CompleteAtCycleEnd() parks it exactly on the marker. Note: silent scrubbing skips " +
				"callbacks by design; play through (or Restart) to see the commands fire.",
				BuildControlChapter());

			AddChapter(master, "7. Typed shortcuts", ShortcutContent,
				"Every shipped shortcut doing its literal thing, out and back (2-cycle yoyo): " +
				"Move, LocalMove, Scale, Rotate, LocalRotate on the cubes; Fade(CanvasGroup), " +
				"Color(Image), Fade(Image), FillAmount(Image) on the corner UI. " +
				"Everything ends exactly where it started.",
				BuildShortcutChapter());

			BuildPlaygroundChapter(master);
			BuildGroupParking(master);

			showcase = master.Start();
		}

		// Wraps a chapter's content in slide-in/slide-out transitions (tweens on
		// the same timeline) and appends it to the master with a label. The
		// content builder must not read totalDuration; duration is passed
		// explicitly and accumulated here.
		private void AddChapter(SequenceBuilder master, string name, float contentDuration, string caption, SequenceBuilder content)
		{
			master.AddLabel(name, totalDuration);
			chapters.Add(new Chapter { Name = name, Caption = caption, Start = totalDuration });

			var index = chapters.Count - 1;
			var group = groups[index];
			var ch = FT.Sequence();
			// Explicit endpoints (From) keep the transitions a pure function of
			// time under arbitrary scrubbing.
			ch.Append(FT.Move(group, new Vector3(0f, 0f, 0f), SlideDuration)
				.From(new Vector3(OffscreenX, 0f, 0f))
				.SetEase(Easing.OutCubic()));
			ch.Append(content);
			ch.Append(FT.Move(group, new Vector3(-OffscreenX, 0f, 0f), SlideDuration)
				.From(new Vector3(0f, 0f, 0f))
				.SetEase(Easing.InCubic()));

			master.Append(ch);
			totalDuration += SlideDuration + contentDuration + SlideDuration;
		}

		private SequenceBuilder BuildTitleChapter()
		{
			var ch = FT.Sequence();
			var eases = new[]
			{
				Easing.Linear(), Easing.OutQuad(), Easing.OutCubic(), Easing.OutBack(),
				Easing.OutElastic(), Easing.OutBounce(), Easing.InOutSine(),
			};
			for (var i = 0; i < titleBlocks.Length; i++)
			{
				var to = new Vector3(-3f + i, 1.5f, 0f);
				var tween = FT.LocalMove(titleBlocks[i].transform, to, TitleContent - 1f)
					.From(new Vector3(to.x, -7f, 0f))
					.SetEase(eases[i]);
				if (i == 0)
				{
					ch.Append(tween);
				}
				else
				{
					ch.Join(tween);
				}
			}
			// The assembled bar takes one synchronized breath to close the card.
			ch.Append(FT.LocalMove(titleBlocks[3].transform, new Vector3(0f, 1.9f, 0f), 0.5f)
				.From(new Vector3(0f, 1.5f, 0f))
				.SetLoops(2, LoopType.Yoyo)
				.SetEase(Easing.InOutSine()));
			return ch;
		}

		private SequenceBuilder BuildCreationChapter()
		{
			var ch = FT.Sequence();
			const float reset = 0.05f;
			const float run = CreationContent - reset;

			// Deterministic park: explicit FromTo pins all three cubes to the left
			// tick at chapter start, so the lazy forms below capture the same
			// start on every pass (replay or scrub).
			ch.Append(FT.LocalMove(creationA.transform, new Vector3(-3f, 2.2f, 0f), reset).From(new Vector3(-3f, 2.2f, 0f)));
			ch.Join(FT.LocalMove(creationB.transform, new Vector3(-3f, 0.8f, 0f), reset).From(new Vector3(-3f, 0.8f, 0f)));
			ch.Join(FT.LocalMove(creationC.transform, new Vector3(-3f, -0.6f, 0f), reset).From(new Vector3(-3f, -0.6f, 0f)));

			// Lane A — To: start read lazily from the scene at window entry.
			ch.Append(FT.LocalMove(creationA.transform, new Vector3(3f, 2.2f, 0f), run)
				.SetEase(Easing.InOutQuad()));
			// Lane B — From(): snaps to the end value, plays back to the lazily
			// read current position: it travels right-to-left.
			ch.Join(FT.LocalMove(creationB.transform, new Vector3(3f, 0.8f, 0f), run)
				.From()
				.SetEase(Easing.InOutQuad()));
			// Lane C — FromTo: both endpoints explicit at build time.
			ch.Join(FT.FromTo(v => creationC.transform.localPosition = v,
					new Vector3(-3f, -0.6f, 0f), new Vector3(3f, -0.6f, 0f), run)
				.SetEase(Easing.InOutQuad()));

			// Snap markers: a flash at each lane's window start — the visible
			// moment the start value is applied (deferred to chapter entry for all
			// three, because they are sequenced children).
			ch.Join(Flash(creationARend, UnityEngine.Color.white));
			ch.Join(Flash(creationBRend, UnityEngine.Color.white));
			ch.Join(Flash(creationCRend, UnityEngine.Color.white));
			return ch;
		}

		private TweenBuilder<UnityEngine.Color> Flash(Renderer renderer, UnityEngine.Color baseColor)
		{
			return FT.To(
					() => renderer.material.color,
					c => renderer.material.color = c,
					new UnityEngine.Color(1f, 0.4f, 0.1f, 1f), 0.2f)
				.From(baseColor)
				.SetLoops(2, LoopType.Yoyo);
		}

		private SequenceBuilder BuildEasesChapter()
		{
			var ch = FT.Sequence();
			for (var i = 0; i < easeDots.Length; i++)
			{
				var col = i / 18;
				var row = i % 18;
				var x0 = col == 0 ? -4f : 0.6f;
				var y = 3.2f - row * 0.35f;
				var tween = FT.LocalMove(easeDots[i].transform, new Vector3(x0 + 3.4f, y, 0f), EasesContent)
					.From(new Vector3(x0, y, 0f))
					.SetEase(easeRefs[i]);
				if (i == 0)
				{
					ch.Append(tween);
				}
				else
				{
					ch.Join(tween);
				}
			}
			return ch;
		}

		private SequenceBuilder BuildLoopsChapter()
		{
			var ch = FT.Sequence();
			// Loop-type lanes: 1s cycles. Restart/Yoyo/Rewind run 4 bounded cycles
			// in [0,4]; Incremental adds (end-start)=+1.5 per cycle:
			// -3 -> -1.5 -> 0 -> 1.5 -> 3, landing on the marker.
			ch.Append(FT.LocalMove(loopRestart.transform, new Vector3(0f, 3f, 0f), 1f)
				.From(new Vector3(-3f, 3f, 0f))
				.SetLoops(4, LoopType.Restart)
				.SetEase(Easing.InOutQuad()));
			ch.Join(FT.LocalMove(loopYoyo.transform, new Vector3(0f, 2f, 0f), 1f)
				.From(new Vector3(-3f, 2f, 0f))
				.SetLoops(4, LoopType.Yoyo)
				.SetEase(Easing.InOutQuad()));
			ch.Join(FT.LocalMove(loopRewind.transform, new Vector3(0f, 1f, 0f), 1f)
				.From(new Vector3(-3f, 1f, 0f))
				.SetLoops(4, LoopType.Rewind)
				.SetEase(Easing.InOutQuad()));
			ch.Join(FT.LocalMove(loopIncremental.transform, new Vector3(-1.5f, 0f, 0f), 1f)
				.From(new Vector3(-3f, 0f, 0f))
				.SetLoops(4, LoopType.Incremental)
				.SetEase(Easing.InOutQuad()));

			// Delay lanes: 2 cycles of 1s with a 1s delay. FirstLoop waits once
			// (moves during [1,3]); EveryLoop waits before every cycle
			// (moves during [1,2] and [3,4]).
			ch.Join(FT.LocalMove(delayFirst.transform, new Vector3(0f, -1.4f, 0f), 1f)
				.From(new Vector3(-3f, -1.4f, 0f))
				.SetDelay(1f, DelayType.FirstLoop)
				.SetLoops(2, LoopType.Restart)
				.SetEase(Easing.InOutQuad()));
			ch.Join(FT.LocalMove(delayEvery.transform, new Vector3(0f, -2.6f, 0f), 1f)
				.From(new Vector3(-3f, -2.6f, 0f))
				.SetDelay(1f, DelayType.EveryLoop)
				.SetLoops(2, LoopType.Restart)
				.SetEase(Easing.InOutQuad()));

			// Countdown bars drain exactly while their cube waits.
			ch.Insert(0f, CountdownBar(delayBarFirst));
			ch.Insert(0f, CountdownBar(delayBarEvery));
			ch.Insert(2f, CountdownBar(delayBarEvery));
			return ch;
		}

		private TweenBuilder<Vector3> CountdownBar(GameObject bar)
		{
			return FT.To(
					() => bar.transform.localScale,
					v => bar.transform.localScale = v,
					new Vector3(0.0001f, 0.12f, 0.12f), 1f)
				.From(new Vector3(1.5f, 0.12f, 0.12f))
				.SetEase(Easing.Linear());
		}

		private SequenceBuilder BuildComposeChapter()
		{
			var ch = FT.Sequence();

			// Append: hero slides right during [0,1].
			ch.Append(FT.LocalMove(hero.transform, new Vector3(0f, 1.2f, 0f), 1f)
				.From(new Vector3(-3f, 1.2f, 0f))
				.SetEase(Easing.OutCubic()));
			// Join: and grows in parallel over the same window.
			ch.Join(FT.Scale(hero.transform, 1.5f, 1f).From(Vector3.one).SetEase(Easing.OutCubic()));
			// AppendInterval: [1.0,1.5] is deliberate empty time.
			ch.AppendInterval(0.5f);
			// AppendCallback at 1.5: fires a visible ping (a deliberately
			// off-timeline one-shot — callbacks are edges, not spans).
			ch.AppendCallback(PingCallback);
			ch.AddLabel("spin", 1.5f);
			// Nested child sequence with its own loops: satellite shuttles twice
			// during [1.5,2.5].
			var shuttle = FT.Sequence()
				.SetLoops(2, LoopType.Yoyo);
			shuttle.Append(FT.LocalMove(satellite.transform, new Vector3(1.2f, 2.4f, 0f), 0.5f)
				.From(new Vector3(-1.2f, 2.4f, 0f))
				.SetEase(Easing.InOutSine()));
			ch.Append(shuttle);
			// Insert at a label: color pulse during [1.5,2.5].
			ch.Insert(Position.AtLabel("spin"), FT.To(
					() => heroRend.material.color,
					c => heroRend.material.color = c,
					UnityEngine.Color.cyan, 0.5f)
				.From(UnityEngine.Color.white)
				.SetLoops(2, LoopType.Yoyo));
			// Insert at an absolute time: spin during [1.0,2.5]. Explicit start
			// keeps the frame a pure function of the playhead under scrubbing.
			ch.Insert(1f, FT.LocalRotate(hero.transform, new Vector3(0f, 0f, 120f), 1.5f)
				.From(Quaternion.identity)
				.SetEase(Easing.InOutQuad()));
			// Shrink back so the chapter leaves the hero as it found it.
			ch.Insert(2.5f, FT.Scale(hero.transform, 1f, 0.5f).From(new Vector3(1.5f, 1.5f, 1.5f)).SetEase(Easing.InCubic()));
			ch.Insert(2.5f, FT.LocalRotate(hero.transform, new Vector3(0f, 0f, 0f), 0.5f)
				.From(Quaternion.Euler(0f, 0f, 120f)));

			// The self-drawing timing diagram: one bar per op, growing exactly
			// during that op's window (1 second = 1 unit of width).
			AddDiagramBar(ch, 0, 0f, 1f);      // Append move
			AddDiagramBar(ch, 1, 0f, 1f);      // Join scale
			AddDiagramBar(ch, 2, 1f, 1.5f);    // Insert(1.0) spin
			AddDiagramBar(ch, 3, 1.5f, 1f);    // nested shuttle (2 cycles)
			AddDiagramBar(ch, 4, 1.5f, 1f);    // Insert(AtLabel) pulse
			return ch;
		}

		private void PingCallback()
		{
			// One-shot started from a sequence callback. Reentrancy-safe: the
			// engine defers the structural start to the end of the tick.
			FT.Scale(pingSphere.transform, 1.8f, 0.15f)
				.SetLoops(2, LoopType.Yoyo)
				.SetTarget(pingSphere)
				.Start();
		}

		private void AddDiagramBar(SequenceBuilder ch, int row, float start, float duration)
		{
			var bar = diagramBars[row];
			var y = -1.2f - row * 0.3f;
			var width = duration;
			// Grow rightward: scale-x and center-x animate together, both with
			// explicit endpoints so scrubbing is deterministic.
			ch.Insert(start, FT.To(
					() => bar.transform.localScale,
					v => bar.transform.localScale = v,
					new Vector3(width, 0.14f, 0.14f), duration)
				.From(new Vector3(0.0001f, 0.14f, 0.14f))
				.SetEase(Easing.Linear()));
			ch.Insert(start, FT.LocalMove(bar.transform, new Vector3(-3f + width * 0.5f, y, 0f), duration)
				.From(new Vector3(-3f, y, 0f))
				.SetEase(Easing.Linear()));
		}

		private SequenceBuilder BuildControlChapter()
		{
			// The driven sequence: an infinite yoyo, deliberately NOT on the
			// master timeline (an open window would make it unendable).
			// SetAutoKill(false) so CompleteAtCycleEnd leaves a live, restartable
			// handle for the next pass.
			var innerBuilder = FT.Sequence()
				.SetAutoKill(false)
				.SetLoops(-1, LoopType.Yoyo);
			innerBuilder.Append(FT.LocalMove(controlCube.transform, new Vector3(2f, 0.5f, 0f), 1f)
				.From(new Vector3(-2f, 0.5f, 0f))
				.SetEase(Easing.InOutQuad()));
			controlInner = innerBuilder.Start();
			controlInner.Pause(); // idle until the chapter's first command

			// The chapter itself is a command script: callbacks drive the inner
			// sequence through its public handle at scripted moments.
			var ch = FT.Sequence();
			ch.AppendCallback(ControlRestart);       // 0.0: restart at 1x
			ch.AppendInterval(1.5f);
			ch.AppendCallback(ControlDouble);        // 1.5: double speed
			ch.AppendInterval(1f);
			ch.AppendCallback(ControlReverse);       // 2.5: reverse
			ch.AppendInterval(1f);
			ch.AppendCallback(ControlFinish);        // 3.5: forward again, stop at the cycle end
			ch.AppendInterval(ControlContent - 3.5f);
			return ch;
		}

		private void ControlRestart()
		{
			controlInner.SetTimeScale(1f);
			controlInner.Restart();
		}

		private void ControlDouble() => controlInner.SetTimeScale(2f);

		private void ControlReverse() => controlInner.Reverse();

		private void ControlFinish()
		{
			controlInner.Reverse();               // forward again
			controlInner.CompleteAtCycleEnd();    // bounded excerpt of an infinite loop
		}

		private SequenceBuilder BuildShortcutChapter()
		{
			var half = ShortcutContent / 2f;
			var ch = FT.Sequence();
			// Every tween is a 2-cycle yoyo, so the chapter provably leaves no
			// trace — scrub across it and the exit frame matches the entry frame.
			ch.Append(FT.Move(scMove.transform, new Vector3(-3.6f, 2f, 0f), half)
				.SetLoops(2, LoopType.Yoyo).SetEase(Easing.InOutQuad()));
			ch.Join(FT.LocalMove(scLocalMove.transform, new Vector3(-1.8f, 2f, 0f), half)
				.SetLoops(2, LoopType.Yoyo).SetEase(Easing.InOutQuad()));
			ch.Join(FT.Scale(scScale.transform, 1.8f, half)
				.SetLoops(2, LoopType.Yoyo).SetEase(Easing.InOutQuad()));
			ch.Join(FT.Rotate(scRotate.transform, new Vector3(0f, 0f, 150f), half)
				.SetLoops(2, LoopType.Yoyo).SetEase(Easing.InOutQuad()));
			ch.Join(FT.LocalRotate(scLocalRotate.transform, new Vector3(0f, 150f, 0f), half)
				.SetLoops(2, LoopType.Yoyo).SetEase(Easing.InOutQuad()));

			ch.Join(FT.Fade(uiFadeGroup, 0.25f, half)
				.SetLoops(2, LoopType.Yoyo));
			ch.Join(FT.Color(uiColorImage, new UnityEngine.Color(1f, 0.3f, 0.1f, 1f), half)
				.SetLoops(2, LoopType.Yoyo));
			ch.Join(FT.Fade(uiFadeImage, 0.1f, half)
				.SetLoops(2, LoopType.Yoyo));
			ch.Join(FT.FillAmount(uiFillImage, 0.1f, half)
				.SetLoops(2, LoopType.Yoyo));
			return ch;
		}

		// The finale is off-timeline by design: the timeline slides the playground
		// in and parks at an AddPause; the buttons in OnGUI take over.
		private void BuildPlaygroundChapter(SequenceBuilder master)
		{
			master.AddLabel("8. Playground", totalDuration);
			chapters.Add(new Chapter
			{
				Name = "8. Playground",
				Caption =
					"The timeline has parked itself at an AddPause — imperative features do not " +
					"live on a timeline. Use the playground buttons: punch one-shots, Kill vs " +
					"Kill(complete:true) on a spinning target, IsTweening readouts, KillAll and " +
					"the global time-scale slider. Replay seeks the master back to 0.",
				Start = totalDuration,
			});

			var group = groups[7];
			var slideIn = FT.Sequence();
			slideIn.Append(FT.Move(group, new Vector3(0f, 0f, 0f), SlideDuration)
				.From(new Vector3(OffscreenX, 0f, 0f))
				.SetEase(Easing.OutCubic()));
			master.Append(slideIn);
			totalDuration += SlideDuration;

			master.AddPause(totalDuration);
			// Padding after the pause keeps the park inside the timeline instead
			// of racing the sequence's own completion at the exact end.
			master.AppendInterval(0.5f);
			totalDuration += 0.5f;
		}

		// A silent Seek renders only the tweens whose window contains the target
		// time, so a chapter group is only positioned while one of its slide
		// tweens is active. These zero-motion "hold" tweens cover every gap —
		// parked off-screen before the chapter, pinned on-stage during it,
		// parked off-screen after it — so ANY seek (chapter jumps, Restart,
		// erratic scrubbing) lands every group exactly where it belongs.
		private void BuildGroupParking(SequenceBuilder master)
		{
			var off = new Vector3(OffscreenX, 0f, 0f);
			var exited = new Vector3(-OffscreenX, 0f, 0f);
			var onStage = new Vector3(0f, 0f, 0f);

			for (var i = 0; i < groups.Length; i++)
			{
				var start = chapters[i].Start;
				// The last chapter (playground) slides in and stays; the others
				// slide out half a second before the next chapter's label.
				var last = i == groups.Length - 1;
				var end = last ? totalDuration : chapters[i + 1].Start;

				if (start > 0f)
				{
					master.Insert(0f, Hold(groups[i], off, start));
				}
				var stageStart = start + SlideDuration;
				var stageEnd = last ? totalDuration : end - SlideDuration;
				master.Insert(stageStart, Hold(groups[i], onStage, stageEnd - stageStart));
				if (!last)
				{
					master.Insert(end, Hold(groups[i], exited, totalDuration - end));
				}
			}
		}

		private TweenBuilder<Vector3> Hold(Transform group, Vector3 position, float duration)
		{
			return FT.Move(group, position, duration).From(position);
		}

		// ---- player UI ----------------------------------------------------------

		// The playhead in seconds, derived from the handle — the same value the
		// seek bar writes back through Seek(). No local playback state.
		private float CurrentTime => showcase.TotalProgress * showcase.Duration;

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
			GUILayout.BeginArea(new Rect(10f, 10f, 560f, 330f), GUI.skin.box);

			var time = CurrentTime;
			var chapter = CurrentChapter(time);
			GUILayout.Label(
				$"FeatherTween Showcase — {chapter.Name}  ({time:0.0}s / {totalDuration:0.0}s, {showcase.Status})",
				GUILayout.Height(40f));
			GUILayout.Label(chapter.Caption, GUILayout.Height(110f));

			// Seek bar, bound two-way: it tracks the playhead during playback and
			// drags call Seek() on the master sequence.
			var target = GUILayout.HorizontalSlider(time, 0f, totalDuration);
			if (Mathf.Abs(target - time) > 0.01f)
			{
				showcase.Seek(target);
			}

			GUILayout.BeginHorizontal();
			if (showcase.Status == TweenStatus.Paused)
			{
				if (GUILayout.Button("Play")) showcase.Resume();
			}
			else if (showcase.Status == TweenStatus.Completed)
			{
				if (GUILayout.Button("Replay")) showcase.Restart();
			}
			else
			{
				if (GUILayout.Button("Pause")) showcase.Pause();
			}
			if (GUILayout.Button("Reverse")) showcase.Reverse();
			if (GUILayout.Button("Restart")) showcase.Restart();
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label($"speed {speed:0.00}x", GUILayout.Width(90f));
			foreach (var s in new[] { 0.25f, 0.5f, 1f, 2f, 4f })
			{
				if (GUILayout.Button($"{s:0.##}x"))
				{
					speed = s;
					showcase.SetTimeScale(s);
				}
			}
			GUILayout.EndHorizontal();

			// Chapter markers built from the labels; click to seek.
			GUILayout.BeginHorizontal();
			foreach (var ch in chapters)
			{
				// "3. Eases gallery" -> "3"
				if (GUILayout.Button(ch.Name.Substring(0, 1)))
				{
					showcase.Seek(ch.Start);
				}
			}
			GUILayout.EndHorizontal();

			if (chapter.Name == "8. Playground")
			{
				DrawPlayground();
			}

			GUILayout.EndArea();
		}

		private void DrawPlayground()
		{
			GUILayout.Space(6f);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Punch P1"))
			{
				FT.Scale(playP1.transform, 1.5f, 0.15f)
					.SetLoops(2, LoopType.Yoyo)
					.SetEase(Easing.OutQuad())
					.Start();
			}
			if (GUILayout.Button("Spin P2 (infinite)") && !FT.IsTweening(playP2.transform))
			{
				FT.LocalRotate(playP2.transform, new Vector3(0f, 0f, 120f), 0.4f)
					.SetLoops(-1, LoopType.Incremental)
					.Start();
			}
			if (GUILayout.Button("Kill P2"))
			{
				FT.Kill(playP2.transform);
			}
			if (GUILayout.Button("Kill P2 (complete)"))
			{
				FT.Kill(playP2.transform, complete: true);
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label(
				$"IsTweening — P1: {FT.IsTweening(playP1.transform)}  P2: {FT.IsTweening(playP2.transform)}",
				GUILayout.Width(240f));
			if (GUILayout.Button("KillAll (kills the showcase too!)"))
			{
				FT.KillAll();
			}
			if (GUILayout.Button("Replay showcase"))
			{
				ReplayShowcase();
			}
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label($"global scale {FT.GlobalTimeScale:0.00}x", GUILayout.Width(130f));
			FT.GlobalTimeScale = GUILayout.HorizontalSlider(FT.GlobalTimeScale, 0f, 2f);
			GUILayout.EndHorizontal();
		}

		// Replay seeks the master back to 0 — unless KillAll destroyed it, in
		// which case the scene is rebuilt from scratch.
		private void ReplayShowcase()
		{
			FT.GlobalTimeScale = 1f;
			if (showcase.IsAlive)
			{
				showcase.Seek(0f);
				showcase.Resume();
				return;
			}
			controlInner.Kill();
			foreach (var go in spawned)
			{
				if (go != null)
				{
					Destroy(go);
				}
			}
			spawned.Clear();
			speed = 1f;
			SpawnCast();
			BuildShowcase();
		}

		private GameObject Spawn(Transform parent, PrimitiveType type, Vector3 localPosition, UnityEngine.Color color, string label)
		{
			var go = GameObject.CreatePrimitive(type);
			go.name = $"FeatherTweenShowcase_{label}";
			go.transform.SetParent(parent, worldPositionStays: false);
			go.transform.localPosition = localPosition;
			go.GetComponent<Renderer>().material.color = color;
			spawned.Add(go);
			return go;
		}
	}
}
