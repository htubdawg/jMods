using System;
using HarmonyLib;
using UnityEngine;

namespace HalfGripHelicopter
{
	/// <summary>
	/// The garage's car preview (the game's <see cref="LoadoutPreview"/>, in the garage scene that normal runs and the Multiplayer mod's
	/// garage both use) shows a chassis only by repainting the car with the chassis's skin, and the Helicopter has none, so it showed the
	/// plain car. After each of the preview's updates (it re-reads the slots every 0.25 s and on letting go of a dragged item, and keeps
	/// what it shows in <c>shownChassis</c>), an outside-only helicopter (<see cref="Airframe.BuildPreview"/>) is put on the preview car
	/// while the Helicopter is in the chassis slot, and taken off (the car shown again) when it is not.
	/// </summary>
	[HarmonyPatch(typeof(LoadoutPreview), "Update")]
	internal static class Preview
	{
		private static readonly AccessTools.FieldRef<LoadoutPreview, ChassisUpgrade> ShownChassis = AccessTools.FieldRefAccess<LoadoutPreview, ChassisUpgrade>("shownChassis");

		private static Airframe shown;
		private static LoadoutPreview shownOn;

		/// <summary>The preview a build failed on: not tried again on it, so a failure is logged once.</summary>
		private static LoadoutPreview failedOn;

		private static void Postfix(LoadoutPreview __instance)
		{
			try
			{
				bool want = HeliItem.Is(ShownChassis(__instance)) && __instance.skinController != null;
				if (shown != null && (!want || shownOn != __instance))
				{
					UnityEngine.Object.Destroy(shown.gameObject);
					shown = null;
				}
				if (want && shown == null && failedOn != __instance)
				{
					shownOn = __instance;
					shown = Airframe.BuildPreview(__instance.skinController, __instance.tires);
				}
			}
			catch (Exception e)
			{
				failedOn = __instance;
				shown = null;
				Mod.Log.LogWarning("Helicopter: could not show the helicopter on the garage's preview car: " + e.Message);
			}
		}
	}
}
