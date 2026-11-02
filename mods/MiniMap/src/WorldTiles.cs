using System.Collections.Generic;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The Open World's map pictures of the tiles round the player's car, for a local map (see <see cref="PixelMap.DrawLocal"/>). The
	/// Open World's lap is only a token loop, so there a map of the lap would show nothing useful; its own tile pictures (land use,
	/// water, roads by class) are drawn instead. Everything here is in world coordinates (a scene position plus the Open World's origin
	/// offset), so the floating origin moving the scene changes nothing. Plain pixel arrays only: the tile pictures come in through a
	/// <see cref="Fetch"/> (<see cref="OpenWorldLink"/> in game), so the drawing can be tried outside the game too.
	/// </summary>
	internal sealed class WorldTiles
	{
		/// <summary>Copies tile (tx, tz)'s picture into <paramref name="into"/>, rows from the south, x east; false if it is not loaded.</summary>
		internal delegate bool Fetch(int tx, int tz, Color32[] into);

		private sealed class Tile
		{
			internal Color32[] Pixels;
			/// <summary>Per pixel: how much it matters on a small map (see <see cref="Ranked"/>).</summary>
			internal byte[] Rank;
		}

		/// <summary>
		/// The water and road colours of the Open World's tile pictures (its MapView API, the colours of its docs/world.png), least
		/// important first: a pixel of one of these ranks above land and above the ones before it. A map pixel wider than a tile pixel
		/// shows the highest ranked tile pixel under it, so a road two tile pixels wide still shows when the map shrinks it, and the
		/// bigger road where two meet. A colour not in the list is land, so a change of colours over there only loses that.
		/// </summary>
		private static readonly Color32[] Ranked =
		{
			new Color32(46, 82, 128, 255),   // water
			new Color32(150, 110, 70, 255),  // track
			new Color32(238, 236, 228, 255), // lane
			new Color32(70, 70, 74, 255),    // street
			new Color32(240, 225, 130, 255), // secondary
			new Color32(235, 190, 60, 255),  // primary
			new Color32(225, 110, 40, 255),  // trunk
		};

		internal float TileSize = 250f;
		internal int TilePixels = 64;

		private readonly Dictionary<long, Tile> tiles = new Dictionary<long, Tile>();
		/// <summary>Tiles asked for and not loaded yet, not asked for again until the Open World says its pictures changed.</summary>
		private readonly HashSet<long> missing = new HashSet<long>();
		private readonly List<long> drop = new List<long>();

		/// <summary>The tiles round the player this frame, <see cref="gridW"/> by <see cref="gridH"/> from tile (gridX, gridZ); null where not loaded.</summary>
		private Tile[] grid = new Tile[0];
		private int gridX;
		private int gridZ;
		private int gridW;
		private int gridH;
		/// <summary>The player from the south west corner of the grid, in metres: small numbers, so floats keep their precision far out.</summary>
		private float playerX;
		private float playerZ;

		/// <summary>Tile pictures held now (for the log).</summary>
		internal int Count => tiles.Count;

		private static long Key(int tx, int tz) => ((long)tx << 32) ^ (uint)tz;

		/// <summary>Forgets every tile: a new scene, a new world.</summary>
		internal void Clear()
		{
			tiles.Clear();
			missing.Clear();
			gridW = gridH = 0;
		}

		/// <summary>The Open World loaded or dropped tiles: ask again for the ones that were not there.</summary>
		internal void Changed() => missing.Clear();

		/// <summary>
		/// Gathers the tiles within <paramref name="reach"/> metres of the player at world (<paramref name="x"/>, <paramref name="z"/>),
		/// fetching those not held yet, and lets go of the ones well out of reach. A tile's picture never changes (the world is the same
		/// every time), so one is kept after the Open World drops it, for as long as it is near.
		/// </summary>
		internal void Window(double x, double z, float reach, Fetch fetch)
		{
			double size = TileSize;
			int x0 = (int)System.Math.Floor((x - reach) / size);
			int x1 = (int)System.Math.Floor((x + reach) / size);
			int z0 = (int)System.Math.Floor((z - reach) / size);
			int z1 = (int)System.Math.Floor((z + reach) / size);
			gridX = x0;
			gridZ = z0;
			gridW = x1 - x0 + 1;
			gridH = z1 - z0 + 1;
			if (grid.Length < gridW * gridH)
			{
				grid = new Tile[gridW * gridH];
			}
			playerX = (float)(x - x0 * size);
			playerZ = (float)(z - z0 * size);

			int pixels = TilePixels * TilePixels;
			for (int j = 0; j < gridH; j++)
			{
				for (int i = 0; i < gridW; i++)
				{
					long key = Key(x0 + i, z0 + j);
					if (!tiles.TryGetValue(key, out Tile tile) && !missing.Contains(key))
					{
						Color32[] into = new Color32[pixels];
						if (fetch(x0 + i, z0 + j, into))
						{
							tile = new Tile { Pixels = into, Rank = Ranks(into) };
							tiles[key] = tile;
						}
						else
						{
							missing.Add(key);
						}
					}
					grid[j * gridW + i] = tile;
				}
			}

			drop.Clear();
			foreach (long key in tiles.Keys)
			{
				int tx = (int)(key >> 32);
				int tz = (int)(uint)key;
				if (tx < x0 - 2 || tx > x1 + 2 || tz < z0 - 2 || tz > z1 + 2)
				{
					drop.Add(key);
				}
			}
			foreach (long key in drop)
			{
				tiles.Remove(key);
			}
		}

		private static byte[] Ranks(Color32[] pixels)
		{
			byte[] rank = new byte[pixels.Length];
			for (int k = 0; k < pixels.Length; k++)
			{
				Color32 c = pixels[k];
				for (int r = 0; r < Ranked.Length; r++)
				{
					Color32 m = Ranked[r];
					if (c.r == m.r && c.g == m.g && c.b == m.b)
					{
						rank[k] = (byte)(r + 1);
						break;
					}
				}
			}
			return rank;
		}

		/// <summary>
		/// The tile pixel <paramref name="east"/>, <paramref name="north"/> metres from the player, and its rank; false where no tile is
		/// loaded (or the point is out of the window).
		/// </summary>
		internal bool Sample(float east, float north, out Color32 colour, out byte rank)
		{
			float x = playerX + east;
			float z = playerZ + north;
			int i = Mathf.FloorToInt(x / TileSize);
			int j = Mathf.FloorToInt(z / TileSize);
			Tile tile = i >= 0 && j >= 0 && i < gridW && j < gridH ? grid[j * gridW + i] : null;
			if (tile == null)
			{
				colour = default;
				rank = 0;
				return false;
			}
			int px = Mathf.Clamp((int)((x - i * TileSize) * TilePixels / TileSize), 0, TilePixels - 1);
			int pz = Mathf.Clamp((int)((z - j * TileSize) * TilePixels / TileSize), 0, TilePixels - 1);
			int at = pz * TilePixels + px;
			colour = tile.Pixels[at];
			rank = tile.Rank[at];
			return true;
		}
	}
}
