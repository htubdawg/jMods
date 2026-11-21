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
		// The head unit: a black single-piece car stereo, a little taller than one DIN, seen face on like the dash's dials. Since 0.3.0
		// it has no keys (too small for the hand, the owner found): a knob at each end, the left half mirrored (the owner's wish).

		/// <summary>The head unit's size in dash pixels: two knobs round the display, mirror images about the middle (37).</summary>
		internal const int UnitWidth = 74;
		internal const int UnitHeight = 28;

		/// <summary>The display's window on the unit (inside the chrome frame), in dash pixels; the display itself is drawn at <see cref="LcdDetail"/> times this.</summary>
		internal static readonly RectInt LcdWindow = new RectInt(21, 10, 32, 10);
		internal const int LcdDetail = 2;

		/// <summary>The parts of the unit the hand works: the volume knob on the left, the seek knob on the right.</summary>
		internal enum Part { Volume, Seek }

		internal static readonly Part[] Parts = { Part.Volume, Part.Seek };

		/// <summary>Each knob's box round its circle, in dash pixels; the right one is the left one mirrored.</summary>
		internal static RectInt Place(Part part) => part == Part.Volume ? new RectInt(3, 5, 15, 15) : new RectInt(UnitWidth - 3 - 15, 5, 15, 15);

		/// <summary>
		/// The head unit's casing, flat (no lean; everything but the display, which is its own glowing sprite over the window), for the
		/// preview's pictures: the same drawing as the cockpit's (<see cref="Face"/>) with its sides upright.
		/// </summary>
		internal static Canvas HeadUnit(Part? pressed, float volumeTurn, int seekNotches) => new Face(Vector2.zero, 0f).Casing(pressed, volumeTurn, seekNotches);

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
			/// <summary>The hand hovers its sleeve: the disc stands out of its sleeve's cut, to the right (Visor.PopOut pixels).</summary>
			public bool Hovered;
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

		/// <summary>The most a hovered disc comes out (the top of Visor.PopOut's range).</summary>
		internal const int MaxPop = 16;

		/// <summary>
		/// Clear columns right of the visor in its picture, so the last disc pulled out by up to <see cref="MaxPop"/> is drawn whole
		/// past the visor's end (the visor itself never widens): the last disc's right edge is 8 columns short of the visor's end.
		/// </summary>
		internal const int Spill = MaxPop - 8;

		/// <summary>The visor picture's width: the visor and the clear <see cref="Spill"/> right of it.</summary>
		internal static int VisorCanvasWidth(int discs) => VisorWidth(discs) + Spill;

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
		/// The hand's box for disc <paramref name="i"/>: its <see cref="PocketPlace"/> widened right to the next disc's (one
		/// <see cref="DiscPitch"/> wide); the boxes do not overlap, and a box does not move when its disc pops, so it never flickers.
		/// </summary>
		internal static RectInt HoverPlace(int i)
		{
			RectInt p = PocketPlace(i);
			return new RectInt(p.x, p.y, DiscPitch, p.height);
		}

		/// <summary>
		/// The sun visor, folded up against the roof, with the wallet strapped round it. A disc that is out leaves its sleeve empty and
		/// dark. Drawn back to front: each disc, then its sleeve over it, from the last (at the back, on the right) to the first.
		/// </summary>
		/// <param name="pop">How far a hovered disc comes out of its sleeve to the right, in pixels.</param>
		internal static Canvas VisorWallet(IList<DiscLook> discs, int pop)
		{
			int n = Mathf.Max(1, discs.Count);
			int w = VisorWidth(n), h = VisorHeight, cw = VisorCanvasWidth(n);
			pop = Mathf.Clamp(pop, 0, MaxPop);
			Canvas c = new Canvas(cw, h);
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
					// A hovered disc slides out of its sleeve's cut, to the right, over the next sleeve; the sleeve itself stays put.
					DrawDisc(c, cx + (d.Hovered ? pop : 0), cy, DiscRadius, d.Label);
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
			// A hovered last disc pulled out far enough sticks out past the wallet's right edge: drawn again over the edge, the visor
			// beyond it and the clear spill past the visor (no sleeve reaches there), so it is not cut by either end.
			int last = n - 1;
			if (last < discs.Count && discs[last].Hovered && !discs[last].Out)
			{
				Vector2Int corner = DiscCorner(last);
				Canvas over = new Canvas(cw, h);
				DrawDisc(over, corner.x + DiscRadius - 0.5f + pop, corner.y + DiscRadius - 0.5f, DiscRadius, discs[last].Label);
				for (int y = 0; y < h; y++)
				{
					for (int x = wx1; x < cw; x++)
					{
						Color32 p = over.Get(x, y);
						if (p.a > 0)
						{
							c.Set(x, y, p);
						}
					}
				}
			}
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
