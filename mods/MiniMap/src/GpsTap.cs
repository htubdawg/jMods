using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The sound of the main menu's screen tap, for the GPS. The menu plays its clip <c>ScreenTap</c> from an audio source of its own
	/// (with a VariedSound, half its pitch either way), and no race scene has it, so it is looked for among the loaded assets at each
	/// scene load until found, which is the main menu at game start, and kept: a clip held in a static field is not unloaded with the
	/// menu. Not found, the GPS is tapped in silence.
	///
	/// The clip is much longer than one tap (22.8 s in game 0.418), and played to its end it went on making tap noises long after the
	/// poke (owner's test of 1.2.2). Its first sound is the tap; what follows sounds like menu clicks (owner, 1.2.3). So only the first
	/// is played: where each sound starts and ends is read off the clip's samples when it is found (<see cref="Measure"/>), and the
	/// GPS's own audio source is destroyed, which stops it, at the first one's end.
	/// </summary>
	internal static class GpsTap
	{
		private const string ClipName = "ScreenTap";
		/// <summary>How far either side of its own pitch the sound may land: the menu's VariedSound.pitchJitter.</summary>
		private const float PitchJitter = 0.5f;
		/// <summary>Seconds played when the clip's samples cannot be read (a compressed or streamed clip): about one short tap.</summary>
		private const float FallbackLength = 0.4f;
		/// <summary>Envelope window, and how long a quiet stretch must last to end a sound, in seconds.</summary>
		private const float Window = 0.01f;
		private const float Gap = 0.15f;
		/// <summary>Share of the clip's loudest sample under which it counts as quiet.</summary>
		private const float Quiet = 0.1f;

		private static AudioClip clip;
		/// <summary>The separate sounds in the clip, start and end in seconds; just its start when the samples cannot be read.</summary>
		private static readonly System.Collections.Generic.List<Vector2> sounds = new System.Collections.Generic.List<Vector2>();
		private static bool missed;

		internal static void OnSceneLoaded(Scene loaded) => Find();

		internal static void Find()
		{
			if (clip != null)
			{
				return;
			}
			try
			{
				foreach (AudioClip c in Resources.FindObjectsOfTypeAll<AudioClip>())
				{
					if (c != null && c.name == ClipName)
					{
						clip = c;
						Mod.Info("GPS: kept the main menu's tap sound '" + ClipName + "' (" + c.length.ToString("0.00") + " s, " + c.channels + " channel(s), " + c.frequency + " Hz, " + c.loadType + ", " + c.loadState + ")");
						Measure(c);
						return;
					}
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("GPS: could not look for the tap sound: " + e.Message);
			}
		}

		/// <summary>
		/// Splits the clip into its separate sounds: the loudness in <see cref="Window"/> steps, a sound starting at a step over
		/// <see cref="Quiet"/> of the loudest after a quiet stretch of <see cref="Gap"/>, and ending where the next such stretch begins.
		/// Logs them all, which tells what the long clip holds. Unreadable samples leave the clip's first <see cref="FallbackLength"/>.
		/// </summary>
		private static void Measure(AudioClip c)
		{
			sounds.Clear();
			sounds.Add(new Vector2(0f, Mathf.Min(FallbackLength, c.length)));
			try
			{
				int channels = Mathf.Max(1, c.channels);
				int frames = c.samples;
				float[] data = new float[frames * channels];
				if (frames <= 0 || !c.GetData(data, 0))
				{
					Mod.Info("GPS: the tap sound's samples cannot be read; playing its first " + FallbackLength + " s");
					return;
				}
				int step = Mathf.Max(1, Mathf.RoundToInt(c.frequency * Window));
				int windows = (frames + step - 1) / step;
				float[] loud = new float[windows];
				float peak = 0f;
				for (int i = 0; i < data.Length; i++)
				{
					float a = Mathf.Abs(data[i]);
					int w = i / channels / step;
					if (a > loud[w])
					{
						loud[w] = a;
					}
					peak = Mathf.Max(peak, a);
				}
				if (peak <= 0f)
				{
					Mod.Info("GPS: the tap sound is silent");
					return;
				}
				float quiet = peak * Quiet;
				int gap = Mathf.CeilToInt(Gap / Window);
				System.Collections.Generic.List<Vector2> found = new System.Collections.Generic.List<Vector2>();
				int from = -1;
				int run = gap;
				for (int w = 0; w < windows; w++)
				{
					if (loud[w] > quiet)
					{
						if (from < 0)
						{
							from = w;
						}
						run = 0;
					}
					else if (++run == gap && from >= 0)
					{
						found.Add(Span(from, w - gap + 1, c.length));
						from = -1;
					}
				}
				if (from >= 0)
				{
					found.Add(Span(from, windows, c.length));
				}
				if (found.Count == 0)
				{
					return;
				}
				sounds.Clear();
				sounds.AddRange(found);
				System.Text.StringBuilder text = new System.Text.StringBuilder();
				for (int i = 0; i < sounds.Count && i < 40; i++)
				{
					text.Append(i > 0 ? ", " : "").Append(sounds[i].x.ToString("0.00")).Append("-").Append(sounds[i].y.ToString("0.00"));
				}
				Mod.Info("GPS: the tap sound holds " + sounds.Count + " sound(s), at " + text + (sounds.Count > 40 ? ", ..." : "") + " s; a poke plays the first");
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("GPS: could not measure the tap sound (playing its first " + FallbackLength + " s): " + e.Message);
			}
		}

		/// <summary>One sound from step <paramref name="from"/> to step <paramref name="to"/>, in seconds, with a little tail.</summary>
		private static Vector2 Span(int from, int to, float length) => new Vector2(from * Window, Mathf.Min(length, to * Window + 0.05f));

		/// <summary>Plays the clip's first sound from <paramref name="on"/>, so it goes to the mixer group of the cockpit it is in.</summary>
		internal static void Play(GameObject on)
		{
			if (clip == null)
			{
				if (!missed)
				{
					missed = true;
					Mod.Info("GPS: the main menu's tap sound '" + ClipName + "' was not found; tapping in silence");
				}
				return;
			}
			Vector2 sound = sounds.Count > 0 ? sounds[0] : new Vector2(0f, FallbackLength);
			float pitch = 1f + UnityEngine.Random.Range(-PitchJitter, PitchJitter);
			AudioSource source = SoundController.Create2DAudioSource(clip, on, 1f, pitch);
			if (source == null)
			{
				return;
			}
			source.time = sound.x;
			source.Play();
			// Destroying the source stops it: nothing past this sound is heard.
			UnityEngine.Object.Destroy(source.gameObject, Mathf.Max(0.05f, sound.y - sound.x) / pitch);
		}
	}

	/// <summary>
	/// The GPS screen as the cockpit hand sees it: the game's own click target (Grabber clicks the <see cref="Clickable"/> over the
	/// interior collider it hovers, left and right), so the hand shows its pointing pose over the screen and the gamepad hand snaps to it.
	/// HalfGripTools has no interaction API of its own; every mod's cockpit parts go through Grabber like this.
	/// </summary>
	internal sealed class GpsButton : Clickable
	{
		/// <summary>
		/// Grabber's hover goes to the highest sortingLayer under the hand, then the nearest. The game's controls use -1 to 2 (the revolver's
		/// barrel -1, levers 0, the claxon 1, the shotgun's pump 2); the GPS is below them all, so a lever, a gun or a magazine over or
		/// beside the screen always wins and the GPS only gets the hand when nothing else wants it.
		/// </summary>
		internal const int SortingLayer = -100;

		internal Action Left;
		internal Action Right;

		protected override void OnClick() => Left?.Invoke();

		protected override void OnRightClick() => Right?.Invoke();
	}
}
