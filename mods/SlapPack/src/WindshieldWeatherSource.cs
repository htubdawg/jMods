using System;
using UnityEngine;

namespace HalfGripSlapPack
{
	/// <summary>Final local precipitation and world-space wind from the selected weather provider.</summary>
	internal struct WindshieldPrecipitation
	{
		internal float Rain, Snow;
		internal Vector3 Wind;
	}

	/// <summary>Uses Open World's public WeatherView API when available, otherwise the vanilla profile.</summary>
	internal sealed class WindshieldWeatherSource
	{
		private WeatherController controller;
		private OptionalWeatherApi<Vector3> api;
		private float nextScan;
		private bool warned;
		private bool apiActive;
		private string provider;
		private readonly Action<string> warning;

		internal WindshieldWeatherSource(Action<string> warning) { this.warning = warning; }

		/// <summary>Reads final rain/snow values; the publisher owns cutoff, altitude and origin handling.</summary>
		internal WindshieldPrecipitation Read(CarControllerNew car)
		{
			if (Time.unscaledTime >= nextScan)
			{
				nextScan = Time.unscaledTime + 2;
				if (!controller) controller = UnityEngine.Object.FindFirstObjectByType<WeatherController>();
				// A bound API stays bound when inactive (outside Open World); only a missing one is looked for again.
				if (api == null)
				{
					try { FindApi(); }
					catch (Exception error) { Warn("Optional public weather API discovery failed; using vanilla precipitation: " + error.Message); }
				}
			}
			if (api != null)
			{
				try
				{
					apiActive = api.TryRead(out float rain, out float snow, out Vector3 wind);
					if (apiActive)
					{
						Provider("Open World WeatherView v1");
						rain = WindshieldWeatherState.Unit(rain); snow = WindshieldWeatherState.Unit(snow);
						float sum = rain + snow; if (sum > 1) { rain /= sum; snow /= sum; }
						return new WindshieldPrecipitation { Rain = rain, Snow = snow, Wind = wind };
					}
				}
				catch (Exception error) { api = null; apiActive = false; Warn("Open World public weather query failed; using vanilla precipitation: " + error.Message); }
			}
			Provider("vanilla profile (public Open World API absent/inactive)");
			WeatherProfile profile = controller ? controller.activeProfile : RunState.ActiveWeather;
			return new WindshieldPrecipitation { Rain = profile && profile.rain ? 0.5f : 0, Snow = profile && profile.snow ? 0.5f : 0 };
		}

		/// <summary>Retries discovery for load order, binding public members only.</summary>
		private void FindApi()
		{
			bool openWorldLoaded = false;
			System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = assemblies.Length - 1; i >= 0; i--)
			{
				System.Reflection.Assembly assembly = assemblies[i];
				Type type = assembly.GetType("HalfGripOpenWorld.WeatherView", false);
				openWorldLoaded |= type != null || assembly.GetType("HalfGripOpenWorld.Mod", false) != null;
				if (type == null) continue;
				if (!OptionalWeatherApi<Vector3>.TryBind(type, out api, out string reason))
					Warn("Open World public weather API unavailable; requires WeatherView v1 (Open World 0.24.0+): " + reason);
				return;
			}
			if (openWorldLoaded) Warn("Open World is loaded without public WeatherView v1; update Open World to 0.24.0 or newer for variable windshield weather. Using vanilla flags.");
		}

		/// <summary>Logs provider changes only when verbose diagnostics are enabled.</summary>
		private void Provider(string name)
		{
			if (provider == name) return;
			provider = name; ModLog.Info("Windshield weather provider: " + name);
		}

		/// <summary>Warns once per surface for an unavailable or incompatible optional integration.</summary>
		private void Warn(string message) { if (!warned) { warned = true; warning(message); } }
	}
}
