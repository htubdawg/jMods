using System.Collections.Generic;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The lap drawn from above, flat colours only (ground, road, the edge line along each side, the start line), at <see cref="Detail"/>
	/// times the resolution of the minimap. It is never shown itself: <see cref="PixelMap"/> picks one of its pixels for each pixel of
	/// the minimap, turned to the player's heading, which keeps the road a steady few pixels wide at any angle. The lap is fitted inside
	/// the round window, so however the map is turned none of it is cut off. Zoomed in (the GPS's closer views) the lap is drawn that
	/// many times bigger, the road still a few minimap pixels wide, with fewer image pixels to a minimap pixel so the image is the same
	/// size at either closer view.
	/// </summary>
	internal sealed class TrackImage
	{
		/// <summary>Pixels of the image per pixel of the minimap, for the whole lap.</summary>
		internal const int LapDetail = 4;
		/// <summary>Half the drawn road, in minimap pixels (a road 3 pixels wide). A fixed width, not the road's: on a whole lap a true width would be a hair.</summary>
		private const float RoadHalf = 1.5f;
		/// <summary>The edge line outside the road, in minimap pixels.</summary>
		private const float EdgeWidth = 1f;

		internal Color32[] Pixels;
		internal int Side;
		/// <summary>Pixels of this image per pixel of the minimap.</summary>
		internal int Detail;
		/// <summary>How many times bigger than the whole lap fitted in the window it is drawn.</summary>
		internal int Zoom;
		/// <summary>The world point (x, z) at the middle of the image.</summary>
		internal Vector2 Centre;
		/// <summary>Image pixels per metre.</summary>
		internal float Scale;

		/// <summary>Where a world position is on the image, in image pixels from its middle (x east, y north).</summary>
		internal Vector2 ToPixels(Vector3 world) => new Vector2(world.x - Centre.x, world.z - Centre.y) * Scale;

		/// <param name="size">Width of the minimap in its own pixels.</param>
		/// <param name="room">Minimap pixels kept clear inside the rim, for the frame ring.</param>
		/// <param name="zoom">1 for the whole lap in the window, 2 or 4 for the lap that many times bigger.</param>
		internal static TrackImage Build(List<Vector3> points, Palette palette, int size, int room, int zoom = 1)
		{
			int n = points.Count;
			Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
			Vector2 max = new Vector2(float.MinValue, float.MinValue);
			foreach (Vector3 p in points)
			{
				min = Vector2.Min(min, new Vector2(p.x, p.z));
				max = Vector2.Max(max, new Vector2(p.x, p.z));
			}
			Vector2 centre = (min + max) * 0.5f;
			float radius = 1f;
			foreach (Vector3 p in points)
			{
				radius = Mathf.Max(radius, (new Vector2(p.x, p.z) - centre).magnitude);
			}

			zoom = Mathf.Max(1, zoom);
			int detail = Mathf.Clamp(8 / zoom, 1, LapDetail);
			int side = size * zoom * detail;
			float roadHalf = RoadHalf * detail;
			float reach = (RoadHalf + EdgeWidth) * detail;
			TrackImage image = new TrackImage { Side = side, Centre = centre, Detail = detail, Zoom = zoom };
			// One more pixel of ground between the edge line and the ring.
			image.Scale = ((size * 0.5f * zoom - room - RoadHalf - EdgeWidth - 1f) * detail) / radius;

			Vector2[] px = new Vector2[n];
			for (int i = 0; i < n; i++)
			{
				px[i] = image.ToPixels(points[i]) + new Vector2(side * 0.5f, side * 0.5f);
			}

			// 0 ground, 1 edge, 2 road: hard edges, no blending, so the minimap has only the palette's colours in it.
			byte[] kind = new byte[side * side];
			for (int i = 0; i < n; i++)
			{
				Vector2 a = px[i];
				Vector2 b = px[(i + 1) % n];
				int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - reach - 1));
				int x1 = Mathf.Min(side - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + reach + 1));
				int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - reach - 1));
				int y1 = Mathf.Min(side - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + reach + 1));
				for (int y = y0; y <= y1; y++)
				{
					for (int x = x0; x <= x1; x++)
					{
						float d = Distance(new Vector2(x + 0.5f, y + 0.5f), a, b);
						int at = y * side + x;
						if (d <= roadHalf)
						{
							kind[at] = 2;
						}
						else if (d <= reach && kind[at] == 0)
						{
							kind[at] = 1;
						}
					}
				}
			}

			Color32 ground = palette.Ground;
			Color32 edge = palette.Edge;
			Color32 road = palette.Road;
			Color32[] pixels = new Color32[side * side];
			for (int i = 0; i < pixels.Length; i++)
			{
				pixels[i] = kind[i] == 2 ? road : kind[i] == 1 ? edge : ground;
			}
			StartLine(pixels, side, px, reach, detail);
			image.Pixels = pixels;
			return image;
		}

		/// <summary>A white bar one minimap pixel thick across the road at the first point of the line, where the lap counter starts a lap.</summary>
		private static void StartLine(Color32[] pixels, int side, Vector2[] px, float half, int detail)
		{
			int n = px.Length;
			Vector2 o = px[0];
			Vector2 along = (px[1] - px[n - 1]).normalized;
			Vector2 across = new Vector2(-along.y, along.x);
			float thick = detail * 0.5f;
			Color32 white = new Color32(240, 240, 236, 255);
			int r = Mathf.CeilToInt(half + thick + 1);
			for (int y = (int)o.y - r; y <= (int)o.y + r; y++)
			{
				for (int x = (int)o.x - r; x <= (int)o.x + r; x++)
				{
					if (x < 0 || y < 0 || x >= side || y >= side)
					{
						continue;
					}
					Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - o;
					if (Mathf.Abs(Vector2.Dot(d, across)) <= half && Mathf.Abs(Vector2.Dot(d, along)) <= thick)
					{
						pixels[y * side + x] = white;
					}
				}
			}
		}

		private static float Distance(Vector2 p, Vector2 a, Vector2 b)
		{
			Vector2 ab = b - a;
			float len = ab.sqrMagnitude;
			float t = len > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
			return (p - (a + ab * t)).magnitude;
		}
	}
}
