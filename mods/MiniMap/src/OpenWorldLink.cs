using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The link to the Open World mod's public map, <c>HalfGripOpenWorld.MapView</c> (Open World 0.4.6+). Each mod is compiled on its
	/// own, so MiniMap cannot name it: the type is found by name in the loaded assemblies, and its static members are made into delegates
	/// once, so reading them each frame costs no more than a call. Without the Open World mod (or an older one without MapView) nothing
	/// here does anything and every map is a race map.
	/// </summary>
	internal static class OpenWorldLink
	{
		private const string TypeName = "HalfGripOpenWorld.MapView";

		private static Func<bool> active;
		private static Func<double> originX;
		private static Func<double> originZ;
		private static Func<float> tileSize;
		private static Func<int> tilePixels;
		private static Func<int> version;
		private static WorldTiles.Fetch fetch;
		private static bool broken;
		private static int seen = int.MinValue;

		/// <summary>The tiles round the player's car, for the maps to draw.</summary>
		internal static readonly WorldTiles Tiles = new WorldTiles();

		/// <summary>
		/// A new scene: the tiles of the last one are of no use. The type is looked for at each scene load until it is found, so the link
		/// does not depend on which of the two mods the loader compiled first.
		/// </summary>
		internal static void OnSceneLoaded(Scene loaded)
		{
			Tiles.Clear();
			seen = int.MinValue;
			if (active == null && !broken)
			{
				Link();
			}
		}

		private static void Link()
		{
			try
			{
				Type type = null;
				foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					type = assembly.GetType(TypeName, false);
					if (type != null)
					{
						break;
					}
				}
				if (type == null)
				{
					return;
				}
				fetch = (WorldTiles.Fetch)Delegate.CreateDelegate(typeof(WorldTiles.Fetch), type.GetMethod("TryGetTile", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int), typeof(int), typeof(Color32[]) }, null));
				originX = Getter<double>(type, "OriginX");
				originZ = Getter<double>(type, "OriginZ");
				tileSize = Getter<float>(type, "TileSize");
				tilePixels = Getter<int>(type, "TilePixels");
				version = Getter<int>(type, "Version");
				active = Getter<bool>(type, "Active");
				Mod.Info("Minimap: linked to the Open World's map (" + type.Assembly.GetName().Name + ")");
			}
			catch (Exception e)
			{
				// An Open World whose MapView is not the one this was written for: race maps only, and no warning at every scene.
				broken = true;
				active = null;
				Mod.Log.LogWarning("Minimap: the Open World mod is there but its map could not be read, so the Open World gets no map: " + e.Message);
			}
		}

		private static Func<T> Getter<T>(Type type, string name)
		{
			PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
			if (property == null || property.PropertyType != typeof(T))
			{
				throw new MissingMemberException(TypeName, name);
			}
			return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), property.GetGetMethod());
		}

		/// <summary>True while an Open World race is loaded.</summary>
		internal static bool Active
		{
			get
			{
				if (active == null)
				{
					return false;
				}
				try
				{
					return active();
				}
				catch (Exception e)
				{
					active = null;
					broken = true;
					Mod.Log.LogWarning("Minimap: the Open World's map stopped answering: " + e.Message);
					return false;
				}
			}
		}

		/// <summary>
		/// Brings the tiles round the player at scene position <paramref name="player"/> up to date: the scene position plus the origin
		/// offset is where the car is in the world, whatever the floating origin has done. Called once a frame while the race is shown.
		/// </summary>
		internal static void Update(Vector3 player, float reach)
		{
			int now = version();
			if (now != seen)
			{
				if (seen == int.MinValue)
				{
					Tiles.TileSize = tileSize();
					Tiles.TilePixels = tilePixels();
				}
				seen = now;
				Tiles.Changed();
			}
			Tiles.Window(player.x + originX(), player.z + originZ(), reach, fetch);
		}
	}
}
