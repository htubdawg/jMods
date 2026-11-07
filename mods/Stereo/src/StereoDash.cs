using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfGripStereo
{
	/// <summary>
	/// The stereo in the cockpit, put on the cockpit's root (CarInterior) by <see cref="Fitter"/>. It builds two things there:
	///
	/// - the **head unit**, standing just left of the steering wheel (sized from the wheel's own sprite, so it follows the wheel), slanted
	///   in the dash's perspective (<see cref="Face"/>: its lines run to the cockpit camera's vanishing point, as the dash's do), with its
	///   display as a glowing sprite of its own, a click target on each key, and the volume knob grabbed and turned by the hand;
	/// - the **CD wallet on the sun visor**, at the top left of the view (placed through the cockpit camera, at the wheel's depth), with a
	///   crescent of each disc showing past its sleeve.
	///
	/// The discs are carried by hand (the owner's wish): the hand grabs one from its sleeve, and let go over the stereo it slides into
	/// the slot; over the visor it goes back in its sleeve; anywhere else it falls, and once out of sight it is back in its sleeve. A
	/// right click anywhere on the stereo (or the eject key) pushes the disc half out of the slot, where the hand can take it.
	///
	/// The cockpit is flat sprites drawn by its own camera; these are the same, at the cockpit's own pixel size (the middle value over
	/// its sprites, as the MiniMap's GPS measures it), in a copy of the wheel's lit material, so they are lit as the cockpit is. The
	/// display is unlit, so it glows. Click and grab targets are the game's own (InteriorSquareCollider with Clickable or Grabbable), so
	/// the cockpit hand points at them like any control; they sit at the lowest hover priority, so they never take the hand from a real
	/// control. Changing the Stereo settings in the config file rebuilds it, so its place can be tuned with the game running.
	/// </summary>
	internal sealed class StereoDash : MonoBehaviour
	{
		/// <summary>World distance the display stands in front of the casing, the loose discs in front of that, and the targets in front of what they are on (as the GPS).</summary>
		private const float Lift = 0.02f;
		private const float DiscLift = 0.05f;
		private const float TouchLift = 0.1f;
		/// <summary>Cockpit units between the head unit's right edge and the wheel's rim.</summary>
		private const float WheelGap = 0.15f;
		private const float FallbackTexel = 0.05f;
		/// <summary>How long a key shows pushed in.</summary>
		private const float PressTime = 0.15f;
		/// <summary>Seconds per letter the song's name scrolls along the display.</summary>
		private const float ScrollStep = 0.3f;
		/// <summary>Cockpit pixels per second a disc slides in or out of the slot.</summary>
		private const float SlideSpeed = 110f;
		/// <summary>A falling disc out of sight for this long, or falling this long at all, is back in its sleeve.</summary>
		private const float FallTime = 4f;

		/// <summary>The disc sticking out of the slot (ejected, not yet taken), or -1. Kept between races as the deck's state is.</summary>
		private static int sticking = -1;

		private Deck deck;
		private Transform interior;
		private Camera cam;
		private float texel = FallbackTexel;
		private Material lit;
		private Material unlit;
		private int sortingLayer;
		private int sortingOrder;
		private float toward = -1f;
		private int layer;
		private bool failed;
		private string built;
		private int builtDiscs = -1;

		private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
		private GameObject unit;
		private GameObject visor;
		/// <summary>The head unit's face in the dash's perspective: where everything on it is drawn and placed.</summary>
		private Face face = new Face(new Vector2(201f, 137f), 1f);
		/// <summary>The visor's bottom left and size, in the head unit's space.</summary>
		private Rect visorBox;
		private Texture2D unitTexture;
		private Texture2D visorTexture;
		private Texture2D lcdTexture;
		private string lcdShown;
		private Art.Part? pressed;
		private float pressedAt;
		private string unitShown;
		private string visorShown;
		private readonly List<GameObject> sleeves = new List<GameObject>();
		private readonly List<Loose> loose = new List<Loose>();

		/// <summary>A disc out of its sleeve and what it is drawn with.</summary>
		private sealed class Loose
		{
			public DiscGrab Grab;
			public GameObject Touch;
			public InteriorSquareCollider Target;
			public Texture2D Texture;
			public string Shown;
			/// <summary>In the slot: its bottom row now (unit rows, before the slant), where it is going, and whether it then plays.</summary>
			public float Row;
			public int RowTo;
			public bool Inserting;
			public float FellAt;
			public readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
		}

		private void Start()
		{
			try
			{
				deck = GetComponent<Deck>();
				interior = FindInterior();
				if (interior == null)
				{
					throw new Exception("not under the cockpit (no CarInterior above it)");
				}
				// On the cockpit's root, in its units.
				transform.SetParent(interior, false);
				transform.localPosition = Vector3.zero;
				transform.localRotation = Quaternion.identity;
				transform.localScale = Vector3.one;
				layer = interior.gameObject.layer;
				gameObject.layer = layer;
				texel = MeasureDash();
				Build();
			}
			catch (Exception e)
			{
				failed = true;
				Mod.Log.LogWarning("Stereo: could not put itself in the cockpit: " + e);
			}
		}

		private Transform FindInterior()
		{
			for (Transform t = transform; t != null; t = t.parent)
			{
				if (t.name == "CarInterior" || t.CompareTag("carInterior"))
				{
					return t;
				}
			}
			NSteeringWheel wheel = FindFirstObjectByType<NSteeringWheel>();
			return wheel != null ? wheel.transform.parent : null;
		}

		/// <summary>The settings the stereo was last built from.</summary>
		private static string Settings() => string.Join("|", Mod.UnitX.Value, Mod.UnitY.Value, Mod.Lean.Value, Mod.VisorX.Value, Mod.VisorY.Value, Mod.ShowVisor.Value, Mod.LitParts.Value);

		/// <summary>
		/// The cockpit's pixel size in its own units: the middle value over every sprite showing in it (one sprite is not enough: some are
		/// drawn much finer than the rest).
		/// </summary>
		private float MeasureDash()
		{
			List<float> sizes = new List<float>();
			float own = Mathf.Max(1e-6f, Mathf.Abs(interior.lossyScale.x));
			foreach (SpriteRenderer r in interior.GetComponentsInChildren<SpriteRenderer>(false))
			{
				if (r.sprite == null || r.sprite.pixelsPerUnit <= 0f || r.transform.IsChildOf(transform))
				{
					continue;
				}
				float t = Mathf.Abs(r.transform.lossyScale.x) / r.sprite.pixelsPerUnit / own;
				if (t > 0f)
				{
					sizes.Add(t);
				}
			}
			if (sizes.Count < 5)
			{
				Mod.Log.LogWarning("Stereo: only " + sizes.Count + " sprites in the cockpit to measure its pixels by; using " + FallbackTexel);
				return FallbackTexel;
			}
			sizes.Sort();
			float middle = sizes[sizes.Count / 2];
			Mod.Info("Stereo: cockpit pixel " + middle.ToString("0.0000") + " units, the middle of " + sizes.Count + " sprites");
			return middle < 0.005f || middle > 0.5f ? FallbackTexel : middle;
		}

		/// <summary>The steering wheel's biggest sprite: what the head unit is placed by, and whose material and sorting it borrows.</summary>
		private SpriteRenderer Wheel()
		{
			NSteeringWheel wheel = interior.GetComponentInChildren<NSteeringWheel>(true);
			if (wheel == null)
			{
				return null;
			}
			SpriteRenderer best = null;
			float area = 0f;
			foreach (SpriteRenderer r in wheel.GetComponentsInChildren<SpriteRenderer>(true))
			{
				if (r.sprite == null)
				{
					continue;
				}
				Vector3 s = r.transform.lossyScale;
				float a = r.sprite.bounds.size.x * r.sprite.bounds.size.y * Mathf.Abs(s.x * s.y);
				if (a > area)
				{
					area = a;
					best = r;
				}
			}
			return best;
		}

		private void Build()
		{
			Clear();
			built = Settings();
			builtDiscs = Discs.Version;
			SpriteRenderer wheel = Wheel();
			cam = CockpitCamera();
			// Where the wheel is, in the cockpit's units: its middle, its radius (from the sprite itself, so the wheel's turn does not
			// matter) and its depth, the plane both parts stand in.
			Vector3 centre;
			float radius;
			if (wheel != null)
			{
				centre = interior.InverseTransformPoint(wheel.transform.TransformPoint(wheel.sprite.bounds.center));
				Vector3 ext = wheel.sprite.bounds.extents;
				radius = Mathf.Max(ext.x, ext.y) * Mathf.Abs(wheel.transform.lossyScale.x) / Mathf.Max(1e-6f, Mathf.Abs(interior.lossyScale.x));
				sortingLayer = wheel.sortingLayerID;
				sortingOrder = wheel.sortingOrder;
			}
			else
			{
				// The game's cockpit in 0.418: the wheel at (-1.8, -0.36, 1.14), about 2.2 units round.
				Mod.Log.LogWarning("Stereo: no steering wheel in the cockpit; placing the stereo where the game's wheel would be");
				centre = new Vector3(-1.8f, -0.36f, 1.14f);
				radius = 2.2f;
			}
			Materials(wheel);
			toward = Toward(cam);

			// The head unit: its right edge a little clear of the rim, its middle a little above the wheel's. Square on, its sides leaning
			// toward the cockpit camera's vanishing point (the point straight ahead of the camera, in the wheel's plane; see Face).
			// Its object is at the face canvas's bottom left, and everything on it is placed in the canvas's pixels.
			int h = Art.UnitHeight;
			face = new Face(Vector2.zero, 0f);
			Vector3 corner = Vector3.zero;
			for (int pass = 0; pass < 2; pass++)
			{
				// Twice: the face's width (from the right side's lean) sets where its left end is, which sets the lean.
				corner = new Vector3(centre.x - radius - WheelGap - face.W * texel + Mod.UnitX.Value, centre.y + 0.5f - h * 0.5f * texel + Mod.UnitY.Value, centre.z);
				face = new Face(Vanish(corner), Mod.Lean.Value);
			}
			Vector3 unitAt = corner + new Vector3(0f, face.Low * texel, 0f);
			unit = Part("StereoUnit", transform, unitAt);
			int w = face.W;
			unitTexture = MakeTexture(w, face.Rows, "StereoUnit");
			Sprite(unit, "Casing", unitTexture, Vector3.zero, lit, 0);

			// The display, its glass meeting the frame's window in whole cockpit pixels.
			face.DisplaySize(out int lw, out int lh);
			face.DisplayCorner(out int lx, out int ly);
			lcdTexture = MakeTexture(lw, lh, "StereoLcd");
			lcdShown = null;
			Sprite(unit, "Display", lcdTexture, new Vector3(lx * texel, ly * texel, toward * Lift), unlit, 1);

			// Behind the keys, the whole unit: a right click anywhere on it ejects.
			Touch(unit, "Unit", new Vector3(w * 0.5f, face.Rows * 0.5f, 0f) * texel, w * texel, face.Rows * texel, StereoButton.SortingLayer - 1, null, Eject);
			foreach (Art.Part p in Art.Parts)
			{
				Rect r = face.Target(p);
				Art.Part part = p;
				if (p == Art.Part.Knob)
				{
					Knob(r);
					continue;
				}
				Touch(unit, p.ToString(), new Vector3(r.center.x, r.center.y, 0f) * texel, r.width * texel, r.height * texel, StereoButton.SortingLayer, () => Press(part), Eject);
			}
			unitShown = null;
			DrawUnit();

			if (Mod.ShowVisor.Value)
			{
				BuildVisor(centre);
			}
			if (sticking >= 0 && sticking < Discs.All.Count && (deck == null || deck.DiscIndex < 0))
			{
				Loose l = MakeLoose(sticking, Vector2.zero);
				InSlot(l, face.DiscOutRow, false);
			}
			else
			{
				sticking = -1;
			}
			Mod.Info("Stereo: head unit at " + unitAt.ToString("0.00") + " (" + (w * texel).ToString("0.00") + " units wide, " + face.Rows + " px tall, vanishing point " + Vanish(corner).ToString("0") + " px from its corner, sides leaning " + Mod.Lean.Value.ToString("0.0") + " px), wheel at " + centre.ToString("0.00") + " radius " + radius.ToString("0.00") + ", sorting " + sortingLayer + "/" + sortingOrder + ", hand sorting " + HandSorting() + ", lit '" + (lit != null ? lit.shader.name : "none") + "', display '" + (unlit != null ? unlit.shader.name : "none") + "', camera '" + (cam != null ? cam.name : "none") + "' toward " + toward);
		}

		/// <summary>
		/// Where the cockpit camera's middle ray meets the wheel's plane, in cockpit pixels from <paramref name="corner"/>: the vanishing
		/// point the dash's forward lines run to. Without a camera, the one measured in the owner's 0.2.1 screenshot.
		/// </summary>
		private Vector2 Vanish(Vector3 corner)
		{
			if (cam != null)
			{
				Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
				Vector3 o = interior.InverseTransformPoint(ray.origin);
				Vector3 dir = interior.InverseTransformDirection(ray.direction);
				if (Mathf.Abs(dir.z) > 1e-4f)
				{
					float k = (corner.z - o.z) / dir.z;
					if (k > 0f)
					{
						Vector3 p = o + dir * k;
						return new Vector2(p.x - corner.x, p.y - corner.y) / texel;
					}
				}
			}
			return new Vector2(201f, 137f);
		}

		/// <summary>The cockpit hand's sprites' sorting, for the log: a loose disc should draw under the hand.</summary>
		private string HandSorting()
		{
			Grabber hand = FindFirstObjectByType<Grabber>();
			if (hand == null)
			{
				return "?";
			}
			int lo = int.MaxValue, hi = int.MinValue;
			foreach (SpriteRenderer r in hand.GetComponentsInChildren<SpriteRenderer>(true))
			{
				lo = Mathf.Min(lo, r.sortingOrder);
				hi = Mathf.Max(hi, r.sortingOrder);
			}
			return lo > hi ? "?" : lo + ".." + hi;
		}

		/// <summary>The volume knob: grabbed and turned round its middle (see <see cref="KnobGrab"/>); each step clicks.</summary>
		private void Knob(Rect r)
		{
			GameObject go = new GameObject("Knob");
			go.layer = layer;
			go.transform.SetParent(unit.transform, false);
			go.transform.localPosition = new Vector3(r.center.x * texel, r.center.y * texel, toward * TouchLift);
			InteriorSquareCollider target = go.AddComponent<InteriorSquareCollider>();
			target.width = r.width * texel;
			target.height = r.height * texel;
			target.hitBoxExpand = 0f;
			target.localNormalPlane = Vector3.forward;
			target.sortingLayer = StereoButton.SortingLayer;
			KnobGrab knob = go.AddComponent<KnobGrab>();
			knob.attachedCollider = target;
			knob.keepPhysicalCursorClose = true;
			knob.keepPhysicalCursorRange = Mathf.Max(r.width, r.height) * texel;
			knob.Read = () => deck != null ? deck.VolumeTurn : 0.7f;
			knob.Turned = t =>
			{
				if (deck != null && deck.SetVolume(Mathf.RoundToInt(t * Mod.VolumeSteps)))
				{
					Sounds.Tick(gameObject);
				}
			};
			knob.RightClicked = Eject;
		}

		private void BuildVisor(Vector3 wheelCentre)
		{
			int n = Discs.All.Count;
			int vw = Art.VisorWidth(n), vh = Art.VisorHeight;
			// The visor's middle where the camera sees the asked-for point of the screen, in the wheel's plane.
			Vector3 at = new Vector3(wheelCentre.x - 2.6f, wheelCentre.y + 8.9f, wheelCentre.z);
			if (cam != null)
			{
				Ray ray = cam.ViewportPointToRay(new Vector3(Mod.VisorX.Value, Mod.VisorY.Value, 0f));
				Vector3 o = interior.InverseTransformPoint(ray.origin);
				Vector3 dir = interior.InverseTransformDirection(ray.direction);
				if (Mathf.Abs(dir.z) > 1e-4f)
				{
					float k = (wheelCentre.z - o.z) / dir.z;
					if (k > 0f)
					{
						at = o + dir * k;
					}
				}
			}
			Vector3 corner = at - new Vector3(vw * 0.5f, vh * 0.5f, 0f) * texel;
			visor = Part("StereoVisor", transform, corner);
			Vector3 inUnit = corner - unit.transform.localPosition;
			visorBox = new Rect(inUnit.x, inUnit.y, vw * texel, vh * texel);
			visorTexture = MakeTexture(vw, vh, "StereoVisor");
			Sprite(visor, "Wallet", visorTexture, Vector3.zero, lit, 0);
			sleeves.Clear();
			for (int i = 0; i < n; i++)
			{
				sleeves.Add(Sleeve(i));
			}
			visorShown = null;
			DrawVisor();
			Mod.Info("Stereo: CD wallet with " + n + " disc(s) at " + at.ToString("0.00") + " (" + (vw * texel).ToString("0.00") + " units wide)");
		}

		/// <summary>Disc <paramref name="i"/>'s sleeve: grabbing it takes the disc out into the hand.</summary>
		private GameObject Sleeve(int i)
		{
			RectInt box = Art.PocketPlace(i);
			GameObject go = new GameObject("Sleeve" + (i + 1));
			go.layer = layer;
			go.transform.SetParent(visor.transform, false);
			go.transform.localPosition = new Vector3(box.center.x * texel, box.center.y * texel, toward * TouchLift);
			InteriorSquareCollider target = go.AddComponent<InteriorSquareCollider>();
			target.width = box.width * texel;
			target.height = box.height * texel;
			target.hitBoxExpand = 0f;
			target.localNormalPlane = Vector3.forward;
			target.sortingLayer = StereoButton.SortingLayer;
			SleeveGrab sleeve = go.AddComponent<SleeveGrab>();
			sleeve.attachedCollider = target;
			int index = i;
			sleeve.Take = () => TakeFromSleeve(index);
			return go;
		}

		/// <summary>The cockpit's own camera (the hand's), or the one named InteriorCamera.</summary>
		private Camera CockpitCamera()
		{
			Grabber hand = FindFirstObjectByType<Grabber>();
			if (hand != null && hand.viewCamera != null)
			{
				return hand.viewCamera;
			}
			foreach (Camera c in interior.GetComponentsInChildren<Camera>(true))
			{
				return c;
			}
			return null;
		}

		/// <summary>Which way along the cockpit's z its camera is: toward it is in front. The game's cockpit has it at -z.</summary>
		private float Toward(Camera c)
		{
			if (c == null)
			{
				return -1f;
			}
			return interior.InverseTransformPoint(c.transform.position).z > 0f ? 1f : -1f;
		}

		/// <summary>
		/// The casing's material: a copy of the wheel's lit one with our texture set (the cockpit's lit shader reads the material's own
		/// texture, not the sprite's, so each cockpit sprite has its own copy). The display's: an unlit sprite material from the cockpit,
		/// or the game's unlit sprite shader.
		/// </summary>
		private void Materials(SpriteRenderer wheel)
		{
			Material basis = wheel != null ? wheel.sharedMaterial : null;
			lit = Mod.LitParts.Value ? basis : null;
			unlit = null;
			foreach (SpriteRenderer r in interior.GetComponentsInChildren<SpriteRenderer>(true))
			{
				Material m = r.sharedMaterial;
				if (m != null && m.shader != null && m.shader.name.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					unlit = m;
					break;
				}
			}
			if (unlit == null)
			{
				Shader s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
				if (s != null)
				{
					unlit = Own(new Material(s) { name = "StereoUnlit" });
				}
			}
			if (lit == null)
			{
				lit = unlit;
			}
		}

		private T Own<T>(T thing) where T : UnityEngine.Object
		{
			thing.hideFlags = HideFlags.HideAndDontSave;
			owned.Add(thing);
			return thing;
		}

		private static Texture2D NewTexture(int w, int h, string name) => new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };

		private Texture2D MakeTexture(int w, int h, string name) => Own(NewTexture(w, h, name));

		private GameObject Part(string name, Transform parent, Vector3 local)
		{
			GameObject go = new GameObject(name);
			go.layer = layer;
			go.transform.SetParent(parent, false);
			go.transform.localPosition = local;
			owned.Add(go);
			return go;
		}

		/// <summary>
		/// A sprite of the whole texture with its bottom left on <paramref name="local"/>; a lit material gets its own copy holding the
		/// texture. What it makes besides the object goes in <paramref name="into"/> (the stereo's own list by default).
		/// </summary>
		private SpriteRenderer Sprite(GameObject on, string name, Texture2D tex, Vector3 local, Material material, int above, List<UnityEngine.Object> into = null)
		{
			into ??= owned;
			GameObject go = new GameObject(name);
			go.layer = layer;
			go.transform.SetParent(on.transform, false);
			go.transform.localPosition = local;
			SpriteRenderer r = go.AddComponent<SpriteRenderer>();
			Sprite sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, 1f / texel * (tex == lcdTexture ? Art.LcdDetail : 1), 0, SpriteMeshType.FullRect);
			sprite.hideFlags = HideFlags.HideAndDontSave;
			into.Add(sprite);
			r.sprite = sprite;
			if (material != null)
			{
				if (material != unlit)
				{
					Material copy = new Material(material) { name = name + "Lit", hideFlags = HideFlags.HideAndDontSave };
					into.Add(copy);
					foreach (string p in new[] { "_BaseMap", "_MainTex" })
					{
						if (copy.HasProperty(p))
						{
							copy.SetTexture(p, tex);
							copy.SetTextureScale(p, Vector2.one);
							copy.SetTextureOffset(p, Vector2.zero);
						}
					}
					foreach (string p in new[] { "_BaseColor", "_Color" })
					{
						if (copy.HasProperty(p))
						{
							copy.SetColor(p, Color.white);
						}
					}
					material = copy;
				}
				r.sharedMaterial = material;
			}
			r.sortingLayerID = sortingLayer;
			r.sortingOrder = sortingOrder + above;
			return r;
		}

		/// <summary>A click target of the given size centred on <paramref name="local"/>, in front of it.</summary>
		private void Touch(GameObject on, string name, Vector3 local, float width, float height, int priority, Action left, Action right)
		{
			GameObject go = new GameObject("Touch" + name);
			go.layer = layer;
			go.transform.SetParent(on.transform, false);
			go.transform.localPosition = local + new Vector3(0f, 0f, toward * TouchLift);
			InteriorSquareCollider target = go.AddComponent<InteriorSquareCollider>();
			target.width = width;
			target.height = height;
			target.hitBoxExpand = 0f;
			target.localNormalPlane = Vector3.forward;
			target.sortingLayer = priority;
			StereoButton button = go.AddComponent<StereoButton>();
			button.attachedCollider = target;
			button.Left = left;
			button.Right = right;
		}

		// ---------------------------------------------------------------------------------------------------------------------------
		// The hand on the keys.

		/// <summary>A key pressed (left click): power, previous, play / pause, next, eject.</summary>
		private void Press(Art.Part part)
		{
			if (deck == null)
			{
				return;
			}
			pressed = part;
			pressedAt = Time.unscaledTime;
			Sounds.Click(gameObject);
			switch (part)
			{
				case Art.Part.Power: deck.TogglePower(); break;
				case Art.Part.Prev: deck.Skip(-1); break;
				case Art.Part.Next: deck.Skip(1); break;
				case Art.Part.Play: deck.TogglePause(); break;
				case Art.Part.Eject: Eject(); break;
			}
		}

		/// <summary>The disc in the stereo comes half out of the slot, for the hand to take.</summary>
		private void Eject()
		{
			if (deck == null || unit == null)
			{
				return;
			}
			pressed = Art.Part.Eject;
			pressedAt = Time.unscaledTime;
			int i = deck.DiscIndex;
			if (i < 0)
			{
				if (sticking < 0)
				{
					deck.Eject();
				}
				return;
			}
			deck.Eject();
			Sounds.Disc(gameObject);
			Loose l = MakeLoose(i, Vector2.zero);
			InSlot(l, face.DiscInRow, false);
			l.RowTo = face.DiscOutRow;
			sticking = i;
		}

		// ---------------------------------------------------------------------------------------------------------------------------
		// The loose discs.

		/// <summary>A disc out of its sleeve, its middle at <paramref name="centre"/> in the head unit's space.</summary>
		private Loose MakeLoose(int index, Vector2 centre)
		{
			Remove(Find(index));
			Loose l = new Loose();
			GameObject go = new GameObject("Disc" + (index + 1));
			go.layer = layer;
			go.transform.SetParent(unit.transform, false);
			go.transform.localPosition = new Vector3(centre.x, centre.y, toward * DiscLift);
			l.Owned.Add(go);
			l.Texture = NewTexture(Art.DiscSize, Art.DiscSize, "StereoDisc");
			l.Owned.Add(l.Texture);
			float half = Art.DiscRadius * texel;
			Sprite(go, "Sprite", l.Texture, new Vector3(-half, -half, 0f), lit, 3, l.Owned);
			l.Touch = new GameObject("Touch");
			l.Touch.layer = layer;
			l.Touch.transform.SetParent(go.transform, false);
			l.Touch.transform.localPosition = new Vector3(0f, 0f, toward * (TouchLift - DiscLift));
			l.Target = l.Touch.AddComponent<InteriorSquareCollider>();
			l.Target.width = l.Target.height = Art.DiscSize * texel;
			l.Target.localNormalPlane = Vector3.forward;
			// Above the stereo's keys, so a disc sticking out of the slot is what the hand takes there.
			l.Target.sortingLayer = StereoButton.SortingLayer + 1;
			l.Grab = go.AddComponent<DiscGrab>();
			l.Grab.attachedCollider = l.Target;
			l.Grab.Index = index;
			l.Grab.keepPhysicalCursorClose = false;
			l.Grab.directSpeedKeptOnRelease = 0.8f;
			l.Grab.Gravity = 600f * texel;
			l.Grab.Taken = g => Taken(l);
			l.Grab.Dropped = g => Dropped(l);
			loose.Add(l);
			Draw(l);
			return l;
		}

		private Loose Find(int index)
		{
			foreach (Loose l in loose)
			{
				if (l.Grab != null && l.Grab.Index == index)
				{
					return l;
				}
			}
			return null;
		}

		/// <summary>A disc gone (back in its sleeve, or in the stereo).</summary>
		private void Remove(Loose l)
		{
			if (l == null)
			{
				return;
			}
			loose.Remove(l);
			if (l.Grab != null && sticking == l.Grab.Index)
			{
				sticking = -1;
			}
			foreach (UnityEngine.Object o in l.Owned)
			{
				if (o != null)
				{
					Destroy(o);
				}
			}
			l.Owned.Clear();
		}

		/// <summary>The hand takes disc <paramref name="i"/> out of its sleeve: a loose disc where it was, which the hand then holds.</summary>
		private Grabbable TakeFromSleeve(int i)
		{
			if (unit == null || visor == null || IsOut(i))
			{
				return null;
			}
			Vector2Int corner = Art.DiscCorner(i);
			Vector2 centre = visorBox.position + new Vector2(corner.x + Art.DiscRadius, corner.y + Art.DiscRadius) * texel;
			Loose l = MakeLoose(i, centre);
			l.Grab.State = DiscGrab.Mode.Held;
			Sounds.Pull(gameObject);
			Mod.Info("Stereo: disc " + (i + 1) + " taken from the visor");
			return l.Grab;
		}

		/// <summary>The hand takes a loose disc: out of the slot if it was there, whole again.</summary>
		private void Taken(Loose l)
		{
			if (l.Grab.Index == sticking)
			{
				sticking = -1;
				Sounds.Pull(gameObject);
			}
			l.Inserting = false;
			Draw(l);
		}

		/// <summary>The hand lets a disc go: over the stereo it goes in, over the visor back in its sleeve, anywhere else it falls.</summary>
		private void Dropped(Loose l)
		{
			Vector2 c = l.Grab.Centre;
			float r = Art.DiscRadius * texel;
			Rect stereo = new Rect(-r * 0.5f, -r, face.W * texel + r, face.Rows * texel + 2f * r);
			if (stereo.Contains(c))
			{
				// Another disc sticking out of the slot goes back to the visor; the one playing comes out when this one is in.
				Loose other = Find(sticking);
				if (other != null && other != l)
				{
					Remove(other);
				}
				float row = c.y / texel - Art.DiscRadius;
				InSlot(l, Mathf.Clamp(row, face.DiscInRow, face.DiscOutRow + 20), true);
				l.RowTo = face.DiscInRow;
				Sounds.Disc(gameObject);
				Mod.Info("Stereo: disc " + (l.Grab.Index + 1) + " put in the slot");
				return;
			}
			Rect v = visorBox;
			if (visor != null && new Rect(v.x - r * 0.5f, v.y - r * 0.5f, v.width + r, v.height + r).Contains(c))
			{
				Sounds.Pull(gameObject);
				Mod.Info("Stereo: disc " + (l.Grab.Index + 1) + " back in the visor");
				Remove(l);
				return;
			}
			l.Grab.State = DiscGrab.Mode.Falling;
			l.FellAt = Time.time;
		}

		/// <summary>The disc in the slot with its bottom at canvas row <paramref name="row"/>, going in if <paramref name="inserting"/>.</summary>
		private void InSlot(Loose l, float row, bool inserting)
		{
			l.Grab.State = DiscGrab.Mode.Slot;
			l.Row = row;
			l.RowTo = Mathf.RoundToInt(row);
			l.Inserting = inserting;
			PlaceInSlot(l);
		}

		private void PlaceInSlot(Loose l)
		{
			int bottom = Mathf.RoundToInt(l.Row);
			l.Grab.Centre = new Vector2((face.SlotDiscLeft + Art.DiscRadius) * texel, (bottom + Art.DiscRadius) * texel);
			Draw(l);
		}

		/// <summary>Redraws a disc, cut along the slot while it is in it; its target covers only the part that shows.</summary>
		private void Draw(Loose l)
		{
			int[] clip = null;
			int shows = Art.DiscSize;
			int width = Art.DiscSize;
			if (l.Grab.State == DiscGrab.Mode.Slot)
			{
				// In the slot it is seen at the face's angle (an oval), and the part inside the slot is cut away.
				clip = face.SlotClip(Mathf.RoundToInt(l.Row));
				width = face.SlotDiscWidth;
				shows = 0;
				for (int x = (Art.DiscSize - width) / 2; x < (Art.DiscSize + width) / 2; x++)
				{
					shows = Mathf.Max(shows, Art.DiscSize - clip[x]);
				}
				shows = Mathf.Clamp(shows, 0, Art.DiscSize);
			}
			string key = clip == null ? "whole" : width + ":" + string.Join(",", clip);
			if (key != l.Shown)
			{
				l.Shown = key;
				Disc d = l.Grab.Index < Discs.All.Count ? Discs.All[l.Grab.Index] : null;
				Art.Canvas c = Art.LooseDisc(Art.LabelFor(d == null || d.Mix ? "MIX" : d.Title), d != null && d.Mix ? "MY" : (l.Grab.Index + 1).ToString(), clip, width);
				l.Texture.SetPixels32(c.Px);
				l.Texture.Apply(false);
			}
			// The target over the part above the slot only (the keys under it stay reachable); none while it slides.
			bool still = l.Grab.State != DiscGrab.Mode.Slot || (!l.Inserting && Mathf.Approximately(l.Row, l.RowTo));
			l.Target.enabled = still && shows > 2;
			l.Target.height = Mathf.Max(1, shows) * texel;
			Vector3 t = l.Touch.transform.localPosition;
			l.Touch.transform.localPosition = new Vector3(t.x, (Art.DiscRadius - shows * 0.5f) * texel, t.z);
		}

		/// <summary>
		/// A disc found late went in at <paramref name="at"/>: the discs from there on are one further on, so the one out of the slot and the
		/// loose ones keep their disc (and are drawn again, for the number on them).
		/// </summary>
		internal static void DiscAdded(int at)
		{
			if (sticking >= at)
			{
				sticking++;
			}
			StereoDash dash = Deck.Active != null ? Deck.Active.GetComponent<StereoDash>() : null;
			if (dash == null)
			{
				return;
			}
			foreach (Loose l in dash.loose)
			{
				if (l.Grab != null && l.Grab.Index >= at)
				{
					l.Grab.Index++;
					l.Shown = null;
					dash.Draw(l);
				}
			}
		}

		/// <summary>Is disc <paramref name="i"/> out of its sleeve (in the stereo or loose)?</summary>
		private bool IsOut(int i) => (deck != null && deck.DiscIndex == i) || Find(i) != null;

		/// <summary>Slides the discs in the slot, lets fallen ones go home, and keeps the discs in step with the deck.</summary>
		private void StepLoose()
		{
			int playing = deck != null ? deck.DiscIndex : -1;
			for (int k = loose.Count - 1; k >= 0; k--)
			{
				Loose l = loose[k];
				if (l.Grab == null)
				{
					Remove(l);
					continue;
				}
				// The deck took a disc by itself (the race's song, next or previous): the same one loose is in it; one sticking out goes home.
				if (playing >= 0 && !l.Inserting && (l.Grab.Index == playing || l.Grab.State == DiscGrab.Mode.Slot) && !l.Grab.IsGrabbed)
				{
					Remove(l);
					continue;
				}
				if (l.Grab.State == DiscGrab.Mode.Slot && !l.Grab.IsGrabbed)
				{
					if (!Mathf.Approximately(l.Row, l.RowTo))
					{
						l.Row = Mathf.MoveTowards(l.Row, l.RowTo, SlideSpeed * Time.deltaTime);
						PlaceInSlot(l);
					}
					else if (l.Inserting)
					{
						int i = l.Grab.Index;
						Remove(l);
						deck?.Insert(i);
						continue;
					}
					else
					{
						Draw(l);
					}
				}
				else if (l.Grab.State == DiscGrab.Mode.Falling && (Time.time - l.FellAt > FallTime || OutOfSight(l)))
				{
					Mod.Info("Stereo: disc " + (l.Grab.Index + 1) + " fell; back in the visor");
					Remove(l);
				}
			}
		}

		/// <summary>A falling disc wholly below the bottom of the screen.</summary>
		private bool OutOfSight(Loose l)
		{
			if (cam == null)
			{
				return l.Grab.Centre.y < -20f;
			}
			Vector3 top = l.Grab.transform.TransformPoint(new Vector3(0f, Art.DiscRadius * texel, 0f));
			return cam.WorldToViewportPoint(top).y < -0.05f;
		}

		// ---------------------------------------------------------------------------------------------------------------------------
		// Every frame.

		private void Update()
		{
			if (failed || unit == null)
			{
				return;
			}
			try
			{
				Mod.WatchConfig();
				if (built != Settings() || (builtDiscs != Discs.Version && Mod.ShowVisor.Value))
				{
					Build();
				}
				if (pressed != null && Time.unscaledTime - pressedAt > PressTime)
				{
					pressed = null;
				}
				StepLoose();
				DrawUnit();
				DrawVisor();
				DrawLcd();
			}
			catch (Exception e)
			{
				failed = true;
				Mod.Log.LogWarning("Stereo: stopped after an error: " + e);
			}
		}

		private void DrawUnit()
		{
			bool power = deck == null || deck.Power;
			float turn = deck != null ? deck.VolumeTurn : 0.7f;
			string key = pressed + "|" + power + "|" + turn;
			if (key == unitShown)
			{
				return;
			}
			unitShown = key;
			unitTexture.SetPixels32(face.Casing(pressed, power, turn).Px);
			unitTexture.Apply(false);
		}

		private void DrawVisor()
		{
			if (visorTexture == null)
			{
				return;
			}
			string key = Discs.All.Count + "|";
			for (int i = 0; i < Discs.All.Count; i++)
			{
				key += IsOut(i) ? "o" : "i";
			}
			if (key == visorShown)
			{
				return;
			}
			visorShown = key;
			List<Art.DiscLook> looks = new List<Art.DiscLook>();
			for (int i = 0; i < Discs.All.Count; i++)
			{
				Disc d = Discs.All[i];
				bool gone = IsOut(i);
				looks.Add(new Art.DiscLook { Label = Art.LabelFor(d.Mix ? "MIX" : d.Title), Mark = d.Mix ? "MY" : null, Out = gone });
				if (i < sleeves.Count && sleeves[i] != null)
				{
					sleeves[i].SetActive(!gone);
				}
			}
			visorTexture.SetPixels32(Art.VisorWallet(looks).Px);
			visorTexture.Apply(false);
		}

		private void DrawLcd()
		{
			string top = "", bottom = "";
			bool on = deck != null && deck.Power;
			if (on)
			{
				deck.Lines(out top, out bottom);
				bottom = Scroll(bottom ?? "");
			}
			string key = on + "|" + top + "|" + bottom;
			if (key == lcdShown)
			{
				return;
			}
			lcdShown = key;
			lcdTexture.SetPixels32(face.Display(top, bottom, on).Px);
			lcdTexture.Apply(false);
		}

		/// <summary>A name too long for the display runs along it, with a gap before it comes round again.</summary>
		private string Scroll(string text)
		{
			int cols = face.DisplayColumns;
			if (text.Length <= cols)
			{
				return text;
			}
			string loop = text + "    ";
			int at = Mathf.FloorToInt(Time.unscaledTime / ScrollStep) % loop.Length;
			string twice = loop + loop;
			return twice.Substring(at, cols);
		}

		private void Clear()
		{
			for (int k = loose.Count - 1; k >= 0; k--)
			{
				int keep = sticking;
				Remove(loose[k]);
				// Rebuilt with the game running: a disc sticking out of the slot stays out.
				sticking = keep;
			}
			foreach (UnityEngine.Object o in owned)
			{
				if (o != null)
				{
					Destroy(o);
				}
			}
			owned.Clear();
			sleeves.Clear();
			unit = visor = null;
			unitTexture = visorTexture = lcdTexture = null;
		}

		private void OnDestroy() => Clear();
	}

	/// <summary>
	/// A key as the cockpit hand sees it: the game's own click target (the hand clicks the Clickable over the interior collider it
	/// hovers, left and right), as the MiniMap's GPS does; HalfGripTools has no interaction API of its own.
	/// </summary>
	internal sealed class StereoButton : Clickable
	{
		/// <summary>
		/// The hand hovers the highest sortingLayer under it, then the nearest. The game's controls use -1 to 2; the stereo is far below
		/// (with the GPS, at -100), so it only gets the hand when no control wants it.
		/// </summary>
		internal const int SortingLayer = -100;

		internal Action Left;
		internal Action Right;

		protected override void OnClick() => Left?.Invoke();

		protected override void OnRightClick() => Right?.Invoke();
	}
}
