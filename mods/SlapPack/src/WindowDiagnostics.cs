using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using HalfGripTools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
    // First identify the existing cranks and panes. The reference scene dumps collapse generic
    // meshes and contain no UVs/vertices, so they cannot establish safe crank pivots or pane bounds.
    public sealed class WindowDiagnostics : MonoBehaviour
    {
        private ModContext context;
        private ConfigEntry<bool> enabledReport;
        private float nextScan;
        private int reportedCar;

        internal void Initialize(ModContext ctx)
        {
            context = ctx;
            enabledReport = ctx.Config.Bind("Windows", "WriteGeometryReport", false,
                "Window-crank development: export the local car's cabin hierarchy, readable meshes and material previews once per spawned car. Does not alter the windows.");
            ctx.OnSceneLoaded(scene => { reportedCar = 0; nextScan = Time.unscaledTime + 1f; });
        }

        private void Update()
        {
            if (context == null || !enabledReport.Value || Time.unscaledTime < nextScan) return;
			if (!ChassisCompatibility.AllowsAdditions) return;
            nextScan = Time.unscaledTime + 1f;
            foreach (CarControllerNew car in Object.FindObjectsByType<CarControllerNew>(FindObjectsSortMode.None))
            {
                if (!car.isPlayer || car.GetInstanceID() == reportedCar) continue;
                foreach (Grabber grabber in Object.FindObjectsByType<Grabber>(FindObjectsSortMode.None))
                {
                    if (grabber.gameObject.scene != car.gameObject.scene) continue;
                    Transform cockpit = grabber.transform;
                    while (cockpit && cockpit.name != "CarInterior") cockpit = cockpit.parent;
                    if (!cockpit) continue;
                    // Mark before exporting: a failed read must not retry and spam logs every second.
                    reportedCar = car.GetInstanceID();
                    try { Write(car, cockpit); }
                    catch (Exception error) { context.Log.LogError("Window geometry report failed: " + error); }
                    return;
                }
            }
        }

        private void Write(CarControllerNew car, Transform cockpit)
        {
            string folder = Path.Combine(Paths.BepInExRootPath, "SlapPack", "WindowDiagnostics",
                Safe(car.gameObject.scene.name) + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            var text = new StringBuilder();
            text.AppendLine("Slap Pack window geometry report. Local reference data; do not commit game assets to the mod repository.");
            text.AppendLine("Scene: " + car.gameObject.scene.name);
            text.AppendLine("Car: " + car.name);
            text.AppendLine("TRS are local to the parent. Mesh car coordinates use the player's CarControllerNew transform.");
            var textures = new HashSet<Texture>();
            var meshes = new Dictionary<Mesh, MeshSnapshot>();
            var roots = new List<Transform> { cockpit, car.transform };
            foreach (Transform root in roots) Describe(root, car.transform, "", text, textures, meshes, folder);
            File.WriteAllText(Path.Combine(folder, "geometry.txt"), text.ToString());
            context.Log.LogInfo("Window-crank geometry report written to " + folder);
        }

        private static void Describe(Transform node, Transform car, string indent, StringBuilder text,
            HashSet<Texture> textures, Dictionary<Mesh, MeshSnapshot> meshes, string folder)
        {
            text.AppendLine(indent + node.name + " active=" + node.gameObject.activeSelf + " layer=" + LayerMask.LayerToName(node.gameObject.layer)
                + " position=" + V(node.localPosition) + " rotation=" + V(node.localEulerAngles) + " scale=" + V(node.localScale));
            foreach (Component component in node.GetComponents<Component>())
                if (component) text.AppendLine(indent + "  component " + component.GetType().FullName);
            SpriteRenderer sprite = node.GetComponent<SpriteRenderer>();
            if (sprite && sprite.sprite)
                text.AppendLine(indent + "  sprite " + sprite.sprite.name + " bounds=" + V(sprite.sprite.bounds.center) + "/" + V(sprite.sprite.bounds.size));
            MeshFilter filter = node.GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh)
            {
                Mesh mesh = filter.sharedMesh;
                text.AppendLine(indent + "  mesh " + mesh.name + " vertices=" + mesh.vertexCount + " readable=" + mesh.isReadable
                    + " bounds=" + V(mesh.bounds.center) + "/" + V(mesh.bounds.size));
                try
                {
                    if (!meshes.TryGetValue(mesh, out MeshSnapshot snapshot))
                    {
                        snapshot = MeshSnapshot.Read(mesh);
                        meshes.Add(mesh, snapshot);
                    }
                    text.AppendLine(indent + "  mesh data source=" + (mesh.isReadable ? "CPU" : "GPU"));
                    Vector3[] vertices = snapshot.Vertices;
                    Vector2[] uv = snapshot.UV;
                    for (int i = 0; i < vertices.Length; i++)
                        text.AppendLine(indent + "    v" + i + " local=" + V(vertices[i]) + " car=" + V(car.InverseTransformPoint(node.TransformPoint(vertices[i])))
                            + (i < uv.Length ? " uv=" + F(uv[i].x) + "," + F(uv[i].y) : ""));
                    for (int i = 0; i < snapshot.Triangles.Length; i++)
                        text.AppendLine(indent + "    submesh" + i + " triangles=" + string.Join(",", snapshot.Triangles[i]));
                }
                catch (Exception error) { text.AppendLine(indent + "  Mesh data unavailable: " + error.Message); }
            }
            MeshRenderer renderer = node.GetComponent<MeshRenderer>();
            if (renderer)
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (!material) continue;
                    text.AppendLine(indent + "  material " + material.name + " shader=" + (material.shader ? material.shader.name : "none"));
                    Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                    if (!texture && material.HasProperty("_MainTex")) texture = material.GetTexture("_MainTex");
                    if (!texture || !textures.Add(texture)) continue;
                    string name = "texture-" + textures.Count + "-" + Safe(texture.name) + ".png";
                    text.AppendLine(indent + "  texture " + texture.name + " preview=" + name);
                    try { SaveTexture(texture, Path.Combine(folder, name)); }
                    catch (Exception error) { text.AppendLine(indent + "  Texture preview unavailable: " + error.Message); }
                }
            for (int i = 0; i < node.childCount; i++) Describe(node.GetChild(i), car, indent + "  ", text, textures, meshes, folder);
        }

        private static void SaveTexture(Texture source, string file)
        {
            float scale = Mathf.Min(1f, 1024f / Mathf.Max(source.width, source.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Texture2D copy = null;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                copy.Apply();
                File.WriteAllBytes(file, copy.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (copy) Object.Destroy(copy);
            }
        }

        private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string V(Vector3 value) => F(value.x) + "," + F(value.y) + "," + F(value.z);
        private static string Safe(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace('/', '_').Replace('\\', '_');
        }
    }
}
