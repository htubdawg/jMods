using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfGripStereo
{
	/// <summary>One CD in the wallet: a song from the game's soundtrack, or the player's own folder (<see cref="Mix"/>).</summary>
	internal sealed class Disc
	{
		/// <summary>The name on the display: the clip's name without " OST" ("FOREST"), or "MY MUSIC".</summary>
		public string Title;
		/// <summary>The song, for a soundtrack disc. Kept in a static list, so it is not unloaded with the scene that loaded it.</summary>
		public AudioClip Clip;
		/// <summary>The disc that plays the songs in the player's folder.</summary>
		public bool Mix;
		/// <summary>Other names the same song goes by (a clip loaded twice under two names), so the game's pick still finds its disc.</summary>
		public readonly List<string> Aliases = new List<string>();
	}

	/// <summary>
	/// The discs the stereo can play. The game's songs are not listed anywhere as a soundtrack: the race songs are clips in Resources,
	/// loaded by name (TrackManager's four race songs), and each map names its own (MapDefinition.musicTrack and altMusicTrack: Winter for
	/// the Mountain, a second Neon song drawn now and then for Tokyo). So they are gathered from all three places, at game start and at
	/// each scene load until every known song is found, and kept: game 0.418 has five map songs (Forest, Neon, Factory, Stadion, Winter)
	/// and Neon 2, Tokyo's other song.
	///
	/// The same song can be reachable twice (a map's reference and a Resources load are the same object, but a song could also be shipped
	/// twice under two names), so a clip with the same length and samples as one already found is not given a disc of its own; each merge
	/// is logged. The main menu's song is not a race song and is left out. Last comes the player's own disc, the songs in a folder on
	/// their computer (<see cref="Folder"/>).
	/// </summary>
	internal static class Discs
	{
		/// <summary>The game's songs in the wallet's order, as the game names them. Songs found that are not here go after them.</summary>
		internal static readonly string[] Known = { "Forest OST", "Neon OST", "Neon 2 OST", "Factory OST", "Stadion OST", "Winter OST" };

		/// <summary>The discs in the wallet's order: the soundtrack, then the player's own disc.</summary>
		internal static readonly List<Disc> All = new List<Disc> { new Disc { Title = "MY MUSIC", Mix = true } };

		/// <summary>Goes up by one each time a disc is added, so the wallet knows to redraw itself.</summary>
		internal static int Version;

		private static bool complete;

		internal static void OnSceneLoaded(Scene scene) => Find();

		/// <summary>Looks for songs not found yet. Cheap once every known song is in.</summary>
		internal static void Find()
		{
			if (complete)
			{
				return;
			}
			try
			{
				List<AudioClip> found = new List<AudioClip>();
				foreach (string name in Known)
				{
					AudioClip c = null;
					try
					{
						c = Resources.Load<AudioClip>(name);
					}
					catch (Exception)
					{
					}
					if (c != null)
					{
						found.Add(c);
					}
				}
				foreach (MapDefinition map in Resources.FindObjectsOfTypeAll<MapDefinition>())
				{
					if (map == null)
					{
						continue;
					}
					if (map.musicTrack != null)
					{
						found.Add(map.musicTrack);
					}
					if (map.altMusicTrack != null)
					{
						found.Add(map.altMusicTrack);
					}
				}
				foreach (AudioClip c in Resources.FindObjectsOfTypeAll<AudioClip>())
				{
					if (c != null && c.name.EndsWith(" OST", StringComparison.OrdinalIgnoreCase))
					{
						found.Add(c);
					}
				}
				foreach (AudioClip c in found)
				{
					Add(c);
				}
				complete = Known.All(n => All.Any(d => d.Clip != null && (d.Clip.name == n || d.Aliases.Contains(n))));
				if (complete)
				{
					Mod.Info("Stereo: every known song found: " + string.Join(", ", All.Select(Describe)));
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Stereo: could not look for the game's songs: " + e.Message);
			}
		}

		private static string Describe(Disc d) => d.Mix ? "'" + d.Title + "' (folder)" : "'" + d.Clip.name + "' (" + d.Clip.length.ToString("0.0") + " s" + (d.Aliases.Count > 0 ? ", also " + string.Join(", ", d.Aliases) : "") + ")";

		private static void Add(AudioClip clip)
		{
			foreach (Disc d in All)
			{
				if (d.Clip == clip || (d.Clip != null && d.Clip.name == clip.name && SameSong(d.Clip, clip)))
				{
					return;
				}
			}
			foreach (Disc d in All)
			{
				if (d.Clip != null && SameSong(d.Clip, clip))
				{
					if (!d.Aliases.Contains(clip.name))
					{
						d.Aliases.Add(clip.name);
						Mod.Info("Stereo: '" + clip.name + "' is the same song as '" + d.Clip.name + "' (" + clip.samples + " samples); one disc for both");
					}
					return;
				}
			}
			Disc disc = new Disc { Title = TitleOf(clip.name), Clip = clip };
			int at = Rank(clip.name);
			int index = All.FindIndex(d => d.Mix || Rank(d.Clip.name) > at);
			if (index < 0)
			{
				index = All.Count;
			}
			All.Insert(index, disc);
			Version++;
			// The discs after it moved up one: what points at them by number follows (the disc in the stereo, one out of the slot).
			Deck.DiscAdded(index);
			StereoDash.DiscAdded(index);
			Mod.Info("Stereo: found the song '" + clip.name + "' (" + clip.length.ToString("0.0") + " s, " + clip.channels + " channel(s), " + clip.frequency + " Hz, " + clip.loadType + "), disc " + (All.IndexOf(disc) + 1));
		}

		private static int Rank(string name)
		{
			int i = Array.IndexOf(Known, name);
			return i < 0 ? Known.Length : i;
		}

		/// <summary>"Forest OST" to "FOREST".</summary>
		private static string TitleOf(string name)
		{
			string t = name;
			if (t.EndsWith(" OST", StringComparison.OrdinalIgnoreCase))
			{
				t = t.Substring(0, t.Length - 4);
			}
			return t.Trim().ToUpperInvariant();
		}

		/// <summary>
		/// Whether two clips are the same recording: the same length to the sample, and, where both can be read, the same samples at a few
		/// places. Music is often streamed and cannot be read; then the length alone decides, which two different songs never share.
		/// </summary>
		private static bool SameSong(AudioClip a, AudioClip b)
		{
			if (a.samples != b.samples || a.channels != b.channels || a.frequency != b.frequency)
			{
				return false;
			}
			try
			{
				int n = 256 * a.channels;
				float[] x = new float[n], y = new float[n];
				foreach (float at in new[] { 0.25f, 0.5f, 0.75f })
				{
					int from = (int)(a.samples * at);
					if (!a.GetData(x, from) || !b.GetData(y, from))
					{
						return true;
					}
					for (int i = 0; i < n; i++)
					{
						if (Mathf.Abs(x[i] - y[i]) > 1e-3f)
						{
							return false;
						}
					}
				}
			}
			catch (Exception)
			{
			}
			return true;
		}

		/// <summary>The disc for the song the game picked for this race, if it has one.</summary>
		internal static int IndexOf(AudioClip clip)
		{
			if (clip == null)
			{
				return -1;
			}
			Add(clip);
			return All.FindIndex(d => d.Clip == clip || (d.Clip != null && (d.Clip.name == clip.name || d.Aliases.Contains(clip.name))));
		}

		// ---------------------------------------------------------------------------------------------------------------------------
		// The player's own disc.

		/// <summary>The kinds of file Unity can read off the disk, by extension.</summary>
		internal static readonly Dictionary<string, AudioType> Types = new Dictionary<string, AudioType>(StringComparer.OrdinalIgnoreCase)
		{
			{ ".ogg", AudioType.OGGVORBIS },
			{ ".mp3", AudioType.MPEG },
			{ ".wav", AudioType.WAV },
			{ ".aif", AudioType.AIFF },
			{ ".aiff", AudioType.AIFF },
		};

		private static bool folderMade;

		/// <summary>
		/// The folder the player's own disc plays: Music.Folder, or when that is empty "Half Grip" in their Music folder. It is made the
		/// first time it is asked for if it is not there, so the player finds it.
		/// </summary>
		internal static string Folder()
		{
			string folder = Mod.MusicFolder.Value;
			if (string.IsNullOrWhiteSpace(folder))
			{
				string music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
				if (string.IsNullOrEmpty(music))
				{
					music = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music");
				}
				folder = Path.Combine(music, "Half Grip");
			}
			folder = Environment.ExpandEnvironmentVariables(folder.Trim().Trim('"'));
			if (!folderMade)
			{
				folderMade = true;
				try
				{
					if (!Directory.Exists(folder))
					{
						Directory.CreateDirectory(folder);
						Mod.Info("Stereo: made the folder for your own songs: " + folder);
					}
				}
				catch (Exception e)
				{
					Mod.Log.LogWarning("Stereo: could not make the folder for your own songs (" + folder + "): " + e.Message);
				}
			}
			return folder;
		}

		/// <summary>The songs in the player's folder (not its subfolders), sorted by file name, so a "01 ..." numbering sets the order.</summary>
		internal static List<string> Songs()
		{
			string folder = Folder();
			try
			{
				if (!Directory.Exists(folder))
				{
					return new List<string>();
				}
				List<string> songs = Directory.GetFiles(folder).Where(f => Types.ContainsKey(Path.GetExtension(f))).ToList();
				songs.Sort(StringComparer.OrdinalIgnoreCase);
				Mod.Info("Stereo: " + songs.Count + " song(s) in " + folder);
				return songs;
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Stereo: could not read the folder " + folder + ": " + e.Message);
				return new List<string>();
			}
		}
	}
}
