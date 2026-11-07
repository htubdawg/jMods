using BepInEx.Configuration;
using BepInEx.Logging;

namespace HalfGripSlapPack
{
	/// <summary>Keeps routine diagnostics opt-in; warnings and errors remain visible.</summary>
	internal static class ModLog
	{
		private static ConfigEntry<bool> verbose;
		private static ManualLogSource source;
		internal static bool Verbose => verbose != null && verbose.Value;

		/// <summary>Binds the common log gate before any feature initializes.</summary>
		internal static void Initialize(ConfigFile config, ManualLogSource log)
		{
			source = log;
			verbose = config.Bind("Diagnostics", "VerboseLogging", false, "Log feature binding, the weather provider, and periodic refraction, mirror-visibility and held-gun occlusion diagnostics. Warnings and errors are always logged.");
		}

		/// <summary>Writes shared integration diagnostics without exposing a logger to pure model code.</summary>
		internal static void Info(string message) { if (Verbose) source?.LogInfo(message); }

		/// <summary>Emits routine information only when verbose diagnostics are enabled.</summary>
		internal static void Info(ManualLogSource log, string message)
		{
			if (verbose != null && verbose.Value) log.LogInfo(message);
		}
	}
}
