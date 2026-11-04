using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
    internal static class TurnSignalVisuals
    {
        internal static MeshRenderer Polygon(string name, Transform parent, Vector3[] points, Material material,
            int layer, List<Object> owned)
        {
            var go = new GameObject(name);
            owned.Add(go);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            owned.Add(mesh);
            mesh.vertices = points;
            mesh.triangles = Enumerable.Range(1, points.Length - 2).SelectMany(i => new[] { 0, i, i + 1 }).ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        internal static Material Material(Color color, List<Object> owned)
        {
            Material material = WindowMaterials.Transparent(color, Texture2D.whiteTexture, owned);
            material.name = "SlapPack turn signal";
            return material;
        }

        internal static MeshRenderer Rectangle(string name, Transform parent, float x0, float y0, float x1, float y1,
            float z, Material material, int layer, List<Object> owned) =>
            Polygon(name, parent, new[] { new Vector3(x0, y0, z), new Vector3(x1, y0, z),
                new Vector3(x1, y1, z), new Vector3(x0, y1, z) }, material, layer, owned);

        internal static SignalDashLamp Arrow(Transform parent, Vector3 center, bool right, int layer, List<Object> owned, float scale = 1) =>
            new SignalDashLamp(parent, center, right, layer, owned, scale);

        internal static void SetArrow(SignalDashLamp arrow, bool lit, bool visible)
        {
            if (arrow != null) arrow.Set(lit, visible);
        }

        internal static List<MeshRenderer> Exterior(CarControllerNew car, bool left, Material material, List<Object> owned)
        {
            var result = new List<MeshRenderer>();
            foreach (MeshRenderer source in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!source.gameObject.activeInHierarchy) continue;
                bool front = source.name == "BecuriFata" || source.name == "HeadlightsCar2";
                bool rear = source.name == "BecuriSpate" || source.name == "TailLightsCar2";
                if (!front && !rear) continue;
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = car.transform.InverseTransformPoint(source.transform.TransformPoint(corner));
                    min = Vector3.Min(min, point); max = Vector3.Max(max, point);
                }
                float x = Mathf.Lerp(min.x, max.x, left ? 0.1f : 0.9f);
                float y = (min.y + max.y) * 0.5f;
                float z = front ? max.z + 0.02f : min.z - 0.02f;
                Vector3[] corners = { new Vector3(x - 0.10f, y - 0.055f, z), new Vector3(x + 0.10f, y - 0.055f, z),
                    new Vector3(x + 0.10f, y + 0.055f, z), new Vector3(x - 0.10f, y + 0.055f, z) };
                MeshRenderer lamp = Polygon("SlapPack.AmberSignal", source.transform,
                    corners.Select(p => source.transform.InverseTransformPoint(car.transform.TransformPoint(p))).ToArray(),
                    material, source.gameObject.layer, owned);
                lamp.enabled = false;
                result.Add(lamp);
            }
            return result;
        }
    }
}
