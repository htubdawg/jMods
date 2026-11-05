using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HalfGripSlapPack
{
    internal sealed class WindowPane
    {
        internal readonly MeshFilter Source;
        internal readonly HashSet<int> Indices;
        internal float OpenFraction { get; private set; }
        private readonly Mesh mesh;
        private readonly List<PanePoint> opening;
        private readonly Vector3 origin, right, up;

        internal WindowPane(MeshFilter source, Transform car, IEnumerable<int> indices, Material material, List<Object> owned)
        {
            MeshSnapshot snapshot = MeshSnapshot.Read(source.sharedMesh);
            int[] triangles = indices.ToArray();
            Source = source; Indices = new HashSet<int>(triangles);
            Vector3[] all = snapshot.Vertices.Select(p => car.InverseTransformPoint(source.transform.TransformPoint(p))).ToArray();
            Vector3[] points = triangles.Distinct().Select(i => all[i]).ToArray();
            Vector3 center = points.Aggregate(Vector3.zero, (a, b) => a + b) / points.Length;
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < triangles.Length; i += 3)
                normal += Vector3.Cross(all[triangles[i + 1]] - all[triangles[i]], all[triangles[i + 2]] - all[triangles[i]]);
            normal.Normalize();
            Vector3 vertical = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            Vector3 horizontal = Vector3.Cross(vertical, normal).normalized;
            opening = WindowPolygon.Hull(points.Select(p => new PanePoint(Vector3.Dot(p - center, horizontal), Vector3.Dot(p - center, vertical))));
            origin = source.transform.InverseTransformPoint(car.TransformPoint(center));
            right = source.transform.InverseTransformVector(car.TransformVector(horizontal));
            up = source.transform.InverseTransformVector(car.TransformVector(vertical));
            var go = new GameObject("SlapPack.DoorGlass");
            owned.Add(go);
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform, false);
            mesh = new Mesh { name = "SlapPack moving door glass" };
            owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetOpen(0);
        }

        internal void SetOpen(float fraction)
        {
            OpenFraction = fraction;
            List<PanePoint> polygon = WindowPolygon.Lower(opening, fraction);
            mesh.Clear();
            if (polygon.Count < 3) return;
            mesh.vertices = polygon.Select(p => origin + right * p.X + up * p.Y).ToArray();
            mesh.triangles = Enumerable.Range(1, polygon.Count - 2).SelectMany(i => new[] { 0, i, i + 1 }).ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }
    }
}
