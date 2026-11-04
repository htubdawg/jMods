using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
    internal sealed class SignalDashLamp
    {
        private readonly GameObject root;
        private readonly MeshRenderer glyph, glow;
        private readonly Material on, off;
        private bool illuminated;

        internal SignalDashLamp(Transform parent, Vector3 center, bool right, int layer, List<Object> owned, float scale = 1)
        {
            root = new GameObject(right ? "SlapPack.RightIndicator" : "SlapPack.LeftIndicator");
            owned.Add(root); root.layer = layer;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;
            root.transform.localScale = Vector3.one * scale;
            Material bezel = TurnSignalVisuals.Material(new Color(0.075f, 0.085f, 0.08f, 1), owned);
            TurnSignalVisuals.Polygon("IndicatorBezel", root.transform, Bevel(0.25f, 0.16f, 0.035f, 0), bezel, layer, owned);
            TurnSignalVisuals.Polygon("RecessedLens", root.transform, Bevel(0.223f, 0.134f, 0.025f, -0.008f),
                TurnSignalVisuals.Material(new Color(0.018f, 0.032f, 0.023f, 1), owned), layer, owned);
            TurnSignalVisuals.Rectangle("LensHighlight", root.transform, -0.18f, 0.117f, 0.18f, 0.126f, -0.01f,
                TurnSignalVisuals.Material(new Color(0.14f, 0.17f, 0.15f, 0.6f), owned), layer, owned);

            Texture2D icon = Icon(false), halo = Icon(true);
            owned.Add(icon); owned.Add(halo);
            on = WindowMaterials.Transparent(new Color(0.28f, 0.96f, 0.34f, 1), icon, owned);
            off = WindowMaterials.Transparent(new Color(0.045f, 0.14f, 0.065f, 1), icon, owned);
            Material bloom = WindowMaterials.Transparent(new Color(0.15f, 0.85f, 0.24f, 0.3f), halo, owned);
            glow = Quad("IndicatorGlow", 0.195f, 0.117f, -0.012f, bloom, right, layer, owned);
            glyph = Quad("IndicatorArrow", 0.18f, 0.108f, -0.014f, off, right, layer, owned);
            glow.enabled = false;
        }

        internal void Set(bool lit, bool visible)
        {
            if (!root) return;
            if (root.activeSelf != visible) root.SetActive(visible);
            if (lit == illuminated) return;
            illuminated = lit;
            glyph.sharedMaterial = lit ? on : off;
            glow.enabled = lit;
        }

        private MeshRenderer Quad(string name, float x, float y, float z, Material material, bool right, int layer, List<Object> owned)
        {
            MeshRenderer renderer = TurnSignalVisuals.Rectangle(name, root.transform, -x, -y, x, y, z, material, layer, owned);
            renderer.GetComponent<MeshFilter>().sharedMesh.uv = right
                ? new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }
                : new[] { Vector2.right, Vector2.zero, Vector2.up, Vector2.one };
            return renderer;
        }

        private static Vector3[] Bevel(float x, float y, float cut, float z) => new[] {
            new Vector3(-x + cut, -y, z), new Vector3(x - cut, -y, z), new Vector3(x, -y + cut, z),
            new Vector3(x, y - cut, z), new Vector3(x - cut, y, z), new Vector3(-x + cut, y, z),
            new Vector3(-x, y - cut, z), new Vector3(-x, -y + cut, z)
        };

        private static Texture2D Icon(bool blurred)
        {
            const int width = 19, height = 13;
            var mask = new bool[width * height];
            int[] xs = { 3, 10, 10, 16, 10, 10, 3 }, ys = { 4, 4, 2, 6, 10, 8, 8 };
            for (int edge = 0; edge < xs.Length; edge++)
            {
                int next = (edge + 1) % xs.Length;
                int x = xs[edge], y = ys[edge], endX = xs[next], endY = ys[next];
                int dx = Mathf.Abs(endX - x), sx = x < endX ? 1 : -1;
                int dy = -Mathf.Abs(endY - y), sy = y < endY ? 1 : -1, error = dx + dy;
                while (true)
                {
                    mask[y * width + x] = true;
                    if (x == endX && y == endY) break;
                    int twice = 2 * error;
                    if (twice >= dy) { error += dy; x += sx; }
                    if (twice <= dx) { error += dx; y += sy; }
                }
            }
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float alpha = mask[y * width + x] ? 1 : 0;
                    if (blurred)
                    {
                        alpha = 0;
                        for (int oy = -1; oy <= 1; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                                if (x + ox >= 0 && x + ox < width && y + oy >= 0 && y + oy < height && mask[(y + oy) * width + x + ox]) alpha += 1f / 9;
                    }
                    pixels[y * width + x] = new Color(1, 1, 1, alpha);
                }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) {
                name = blurred ? "SlapPack indicator glow" : "SlapPack etched arrow",
                filterMode = blurred ? FilterMode.Bilinear : FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels); texture.Apply();
            return texture;
        }
    }
}
