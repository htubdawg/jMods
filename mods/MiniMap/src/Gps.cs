using System.Collections.Generic;
using System.Linq;
using HalfGripTools;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The GPS: a shop item that stands on the dash in one of the two module slots (where the nitro lever, the magnet or the slow-mo
	/// buttons go) and shows the minimap on its screen. It is a copy of the game's magnet item, which has no code of its own, so it gets
	/// the magnet's card, its MEDIUM inventory slot and its module mount for free; what made it a magnet (the lever, the magnet on the
	/// car's outside) is replaced by <see cref="Prefab"/>.
	///
	/// The game fits an item by copying its interior prefab onto a free mount, keeping the prefab's own position. The cockpit is made of
	/// flat sprites, so the GPS is two: the casing, lit like the dash, and the screen, unlit so it glows. Their pixels are sized from the
	/// magnet's own sprites, so the GPS has the same pixel size as the rest of the cockpit.
	/// </summary>
	internal static class Gps
	{
		internal const string Id = "minimap.gps";
		private const int Cost = 25;

		/// <summary>
		/// Size of one pixel of the magnet's biggest sprite, in the units of the item's interior. Only logged: that sprite is drawn much
		/// finer than the cockpit, so <see cref="GpsScreen"/> measures the dash itself.
		/// </summary>
		internal static float Texel;
		/// <summary>Where the bottom middle of the casing goes, in the units of the item's interior: where the magnet's base stands.</summary>
		internal static Vector3 Foot;
		internal static Material Lit;
		internal static Material Unlit;
		internal static int SortingLayer;
		internal static int SortingOrder;

		/// <summary>The interior prefab: never shown itself, it hangs under an inactive holder and is copied onto the dash.</summary>
		internal static GameObject Prefab;
		private static GameObject holder;

		internal static void Register(ModContext ctx)
		{
			ctx.Items.Add(Id, catalog => Make(ctx, catalog));
			ctx.OnUnload(() =>
			{
				if (holder != null)
				{
					Object.Destroy(holder);
				}
				holder = null;
				Prefab = null;
			});
		}

		private static UpgradeComponent Make(ModContext ctx, ItemCatalog catalog)
		{
			// The magnet, told by its lever; failing that any module item without code of its own.
			List<GenericUpgrade> modules = catalog.items.OfType<GenericUpgrade>().Where(u => u != null && u.mountSlot == MountSlot.Module && u.interiorPrefab != null).ToList();
			GenericUpgrade basis = modules.FirstOrDefault(u => u.interiorPrefab.GetComponentInChildren<NMagnetLever>(true) != null) ?? modules.FirstOrDefault();
			if (basis == null)
			{
				Mod.Log.LogWarning("GPS: the game has no module item to copy (the magnet), so there is no GPS in the shop.");
				return null;
			}

			GenericUpgrade made = ctx.Items.Clone(basis, Id);
			made.description = "GPS\n<color=#7e7e7e>Recalculating...</color>";
			made.interaction = "Shows the whole lap and every car, on the dash";
			made.secondaryDescription = "";
			made.cost = Cost;
			made.tier = ItemTier.Stock;
			made.disabled = false;
			made.startsLocked = false;
			made.upgradeCategory = UpgradeCategory.MEDIUM;
			made.mountSlot = MountSlot.Module;
			made.statBoosts = new List<StatBoost>();
			made.hideStatLines = true;
			made.forceShow = new List<string>();
			made.forceHide = new List<string>();
			made.customSprites = new List<CustomSprite>();
			made.exteriorPrefab = null;
			made.exteriorSocket = "";
			if (made is PassiveUpgrade passive)
			{
				passive.charmSprite = null;
			}

			Measure(basis.interiorPrefab);
			made.interiorPrefab = BuildPrefab(basis.interiorPrefab);
			made.image = Icon(catalog);
			Mod.Info("GPS: made from '" + basis.id + "' (" + basis.GetType().Name + "), magnet pixel " + Texel.ToString("0.0000") + ", foot " + Foot + ", sorting " + SortingLayer + "/" + SortingOrder + ", lit '" + (Lit != null ? Lit.name : "none") + "', unlit '" + (Unlit != null ? Unlit.name : "none") + "'");
			return made;
		}

		/// <summary>
		/// Reads the magnet's interior: the pixel size, position, sorting and material of its biggest sprite (the base it stands on), and
		/// an unlit sprite material from any of its parts.
		/// </summary>
		private static void Measure(GameObject magnet)
		{
			Transform root = magnet.transform;
			SpriteRenderer biggest = null;
			float area = 0f;
			foreach (SpriteRenderer r in magnet.GetComponentsInChildren<SpriteRenderer>(true))
			{
				if (r.sprite == null)
				{
					continue;
				}
				float a = r.sprite.rect.width * r.sprite.rect.height;
				if (a > area)
				{
					area = a;
					biggest = r;
				}
				if (Unlit == null && r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.IndexOf("Unlit", System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					Unlit = r.sharedMaterial;
				}
			}
			if (biggest == null)
			{
				Mod.Log.LogWarning("GPS: the magnet has no sprites to measure the dash by; guessing the GPS's size.");
				return;
			}

			// Scale from the sprite up to (not including) the prefab's root, which the game copies onto the instance as it is.
			float scale = 1f;
			for (Transform t = biggest.transform; t != null && t != root; t = t.parent)
			{
				scale *= Mathf.Abs(t.localScale.x);
			}
			Texel = scale / biggest.sprite.pixelsPerUnit;
			// From the sprite's own bounds: a renderer's world bounds are empty on a prefab that is not in a scene.
			Bounds b = biggest.sprite.bounds;
			Transform t2 = biggest.transform;
			Vector3 centre = root.InverseTransformPoint(t2.TransformPoint(b.center));
			Vector3 low = root.InverseTransformPoint(t2.TransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)));
			Foot = new Vector3(centre.x, low.y, centre.z);
			Lit = biggest.sharedMaterial;
			SortingLayer = biggest.sortingLayerID;
			SortingOrder = biggest.sortingOrder;
		}

		private static GameObject BuildPrefab(GameObject magnet)
		{
			holder = new GameObject("MiniMapGpsHolder");
			holder.SetActive(false);
			holder.hideFlags = HideFlags.HideAndDontSave;
			Object.DontDestroyOnLoad(holder);

			// Active itself, so the copy the game makes on the dash is active; the holder keeps this one from ever running.
			Prefab = new GameObject("GPS");
			Prefab.transform.SetParent(holder.transform, false);
			Prefab.transform.localPosition = magnet.transform.localPosition;
			Prefab.transform.localRotation = magnet.transform.localRotation;
			Prefab.transform.localScale = magnet.transform.localScale;
			Prefab.layer = magnet.layer;
			Prefab.AddComponent<GpsScreen>();
			return Prefab;
		}

		/// <summary>The shop icon's size in pixels: about the size of the other item icons in the inventory bar, with room for the casing.</summary>
		private const int IconWidth = 26;
		private const int IconHeight = 24;

		/// <summary>
		/// The shop icon, drawn at a size of its own (the magnet's is no guide: drawn to its size the GPS came out as a speck). Its pixel
		/// density is the one most of the catalog's icons share, so its pixels are the size of theirs on the card.
		/// </summary>
		private static Sprite Icon(ItemCatalog catalog)
		{
			int w = IconWidth;
			int h = IconHeight;
			float ppu = catalog.items.Where(i => i != null && i.image != null).GroupBy(i => i.image.pixelsPerUnit).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
			if (ppu <= 0f)
			{
				ppu = 100f;
			}
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			tex.name = "GpsIcon";
			tex.filterMode = FilterMode.Point;
			tex.wrapMode = TextureWrapMode.Clamp;
			tex.hideFlags = HideFlags.HideAndDontSave;
			tex.SetPixels32(GpsArt.Icon(w, h));
			tex.Apply(false, true);
			Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
			sprite.name = "GpsIcon";
			sprite.hideFlags = HideFlags.HideAndDontSave;
			Mod.Info("GPS: shop icon " + w + "x" + h + " at " + ppu + " px/unit");
			return sprite;
		}
	}
}
