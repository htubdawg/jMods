using System;
using HarmonyLib;
using UnityEngine;

namespace HalfGripStereo
{
	/// <summary>
	/// With the stereo in the car, the game's race song is not started when the countdown ends: the stereo starts instead, with the song the
	/// game picked (so its disc goes in, unless the player chose one). Without the stereo (Stereo.Enabled off), or if anything here fails, the game plays its
	/// song as usual.
	/// </summary>
	[HarmonyPatch(typeof(TrackManager), "PlayRaceMusic")]
	internal static class RaceMusicPatch
	{
		private static bool Prefix(TrackManager __instance)
		{
			Deck deck = Deck.Active;
			if (deck == null)
			{
				return true;
			}
			try
			{
				AudioClip pick = Traverse.Create(__instance).Method("PickRaceSong").GetValue<AudioClip>();
				deck.Go(pick);
				return false;
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Stereo: could not take over the race song; the game plays it: " + e.Message);
				return true;
			}
		}
	}
}
