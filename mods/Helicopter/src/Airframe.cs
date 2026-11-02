using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfGripHelicopter
{
	/// <summary>
	/// What the car looks like while the Helicopter is fitted: the car's body and wheels hidden and a helicopter in their place, made
	/// of meshes built from <see cref="Shape"/> (no colliders, so the car's own collider and ground rays are all that touch anything) in
	/// plain Lit materials, one per <see cref="Paint"/>. Its shape is worked out from the car's body bounds, the wheels' bottom and the
	/// driver's head (the car's camera tagged MainCamera, which is also the cockpit view's world camera), so that view is from inside
	/// the cabin.
	/// <para>The cabin and the panes (the glass seen from inside) are drawn for every camera: they sit inside the opaque outer skin, so
	/// from outside they can't be seen. The one exception is the rear-view mirror's camera, which sits by the head and would see the
	/// bulkhead; it skips them. (0.1.2 drew
	/// the cabin only for a camera it recognised as the cockpit's, and in the game that never matched, so the inside was empty.)</para>
	/// <para>The body is the car's active skin model (<see cref="CarSkinController.carModels"/>; the Dacia Body mod swaps its meshes but
	/// keeps these objects) and the wheels' visual children; they are hidden again every half second, since a refit or a skin change
	/// switches them back on, and shown again when the Helicopter comes off. The parts go on the body's own layer, so every camera that
	/// draws the car draws the helicopter instead.</para>
	/// </summary>
	internal sealed class Airframe : MonoBehaviour
	{
		private static readonly Color BodyColour = new Color(0.30f, 0.36f, 0.22f);
		private static readonly Color GlassColour = new Color(0.22f, 0.36f, 0.44f);
		private static readonly Color MetalColour = new Color(0.14f, 0.15f, 0.16f);
		private static readonly Color WallColour = new Color(0.46f, 0.47f, 0.42f);
		private static readonly Color FloorColour = new Color(0.2f, 0.2f, 0.19f);
		private static readonly Color SeatColour = new Color(0.33f, 0.24f, 0.17f);

		private CarControllerNew cc;
		private Flight flight;
		private Transform main;
		private Transform tail;
		private readonly List<Renderer> inside = new List<Renderer>();
		private float angle;
		private float nextHide;
		private Shader lit;
		private int partLayer;
		private bool headFound;
		private Camera headCamera;
		private Shape.Frame frame;
		private Texture2D panelTexture;
		private bool hooked;
		private readonly List<Renderer> hidden = new List<Renderer>();
		private readonly List<Object> owned = new List<Object>();
		private readonly Dictionary<Paint, Material> materials = new Dictionary<Paint, Material>();

		/// <summary>The car's body bounds in its own space (from its active skin model), or a car-sized guess.</summary>
		internal static Bounds Body(CarControllerNew cc)
		{
			bool any = false;
			Bounds b = new Bounds(new Vector3(0f, 1.2f, 0f), new Vector3(3.3f, 1.6f, 8.7f));
			foreach (Renderer r in BodyRenderers(cc))
			{
				if (!r.enabled || !r.gameObject.activeInHierarchy)
				{
					continue;
				}
				Bounds w = r.bounds;
				for (int i = 0; i < 8; i++)
				{
					Vector3 corner = new Vector3((i & 1) == 0 ? w.min.x : w.max.x, (i & 2) == 0 ? w.min.y : w.max.y, (i & 4) == 0 ? w.min.z : w.max.z);
					Vector3 p = cc.transform.InverseTransformPoint(corner);
					if (!any)
					{
						b = new Bounds(p, Vector3.zero);
						any = true;
					}
					else
					{
						b.Encapsulate(p);
					}
				}
			}
			return b;
		}

		/// <summary>The renderers of the car's skin models (all of them, so the one shown later is hidden too).</summary>
		private static IEnumerable<Renderer> BodyRenderers(CarControllerNew cc)
		{
			CarSkinController skin = cc.GetComponent<CarSkinController>();
			if (skin == null || skin.carModels == null)
			{
				yield break;
			}
			foreach (GameObject model in skin.carModels)
			{
				if (model == null)
				{
					continue;
				}
				foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
				{
					if (r is MeshRenderer || r is SkinnedMeshRenderer)
					{
						yield return r;
					}
				}
			}
		}

		/// <summary>
		/// The car's lap-delta meter: sprites on the car's own dashboard (in the body model, but not meshes), which would hang in the cabin
		/// by the left pillar as a slanted dark bar.
		/// </summary>
		private static IEnumerable<Renderer> MeterRenderers(CarControllerNew cc)
		{
			foreach (LapDeltaTimerDisplay d in cc.GetComponentsInChildren<LapDeltaTimerDisplay>(true))
			{
				foreach (Renderer r in d.GetComponentsInChildren<Renderer>(true))
				{
					yield return r;
				}
			}
		}

		/// <summary>The wheels' meshes (not their particles or trails).</summary>
		private static IEnumerable<Renderer> WheelRenderers(CarControllerNew cc)
		{
			foreach (List<Transform> list in new[] { cc.frontWheels, cc.backWheels })
			{
				foreach (Transform w in list)
				{
					if (w == null)
					{
						continue;
					}
					foreach (Renderer r in w.GetComponentsInChildren<Renderer>(true))
					{
						if (r is MeshRenderer || r is SkinnedMeshRenderer)
						{
							yield return r;
						}
					}
				}
			}
		}

		/// <summary>The bottom of the wheels in the car's space: where the skids go.</summary>
		private static float Ground(CarControllerNew cc, Bounds body)
		{
			float low = float.MaxValue;
			foreach (Renderer r in WheelRenderers(cc))
			{
				if (!r.enabled || !r.gameObject.activeInHierarchy)
				{
					continue;
				}
				Bounds w = r.bounds;
				low = Mathf.Min(low, cc.transform.InverseTransformPoint(new Vector3(w.center.x, w.min.y, w.center.z)).y);
			}
			return low < float.MaxValue ? low : body.min.y;
		}

		internal static Airframe Build(CarControllerNew cc, Flight flight, Bounds body)
		{
			GameObject holder = new GameObject("HelicopterAirframe");
			holder.transform.SetParent(cc.transform, false);
			Airframe a = holder.AddComponent<Airframe>();
			try
			{
				a.cc = cc;
				a.flight = flight;
				a.Make(body);
			}
			catch
			{
				Destroy(holder);
				throw;
			}
			return a;
		}

		private void Make(Bounds b)
		{
			float ground = Ground(cc, b);
			partLayer = cc.gameObject.layer;
			foreach (Renderer r in BodyRenderers(cc))
			{
				partLayer = r.gameObject.layer;
				break;
			}
			gameObject.layer = partLayer;
			lit = FindLit();
			Hide();

			Vector3 head = Head(b);
			Shape.Frame f = Shape.Measure(b.size.z, b.size.x, b.center.x, b.center.z, ground, head);
			frame = f;
			foreach (Piece piece in Shape.Build(f))
			{
				Transform t = MeshPart(piece);
				if (piece.Name == "MainRotor")
				{
					main = t;
				}
				else if (piece.Name == "TailRotor")
				{
					tail = t;
				}
				else if (piece.Name == "Cabin" || piece.Name == "Panes")
				{
					inside.Add(t.GetComponent<Renderer>());
				}
			}
			RenderPipelineManager.beginCameraRendering += OnCamera;
			hooked = true;
			Mod.Info("Helicopter: airframe built on body " + b.size + " at " + b.center + ", ground " + ground.ToString("0.00") + ", head " + head + (headFound ? "" : " (guessed)") + ", pod " + f.PodLength.ToString("0.00") + " long, " + f.PodHeight.ToString("0.00") + " high, " + f.PodWidth.ToString("0.00") + " wide, layer " + LayerMask.LayerToName(partLayer) + ", hid " + hidden.Count + " car renderer(s), shader '" + (lit != null ? lit.name : "none") + "'.");
		}

		/// <summary>
		/// Where the cockpit view's world camera sits, in the car's space: the car's own camera tagged MainCamera (CarBodyPivot/Head in
		/// the game's cars). The cabin is built round it. Without one, a guess at the roof's middle.
		/// </summary>
		private Vector3 Head(Bounds b)
		{
			foreach (Camera c in cc.GetComponentsInChildren<Camera>(true))
			{
				if (c.CompareTag("MainCamera"))
				{
					Vector3 p = cc.transform.InverseTransformPoint(c.transform.position);
					Bounds near = b;
					near.Expand(b.size);
					if (near.Contains(p))
					{
						headFound = true;
						headCamera = c;
						return p;
					}
				}
			}
			return new Vector3(b.center.x, b.max.y - 0.1f * b.size.y, b.center.z - 0.03f * b.size.z);
		}

		/// <summary>The rear-view mirror's camera sits by the head, inside the cabin: it skips the cabin and the panes so the mirror
		/// shows what is behind. Every other camera draws them (from outside they are hidden by the outer skin).</summary>
		private void OnCamera(ScriptableRenderContext context, Camera cam)
		{
			bool show = cam == null || cam.name != "RearViewCamera";
			foreach (Renderer r in inside)
			{
				if (r != null)
				{
					r.enabled = show;
				}
			}
		}

		private Transform MeshPart(Piece piece)
		{
			Mesh mesh = new Mesh();
			mesh.name = "Helicopter" + piece.Name;
			mesh.hideFlags = HideFlags.HideAndDontSave;
			if (piece.Vertices.Count > 65000)
			{
				mesh.indexFormat = IndexFormat.UInt32;
			}
			mesh.SetVertices(piece.Vertices);
			mesh.SetUVs(0, piece.Uvs);
			// White vertex colours: the sprite shader the panes use multiplies by them.
			Color32[] white = new Color32[piece.Vertices.Count];
			for (int i = 0; i < white.Length; i++)
			{
				white[i] = new Color32(255, 255, 255, 255);
			}
			mesh.colors32 = white;
			List<Material> mats = new List<Material>();
			mesh.subMeshCount = piece.Triangles.Count;
			int sub = 0;
			foreach (KeyValuePair<Paint, List<int>> kv in piece.Triangles)
			{
				mesh.SetTriangles(kv.Value, sub++);
				mats.Add(MaterialFor(kv.Key));
			}
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			owned.Add(mesh);

			GameObject part = new GameObject(piece.Name);
			part.layer = partLayer;
			part.transform.SetParent(transform, false);
			part.transform.localPosition = piece.Pivot;
			part.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer r = part.AddComponent<MeshRenderer>();
			r.sharedMaterials = mats.ToArray();
			r.shadowCastingMode = piece.CastsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
			return part.transform;
		}

		private static Color ColourOf(Paint paint)
		{
			switch (paint)
			{
				case Paint.Glass: return GlassColour;
				case Paint.Metal: return MetalColour;
				case Paint.Wall: return WallColour;
				case Paint.Floor: return FloorColour;
				case Paint.Seat: return SeatColour;
				default: return BodyColour;
			}
		}

		private Material MaterialFor(Paint paint)
		{
			if (materials.TryGetValue(paint, out Material m))
			{
				return m;
			}
			if (paint == Paint.Pane)
			{
				m = PaneMaterial();
			}
			else if (lit != null)
			{
				m = new Material(lit);
				Color colour = paint == Paint.Panel ? Color.white : ColourOf(paint);
				if (m.HasProperty("_BaseColor"))
				{
					m.SetColor("_BaseColor", colour);
				}
				if (m.HasProperty("_Smoothness"))
				{
					m.SetFloat("_Smoothness", paint == Paint.Glass ? 0.7f : 0.2f);
				}
				// Back faces culled, always: the glass is opaque only from outside because its inside faces are never drawn, and
				// the cabin's walls only from inside.
				if (m.HasProperty("_Cull"))
				{
					m.SetFloat("_Cull", (float)CullMode.Back);
				}
				Texture2D tex = null;
				if (paint == Paint.Wall || paint == Paint.Floor || paint == Paint.Seat)
				{
					tex = Texture("HelicopterSpeckle", Shape.Speckle(out int n), n, n);
				}
				else if (paint == Paint.Panel)
				{
					tex = Texture("HelicopterPanel", Shape.PanelPixels(frame, out int w, out int h), w, h);
					tex.wrapMode = TextureWrapMode.Clamp;
					panelTexture = tex;
				}
				if (tex != null && m.HasProperty("_BaseMap"))
				{
					m.SetTexture("_BaseMap", tex);
				}
			}
			if (m == null)
			{
				return null;
			}
			m.name = "Helicopter" + paint;
			m.hideFlags = HideFlags.HideAndDontSave;
			materials[paint] = m;
			owned.Add(m);
			return m;
		}

		/// <summary>
		/// The glass seen from inside: the game's own unlit sprite shader (alpha blended, in every race's dash and world, so never
		/// stripped from the build), with the faint glass texture. A transparent Lit material would need a shader variant the game
		/// may not ship. The sprite shader draws both sides; from outside the opaque outer glass is in front and hides it.
		/// </summary>
		private Material PaneMaterial()
		{
			const string name = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
			Shader shader = null;
			foreach (Shader s in Resources.FindObjectsOfTypeAll<Shader>())
			{
				if (s != null && s.name == name)
				{
					shader = s;
					break;
				}
			}
			if (shader == null)
			{
				shader = Shader.Find(name);
			}
			if (shader == null)
			{
				Mod.Log.LogWarning("Helicopter: no sprite shader for the glass, so the windows are left clear.");
				return null;
			}
			Material m = new Material(shader);
			m.mainTexture = Texture("HelicopterGlass", Shape.GlassPixels(out int n), n, n);
			return m;
		}

		private (Quaternion Head, Quaternion View)? fittedHead;

		/// <summary>The head's camera's turn in the car and the dash camera's in the interior (keys 1/3 turn them).</summary>
		private (Quaternion Head, Quaternion View) HeadTurn(Cockpit cockpit)
		{
			Quaternion head = headCamera != null ? Quaternion.Inverse(transform.rotation) * headCamera.transform.rotation : Quaternion.identity;
			Transform root = cockpit != null ? cockpit.transform.parent : null;
			Quaternion view = cockpit != null && cockpit.View != null ? (root != null ? Quaternion.Inverse(root.rotation) : Quaternion.identity) * cockpit.View.transform.rotation : Quaternion.identity;
			return (head, view);
		}

		/// <summary>
		/// True while the cameras look the way they did when the face was last fitted (or it never was): the fit works through both
		/// cameras, so a refit with the head turned would put the clear parts in the wrong places.
		/// </summary>
		internal bool HeadAsFitted(Cockpit cockpit)
		{
			if (!fittedHead.HasValue)
			{
				return true;
			}
			(Quaternion head, Quaternion view) = HeadTurn(cockpit);
			return Quaternion.Angle(head, fittedHead.Value.Head) < 1f && Quaternion.Angle(view, fittedHead.Value.View) < 1f;
		}

		/// <summary>
		/// Redraws the instrument panel's face round the cockpit's controls, at the dash's pixel size. The controls are dash sprites drawn by
		/// the dash's camera over the world; each of their <see cref="Cockpit.Areas"/> is taken to the screen by that camera, then back out
		/// through the head's camera (the cockpit view's world camera) onto the face's plane. The face is kept clear there (a console under
		/// the levers), and its size in texture pixels is its width on the screen over a dash pixel's. The part below the screen's bottom
		/// edge gets nothing. Logs what it found; on any failure the face keeps the guessed layout.
		/// </summary>
		internal void FitPanel(Cockpit cockpit)
		{
			Camera view = cockpit != null ? cockpit.View : null;
			if (view == null || headCamera == null || panelTexture == null)
			{
				Mod.Info("Helicopter: panel left as guessed (" + (view == null ? "no dash camera" : headCamera == null ? "no head camera" : "no panel") + ").");
				return;
			}
			fittedHead = HeadTurn(cockpit);
			Shape.PanelFace(frame, out Vector3 bl, out Vector3 right, out Vector3 up);
			Vector3 o = transform.TransformPoint(bl);
			Vector3 ru = transform.TransformVector(right);
			Vector3 vu = transform.TransformVector(up);
			Vector3 normal = Vector3.Cross(ru, vu);

			// Viewport point -> (u, v) on the face, or null if the head's ray misses its plane.
			Vector2? OnFace(Vector3 vp)
			{
				Ray ray = headCamera.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
				float den = Vector3.Dot(ray.direction, normal);
				if (Mathf.Abs(den) < 1e-6f)
				{
					return null;
				}
				float t = Vector3.Dot(o - ray.origin, normal) / den;
				if (t <= 0f)
				{
					return null;
				}
				Vector3 q = ray.origin + ray.direction * t - o;
				return new Vector2(Vector3.Dot(q, ru) / ru.sqrMagnitude, Vector3.Dot(q, vu) / vu.sqrMagnitude);
			}

			List<Panel.Zone> zones = new List<Panel.Zone>();
			List<(Vector3 Min, Vector3 Max, bool Console, bool Mount)> areas = new List<(Vector3, Vector3, bool, bool)>();
			foreach ((Vector3 min, Vector3 max, bool console) in cockpit.Areas)
			{
				areas.Add((min, max, console, false));
			}
			if (cockpit.Gps.HasValue)
			{
				areas.Add((cockpit.Gps.Value.Min, cockpit.Gps.Value.Max, false, true));
			}
			foreach ((Vector3 min, Vector3 max, bool console, bool mount) in areas)
			{
				float u0 = float.MaxValue, v0 = float.MaxValue, u1 = float.MinValue, v1 = float.MinValue;
				bool any = false;
				for (int i = 0; i < 4; i++)
				{
					Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, min.z);
					Vector3 vp = view.WorldToViewportPoint(cockpit.World(corner));
					// Below the screen counts as at its edge: the face isn't seen there anyway.
					vp.y = Mathf.Max(vp.y, -0.05f);
					Vector2? uv = OnFace(vp);
					if (!uv.HasValue)
					{
						continue;
					}
					any = true;
					u0 = Mathf.Min(u0, uv.Value.x);
					v0 = Mathf.Min(v0, uv.Value.y);
					u1 = Mathf.Max(u1, uv.Value.x);
					v1 = Mathf.Max(v1, uv.Value.y);
				}
				if (any && u1 > 0f && u0 < 1f && v1 > 0f && v0 < 1f)
				{
					zones.Add(new Panel.Zone(u0, v0, u1, v1, console, mount));
				}
			}
			Vector2? bottom = OnFace(new Vector3(0.5f, 0f, 0f));
			float seen = bottom.HasValue ? Mathf.Clamp01(bottom.Value.y) : 0.45f;

			// A dash pixel's height on the screen (viewport units), and the face's top edge's width in the same units.
			Vector3 grip = cockpit.Areas.Count > 0 ? cockpit.Areas[0].Min : Vector3.zero;
			float pixel = Mathf.Abs(view.WorldToViewportPoint(cockpit.World(grip + Vector3.up * cockpit.Texel)).y - view.WorldToViewportPoint(cockpit.World(grip)).y);
			Vector3 a = headCamera.WorldToViewportPoint(o + vu);
			Vector3 b = headCamera.WorldToViewportPoint(o + vu + ru);
			int across = 300;
			if (pixel > 1e-5f && a.z > 0f && b.z > 0f)
			{
				across = Mathf.RoundToInt(Mathf.Abs(b.x - a.x) * headCamera.aspect / pixel);
			}
			Shape.PanelSize(frame, across, out int w, out int h);
			Canvas art = Panel.Draw(w, h, seen, zones);
			Texture2D fresh = Texture("HelicopterPanel", art.Pixels, w, h);
			fresh.wrapMode = TextureWrapMode.Clamp;
			if (materials.TryGetValue(Paint.Panel, out Material m) && m != null && m.HasProperty("_BaseMap"))
			{
				m.SetTexture("_BaseMap", fresh);
			}
			owned.Remove(panelTexture);
			Destroy(panelTexture);
			panelTexture = fresh;
			// The console's lines as seen from the seat, at the gate's two sides: where a step up the face goes on the screen, brought back
			// into the dash at the gate's depth. The gate is drawn leaning the same way.
			float? Slope(Vector3 local)
			{
				Vector3 vp = view.WorldToViewportPoint(cockpit.World(local));
				Vector2? uv = OnFace(vp);
				if (!uv.HasValue || vp.z <= 0f)
				{
					return null;
				}
				Vector3 f0 = o + ru * uv.Value.x + vu * uv.Value.y;
				Vector3 a0 = headCamera.WorldToViewportPoint(f0);
				Vector3 a1 = headCamera.WorldToViewportPoint(f0 + vu * 0.1f);
				if (a0.z <= 0f || a1.z <= 0f)
				{
					return null;
				}
				Vector3 p0 = cockpit.Local(view.ViewportToWorldPoint(new Vector3(a0.x, a0.y, vp.z)));
				Vector3 p1 = cockpit.Local(view.ViewportToWorldPoint(new Vector3(a1.x, a1.y, vp.z)));
				Vector3 d = p1 - p0;
				return d.y > 1e-5f ? d.x / d.y : (float?)null;
			}
			float? sl = Slope(cockpit.GateLeft), sr = Slope(cockpit.GateRight);
			if (sl.HasValue && sr.HasValue)
			{
				cockpit.FitGate(sl.Value, sr.Value);
			}

			System.Text.StringBuilder z = new System.Text.StringBuilder();
			foreach (Panel.Zone zone in zones)
			{
				z.Append(" [" + zone.U0.ToString("0.00") + "-" + zone.U1.ToString("0.00") + " x " + zone.V0.ToString("0.00") + "-" + zone.V1.ToString("0.00") + (zone.Console ? " console" : zone.Mount ? " GPS" : "") + "]");
			}
			Mod.Info("Helicopter: panel fitted, " + w + "x" + h + " pixels, seen from " + seen.ToString("0.00") + " up, gate leaning " + (sl.HasValue && sr.HasValue ? sl.Value.ToString("0.00") + " / " + sr.Value.ToString("0.00") : "as guessed") + ", clear" + z);
		}

		/// <summary>A point-filtered, repeating texture from plain pixels (rows from the bottom).</summary>
		private Texture2D Texture(string name, Color32[] px, int w, int h)
		{
			Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
			t.name = name;
			t.hideFlags = HideFlags.HideAndDontSave;
			t.filterMode = FilterMode.Point;
			t.wrapMode = TextureWrapMode.Repeat;
			t.SetPixels32(px);
			t.Apply(false, false);
			owned.Add(t);
			return t;
		}

		/// <summary>The URP Lit shader, from a material already in the car (always in the build), or by name.</summary>
		private Shader FindLit()
		{
			foreach (Renderer r in cc.GetComponentsInChildren<Renderer>(true))
			{
				foreach (Material m in r.sharedMaterials)
				{
					if (m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit")
					{
						return m.shader;
					}
				}
			}
			return Shader.Find("Universal Render Pipeline/Lit");
		}

		/// <summary>The car's body and wheel meshes and its lap-delta meter off (again: a refit or a skin change turns them back on).</summary>
		private void Hide()
		{
			foreach (IEnumerable<Renderer> set in new[] { BodyRenderers(cc), WheelRenderers(cc), MeterRenderers(cc) })
			{
				foreach (Renderer r in set)
				{
					if (r != null && r.enabled)
					{
						r.enabled = false;
						if (!hidden.Contains(r))
						{
							hidden.Add(r);
						}
					}
				}
			}
		}

		private void Update()
		{
			if (Time.unscaledTime >= nextHide && cc != null)
			{
				nextHide = Time.unscaledTime + 0.5f;
				Hide();
			}
			float rpm = flight != null ? flight.Rpm : 0f;
			// Up to 4 turns a second: reads as spinning without strobing at the game's frame rates.
			angle = (angle + rpm * 1440f * Time.deltaTime) % 360f;
			if (main != null)
			{
				main.localRotation = Quaternion.Euler(0f, angle, 0f);
			}
			if (tail != null)
			{
				tail.localRotation = Quaternion.Euler(angle * 2.3f, 0f, 0f);
			}
		}

		private void OnDestroy()
		{
			if (hooked)
			{
				RenderPipelineManager.beginCameraRendering -= OnCamera;
				hooked = false;
			}
			foreach (Renderer r in hidden)
			{
				if (r != null)
				{
					r.enabled = true;
				}
			}
			hidden.Clear();
			foreach (Object o in owned)
			{
				if (o != null)
				{
					Destroy(o);
				}
			}
			owned.Clear();
		}
	}
}
