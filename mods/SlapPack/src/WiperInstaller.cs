using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HalfGripTools;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
	/// <summary>Binds the manual controls, blades and weather effects to the current player body.</summary>
	public sealed class WiperInstaller : MonoBehaviour
	{
		private ModContext context;
		private ConfigEntry<bool> enableWipers, enableWeather;
		private ConfigEntry<float> stalkLength, stalkUpAngle, intermittentPause, rainObscuration;
		private CarLink link;
		private CarControllerNew car;
		private CameraSwitch cameras;
		private WiperStalk stalk;
		private WiperState state;
		private WiperBlades blades;
		private WindshieldWeather weather;
		private WindshieldRefractionSettings refractionSettings;
		private readonly List<Object> owned = new List<Object>(), bladeOwned = new List<Object>();
		private float nextScan;
		private int glassSignature;

		internal void Initialize(ModContext ctx)
		{
			context = ctx;
			refractionSettings = new WindshieldRefractionSettings(ctx.Config);
			enableWipers = ctx.Config.Bind("Wipers", "Enabled", true, "Enable the right-hand washer/speed stalk, windshield wipers and precipitation interaction.");
			enableWeather = ctx.Config.Bind("Wipers", "WeatherEffects", true, "Rain/snow impacts, buildup and blade clearing on the front glass. Supports vanilla and optional Open World weather. Restart after changing.");
			rainObscuration = ctx.Config.Bind("Wipers", "RainObscuration", 1f,
				new ConfigDescription("Strength of water obscuration on the windshield. Higher values reduce visibility more. Restart after changing.", new AcceptableValueRange<float>(0.5f, 3f)));
			intermittentPause = ctx.Config.Bind("Wipers", "IntermittentPause", 2.5f,
				new ConfigDescription("Seconds parked between normal-speed wipes in the first dial position. Restart after changing.", new AcceptableValueRange<float>(0.5f, 10f)));
			stalkLength = ctx.Config.Bind("Wipers", "StalkLength", 3.5f,
				new ConfigDescription("Right-hand stalk length in cockpit units. Shorter than the signal stalk; automatically limited for shifter clearance. Restart after changing.", new AcceptableValueRange<float>(2.6f, 4f)));
			stalkUpAngle = ctx.Config.Bind("Wipers", "StalkUpAngle", 24f,
				new ConfigDescription("Upward neutral angle of the right-hand wiper stalk. Restart after changing.", new AcceptableValueRange<float>(0, 30)));
			ctx.OnSceneLoaded(scene => { RemoveWipers(); nextScan = Time.unscaledTime + 0.5f; });
		}

		private void Update()
		{
			if (context == null) return;
			if (!enableWipers.Value) { RemoveWipers(); return; }
			if (car && link && stalk && cameras && link.sw && link.sw.wheel)
			{
				stalk.gameObject.SetActive(car.fpvCamera && car.fpvCamera.isActiveAndEnabled);
				stalk.CheckInteraction();
				if (Time.unscaledTime >= nextScan)
				{
					nextScan = Time.unscaledTime + 1;
					int signature = GlassSignature();
					if (signature != glassSignature)
					{
						DestroyOwned(bladeOwned); blades = null; weather = null; glassSignature = signature;
						try { FitGlass(); }
						catch (Exception error)
						{
							DestroyOwned(bladeOwned);
							blades = null; weather = null;
							context.Log.LogWarning("Wipers could not fit this body: " + error.Message);
						}
					}
				}
				return;
			}
			if (owned.Count > 0 || bladeOwned.Count > 0) RemoveWipers();
			if (Time.unscaledTime < nextScan) return;
			nextScan = Time.unscaledTime + 1;
			foreach (CarLink target in Object.FindObjectsByType<CarLink>(FindObjectsSortMode.None))
			{
				if (!target.cc || !target.cc.isPlayer || !target.sw || !target.sw.wheel) continue;
				CameraSwitch cameraSwitch = target.cc.GetComponent<CameraSwitch>();
				Grabber hand = Object.FindObjectsByType<Grabber>(FindObjectsSortMode.None)
					.FirstOrDefault(g => g.gameObject.scene == target.gameObject.scene && g.viewCamera);
				if (!cameraSwitch || !hand) continue;
				try { Attach(target, cameraSwitch, hand); }
				catch (Exception error)
				{
					RemoveWipers(); context.Log.LogError("Wipers could not initialize: " + error);
					nextScan = Time.unscaledTime + 10;
				}
				return;
			}
		}

		private void Attach(CarLink target, CameraSwitch cameraSwitch, Grabber hand)
		{
			link = target; car = target.cc; cameras = cameraSwitch; state = new WiperState(intermittentPause.Value);
			FitGlass();
			glassSignature = GlassSignature();
			HingedObject wheel = target.sw.wheel;
			Transform parent = wheel.transform.parent;
			Vector3 normal = wheel.movementPlane.normalized;
			if (normal.sqrMagnitude < 0.5f) normal = Vector3.forward;
			float length = stalkLength.Value;
			// Shifter variants have different knob sprites, but share a right-hand assembly.
			// Leave room for the knob and for both controls' hand hitboxes.
			if (target.sa)
			{
				Vector3 shifter = target.sa.transform.position;
				if (parent) shifter = parent.InverseTransformPoint(shifter);
				float available = (shifter.x - wheel.transform.localPosition.x - 0.9f) / Mathf.Cos(stalkUpAngle.Value * Mathf.Deg2Rad);
				if (available > 2.6f) length = Mathf.Min(length, available);
			}
			var root = new GameObject("SlapPack.WiperStalk"); owned.Add(root); root.layer = wheel.gameObject.layer;
			root.transform.SetParent(parent, false);
			root.transform.localPosition = wheel.transform.localPosition + normal * 0.15f;
			root.transform.localRotation = Quaternion.AngleAxis(stalkUpAngle.Value, normal) * Quaternion.LookRotation(normal, Vector3.up);
			var artwork = new MeshRenderer[4];
			for (int speed = 0; speed < 4; speed++)
			{
				artwork[speed] = SignalStalkArtwork.Attach(root.transform, length, root.layer, owned, WiperStalkPixels.Build(speed), true);
				artwork[speed].enabled = speed == 0;
			}
			var grip = new GameObject("WiperDialGrip"); owned.Add(grip); grip.layer = root.layer;
			grip.transform.SetParent(root.transform, false);
			grip.transform.localPosition = new Vector3(length, 0, -0.03f);
			InteriorCircleCollider hit = grip.AddComponent<InteriorCircleCollider>();
			hit.radius = 0.38f; hit.hitBoxExpand = 0.08f;
			InteriorCollider wheelHit = wheel.attachedCollider ? wheel.attachedCollider : wheel.GetComponentInChildren<InteriorCollider>();
			int priority = wheelHit ? wheelHit.sortingLayer : 0;
			hit.sortingLayer = priority < int.MaxValue ? priority + 1 : priority;
			stalk = root.AddComponent<WiperStalk>(); stalk.attachedCollider = hit;
			TurnSignalAudio clicks = null;
			try { clicks = new TurnSignalAudio(transform, hand.gameObject, owned); }
			catch (Exception error) { context.Log.LogWarning("Wiper dial click unavailable: " + error.Message); }
			stalk.Initialize(state, hand, car.fpvCamera, normal, length, artwork, clicks);
			ModLog.Info(context.Log, "Wipers attached: off/intermittent/continuous/fast detents; intermittent park pause=" + intermittentPause.Value + "s. Pull down to wash with a three-cycle minimum.");
		}

		private void LateUpdate()
		{
			if (!car || state == null) return;
			if (Time.timeScale <= 0) return;
			if (weather == null) state.Tick(Time.deltaTime);
			else
			{
				// Sample the motor's actual path cheaply, including stroke reversals.
				// The full water/slush grid and texture are updated only at 20 Hz.
				int steps = Mathf.Max(1, Mathf.CeilToInt(Time.deltaTime * 120));
				float dt = Time.deltaTime / steps;
				for (int i = 0; i < steps; i++)
				{
					float previous = state.Sweep;
					state.Tick(dt);
					weather.Record(dt, previous, state.Sweep, state.Washing);
				}
				weather.Step();
			}
			if (blades != null) blades.Step(state.Sweep, state.Washing, Time.deltaTime);
		}

		private void FitGlass()
		{
			WiperWindshield glass = WiperWindshield.Find(car, cameras);
			blades = new WiperBlades(car.transform, glass, bladeOwned);
			if (enableWeather.Value)
			{
				weather = new WindshieldWeather(car, glass, bladeOwned, message => context.Log.LogWarning(message), rainObscuration.Value, refractionSettings);
				ModLog.Info(context.Log, "Windshield refraction configured: enabled=" + refractionSettings.Enabled.Value + ", same-frame copy width=" + refractionSettings.Width.Value + ", strength=" + refractionSettings.Strength.Value + ".");
				ModLog.Info(context.Log, "Windshield precipitation attached: vanilla fixed medium intensity; optional Open World 0–1 rain/snow; manual wiper selection.");
			}
		}

		private int GlassSignature()
		{
			int signature = 17;
			foreach (MeshRenderer glass in cameras.windshields ?? new MeshRenderer[0])
			{
				if (!glass || !glass.gameObject.activeInHierarchy) continue;
				MeshFilter filter = glass.GetComponent<MeshFilter>();
				signature = unchecked(signature * 31 + glass.GetInstanceID());
				if (filter && filter.sharedMesh) signature = unchecked(signature * 31 + filter.sharedMesh.GetInstanceID());
			}
			return signature;
		}

		internal void RemoveWipers()
		{
			if (stalk) stalk.gameObject.SetActive(false);
			DestroyOwned(owned); DestroyOwned(bladeOwned);
			car = null; link = null; cameras = null; stalk = null; state = null; blades = null; weather = null;
		}

		private static void DestroyOwned(List<Object> objects)
		{
			for (int i = objects.Count - 1; i >= 0; i--)
				if (objects[i]) { if (objects[i] is GameObject go) go.SetActive(false); Object.Destroy(objects[i]); }
			objects.Clear();
		}

		private void OnDestroy() { RemoveWipers(); }
	}
}
