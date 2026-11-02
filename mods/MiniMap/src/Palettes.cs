using System;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>The colours of one minimap: what lies off the road, the road, the line along its edges and the ring round the map.</summary>
	internal sealed class Palette
	{
		public string Name;
		public Color Ground;
		public Color Road;
		public Color Edge;
		public Color Frame;
		/// <summary>False for a map that is drawn as a night map already (Tokyo), so a night weather does not darken it a second time.</summary>
		public bool DimAtNight = true;

		public Palette Copy() => (Palette)MemberwiseClone();
	}

	/// <summary>
	/// Picks the minimap's colours for a race. The base map is told from the scene's name, not from the run map's entry: ProcTrack's maps
	/// and any other mod's map that reuses one of the game's scenes keep that scene's ground and road, so they get its colours too. A scene
	/// none of these know is coloured from its own surfaces (<see cref="Sampler"/>). Then the weather shades the result: night darkens it,
	/// snow and ash lie over the ground, and the forest's red weather turns its grass red.
	/// </summary>
	internal static class Palettes
	{
		/// <summary>SCENE_TRACK_1_RaceTrack: an asphalt ring (MAT_Asphalt1) on a dirt floor (MAT_Dirt1), with concrete barriers.</summary>
		internal static readonly Palette Stadium = new Palette
		{
			Name = "Stadium",
			Ground = Rgb(0x8a6e4f),
			Road = Rgb(0x3a3a3e),
			Edge = Rgb(0xc9c7c0),
			Frame = Rgb(0x2a2724),
		};

		/// <summary>SCENE_TRACK_2_Forrest_Field: a dirt road (ForrestRoadTexture) through grass and wheat fields.</summary>
		internal static readonly Palette Forest = new Palette
		{
			Name = "Forest",
			Ground = Rgb(0x4f7d34),
			Road = Rgb(0x8c6239),
			Edge = Rgb(0x4e3420),
			Frame = Rgb(0x26331c),
		};

		/// <summary>SCENE_TRACK_3_WinterMountain: a grey road across a snowed-in crater floor.</summary>
		internal static readonly Palette Mountain = new Palette
		{
			Name = "Mountain",
			Ground = Rgb(0xeef2f7),
			Road = Rgb(0x515a66),
			Edge = Rgb(0x8fb3d9),
			Frame = Rgb(0x34404f),
		};

		/// <summary>SCENE_TRACK_4_FireFactory: a road across an open dirt basin round a factory yard, hazard orange along it.</summary>
		internal static readonly Palette Factory = new Palette
		{
			Name = "Factory",
			Ground = Rgb(0x96703f),
			Road = Rgb(0x34302d),
			Edge = Rgb(0xe07b24),
			Frame = Rgb(0x2b231b),
		};

		/// <summary>SCENE_TRACK_5_TokyoTemple: a night city, dark asphalt between walls, with neon along the road.</summary>
		internal static readonly Palette Tokyo = new Palette
		{
			Name = "Tokyo",
			Ground = Rgb(0x2b2d3c),
			Road = Rgb(0x17171d),
			Edge = Rgb(0xff4fa8),
			Frame = Rgb(0x121219),
			DimAtNight = false,
		};

		/// <summary>When a scene is not known and its surfaces cannot be read: a grey road on grass.</summary>
		internal static readonly Palette Default = new Palette
		{
			Name = "Default",
			Ground = Rgb(0x5d7a4a),
			Road = Rgb(0x3c3c40),
			Edge = Rgb(0xd8d8d0),
			Frame = Rgb(0x222222),
		};

		/// <summary>
		/// The Open World, whose map is the colours of its own tile pictures: only the ring round the map and the ground where a tile is
		/// not loaded yet come from here, in the dark green of its woods.
		/// </summary>
		internal static readonly Palette OpenWorld = new Palette
		{
			Name = "OpenWorld",
			Ground = Rgb(0x2f3a2a),
			Road = Rgb(0x46464a),
			Edge = Rgb(0xeeece4),
			Frame = Rgb(0x1f261c),
		};

		/// <summary>The scene's base map from its name, or null for a scene that is none of the game's race maps.</summary>
		internal static Palette Known(string scene)
		{
			string s = (scene ?? "").ToLowerInvariant();
			if (s.Contains("racetrack") || s.Contains("stadium"))
			{
				return Stadium;
			}
			if (s.Contains("forrest") || s.Contains("forest"))
			{
				return Forest;
			}
			if (s.Contains("wintermountain") || s.Contains("mountain"))
			{
				return Mountain;
			}
			if (s.Contains("factory"))
			{
				return Factory;
			}
			if (s.Contains("tokyo"))
			{
				return Tokyo;
			}
			return null;
		}

		/// <summary>The colours for a race in this scene, under this weather (null: the scene's own weather is not known).</summary>
		internal static Palette For(string scene, WeatherProfile weather, PathManager line)
		{
			Palette basis = Known(scene);
			if (basis == null)
			{
				basis = Sampler.FromScene(line) ?? Default;
			}
			Palette p = basis.Copy();
			if (weather == null)
			{
				Contrast(p);
				return p;
			}

			string w = weather.name ?? "";
			if (basis == Forest && w.IndexOf("red", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				// RedForrest: the forest under red ash, its grass gone rust red.
				p.Ground = Rgb(0x8a3b24);
				p.Frame = Rgb(0x3a1a12);
				p.Name += " (red)";
			}
			if (weather.snow && basis != Mountain)
			{
				p.Ground = Color.Lerp(p.Ground, Mountain.Ground, 0.6f);
				p.Name += " (snow)";
			}
			if (weather.ash)
			{
				p.Ground = Color.Lerp(p.Ground, Rgb(0x8c8c8c), 0.3f);
				p.Name += " (ash)";
			}
			if (weather.headlightsActive && p.DimAtNight)
			{
				// Darker, the road more than the ground so the two stay apart; the edge line stays as it is.
				p.Ground = Scale(p.Ground, 0.72f);
				p.Road = Scale(p.Road, 0.7f);
				p.Frame = Scale(p.Frame, 0.7f);
				p.Name += " (night)";
			}
			Contrast(p);
			return p;
		}

		/// <summary>Pulls the road away from the ground when the two are too alike in brightness to tell apart (a snowed-over dirt road).</summary>
		internal static void Contrast(Palette p)
		{
			float g = Luma(p.Ground);
			float r = Luma(p.Road);
			if (Mathf.Abs(g - r) >= 0.15f)
			{
				return;
			}
			p.Road = g > 0.45f ? Scale(p.Road, 0.55f) : Color.Lerp(p.Road, Color.white, 0.35f);
		}

		internal static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

		internal static Color Scale(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

		internal static Color Rgb(int hex) => new Color(((hex >> 16) & 0xff) / 255f, ((hex >> 8) & 0xff) / 255f, (hex & 0xff) / 255f, 1f);
	}
}
