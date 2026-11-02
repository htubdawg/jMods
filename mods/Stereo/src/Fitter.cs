using System;
using UnityEngine;

namespace HalfGripStereo
{
	/// <summary>
	/// Puts the stereo in the cockpit of every race. It is not a shop item: it takes no item slot, so it is simply part of the car. A
	/// persistent object looks twice a second for the player's cockpit (the object holding the steering wheel, NSteeringWheel, whose
	/// parent is CarInterior) and adds the stereo to it once; the stereo goes with the scene. Scenes whose TrackManager plays no race
	/// music (the main menu's background race, the credits) are left alone, and so is everything with Stereo.Enabled off.
	/// </summary>
	internal sealed class Fitter : MonoBehaviour
	{
		private float next;
		private bool warned;

		private void Update()
		{
			if (Time.unscaledTime < next)
			{
				return;
			}
			next = Time.unscaledTime + 0.5f;
			if (Deck.Active != null || !Mod.Enabled.Value)
			{
				return;
			}
			try
			{
				NSteeringWheel wheel = FindFirstObjectByType<NSteeringWheel>();
				if (wheel == null || wheel.transform.parent == null)
				{
					return;
				}
				TrackManager tm = FindFirstObjectByType<TrackManager>();
				if (tm != null && !tm.playRaceMusic)
				{
					return;
				}
				GameObject stereo = new GameObject("Stereo");
				stereo.transform.SetParent(wheel.transform.parent, false);
				stereo.AddComponent<Deck>();
				stereo.AddComponent<StereoDash>();
				Mod.Info("Stereo: put in the cockpit '" + wheel.transform.parent.name + "'");
			}
			catch (Exception e)
			{
				if (!warned)
				{
					warned = true;
					Mod.Log.LogWarning("Stereo: could not put the stereo in the cockpit: " + e);
				}
			}
		}
	}
}
