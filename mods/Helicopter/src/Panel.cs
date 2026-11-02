using System.Collections.Generic;
using UnityEngine;

namespace HalfGripHelicopter
{
	/// <summary>
	/// The instrument panel's face, drawn to fit round the cockpit's controls. The stick and the levers are dash sprites drawn over the
	/// world, so wherever they fall on the panel nothing may be drawn that they would seem to stand on (0.1.6: the stick stood on a
	/// gauge). <see cref="Airframe.FitPanel"/> works out where they fall (<see cref="Zone"/>s, in the face's texture space) and how big a
	/// dash pixel is there, and this draws the face at that pixel size, in the dash's style (speckled plastic, hard bevels, the game's
	/// palette): plain under the stick, a grey console under the levers' gate (the gate looks set into it), and the gauges, radio, lights
	/// and switches packed into what is left of the part that can be seen.
	///
	/// Plain managed code (Unity's structs and <see cref="Canvas"/> only), so <c>tools/preview</c> draws it too. Rows from the bottom.
	/// </summary>
	internal static class Panel
	{
		/// <summary>
		/// A part of the face kept clear, in texture space (0..1, v up). Console: drawn as the levers' plate; Mount: the MiniMap's GPS
		/// on its plate (kept plain, like the stick's); else plain.
		/// </summary>
		internal struct Zone
		{
			internal float U0, V0, U1, V1;
			internal bool Console;
			internal bool Mount;

			internal Zone(float u0, float v0, float u1, float v1, bool console, bool mount = false)
			{
				Mount = mount;
				U0 = Mathf.Min(u0, u1);
				U1 = Mathf.Max(u0, u1);
				V0 = Mathf.Min(v0, v1);
				V1 = Mathf.Max(v0, v1);
				Console = console;
			}
		}

		private static readonly Color32 Face = new Color32(58, 60, 66, 255);
		private static readonly Color32 FaceDot = new Color32(48, 50, 56, 255);
		private static readonly Color32 Needle = new Color32(220, 52, 40, 255);
		private static readonly Color32 Amber = new Color32(240, 170, 40, 255);
		private static readonly Color32 Digit = new Color32(90, 220, 110, 255);
		private static readonly Color32 DigitDim = new Color32(36, 90, 48, 255);

		/// <summary>
		/// Where the stick and the gate fall on the game's car with its own camera (measured off the owner's 0.1.6 screenshot), for when
		/// <see cref="Airframe.FitPanel"/> can't work them out; and the part of the face seen above the screen's bottom edge.
		/// </summary>
		internal static List<Zone> GuessZones(out float seenFrom)
		{
			seenFrom = 0.45f;
			return new List<Zone>
			{
				new Zone(0.0f, 0f, 0.16f, 1f, false),
				new Zone(0.42f, 0f, 0.68f, 1f, true),
			};
		}

		/// <summary>
		/// The face, <paramref name="w"/> by <paramref name="h"/> (one pixel per dash pixel), seen from row <paramref name="seenFrom"/> (0..1)
		/// up, with <paramref name="zones"/> kept clear.
		/// </summary>
		internal static Canvas Draw(int w, int h, float seenFrom, List<Zone> zones)
		{
			Canvas c = new Canvas(w, h);
			c.Rect(0, 0, w, h, Face);
			Art.Speckle(c, Face, FaceDot, 11);
			// The face's edges: light along the top, dark along the bottom and right.
			c.Rect(0, h - 1, w, 1, Art.Light);
			c.Rect(0, h - 2, w, 1, Art.Grey);
			c.Rect(0, 0, w, 1, Art.Black);
			c.Rect(w - 1, 0, 1, h, Art.Black);
			c.Rect(0, 0, 1, h, Art.Grey);

			bool[] used = new bool[w * h];
			foreach (Zone z in zones)
			{
				int x0 = Mathf.Clamp(Mathf.FloorToInt(z.U0 * w), 0, w), x1 = Mathf.Clamp(Mathf.CeilToInt(z.U1 * w), 0, w);
				int y0 = Mathf.Clamp(Mathf.FloorToInt(z.V0 * h), 0, h), y1 = Mathf.Clamp(Mathf.CeilToInt(z.V1 * h), 0, h);
				if (x1 <= x0 || y1 <= y0)
				{
					continue;
				}
				int keep = 2;
				for (int y = Mathf.Max(0, y0 - keep); y < Mathf.Min(h, y1 + keep); y++)
				{
					for (int x = Mathf.Max(0, x0 - keep); x < Mathf.Min(w, x1 + keep); x++)
					{
						used[y * w + x] = true;
					}
				}
				if (z.Console)
				{
					Console(c, x0, y0, x1, Mathf.Min(y1, h - 3));
				}
			}

			int seen = Mathf.Clamp(Mathf.RoundToInt(seenFrom * h), 0, h - 8);
			int top = h - 4;
			int room = top - seen;
			// Rows from the top down while they can be seen: big gauges (the game's dials are about 20 dash pixels across), a strip of
			// radios, lights and switches, smaller gauges, another strip. Each row is centred in each stretch the controls leave free.
			int d = Mathf.Clamp(Mathf.RoundToInt(room * 0.4f), 11, 30);
			int rowTop = top;
			int gauges = 0, items = 0;
			for (int r = 0; r < 4; r++)
			{
				bool gaugeRow = r % 2 == 0;
				int rh = gaugeRow ? (r == 0 ? d : Mathf.Max(11, d * 3 / 4)) : 8;
				int y0 = rowTop - rh;
				if (y0 < seen + 1)
				{
					break;
				}
				foreach ((int x0, int x1) in Runs(used, w, h, y0 - 1, rh + 2))
				{
					List<int> widths = new List<int>();
					int total = -3;
					while (true)
					{
						int iw = gaugeRow ? rh : ItemWidth(items + widths.Count);
						if (total + 3 + iw > x1 - x0 - 2)
						{
							break;
						}
						widths.Add(iw);
						total += 3 + iw;
					}
					int x = x0 + (x1 - x0 - total) / 2;
					foreach (int iw in widths)
					{
						if (gaugeRow)
						{
							Gauge(c, x, y0, iw, needles[gauges % needles.Length], gauges);
							gauges++;
						}
						else
						{
							Item(c, items, x, y0, iw);
							items++;
						}
						x += iw + 3;
					}
				}
				rowTop = y0 - (gaugeRow ? 4 : 5);
			}
			// Screws in the face's corners.
			foreach (int sx in new[] { 2, w - 4 })
			{
				Screw(c, sx, h - 5);
				Screw(c, sx, Mathf.Max(2, seen + 1));
			}
			return c;
		}

		private static readonly Color32[] needles = { Needle, Art.White, Amber, Art.White, Needle, Amber };

		/// <summary>The stretches of columns (start, end) free of the controls over rows y0..y0+rows, inside the face's edges.</summary>
		private static List<(int, int)> Runs(bool[] used, int w, int h, int y0, int rows)
		{
			List<(int, int)> runs = new List<(int, int)>();
			int start = -1;
			for (int x = 2; x <= w - 3; x++)
			{
				bool free = x < w - 3 && Free(used, w, h, x, y0, 1, rows);
				if (free && start < 0)
				{
					start = x;
				}
				else if (!free && start >= 0)
				{
					runs.Add((start, x));
					start = -1;
				}
			}
			return runs;
		}

		private static int ItemWidth(int k) => k % 3 == 0 ? 20 : k % 3 == 1 ? 13 : 17;

		private static void Item(Canvas c, int k, int x, int y, int w)
		{
			if (k % 3 == 0)
			{
				Radio(c, x, y, w, 8);
			}
			else if (k % 3 == 1)
			{
				Lights(c, x, y);
			}
			else
			{
				Switches(c, x, y, w);
			}
		}

		private static bool Free(bool[] used, int w, int h, int x0, int y0, int bw, int bh)
		{
			if (x0 < 0 || y0 < 0 || x0 + bw > w || y0 + bh > h)
			{
				return false;
			}
			for (int y = y0; y < y0 + bh; y++)
			{
				for (int x = x0; x < x0 + bw; x++)
				{
					if (used[y * w + x])
					{
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>The levers' console: the gate's speckled grey plate, bevelled, with a seam and screws.</summary>
		private static void Console(Canvas c, int x0, int y0, int x1, int y1)
		{
			c.Rect(x0, y0, x1 - x0, y1 - y0, Art.Plate);
			Speckle(c, x0, y0, x1, y1, Art.Plate, Art.PlateDot, 5);
			c.Rect(x0, y1 - 1, x1 - x0, 1, Art.Light);
			c.Rect(x0, y0, 1, y1 - y0, Art.Light);
			c.Rect(x1 - 1, y0, 1, y1 - y0, Art.Black);
			c.Rect(x0 - 1, y0, 1, y1 - y0, Art.Black);
			c.Rect(x0 - 1, y1, x1 - x0 + 2, 1, Art.Black);
			Screw(c, x0 + 2, y1 - 4);
			Screw(c, x1 - 4, y1 - 4);
		}

		private static void Speckle(Canvas c, int x0, int y0, int x1, int y1, Color32 a, Color32 b, int seed)
		{
			for (int y = Mathf.Max(0, y0); y < Mathf.Min(c.Height, y1); y++)
			{
				for (int x = Mathf.Max(0, x0); x < Mathf.Min(c.Width, x1); x++)
				{
					uint k = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791);
					k ^= k >> 13;
					k *= 0x5bd1e995;
					k ^= k >> 15;
					if ((k & 7) == 0 && c.Get(x, y).r == a.r)
					{
						c.Set(x, y, b);
					}
				}
			}
		}

		private static void Screw(Canvas c, int x, int y)
		{
			c.Set(x, y, Art.Light);
			c.Set(x + 1, y, Art.Grey);
			c.Set(x, y - 1, Art.Grey);
			c.Set(x + 1, y - 1, Art.Black);
		}

		/// <summary>A round gauge like the game's dials: a bevelled grey rim, a black face with white ticks round 270 degrees, a needle.</summary>
		private static void Gauge(Canvas c, int x, int y, int d, Color32 needle, int seed)
		{
			c.Ellipse(x - 1, y + 1, d, d, Art.Light);
			c.Ellipse(x + 1, y - 1, d, d, Art.Black);
			c.Ellipse(x, y, d, d, Art.Grey);
			c.Ellipse(x + 2, y + 2, d - 4, d - 4, Art.Black);
			float r = 0.5f * d;
			float cx = x + r, cy = y + r;
			int ticks = d >= 18 ? 9 : 5;
			for (int k = 0; k < ticks; k++)
			{
				float a = (225f - 270f * k / (ticks - 1)) * Mathf.Deg2Rad;
				float tr = r - 3.5f;
				c.Set(Mathf.FloorToInt(cx + Mathf.Cos(a) * tr), Mathf.FloorToInt(cy + Mathf.Sin(a) * tr), Art.White);
				if (d >= 20)
				{
					c.Set(Mathf.FloorToInt(cx + Mathf.Cos(a) * (tr - 1f)), Mathf.FloorToInt(cy + Mathf.Sin(a) * (tr - 1f)), Art.Grey);
				}
			}
			// The needle somewhere along the scale, a different place on each gauge.
			float na = (200f - 37f * (seed * 3 % 7)) * Mathf.Deg2Rad;
			int nx = Mathf.FloorToInt(cx + Mathf.Cos(na) * (r - 5f)), ny = Mathf.FloorToInt(cy + Mathf.Sin(na) * (r - 5f));
			c.Line(Mathf.FloorToInt(cx), Mathf.FloorToInt(cy), nx, ny, d >= 22 ? 2 : 1, needle);
			c.Rect(Mathf.FloorToInt(cx) - 1, Mathf.FloorToInt(cy) - 1, 2, 2, Art.Grey);
		}

		/// <summary>A radio: a bevelled box with a green display and two knobs.</summary>
		private static void Radio(Canvas c, int x, int y, int w, int h)
		{
			Box(c, x, y, w, h, Art.Dark);
			c.Rect(x + 2, y + 3, w - 9, h - 5, Art.Black);
			for (int k = 0; k < (w - 11) / 2; k++)
			{
				c.Set(x + 3 + 2 * k, y + 4, k % 3 == 2 ? DigitDim : Digit);
				c.Set(x + 3 + 2 * k, y + 5, Digit);
			}
			c.Rect(x + w - 6, y + h - 4, 2, 2, Art.Light);
			c.Rect(x + w - 4, y + 2, 2, 2, Art.Grey);
		}

		/// <summary>Three warning lights, red, amber and green, in a bevelled strip.</summary>
		private static void Lights(Canvas c, int x, int y)
		{
			Box(c, x, y, 13, 8, Art.Dark);
			Color32[] l = { Needle, Amber, Art.Green };
			for (int k = 0; k < 3; k++)
			{
				c.Rect(x + 2 + 3 * k + k, y + 3, 2, 2, l[k]);
				c.Set(x + 2 + 3 * k + k, y + 4, Art.White);
			}
		}

		/// <summary>A row of toggle switches on a bevelled strip.</summary>
		private static void Switches(Canvas c, int x, int y, int w)
		{
			Box(c, x, y, w, 8, Art.Dark);
			for (int sx = x + 2; sx < x + w - 2; sx += 3)
			{
				c.Rect(sx, y + 2, 2, 2, Art.Black);
				c.Set(sx, y + 4, Art.Grey);
				c.Set(sx, y + 5, Art.Light);
			}
		}

		private static void Box(Canvas c, int x, int y, int w, int h, Color32 fill)
		{
			c.Rect(x, y, w, h, fill);
			c.Rect(x, y + h - 1, w, 1, Art.Grey);
			c.Rect(x, y, 1, h, Art.Grey);
			c.Rect(x, y, w, 1, Art.Black);
			c.Rect(x + w - 1, y, 1, h, Art.Black);
		}
	}
}
