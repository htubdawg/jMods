using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfGripSlapPack
{
	/// <summary>Restricts every Slap Pack addition to a known loadout using the game's original chassis items.</summary>
	internal static class ChassisCompatibility
	{
		private static StockChassisPolicy stock;

		/// <summary>Called during mod loading, before HalfGripTools injects registered items into the catalog.</summary>
		internal static void Initialize(Action<string> warning)
		{
			var ids = new List<string>();
			ItemCatalog catalog = ItemCatalog.Instance;
			if (catalog && catalog.items != null)
				foreach (UpgradeComponent item in catalog.items)
					if (item is ChassisUpgrade && (item.hideFlags & HideFlags.DontSave) == 0)
						ids.Add(item.id);
			stock = new StockChassisPolicy(ids);
			if (!stock.Ready) warning("No original chassis items were available; Slap Pack additions will remain disabled.");
		}

		/// <summary>Checks every equipped chassis, including custom items which reuse stock cabin objects or skins.</summary>
		/// <remarks>
		/// An unknown loadout (the tutorial, or any scene loaded without the race menu setting a loadout) is
		/// the stock car: the game then fits no chassis item (<c>LoadoutSpawner</c>, <c>CarSkinController</c>), so additions are allowed.
		/// </remarks>
		internal static bool AllowsAdditions
		{
			get
			{
				if (stock == null || !stock.Ready) return false;
				if (!PlayerLoadout.Known) return true;
				foreach (UpgradeComponent item in PlayerLoadout.Equipped)
					if (item && (item is ChassisUpgrade || item.upgradeCategory == UpgradeCategory.CHASSIS) && !stock.Includes(item.id))
						return false;
				return true;
			}
		}
	}
}
