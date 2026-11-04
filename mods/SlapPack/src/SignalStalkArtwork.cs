using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
    internal static class SignalStalkArtwork
    {
        internal static MeshRenderer Attach(Transform parent, float length, int layer, List<Object> owned, byte[] pixels = null, bool right = false)
        {
            var texture = new Texture2D(SignalStalkLayout.TextureWidth, SignalStalkLayout.TextureHeight, TextureFormat.RGBA32, false) {
                name = "SlapPack hand-pixeled signal stalk", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            owned.Add(texture);
            byte[] indicesInPalette = pixels ?? SignalStalkPixels.Build();
            var colors = new Color32[indicesInPalette.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                uint rgba = SignalStalkPixels.Palette[indicesInPalette[i]];
                colors[i] = new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
            }
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            var layout = new SignalStalkLayout(length);
            float[] cuts = { SignalStalkLayout.SourceLeft, SignalStalkLayout.GripCut, SignalStalkLayout.BootCut, SignalStalkLayout.SourceRight };
            var vertices = new Vector3[12]; var uv = new Vector2[12]; var indices = new int[18];
            for (int segment = 0; segment < 3; segment++)
            {
                int first = segment * 4, triangle = segment * 6;
                float x0 = cuts[segment], x1 = cuts[segment + 1];
                float y0 = SignalStalkLayout.SourceBottom, y1 = SignalStalkLayout.SourceTop;
                vertices[first] = new Vector3(layout.MapX(x0), SignalStalkLayout.MapY(y0), 0);
                vertices[first + 1] = new Vector3(layout.MapX(x1), SignalStalkLayout.MapY(y0), 0);
                vertices[first + 2] = new Vector3(layout.MapX(x1), SignalStalkLayout.MapY(y1), 0);
                vertices[first + 3] = new Vector3(layout.MapX(x0), SignalStalkLayout.MapY(y1), 0);
                if (right) for (int i = first; i < first + 4; i++) vertices[i].x = -vertices[i].x;
                uv[first] = new Vector2(x0 / texture.width, y0 / texture.height);
                uv[first + 1] = new Vector2(x1 / texture.width, y0 / texture.height);
                uv[first + 2] = new Vector2(x1 / texture.width, y1 / texture.height);
                uv[first + 3] = new Vector2(x0 / texture.width, y1 / texture.height);
                indices[triangle] = first; indices[triangle + 1] = first + 1; indices[triangle + 2] = first + 2;
                indices[triangle + 3] = first; indices[triangle + 4] = first + 2; indices[triangle + 5] = first + 3;
            }
            var mesh = new Mesh { name = "SlapPack pixel-art signal stalk" };
            owned.Add(mesh);
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices;
            mesh.RecalculateBounds(); mesh.RecalculateNormals();
            var go = new GameObject(right ? "SlapPack.WiperStalkArtwork" : "SlapPack.SignalStalkArtwork");
            owned.Add(go); go.layer = layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            // This is an opaque silhouette with holes, not translucent glass. Write depth
            // before transparent gauge needles draw, while preserving the wheel's nearer depth.
            // Otherwise transparent sorting can put a needle over the shaft despite its position.
            Material material = WindowMaterials.Transparent(Color.white, texture, owned);
            material.name = "SlapPack cutout signal stalk";
            material.SetFloat("_Surface", 0);
            material.SetFloat("_AlphaClip", 1);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1);
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
