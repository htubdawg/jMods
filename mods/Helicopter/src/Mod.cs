using BepInEx.Configuration;
using BepInEx.Logging;
using HalfGripTools;
using UnityEngine;

namespace HalfGripHelicopter
{
	/// <summary>
	/// The Helicopter mod: a chassis item (<see cref="HeliItem"/>) that, while fitted, takes every other item off the car
	/// (<see cref="Lock"/>), swaps the wheel and shifter for a throttle, a collective and a stick (<see cref="Cockpit"/>) and flies the
	/// car (<see cref="Flight"/>). A persistent object (<see cref="HeliDriver"/>) notices the item on the player's car in each race and
	/// puts the helicopter on or takes it off, so a refit in the Open World's garages works too.
	/// </summary>
	public class Mod : HalfGripMod
	{
		internal static ManualLogSource Log;

		internal static ConfigEntry<float> TopSpeed;
		internal static ConfigEntry<float> ClimbRate;
		internal static ConfigEntry<float> MaxTilt;
		internal static ConfigEntry<float> TiltResponse;
		internal static ConfigEntry<float> TiltRate;
		internal static ConfigEntry<float> SpoolSeconds;
		internal static ConfigEntry<bool> InRuns;
		internal static ConfigEntry<int> Price;
		internal static ConfigEntry<bool> Verbose;

		private static GameObject driver;

		/// <summary>Everything but warnings and errors, only with Debug.VerboseLog on.</summary>
		internal static void Info(string text)
		{
			if (Verbose != null && Verbose.Value)
			{
				Log.LogInfo(text);
			}
		}

		public override void Load(ModContext ctx)
		{
			Log = ctx.Log;
			TopSpeed = ctx.Config.Bind("Flight", "TopSpeed", 140f, "Top speed with the stick fully forward, in game units a second (the car tops out near 100).");
			ClimbRate = ctx.Config.Bind("Flight", "ClimbRate", 30f, "Climb rate with the collective fully up, in game units a second. Half way up is a hover.");
			MaxTilt = ctx.Config.Bind("Flight", "MaxTilt", 30f, "How far the stick tilts the helicopter at full deflection, in degrees.");
			TiltResponse = ctx.Config.Bind("Flight", "TiltResponse", 3f, "How quickly the helicopter pitches and banks to follow the stick (the attitude spring's natural frequency, rad/s). Lower is gentler.");
			TiltRate = ctx.Config.Bind("Flight", "TiltRate", 30f, "The fastest the helicopter pitches or banks, in degrees a second.");
			SpoolSeconds = ctx.Config.Bind("Flight", "SpoolSeconds", 4f, "Seconds the rotor takes to spin up to full speed from rest.");
			InRuns = ctx.Config.Bind("Shop", "InRuns", false, "Offer the Helicopter in normal runs too (shops and race rewards). Off: only the Open World's and the Multiplayer mod's garages offer it.");
			Price = ctx.Config.Bind("Shop", "Price", 80, new ConfigDescription("The Helicopter's shop price.", new AcceptableValueRange<int>(0, 9999)));
			Verbose = ctx.Config.Bind("Debug", "VerboseLog", false, "Write what the mod does to the log.");

			HeliItem.Register(ctx);
			Remote.Link(ctx);
			ctx.PatchAll();
			ctx.OnSceneLoaded(scene =>
			{
				Lock.LinkOpenWorld(ctx);
				Lock.LinkMultiplayer(ctx);
			});

			driver = new GameObject("HelicopterDriver");
			driver.hideFlags = HideFlags.HideAndDontSave;
			Object.DontDestroyOnLoad(driver);
			driver.AddComponent<HeliDriver>();
			ctx.OnUnload(() =>
			{
				if (driver != null)
				{
					Object.Destroy(driver);
				}
				driver = null;
			});
		}
	}
}
