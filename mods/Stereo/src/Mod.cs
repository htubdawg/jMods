using System;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;
using HalfGripTools;
using UnityEngine;

namespace HalfGripStereo
{
	/// <summary>
	/// A car stereo built into every car's cockpit (not a shop item: it takes no slot): a CD head unit left of the steering wheel and a CD
	/// wallet on the sun visor.
	/// One disc per song of the game's soundtrack and one that plays the songs in a folder on the player's computer; the cockpit hand
	/// works it all (no keys). The race's music is whatever the stereo plays.
	/// </summary>
	public class Mod : HalfGripMod
	{
		internal static ManualLogSource Log;

		internal const int VolumeSteps = 10;

		internal static ConfigEntry<bool> Enabled;
		internal static ConfigEntry<string> MusicFolder;
		internal static ConfigEntry<bool> Shuffle;
		internal static ConfigEntry<int> Volume;
		internal static ConfigEntry<float> Loudness;
		internal static ConfigEntry<float> UnitX;
		internal static ConfigEntry<float> UnitY;
		internal static ConfigEntry<float> Lean;
		internal static ConfigEntry<bool> ShowVisor;
		internal static ConfigEntry<float> VisorX;
		internal static ConfigEntry<float> VisorY;
		internal static ConfigEntry<bool> LitParts;
		internal static ConfigEntry<bool> Verbose;
		internal static ConfigEntry<int> ConfigVersion;

		/// <summary>The config's layout now; <see cref="Migrate"/> brings an older file up to it once.</summary>
		private const int CurrentConfig = 2;

		/// <summary>A detail line for the log, only written with Debug.VerboseLog on. Warnings and errors are always written.</summary>
		internal static void Info(string text)
		{
			if (Verbose != null && Verbose.Value)
			{
				Log.LogInfo(text);
			}
		}

		private static ConfigFile config;
		private static DateTime configWritten;
		private static float nextConfigCheck;

		/// <summary>Re-reads the config file when it has been saved since, once a second at most, so the stereo's place can be tuned live.</summary>
		internal static void WatchConfig()
		{
			if (config == null || Time.unscaledTime < nextConfigCheck)
			{
				return;
			}
			nextConfigCheck = Time.unscaledTime + 1f;
			try
			{
				DateTime written = File.GetLastWriteTimeUtc(config.ConfigFilePath);
				if (written != configWritten)
				{
					bool first = configWritten == default;
					configWritten = written;
					if (!first)
					{
						config.Reload();
						Info("Stereo: config file changed, read it again");
					}
				}
			}
			catch (Exception e)
			{
				Log.LogWarning("Stereo: could not re-read the config file: " + e.Message);
				config = null;
			}
		}

		/// <summary>
		/// BepInEx keeps a saved value over a new default, so a changed default is moved on here, once, and only if it was never changed.
		/// 2 (0.2.1): nothing left to move (0.2.1 moved Stereo.Slant 0.33 to 0.25; 0.2.2 replaced Slant with Stereo.Perspective, and 0.2.3
		/// Perspective with Stereo.Lean, each a new key,
		/// the angle now coming from the cockpit camera).
		/// </summary>
		private static void Migrate()
		{
			if (ConfigVersion.Value >= CurrentConfig)
			{
				return;
			}
			ConfigVersion.Value = CurrentConfig;
		}

		public override void Load(ModContext ctx)
		{
			Log = ctx.Log;
			Enabled = ctx.Config.Bind("Stereo", "Enabled", true, "Put the stereo in the car. Off, the game plays its own race music as usual (from the next race on).");
			MusicFolder = ctx.Config.Bind("Music", "Folder", "", "The folder the MY MUSIC disc plays (.ogg, .mp3, .wav, .aiff; not its subfolders), in file name order. Empty: \"Half Grip\" in your Music folder, made the first time the stereo needs it.");
			Shuffle = ctx.Config.Bind("Music", "Shuffle", false, "Play the MY MUSIC disc's songs in a random order.");
			Volume = ctx.Config.Bind("Music", "Volume", 7, new ConfigDescription("The stereo's volume knob, 0 to 10 (grab it with the hand in the cockpit and turn it: clockwise is louder). The game's Music volume applies on top.", new AcceptableValueRange<int>(0, VolumeSteps)));
			Loudness = ctx.Config.Bind("Music", "Loudness", 1f, new ConfigDescription("How loud the stereo is at full volume against the game's own music (1: as loud).", new AcceptableValueRange<float>(0.1f, 1f)));
			UnitX = ctx.Config.Bind("Stereo", "OffsetX", 0f, "Moves the stereo sideways from its place left of the steering wheel (cockpit units, right is positive; a cockpit pixel is about 0.05). Takes effect as soon as the file is saved.");
			UnitY = ctx.Config.Bind("Stereo", "OffsetY", 0f, "Moves the stereo up or down (cockpit units, up is positive). Takes effect as soon as the file is saved.");
			Lean = ctx.Config.Bind("Stereo", "Lean", 3f, new ConfigDescription("How far the stereo's left side leans to the right over its height, in cockpit pixels: its sides run up toward the middle of the view, as the dash's do (the right side leans less). The face itself stays square on. 0: a plain rectangle. Takes effect as soon as the file is saved.", new AcceptableValueRange<float>(0f, 12f)));
			ShowVisor = ctx.Config.Bind("Visor", "Shown", true, "Show the CD wallet on the sun visor. Off, the stereo's next and previous keys still change disc.");
			VisorX = ctx.Config.Bind("Visor", "ScreenX", 0.3f, new ConfigDescription("Where the CD wallet's middle is across the screen (0 left edge, 1 right edge). Takes effect as soon as the file is saved.", new AcceptableValueRange<float>(0f, 1f)));
			VisorY = ctx.Config.Bind("Visor", "ScreenY", 0.87f, new ConfigDescription("Where the CD wallet's middle is up the screen (0 bottom, 1 top). Takes effect as soon as the file is saved.", new AcceptableValueRange<float>(0f, 1f)));
			LitParts = ctx.Config.Bind("Display", "Lit", true, "Light the stereo and the visor like the rest of the cockpit. Turn it off if they do not show.");
			Verbose = ctx.Config.Bind("Debug", "VerboseLog", false, "Write what the stereo finds and does (songs, placing, discs) to the BepInEx log.");
			ConfigVersion = ctx.Config.Bind("Debug", "ConfigVersion", 0, "The settings' layout, for moving old values on once. Leave it alone.");
			config = ctx.Config;
			Migrate();

			ctx.PatchAll();
			ctx.OnSceneLoaded(Discs.OnSceneLoaded);
			ctx.OnSceneLoaded(Sounds.OnSceneLoaded);
			Discs.Find();
			Sounds.Find();

			fitter = new GameObject("StereoFitter");
			UnityEngine.Object.DontDestroyOnLoad(fitter);
			fitter.AddComponent<Fitter>();
			ctx.OnUnload(() =>
			{
				if (fitter != null)
				{
					UnityEngine.Object.Destroy(fitter);
				}
				fitter = null;
				if (Deck.Active != null)
				{
					UnityEngine.Object.Destroy(Deck.Active.gameObject);
				}
			});
		}

		private static GameObject fitter;
	}
}
