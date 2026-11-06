using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfGripSlapPack
{
	/// <summary>Maintains point-filtered water/slush highlights and optional same-frame refraction.</summary>
	internal sealed class WindshieldWeather
	{
		private readonly WindshieldWeatherState field;
		private readonly WiperWindshield glass;
		private readonly Texture2D texture;
		private readonly Color32[] pixels = new Color32[WindshieldWeatherState.Width * WindshieldWeatherState.Height];
		private readonly WindshieldWeatherSource source;
		private readonly CarControllerNew car;
		private readonly MeshRenderer renderer;
		private readonly WindshieldWeatherClock clock = new WindshieldWeatherClock();
		private readonly float obscuration;
		private WindshieldRefraction refraction;

		internal WindshieldWeather(CarControllerNew car, WiperWindshield glass, List<Object> owned, Action<string> warning, float obscuration, WindshieldRefractionSettings refractionSettings)
		{
			this.car = car; this.glass = glass;
			this.obscuration = obscuration;
			field = new WindshieldWeatherState(glass.Opening);
			source = new WindshieldWeatherSource(warning);
			texture = new Texture2D(WindshieldWeatherState.Width, WindshieldWeatherState.Height, TextureFormat.RGBA32, false)
				{ name = "SlapPack windshield water/slush", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
			owned.Add(texture);
			texture.SetPixels32(pixels); texture.Apply(false, false);
			Material material = WindowMaterials.Precipitation(Color.white, texture, owned);
			material.name = "SlapPack windshield precipitation";
			var go = new GameObject("SlapPack.WindshieldWeather"); owned.Add(go);
			go.layer = glass.Source.gameObject.layer; go.transform.SetParent(car.transform, false);
			Mesh mesh = glass.WeatherMesh(field); owned.Add(mesh);
			go.AddComponent<MeshFilter>().sharedMesh = mesh;
			renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
			renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
			if (refractionSettings.Enabled.Value)
			{
				var refracted = new GameObject("SlapPack.WindshieldRefraction"); owned.Add(refracted);
				refracted.transform.SetParent(car.transform, false);
				refraction = refracted.AddComponent<WindshieldRefraction>();
				try { refraction.Initialize(car, glass, field, refractionSettings, owned, warning); }
				catch (Exception error)
				{
					refracted.SetActive(false); refraction = null;
					warning("Windshield refraction could not initialize; retaining tint: " + error.Message);
				}
			}
		}

		internal void Record(float dt, float previous, float current, bool washing) => clock.Record(dt, previous, current, washing);

		internal void Step()
		{
			if (!clock.TryStep(out float dt, out float washerFraction, out float previous, out float current)) return;
			WindshieldPrecipitation weather = source.Read(car);
			Vector3 velocity = car.rb ? car.rb.linearVelocity : Vector3.zero;
			Vector3 relative = car.transform.InverseTransformDirection(weather.Wind - velocity);
			// Project gravity and air onto the real glass, so reversing and crosswind
			// reverse the appropriate streak direction. Normal incidence adds impacts;
			// the field separately limits how quickly adhered water can run off.
			Vector3 flow = car.transform.InverseTransformDirection(Vector3.down) * 0.018f + relative * 0.0025f;
			PanePoint planeFlow = glass.ProjectVector(flow);
			float incidence = Mathf.Max(0, -Vector3.Dot(relative, glass.Normal));
			field.Deposit(dt, weather.Rain, weather.Snow, incidence, planeFlow.X, planeFlow.Y, washerFraction > 0, washerFraction);
			field.Wipe(previous, current);
			Draw();
		}

		private void Draw()
		{
			renderer.enabled = glass.Source && glass.Source.gameObject.activeInHierarchy;
			const int width = WindshieldWeatherState.Width;
			bool refracting = refraction && refraction.Ready;
			for (int k = 0; k < pixels.Length; k++)
			{
				if (!field.Inside[k]) continue;
				float water = field.Water[k], snow = field.Snow[k];
				// A bright leading rim, dark trailing rim and translucent center give
				// each deposit relief without shipping shaders or game textures.
				float gradient = (k >= width ? field.Water[k - width] : 0) - (k + width < pixels.Length ? field.Water[k + width] : 0);
				float shine = Mathf.Clamp01(0.46f + gradient * 1.4f + field.Impact[k] * 0.3f);
				float alpha = WindshieldWeatherOptics.Opacity(water, snow, obscuration);
				if (refracting) alpha = WindshieldWeatherOptics.RefractionFloor(water, snow, obscuration);
				float white = snow / Mathf.Max(0.001f, snow + water);
				pixels[k] = new Color32((byte)(Mathf.Lerp(shine * 0.8f, 0.88f, white) * 255), (byte)(Mathf.Lerp(shine * 0.9f, 0.93f, white) * 255),
					(byte)(Mathf.Lerp(shine, 0.96f, white) * 255), (byte)(alpha * 255));
			}
			texture.SetPixels32(pixels); texture.Apply(false, false);
		}
	}
}
