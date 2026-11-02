using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HalfGripTools;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HalfGripHelicopter
{
	/// <summary>
	/// The Helicopter fits alone, but for guns and the MiniMap mod's GPS (<see cref="Allowed"/>, the owner's choice after 0.1.9).
	/// Every way another item reaches the car is closed while it is fitted:
	/// <list type="bullet">
	/// <item>the garage's slots (<see cref="InventorySlot.OnDrop"/>): fitting the Helicopter puts everything else back in the hold, and
	/// nothing else can go in while it is there;</item>
	/// <item>the race's spawn (<see cref="LoadoutSpawner"/>'s Awake, the one place a race reads the loadout): whatever the save or
	/// the menu held, the car gets the Helicopter alone and its stats are worked out again from it alone;</item>
	/// <item>a live refit (<see cref="LoadoutRefit.Swap"/>, which the Open World's garages and the dev console use): an item is refused
	/// while the Helicopter is on, and fitting the Helicopter takes the rest off.</item>
	/// </list>
	/// </summary>
	internal static class Lock
	{
		/// <summary>The MiniMap mod's GPS (its saved item id): that one item only, not every item of its kind.</summary>
		private const string GpsId = "minimap.gps";

		/// <summary>
		/// What may go on beside the Helicopter: the Helicopter itself, any gun (its own category, fired from the cockpit's module slots
		/// and the car's exterior sockets, which the airframe leaves alone) and the GPS.
		/// </summary>
		internal static bool Allowed(UpgradeComponent u)
		{
			return u != null && (HeliItem.Is(u) || u.upgradeCategory == UpgradeCategory.GUN || u.id == GpsId);
		}

		/// <summary>The race's loadout cut down to what may go with the Helicopter, its stats from those alone. Called as the race spawns the loadout.</summary>
		internal static void Enforce()
		{
			if (!HeliItem.Fitted)
			{
				return;
			}
			List<UpgradeComponent> kept = PlayerLoadout.Equipped.Where(Allowed).ToList();
			if (kept.Count == PlayerLoadout.Equipped.Count)
			{
				return;
			}
			Mod.Info("Helicopter: fitted with " + (PlayerLoadout.Equipped.Count - kept.Count) + " other item(s), which stay off: " + string.Join(", ", PlayerLoadout.Equipped.Where(u => !Allowed(u)).Select(u => u != null ? u.id : "?").ToArray()));
			PlayerStats.ResetAllUpgrades();
			foreach (UpgradeComponent u in kept)
			{
				u.Apply();
			}
			PlayerLoadout.Set(kept);
		}

		/// <summary>The garage's draggable item the Helicopter sits as, in any slot, other than <paramref name="except"/>.</summary>
		internal static bool HeliInSlots(DraggableItem except)
		{
			foreach (InventorySlot slot in UnityEngine.Object.FindObjectsByType<InventorySlot>(FindObjectsSortMode.None))
			{
				foreach (DraggableItem d in slot.GetComponentsInChildren<DraggableItem>())
				{
					if (d != except && HeliItem.Is(d.Upgrade))
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>Puts every fitted item but the Helicopter, guns and the GPS back in the hold, the way a slot puts back the item a new one replaces.</summary>
		internal static void ClearOtherSlots(DraggableItem heli)
		{
			InventoryHold hold = UnityEngine.Object.FindAnyObjectByType<InventoryHold>();
			if (hold == null)
			{
				return;
			}
			int moved = 0;
			foreach (InventorySlot slot in UnityEngine.Object.FindObjectsByType<InventorySlot>(FindObjectsSortMode.None))
			{
				foreach (DraggableItem d in slot.GetComponentsInChildren<DraggableItem>())
				{
					if (d == heli || Allowed(d.Upgrade))
					{
						continue;
					}
					d.SnapToParent(hold.ItemsRoot);
					d.transform.SetAsLastSibling();
					d.IsEquipped = false;
					moved++;
				}
			}
			if (moved > 0)
			{
				Mod.Info("Helicopter: put " + moved + " item(s) back in the hold.");
			}
		}

		// ---- The Open World mod ----

		private static bool owLinked;

		/// <summary>
		/// The Open World's garages open when the hand lets go of the shifter in neutral (its internal <c>ShifterHand</c>), and its events
		/// sign on the same way. The Helicopter has no shifter in sight, so letting go of the collective at the bottom does it instead:
		/// <c>ReleasedInNeutral</c> answers for the collective and <c>Neutral</c> is true while the Helicopter flies. Patched by hand, by
		/// name, since each mod is compiled on its own; tried at each scene load until the Open World is found.
		/// </summary>
		internal static void LinkOpenWorld(ModContext ctx)
		{
			if (owLinked)
			{
				return;
			}
			try
			{
				Type hand = AppDomain.CurrentDomain.GetAssemblies().Select(a => SafeType(a, "HalfGripOpenWorld.ShifterHand")).FirstOrDefault(t => t != null);
				if (hand == null)
				{
					return;
				}
				owLinked = true;
				MethodInfo released = AccessTools.Method(hand, "ReleasedInNeutral");
				MethodInfo neutral = AccessTools.Method(hand, "Neutral");
				if (released == null || neutral == null)
				{
					Mod.Log.LogWarning("Helicopter: the Open World's ShifterHand has no ReleasedInNeutral/Neutral, so its garages will not open for the Helicopter.");
					return;
				}
				ctx.Harmony.Patch(released, postfix: new HarmonyMethod(typeof(Lock), nameof(ReleasedPostfix)));
				ctx.Harmony.Patch(neutral, postfix: new HarmonyMethod(typeof(Lock), nameof(NeutralPostfix)));
				Mod.Info("Helicopter: linked to the Open World's garages (the collective dropped and let go opens one).");
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: could not link to the Open World's garages: " + e.Message);
			}
		}

		private static Type SafeType(Assembly a, string name)
		{
			try
			{
				return a.GetType(name, false);
			}
			catch
			{
				return null;
			}
		}

		private static void ReleasedPostfix(ref bool __result)
		{
			if (HeliDriver.Active)
			{
				__result = Cockpit.TakeCollectiveDrop();
			}
		}

		private static void NeutralPostfix(ref bool __result)
		{
			if (HeliDriver.Active)
			{
				__result = true;
			}
		}
	}

	/// <summary>The garage: the Helicopter clears the other slots, and nothing but guns and the GPS goes in beside it.</summary>
	[HarmonyPatch(typeof(InventorySlot), nameof(InventorySlot.OnDrop))]
	internal static class SlotDrop
	{
		private static bool Prefix(InventorySlot __instance, PointerEventData eventData)
		{
			try
			{
				DraggableItem d = eventData != null && eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<DraggableItem>() : null;
				if (d == null || d.Upgrade == null || Lock.Allowed(d.Upgrade) || __instance.category != d.upgradeCategory)
				{
					return true;
				}
				if (!Lock.HeliInSlots(d))
				{
					return true;
				}
				d.SnapToParent(d.ReturnParent);
				MenuSounds.Play(MenuSound.PutDown);
				Mod.Info("Helicopter: refused '" + d.Upgrade.id + "' (only guns and the GPS go with the Helicopter).");
				return false;
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: the garage check failed, so the item went in as usual: " + e.Message);
				return true;
			}
		}

		private static void Postfix(InventorySlot __instance, PointerEventData eventData)
		{
			try
			{
				DraggableItem d = eventData != null && eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<DraggableItem>() : null;
				if (d != null && HeliItem.Is(d.Upgrade) && d.IsEquipped && d.transform.parent == __instance.transform)
				{
					Lock.ClearOtherSlots(d);
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: could not clear the other slots: " + e.Message);
			}
		}
	}

	/// <summary>The race: the car gets the Helicopter alone, before anything is spawned.</summary>
	[HarmonyPatch(typeof(LoadoutSpawner), "Awake")]
	internal static class SpawnAlone
	{
		private static void Prefix()
		{
			try
			{
				Lock.Enforce();
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: could not take the other items off: " + e.Message);
			}
		}
	}

	/// <summary>A live refit: nothing but guns and the GPS goes on with the Helicopter, and the Helicopter takes the rest off.</summary>
	[HarmonyPatch(typeof(LoadoutRefit), nameof(LoadoutRefit.Swap))]
	internal static class RefitAlone
	{
		private static void Prefix(UpgradeComponent off, ref UpgradeComponent on, ref IEnumerable<UpgradeComponent> loadout)
		{
			try
			{
				List<UpgradeComponent> list = loadout != null ? loadout.ToList() : new List<UpgradeComponent>();
				UpgradeComponent heli = list.FirstOrDefault(HeliItem.Is);
				if (heli == null)
				{
					return;
				}
				if (on != null && !Lock.Allowed(on))
				{
					Mod.Info("Helicopter: refit refused '" + on.id + "' (only guns and the GPS go with the Helicopter).");
					on = null;
				}
				loadout = list.Where(Lock.Allowed).ToList();
				if (on != null && HeliItem.Is(on))
				{
					// The Helicopter going on: take off what is already fitted.
					LoadoutSpawner spawner = Rig.First<LoadoutSpawner>();
					if (spawner != null)
					{
						foreach (UpgradeSpawn s in spawner.Fitted.ToList())
						{
							if (s.upgrade != null && !Lock.Allowed(s.upgrade))
							{
								spawner.Swap(s.upgrade, null);
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: the refit check failed: " + e.Message);
			}
		}
	}

	/// <summary>The car's driving: off while the Helicopter flies (the flight is <see cref="Flight"/>), the rest of CarLink keeps going.</summary>
	[HarmonyPatch(typeof(CarLink), "FixedUpdate")]
	internal static class NoDriving
	{
		private static bool Prefix(CarLink __instance)
		{
			if (!HeliDriver.Active || __instance != HeliDriver.Link)
			{
				return true;
			}
			try
			{
				HeliDriver.DriveCar(__instance);
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Helicopter: " + e.Message);
			}
			return false;
		}
	}

	/// <summary>A helicopter is always in the air: no airtime combo (the growing "AirTime" multiplier line on the screen) while flying.</summary>
	[HarmonyPatch(typeof(ComboManager), nameof(ComboManager.IncrementComboContinuous))]
	internal static class NoAirTime
	{
		private static bool Prefix(string name)
		{
			return !(name == "AirTime" && HeliDriver.Active);
		}
	}

	/// <summary>A helicopter is always in the air: no Big Air achievement for it.</summary>
	[HarmonyPatch(typeof(SteamAchievements), nameof(SteamAchievements.Unlock))]
	internal static class NoBigAir
	{
		private static bool Prefix(string id)
		{
			if (id == "ACH_BIG_AIR" && HeliDriver.Active)
			{
				Mod.Info("Helicopter: no Big Air achievement while flying.");
				return false;
			}
			return true;
		}
	}
}
