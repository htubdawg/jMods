using System.Collections.Generic;
using UnityEngine;

namespace HalfGripStereo
{
	/// <summary>
	/// The stereo's pixel art, drawn in code in the manner of the cockpit (chunky flat colours, speckled plastic, hard edges): the head
	/// unit that sits left of the steering wheel, its glowing display, the CD wallet on the sun visor. Pixel arrays
	/// only (rows from the bottom, as Unity keeps them), so <c>tools/preview</c> draws the same pictures outside the game.
	/// </summary>
	internal static class Art
	{
		internal static readonly Color32 Clear = new Color32(0, 0, 0, 0);

		/// <summary>A picture being drawn: pixels with rows from the bottom.</summary>
		internal sealed class Canvas
		{
			public readonly int W;
			public readonly int H;
			public readonly Color32[] Px;

			public Canvas(int w, int h)
			{
				W = w;
				H = h;
				Px = new Color32[w * h];
			}

			public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

			public void Set(int x, int y, Color32 c)
			{
				if (In(x, y))
				{
					Px[y * W + x] = c;
				}
			}

			public Color32 Get(int x, int y) => In(x, y) ? Px[y * W + x] : Clear;

			/// <summary>Fills x0..x1, y0..y1, both ends included.</summary>
			public void Rect(int x0, int y0, int x1, int y1, Color32 c)
			{
				for (int y = y0; y <= y1; y++)
				{
					for (int x = x0; x <= x1; x++)
					{
						Set(x, y, c);
					}
				}
			}

			/// <summary>A filled disc round the middle of pixel (cx, cy), or between pixels when the centre ends in .5.</summary>
			public void Disc(float cx, float cy, float r, Color32 c)
			{
				for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
				{
					for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
					{
						float dx = x - cx, dy = y - cy;
						if (dx * dx + dy * dy <= r * r)
						{
							Set(x, y, c);
						}
					}
				}
			}

			/// <summary>Varies every drawn pixel a little, hard steps only, the speckle of the dash's plastic.</summary>
			public void Speckle(int x0, int y0, int x1, int y1, int amount, int seed)
			{
				for (int y = y0; y <= y1; y++)
				{
					for (int x = x0; x <= x1; x++)
					{
						if (!In(x, y) || Px[y * W + x].a == 0)
						{
							continue;
						}
						int h = Hash(x, y, seed) % 7;
						int d = h == 0 ? amount : h == 1 ? -amount : 0;
						Px[y * W + x] = Shade(Px[y * W + x], d);
					}
				}
			}

			/// <summary>Draws another picture onto this one, its bottom left at (x, y), skipping its clear pixels.</summary>
			public void Paste(Canvas other, int x, int y)
			{
				for (int j = 0; j < other.H; j++)
				{
					for (int i = 0; i < other.W; i++)
					{
						Color32 c = other.Px[j * other.W + i];
						if (c.a > 0)
						{
							Set(x + i, y + j, c);
						}
					}
				}
			}
		}

		internal static int Hash(int x, int y, int seed)
		{
			unchecked
			{
				uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
				h = (h ^ (h >> 13)) * 1274126177;
				return (int)((h ^ (h >> 16)) & 0x7fffffff);
			}
		}

		internal static Color32 Shade(Color32 c, int d) => new Color32((byte)Mathf.Clamp(c.r + d, 0, 255), (byte)Mathf.Clamp(c.g + d, 0, 255), (byte)Mathf.Clamp(c.b + d, 0, 255), c.a);

		// ---------------------------------------------------------------------------------------------------------------------------
		// The 3x5 font, for the display and the labels. Lower case is drawn as upper case; anything unknown as '?'.

		private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
		{
			{ 'A', "010101111101101" }, { 'B', "110101110101110" }, { 'C', "011100100100011" }, { 'D', "110101101101110" },
			{ 'E', "111100110100111" }, { 'F', "111100110100100" }, { 'G', "011100101101011" }, { 'H', "101101111101101" },
			{ 'I', "111010010010111" }, { 'J', "001001001101010" }, { 'K', "101101110101101" }, { 'L', "100100100100111" },
			{ 'M', "101111111101101" }, { 'N', "110101101101101" }, { 'O', "010101101101010" }, { 'P', "110101110100100" },
			{ 'Q', "010101101110011" }, { 'R', "110101110101101" }, { 'S', "011100010001110" }, { 'T', "111010010010010" },
			{ 'U', "101101101101111" }, { 'V', "101101101101010" }, { 'W', "101101111111101" }, { 'X', "101101010101101" },
			{ 'Y', "101101010010010" }, { 'Z', "111001010100111" },
			{ '0', "111101101101111" }, { '1', "010110010010111" }, { '2', "110001010100111" }, { '3', "110001010001110" },
			{ '4', "101101111001001" }, { '5', "111100110001110" }, { '6', "011100111101111" }, { '7', "111001010010010" },
			{ '8', "111101111101111" }, { '9', "111101111001110" },
			{ ' ', "000000000000000" }, { ':', "000010000010000" }, { '-', "000000111000000" }, { '.', "000000000000010" },
			{ ',', "000000000010100" }, { '/', "001001010100100" }, { '!', "010010010000010" }, { '?', "110001010000010" },
			{ '\'', "010010000000000" }, { '(', "001010010010001" }, { ')', "100010010010100" }, { '&', "010101010101011" },
			{ '_', "000000000000111" }, { '+', "000010111010000" }, { '#', "101111101111101" }, { '=', "000111000111000" },
			{ '*', "000101010101000" }, { '[', "011010010010011" }, { ']', "110010010010110" },
			// The display's own signs: a full block (volume bar), play, pause.
			{ '█', "111111111111111" }, { '▶', "100110111110100" }, { '‖', "000101101101000" },
		};

		internal const int GlyphWidth = 3;
		internal const int GlyphHeight = 5;
		/// <summary>Pixels per letter along a line: the glyph and one pixel of space.</summary>
		internal const int Advance = GlyphWidth + 1;

		internal static bool Known(char c) => Glyphs.ContainsKey(char.ToUpperInvariant(c));

		/// <summary>Writes <paramref name="text"/> with its first letter's bottom left at (x, y); letters off the canvas are cut.</summary>
		internal static void Text(Canvas c, string text, int x, int y, Color32 colour)
		{
			for (int i = 0; i < text.Length; i++)
			{
				char ch = char.ToUpperInvariant(text[i]);
				if (!Glyphs.TryGetValue(ch, out string g))
				{
					g = Glyphs['?'];
				}
				for (int row = 0; row < GlyphHeight; row++)
				{
					for (int col = 0; col < GlyphWidth; col++)
					{
						if (g[row * GlyphWidth + col] == '1')
						{
							c.Set(x + i * Advance + col, y + GlyphHeight - 1 - row, colour);
						}
					}
				}
			}
		}

		// ---------------------------------------------------------------------------------------------------------------------------
		// The head unit: a black single-piece car stereo, a little taller than one DIN, seen face on like the dash's dials.

		private static readonly Color32 Outline = new Color32(12, 12, 14, 255);
		private static readonly Color32 Body = new Color32(40, 40, 45, 255);
		private static readonly Color32 BodyTop = new Color32(66, 66, 72, 255);
		private static readonly Color32 BodyLow = new Color32(28, 28, 32, 255);
		private static readonly Color32 Chrome = new Color32(150, 150, 158, 255);
		private static readonly Color32 ChromeDark = new Color32(92, 92, 100, 255);
		private static readonly Color32 Slot = new Color32(6, 6, 8, 255);
		private static readonly Color32 KnobRim = new Color32(100, 100, 108, 255);
		private static readonly Color32 KnobFace = new Color32(58, 58, 64, 255);
		private static readonly Color32 KnobDark = new Color32(30, 30, 34, 255);
		private static readonly Color32 KnobMark = new Color32(220, 220, 220, 255);
		private static readonly Color32 Key = new Color32(64, 64, 70, 255);
		private static readonly Color32 KeyTop = new Color32(98, 98, 106, 255);
		private static readonly Color32 KeyLow = new Color32(22, 22, 26, 255);
		private static readonly Color32 KeyPressed = new Color32(44, 44, 49, 255);
		private static readonly Color32 KeySign = new Color32(190, 190, 198, 255);
		private static readonly Color32 LedOn = new Color32(236, 52, 36, 255);
		private static readonly Color32 LedOff = new Color32(72, 22, 20, 255);
		private static readonly Color32 Badge = new Color32(176, 150, 96, 255);

		/// <summary>The head unit's size in dash pixels.</summary>
		internal const int UnitWidth = 64;
		internal const int UnitHeight = 28;

		/// <summary>The display's window on the unit (inside the chrome frame), in dash pixels; the display itself is drawn at <see cref="LcdDetail"/> times this.</summary>
		internal static readonly RectInt LcdWindow = new RectInt(21, 10, 32, 10);
		internal const int LcdDetail = 2;

		/// <summary>The parts of the unit the hand can press.</summary>
		internal enum Part { Knob, Power, Prev, Play, Next, Eject }

		internal static readonly Part[] Parts = { Part.Knob, Part.Power, Part.Prev, Part.Play, Part.Next, Part.Eject };

		/// <summary>Where each part is on the unit, in dash pixels (the knob's box round its circle).</summary>
		internal static RectInt Place(Part part)
		{
			switch (part)
			{
				case Part.Knob: return new RectInt(3, 5, 15, 15);
				case Part.Power: return new RectInt(20, 2, 7, 5);
				case Part.Prev: return new RectInt(29, 2, 7, 5);
				case Part.Play: return new RectInt(38, 2, 7, 5);
				case Part.Next: return new RectInt(47, 2, 7, 5);
				default: return new RectInt(56, 21, 6, 4);
			}
		}

		/// <summary>
		/// The head unit's casing (everything but the display, which is its own glowing sprite over the window): <paramref name="pressed"/>
		/// is drawn pushed in, <paramref name="power"/> lights the power key's red light, <paramref name="knobTurn"/> (0 to 1) turns the
		/// volume knob's mark from 7 o'clock to 5 o'clock.
		/// </summary>
		/// <remarks>The flat layout; in the cockpit the unit is drawn on its <see cref="Face"/>, from these places.</remarks>
		internal static Canvas HeadUnit(Part? pressed, bool power, float knobTurn)
		{
			int w = UnitWidth, h = UnitHeight;
			Canvas c = new Canvas(w, h);
			c.Rect(0, 0, w - 1, h - 1, Outline);
			c.Rect(1, 1, w - 2, h - 2, Body);
			// A lighter lip along the top (light from above, as on the dash's parts), a darker one along the bottom.
			c.Rect(1, h - 3, w - 2, h - 2, BodyTop);
			c.Rect(1, 1, w - 2, 1, BodyLow);
			c.Speckle(1, 1, w - 2, h - 2, 5, 11);

			// The CD slot across the top, with a highlight under its lip, and the eject key at its end.
			c.Rect(20, 23, 53, 23, Slot);
			c.Rect(20, 22, 53, 22, BodyTop);
			Key3(c, Place(Part.Eject), pressed == Part.Eject);
			RectInt e = Place(Part.Eject);
			int ex = e.x + 1, ey = e.y + (pressed == Part.Eject ? 0 : 1);
			c.Set(ex + 1, ey + 2, KeySign);
			c.Rect(ex, ey + 1, ex + 2, ey + 1, KeySign);
			c.Rect(ex, ey, ex + 2, ey, KeySign);

			// The display's chrome frame; the window inside it is left dark, the display sprite covers it.
			RectInt l = LcdWindow;
			c.Rect(l.x - 2, l.y - 2, l.xMax + 1, l.yMax + 1, ChromeDark);
			c.Rect(l.x - 2, l.yMax + 1, l.xMax + 1, l.yMax + 1, Chrome);
			c.Rect(l.xMax + 1, l.y - 1, l.xMax + 1, l.yMax + 1, Chrome);
			c.Rect(l.x - 1, l.y - 1, l.xMax, l.yMax, Outline);
			c.Rect(l.x, l.y, l.xMax - 1, l.yMax - 1, new Color32(20, 12, 6, 255));

			// The volume knob: a chrome rim, a dark groove, a knurled face, a white mark that turns with the volume.
			RectInt k = Place(Part.Knob);
			float cx = k.x + (k.width - 1) * 0.5f, cy = k.y + (k.height - 1) * 0.5f;
			bool knobIn = pressed == Part.Knob;
			c.Disc(cx, cy, 7.4f, Outline);
			c.Disc(cx, cy, 6.6f, KnobRim);
			c.Disc(cx, cy, 5.6f, KnobDark);
			c.Disc(cx, cy + (knobIn ? -0.5f : 0f), 4.8f, knobIn ? KnobDark : KnobFace);
			for (int a = 0; a < 12; a++)
			{
				float ang = a * Mathf.PI / 6f;
				c.Set(Mathf.RoundToInt(cx + Mathf.Cos(ang) * 4.6f), Mathf.RoundToInt(cy + Mathf.Sin(ang) * 4.6f), KnobRim);
			}
			c.Disc(cx - 1.5f, cy + 1.5f, 1.1f, Shade(KnobFace, 26));
			float turn = Mathf.Lerp(Mathf.PI * 1.25f, -Mathf.PI * 0.25f, Mathf.Clamp01(knobTurn));
			for (float r = 1.5f; r <= 4.2f; r += 0.5f)
			{
				c.Set(Mathf.RoundToInt(cx + Mathf.Cos(turn) * r), Mathf.RoundToInt(cy + Mathf.Sin(turn) * r), KnobMark);
			}
			// Tick marks round the knob for the volume's range.
			for (int t = 0; t <= 4; t++)
			{
				float ang = Mathf.Lerp(Mathf.PI * 1.25f, -Mathf.PI * 0.25f, t / 4f);
				c.Set(Mathf.RoundToInt(cx + Mathf.Cos(ang) * 8.2f), Mathf.RoundToInt(cy + Mathf.Sin(ang) * 8.2f), ChromeDark);
			}

			// The keys under the display.
			foreach (Part p in new[] { Part.Power, Part.Prev, Part.Play, Part.Next })
			{
				RectInt r = Place(p);
				bool down = pressed == p;
				Key3(c, r, down);
				int sx = r.x + 2, sy = r.y + (down ? 1 : 2);
				switch (p)
				{
					case Part.Power:
						// The power sign (a ring open at the top, a stroke through it) beside a red light that is lit while on.
						c.Rect(sx - 1, sy - 1, sx - 1, sy, KeySign);
						c.Rect(sx + 1, sy - 1, sx + 1, sy, KeySign);
						c.Set(sx, sy - 1, KeySign);
						c.Rect(sx, sy, sx, sy + 1, KeySign);
						c.Set(sx + 3, sy + 1, power ? LedOn : LedOff);
						break;
					case Part.Prev:
						c.Rect(sx - 1, sy - 1, sx - 1, sy + 1, KeySign);
						c.Set(sx, sy, KeySign);
						c.Rect(sx + 1, sy - 1, sx + 1, sy + 1, KeySign);
						c.Set(sx + 2, sy, KeySign);
						c.Rect(sx + 3, sy - 1, sx + 3, sy + 1, KeySign);
						c.Set(sx + 2, sy, KeySign);
						break;
					case Part.Play:
						c.Rect(sx - 1, sy - 1, sx - 1, sy + 1, KeySign);
						c.Set(sx, sy, KeySign);
						c.Rect(sx + 2, sy - 1, sx + 2, sy + 1, KeySign);
						c.Rect(sx + 3, sy - 1, sx + 3, sy + 1, KeySign);
						break;
					case Part.Next:
						c.Rect(sx - 1, sy - 1, sx - 1, sy + 1, KeySign);
						c.Set(sx, sy, KeySign);
						c.Rect(sx + 1, sy - 1, sx + 1, sy + 1, KeySign);
						c.Set(sx + 2, sy, KeySign);
						c.Rect(sx + 3, sy - 1, sx + 3, sy + 1, KeySign);
						break;
				}
			}
			// Prev is the mirror of next: |<< rather than >>|.
			Mirror(c, Place(Part.Prev));

			// A small gold "CD" badge in the right hand corner, the way car stereos wore one.
			Text(c, "CD", 56, 11, Badge);
			c.Rect(56, 9, 62, 9, ChromeDark);
			return c;
		}

		/// <summary>A key: a raised block with a light top edge and a dark bottom edge, or pushed in, flat and darker.</summary>
		private static void Key3(Canvas c, RectInt r, bool down)
		{
			c.Rect(r.x, r.y, r.xMax - 1, r.yMax - 1, KeyLow);
			if (down)
			{
				c.Rect(r.x + 1, r.y, r.xMax - 2, r.yMax - 2, KeyPressed);
				return;
			}
			c.Rect(r.x, r.y + 1, r.xMax - 1, r.yMax - 1, Key);
			c.Rect(r.x, r.yMax - 1, r.xMax - 1, r.yMax - 1, KeyTop);
		}

		/// <summary>Mirrors the key signs (only their light pixels) left for right inside a key's box.</summary>
		private static void Mirror(Canvas c, RectInt r)
		{
			List<Vector2Int> lit = new List<Vector2Int>();
			for (int y = r.y; y < r.yMax; y++)
			{
				for (int x = r.x; x < r.xMax; x++)
				{
					if (Same(c.Get(x, y), KeySign))
					{
						lit.Add(new Vector2Int(x, y));
					}
				}
			}
			Color32 under = c.Get(r.x + 1, r.y + 2);
			foreach (Vector2Int p in lit)
			{
				c.Set(p.x, p.y, under);
			}
			foreach (Vector2Int p in lit)
			{
				c.Set(r.x + r.xMax - 1 - p.x, p.y, KeySign);
			}
		}

		private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

		// ---------------------------------------------------------------------------------------------------------------------------
		// The display: amber segments on a dark brown glass, with the faint ghost of every unlit segment, as on an old car stereo.

		internal static readonly Color32 LcdGlass = new Color32(34, 18, 8, 255);
		internal static readonly Color32 LcdGhost = new Color32(52, 30, 12, 255);
		internal static readonly Color32 LcdLit = new Color32(255, 168, 52, 255);
		internal static readonly Color32 LcdDim = new Color32(150, 90, 30, 255);

		/// <summary>Display pixels across and up.</summary>
		internal static int LcdWidth => LcdWindow.width * LcdDetail;
		internal static int LcdHeight => LcdWindow.height * LcdDetail;

		/// <summary>Letters that fit on one line of the display.</summary>
		internal static int LcdColumns => (LcdWidth - 1) / Advance;

		/// <summary>
		/// The display with its two lines: <paramref name="top"/> (the disc, the track and the time) and <paramref name="bottom"/> (the
		/// song's name, scrolled by the caller). Off, only the ghosts of the segments show.
		/// </summary>
		internal static void Lcd(Color32[] into, string top, string bottom, bool on)
		{
			int w = LcdWidth, h = LcdHeight;
			Canvas c = new Canvas(w, h);
			c.Rect(0, 0, w - 1, h - 1, LcdGlass);
			int cols = LcdColumns;
			string ghost = new string('█', cols);
			int x0 = (w - cols * Advance + 1) / 2;
			int topY = h - 2 - GlyphHeight;
			int bottomY = 3;
			Text(c, ghost, x0, topY, LcdGhost);
			Text(c, ghost, x0, bottomY, LcdGhost);
			if (on)
			{
				Text(c, Fit(top, cols), x0, topY, LcdDim);
				Text(c, Fit(bottom, cols), x0, bottomY, LcdLit);
			}
			System.Array.Copy(c.Px, into, into.Length);
		}

		private static string Fit(string s, int n) => s == null ? "" : s.Length > n ? s.Substring(0, n) : s;

		// ---------------------------------------------------------------------------------------------------------------------------
		// The discs, at the stereo's own scale: a disc is as wide as most of the slot.

		/// <summary>A disc's diameter in cockpit pixels, and its radius (the disc's middle is between pixels: 15.5 from either edge).</summary>
		internal const int DiscSize = 31;
		internal const float DiscRadius = DiscSize * 0.5f;

		/// <summary>The slot on the head unit: the dark row, and the columns a disc stands over when it goes in (centred in the slot).</summary>
		internal const int SlotRow = 23;
		internal const int SlotLeft = 20;
		internal const int SlotRight = 53;

		/// <summary>
		/// A disc on its own (in the hand, falling, or half out of the slot): its label colour with its number or MY on it. Rows below
		/// <paramref name="clipBelow"/>[x] in column x are left clear (the part already inside the slot); null clips nothing. In the slot
		/// it is an oval <paramref name="width"/> wide (seen at the face's angle), centred on the 31-pixel canvas.
		/// </summary>
		internal static Canvas LooseDisc(Color32 label, string mark, int[] clipBelow, int width = DiscSize)
		{
			Canvas c = new Canvas(DiscSize, DiscSize);
			float m = (DiscSize - 1) * 0.5f;
			if (width >= DiscSize)
			{
				DrawDisc(c, m, m, DiscRadius, label);
			}
			else
			{
				float rx = width * 0.5f, ry = DiscRadius, cx = DiscSize * 0.5f, cy = DiscSize * 0.5f;
				Face.Oval(c, cx, cy, rx, ry, SilverDark);
				Face.Oval(c, cx, cy, rx - 0.8f, ry - 0.8f, Silver);
				Face.Oval(c, cx, cy, rx - 2f * rx / ry, ry - 2f, label);
				Face.Oval(c, cx, cy, (rx - 2f) * 0.45f, (ry - 2f) * 0.45f, Shade(label, -24));
				Face.Oval(c, cx, cy, Mathf.Max(1.2f, rx * 0.3f), Mathf.Max(1.2f, ry * 0.3f), SilverDark);
				Face.Oval(c, cx, cy, Mathf.Max(0.6f, rx * 0.14f), Mathf.Max(0.6f, ry * 0.14f), Hole);
			}
			if (!string.IsNullOrEmpty(mark) && mark.Length * Advance - 1 <= width - 6)
			{
				int tw = mark.Length * Advance - 1;
				Text(c, mark, Mathf.RoundToInt(m - (tw - 1) * 0.5f), Mathf.RoundToInt(m) + 5, LabelInk);
			}
			if (clipBelow != null)
			{
				for (int x = 0; x < c.W && x < clipBelow.Length; x++)
				{
					for (int y = 0; y < c.H && y < clipBelow[x]; y++)
					{
						c.Px[y * c.W + x] = Clear;
					}
				}
			}
			return c;
		}

		// ---------------------------------------------------------------------------------------------------------------------------
		// The CD wallet on the sun visor, after the owner's photo of one: black sleeves overlapping like cards, each cut in a curve on
		// its right, so only a crescent of each disc shows, the front panel (on the left) the widest.

		private static readonly Color32 Visor = new Color32(70, 66, 62, 255);
		private static readonly Color32 VisorEdge = new Color32(44, 42, 40, 255);
		private static readonly Color32 VisorLight = new Color32(92, 88, 82, 255);
		private static readonly Color32 Stitch = new Color32(110, 104, 96, 255);
		private static readonly Color32 Hinge = new Color32(128, 128, 134, 255);
		private static readonly Color32 HingeDark = new Color32(70, 70, 76, 255);
		private static readonly Color32 Wallet = new Color32(24, 24, 27, 255);
		private static readonly Color32 WalletEdge = new Color32(52, 52, 57, 255);
		private static readonly Color32 WalletStitch = new Color32(70, 70, 76, 255);
		private static readonly Color32 Pocket = new Color32(10, 10, 12, 255);
		private static readonly Color32 Strap = new Color32(34, 34, 38, 255);
		private static readonly Color32 Silver = new Color32(196, 200, 208, 255);
		private static readonly Color32 SilverDark = new Color32(132, 136, 146, 255);
		private static readonly Color32 Hole = new Color32(14, 14, 16, 255);
		private static readonly Color32 LabelInk = new Color32(30, 30, 30, 255);
		private static readonly Color32 Pen = new Color32(210, 206, 196, 255);

		/// <summary>One CD as the wallet shows it: its label colour and a short mark on the label.</summary>
		internal struct DiscLook
		{
			public Color32 Label;
			public string Mark;
			/// <summary>The disc is not in its sleeve (in the stereo, in the hand): the sleeve is empty.</summary>
			public bool Out;
		}

		/// <summary>Columns from one disc to the next, and how much of each disc shows past its sleeve's cut.</summary>
		internal const int DiscPitch = 10;
		internal const int Crescent = 7;
		/// <summary>The front panel's width left of the first disc.</summary>
		private const int Lead = 18;
		private const int WalletLeft = 5;
		private const int WalletBottom = 4;
		/// <summary>Rows under the discs for their numbers.</summary>
		private const int NumberRows = 7;
		internal const int VisorHeight = WalletBottom + NumberRows + DiscSize + 2 + 4;

		/// <summary>The visor's width for <paramref name="discs"/> discs.</summary>
		internal static int VisorWidth(int discs) => WalletLeft + Lead + (Mathf.Max(1, discs) - 1) * DiscPitch + DiscSize + 4 + WalletLeft;

		/// <summary>Disc <paramref name="i"/>'s bottom left in the wallet, in cockpit pixels.</summary>
		internal static Vector2Int DiscCorner(int i) => new Vector2Int(WalletLeft + Lead + i * DiscPitch, WalletBottom + NumberRows);

		/// <summary>What the hand takes disc <paramref name="i"/> by: its crescent and its number under it.</summary>
		internal static RectInt PocketPlace(int i)
		{
			Vector2Int d = DiscCorner(i);
			return new RectInt(d.x + DiscSize - Crescent - 1, WalletBottom + 1, Crescent + 1, NumberRows + DiscSize - 1);
		}

		/// <summary>
		/// The sun visor, folded up against the roof, with the wallet strapped round it. A disc that is out leaves its sleeve empty and
		/// dark. Drawn back to front: each disc, then its sleeve over it, from the last (at the back, on the right) to the first.
		/// </summary>
		internal static Canvas VisorWallet(IList<DiscLook> discs)
		{
			int n = Mathf.Max(1, discs.Count);
			int w = VisorWidth(n), h = VisorHeight;
			Canvas c = new Canvas(w, h);
			// The padded visor: rounded corners at the bottom (the free edge), straight along the hinge at the top.
			c.Rect(1, 0, w - 2, h - 1, VisorEdge);
			c.Rect(0, 2, w - 1, h - 1, VisorEdge);
			c.Rect(2, 1, w - 3, h - 2, Visor);
			c.Rect(1, 3, w - 2, h - 2, Visor);
			c.Rect(2, h - 3, w - 3, h - 2, VisorLight);
			c.Speckle(1, 1, w - 2, h - 2, 4, 5);
			for (int x = 3; x < w - 3; x += 2)
			{
				c.Set(x, 1, Stitch);
				c.Set(x, h - 4, Stitch);
			}
			c.Rect(0, h - 2, 3, h - 1, HingeDark);
			c.Rect(0, h - 1, 3, h - 1, Hinge);
			c.Rect(w - 4, h - 2, w - 1, h - 1, HingeDark);
			c.Rect(w - 4, h - 1, w - 1, h - 1, Hinge);

			int wx0 = WalletLeft, wx1 = w - 1 - WalletLeft, wy0 = WalletBottom - 1, wy1 = WalletBottom + NumberRows + DiscSize + 1;
			// The elastic straps round the visor, top and bottom of the wallet's ends.
			c.Rect(wx0 + 2, 0, wx0 + 6, h - 1, Strap);
			c.Rect(wx1 - 6, 0, wx1 - 2, h - 1, Strap);
			// The back plate, seen only where every sleeve is cut away.
			c.Rect(wx0, wy0, wx1, wy1, Wallet);
			for (int i = n - 1; i >= 0; i--)
			{
				DiscLook d = i < discs.Count ? discs[i] : new DiscLook { Out = true };
				Vector2Int corner = DiscCorner(i);
				float cx = corner.x + DiscRadius - 0.5f, cy = corner.y + DiscRadius - 0.5f;
				if (d.Out)
				{
					c.Disc(cx, cy, DiscRadius, Pocket);
				}
				else
				{
					DrawDisc(c, cx, cy, DiscRadius, d.Label);
				}
				// The sleeve: everything left of its cut, the cut a circle the disc's size moved left by the crescent.
				float sx = cx - Crescent;
				for (int y = wy0; y <= wy1; y++)
				{
					for (int x = wx0; x <= wx1; x++)
					{
						if (Sleeve(x, y, sx, cy) && !Sleeve(x + 1, y, sx, cy) && x < wx1)
						{
							c.Set(x, y, y > wy0 && y < wy1 ? WalletEdge : Wallet);
						}
						else if (Sleeve(x, y, sx, cy))
						{
							c.Set(x, y, Wallet);
						}
					}
				}
			}
			// The wallet's edge, stitched, and a card pocket on the front panel.
			for (int x = wx0; x <= wx1; x++)
			{
				c.Set(x, wy0, WalletEdge);
				c.Set(x, wy1, WalletEdge);
				if ((x & 1) == 0)
				{
					c.Set(x, wy1 - 1, WalletStitch);
					c.Set(x, wy0 + 1, WalletStitch);
				}
			}
			c.Rect(wx0, wy0, wx0, wy1, WalletEdge);
			c.Rect(wx1, wy0, wx1, wy1, WalletEdge);
			int px0 = wx0 + 3, px1 = wx0 + Lead + 1, py1 = wy0 + 20;
			c.Rect(px0, wy0 + 3, px1, py1, WalletEdge);
			c.Rect(px0 + 1, wy0 + 3, px1 - 1, py1 - 1, Wallet);
			for (int x = px0 + 1; x < px1; x += 2)
			{
				c.Set(x, py1 - 2, WalletStitch);
			}
			c.Speckle(wx0, wy0, wx1, wy1, 3, 9);
			// Each disc's number under its crescent, in white pen.
			for (int i = 0; i < n; i++)
			{
				DiscLook d = i < discs.Count ? discs[i] : default;
				string mark = string.IsNullOrEmpty(d.Mark) ? (i + 1).ToString() : d.Mark;
				RectInt p = PocketPlace(i);
				int tw = mark.Length * Advance - 1;
				Text(c, mark, p.x + (p.width - tw + 1) / 2, WalletBottom + 1, Pen);
			}
			return c;
		}

		/// <summary>Is (x, y) under a sleeve whose cut is the circle round (sx, cy)?</summary>
		private static bool Sleeve(int x, int y, float sx, float cy)
		{
			float dx = x - sx, dy = y - cy;
			return x <= sx || dx * dx + dy * dy <= (DiscRadius + 0.5f) * (DiscRadius + 0.5f);
		}

		/// <summary>A CD: a silver rim, the label, a clear ring round the hole and the hole, with a glint on the silver.</summary>
		internal static void DrawDisc(Canvas c, float cx, float cy, float r, Color32 label)
		{
			c.Disc(cx, cy, r, SilverDark);
			c.Disc(cx, cy, r - 0.7f, Silver);
			// Printed edge to edge, as most discs were, so a crescent past a sleeve still shows its colour.
			c.Disc(cx, cy, r - 2f, label);
			c.Disc(cx, cy, r * 0.45f, Shade(label, -24));
			c.Disc(cx, cy, Mathf.Max(1.2f, r * 0.3f), SilverDark);
			c.Disc(cx, cy, Mathf.Max(0.6f, r * 0.14f), Hole);
			c.Set(Mathf.RoundToInt(cx + r * 0.55f), Mathf.RoundToInt(cy + r * 0.62f), new Color32(255, 255, 255, 255));
		}

		/// <summary>The label colour of a disc, from the song: the colours of the maps the songs belong to.</summary>
		internal static Color32 LabelFor(string title)
		{
			string t = (title ?? "").ToUpperInvariant();
			if (t.StartsWith("FOREST")) return new Color32(58, 150, 64, 255);
			if (t.StartsWith("NEON 2")) return new Color32(150, 80, 230, 255);
			if (t.StartsWith("NEON")) return new Color32(236, 64, 176, 255);
			if (t.StartsWith("FACTORY")) return new Color32(232, 122, 34, 255);
			if (t.StartsWith("STADION") || t.StartsWith("STADIUM")) return new Color32(54, 114, 226, 255);
			if (t.StartsWith("WINTER")) return new Color32(186, 224, 246, 255);
			if (t.StartsWith("MIX")) return new Color32(236, 234, 222, 255);
			int h = Hash(t.Length, t.GetHashCode(), 3);
			Color c = Color.HSVToRGB((h % 360) / 360f, 0.6f, 0.85f);
			return new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
		}
	}
}
