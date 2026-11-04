using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
	// Only sample the front glass. Geometry is expressed in car coordinates, so body
	// mesh import scales cannot stretch the blades or change their physical sweep.
	internal sealed class WiperWindshield
	{
		internal readonly MeshRenderer Source;
		internal readonly List<PanePoint> Opening;
		private readonly Vector3[] points;
		private readonly PanePoint[] projected;
		private readonly int[] triangles;
		private readonly Vector3 origin, right, up, normal;

		private WiperWindshield(MeshRenderer source, Vector3[] vertices, int[] indices)
		{
			Source = source; points = vertices; triangles = indices;
			origin = indices.Distinct().Aggregate(Vector3.zero, (sum, i) => sum + points[i]) / indices.Distinct().Count();
			Vector3 sumNormal = Vector3.zero;
			for (int i = 0; i < indices.Length; i += 3)
			{
				Vector3 n = Vector3.Cross(points[indices[i + 1]] - points[indices[i]], points[indices[i + 2]] - points[indices[i]]);
				sumNormal += n.z < 0 ? -n : n;
			}
			normal = sumNormal.normalized;
			right = Vector3.ProjectOnPlane(Vector3.right, normal).normalized;
			up = Vector3.Cross(normal, right).normalized;
			projected = points.Select(p => new PanePoint(Vector3.Dot(p - origin, right), Vector3.Dot(p - origin, up))).ToArray();
			Opening = WindowPolygon.Hull(indices.Distinct().Select(i => projected[i]));
		}

		internal static WiperWindshield Find(CarControllerNew car, CameraSwitch cameras)
		{
			WiperWindshield best = null;
			float bestArea = 0;
			foreach (MeshRenderer source in cameras.windshields ?? new MeshRenderer[0])
			{
				if (!source || !source.gameObject.activeInHierarchy) continue;
				MeshFilter filter = source.GetComponent<MeshFilter>();
				if (!filter || !filter.sharedMesh) continue;
				MeshSnapshot mesh = MeshSnapshot.Read(filter.sharedMesh);
				Vector3[] vertices = mesh.Vertices.Select(p => car.transform.InverseTransformPoint(filter.transform.TransformPoint(p))).ToArray();
				var selected = new List<int>();
				float area = 0;
				foreach (int[] submesh in mesh.Triangles)
					for (int i = 0; i < submesh.Length; i += 3)
					{
						int a = submesh[i], b = submesh[i + 1], c = submesh[i + 2];
						Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3;
						Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
						Vector3 n = cross.normalized;
						if (center.z <= 0 || center.y <= 0.5f || Mathf.Abs(n.z) < 0.35f || Mathf.Abs(n.x) > 0.55f) continue;
						selected.Add(a); selected.Add(b); selected.Add(c); area += cross.magnitude;
					}
				if (selected.Count < 6 || area <= bestArea) continue;
				best = new WiperWindshield(source, vertices, selected.ToArray()); bestArea = area;
			}
			if (best == null) throw new InvalidOperationException("Front windshield triangles could not be identified; no wipers were added.");
			return best;
		}

		// Interpolate the real glass triangles, including the stock windshield's shallow
		// center bend, rather than animating a flat quad through the glass.
		internal Vector3 Surface(PanePoint p, float clearance = 0.012f)
		{
			for (int i = 0; i < triangles.Length; i += 3)
			{
				int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
				PanePoint a = projected[ia], b = projected[ib], c = projected[ic];
				float determinant = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
				if (Mathf.Abs(determinant) < 0.000001f) continue;
				float u = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / determinant;
				float v = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / determinant;
				if (u < -0.001f || v < -0.001f || u + v > 1.001f) continue;
				return points[ia] * u + points[ib] * v + points[ic] * (1 - u - v) + normal * clearance;
			}
			return origin + right * p.X + up * p.Y + normal * clearance;
		}

		internal Vector3 Normal => normal;
		internal PanePoint ProjectVector(Vector3 v) => new PanePoint(Vector3.Dot(v, right), Vector3.Dot(v, up));

		internal Mesh WeatherMesh(WindshieldWeatherState field)
		{
			var mesh = new Mesh { name = "SlapPack precipitation on front glass" };
			mesh.indexFormat = points.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
			mesh.vertices = points.Select(p => p + normal * 0.006f).ToArray();
			mesh.uv = projected.Select(p => new Vector2((p.X - field.MinX) / field.SpanX, (p.Y - field.MinY) / field.SpanY)).ToArray();
			mesh.triangles = triangles;
			mesh.RecalculateBounds(); mesh.RecalculateNormals();
			return mesh;
		}

		/// <summary>Matches mesh sampling density to the water grid while following the real glass triangles.</summary>
		internal Mesh RefractionMesh(WindshieldWeatherState field, out PanePoint[] samples)
		{
			int width = WindshieldWeatherState.Width, height = WindshieldWeatherState.Height;
			var vertices = new List<Vector3>(); var samplePoints = new List<PanePoint>();
			var grid = Enumerable.Repeat(-1, (width + 1) * (height + 1)).ToArray();
			for (int y = 0; y <= height; y++)
				for (int x = 0; x <= width; x++)
				{
					var p = new PanePoint(field.MinX + field.SpanX * x / width, field.MinY + field.SpanY * y / height);
					bool inside = true;
					for (int i = 0; i < Opening.Count; i++)
					{
						PanePoint a = Opening[i], b = Opening[(i + 1) % Opening.Count];
						if ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X) < -0.000001f) { inside = false; break; }
					}
					if (!inside) continue;
					grid[y * (width + 1) + x] = vertices.Count;
					vertices.Add(Surface(p, 0.004f)); samplePoints.Add(p);
				}
			var faces = new List<int>(width * height * 6);
			for (int y = 0; y < height; y++)
				for (int x = 0; x < width; x++)
				{
					int i = y * (width + 1) + x;
					int a = grid[i], b = grid[i + 1], c = grid[i + width + 1], d = grid[i + width + 2];
					if (a >= 0 && b >= 0 && c >= 0) { faces.Add(a); faces.Add(b); faces.Add(c); }
					if (b >= 0 && c >= 0 && d >= 0) { faces.Add(b); faces.Add(d); faces.Add(c); }
				}
			samples = samplePoints.ToArray();
			var mesh = new Mesh { name = "SlapPack water-grid refraction surface" };
			mesh.vertices = vertices.ToArray(); mesh.triangles = faces.ToArray();
			mesh.RecalculateBounds(); mesh.RecalculateNormals();
			return mesh;
		}

	}

	internal sealed class WiperBlades
	{
		private readonly WiperWindshield glass;
		private readonly WiperGeometry[] geometry;
		private readonly Mesh[] meshes;
		private readonly Vector3[][] vertices;
		private readonly float width;
		private readonly Mesh spray;
		private readonly MeshRenderer sprayRenderer;
		private readonly Vector3[] drops = new Vector3[48];
		private float sprayClock;
		private float lastSweep = float.NaN;

		internal WiperBlades(Transform car, WiperWindshield surface, List<Object> owned)
		{
			glass = surface;
			width = glass.Opening.Max(p => p.X) - glass.Opening.Min(p => p.X);
			geometry = new[] { new WiperGeometry(glass.Opening, false), new WiperGeometry(glass.Opening, true) };
			Material rubber = Solid(new Color(0.055f, 0.06f, 0.065f), owned);
			Material metal = Solid(new Color(0.18f, 0.20f, 0.22f), owned);
			Material edge = Solid(new Color(0.34f, 0.37f, 0.39f), owned);
			meshes = new Mesh[2]; vertices = new Vector3[2][];
			for (int i = 0; i < 2; i++)
			{
				meshes[i] = CreateMesh(car, "SlapPack.WiperBlade." + i, new[] { rubber, metal, edge }, surface.Source.gameObject.layer, owned, out _);
				vertices[i] = new Vector3[28];
				meshes[i].vertices = vertices[i];
				meshes[i].subMeshCount = 3;
				meshes[i].SetTriangles(QuadIndices(0, 2, 6), 0);
				meshes[i].SetTriangles(QuadIndices(3, 4), 1);
				meshes[i].SetTriangles(QuadIndices(1, 5), 2);
			}
			Material mist = WindowMaterials.Transparent(new Color(0.72f, 0.83f, 0.86f, 0.45f), Texture2D.whiteTexture, owned);
			spray = CreateMesh(car, "SlapPack.WasherJets", new[] { mist }, surface.Source.gameObject.layer, owned, out sprayRenderer);
			spray.vertices = drops;
			spray.triangles = QuadIndices(Enumerable.Range(0, 12).ToArray());
			sprayRenderer.enabled = false;
			Step(0, false, 0);
		}

		private static Material Solid(Color color, List<Object> owned)
		{
			Material material = WindowMaterials.Transparent(color, Texture2D.whiteTexture, owned);
			material.name = "SlapPack wiper metal/rubber";
			material.SetFloat("_Surface", 0);
			material.SetFloat("_SrcBlend", (float)BlendMode.One);
			material.SetFloat("_DstBlend", (float)BlendMode.Zero);
			material.SetFloat("_ZWrite", 1);
			material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetOverrideTag("RenderType", "Opaque");
			material.renderQueue = (int)RenderQueue.Geometry;
			return material;
		}

		private static Mesh CreateMesh(Transform car, string name, Material[] materials, int layer, List<Object> owned, out MeshRenderer renderer)
		{
			var go = new GameObject(name); owned.Add(go); go.layer = layer;
			go.transform.SetParent(car, false);
			var mesh = new Mesh { name = name }; owned.Add(mesh); mesh.MarkDynamic();
			go.AddComponent<MeshFilter>().sharedMesh = mesh;
			renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
			renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
			return mesh;
		}

		private static int[] QuadIndices(params int[] quads) => quads.SelectMany(q => new[] { q * 4, q * 4 + 1, q * 4 + 2, q * 4, q * 4 + 2, q * 4 + 3 }).ToArray();

		/// <summary>Updates blade geometry only when its local sweep changes; washer animation remains independent.</summary>
		internal void Step(float sweep, bool washing, float seconds)
		{
			float w = width * 0.009f;
			if (sweep != lastSweep)
			{
				WiperGeometry.SweepAxes(sweep, out float c, out float s);
				for (int i = 0; i < 2; i++)
				{
					WiperGeometry g = geometry[i]; float r = g.Radius;
					// Seven broad, flat shapes: arm, edge, rubber, support, hinge and hub.
					Rect(vertices[i], 0, g, 0, -w, r * 0.66f, w, c, s, 0.018f);
					Rect(vertices[i], 1, g, r * 0.05f, w * 0.35f, r * 0.62f, w * 0.7f, c, s, 0.020f);
					Rect(vertices[i], 2, g, r * 0.35f, -w * 1.25f, r, w * 1.25f, c, s, 0.012f);
					Rect(vertices[i], 3, g, r * 0.40f, -w * 0.55f, r * 0.95f, w * 0.55f, c, s, 0.016f);
					Rect(vertices[i], 4, g, r * 0.61f, -w * 1.6f, r * 0.67f, w * 1.6f, c, s, 0.022f);
					Rect(vertices[i], 5, g, -w, -w, w, w, c, s, 0.025f);
					Rect(vertices[i], 6, g, -w * 1.6f, -w * 1.6f, w * 1.6f, w * 1.6f, c, s, 0.022f);
					meshes[i].vertices = vertices[i]; meshes[i].RecalculateBounds();
				}
				lastSweep = sweep;
			}
			sprayRenderer.enabled = washing;
			if (!washing) { sprayClock = 0; return; }
			sprayClock = (sprayClock + seconds) % 10;
			for (int jet = 0; jet < 2; jet++)
				for (int drop = 0; drop < 6; drop++)
				{
					float t = Mathf.Repeat(sprayClock * 2.5f + drop / 6f, 1);
					PanePoint pivot = geometry[jet].Pivot;
					PanePoint point = WasherSpray.Point(pivot, geometry[jet].Radius, width, jet == 0, drop, t);
					float x = point.X, y = point.Y;
					float halfWidth = width * 0.002f;
					float halfHeight = width * 0.008f;
					int start = (jet * 6 + drop) * 4;
					drops[start] = glass.Surface(new PanePoint(x - halfWidth, y - halfHeight), 0.024f);
					drops[start + 1] = glass.Surface(new PanePoint(x + halfWidth, y - halfHeight), 0.024f);
					drops[start + 2] = glass.Surface(new PanePoint(x + halfWidth, y + halfHeight), 0.024f);
					drops[start + 3] = glass.Surface(new PanePoint(x - halfWidth, y + halfHeight), 0.024f);
				}
			spray.vertices = drops; spray.RecalculateBounds();
		}

		private void Rect(Vector3[] target, int quad, WiperGeometry g, float x0, float y0, float x1, float y1, float c, float s, float depth)
		{
			int i = quad * 4;
			target[i] = glass.Surface(g.Point(x0, y0, c, s), depth);
			target[i + 1] = glass.Surface(g.Point(x1, y0, c, s), depth);
			target[i + 2] = glass.Surface(g.Point(x1, y1, c, s), depth);
			target[i + 3] = glass.Surface(g.Point(x0, y1, c, s), depth);
		}
	}
}
