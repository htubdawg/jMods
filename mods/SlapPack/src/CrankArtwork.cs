using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
    internal static class WindowMaterials
    {
		/// <summary>Draws glass deposits before transparent guns/hands, retaining opaque depth occlusion.</summary>
		internal static Material Precipitation(Color color, Texture texture, List<Object> owned)
		{
			Material material = Transparent(color, texture, owned);
			// Transparent gun materials need not write depth. A later queue would
			// paint deposits over them regardless of their distance from the glass.
			material.renderQueue = (int)RenderQueue.Transparent - 1;
			return material;
		}

        internal static Material Transparent(Color color, Texture texture, List<Object> owned)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("No supported transparent glass shader is available.");
            var material = new Material(shader) { name = "SlapPack window material", renderQueue = (int)RenderQueue.Transparent };
            owned.Add(material);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            return material;
        }
    }

    internal sealed class CrankArtwork
    {
        // Landmark measured in the supplied stock cabin atlas; coordinates are normalized so the
        // actual texture need not be the same resolution as the diagnostic preview.
        internal static readonly Vector2 PivotUV = new Vector2(104f / 1024, 1 - 418f / 1024);
        internal readonly Texture2D Door, Stamp;
        internal readonly Rect UVRect;

        internal CrankArtwork(Texture source, List<Object> owned)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Door = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            owned.Add(Door);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                Door.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                Door.Apply();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
            int x0 = Mathf.RoundToInt(source.width * 91f / 1024), x1 = Mathf.RoundToInt(source.width * 119f / 1024);
            int y0 = Mathf.RoundToInt(source.height * (1 - 431f / 1024)), y1 = Mathf.RoundToInt(source.height * (1 - 381f / 1024));
            UVRect = Rect.MinMaxRect((float)x0 / source.width, (float)y0 / source.height, (float)x1 / source.width, (float)y1 / source.height);
            Stamp = new Texture2D(x1 - x0, y1 - y0, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            owned.Add(Stamp);
            Color[] pixels = Door.GetPixels();
            var stamp = new Color[(x1 - x0) * (y1 - y0)];
            int visiblePixels = 0;
            for (int y = y0; y < y1; y++)
            {
                Color background = pixels[y * source.width + Mathf.Max(0, x0 - 4)];
                for (int x = x0; x < x1; x++)
                {
                    Color pixel = pixels[y * source.width + x];
                    // The atlas has a dark door and a light crank. Drop the background from the
                    // rotating stamp; fill its original location in a private texture copy.
                    pixel.a = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) > 0.14f ? 1 : 0;
                    if (pixel.a > 0) visiblePixels++;
                    stamp[(y - y0) * (x1 - x0) + x - x0] = pixel;
                    pixels[y * source.width + x] = background;
                }
            }
            if (visiblePixels < 8) throw new InvalidOperationException("The cabin atlas has no recognizable stock crank artwork at its expected UV location.");
            Stamp.SetPixels(stamp);
            Stamp.Apply();
            Door.SetPixels(pixels);
            Door.filterMode = source.filterMode;
            Door.wrapMode = source.wrapMode;
            Door.Apply();
        }
    }

    internal sealed class CrankFace
    {
        internal MeshFilter Source;
        internal MeshRenderer Renderer;
        internal int MaterialIndex;
        internal Material OriginalMaterial;
        internal Vector3 Pivot, U, V, Normal;
        internal Vector3 WorldPivot => Source.transform.TransformPoint(Pivot);

        internal static List<CrankFace> Find(Transform model, Transform car, Dictionary<Mesh, MeshSnapshot> cache)
        {
            var result = new List<CrankFace>();
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(false))
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !filter.sharedMesh) continue;
                Material[] materials = renderer.sharedMaterials;
                bool atlas = false;
                foreach (Material material in materials)
                {
                    Texture texture = TextureOf(material);
                    if (texture && texture.name.IndexOf("uv interior (fara bord)", StringComparison.OrdinalIgnoreCase) >= 0) atlas = true;
                }
                if (!atlas) continue;
                if (!cache.TryGetValue(filter.sharedMesh, out MeshSnapshot snapshot))
                    cache.Add(filter.sharedMesh, snapshot = MeshSnapshot.Read(filter.sharedMesh));
                if (snapshot.UV.Length != snapshot.Vertices.Length) continue;
                for (int s = 0; s < snapshot.Triangles.Length && s < materials.Length; s++)
                {
                    Texture texture = TextureOf(materials[s]);
                    if (!texture || texture.name.IndexOf("uv interior (fara bord)", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    int[] indices = snapshot.Triangles[s];
                    for (int t = 0; t < indices.Length; t += 3)
                    {
                        int a = indices[t], b = indices[t + 1], c = indices[t + 2];
                        Vector2 ab = snapshot.UV[b] - snapshot.UV[a], ac = snapshot.UV[c] - snapshot.UV[a], point = CrankArtwork.PivotUV - snapshot.UV[a];
                        float det = ab.x * ac.y - ab.y * ac.x;
                        if (Mathf.Abs(det) < 0.000001f) continue;
                        float u = (point.x * ac.y - point.y * ac.x) / det, v = (ab.x * point.y - ab.y * point.x) / det;
                        if (u < -0.0001f || v < -0.0001f || u + v > 1.0001f) continue;
                        Vector3 va = snapshot.Vertices[a], vb = snapshot.Vertices[b] - va, vc = snapshot.Vertices[c] - va;
                        Vector3 normal = Vector3.Cross(vb, vc).normalized;
                        Vector3 worldNormal = filter.transform.TransformDirection(normal);
                        if (Mathf.Abs(Vector3.Dot(worldNormal, car.right)) < 0.7f) continue;
                        var face = new CrankFace { Source = filter, Renderer = renderer, MaterialIndex = s, OriginalMaterial = materials[s],
                            Pivot = va + vb * u + vc * v, U = (vb * ac.y - vc * ab.y) / det,
                            V = (vc * ab.x - vb * ac.x) / det, Normal = normal };
                        bool duplicate = result.Exists(f => (f.WorldPivot - face.WorldPivot).sqrMagnitude < 0.0001f);
                        if (!duplicate) result.Add(face);
                    }
                }
            }
            return result;
        }

        internal static Texture TextureOf(Material material)
        {
            if (!material) return null;
            Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
            return texture ? texture : material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        }

        internal Transform CreateVisual(CrankArtwork artwork, Camera camera, List<Object> owned)
        {
            if (Vector3.Dot(Source.transform.TransformDirection(Normal), camera.transform.position - WorldPivot) < 0) Normal = -Normal;
            var go = new GameObject("SlapPack.RotatingCrank");
            owned.Add(go);
            go.layer = Source.gameObject.layer;
            go.transform.SetParent(Source.transform, false);
            go.transform.localPosition = Pivot + Normal * Source.transform.InverseTransformVector(Source.transform.TransformDirection(Normal) * 0.003f).magnitude;
            var mesh = new Mesh { name = "SlapPack crank artwork" };
            owned.Add(mesh);
            Rect r = artwork.UVRect;
            Vector2 pivot = CrankArtwork.PivotUV;
            mesh.vertices = new[] { U * (r.xMin - pivot.x) + V * (r.yMin - pivot.y), U * (r.xMax - pivot.x) + V * (r.yMin - pivot.y),
                U * (r.xMax - pivot.x) + V * (r.yMax - pivot.y), U * (r.xMin - pivot.x) + V * (r.yMax - pivot.y) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WindowMaterials.Transparent(Color.white, artwork.Stamp, owned);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }
    }
}
