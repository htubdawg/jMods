using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HalfGripTools;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
	/// <summary>Owns side/rear precipitation independently of the optional crank and wiper controls.</summary>
	public sealed class WindowWeatherInstaller : MonoBehaviour
	{
		private ModContext context;
		private Action<string> warning;
		private ConfigEntry<bool> enabledWeather;
		private WindowInstaller windows;
		private CarControllerNew car;
		private CameraSwitch cameras;
		private Camera mirrorCamera;
		private WindshieldWeatherSource source;
		private List<WindowWeatherSurface> surfaces = new List<WindowWeatherSurface>();
		private readonly List<Object> owned = new List<Object>();
		private float nextScan, elapsed;
		private int signature;
		private float nextMirrorDiagnostic;
		private bool subscribed;

		internal void Initialize(ModContext ctx, WindowInstaller windows)
		{
			context = ctx; this.windows = windows; warning = message => context.Log.LogWarning(message);
			enabledWeather = ctx.Config.Bind("Windows", "WeatherEffects", true,
				"Sparse rain and light edge snow on all side/rear glass. Drains and clears naturally, with capped opacity; independent of wipers and cranks.");
			ctx.OnSceneLoaded(scene => { RemoveWeather(); nextScan = Time.unscaledTime + 0.5f; });
			Subscribe();
		}

		private void Update()
		{
			if (context == null) return;
			if (!enabledWeather.Value) { RemoveWeather(); return; }
			if (Time.unscaledTime < nextScan) return;
			nextScan = Time.unscaledTime + 1;
			CarControllerNew target = null;
			foreach (CarControllerNew candidate in Object.FindObjectsByType<CarControllerNew>(FindObjectsSortMode.None))
				if (candidate.isPlayer) { target = candidate; break; }
			CameraSwitch cameraSwitch = target ? target.GetComponent<CameraSwitch>() : null;
			Camera rear = null;
			if (target)
				foreach (Camera candidate in target.GetComponentsInChildren<Camera>(true))
					if (candidate.name == "RearViewCamera") { rear = candidate; break; }
			int current = Signature(cameraSwitch);
			if (target == car && cameraSwitch == cameras && current == signature) { mirrorCamera = rear; return; }
			RemoveWeather();
			car = target; cameras = cameraSwitch; signature = current; mirrorCamera = rear;
			if (!car || !cameras) return;
			try
			{
				surfaces = WindowWeatherSurface.Find(car, cameras, owned);
				source = new WindshieldWeatherSource(warning);
				ModLog.Info(context.Log, "Side/rear precipitation attached to " + surfaces.Count + " glass patches; self-clearing, capped opacity.");
			}
			catch (Exception error)
			{
				ClearOwned();
				context.Log.LogWarning("Side/rear precipitation could not fit this body: " + error.Message);
			}
		}

		private static int Signature(CameraSwitch cameras)
		{
			int value = 17;
			if (!cameras) return value;
			foreach (MeshRenderer glass in cameras.windshields ?? new MeshRenderer[0])
			{
				if (!glass || !glass.gameObject.activeInHierarchy) continue;
				MeshFilter filter = glass.GetComponent<MeshFilter>();
				value = unchecked(value * 31 + glass.GetInstanceID());
				if (filter && filter.sharedMesh) value = unchecked(value * 31 + filter.sharedMesh.GetInstanceID());
			}
			return value;
		}

		private void LateUpdate()
		{
			if (!car || source == null || surfaces.Count == 0 || Time.timeScale <= 0) return;
			elapsed += Time.deltaTime;
			if (elapsed < 0.05f) return;
			float dt = Mathf.Min(elapsed, 0.1f); elapsed = 0;
			WindshieldPrecipitation weather = source.Read(car);
			Vector3 velocity = car.rb ? car.rb.linearVelocity : Vector3.zero;
			Vector3 relative = car.transform.InverseTransformDirection(weather.Wind - velocity);
			foreach (WindowWeatherSurface surface in surfaces)
			{
				if (!surface.Source) continue;
				surface.IncludeMirror(car.fpvCamera, mirrorCamera, warning);
				WindowPane moving = windows ? windows.MovingPane(surface.Source, surface.Indices) : null;
				surface.Step(car, weather, relative, dt, moving != null ? moving.OpenFraction : 0);
			}
		}

		private void ClearOwned()
		{
			surfaces.Clear(); source = null; elapsed = 0;
			for (int i = owned.Count - 1; i >= 0; i--)
				if (owned[i]) { if (owned[i] is GameObject go) go.SetActive(false); Object.Destroy(owned[i]); }
			owned.Clear();
		}

		/// <summary>Checks the actual existing mirror view immediately before URP culls its transparent geometry.</summary>
		private void BeginCamera(ScriptableRenderContext renderContext, Camera camera)
		{
			if (!car || !camera || (camera != car.fpvCamera && camera != mirrorCamera) || surfaces.Count == 0) return;
			foreach (WindowWeatherSurface surface in surfaces)
				surface.IncludeMirror(car.fpvCamera, mirrorCamera, warning);
			if (camera != mirrorCamera || !ModLog.Verbose || Time.unscaledTime < nextMirrorDiagnostic) return;
			nextMirrorDiagnostic = Time.unscaledTime + 5;
			int rearPatches = 0, included = 0, wetSamples = 0;
			foreach (WindowWeatherSurface surface in surfaces)
			{
				if (!surface.Field.Rear || !surface.Source) continue;
				rearPatches++;
				if (!surface.Visible || (camera.cullingMask & (1 << surface.Layer)) == 0) continue;
				included++;
				wetSamples += surface.VisibleWetSamples(car.transform, camera);
			}
			ModLog.Info(context.Log, "Rear-window mirror visibility: camera=" + camera.name + ", rear patches/layer-included=" + rearPatches + "/" + included
				+ ", wet samples in frustum=" + wetSamples + ", target=" + (camera.targetTexture ? camera.targetTexture.width + "x" + camera.targetTexture.height : "screen")
				+ ". CPU layer/projection checks; depth occlusion and final GPU pixels still require a visual check.");
		}

		/// <summary>Uses existing camera callbacks; no camera or render pass is added.</summary>
		private void Subscribe()
		{
			if (context == null || subscribed) return;
			RenderPipelineManager.beginCameraRendering += BeginCamera; subscribed = true;
		}

		internal void RemoveWeather() { ClearOwned(); car = null; cameras = null; mirrorCamera = null; signature = 0; }
		private void OnEnable() { Subscribe(); }
		private void OnDisable()
		{
			if (subscribed) RenderPipelineManager.beginCameraRendering -= BeginCamera;
			subscribed = false;
		}
		private void OnDestroy() { OnDisable(); RemoveWeather(); }
	}
}
