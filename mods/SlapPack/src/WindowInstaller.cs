using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HalfGripTools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
    [DefaultExecutionOrder(-1000)]
    public sealed class WindowInstaller : MonoBehaviour
    {
        private ModContext context;
        private ConfigEntry<bool> enableWindows;
        private ConfigEntry<float> opacity, turns;
        private float nextScan;
        private int attemptedModel;
        private Transform boundModel;
        private CameraSwitch boundCameras;
        private readonly List<Object> owned = new List<Object>();
        private readonly List<WindowPane> panes = new List<WindowPane>();
        private readonly List<WindowCrank> cranks = new List<WindowCrank>();
        private readonly List<Tuple<MeshRenderer, Material, Material>> replacements = new List<Tuple<MeshRenderer, Material, Material>>();

        internal void Initialize(ModContext ctx)
        {
            context = ctx;
            enableWindows = ctx.Config.Bind("Windows", "Enabled", true, "Enable stock driver/passenger window-crank prototype. Windows start fully raised each race.");
            opacity = ctx.Config.Bind("Windows", "TintOpacity", 0.04f,
                new ConfigDescription("Minimal blue-gray door glass opacity; windshield and rear glass are unchanged. Applied when the car is initialized.", new AcceptableValueRange<float>(0, 0.15f)));
            turns = ctx.Config.Bind("Windows", "TurnsForFullTravel", 3f,
                new ConfigDescription("Clockwise crank turns to fully lower a door window. Applied when the car is initialized.", new AcceptableValueRange<float>(1, 8)));
            ctx.OnSceneLoaded(scene => { RemoveWindows(); attemptedModel = 0; nextScan = Time.unscaledTime + 1; });
        }

        private void Update()
        {
            if (context == null) return;
			if (!ChassisCompatibility.AllowsAdditions) { RemoveWindows(); attemptedModel = 0; return; }
            if (!enableWindows.Value) { RemoveWindows(); attemptedModel = 0; return; }
            if (boundModel && boundCameras && boundCameras.interiorModels != null &&
                !boundCameras.interiorModels.Any(m => m && m != boundModel && m.gameObject.activeInHierarchy))
            {
                foreach (WindowCrank crank in cranks) if (crank) crank.Project();
                return;
            }
            if (owned.Count > 0) RemoveWindows();
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 1;
            foreach (CarControllerNew car in Object.FindObjectsByType<CarControllerNew>(FindObjectsSortMode.None))
            {
                if (!car.isPlayer) continue;
                CameraSwitch cameras = car.GetComponent<CameraSwitch>();
                if (!cameras || cameras.interiorModels == null) continue;
                Transform model = cameras.interiorModels.FirstOrDefault(m => m && m.gameObject.activeInHierarchy);
                if (!model || model.GetInstanceID() == attemptedModel) continue;
                Camera camera = car.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.name == "Main Camera");
                Grabber hand = Object.FindObjectsByType<Grabber>(FindObjectsSortMode.None).FirstOrDefault(g => g.gameObject.scene == car.gameObject.scene && g.viewCamera);
                if (!camera || !hand || !camera.enabled) continue;
                attemptedModel = model.GetInstanceID();
                try { Attach(car, cameras, model, camera, hand); }
                catch (Exception error) { RemoveWindows(); context.Log.LogWarning("Window prototype could not bind this cabin: " + error); }
                return;
            }
        }

        private void Attach(CarControllerNew car, CameraSwitch cameras, Transform model, Camera camera, Grabber hand)
        {
            var cache = new Dictionary<Mesh, MeshSnapshot>();
            List<CrankFace> faces = CrankFace.Find(model, car.transform, cache);
            CrankFace driver = faces.FirstOrDefault(f => car.transform.InverseTransformPoint(f.WorldPivot).x < 0);
            CrankFace passenger = faces.FirstOrDefault(f => car.transform.InverseTransformPoint(f.WorldPivot).x > 0);
            if (driver == null || passenger == null) throw new InvalidOperationException("Both stock door crank UV landmarks were not found; original cabin left unchanged.");
            var artwork = new Dictionary<Texture, CrankArtwork>();
            Material glassMaterial = WindowMaterials.Transparent(new Color(0.62f, 0.74f, 0.80f, opacity.Value), Texture2D.whiteTexture, owned);
            foreach (CrankFace face in new[] { driver, passenger })
            {
                WindowPane pane = FindPane(car.transform, cameras, face, glassMaterial, cache);
                Material original = face.OriginalMaterial;
                Texture texture = CrankFace.TextureOf(original);
                if (!artwork.TryGetValue(texture, out CrankArtwork art)) artwork.Add(texture, art = new CrankArtwork(texture, owned));
                // Both sides can live in the same renderer/material. Replace it once, not once per door.
                if (!replacements.Any(r => r.Item1 == face.Renderer && r.Item2 == original))
                {
                    var clone = new Material(original) { name = original.name + " (SlapPack crank removed)" };
                    owned.Add(clone);
                    if (clone.HasProperty("_BaseMap")) clone.SetTexture("_BaseMap", art.Door);
                    if (clone.HasProperty("_MainTex")) clone.SetTexture("_MainTex", art.Door);
                    Material[] materials = face.Renderer.sharedMaterials;
                    materials[face.MaterialIndex] = clone;
                    face.Renderer.sharedMaterials = materials;
                    replacements.Add(Tuple.Create(face.Renderer, original, clone));
                }
                Transform visual = face.CreateVisual(art, camera, owned);
                var proxy = new GameObject(car.transform.InverseTransformPoint(face.WorldPivot).x < 0 ? "SlapPack.DriverCrank" : "SlapPack.PassengerCrank");
                owned.Add(proxy);
                proxy.layer = hand.gameObject.layer;
                proxy.transform.SetParent(hand.viewCamera.transform, false);
                var collider = proxy.AddComponent<InteriorCircleCollider>();
                collider.radius = 0.2f;
                collider.hitBoxExpand = 0.1f;
                var crank = proxy.AddComponent<WindowCrank>();
                crank.attachedCollider = collider;
                crank.Initialize(hand, camera, face, visual, pane, turns.Value);
                cranks.Add(crank);
            }
            boundModel = model;
            boundCameras = cameras;
            ModLog.Info(context.Log, "Windows bound: stock driver/passenger crank artwork; clockwise lowers, counterclockwise raises; tint=" + opacity.Value);
        }

        private WindowPane FindPane(Transform car, CameraSwitch cameras, CrankFace face, Material material, Dictionary<Mesh, MeshSnapshot> cache)
        {
            MeshFilter chosen = null;
            List<int> chosenIndices = null;
            float best = float.PositiveInfinity;
            Vector3 crank = car.InverseTransformPoint(face.WorldPivot);
            foreach (MeshRenderer glass in cameras.windshields ?? new MeshRenderer[0])
            {
                if (!glass || !glass.gameObject.activeInHierarchy) continue;
                MeshFilter filter = glass.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                if (!cache.TryGetValue(filter.sharedMesh, out MeshSnapshot snapshot)) cache.Add(filter.sharedMesh, snapshot = MeshSnapshot.Read(filter.sharedMesh));
                Vector3[] points = snapshot.Vertices.Select(p => car.InverseTransformPoint(filter.transform.TransformPoint(p))).ToArray();
                foreach (List<int> group in Groups(snapshot))
                {
                    Vector3 center = group.Distinct().Aggregate(Vector3.zero, (sum, i) => sum + points[i]) / group.Distinct().Count();
                    Vector3 normal = Vector3.zero;
                    for (int i = 0; i < group.Count; i += 3) normal += Vector3.Cross(points[group[i + 1]] - points[group[i]], points[group[i + 2]] - points[group[i]]);
                    if (Mathf.Abs(normal.normalized.x) < 0.65f || center.x * crank.x <= 0) continue;
                    float distance = (center - crank).sqrMagnitude;
                    if (distance < best) { best = distance; chosen = filter; chosenIndices = group; }
                }
            }
            if (!chosen) throw new InvalidOperationException("Matching front door glass was not found; windshield will not be modified.");
            var pane = new WindowPane(chosen, car, chosenIndices, material, owned);
            panes.Add(pane);
            return pane;
        }

        private static IEnumerable<List<int>> Groups(MeshSnapshot mesh)
        {
            var remaining = new List<int[]>();
            foreach (int[] submesh in mesh.Triangles)
                for (int i = 0; i < submesh.Length; i += 3) remaining.Add(new[] { submesh[i], submesh[i + 1], submesh[i + 2] });
            while (remaining.Count > 0)
            {
                var group = new List<int>(remaining[0]);
                remaining.RemoveAt(0);
                bool changed;
                do
                {
                    changed = false;
                    for (int i = remaining.Count - 1; i >= 0; i--)
                        if (remaining[i].Any(v => group.Contains(v))) { group.AddRange(remaining[i]); remaining.RemoveAt(i); changed = true; }
                } while (changed);
                yield return group;
            }
        }

        /// <summary>Matches weather faces to the crank-controlled glass without changing shared meshes.</summary>
        internal WindowPane MovingPane(MeshRenderer source, IEnumerable<int> indices) =>
            panes.FirstOrDefault(p => p.Source && p.Source.gameObject == source.gameObject && indices.Any(p.Indices.Contains));

        internal void RemoveWindows()
        {
            WindowCrankIndicators.RestoreAll();
            foreach (WindowCrank crank in cranks) if (crank) crank.gameObject.SetActive(false);
            cranks.Clear();
            panes.Clear();
            foreach (var replacement in replacements)
            {
                if (!replacement.Item1) continue;
                Material[] materials = replacement.Item1.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) if (materials[i] == replacement.Item3) materials[i] = replacement.Item2;
                replacement.Item1.sharedMaterials = materials;
            }
            replacements.Clear();
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i]) { if (owned[i] is GameObject go) go.SetActive(false); Object.Destroy(owned[i]); }
            owned.Clear();
            boundModel = null;
            boundCameras = null;
        }

        private void OnDestroy() { RemoveWindows(); }
    }
}
