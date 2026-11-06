using System;
using System.Collections.Generic;
using HalfGripTools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HalfGripSlapPack
{
	/// <summary>Protects the active 3D aim model from glass weather without moving the gun or changing its materials.</summary>
	public sealed class HeldGunWeatherOcclusion : MonoBehaviour
	{
		private WiperInstaller wipers;
		private WindowWeatherInstaller windows;
		private ModContext context;
		private HeldGunWeatherOcclusionPass pass;
		private Gun[] guns = Array.Empty<Gun>();
		private readonly List<HeldGunWeatherOcclusionPass.Draw> draws = new List<HeldGunWeatherOcclusionPass.Draw>();
		private readonly HashSet<int> warned = new HashSet<int>();
		private float nextScan;
		private float nextDiagnostic;
		private bool subscribed;

		internal void Initialize(ModContext context, WiperInstaller wipers, WindowWeatherInstaller windows)
		{
			this.context = context; this.wipers = wipers; this.windows = windows;
			pass = new HeldGunWeatherOcclusionPass();
			context.OnSceneLoaded(scene => { guns = Array.Empty<Gun>(); nextScan = 0; });
			RenderPipelineManager.beginCameraRendering += BeginCamera;
			subscribed = true;
		}

		/// <summary>Uses Gun.aimModel, never the hanging cockpit sprite or unrelated world weapons.</summary>
		private void BeginCamera(ScriptableRenderContext renderContext, Camera camera)
		{
			if (!camera || !camera.isActiveAndEnabled || !ChassisCompatibility.AllowsAdditions) return;
			CarControllerNew car = camera.GetComponentInParent<CarControllerNew>();
			if (!car || !car.isPlayer || camera != car.fpvCamera ||
				!((wipers && wipers.HasWeather(car)) || (windows && windows.HasWeather(car)))) return;
			UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
			if (!data || data.renderType != CameraRenderType.Base || camera.stereoEnabled) return;
			if (Time.unscaledTime >= nextScan)
			{
				nextScan = Time.unscaledTime + 0.5f;
				guns = UnityEngine.Object.FindObjectsByType<Gun>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			}
			draws.Clear();
			foreach (Gun gun in guns)
			{
				if (!gun || !gun.isActiveAndEnabled || gun.fpvcamera != camera || !gun.aimModel || !gun.aimModel.gameObject.activeInHierarchy) continue;
				foreach (Renderer renderer in gun.aimModel.GetComponentsInChildren<Renderer>(false))
				{
					if (!renderer.enabled || renderer.forceRenderingOff || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
					Mesh mesh = null;
					if (renderer is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
					else if (renderer is MeshRenderer)
					{
						MeshFilter filter = renderer.GetComponent<MeshFilter>();
						if (filter) mesh = filter.sharedMesh;
					}
					if (!mesh) continue;
					Material[] materials = renderer.sharedMaterials;
					for (int i = 0; i < Math.Min(mesh.subMeshCount, materials.Length); i++)
					{
						Material material = materials[i];
						// Only solid/cutout surfaces occlude. Native DepthOnly retains
						// alpha clipping and vertex deformation, including skinned guns.
						if (!material || material.renderQueue > (int)RenderQueue.GeometryLast) continue;
						int depthPass = material.FindPass("DepthOnly");
						if (depthPass < 0 || !material.GetShaderPassEnabled("DepthOnly"))
						{
							if (warned.Add(material.GetInstanceID())) context.Log.LogWarning("Held gun weather occlusion: no enabled DepthOnly pass on " + material.name + ".");
							continue;
						}
						draws.Add(new HeldGunWeatherOcclusionPass.Draw(renderer, material, i, depthPass));
					}
				}
			}
			if (draws.Count == 0) return;
			if (ModLog.Verbose && Time.unscaledTime >= nextDiagnostic)
			{
				nextDiagnostic = Time.unscaledTime + 5;
				ModLog.Info("Held 3D gun weather occlusion: submesh draws=" + draws.Count + ", graph records=" + pass.Records + ", depth submissions=" + pass.Submissions + ".");
			}
			pass.Prepare(camera, draws.ToArray());
			data.scriptableRenderer.EnqueuePass(pass);
		}

		/// <summary>Unsubscribes before owned weather and the host are destroyed.</summary>
		internal void Stop()
		{
			if (subscribed) RenderPipelineManager.beginCameraRendering -= BeginCamera;
			subscribed = false; guns = Array.Empty<Gun>(); draws.Clear();
		}

		private void OnDisable() { Stop(); }
		private void OnDestroy() { Stop(); }
	}
}
