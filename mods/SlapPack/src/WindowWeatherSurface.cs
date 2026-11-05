using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
	/// <summary>One connected side/rear glass patch, retaining its original triangle footprint.</summary>
	internal sealed class WindowWeatherSurface
	{
		internal readonly MeshRenderer Source;
		internal readonly int[] Indices;
		internal readonly WindowWeatherState Field;
		internal readonly Vector3 Normal, Right, Up;
		private readonly Vector3 origin;
		private readonly List<PanePoint> opening;
		private readonly PanePoint[] projected;
		private readonly Mesh mesh;
		private readonly MeshRenderer renderer;
		private readonly Texture2D texture;
		private readonly Color32[] pixels = new Color32[WindowWeatherState.Width * WindowWeatherState.Height];
		private float lastOpen = -1;
		private bool warnedLayer;
		internal int Layer => renderer.gameObject.layer;
		internal bool Visible => renderer.enabled && renderer.gameObject.activeInHierarchy;

		private WindowWeatherSurface(Transform car, MeshRenderer source, Vector3[] points, int[] indices, bool rear, List<Object> owned, int seed)
		{
			Source = source; Indices = indices;
			origin = indices.Distinct().Aggregate(Vector3.zero, (sum, i) => sum + points[i]) / indices.Distinct().Count();
			Normal = Vector3.Cross(points[indices[1]] - points[indices[0]], points[indices[2]] - points[indices[0]]).normalized;
			if (rear ? Normal.z > 0 : Normal.x * origin.x < 0) Normal = -Normal;
			Up = Vector3.ProjectOnPlane(Vector3.up, Normal).normalized;
			Right = Vector3.Cross(Up, Normal).normalized;
			projected = points.Select(p => new PanePoint(Vector3.Dot(p - origin, Right), Vector3.Dot(p - origin, Up))).ToArray();
			opening = WindowPolygon.Hull(indices.Distinct().Select(i => projected[i]));
			Field = new WindowWeatherState(opening, rear, seed);
			texture = new Texture2D(WindowWeatherState.Width, WindowWeatherState.Height, TextureFormat.RGBA32, false)
				{ name = "SlapPack side/rear precipitation", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
			owned.Add(texture); texture.SetPixels32(pixels); texture.Apply(false, false);
			Material material = WindowMaterials.Transparent(Color.white, texture, owned); material.renderQueue = 3001;
			var go = new GameObject(rear ? "SlapPack.RearWeather" : "SlapPack.SideWeather"); owned.Add(go);
			// Follow the source's hierarchy for body activation and destruction, while
			// converting car-space geometry into its unmodified local coordinates.
			go.layer = source.gameObject.layer; go.transform.SetParent(source.transform, false);
			mesh = new Mesh { name = go.name }; owned.Add(mesh);
			go.AddComponent<MeshFilter>().sharedMesh = mesh;
			renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
			renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
			Rebuild(car, 0);
		}

		/// <summary>Finds coplanar connected patches, including rear quarter panes, without selecting the front glass.</summary>
		internal static List<WindowWeatherSurface> Find(CarControllerNew car, CameraSwitch cameras, List<Object> owned)
		{
			var result = new List<WindowWeatherSurface>();
			foreach (MeshRenderer source in (cameras.windshields ?? new MeshRenderer[0]).Distinct())
			{
				if (!source || !source.gameObject.activeInHierarchy) continue;
				MeshFilter filter = source.GetComponent<MeshFilter>();
				if (!filter || !filter.sharedMesh) continue;
				MeshSnapshot snapshot = MeshSnapshot.Read(filter.sharedMesh);
				Vector3[] points = snapshot.Vertices.Select(p => car.transform.InverseTransformPoint(source.transform.TransformPoint(p))).ToArray();
				var remaining = new List<int[]>();
				var seen = new HashSet<(Vector3, Vector3, Vector3)>();
				foreach (int[] submesh in snapshot.Triangles)
					for (int i = 0; i < submesh.Length; i += 3)
					{
						int a = submesh[i], b = submesh[i + 1], c = submesh[i + 2];
						Vector3 center = (points[a] + points[b] + points[c]) / 3;
						Vector3 n = Vector3.Cross(points[b] - points[a], points[c] - points[a]).normalized;
						bool side = Mathf.Abs(n.x) >= 0.65f;
						bool rear = center.z < 0 && Mathf.Abs(n.z) >= 0.35f && Mathf.Abs(n.x) <= 0.55f;
						if (center.y <= 0.5f || !(side || rear)) continue;
						// Imported glass can contain both windings or duplicated submesh
						// faces. Our material is two-sided: drawing either twice would
						// compound alpha beyond the intended visibility ceiling.
						Vector3[] key = new[] { points[a], points[b], points[c] }.OrderBy(p => p.x).ThenBy(p => p.y).ThenBy(p => p.z).ToArray();
						if (seen.Add((key[0], key[1], key[2]))) remaining.Add(new[] { a, b, c });
					}
				while (remaining.Count > 0)
				{
					var group = new List<int>(remaining[0]); remaining.RemoveAt(0);
					Vector3 normal = Vector3.Cross(points[group[1]] - points[group[0]], points[group[2]] - points[group[0]]).normalized;
					bool changed;
					do
					{
						changed = false;
						for (int i = remaining.Count - 1; i >= 0; i--)
						{
							int[] t = remaining[i];
							Vector3 n = Vector3.Cross(points[t[1]] - points[t[0]], points[t[2]] - points[t[0]]).normalized;
							if (Mathf.Abs(Vector3.Dot(normal, n)) < 0.995f || t.Any(v => Mathf.Abs(Vector3.Dot(points[v] - points[group[0]], normal)) > 0.002f)) continue;
							if (!t.Any(v => group.Any(g => (points[g] - points[v]).sqrMagnitude < 0.000001f))) continue;
							group.AddRange(t); remaining.RemoveAt(i); changed = true;
						}
					} while (changed);
					if (group.Distinct().Count() < 3) continue;
					result.Add(new WindowWeatherSurface(car.transform, source, points, group.ToArray(), Mathf.Abs(normal.x) < 0.65f, owned, 8191 + result.Count * 137));
				}
			}
			return result;
		}

		/// <summary>Moves deposits with the glass and clips every source triangle to the door opening.</summary>
		private void Rebuild(Transform car, float open)
		{
			lastOpen = open;
			var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
			if (open < 1)
				for (int i = 0; i < Indices.Length; i += 3)
				{
					float offset = open * Field.SpanY;
					var triangle = new List<PanePoint>();
					for (int j = 0; j < 3; j++) { PanePoint p = projected[Indices[i + j]]; triangle.Add(new PanePoint(p.X, p.Y - offset)); }
					// The field hull bounds the aperture; the original triangles retain any
					// missing corners rather than drawing a single hull across them.
					List<PanePoint> polygon = WindowPolygon.Clip(triangle, opening);
					int start = vertices.Count;
					foreach (PanePoint p in polygon)
					{
						Vector3 point = origin + Right * p.X + Up * p.Y + Normal * 0.006f;
						vertices.Add(Source.transform.InverseTransformPoint(car.TransformPoint(point)));
						uv.Add(new Vector2((p.X - Field.MinX) / Field.SpanX, (p.Y + offset - Field.MinY) / Field.SpanY));
					}
					for (int j = 1; j + 1 < polygon.Count; j++) { triangles.Add(start); triangles.Add(start + j); triangles.Add(start + j + 1); }
				}
			mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
			mesh.RecalculateNormals(); mesh.RecalculateBounds();
		}

		internal void Step(CarControllerNew car, WindshieldPrecipitation weather, Vector3 relative, float dt, float open)
		{
			if (!Source) { renderer.enabled = false; return; }
			if (open != lastOpen) Rebuild(car.transform, open);
			// CameraSwitch deliberately disables the original glass renderer in
			// cockpit view. The owned precipitation must remain visible there,
			// just like the windshield overlay; only body activation gates it.
			renderer.enabled = Source.gameObject.activeInHierarchy && open < 1;
			Vector3 flow = car.transform.InverseTransformDirection(Vector3.down) * (Field.Rear ? 0.060f : 0.10f)
				+ relative * (Field.Rear ? 0.0008f : 0.0045f);
			Field.Step(dt, weather.Rain, weather.Snow, Mathf.Max(0, -Vector3.Dot(relative, Normal)), Vector3.Dot(flow, Right), Vector3.Dot(flow, Up), open);
			if (!renderer.enabled) return;
			for (int k = 0; k < pixels.Length; k++)
			{
				float white = Field.Snow[k] / Mathf.Max(0.001f, Field.Snow[k] + Field.Water[k] + Field.Trail[k]);
				float shine = Field.Shine(k);
				pixels[k] = new Color32((byte)(Mathf.Lerp(shine * 0.8f, 0.88f, white) * 255),
					(byte)(Mathf.Lerp(shine * 0.9f, 0.93f, white) * 255), (byte)(Mathf.Lerp(shine, 0.96f, white) * 255), (byte)(Field.Opacity(k) * 255));
			}
			texture.SetPixels32(pixels); texture.Apply(false, false);
		}

		/// <summary>Keeps owned overlays in both camera masks without editing the source layer or cameras.</summary>
		internal void IncludeMirror(Camera cockpit, Camera mirror, Action<string> warning)
		{
			if (!Source || !cockpit || !mirror) return;
			int layer = WindowWeatherLayer.Select(Source.gameObject.layer, cockpit.cullingMask, mirror.cullingMask);
			if (layer >= 0) renderer.gameObject.layer = layer;
			else if (!warnedLayer)
			{
				warnedLayer = true;
				warning("Window weather: cockpit and rear-view cameras share no render layers; mirror weather cannot be included without changing camera masks.");
			}
		}

		/// <summary>Counts wet field samples projected into the real mirror, including clipping and near/far planes.</summary>
		internal int VisibleWetSamples(Transform car, Camera camera)
		{
			int count = 0;
			for (int y = 0; y < WindowWeatherState.Height; y++)
				for (int x = 0; x < WindowWeatherState.Width; x++)
				{
					int k = y * WindowWeatherState.Width + x;
					if (!Field.Inside[k] || Field.Opacity(k) < 0.02f) continue;
					PanePoint p = Field.Point(x, y);
					if (!Covers(p)) continue;
					p.Y -= lastOpen * Field.SpanY;
					if (!Field.Contains(p)) continue;
					Vector3 v = camera.WorldToViewportPoint(car.TransformPoint(origin + Right * p.X + Up * p.Y + Normal * 0.006f));
					if (v.z >= camera.nearClipPlane && v.z <= camera.farClipPlane && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1) count++;
				}
			return count;
		}

		/// <summary>Excludes holes and missing corners from mirror diagnostics using the source triangle footprint.</summary>
		private bool Covers(PanePoint p)
		{
			for (int i = 0; i < Indices.Length; i += 3)
			{
				PanePoint a = projected[Indices[i]], b = projected[Indices[i + 1]], c = projected[Indices[i + 2]];
				float ab = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
				float bc = (c.X - b.X) * (p.Y - b.Y) - (c.Y - b.Y) * (p.X - b.X);
				float ca = (a.X - c.X) * (p.Y - c.Y) - (a.Y - c.Y) * (p.X - c.X);
				if ((ab >= -0.000001f && bc >= -0.000001f && ca >= -0.000001f) || (ab <= 0.000001f && bc <= 0.000001f && ca <= 0.000001f)) return true;
			}
			return false;
		}
	}
}
