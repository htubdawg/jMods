using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfGripStereo
{
	/// <summary>
	/// The stereo's clicks, borrowed from the game's own sounds (found by name among the loaded clips at each scene load until found, and
	/// kept): the hand's grab click for a key (and, quiet and high, for each step of the volume knob), the gun's reload, higher, for a
	/// disc going in or out of the slot, the hand's second grab click for a disc taken out of a sleeve or the slot. Not found, it works
	/// in silence.
	/// </summary>
	internal static class Sounds
	{
		private const string KeyName = "Grab1";
		private const string DiscName = "Reload";
		private const string PullName = "Grab2";

		private static AudioClip key;
		private static AudioClip disc;
		private static AudioClip pull;

		internal static void OnSceneLoaded(Scene scene) => Find();

		internal static void Find()
		{
			if (key != null && disc != null && pull != null)
			{
				return;
			}
			try
			{
				foreach (AudioClip c in Resources.FindObjectsOfTypeAll<AudioClip>())
				{
					if (c == null)
					{
						continue;
					}
					if (key == null && c.name == KeyName)
					{
						key = c;
						Mod.Info("Stereo: kept the sound '" + KeyName + "' for the keys");
					}
					else if (disc == null && c.name == DiscName)
					{
						disc = c;
						Mod.Info("Stereo: kept the sound '" + DiscName + "' for the discs");
					}
					else if (pull == null && c.name == PullName)
					{
						pull = c;
						Mod.Info("Stereo: kept the sound '" + PullName + "' for taking a disc");
					}
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Stereo: could not look for its sounds: " + e.Message);
			}
		}

		internal static void Click(GameObject on) => Play(key, on, 0.5f, 1.5f);

		internal static void Disc(GameObject on) => Play(disc, on, 0.45f, 1.35f);

		internal static void Pull(GameObject on) => Play(pull ?? key, on, 0.45f, 1.2f);

		/// <summary>One step of the volume knob.</summary>
		internal static void Tick(GameObject on) => Play(key, on, 0.2f, 2.2f);

		/// <summary>From <paramref name="on"/>, so it goes to the mixer group of the cockpit it is in.</summary>
		private static void Play(AudioClip clip, GameObject on, float volume, float pitch)
		{
			if (clip == null)
			{
				return;
			}
			AudioSource s = SoundController.Create2DAudioSource(clip, on, volume, pitch * (1f + UnityEngine.Random.Range(-0.04f, 0.04f)));
			if (s != null)
			{
				s.Play();
				UnityEngine.Object.Destroy(s.gameObject, clip.length / s.pitch + 0.1f);
			}
		}
	}
}
