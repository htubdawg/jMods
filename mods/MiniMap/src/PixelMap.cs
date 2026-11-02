using System.Collections.Generic;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>What a car marker on the map stands for.</summary>
	internal enum Marker
	{
		Bot,
		Remote,
	}

	/// <summary>
	/// A map as the player sees it: a small picture put together pixel by pixel each frame and shown with point filtering, so it has the
	/// hard square pixels of the game's own art. Turning a smooth picture would blur it; here each pixel of the map takes the one pixel of
	/// the <see cref="TrackImage"/> under it after the turn, and the car markers are drawn as pixel shapes on top, always upright.
	/// Round (the screen minimap, with a ring at the rim) or square (the GPS screen, whose casing is drawn round it separately).
	/// </summary>
	internal sealed class PixelMap
	{
		/// <summary>Pixels of ring at the rim of a round map: a dark outline, the frame colour, a lighter inner line.</summary>
		internal const int Ring = 4;

		internal static readonly Color32 Outline = new Color32(14, 13, 16, 255);
		private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
		private static readonly Color32 PlayerFill = new Color32(245, 245, 240, 255);
		private static readonly Color32 BotFill = new Color32(232, 67, 58, 255);
		private static readonly Color32 RemoteFill = new Color32(63, 169, 245, 255);

		/// <summary>The player: an arrowhead pointing up, the way the car points. '#' outline, 'o' fill, rows from the top.</summary>
		private static readonly string[] Arrow =
		{
			"...#...",
			"..#o#..",
			".#ooo#.",
			".#ooo#.",
			"#ooooo#",
			"#oo#oo#",
			"##...##",
		};

		/// <summary>The player on a small map: the same arrowhead, smaller.</summary>
		private static readonly string[] SmallArrow =
		{
			"..#..",
			".#o#.",
			"#ooo#",
			"#o#o#",
		};

		/// <summary>Any other car: a round dot.</summary>
		private static readonly string[] Dot =
		{
			".###.",
			"#ooo#",
			"#ooo#",
			"#ooo#",
			".###.",
		};

		/// <summary>Any other car on a small map.</summary>
		private static readonly string[] SmallDot =
		{
			"###",
			"#o#",
			"###",
		};

		/// <summary>Below this width the markers are drawn small, so they do not hide the lap.</summary>
		private const int SmallBelow = 72;

		internal readonly int Size;
		internal readonly bool Round;
		/// <summary>Pixels kept clear of the lap at the rim.</summary>
		internal int Room => Round ? Ring : 1;
		/// <summary>The picture, rows from the bottom.</summary>
		internal readonly Color32[] Pixels;
		private Texture2D texture;

		internal PixelMap(int size, bool round)
		{
			Size = size;
			Round = round;
			Pixels = new Color32[size * size];
		}

		/// <summary>The texture the picture is shown on, made on first use.</summary>
		internal Texture2D Texture
		{
			get
			{
				if (texture == null)
				{
					texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
					texture.name = "MiniMap";
					texture.filterMode = FilterMode.Point;
					texture.wrapMode = TextureWrapMode.Clamp;
					texture.hideFlags = HideFlags.HideAndDontSave;
				}
				return texture;
			}
		}

		/// <summary>Sends the picture to the texture.</summary>
		internal void Upload()
		{
			Texture.SetPixels32(Pixels);
			Texture.Apply(false);
		}

		internal void Destroy()
		{
			if (texture != null)
			{
				Object.Destroy(texture);
			}
			texture = null;
		}

		/// <summary>
		/// Draws the map turned so <paramref name="heading"/> (degrees clockwise from north) points up, with the player at
		/// <paramref name="player"/>. The whole lap turns about its own middle; a zoomed-in image (<see cref="TrackImage.Zoom"/>) cannot
		/// show the whole lap, so it is drawn round the player in the middle, like the Open World's map, and cars out of sight wait at the
		/// rim.
		/// </summary>
		internal void Draw(TrackImage image, Palette palette, float heading, Vector3 player, List<KeyValuePair<Vector3, Marker>> cars)
		{
			float rad = heading * Mathf.Deg2Rad;
			float cos = Mathf.Cos(rad);
			float sin = Mathf.Sin(rad);
			float half = Size * 0.5f;
			int detail = image.Detail;
			bool follow = image.Zoom > 1;
			// The image point in the middle of the map, in image pixels from the image's own middle.
			Vector2 middle = follow ? image.ToPixels(player) : Vector2.zero;
			int side = image.Side;
			Color32[] source = image.Pixels;
			Color32 ground = palette.Ground;
			Color32 frame = palette.Frame;
			Color32 rim = RimColour(palette);

			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					float ox = x + 0.5f - half;
					float oy = y + 0.5f - half;
					int at = y * Size + x;
					if (Rim(ox, oy, frame, rim, at))
					{
						continue;
					}
					// The map turns the other way to the car: un-turn the screen point to find the map point under it.
					float mx = ox * cos + oy * sin;
					float my = -ox * sin + oy * cos;
					int sx = Mathf.FloorToInt(side * 0.5f + middle.x + mx * detail);
					int sy = Mathf.FloorToInt(side * 0.5f + middle.y + my * detail);
					Pixels[at] = sx < 0 || sy < 0 || sx >= side || sy >= side ? ground : source[sy * side + sx];
				}
			}

			bool small = Size < SmallBelow;
			float edge = Round ? half - Ring - 3f : half - 3f;
			foreach (KeyValuePair<Vector3, Marker> car in cars)
			{
				Vector2 at = Turn((image.ToPixels(car.Key) - middle) / detail, cos, sin);
				Stamp(small ? SmallDot : Dot, car.Value == Marker.Remote ? RemoteFill : BotFill, Pixel(follow ? Pin(at, edge) : at));
			}
			Stamp(small ? SmallArrow : Arrow, PlayerFill, Pixel(Turn((image.ToPixels(player) - middle) / detail, cos, sin)));
		}

		/// <summary>
		/// Draws the Open World round the player, in the middle, turned so <paramref name="heading"/> points up, <paramref name="metres"/>
		/// to a map pixel. A map pixel is wider than a tile pixel at most sizes, so it looks at a few points across itself and shows the
		/// highest ranked (see <see cref="WorldTiles"/>): roads and rivers stay unbroken however small the map. Cars out of sight are kept
		/// at the rim, so the way to another player shows; a small N at the rim points north, as there is no lap to steer by.
		/// </summary>
		internal void DrawLocal(WorldTiles world, Palette palette, float heading, float metres, Vector3 player, List<KeyValuePair<Vector3, Marker>> cars)
		{
			float rad = heading * Mathf.Deg2Rad;
			float cos = Mathf.Cos(rad);
			float sin = Mathf.Sin(rad);
			float half = Size * 0.5f;
			Color32 ground = palette.Ground;
			Color32 frame = palette.Frame;
			Color32 rim = RimColour(palette);
			int looks = Mathf.Clamp(Mathf.CeilToInt(metres * world.TilePixels / world.TileSize), 1, 4);

			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					float ox = x + 0.5f - half;
					float oy = y + 0.5f - half;
					int at = y * Size + x;
					if (Rim(ox, oy, frame, rim, at))
					{
						continue;
					}
					// The middle decides the land; a point anywhere across the pixel can put a road or water on it.
					Color32 colour = ground;
					byte best = 0;
					if (world.Sample((ox * cos + oy * sin) * metres, (-ox * sin + oy * cos) * metres, out Color32 c, out byte r))
					{
						colour = c;
						best = r;
					}
					if (looks > 1)
					{
						for (int j = 0; j < looks; j++)
						{
							float sy = oy + (j + 0.5f) / looks - 0.5f;
							for (int i = 0; i < looks; i++)
							{
								float sx = ox + (i + 0.5f) / looks - 0.5f;
								if (world.Sample((sx * cos + sy * sin) * metres, (-sx * sin + sy * cos) * metres, out c, out r) && r > best)
								{
									colour = c;
									best = r;
								}
							}
						}
					}
					Pixels[at] = colour;
				}
			}

			bool small = Size < SmallBelow;
			float edge = Round ? half - Ring - 3f : half - 3f;
			foreach (KeyValuePair<Vector3, Marker> car in cars)
			{
				Vector2 d = new Vector2(car.Key.x - player.x, car.Key.z - player.z) / metres;
				Stamp(small ? SmallDot : Dot, car.Value == Marker.Remote ? RemoteFill : BotFill, Pixel(Pin(Turn(d, cos, sin), edge)));
			}
			Stamp(North, PlayerFill, Pixel(Pin(Turn(new Vector2(0f, 1000f), cos, sin), edge - 1f)));
			Stamp(small ? SmallArrow : Arrow, PlayerFill, Pixel(Vector2.zero));
		}

		/// <summary>The N at the rim of a local map: white on a dark plate, rows from the top.</summary>
		private static readonly string[] North =
		{
			"######",
			"#o##o#",
			"#oo#o#",
			"#o#oo#",
			"#o##o#",
			"######",
		};

		/// <summary>A point east, north of the middle in map pixels, turned with the map.</summary>
		private static Vector2 Turn(Vector2 m, float cos, float sin) => new Vector2(m.x * cos - m.y * sin, m.x * sin + m.y * cos);

		/// <summary>Brings a point from the middle back to <paramref name="edge"/> pixels out if it is further: round or square, as the map.</summary>
		private Vector2 Pin(Vector2 p, float edge)
		{
			float far = Round ? p.magnitude : Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
			return far > edge ? p * (edge / far) : p;
		}

		/// <summary>The map pixel of a point given from the middle.</summary>
		private Vector2Int Pixel(Vector2 p) => new Vector2Int(Mathf.FloorToInt(Size * 0.5f + p.x), Mathf.FloorToInt(Size * 0.5f + p.y));

		private static Color32 RimColour(Palette palette) => Color.Lerp(palette.Frame, Color.white, 0.35f);

		/// <summary>On a round map, sets the pixels outside the circle and in the ring round it and says so; the rest is the map's own.</summary>
		private bool Rim(float ox, float oy, Color32 frame, Color32 rim, int at)
		{
			if (!Round)
			{
				return false;
			}
			float half = Size * 0.5f;
			float d = Mathf.Sqrt(ox * ox + oy * oy);
			if (d > half)
			{
				Pixels[at] = Clear;
				return true;
			}
			if (d > half - Ring)
			{
				Pixels[at] = d > half - 1f ? Outline : d > half - Ring + 1f ? frame : rim;
				return true;
			}
			return false;
		}

		/// <summary>Fills the map with one colour: the GPS screen before a race has a lap to show.</summary>
		internal void Blank(Color32 colour)
		{
			for (int i = 0; i < Pixels.Length; i++)
			{
				Pixels[i] = colour;
			}
		}

		/// <summary>Seconds a tap's mark takes to ease away: the main menu's (VhsEffect.tapSeconds).</summary>
		internal const float TapSeconds = 0.45f;
		/// <summary>How far the mark reaches from the middle, as a share of the map's width. The menu's is 0.12 of the screen; on a screen this small that would be a speck.</summary>
		private const float TapReach = 0.5f;
		/// <summary>How far the picture bulges out from the middle: the menu's tapWarp.</summary>
		private const float TapWarp = 0.35f;
		/// <summary>How far the tape tears and the colour comes apart: the menu's tapGlitch, as a share of the map's width at most.</summary>
		private const float TapGlitch = 0.35f;
		/// <summary>Steps of light the glow comes in, and how much each adds to a channel: hard rings, not a smooth glow.</summary>
		private const int GlowSteps = 3;
		private const int GlowStep = 18;

		private Color32[] before;

		/// <summary>
		/// The main menu's screen tap on this map, <paramref name="age"/> seconds after it: the picture bulges out from the middle, rows
		/// tear sideways and red and blue come apart, and light gathers in stepped rings, all easing away over <see cref="TapSeconds"/>.
		/// The menu does it in a full-screen shader (VhsScreenTap and the VHS renderer feature); here it is done to the map's own pixels
		/// after they are drawn, by whole pixels, so the map keeps its hard pixel look. Made for the square GPS screen.
		/// </summary>
		internal void Tap(float age)
		{
			if (age < 0f || age >= TapSeconds)
			{
				return;
			}
			if (before == null)
			{
				before = new Color32[Pixels.Length];
			}
			System.Array.Copy(Pixels, before, Pixels.Length);
			float t = age / TapSeconds;
			float strength = (1f - t) * (1f - t);
			float half = Size * 0.5f;
			float reach = Size * TapReach;
			// The tear changes thirty times a second, the same on every row of a band two pixels high.
			int frame = Mathf.FloorToInt(age * 30f);
			float tear = TapGlitch * Size * 0.15f * strength;

			for (int y = 0; y < Size; y++)
			{
				float oy = y + 0.5f - half;
				int shift = Mathf.RoundToInt(tear * (Noise(y / 2, frame) * 2f - 1f));
				for (int x = 0; x < Size; x++)
				{
					float ox = x + 0.5f - half;
					float d = Mathf.Sqrt(ox * ox + oy * oy) / reach;
					if (d >= 1f)
					{
						continue;
					}
					float f = (1f - d) * (1f - d) * strength;
					float pull = 1f - TapWarp * f;
					int sx = Mathf.FloorToInt(half + ox * pull) + Mathf.RoundToInt(shift * (1f - d));
					int sy = Mathf.FloorToInt(half + oy * pull);
					int split = Mathf.RoundToInt(TapGlitch * 6f * f);
					int light = Mathf.Min(GlowSteps, Mathf.FloorToInt(f * (GlowSteps + 1))) * GlowStep;
					Color32 c = Before(sx, sy);
					c.r = (byte)Mathf.Min(255, Before(sx + split, sy).r + light);
					c.g = (byte)Mathf.Min(255, c.g + light);
					c.b = (byte)Mathf.Min(255, Before(sx - split, sy).b + light);
					Pixels[y * Size + x] = c;
				}
			}
		}

		/// <summary>A pixel of the picture as it was before the tap, the nearest one inside it for a point outside.</summary>
		private Color32 Before(int x, int y) => before[Mathf.Clamp(y, 0, Size - 1) * Size + Mathf.Clamp(x, 0, Size - 1)];

		/// <summary>A number from 0 to 1 that looks random, the same for the same row band and frame.</summary>
		private static float Noise(int band, int frame)
		{
			uint h = (uint)band * 0x9E3779B1u ^ (uint)frame * 0x85EBCA77u;
			h ^= h >> 15;
			h *= 0x2C1B3C6Du;
			h ^= h >> 12;
			return (h & 0xFFFF) / 65535f;
		}

		/// <summary>Draws a pixel shape centred on a point, inside the ring of a round map.</summary>
		private void Stamp(string[] shape, Color32 fill, Vector2Int at)
		{
			int h = shape.Length;
			float half = Size * 0.5f;
			float inside = half - Ring;
			for (int row = 0; row < h; row++)
			{
				string line = shape[row];
				int y = at.y + h / 2 - row;
				for (int col = 0; col < line.Length; col++)
				{
					char c = line[col];
					int x = at.x - line.Length / 2 + col;
					if (c == '.' || x < 0 || y < 0 || x >= Size || y >= Size)
					{
						continue;
					}
					float ox = x + 0.5f - half;
					float oy = y + 0.5f - half;
					if (Round && ox * ox + oy * oy > inside * inside)
					{
						continue;
					}
					Pixels[y * Size + x] = c == '#' ? Outline : fill;
				}
			}
		}
	}
}
