using UnityEngine;

namespace HalfGripHelicopter
{
	/// <summary>
	/// A small pixel canvas and the mod's pictures, drawn in code from flat palette colours so they match the game's pixel art (hard edges,
	/// no blending) and need no files. Row 0 is the bottom, as in a Unity texture.
	/// </summary>
	internal sealed class Canvas
	{
		internal readonly int Width;
		internal readonly int Height;
		internal readonly Color32[] Pixels;

		internal Canvas(int width, int height)
		{
			Width = width;
			Height = height;
			Pixels = new Color32[width * height];
		}

		internal void Clear()
		{
			for (int i = 0; i < Pixels.Length; i++)
			{
				Pixels[i] = default;
			}
		}

		internal void Set(int x, int y, Color32 c)
		{
			if (x >= 0 && y >= 0 && x < Width && y < Height)
			{
				Pixels[y * Width + x] = c;
			}
		}

		internal Color32 Get(int x, int y)
		{
			return x >= 0 && y >= 0 && x < Width && y < Height ? Pixels[y * Width + x] : default;
		}

		internal void Rect(int x0, int y0, int w, int h, Color32 c)
		{
			for (int y = y0; y < y0 + h; y++)
			{
				for (int x = x0; x < x0 + w; x++)
				{
					Set(x, y, c);
				}
			}
		}

		/// <summary>A filled ellipse inside the box, tested at pixel centres.</summary>
		internal void Ellipse(int x0, int y0, int w, int h, Color32 c)
		{
			float rx = w * 0.5f;
			float ry = h * 0.5f;
			for (int y = y0; y < y0 + h; y++)
			{
				for (int x = x0; x < x0 + w; x++)
				{
					float dx = (x + 0.5f - x0 - rx) / rx;
					float dy = (y + 0.5f - y0 - ry) / ry;
					if (dx * dx + dy * dy <= 1f)
					{
						Set(x, y, c);
					}
				}
			}
		}

		/// <summary>A line <paramref name="thick"/> pixels across (Bresenham, a square brush).</summary>
		internal void Line(int x0, int y0, int x1, int y1, int thick, Color32 c)
		{
			int dx = Mathf.Abs(x1 - x0);
			int dy = -Mathf.Abs(y1 - y0);
			int sx = x0 < x1 ? 1 : -1;
			int sy = y0 < y1 ? 1 : -1;
			int err = dx + dy;
			int lo = -(thick - 1) / 2;
			while (true)
			{
				Rect(x0 + lo, y0 + lo, thick, thick, c);
				if (x0 == x1 && y0 == y1)
				{
					break;
				}
				int e2 = 2 * err;
				if (e2 >= dy)
				{
					err += dy;
					x0 += sx;
				}
				if (e2 <= dx)
				{
					err += dx;
					y0 += sy;
				}
			}
		}

		/// <summary>
		/// Shades what is drawn the way the game's dash parts are lit, from the top left: a light pixel where the one up-left of it is
		/// empty, a dark one where the one down-right is, and a black outline round the shape.
		/// </summary>
		internal void Shade(Color32 light, Color32 dark, Color32 outline)
		{
			Color32[] src = (Color32[])Pixels.Clone();
			bool Filled(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && src[y * Width + x].a > 0;
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					if (Filled(x, y))
					{
						if (!Filled(x - 1, y + 1) || !Filled(x, y + 1))
						{
							Pixels[y * Width + x] = Mix(src[y * Width + x], light);
						}
						else if (!Filled(x + 1, y - 1) || !Filled(x, y - 1))
						{
							Pixels[y * Width + x] = Mix(src[y * Width + x], dark);
						}
						continue;
					}
					if (Filled(x - 1, y) || Filled(x + 1, y) || Filled(x, y - 1) || Filled(x, y + 1))
					{
						Pixels[y * Width + x] = outline;
					}
				}
			}
		}

		/// <summary>Half way between two palette colours, snapped to the nearer of the two plus the step (keeps a flat look).</summary>
		private static Color32 Mix(Color32 a, Color32 b)
		{
			return new Color32((byte)((a.r + b.r) / 2), (byte)((a.g + b.g) / 2), (byte)((a.b + b.b) / 2), 255);
		}

		internal Texture2D Texture(string name)
		{
			Texture2D t = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
			t.name = name;
			t.filterMode = FilterMode.Point;
			t.wrapMode = TextureWrapMode.Clamp;
			t.hideFlags = HideFlags.HideAndDontSave;
			t.SetPixels32(Pixels);
			t.Apply(false, false);
			return t;
		}
	}

	/// <summary>The mod's pictures: the shop icon and the cockpit's parts.</summary>
	internal static class Art
	{
		internal static readonly Color32 Black = new Color32(20, 20, 24, 255);
		internal static readonly Color32 Dark = new Color32(48, 50, 56, 255);
		internal static readonly Color32 Grey = new Color32(96, 100, 108, 255);
		internal static readonly Color32 Light = new Color32(170, 174, 182, 255);
		internal static readonly Color32 White = new Color32(236, 236, 230, 255);
		internal static readonly Color32 Red = new Color32(196, 44, 40, 255);
		internal static readonly Color32 Yellow = new Color32(232, 184, 40, 255);
		internal static readonly Color32 Olive = new Color32(84, 100, 60, 255);
		internal static readonly Color32 Glass = new Color32(120, 176, 200, 255);
		internal static readonly Color32 Green = new Color32(72, 180, 80, 255);

		/// <summary>
		/// The shop icon: a helicopter from the side in olive, filling a <paramref name="w"/> by <paramref name="h"/> card picture (the
		/// chassis item it copies decides the size, so it lines up like the game's own body pictures).
		/// </summary>
		internal static Canvas Icon(int w, int h)
		{
			Canvas c = new Canvas(w, h);
			// Proportions as shares of the picture, so any card size gets the same helicopter.
			int bodyW = Mathf.Max(6, Mathf.RoundToInt(w * 0.48f));
			int bodyH = Mathf.Max(4, Mathf.RoundToInt(h * 0.62f));
			int bodyX = Mathf.RoundToInt(w * 0.5f);
			int skidY = 1;
			int bodyY = skidY + Mathf.Max(2, Mathf.RoundToInt(h * 0.15f));
			int mastTop = Mathf.Min(h - 2, bodyY + bodyH + Mathf.Max(1, h / 10));
			// Tail boom to the left, tail fin at its end.
			int boomY = bodyY + bodyH / 2;
			int boomH = Mathf.Max(1, bodyH / 4);
			c.Rect(2, boomY, bodyX - 2, boomH, Olive);
			c.Rect(1, boomY, Mathf.Max(2, w / 20), Mathf.Max(3, bodyH * 3 / 4), Olive);
			// Body and cabin glass.
			c.Ellipse(bodyX - bodyW / 4, bodyY, bodyW, bodyH, Olive);
			c.Ellipse(bodyX + bodyW / 4, bodyY + bodyH / 3, bodyW / 2, bodyH * 2 / 3, Glass);
			c.Shade(Light, Dark, Black);
			// Mast and the main rotor across the top, the skids under it.
			int mastX = bodyX + bodyW / 4;
			c.Rect(mastX, bodyY + bodyH - 1, 1, mastTop - (bodyY + bodyH - 1), Black);
			c.Rect(Mathf.RoundToInt(w * 0.12f), mastTop, Mathf.RoundToInt(w * 0.8f), 1, Black);
			c.Rect(bodyX - bodyW / 4 + 1, skidY, bodyW, 1, Black);
			c.Rect(bodyX, skidY, 1, bodyY - skidY, Black);
			c.Rect(bodyX + bodyW / 2, skidY, 1, bodyY - skidY, Black);
			return c;
		}

		/// <summary>
		/// The stick's grip, drawn big like the car's shifter knob: a black cyclic grip with finger ridges and a trigger at the front, a hat
		/// switch on top and a collar at the bottom where the shaft goes in (the sprite's pivot). The button on its left turns red while the
		/// stick is held where it is (locked).
		/// </summary>
		internal static Canvas Grip(bool locked)
		{
			Canvas c = new Canvas(28, 40);
			c.Rect(10, 1, 8, 8, Dark);
			c.Ellipse(6, 6, 16, 26, Dark);
			c.Rect(6, 12, 16, 14, Dark);
			c.Ellipse(4, 25, 20, 14, Dark);
			c.Rect(21, 17, 4, 6, Grey);
			c.Shade(Grey, Black, Black);
			// The collar.
			c.Rect(10, 1, 8, 3, Grey);
			c.Rect(10, 4, 8, 1, Black);
			// Finger ridges at the front, a highlight down the back.
			foreach (int y in new[] { 9, 13, 17 })
			{
				c.Rect(18, y, 3, 1, Black);
			}
			c.Rect(8, 10, 1, 14, Grey);
			// The hat switch and the hold button.
			c.Rect(12, 34, 4, 3, Grey);
			c.Set(13, 35, Black);
			c.Set(14, 35, Black);
			c.Rect(7, 30, 3, 3, locked ? Red : Grey);
			c.Rect(7, 29, 3, 1, Black);
			return c;
		}

		/// <summary>The stick's shaft, <paramref name="thick"/> pixels across, from (x0, y0) to (x1, y1), shaded like the dash parts.</summary>
		internal static void Shaft(Canvas c, int x0, int y0, int x1, int y1, int thick)
		{
			c.Clear();
			c.Line(x0, y0, x1, y1, thick, Grey);
			c.Shade(Light, Dark, Black);
		}

		/// <summary>The rubber boot where the stick comes up through the floor (below the screen's edge when that is known).</summary>
		internal static Canvas Boot()
		{
			Canvas c = new Canvas(28, 16);
			c.Ellipse(1, 1, 26, 14, Grey);
			c.Shade(Light, Dark, Black);
			c.Ellipse(6, 4, 16, 9, Black);
			c.Ellipse(7, 5, 14, 8, Dark);
			c.Ellipse(10, 8, 8, 5, Black);
			return c;
		}

		/// <summary>Plastic: <paramref name="a"/> with a scattering of <paramref name="b"/>, the same every time (a hash per pixel).</summary>
		internal static void Speckle(Canvas c, Color32 a, Color32 b, int seed)
		{
			for (int y = 0; y < c.Height; y++)
			{
				for (int x = 0; x < c.Width; x++)
				{
					Color32 p = c.Get(x, y);
					if (p.a == 0 || p.r != a.r || p.g != a.g || p.b != a.b)
					{
						continue;
					}
					uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791);
					h ^= h >> 13;
					h *= 0x5bd1e995;
					h ^= h >> 15;
					if ((h & 7) == 0)
					{
						c.Set(x, y, b);
					}
				}
			}
		}

		internal static readonly Color32 Plate = new Color32(82, 84, 90, 255);
		internal static readonly Color32 PlateDot = new Color32(66, 68, 74, 255);

		/// <summary>
		/// The levers' gate, after the car's H-shifter plate: a speckled grey plate <paramref name="bottom"/> wide at the front, on a dark
		/// front face <paramref name="face"/> rows high, <paramref name="h"/> rows in all. Its sides lean <paramref name="slopeLeft"/> and
		/// <paramref name="slopeRight"/> pixels across per row up (the console's lines as seen from the seat, from
		/// <see cref="Airframe.FitPanel"/>), the slots between them, <paramref name="gap"/> either side of the middle at row
		/// <paramref name="slot0"/>, running <paramref name="travel"/> rows. Ticks at the quarters; the collective's (left) hover mark half
		/// way in yellow, the throttle's full mark in red. The front edge's middle is the canvas's middle column.
		/// </summary>
		/// <summary>
		/// The MiniMap GPS's mounting plate, for a casing <paramref name="w"/> by <paramref name="h"/> pixels: the gate's speckled grey
		/// plate <paramref name="rim"/> pixels round it, a dark seat just round the casing (its shadow), a dark front face
		/// <paramref name="face"/> rows high below with a lit front edge, and a screw at each corner. A 1-pixel outline all round; the
		/// casing's bottom-left corner falls at (rim + 1, rim + face + 1).
		/// </summary>
		/// <summary>
		/// The GPS's mounting plate round a w x h casing. <paramref name="hole"/> (w*h, rows from the bottom) marks the casing's own pixels:
		/// they are left see-through, so the plate can never cover the GPS whatever order the game draws them in (0.2.2 and 0.2.3 drew the
		/// plate over it). Null cuts out the whole w x h box. The seat's shadow follows the hole's edge.
		/// </summary>
		internal static Canvas Mount(int w, int h, int rim, int face, bool[] hole = null)
		{
			Canvas c = new Canvas(w + 2 * rim + 2, h + 2 * rim + face + 2);
			int x0 = 1, x1 = c.Width - 1, y0 = 1, y1 = c.Height - 1;
			c.Rect(x0, y0, x1 - x0, face, Dark);
			c.Rect(x0, y0 + face, x1 - x0, y1 - y0 - face, Plate);
			Speckle(c, Plate, PlateDot, 9);
			c.Shade(Light, Black, Black);
			c.Rect(x0 + 1, y0 + face, x1 - x0 - 2, 1, Light);
			c.Rect(x0 + 1, y0 + face - 1, x1 - x0 - 2, 1, Black);
			int cx = rim + 1, cy = rim + face + 1;
			bool Hole(int x, int y)
			{
				int i = x - cx, j = y - cy;
				return i >= 0 && j >= 0 && i < w && j < h && (hole == null || hole[j * w + i]);
			}
			// The casing's seat: a line round it, its shadow on the plate (black below and right, dark above and left).
			Color32[] seat = new Color32[c.Pixels.Length];
			bool[] mark = new bool[c.Pixels.Length];
			for (int y = cy - 1; y <= cy + h; y++)
			{
				for (int x = cx - 1; x <= cx + w; x++)
				{
					if (Hole(x, y))
					{
						continue;
					}
					if (Hole(x, y + 1) || Hole(x - 1, y) || Hole(x - 1, y + 1))
					{
						seat[y * c.Width + x] = Black;
						mark[y * c.Width + x] = true;
					}
					else if (Hole(x, y - 1) || Hole(x + 1, y) || Hole(x + 1, y - 1))
					{
						seat[y * c.Width + x] = Dark;
						mark[y * c.Width + x] = true;
					}
				}
			}
			for (int y = cy; y < cy + h; y++)
			{
				for (int x = cx; x < cx + w; x++)
				{
					if (Hole(x, y))
					{
						c.Set(x, y, default);
					}
				}
			}
			for (int i = 0; i < mark.Length; i++)
			{
				if (mark[i])
				{
					c.Pixels[i] = seat[i];
				}
			}
			foreach ((int x, int y) in new[] { (x0 + 2, y1 - 2), (x1 - 3, y1 - 2), (x0 + 2, y0 + face + 2), (x1 - 3, y0 + face + 2) })
			{
				c.Set(x, y, Light);
				c.Set(x, y - 1, Black);
			}
			return c;
		}

		internal static Canvas Gate(int bottom, int h, int face, int gap, int slot0, int travel, float slopeLeft, float slopeRight)
		{
			int extra = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(slopeLeft), Mathf.Abs(slopeRight)) * h) + 2;
			Canvas c = new Canvas(bottom + 2 * extra + 2, h + 2);
			int mx = c.Width / 2;
			float left0 = mx - bottom * 0.5f, right0 = mx + bottom * 0.5f;
			int Left(int y) => Mathf.RoundToInt(left0 + slopeLeft * (y - face - 1));
			int Right(int y) => Mathf.RoundToInt(right0 + slopeRight * (y - face - 1));
			for (int y = 1; y <= h; y++)
			{
				c.Rect(Left(y), y, Right(y) - Left(y), 1, y <= face ? Dark : Plate);
			}
			Speckle(c, Plate, PlateDot, 7);
			c.Shade(Light, Black, Black);
			// The front edge catches the light.
			c.Rect(Left(face + 1) + 1, face + 1, Right(face + 1) - Left(face + 1) - 2, 1, Light);
			c.Rect(Left(face) + 1, face, Right(face) - Left(face) - 2, 1, Black);
			// Screws at the back corners.
			foreach (int x in new[] { Left(h - 3) + 3, Right(h - 3) - 4 })
			{
				c.Set(x, h - 3, Light);
				c.Set(x, h - 4, Black);
			}
			foreach (int side in new[] { -1, 1 })
			{
				float x0 = mx + side * gap;
				float t = (x0 - left0) / Mathf.Max(1f, right0 - left0);
				float slope = Mathf.Lerp(slopeLeft, slopeRight, t);
				for (int y = slot0 - 3; y < slot0 + travel + 3; y++)
				{
					int x = Mathf.RoundToInt(x0 + slope * (y - slot0));
					bool rim = y == slot0 - 3 || y == slot0 + travel + 2;
					c.Rect(x - 3, y, 6, 1, Dark);
					if (!rim)
					{
						c.Rect(x - 2, y, 4, 1, Black);
					}
				}
				for (int q = 0; q <= 4; q++)
				{
					int y = slot0 + travel * q / 4;
					int x = Mathf.RoundToInt(x0 + slope * (y - slot0));
					Color32 mark = side < 0 && q == 2 ? Yellow : side > 0 && q == 4 ? Red : Light;
					int len = mark.r == Light.r ? 2 : 3;
					c.Rect(x - 4 - len, y, len, 1, mark);
					c.Rect(x + 4, y, len, 1, mark);
				}
			}
			return c;
		}

		/// <summary>The slope (pixels across per row up) of a slot <paramref name="offset"/> pixels from the plate's middle.</summary>
		internal static float SlotSlope(int bottom, int offset, float slopeLeft, float slopeRight)
		{
			return Mathf.Lerp(slopeLeft, slopeRight, (offset + bottom * 0.5f) / Mathf.Max(1f, bottom));
		}

		/// <summary>Height of a lever's shaft in pixels, from the collar in the slot to the knob.</summary>
		internal const int LeverShaft = 24;

		/// <summary>How far a lever's shaft leans out from its slot, in pixels, so the knob stands beside the slot and its path stays in
		/// sight.</summary>
		internal const int LeverLean = 10;

		/// <summary>
		/// A lever standing up out of its slot, leaning outwards (<paramref name="side"/> -1 to the left, 1 to the right): a collar in the
		/// slot (the pivot, at row 2 of the middle column), a shaft and the knob: the collective's a black head with a yellow and black
		/// band and a red button, the throttle's a red twist grip.
		/// </summary>
		internal static Canvas Lever(bool collective, int side)
		{
			Canvas c = new Canvas(2 * LeverLean + 24, LeverShaft + 20);
			int mx = c.Width / 2;
			int kx = mx + side * LeverLean;
			c.Line(mx, 3, kx, LeverShaft + 3, 4, Grey);
			int k = LeverShaft + 2;
			if (collective)
			{
				c.Rect(kx - 8, k, 16, 14, Dark);
				c.Rect(kx - 7, k + 14, 14, 2, Dark);
			}
			else
			{
				c.Ellipse(kx - 8, k, 16, 16, Red);
			}
			c.Ellipse(mx - 5, 0, 10, 5, Dark);
			c.Shade(Light, Black, Black);
			if (collective)
			{
				for (int x = kx - 7; x < kx + 7; x++)
				{
					c.Rect(x, k + 4, 1, 3, ((x - kx + 16) / 2) % 2 == 0 ? Yellow : Black);
				}
				c.Rect(kx + 2, k + 10, 3, 3, Red);
				c.Rect(kx - 5, k + 10, 4, 2, Grey);
			}
			else
			{
				for (int x = kx - 5; x <= kx + 5; x += 3)
				{
					c.Rect(x, k + 3, 1, 10, new Color32(120, 30, 28, 255));
				}
			}
			return c;
		}
	}
}
