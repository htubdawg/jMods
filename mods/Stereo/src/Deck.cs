using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace HalfGripStereo
{
	/// <summary>
	/// What the stereo plays, and the audio source it plays it on. It lives on the stereo in the cockpit, so it goes with the scene, but
	/// what it remembers is static, so the stereo carries on from race to race in a session: the disc in it, the track and the place in
	/// it, power, pause. The volume is saved in the config.
	///
	/// The game starts its race song when the countdown ends (TrackManager.PlayRaceMusic). With the stereo in the car that call is skipped
	/// (see <see cref="RaceMusicPatch"/>) and the stereo starts instead; until the player picks a disc it plays the disc of the song the
	/// game picked, so a stereo left alone sounds like the game. The source goes to the game's Music mixer group, so the Music volume in
	/// the settings turns it down too; the game's pause menu pauses it with every other sound.
	///
	/// A soundtrack disc holds one song and repeats it, as the game does; next and previous change disc. The player's own disc plays the
	/// songs in their folder one after another (or shuffled), each loaded off the disk when its turn comes, and next and previous change
	/// song.
	/// </summary>
	internal sealed class Deck : MonoBehaviour
	{
		// The session's state, kept between races.
		/// <summary>The disc in the stereo, an index into <see cref="Discs.All"/>, or -1 for none.</summary>
		private static int disc = -1;
		/// <summary>The player chose the disc; until then each race loads the disc of the game's own pick.</summary>
		private static bool chosen;
		private static bool power = true;
		private static bool paused;
		/// <summary>The track on the player's own disc, and where in the song the stereo was.</summary>
		private static int track;
		private static float position;
		private static List<string> songs = new List<string>();
		private static int[] order = new int[0];
		/// <summary>The last song loaded off the disk, kept so the next race does not load it again.</summary>
		private static AudioClip folderClip;
		private static string folderPath;

		private AudioSource source;
		/// <summary>The music has started this race: the countdown ended, or the player touched the stereo.</summary>
		private bool live;
		private bool loading;
		private int loadToken;
		/// <summary>A message on the display's top line instead of the disc and time, until <see cref="noteUntil"/>.</summary>
		private string note;
		private float noteUntil;
		private int failures;
		private string trouble;
		/// <summary>The sound device changed (headphones plugged in, say): Unity stopped every source, so the song is started again.</summary>
		private bool deviceChanged;

		internal static Deck Active;

		internal bool Power => power;
		internal bool Paused => paused;
		internal int DiscIndex => disc;
		/// <summary>The knob's turn, 0 to 1.</summary>
		internal float VolumeTurn => Mod.Volume.Value / (float)Mod.VolumeSteps;

		private void Awake()
		{
			Active = this;
			source = gameObject.AddComponent<AudioSource>();
			source.playOnAwake = false;
			source.spatialBlend = 0f;
			source.loop = false;
			source.outputAudioMixerGroup = SoundController.MusicGroup;
			source.priority = 0;
			Discs.Find();
			AudioSettings.OnAudioConfigurationChanged += OnAudioChanged;
			// Put in after the countdown (the cockpit found late), the game's song is already playing: take over from it now, with the
			// song the game is playing (the map's own is only one of the game's picks: a tutorial's or one of the race songs can be it).
			TrackManager tm = FindFirstObjectByType<TrackManager>();
			if (tm != null && tm.raceStarted)
			{
				Go(GameSong() ?? RunState.ActiveMapMusic);
			}
		}

		private void OnDestroy()
		{
			AudioSettings.OnAudioConfigurationChanged -= OnAudioChanged;
			if (Active == this)
			{
				Active = null;
			}
			Remember();
		}

		/// <summary>
		/// A change of sound device stops every playing source. The game starts its own looping song again (SoundController.RestartMusic);
		/// the stereo's song is started again in <see cref="Update"/>, once the game is not paused, from where it was.
		/// </summary>
		private void OnAudioChanged(bool deviceWasChanged)
		{
			if (deviceWasChanged)
			{
				deviceChanged = true;
			}
		}

		/// <summary>A disc found late went in before the one in the stereo: the same disc is now one further on.</summary>
		internal static void DiscAdded(int at)
		{
			if (disc >= at)
			{
				disc++;
			}
		}

		/// <summary>The song the game's own music source is playing (the game names them "2D_Music_"), if one is.</summary>
		internal static AudioClip GameSong()
		{
			foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
			{
				if (s != null && s.isPlaying && s.clip != null && s.gameObject.name.StartsWith("2D_Music_", StringComparison.Ordinal))
				{
					return s.clip;
				}
			}
			return null;
		}

		/// <summary>Called in place of the game's race song: what the game picked loads its disc, unless the player chose one.</summary>
		internal void Go(AudioClip gamePick)
		{
			live = true;
			if (!chosen)
			{
				int i = Discs.IndexOf(gamePick);
				if (i >= 0 && i != disc)
				{
					disc = i;
					position = 0f;
				}
				else if (disc < 0)
				{
					disc = 0;
				}
				Mod.Info("Stereo: the race starts; the game picked '" + (gamePick != null ? gamePick.name : "nothing") + "', disc " + (disc + 1));
			}
			Restart();
		}

		// ---------------------------------------------------------------------------------------------------------------------------
		// The buttons.

		internal void Insert(int i)
		{
			if (i < 0 || i >= Discs.All.Count)
			{
				return;
			}
			live = true;
			chosen = true;
			if (i == disc)
			{
				return;
			}
			Remember();
			disc = i;
			track = 0;
			position = 0f;
			paused = false;
			if (Discs.All[i].Mix)
			{
				songs = Discs.Songs();
				Shuffle();
			}
			Mod.Info("Stereo: disc " + (i + 1) + " '" + Discs.All[i].Title + "' in");
			Restart();
		}

		internal void Eject()
		{
			live = true;
			chosen = true;
			if (disc < 0)
			{
				Note("NO DISC");
				return;
			}
			Mod.Info("Stereo: disc " + (disc + 1) + " out");
			disc = -1;
			position = 0f;
			Stop();
		}

		internal void TogglePower()
		{
			live = true;
			power = !power;
			if (power)
			{
				Restart();
			}
			else
			{
				Remember();
				Stop();
			}
		}

		internal void TogglePause()
		{
			live = true;
			if (!power)
			{
				return;
			}
			paused = !paused;
			if (paused)
			{
				Remember();
				source.Pause();
			}
			else if (source.clip != null && !loading)
			{
				source.UnPause();
				if (!source.isPlaying)
				{
					Restart();
				}
			}
			else
			{
				Restart();
			}
		}

		/// <summary>Next or previous song on the player's disc, next or previous disc on a soundtrack disc.</summary>
		internal void Skip(int by)
		{
			live = true;
			if (!power)
			{
				return;
			}
			if (disc >= 0 && Discs.All[disc].Mix && songs.Count > 0)
			{
				// Back within the first 3 seconds goes to the song before; later, to the start of this one, as CD players do.
				if (by < 0 && source.clip != null && source.time > 3f)
				{
					position = 0f;
					Restart();
					return;
				}
				track = ((track + by) % songs.Count + songs.Count) % songs.Count;
				position = 0f;
				failures = 0;
				Restart();
				return;
			}
			int n = Discs.All.Count;
			int next = disc < 0 ? 0 : ((disc + by) % n + n) % n;
			Insert(next);
		}

		/// <summary>The knob turned to step <paramref name="v"/> (0 to 10); false if it was already there.</summary>
		internal bool SetVolume(int v)
		{
			v = Mathf.Clamp(v, 0, Mod.VolumeSteps);
			if (v == Mod.Volume.Value)
			{
				return false;
			}
			Mod.Volume.Value = v;
			Note("VOL " + new string('█', v) + new string(' ', Mod.VolumeSteps - v));
			return true;
		}

		private void Note(string text)
		{
			note = text;
			noteUntil = Time.unscaledTime + 1.5f;
		}

		// ---------------------------------------------------------------------------------------------------------------------------
		// Playing.

		private void Remember()
		{
			if (source != null && source.clip != null && !loading)
			{
				position = source.time;
			}
		}

		private void Stop()
		{
			loadToken++;
			loading = false;
			if (source != null)
			{
				source.Stop();
				source.clip = null;
			}
		}

		/// <summary>Plays what the state says from <see cref="position"/>, loading a song off the disk first if it has to.</summary>
		private void Restart()
		{
			Stop();
			trouble = null;
			if (!power || paused || !live || disc < 0 || disc >= Discs.All.Count)
			{
				return;
			}
			Disc d = Discs.All[disc];
			if (!d.Mix)
			{
				Play(d.Clip, true);
				return;
			}
			if (songs.Count == 0)
			{
				songs = Discs.Songs();
				Shuffle();
			}
			if (songs.Count == 0)
			{
				trouble = "EMPTY";
				return;
			}
			track = Mathf.Clamp(track, 0, songs.Count - 1);
			string path = songs[order.Length == songs.Count ? order[track] : track];
			if (folderClip != null && folderPath == path)
			{
				Play(folderClip, false);
				return;
			}
			StartCoroutine(Load(path, ++loadToken));
		}

		private void Play(AudioClip clip, bool loop)
		{
			if (clip == null)
			{
				trouble = "NO SONG";
				return;
			}
			QuietGameMusic();
			source.clip = clip;
			source.loop = loop;
			source.volume = Gain();
			source.Play();
			if (position > 0f && position < clip.length - 0.5f)
			{
				source.time = position;
			}
		}

		private static float Gain() => Mathf.Pow(Mod.Volume.Value / (float)Mod.VolumeSteps, 1.6f) * Mod.Loudness.Value;

		/// <summary>
		/// Stops the game's own race song if one is playing (the game names its music sources "2D_Music_"), for a stereo fitted after the
		/// song began or a race song started by some other route.
		/// </summary>
		private void QuietGameMusic()
		{
			foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
			{
				if (s != null && s != source && s.isPlaying && s.gameObject.name.StartsWith("2D_Music_", StringComparison.Ordinal))
				{
					s.Stop();
					Mod.Info("Stereo: stopped the game's song '" + (s.clip != null ? s.clip.name : "?") + "'");
				}
			}
		}

		private IEnumerator Load(string path, int token)
		{
			loading = true;
			if (folderClip != null)
			{
				Destroy(folderClip);
				folderClip = null;
				folderPath = null;
			}
			Discs.Types.TryGetValue(Path.GetExtension(path), out AudioType type);
			string uri = new Uri(path).AbsoluteUri;
			using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, type))
			{
				if (req.downloadHandler is DownloadHandlerAudioClip handler)
				{
					// Decoded as it plays, not all at once: a long song would take a while and a lot of memory.
					handler.streamAudio = true;
				}
				yield return req.SendWebRequest();
				if (token != loadToken)
				{
					yield break;
				}
				loading = false;
				AudioClip clip = null;
				if (req.result == UnityWebRequest.Result.Success)
				{
					try
					{
						clip = DownloadHandlerAudioClip.GetContent(req);
					}
					catch (Exception e)
					{
						Mod.Log.LogWarning("Stereo: could not read " + path + ": " + e.Message);
					}
				}
				else
				{
					Mod.Log.LogWarning("Stereo: could not read " + path + ": " + req.error);
				}
				if (clip == null || clip.length <= 0f && clip.loadState == AudioDataLoadState.Failed)
				{
					// Skip a file that cannot be read, but stop once every song has failed rather than going round for ever.
					if (++failures >= songs.Count)
					{
						trouble = "CANT READ";
						yield break;
					}
					track = (track + 1) % songs.Count;
					position = 0f;
					Restart();
					yield break;
				}
				failures = 0;
				clip.name = Path.GetFileNameWithoutExtension(path);
				clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
				folderClip = clip;
				folderPath = path;
				Mod.Info("Stereo: playing " + path + " (" + clip.length.ToString("0.0") + " s)");
				Play(clip, false);
			}
		}

		private void Shuffle()
		{
			order = new int[songs.Count];
			for (int i = 0; i < order.Length; i++)
			{
				order[i] = i;
			}
			if (!Mod.Shuffle.Value)
			{
				return;
			}
			System.Random rng = new System.Random();
			for (int i = order.Length - 1; i > 0; i--)
			{
				int j = rng.Next(i + 1);
				(order[i], order[j]) = (order[j], order[i]);
			}
		}

		private void Update()
		{
			if (source == null)
			{
				return;
			}
			source.volume = Gain();
			if (deviceChanged && !InteriorMath.Paused && Time.timeScale > 0f)
			{
				deviceChanged = false;
				if (source.clip != null && !source.isPlaying && !loading && power && !paused)
				{
					Mod.Info("Stereo: the sound device changed; the song goes on from " + position.ToString("0.0") + " s");
					Restart();
					return;
				}
			}
			if (source.clip != null && source.isPlaying)
			{
				position = source.time;
			}
			// A song on the player's disc has ended (not paused by the game's menu, which pauses every source): on to the next.
			if (source.clip != null && !source.loop && !source.isPlaying && !loading && power && !paused && !InteriorMath.Paused && Time.timeScale > 0f
				&& (source.timeSamples == 0 || source.time >= source.clip.length - 0.1f))
			{
				if (disc >= 0 && Discs.All[disc].Mix && songs.Count > 0)
				{
					track = (track + 1) % songs.Count;
					position = 0f;
					Restart();
				}
			}
		}

		// ---------------------------------------------------------------------------------------------------------------------------
		// The display.

		/// <summary>The display's two lines: the disc, track and time on top; the song's name below (whole: the display scrolls it).</summary>
		internal void Lines(out string top, out string bottom)
		{
			if (note != null && Time.unscaledTime < noteUntil)
			{
				top = note;
			}
			else
			{
				note = null;
				top = null;
			}
			if (disc < 0 || disc >= Discs.All.Count)
			{
				top ??= "NO DISC";
				bottom = Discs.All.Count > 1 ? "PICK A CD" : "";
				return;
			}
			Disc d = Discs.All[disc];
			string cd = "CD" + (disc + 1);
			if (d.Mix)
			{
				if (trouble != null)
				{
					top ??= cd + " " + trouble;
					bottom = trouble == "EMPTY" ? "ADD SONGS TO " + Discs.Folder() : "";
					return;
				}
				int n = Mathf.Max(1, songs.Count);
				string t = (track + 1).ToString("00");
				top ??= cd + " " + t + (loading ? " LOAD" : " " + Clock());
				string path = songs.Count > 0 ? songs[order.Length == songs.Count ? order[Mathf.Clamp(track, 0, songs.Count - 1)] : Mathf.Clamp(track, 0, songs.Count - 1)] : "";
				bottom = Path.GetFileNameWithoutExtension(path);
				return;
			}
			top ??= cd + (paused ? " ‖ " : " ▶ ") + Clock();
			bottom = trouble ?? d.Title;
		}

		/// <summary>The time into the song, m:ss (live while playing, else where it stopped).</summary>
		private string Clock()
		{
			float t = source != null && source.clip != null && !loading ? source.time : position;
			int s = Mathf.Max(0, Mathf.FloorToInt(t));
			return (s / 60) + ":" + (s % 60).ToString("00");
		}
	}
}
