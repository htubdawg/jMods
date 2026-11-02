using System.Collections.Generic;
using UnityEngine;

namespace HalfGripHelicopter
{
	/// <summary>The colours the helicopter is painted in; each is one submesh and one material.</summary>
	internal enum Paint { Body, Glass, Metal, Wall, Floor, Seat, Panel, Pane }

	/// <summary>
	/// Plain geometry of one piece of the helicopter: vertices, UVs and a triangle list per paint, wound so that the side meant to be
	/// seen is the front face (clockwise, as Unity draws; the other side is culled). It uses only Unity's managed structs, so
	/// <c>tools/preview</c> compiles it as it is and draws the same triangles outside the game.
	/// </summary>
	internal sealed class Piece
	{
		internal readonly string Name;
		internal readonly List<Vector3> Vertices = new List<Vector3>();
		internal readonly List<Vector2> Uvs = new List<Vector2>();
		internal readonly Dictionary<Paint, List<int>> Triangles = new Dictionary<Paint, List<int>>();
		/// <summary>Where the piece turns about (the rotors), its vertices relative to it.</summary>
		internal Vector3 Pivot;
		internal bool CastsShadows = true;

		internal Piece(string name)
		{
			Name = name;
		}

		internal int Vertex(Vector3 p, Vector2 uv)
		{
			Vertices.Add(p - Pivot);
			Uvs.Add(uv);
			return Vertices.Count - 1;
		}

		/// <summary>A triangle, turned if needed so its front faces along <paramref name="facing"/>.</summary>
		internal void Tri(Paint paint, int a, int b, int c, Vector3 facing)
		{
			Vector3 n = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
			if (n.sqrMagnitude < 1e-14f)
			{
				return;
			}
			if (!Triangles.TryGetValue(paint, out List<int> list))
			{
				list = new List<int>();
				Triangles[paint] = list;
			}
			list.Add(a);
			if (Vector3.Dot(n, facing) >= 0f)
			{
				list.Add(b);
				list.Add(c);
			}
			else
			{
				list.Add(c);
				list.Add(b);
			}
		}
	}

	/// <summary>
	/// The helicopter's shape, worked out from a few measures of the car (all in the car's own space, z forward, y up). The point
	/// everything hangs on is the driver's head, the cockpit view's world camera: the cabin is built round it, so that view is from
	/// a seat inside, looking out through the windscreen and the door windows.
	/// <list type="bullet">
	/// <item>The pod is lofted along its length: a rounded nose, the cabin, then a tail cone rising and narrowing into the boom. Its
	/// height is at least 0.4 and at most 0.5 of its length (0.1.2's height came from the car's width and the head's height and made
	/// it a ball); a high head makes it longer, never rounder.</item>
	/// <item>Two skins. The outer faces out, painted body, glass and frames: from inside it is culled, so the glass, opaque from
	/// outside, is clear from inside. The inner, a little smaller, faces in and has holes where the glass is, so from the seat the
	/// walls, floor and frames show and the windows look out. It ends at a bulkhead behind the seats.</item>
	/// <item>Glass seen from inside: a third skin just inside the windows, faintly tinted with glare bands (drawn see-through).</item>
	/// <item>Inside: a glareshield along the windscreen's bottom edge, an instrument panel with a drawn face under it, a floor, two
	/// seats.</item>
	/// <item>Outside: engine housing, mast and two-blade-pair rotor, tail boom, fin, stabiliser, tail rotor, skids on struts at the
	/// wheels' bottom.</item>
	/// </list>
	/// </summary>
	internal static class Shape
	{
		/// <summary>The inside's speckle texture repeats every this many car units (32 texels of about 0.04).</summary>
		internal const float TextureSpan = 1.28f;

		private const int Around = 96;
		private const float NoseEnd = 0.28f;
		private const float TailStart = 0.6f;
		private const float SuperEllipse = 2.6f;
		private const float InnerScale = 0.93f;
		private const float BulkheadT = 0.64f;

		/// <summary>The measures the shape is made from, and what it works out from them.</summary>
		internal struct Frame
		{
			// In: the car.
			internal float Length;
			internal float Width;
			internal float CentreX;
			internal float CentreZ;
			internal float Ground;
			internal Vector3 Head;

			// Out: the pod.
			internal float PodLength;
			internal float PodHeight;
			internal float PodWidth;
			internal float NoseZ;
			internal float CentreY;
			internal float PodX;
			internal float BoomRadius;
			internal float BoomRise;
		}

		internal static Frame Measure(float length, float width, float centreX, float centreZ, float ground, Vector3 head)
		{
			Frame f = new Frame { Length = length, Width = width, CentreX = centreX, CentreZ = centreZ, Ground = ground, Head = head };
			float eye = Mathf.Max(head.y - ground, 0.3f);
			// The head sits 57% of the way up; the belly about 0.35 of the pod's height off the ground, as on a light helicopter.
			float wantHeight = eye / (0.57f + 0.35f);
			f.PodLength = Mathf.Max(0.62f * length, wantHeight / 0.5f);
			f.PodHeight = Mathf.Clamp(wantHeight, 0.4f * f.PodLength, 0.5f * f.PodLength);
			f.PodWidth = Mathf.Min(0.82f * width, 0.85f * f.PodHeight);
			f.CentreY = head.y - 0.07f * f.PodHeight;
			float belly = f.CentreY - 0.5f * f.PodHeight;
			float lowest = ground + 0.12f * f.PodHeight;
			if (belly < lowest)
			{
				f.CentreY += lowest - belly;
			}
			// The head 40% of the way back: the windscreen ahead of it, the doors beside it.
			f.NoseZ = head.z + 0.4f * f.PodLength;
			f.PodX = Mathf.Clamp(centreX, head.x - 0.28f * f.PodWidth, head.x + 0.28f * f.PodWidth);
			f.BoomRadius = 0.075f * f.PodHeight;
			f.BoomRise = 0.24f * f.PodHeight;
			return f;
		}

		/// <summary>All of it: outer skin and outside parts, the cabin, the glass seen from inside, the main rotor and the tail rotor
		/// (each turning about its pivot).</summary>
		internal static List<Piece> Build(Frame f)
		{
			Piece outer = new Piece("Fuselage");
			Piece cabin = new Piece("Cabin") { CastsShadows = false };
			Piece panes = new Piece("Panes") { CastsShadows = false };
			Pod(outer, f, Skin.Outer);
			Pod(cabin, f, Skin.Cabin);
			Pod(panes, f, Skin.Panes);
			Interior(cabin, f);
			Outside(outer, f, out Piece main, out Piece tail);
			return new List<Piece> { outer, cabin, panes, main, tail };
		}

		// ---- The pod ----

		private enum Skin { Outer, Cabin, Panes }

		private const int NoseRings = 24;
		private const int Rings = 72;
		private const float PaneScale = 0.965f;
		private const float WindscreenEnd = 0.25f;
		private const float WindscreenLow = -0.55f;
		private const float WindscreenHigh = 0.45f;

		/// <summary>How far along the pod (0 nose, 1 tail) ring <paramref name="i"/> is: the nose's rings close together where its
		/// curve is tight.</summary>
		private static float RingT(int i)
		{
			if (i <= NoseRings)
			{
				return NoseEnd * (1f - Mathf.Cos(0.5f * Mathf.PI * i / NoseRings));
			}
			return NoseEnd + (1f - NoseEnd) * (i - NoseRings) / (Rings - NoseRings);
		}

		/// <summary>The pod's cross-section at <paramref name="t"/>: its half sizes and its middle's height above the cabin's.</summary>
		private static void Section(Frame f, float t, out float halfW, out float halfH, out float rise)
		{
			float hw = 0.5f * f.PodWidth;
			float hh = 0.5f * f.PodHeight;
			if (t < NoseEnd)
			{
				float k = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((NoseEnd - t) / NoseEnd, 2f)));
				halfW = hw * k;
				halfH = hh * k;
				// The nose's tip sits a little low, a chin under the windscreen.
				rise = -0.18f * hh * (1f - k);
				return;
			}
			if (t <= TailStart)
			{
				halfW = hw;
				halfH = hh;
				rise = 0f;
				return;
			}
			float s = (t - TailStart) / (1f - TailStart);
			s = s * s * (3f - 2f * s);
			float end = 1.15f * f.BoomRadius;
			halfW = Mathf.Lerp(hw, end, s);
			halfH = Mathf.Lerp(hh, end, s);
			rise = Mathf.Lerp(0f, f.BoomRise, s);
		}

		/// <summary>A point on the pod: ring <paramref name="t"/>, round its section at angle <paramref name="a"/> (0 at the bottom).</summary>
		private static Vector3 PodPoint(Frame f, float t, float a, float scale)
		{
			Section(f, t, out float hw, out float hh, out float rise);
			float sn = Mathf.Sin(a);
			float cn = -Mathf.Cos(a);
			float e = 2f / SuperEllipse;
			float x = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e);
			float y = Mathf.Sign(cn) * Mathf.Pow(Mathf.Abs(cn), e);
			return new Vector3(f.PodX + scale * hw * x, f.CentreY + rise + scale * hh * y, f.NoseZ - t * f.PodLength);
		}

		/// <summary>The pod's half width at height <paramref name="y"/> (car space) at <paramref name="t"/>, shrunk by
		/// <paramref name="scale"/>, or 0 where the section does not reach that height.</summary>
		private static float HalfWidthAt(Frame f, float t, float y, float scale)
		{
			Section(f, t, out float hw, out float hh, out float rise);
			if (hh <= 0f)
			{
				return 0f;
			}
			float v = Mathf.Abs((y - f.CentreY - rise) / (scale * hh));
			if (v >= 1f)
			{
				return 0f;
			}
			return scale * hw * Mathf.Pow(1f - Mathf.Pow(v, SuperEllipse), 1f / SuperEllipse);
		}

		/// <summary>
		/// Which parts of the pod are glass, at <paramref name="t"/> along it, at height <paramref name="y"/> and sideways
		/// <paramref name="x"/> (both as fractions of the cabin's half height and width, from its middle): the windscreen, cut by two
		/// level lines across the nose, and a door window each side by the head. Level cuts give clean curved edges on the round nose
		/// (0.1.3 cut by angle round each ring, which pinched the glass to a point at the tip and left a spike of floor inside).
		/// <paramref name="grow"/> widens them, for the frames round them.
		/// </summary>
		private static bool Glass(float t, float y, float x, float grow)
		{
			bool windscreen = t < WindscreenEnd + 0.25f * grow && y > WindscreenLow - grow && y < WindscreenHigh + grow;
			bool door = t > 0.3f - 0.25f * grow && t < 0.52f + 0.25f * grow && Mathf.Abs(x) > 0.5f && y > -0.25f - grow && y < 0.6f + grow;
			return windscreen || door;
		}

		/// <summary>The heights (fractions of the cabin's half height) where a window's or a frame's edge runs level, lowest first.</summary>
		private static readonly float[] EdgeLevels =
		{
			WindscreenLow - Frames, WindscreenLow, -0.25f - Frames, -0.25f, WindscreenHigh, WindscreenHigh + Frames, 0.6f, 0.6f + Frames,
		};

		private static int[] columnsPerGap;

		/// <summary>The angle (0 bottom, pi top, right side) where ring <paramref name="t"/> reaches a level, or the bottom or top
		/// where it does not reach it.</summary>
		private static float LevelAngle(Frame f, float t, float level)
		{
			Section(f, t, out _, out float hh, out float rise);
			float yy = hh > 0f ? (level * 0.5f * f.PodHeight - rise) / hh : (level > 0f ? 1f : -1f);
			if (yy <= -1f)
			{
				return 0f;
			}
			if (yy >= 1f)
			{
				return Mathf.PI;
			}
			float cn = Mathf.Sign(yy) * Mathf.Pow(Mathf.Abs(yy), 0.5f * SuperEllipse);
			return Mathf.Acos(Mathf.Clamp(-cn, -1f, 1f));
		}

		/// <summary>
		/// The angles round ring <paramref name="t"/>. Every ring has the same columns, and each window and frame edge has a column of
		/// its own that sits exactly on that edge's level on every ring (bottom or top where the ring doesn't reach it); the columns
		/// between are spread evenly. The edges then run along the mesh's lines. 0.1.3 cut across the mesh's squares, and the
		/// next try snapped the nearest column per ring, which jumped from column to column along the nose: both saw-toothed.
		/// </summary>
		private static float[] Angles(Frame f, float t)
		{
			int half = Around / 2;
			int gaps = EdgeLevels.Length + 1;
			if (columnsPerGap == null)
			{
				// Columns per gap between edges in proportion to its size round the cabin's own section, at least one each.
				float[] keys = Keys(f, 0.45f);
				columnsPerGap = new int[gaps];
				int used = 0;
				int widest = 0;
				for (int g = 0; g < gaps; g++)
				{
					columnsPerGap[g] = Mathf.Max(1, Mathf.RoundToInt(half * (keys[g + 1] - keys[g]) / Mathf.PI));
					used += columnsPerGap[g];
					if (columnsPerGap[g] > columnsPerGap[widest])
					{
						widest = g;
					}
				}
				columnsPerGap[widest] += half - used;
			}
			float[] k = Keys(f, t);
			float[] a = new float[Around + 1];
			int j = 0;
			for (int g = 0; g < gaps; g++)
			{
				for (int c = 0; c < columnsPerGap[g]; c++)
				{
					a[j++] = Mathf.Lerp(k[g], k[g + 1], (float)c / columnsPerGap[g]);
				}
			}
			a[half] = Mathf.PI;
			for (int m = 1; m < half; m++)
			{
				a[Around - m] = 2f * Mathf.PI - a[m];
			}
			a[Around] = 2f * Mathf.PI;
			return a;
		}

		/// <summary>The bottom, each edge level's angle in order, and the top, on ring <paramref name="t"/>.</summary>
		private static float[] Keys(Frame f, float t)
		{
			float[] k = new float[EdgeLevels.Length + 2];
			k[0] = 0f;
			for (int i = 0; i < EdgeLevels.Length; i++)
			{
				k[i + 1] = Mathf.Max(k[i], LevelAngle(f, t, EdgeLevels[i]));
			}
			k[k.Length - 1] = Mathf.PI;
			return k;
		}

		/// <summary>How wide the window frames are, as a fraction of the cabin's half size.</summary>
		private const float Frames = 0.05f;

		private static void Pod(Piece p, Frame f, Skin skin)
		{
			float scale = skin == Skin.Outer ? 1f : skin == Skin.Cabin ? InnerScale : PaneScale;
			float hw0 = 0.5f * f.PodWidth;
			float hh0 = 0.5f * f.PodHeight;
			int last = Rings;
			if (skin != Skin.Outer)
			{
				// The cabin ends at the bulkhead.
				while (last > NoseRings && RingT(last) > BulkheadT)
				{
					last--;
				}
			}
			int[,] index = new int[last + 1, Around + 1];
			float[] ts = new float[last + 1];
			// Each corner's place on the outer skin, as fractions of the cabin, for the glass: all three skins agree on it.
			var xs = new float[last + 1, Around + 1];
			var ys = new float[last + 1, Around + 1];
			for (int i = 0; i <= last; i++)
			{
				float t = RingT(i);
				ts[i] = t;
				Section(f, t, out float hw, out float hh, out _);
				float radius = 0.5f * (hw + hh) * scale;
				float[] angles = Angles(f, t);
				for (int j = 0; j <= Around; j++)
				{
					float a = angles[j];
					Vector3 o = PodPoint(f, t, a, 1f);
					xs[i, j] = (o.x - f.PodX) / hw0;
					ys[i, j] = (o.y - f.CentreY) / hh0;
					Vector3 v = PodPoint(f, t, a, scale);
					Vector2 uv = skin == Skin.Panes
						? new Vector2((v.z + 0.6f * v.x) / GlassSpan, v.y / GlassSpan)
						: new Vector2(v.z / TextureSpan, a * radius / TextureSpan);
					index[i, j] = p.Vertex(v, uv);
				}
			}
			for (int i = 0; i < last; i++)
			{
				float tm = 0.5f * (ts[i] + ts[i + 1]);
				for (int j = 0; j < Around; j++)
				{
					float x = 0.25f * (xs[i, j] + xs[i, j + 1] + xs[i + 1, j] + xs[i + 1, j + 1]);
					float y = 0.25f * (ys[i, j] + ys[i, j + 1] + ys[i + 1, j] + ys[i + 1, j + 1]);
					bool glass = Glass(tm, y, x, 0f);
					bool frame = !glass && Glass(tm, y, x, Frames);
					Paint paint;
					switch (skin)
					{
						case Skin.Cabin:
							if (glass)
							{
								continue;
							}
							paint = y < -0.55f ? Paint.Floor : Paint.Wall;
							break;
						case Skin.Panes:
							if (!glass)
							{
								continue;
							}
							paint = Paint.Pane;
							break;
						default:
							paint = glass ? Paint.Glass : frame ? Paint.Metal : Paint.Body;
							break;
					}
					int a = index[i, j], b = index[i, j + 1], c = index[i + 1, j], d = index[i + 1, j + 1];
					// Out from the pod's axis at this ring; the cabin and the panes face the other way, toward the seat.
					Vector3 mid = 0.25f * (p.Vertices[a] + p.Vertices[b] + p.Vertices[c] + p.Vertices[d]);
					Section(f, tm, out _, out _, out float rise);
					Vector3 axis = new Vector3(f.PodX, f.CentreY + rise, mid.z);
					Vector3 outward = mid - axis;
					if (i == 0 || outward.sqrMagnitude < 1e-8f)
					{
						outward += Vector3.forward * 0.5f * outward.magnitude + Vector3.forward * 1e-3f;
					}
					if (skin != Skin.Outer)
					{
						outward = -outward;
					}
					p.Tri(paint, a, b, d, outward);
					p.Tri(paint, a, d, c, outward);
				}
			}
			if (skin == Skin.Panes)
			{
				return;
			}
			// Close the end: the tail's tip outside, the bulkhead inside (facing forward, into the cabin).
			Vector3 centre = PodPoint(f, ts[last], 0f, 0f);
			int ci = p.Vertex(centre, new Vector2(centre.x / TextureSpan, centre.y / TextureSpan));
			Vector3 face = skin == Skin.Cabin ? Vector3.forward : Vector3.back;
			for (int j = 0; j < Around; j++)
			{
				p.Tri(skin == Skin.Cabin ? Paint.Wall : Paint.Body, ci, index[last, j], index[last, j + 1], face);
			}
		}

		// ---- Inside ----

		private static void Interior(Piece p, Frame f)
		{
			float hh = 0.5f * f.PodHeight;
			float hw = 0.5f * f.PodWidth;
			float floorTop = f.CentreY - 0.62f * hh;
			float back = f.NoseZ - BulkheadT * f.PodLength;

			// The glareshield: a level shelf at the windscreen's bottom edge, from the nose's tip back to the panel, following the
			// cabin's curve so it stays inside the skin. It closes the nose below the glass, so nothing of the floor shows up front.
			float shelfY = f.CentreY + WindscreenLow * hh;
			float shelfEnd = WindscreenEnd + 0.03f;
			const int steps = 16;
			int prevL = -1, prevR = -1;
			for (int k = 0; k <= steps; k++)
			{
				float t = shelfEnd * k / steps;
				float half = HalfWidthAt(f, t, shelfY, 0.9f);
				float z = f.NoseZ - t * f.PodLength;
				int l = p.Vertex(new Vector3(f.PodX - half, shelfY, z), new Vector2(-half / TextureSpan, z / TextureSpan));
				int r = p.Vertex(new Vector3(f.PodX + half, shelfY, z), new Vector2(half / TextureSpan, z / TextureSpan));
				if (prevL >= 0)
				{
					p.Tri(Paint.Wall, prevL, prevR, r, Vector3.up);
					p.Tri(Paint.Wall, prevL, r, l, Vector3.up);
				}
				prevL = l;
				prevR = r;
			}

			// The instrument panel on a pedestal in the middle, standing on the shelf's back part and leaning away from the seats, its
			// face drawn (Panel: gauges, radio, lights, switches, fitted round the cockpit's controls). It is narrower than the cabin, so
			// the bubble nose shows each side of it.
			PanelGeometry(f, out float panelHalf, out float zb, out float zt, out float yt, out Vector3 lean, out Vector3 toSeat, out Vector3 faceMid);
			float thick = 0.05f * f.PodLength;
			float tilt = Mathf.Atan2(zt - zb, yt - shelfY) * Mathf.Rad2Deg;
			Box(p, Paint.Metal, faceMid - toSeat * (0.5f * thick), new Vector3(2f * panelHalf, lean.magnitude, thick), AboutX(tilt));
			PanelFace(f, out Vector3 bl, out Vector3 right, out Vector3 upward);
			Quad(p, Paint.Panel, bl, bl + right, bl + upward, bl + right + upward, toSeat);
			// A hood over its top edge, against the glare.
			Box(p, Paint.Metal, new Vector3(f.PodX, yt + 0.015f * hh, zt - 0.012f * f.PodLength), new Vector3(2.04f * panelHalf, 0.025f * hh, 0.04f * f.PodLength), Quaternion.identity);
			float lowZ = zb;

			// Floor: a flat deck over the curved bottom, from the panel back to the bulkhead.
			Box(p, Paint.Floor, new Vector3(f.PodX, floorTop - 0.04f * hh, 0.5f * (lowZ + back)), new Vector3(1.6f * hw, 0.08f * hh, lowZ - back), Quaternion.identity);
			// Two seats: the driver's under the head and one beside it.
			float seatZ = f.Head.z - 0.03f * f.PodLength;
			float seatW = 0.62f * hw;
			foreach (float sx in new[] { f.Head.x, 2f * f.PodX - f.Head.x })
			{
				if (Mathf.Abs(sx - f.Head.x) > 1e-3f && Mathf.Abs(sx - f.Head.x) < seatW)
				{
					continue;
				}
				float seatTop = floorTop + 0.38f * hh;
				Box(p, Paint.Seat, new Vector3(sx, 0.5f * (floorTop + seatTop), seatZ), new Vector3(seatW, seatTop - floorTop, 0.13f * f.PodLength), Quaternion.identity);
				float backZ = seatZ - 0.08f * f.PodLength;
				Box(p, Paint.Seat, new Vector3(sx, seatTop + 0.42f * hh, backZ), new Vector3(seatW, 0.9f * hh, 0.035f * f.PodLength), AboutX(-10f));
			}
		}

		/// <summary>A quad with its texture whole across it (0 to 1), facing along <paramref name="facing"/>.</summary>
		private static void Quad(Piece p, Paint paint, Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, Vector3 facing)
		{
			int a = p.Vertex(bl, new Vector2(0f, 0f));
			int b = p.Vertex(br, new Vector2(1f, 0f));
			int c = p.Vertex(tl, new Vector2(0f, 1f));
			int d = p.Vertex(tr, new Vector2(1f, 1f));
			p.Tri(paint, a, b, d, facing);
			p.Tri(paint, a, d, c, facing);
		}

		// ---- Textures (plain pixels, rows from the bottom; Airframe makes them into point-filtered textures) ----

		/// <summary>The glass texture repeats every this many car units.</summary>
		internal const float GlassSpan = 2.56f;

		/// <summary>A grey speckle with rivet rows, to tint the inside's walls, floor and seats.</summary>
		internal static Color32[] Speckle(out int size)
		{
			size = 32;
			System.Random rng = new System.Random(4163270);
			Color32[] px = new Color32[size * size];
			for (int i = 0; i < px.Length; i++)
			{
				int roll = rng.Next(100);
				byte g = (byte)(roll < 12 ? 196 : roll < 22 ? 255 : 228);
				px[i] = new Color32(g, g, g, 255);
			}
			for (int y = 0; y < size; y += 8)
			{
				for (int x = 2; x < size; x += 4)
				{
					px[y * size + x] = new Color32(160, 160, 160, 255);
				}
			}
			return px;
		}

		/// <summary>The glass seen from inside: a faint blue tint, two hard diagonal glare bands and a few specks, so it reads as glass
		/// (0.1.3 left it an empty hole). Mostly almost clear.</summary>
		internal static Color32[] GlassPixels(out int size)
		{
			size = 64;
			System.Random rng = new System.Random(4163);
			Color32[] px = new Color32[size * size];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					int d = (x + y) % size;
					byte a = 22;
					if (d >= 8 && d < 12)
					{
						a = 60;
					}
					else if (d >= 15 && d < 17)
					{
						a = 44;
					}
					if (rng.Next(160) == 0)
					{
						a = 80;
					}
					px[y * size + x] = new Color32(215, 235, 245, a);
				}
			}
			return px;
		}

		private static void PanelGeometry(Frame f, out float panelHalf, out float zb, out float zt, out float yt, out Vector3 lean, out Vector3 toSeat, out Vector3 faceMid)
		{
			float hh = 0.5f * f.PodHeight;
			float shelfY = f.CentreY + WindscreenLow * hh;
			panelHalf = 0.25f * f.PodWidth;
			zb = f.NoseZ - (WindscreenEnd + 0.03f) * f.PodLength;
			zt = zb + 0.06f * f.PodLength;
			float yb = shelfY;
			yt = f.CentreY - 0.12f * hh;
			lean = new Vector3(0f, yt - yb, zt - zb);
			toSeat = new Vector3(0f, zt - zb, -(yt - yb)).normalized;
			faceMid = new Vector3(f.PodX, 0.5f * (yb + yt), 0.5f * (zb + zt));
		}

		/// <summary>
		/// The panel's drawn face in the airframe's space: its bottom-left corner (texture 0,0), and the vectors along its bottom edge
		/// (to u = 1) and up its side (to v = 1). <see cref="Airframe.FitPanel"/> projects the cockpit's controls onto it.
		/// </summary>
		internal static void PanelFace(Frame f, out Vector3 bottomLeft, out Vector3 right, out Vector3 up)
		{
			PanelGeometry(f, out float panelHalf, out _, out _, out _, out Vector3 lean, out Vector3 toSeat, out Vector3 faceMid);
			Vector3 lift = toSeat * 0.004f * f.PodLength;
			Vector3 side = Vector3.right * (0.94f * panelHalf);
			Vector3 half = lean * 0.47f;
			bottomLeft = faceMid - side - half + lift;
			right = 2f * side;
			up = 2f * half;
		}

		/// <summary>The face's texture size at about one texture pixel per dash pixel on the game's car.</summary>
		internal static void PanelSize(Frame f, int across, out int w, out int h)
		{
			PanelFace(f, out _, out Vector3 right, out Vector3 up);
			w = Mathf.Clamp(across, 32, 1024);
			h = Mathf.Clamp(Mathf.RoundToInt(w * up.magnitude / Mathf.Max(1e-4f, right.magnitude)), 16, 1024);
		}

		/// <summary>The face as drawn before the cockpit's controls are measured: <see cref="Panel.GuessZones"/>, 300 pixels across.</summary>
		internal static Color32[] PanelPixels(Frame f, out int w, out int h)
		{
			PanelSize(f, 300, out w, out h);
			List<Panel.Zone> zones = Panel.GuessZones(out float seen);
			return Panel.Draw(w, h, seen, zones).Pixels;
		}

		// ---- Outside ----

		private static void Outside(Piece p, Frame f, out Piece main, out Piece tail)
		{
			float hh = 0.5f * f.PodHeight;
			float hw = 0.5f * f.PodWidth;
			float x = f.PodX;
			float roof = f.CentreY + hh;
			float mastZ = f.NoseZ - 0.47f * f.PodLength;

			// Engine housing on the roof behind the mast.
			Box(p, Paint.Body, new Vector3(x, roof + 0.05f * hh, mastZ - 0.1f * f.PodLength), new Vector3(0.9f * hw, 0.3f * hh, 0.3f * f.PodLength), Quaternion.identity);
			Box(p, Paint.Metal, new Vector3(x, roof + 0.12f * hh, mastZ - 0.255f * f.PodLength), new Vector3(0.5f * hw, 0.14f * hh, 0.012f * f.PodLength), Quaternion.identity);
			// Mast.
			float hubY = roof + 0.48f * hh;
			Cylinder(p, Paint.Metal, new Vector3(x, roof, mastZ), new Vector3(x, hubY, mastZ), 0.07f * hw, 10);

			// Tail boom, from inside the tail cone's tip to past the car's back.
			float tailEnd = f.NoseZ - f.PodLength;
			float boomY = f.CentreY + f.BoomRise;
			float tailZ = Mathf.Min(f.CentreZ - 0.62f * f.Length, tailEnd - 0.35f * f.Length);
			Cylinder(p, Paint.Body, new Vector3(x, boomY, tailEnd + 0.06f * f.PodLength), new Vector3(x, boomY, tailZ), f.BoomRadius, 12);
			// Fin, swept back, and the stabiliser.
			Box(p, Paint.Body, new Vector3(x, boomY + 0.5f * hh, tailZ + 0.05f * f.Length), new Vector3(0.06f * hw, 1.1f * hh, 0.1f * f.Length), AboutX(-22f));
			Box(p, Paint.Body, new Vector3(x, boomY, tailZ + 0.13f * f.Length), new Vector3(1.3f * hw, 0.06f * hh, 0.07f * f.Length), Quaternion.identity);

			// Skids on struts, at the wheels' bottom so it stands where the car stood.
			float skidY = f.Ground + 0.03f * hh;
			float skidHalf = 0.4f * f.PodLength;
			float skidZ = f.NoseZ - 0.48f * f.PodLength;
			float belly = f.CentreY - hh;
			float tube = 0.035f * hh + 0.02f * hw;
			foreach (float side in new[] { -1f, 1f })
			{
				float sx = x + side * 1.05f * hw;
				Cylinder(p, Paint.Metal, new Vector3(sx, skidY, skidZ - skidHalf), new Vector3(sx, skidY, skidZ + skidHalf), tube, 8);
				// Its front bent up.
				Cylinder(p, Paint.Metal, new Vector3(sx, skidY, skidZ + skidHalf), new Vector3(sx, skidY + 0.16f * hh, skidZ + skidHalf + 0.06f * f.PodLength), tube, 8);
				foreach (float along in new[] { -0.55f, 0.45f })
				{
					float sz = skidZ + along * skidHalf;
					Cylinder(p, Paint.Metal, new Vector3(sx, skidY, sz), new Vector3(x + side * 0.45f * hw, belly + 0.2f * hh, sz), 0.8f * tube, 8);
				}
			}

			// Main rotor: hub and two blade pairs, about 1.15 car lengths across, turning about the mast.
			main = new Piece("MainRotor") { Pivot = new Vector3(x, hubY, mastZ) };
			Cylinder(main, Paint.Metal, new Vector3(x, hubY - 0.04f * hh, mastZ), new Vector3(x, hubY + 0.06f * hh, mastZ), 0.18f * hw, 12);
			float span = 1.15f * f.Length;
			Box(main, Paint.Metal, main.Pivot, new Vector3(span, 0.025f * hh, 0.05f * f.Length), Quaternion.identity);
			Box(main, Paint.Metal, main.Pivot, new Vector3(0.05f * f.Length, 0.025f * hh, span), Quaternion.identity);

			// Tail rotor on the fin's side, turning about the x axis.
			tail = new Piece("TailRotor") { Pivot = new Vector3(x + 0.06f * hw + 0.04f * hw, boomY + 0.75f * hh, tailZ + 0.02f * f.Length) };
			Box(tail, Paint.Metal, tail.Pivot, new Vector3(0.03f * hw, 0.85f * hh, 0.04f * f.Length), Quaternion.identity);
			Box(tail, Paint.Metal, tail.Pivot, new Vector3(0.03f * hw, 0.04f * f.Length, 0.85f * hh), Quaternion.identity);
		}

		// ---- Primitives ----

		/// <summary>A turn about the x axis, made by hand: <c>Quaternion.Euler</c> is native code and does not run in tools/preview.</summary>
		internal static Quaternion AboutX(float degrees)
		{
			float half = 0.5f * degrees * Mathf.Deg2Rad;
			return new Quaternion(Mathf.Sin(half), 0f, 0f, Mathf.Cos(half));
		}

		internal static Quaternion AboutY(float degrees)
		{
			float half = 0.5f * degrees * Mathf.Deg2Rad;
			return new Quaternion(0f, Mathf.Sin(half), 0f, Mathf.Cos(half));
		}

		/// <summary>A box, faces out, flat shaded (each face its own corners).</summary>
		internal static void Box(Piece p, Paint paint, Vector3 centre, Vector3 size, Quaternion rotation)
		{
			Vector3 h = 0.5f * size;
			Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
			for (int ax = 0; ax < 3; ax++)
			{
				Vector3 n = axes[ax];
				Vector3 u = axes[(ax + 1) % 3];
				Vector3 v = axes[(ax + 2) % 3];
				float hn = Vector3.Dot(h, n), hu = Vector3.Dot(h, u), hv = Vector3.Dot(h, v);
				foreach (float sgn in new[] { -1f, 1f })
				{
					int[] idx = new int[4];
					for (int k = 0; k < 4; k++)
					{
						float su = (k & 1) == 0 ? -1f : 1f;
						float sv = (k & 2) == 0 ? -1f : 1f;
						Vector3 local = n * sgn * hn + u * su * hu + v * sv * hv;
						Vector3 world = centre + rotation * local;
						idx[k] = p.Vertex(world, new Vector2(su * hu / Shape.TextureSpan, sv * hv / Shape.TextureSpan));
					}
					Vector3 facing = rotation * (n * sgn);
					p.Tri(paint, idx[0], idx[1], idx[3], facing);
					p.Tri(paint, idx[0], idx[3], idx[2], facing);
				}
			}
		}

		/// <summary>A cylinder from <paramref name="a"/> to <paramref name="b"/>, sides smooth, ends capped.</summary>
		internal static void Cylinder(Piece p, Paint paint, Vector3 a, Vector3 b, float radius, int sides)
		{
			Vector3 axis = b - a;
			if (axis.sqrMagnitude < 1e-10f)
			{
				return;
			}
			Vector3 dir = axis.normalized;
			Vector3 u = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
			Vector3 v = Vector3.Cross(dir, u);
			int[] ra = new int[sides + 1];
			int[] rb = new int[sides + 1];
			for (int k = 0; k <= sides; k++)
			{
				float ang = 2f * Mathf.PI * k / sides;
				Vector3 off = (Mathf.Cos(ang) * u + Mathf.Sin(ang) * v) * radius;
				ra[k] = p.Vertex(a + off, new Vector2((float)k / sides, 0f));
				rb[k] = p.Vertex(b + off, new Vector2((float)k / sides, 1f));
			}
			for (int k = 0; k < sides; k++)
			{
				float ang = 2f * Mathf.PI * (k + 0.5f) / sides;
				Vector3 outward = Mathf.Cos(ang) * u + Mathf.Sin(ang) * v;
				p.Tri(paint, ra[k], ra[k + 1], rb[k + 1], outward);
				p.Tri(paint, ra[k], rb[k + 1], rb[k], outward);
			}
			// Caps with their own corners, so they shade flat.
			foreach (bool end in new[] { false, true })
			{
				Vector3 c = end ? b : a;
				Vector3 facing = end ? dir : -dir;
				int ci = p.Vertex(c, Vector2.zero);
				int prev = -1;
				for (int k = 0; k <= sides; k++)
				{
					float ang = 2f * Mathf.PI * k / sides;
					int vi = p.Vertex(c + (Mathf.Cos(ang) * u + Mathf.Sin(ang) * v) * radius, Vector2.zero);
					if (k > 0)
					{
						p.Tri(paint, ci, prev, vi, facing);
					}
					prev = vi;
				}
			}
		}
	}
}
