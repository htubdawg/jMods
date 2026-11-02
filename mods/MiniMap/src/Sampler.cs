using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// Colours for a scene <see cref="Palettes"/> does not know (a map another mod brings with a scene of its own): the ground under the
	/// track line is read at points round the lap, and so is the ground well to each side of it. A terrain gives the colour of its paint
	/// layers there, weighted as they are painted; a mesh gives its material's colour times the average of its texture.
	/// </summary>
	internal static class Sampler
	{
		/// <summary>How many points round the lap are read.</summary>
		private const int Points = 24;
		/// <summary>How far to the side of the line (metres) the ground beside the road is read.</summary>
		private const float Aside = 40f;

		private static readonly Dictionary<int, Color> averages = new Dictionary<int, Color>();

		/// <summary>The palette read off the scene, or null if too little of it could be read.</summary>
		internal static Palette FromScene(PathManager line)
		{
			try
			{
				List<Vector3> pts = line.sampledPoints;
				int n = pts.Count;
				Color road = Color.clear;
				Color ground = Color.clear;
				int roads = 0;
				int grounds = 0;
				for (int i = 0; i < Points; i++)
				{
					int k = i * n / Points;
					Vector3 p = pts[k];
					Vector3 along = pts[(k + 1) % n] - pts[(k - 1 + n) % n];
					along.y = 0f;
					Vector3 side = Vector3.Cross(Vector3.up, along.normalized);
					if (Read(p, p.y, out Color c))
					{
						road += c;
						roads++;
					}
					if (Read(p + side * Aside, p.y, out c))
					{
						ground += c;
						grounds++;
					}
					if (Read(p - side * Aside, p.y, out c))
					{
						ground += c;
						grounds++;
					}
				}
				if (roads < Points / 3 || grounds < Points / 3)
				{
					Mod.Info("Minimap: only " + roads + " road and " + grounds + " ground points could be read; default colours.");
					return null;
				}
				Palette p2 = new Palette { Name = "Sampled", Road = Opaque(road / roads), Ground = Opaque(ground / grounds) };
				Palettes.Contrast(p2);
				// A light edge on a dark road, a dark one on a light road.
				p2.Edge = Palettes.Luma(p2.Road) < 0.5f ? Color.Lerp(p2.Road, Color.white, 0.6f) : Palettes.Scale(p2.Road, 0.45f);
				p2.Frame = Palettes.Scale(p2.Ground, 0.35f);
				return p2;
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Minimap: could not read the scene's colours, using the default ones: " + e.Message);
				return null;
			}
		}

		/// <summary>The colour of whatever is under this point, if it is near the height of the line (not a roof or a cliff top).</summary>
		private static bool Read(Vector3 at, float lineHeight, out Color colour)
		{
			colour = Color.clear;
			int mask = Physics.DefaultRaycastLayers & ~(1 << 2);
			RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up * 60f, Vector3.down, 160f, mask, QueryTriggerInteraction.Ignore);
			Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
			foreach (RaycastHit hit in hits)
			{
				if (hit.collider.GetComponentInParent<CarControllerNew>() != null || Mathf.Abs(hit.point.y - lineHeight) > 30f)
				{
					continue;
				}
				return Surface(hit, out colour);
			}
			return false;
		}

		private static bool Surface(RaycastHit hit, out Color colour)
		{
			colour = Color.clear;
			Terrain terrain = hit.collider.GetComponent<Terrain>();
			if (terrain != null && terrain.terrainData != null)
			{
				TerrainData data = terrain.terrainData;
				TerrainLayer[] layers = data.terrainLayers;
				if (layers == null || layers.Length == 0)
				{
					return false;
				}
				Vector3 local = hit.point - terrain.transform.position;
				int x = Mathf.Clamp((int)(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
				int z = Mathf.Clamp((int)(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
				float[,,] weights = data.GetAlphamaps(x, z, 1, 1);
				for (int i = 0; i < layers.Length && i < weights.GetLength(2); i++)
				{
					if (layers[i] == null)
					{
						continue;
					}
					Vector4 tint = layers[i].diffuseRemapMax;
					Color c = Average(layers[i].diffuseTexture);
					colour += new Color(c.r * tint.x, c.g * tint.y, c.b * tint.z) * weights[0, 0, i];
				}
				colour.a = 1f;
				return true;
			}

			Renderer renderer = hit.collider.GetComponent<Renderer>() ?? hit.collider.GetComponentInParent<Renderer>();
			Material m = renderer != null ? renderer.sharedMaterial : null;
			if (m == null)
			{
				return false;
			}
			Color basis = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.color : Color.white;
			Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
			colour = Opaque(basis * Average(tex));
			return true;
		}

		/// <summary>
		/// The average colour of a texture. Game textures are not readable from the CPU, so it is drawn down into a small render texture
		/// first and that is read back.
		/// </summary>
		private static Color Average(Texture tex)
		{
			if (tex == null)
			{
				return Color.white;
			}
			if (averages.TryGetValue(tex.GetInstanceID(), out Color known))
			{
				return known;
			}
			const int side = 16;
			RenderTexture rt = RenderTexture.GetTemporary(side, side, 0, RenderTextureFormat.ARGB32);
			RenderTexture before = RenderTexture.active;
			Texture2D read = new Texture2D(side, side, TextureFormat.RGBA32, false);
			Color sum = Color.clear;
			try
			{
				Graphics.Blit(tex, rt);
				RenderTexture.active = rt;
				read.ReadPixels(new Rect(0, 0, side, side), 0, 0);
				read.Apply();
				foreach (Color c in read.GetPixels())
				{
					sum += c;
				}
				sum /= side * side;
			}
			finally
			{
				RenderTexture.active = before;
				RenderTexture.ReleaseTemporary(rt);
				UnityEngine.Object.Destroy(read);
			}
			sum = Opaque(sum);
			averages[tex.GetInstanceID()] = sum;
			return sum;
		}

		private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);
	}
}
