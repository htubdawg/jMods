using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The GPS's pixel art, drawn in code so it fits whatever size the dash and the shop card need, in the manner of the other items on
	/// the dash (the jumper's key cap above all): a chunky grey plastic box seen a little from above and to the left, so its top and right
	/// side show, with a speckled surface, a recessed screen, a green light and two buttons on its chin, standing on a dark base plate.
	/// Pixel arrays only, rows from the bottom; the caller makes the textures.
	/// </summary>
	internal static class GpsArt
	{
		private static readonly Color32 Front = new Color32(132, 132, 138, 255);
		private static readonly Color32 FrontLow = new Color32(110, 110, 116, 255);
		private static readonly Color32 Top = new Color32(178, 178, 184, 255);
		private static readonly Color32 Side = new Color32(86, 86, 92, 255);
		private static readonly Color32 RecessDark = new Color32(26, 26, 30, 255);
		private static readonly Color32 RecessLight = new Color32(62, 62, 68, 255);
		private static readonly Color32 Plate = new Color32(46, 46, 50, 255);
		private static readonly Color32 PlateTop = new Color32(72, 72, 78, 255);
		private static readonly Color32 Led = new Color32(96, 224, 96, 255);
		private static readonly Color32 Button = new Color32(70, 70, 76, 255);
		private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

		/// <summary>How chunky the box is drawn: the dash's size, or a compact one for a small shop icon.</summary>
		internal sealed class Style
		{
			/// <summary>Rows of top face and columns of side face that show.</summary>
			public int TopRows;
			public int SideColumns;
			/// <summary>Rows of base plate, and how far it reaches past the box on each side.</summary>
			public int PlateRows;
			public int PlateReach;
			/// <summary>Pixels of plastic between the outline and the screen's recess.</summary>
			public int Body;
			/// <summary>Rows between the box's bottom edge and the screen's recess.</summary>
			public int Chin;

			/// <summary>Pixels the casing adds round the screen, across and up.</summary>
			public int ExtraWidth => 2 * (Body + 2) + SideColumns + 2 * PlateReach;
			public int ExtraHeight => PlateRows - 1 + Chin + Body + 4 + TopRows;
		}

		internal static readonly Style Dash = new Style { TopRows = 3, SideColumns = 2, PlateRows = 3, PlateReach = 3, Body = 2, Chin = 4 };
		/// <summary>The shop icon's: the same box, thinner, so a card-sized icon still has a screen worth the name.</summary>
		internal static readonly Style Card = new Style { TopRows = 2, SideColumns = 1, PlateRows = 2, PlateReach = 2, Body = 1, Chin = 3 };

		/// <summary>A casing round a screen hole.</summary>
		internal sealed class Casing
		{
			public Color32[] Pixels;
			public int Width;
			public int Height;
			/// <summary>The screen hole's bottom left pixel.</summary>
			public int ScreenX;
			public int ScreenY;
		}

		/// <param name="sw">Width of the screen hole in pixels.</param>
		/// <param name="sh">Height of the screen hole in pixels.</param>
		internal static Casing Draw(int sw, int sh, Style style)
		{
			int body = style.Body;
			int fw = sw + 2 * (body + 2);           // outline, plastic, the recess, the screen, and the same on the other side
			int fh = sh + style.Chin + body + 4;    // outline, chin, recess, screen, recess, plastic, outline
			int ox = style.PlateReach;
			int py = style.PlateRows - 1;           // the box stands on the plate's top row
			int w = ox + fw + style.SideColumns + style.PlateReach;
			int h = py + fh + style.TopRows;
			// 0 outside, 1 front, 2 top, 3 side, 4 plate.
			byte[] face = new byte[w * h];

			for (int y = 0; y < style.PlateRows; y++)
			{
				for (int x = 0; x < w; x++)
				{
					face[y * w + x] = 4;
				}
			}
			for (int y = 0; y < fh; y++)
			{
				for (int x = 0; x < fw; x++)
				{
					face[(py + y) * w + ox + x] = 1;
				}
			}
			// The top face: each row up is shifted right, as a box seen from above and to the left.
			for (int j = 0; j < style.TopRows; j++)
			{
				int shift = (j + 1) * style.SideColumns / style.TopRows;
				for (int x = 0; x < fw; x++)
				{
					face[(py + fh + j) * w + ox + x + shift] = 2;
				}
			}
			// The side face: each column right is shifted up the same way.
			for (int c = 0; c < style.SideColumns; c++)
			{
				int shift = (c + 1) * style.TopRows / style.SideColumns;
				for (int y = 0; y < fh; y++)
				{
					int at = (py + y + shift) * w + ox + fw + c;
					if (face[at] == 0)
					{
						face[at] = 3;
					}
				}
			}

			Color32[] px = new Color32[w * h];
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					int f = face[y * w + x];
					if (f == 0)
					{
						px[y * w + x] = Clear;
						continue;
					}
					if (Edge(face, w, h, x, y))
					{
						px[y * w + x] = PixelMap.Outline;
						continue;
					}
					Color32 c = f == 1 ? (y < py + 1 + style.Chin ? FrontLow : Front) : f == 2 ? Top : f == 3 ? Side : y == style.PlateRows - 1 ? PlateTop : Plate;
					px[y * w + x] = Speckle(c, x, y, f != 4);
				}
			}

			// The screen's recess: dark along its top and left, lighter along its bottom and right, so it reads as set into the box.
			int sx = ox + body + 2;
			int sy = py + style.Chin + 2;
			for (int y = sy - 1; y <= sy + sh; y++)
			{
				for (int x = sx - 1; x <= sx + sw; x++)
				{
					bool light = y == sy - 1 || x == sx + sw;
					px[y * w + x] = light ? RecessLight : RecessDark;
				}
			}

			// The chin: a green light on the left, two buttons on the right.
			if (style.Chin >= 2)
			{
				int cy = py + 1 + style.Chin / 2;
				px[cy * w + sx] = Led;
				if (fw >= 18)
				{
					int bx = ox + fw - 5;
					px[cy * w + bx] = Button;
					px[cy * w + bx - 1] = Button;
					px[cy * w + bx - 4] = Button;
					px[cy * w + bx - 5] = Button;
				}
			}
			return new Casing { Pixels = px, Width = w, Height = h, ScreenX = sx, ScreenY = sy };
		}

		/// <summary>A pixel of the shape with a pixel of nothing beside it: drawn as the outline.</summary>
		private static bool Edge(byte[] face, int w, int h, int x, int y)
		{
			return x == 0 || y == 0 || x == w - 1 || y == h - 1 || face[y * w + x - 1] == 0 || face[y * w + x + 1] == 0 || face[(y - 1) * w + x] == 0 || face[(y + 1) * w + x] == 0;
		}

		/// <summary>The rough, speckled surface of the cockpit's pixel art: each pixel a little lighter or darker, and the odd scuff.</summary>
		private static Color32 Speckle(Color32 c, int x, int y, bool scuffs)
		{
			uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
			hash ^= hash >> 13;
			hash *= 0x5bd1e995;
			hash ^= hash >> 15;
			int d = (int)(hash % 13) - 6;
			if (scuffs && hash % 29 == 0)
			{
				d -= 22;
			}
			return new Color32((byte)Mathf.Clamp(c.r + d, 0, 255), (byte)Mathf.Clamp(c.g + d, 0, 255), (byte)Mathf.Clamp(c.b + d, 0, 255), 255);
		}

		/// <summary>The shop icon, <paramref name="w"/> by <paramref name="h"/>: the GPS in its card style, a lap and the player's arrow on its screen.</summary>
		internal static Color32[] Icon(int w, int h)
		{
			Color32[] px = new Color32[w * h];
			for (int i = 0; i < px.Length; i++)
			{
				px[i] = Clear;
			}
			int sh = h - 2 - Card.ExtraHeight;
			int sw = Mathf.Min(w - 2 - Card.ExtraWidth, sh * 2);
			Casing casing = Draw(sw, sh, Card);
			int ox = (w - casing.Width) / 2;
			int oy = (h - casing.Height) / 2;
			Blit(casing.Pixels, casing.Width, casing.Height, px, w, h, ox, oy);
			Lap(px, w, h, ox + casing.ScreenX, oy + casing.ScreenY, sw, sh);
			return px;
		}

		/// <summary>A lap on a screen of the icon: grass, a dirt loop, the player's arrow on its bottom stretch.</summary>
		private static void Lap(Color32[] px, int w, int h, int x0, int y0, int sw, int sh)
		{
			Palette p = Palettes.Forest;
			Color32 ground = p.Ground;
			Color32 road = Color.Lerp(p.Road, Color.white, 0.25f);
			Color32 arrow = new Color32(245, 245, 240, 255);
			float hx = sw * 0.5f;
			float hy = sh * 0.5f;
			// Radii of the loop, a pixel and a half in from the screen's edges, and the road a pixel and a half wide.
			float rx = Mathf.Max(1f, hx - 1.5f);
			float ry = Mathf.Max(1f, hy - 1.5f);
			for (int y = 0; y < sh; y++)
			{
				for (int x = 0; x < sw; x++)
				{
					float u = (x + 0.5f - hx) / rx;
					float v = (y + 0.5f - hy) / ry;
					float r = Mathf.Sqrt(u * u + v * v);
					bool onRoad = Mathf.Abs(r - 1f) * Mathf.Min(rx, ry) < 0.75f;
					Put(px, w, h, x0 + x, y0 + y, onRoad ? road : ground);
				}
			}
			int ax = x0 + sw / 2;
			int ay = y0 + Mathf.RoundToInt(hy - ry);
			Put(px, w, h, ax, ay + 1, arrow);
			Put(px, w, h, ax - 1, ay, arrow);
			Put(px, w, h, ax, ay, arrow);
			Put(px, w, h, ax + 1, ay, arrow);
		}

		private static void Blit(Color32[] from, int fw, int fh, Color32[] to, int tw, int th, int ox, int oy)
		{
			for (int y = 0; y < fh; y++)
			{
				for (int x = 0; x < fw; x++)
				{
					Color32 c = from[y * fw + x];
					if (c.a > 0)
					{
						Put(to, tw, th, ox + x, oy + y, c);
					}
				}
			}
		}

		private static void Put(Color32[] px, int w, int h, int x, int y, Color32 c)
		{
			if (x >= 0 && y >= 0 && x < w && y < h)
			{
				px[y * w + x] = c;
			}
		}
	}
}
