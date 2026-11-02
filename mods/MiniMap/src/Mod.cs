using System;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;
using HalfGripTools;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// A round minimap in a corner of the screen: the whole lap, turned so the way the player's car points is always up, with a marker for
	/// every car in the race. It is drawn from the race's own track line (the line the lap counter and the bots follow), not from anything
	/// known about one map, so it works on the game's maps, on ProcTrack's laps and on any other map a mod adds. The colours are chosen from
	/// the scene the race is in (see <see cref="Palettes"/>). In the Open World mod's free roam, where the lap is only a token loop, the
	/// maps show the country round the car instead, from the Open World's own tile pictures (see <see cref="OpenWorldLink"/>).
	/// </summary>
	public class Mod : HalfGripMod
	{
		internal static ManualLogSource Log;

		internal static ConfigEntry<bool> Enabled;
		internal static ConfigEntry<string> Corner;
		internal static ConfigEntry<int> Size;
		internal static ConfigEntry<int> Margin;
		internal static ConfigEntry<int> PixelSize;
		internal static ConfigEntry<float> Opacity;
		internal static ConfigEntry<float> GpsWidth;
		internal static ConfigEntry<float> GpsOffsetX;
		internal static ConfigEntry<float> GpsOffsetY;
		internal static ConfigEntry<bool> GpsLitCasing;
		internal static ConfigEntry<float> ViewTiles;
		internal static ConfigEntry<bool> Verbose;
		internal static ModKey Toggle;

		private static GameObject hud;

		/// <summary>A detail line for the log: only written when Debug.VerboseLog is on. Warnings and errors are always written.</summary>
		internal static void Info(string text)
		{
			if (Verbose != null && Verbose.Value)
			{
				Log.LogInfo(text);
			}
		}

		/// <summary>
		/// BepInEx keeps a setting's saved value when its default changes, so a default that moves is moved here once for configs written
		/// by an older version. 2 (0.2.0): the minimap went from the top right to the top left, off the placement board. 3 (0.3.1): the
		/// GPS grew from 1.9 to 2.6 dash units wide. 4 (0.3.2): 1.6, now that its pixels are measured right.
		/// 5 (0.3.3): 1.9 wide and 0.6 lower, to match the jumper's key cap beside it. 8 (1.0.0): the screen minimap is off by
		/// default (Display.Enabled), and the old Display.Shown, which K used to flip in the file, is taken out of it. (6 and 7 were
		/// steps of the release's own test builds; a file from those is at 6 or 7 and gets this step too.)
		/// </summary>
		private static void Migrate(ConfigFile config)
		{
			ConfigEntry<int> version = config.Bind("Debug", "ConfigVersion", 1, "Which version of the mod last updated this file. Leave it alone.");
			if (version.Value < 2)
			{
				if (Corner.Value == "TopRight")
				{
					Corner.Value = "TopLeft";
				}
				version.Value = 2;
			}
			if (version.Value < 3)
			{
				if (Mathf.Approximately(GpsWidth.Value, 1.9f))
				{
					GpsWidth.Value = 2.6f;
				}
				version.Value = 3;
			}
			if (version.Value < 4)
			{
				if (Mathf.Approximately(GpsWidth.Value, 2.6f) || Mathf.Approximately(GpsWidth.Value, 1.9f))
				{
					GpsWidth.Value = 1.6f;
				}
				version.Value = 4;
			}
			if (version.Value < 5)
			{
				if (Mathf.Approximately(GpsWidth.Value, 1.6f))
				{
					GpsWidth.Value = 1.9f;
				}
				if (Mathf.Approximately(GpsOffsetY.Value, 0f))
				{
					GpsOffsetY.Value = -0.6f;
				}
				version.Value = 5;
			}
			if (version.Value < 8)
			{
				// Bound only to be removed, so the line is not kept in the file as an orphan.
				ConfigDefinition shown = new ConfigDefinition("Display", "Shown");
				config.Bind(shown, true);
				config.Remove(shown);
				config.Save();
				version.Value = 8;
			}
		}

		private static ConfigFile config;
		private static DateTime configWritten;
		private static float nextConfigCheck;

		/// <summary>
		/// Re-reads the config file when it has been saved since it was last read, once a second at most, so settings (the GPS's size and
		/// place above all) can be tuned in a text editor while the game runs. Only this mod's own config file is looked at.
		/// </summary>
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
						Info("Minimap: config file changed, read it again");
					}
				}
			}
			catch (Exception e)
			{
				Log.LogWarning("Minimap: could not re-read the config file: " + e.Message);
				config = null;
			}
		}

		public override void Load(ModContext ctx)
		{
			Log = ctx.Log;

			// Off by default: the GPS is how a player gets a map; the screen minimap is there for whoever switches it on. Its key is only
			// added to Settings > Controls when it is on, so nobody who never asked for it sees a control for it.
			Enabled = ctx.Config.Bind("Display", "Enabled", false, "Use the minimap on the screen in races, and add its show / hide key (K) to Settings > Controls. It starts shown every game; K hides and shows it. Restart the game after changing this. The GPS item shows a map on the dash either way.");
			Corner = ctx.Config.Bind("Display", "Corner", "TopLeft", new ConfigDescription("The corner of the screen the minimap sits in. The race's placement board is in the top right.", new AcceptableValueList<string>("TopLeft", "TopRight", "BottomLeft", "BottomRight")));
			Size = ctx.Config.Bind("Display", "Size", 300, new ConfigDescription("Width of the minimap, in pixels of a 1080 pixel high screen (it scales with the screen).", new AcceptableValueRange<int>(120, 700)));
			Margin = ctx.Config.Bind("Display", "Margin", 24, new ConfigDescription("Gap between the minimap and the edges of the screen, in pixels of a 1080 pixel high screen.", new AcceptableValueRange<int>(0, 200)));
			PixelSize = ctx.Config.Bind("Display", "PixelSize", 3, new ConfigDescription("Screen pixels (of a 1080 pixel high screen) per pixel of the minimap. 3 matches the chunky pixels of the game's own art; 1 is the finest.", new AcceptableValueRange<int>(1, 8)));
			Opacity = ctx.Config.Bind("Display", "Opacity", 0.9f, new ConfigDescription("How opaque the minimap is: 1 is solid.", new AcceptableValueRange<float>(0.2f, 1f)));
			GpsWidth = ctx.Config.Bind("GPS", "Width", 1.9f, new ConfigDescription("Width of the GPS on the dash, in the dash's own units (its two module slots are about 2.3 apart). Takes effect as soon as the file is saved, even mid-race.", new AcceptableValueRange<float>(0.8f, 6f)));
			GpsOffsetX = ctx.Config.Bind("GPS", "OffsetX", 0f, "Moves the GPS sideways on the dash from where the magnet would stand (dash units, right is positive). Takes effect as soon as the file is saved.");
			GpsOffsetY = ctx.Config.Bind("GPS", "OffsetY", -0.6f, "Moves the GPS up or down on the dash from where the magnet would stand (dash units, up is positive). Takes effect as soon as the file is saved.");
			GpsLitCasing = ctx.Config.Bind("GPS", "LitCasing", true, "Light the GPS's casing like the rest of the cockpit. Turn it off if the casing does not show (its screen still does). Takes effect as soon as the file is saved.");
			ViewTiles = ctx.Config.Bind("OpenWorld", "ViewTiles", 2.5f, new ConfigDescription("In the Open World (the Open World mod), how much country the minimap and the GPS show across, in its 250 m tiles: 2.5 is about 625 m. Takes effect as soon as the file is saved.", new AcceptableValueRange<float>(1f, 8f)));
			Verbose = ctx.Config.Bind("Debug", "VerboseLog", false, "Write each map build (the scene, the palette, the size of the lap) to the BepInEx log.");
			Migrate(ctx.Config);
			config = ctx.Config;

			if (Enabled.Value)
			{
				Toggle = ctx.Keys.Add(KeyCode.K, "Show / hide screen minimap", Controls.Section.Race);
				if (Toggle == null)
				{
					Log.LogWarning("The screen minimap's key K belongs to the game or another mod; it shows all the time (Display.Enabled off takes it away).");
				}
			}

			Gps.Register(ctx);
			ctx.OnSceneLoaded(Race.OnSceneLoaded);
			ctx.OnSceneLoaded(OpenWorldLink.OnSceneLoaded);
			ctx.OnSceneLoaded(GpsTap.OnSceneLoaded);
			GpsTap.Find();

			hud = new GameObject("MiniMapHud");
			UnityEngine.Object.DontDestroyOnLoad(hud);
			MiniMapHud runner = hud.AddComponent<MiniMapHud>();
			ctx.OnSceneLoaded(runner.OnSceneLoaded);
			ctx.OnUnload(() =>
			{
				if (hud != null)
				{
					UnityEngine.Object.Destroy(hud);
				}
				hud = null;
			});
		}
	}
}
